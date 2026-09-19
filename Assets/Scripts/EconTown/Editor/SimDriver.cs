#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// 编辑器侧 365 WorldDay 全自动长期流程验证：
    /// 模拟玩家决策(变价/生产/采购/雇人)→ 营业 → 结算 → 次日，并验证长线数值与事件。
    /// 通过 execute_code 调用 SimDriver.Start() 启动。
    /// </summary>
    public static class SimDriver
    {
        const int TargetWorldDays = 365;
        const int ChapterRunthroughDayLimit = 150;
        public static EditorApplication.CallbackFunction active;
        public static volatile bool finished;
        public static string log = "";
        static int validatedDays;
        static int startHistoryCount;
        static int startWorldDay;
        static bool prepared;
        static bool chapterMode;
        static string saveDir;

        public static string Start()
        {
            return StartInternal(false);
        }

        /// <summary>
        /// 章节全程模式：从第 1 章走真实的 OnDaySettled→Evaluate→CompleteChapter→SaveKeyMoment
        /// 管线直到主线完成。365 天长跑一开始就强制 completedChapter=30，章节推进链路零自动化
        /// 覆盖——V0.5.3 审查中的 pendingStageReport 覆盖、Toast 重复、checkpoint 语义等 BUG
        /// 全部活在这段盲区里。此模式补上：每日注入当前章节所需的真实行为信号，验证 30 章推进
        /// 的自洽性与存档 round-trip。
        /// </summary>
        public static string StartChapterRunthrough()
        {
            return StartInternal(true);
        }

        static string StartInternal(bool chapters)
        {
            if (active != null) { EditorApplication.update -= active; active = null; }
            // 365 天模拟经 GameBootstrap.TryNextDay 每日写盘:把存档目录切到临时目录,
            // 玩家真实存档零接触;Stop 时恢复并清理(P1-5)。编辑器中途崩溃的残留
            // 由 SaveSystem 的 SubsystemRegistration 复位兜底。
            saveDir = Path.Combine(Path.GetTempPath(), "EconTownSimDriver_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDir);
            SaveSystem.OverrideSaveDirectory(saveDir);
            finished = false;
            log = "running";
            validatedDays = 0;
            startHistoryCount = 0;
            startWorldDay = 0;
            prepared = false;
            chapterMode = chapters;
            active = Step;
            EditorApplication.update += active;
            return "sim started, watch console for [SimDriver]";
        }

        static void Step()
        {
            try
            {
                if (!EditorApplication.isPlaying) { Stop("play mode exited"); return; }
                GameState st = GameState.I;
                if (st == null) { Stop("no GameState"); return; }
                if (!prepared) { if (chapterMode) PrepareChapterScenario(st); else PrepareLongTermScenario(st); }
                UIManager ui = UIManager.I;

                if (st.phase == GamePhase.Morning)
                {
                    if (st.government.currentEvent != null && !st.government.currentEvent.resolved)
                        Require(GovernmentSimulator.ResolveEvent(st, 1), "government event could not be resolved");
                    if (chapterMode) InjectChapterBehavior(st);

                    st.price = 10f + (st.day % 8);
                    if (st.CanProduction) st.planProduction = 110;
                    if (st.CanMaterials && st.materials < 130)
                    {
                        float unit = DaySimulator.MaterialUnitPrice(st);
                        float cost = 100 * unit;
                        if (st.money > cost) { st.money -= cost; st.materials += 100; }
                    }
                    if (st.CanEmployees && st.day >= 6 && st.employees < 3) st.employees++;
                    if (st.CompetitionActive)
                    {
                        if (st.CanQuality && st.playerQualityLevel < 2 && st.money >= CompetitionEco.UpgradeCost(2))
                        {
                            float upgrade = CompetitionEco.UpgradeCost(2);
                            st.money -= upgrade; st.todayStrategicSpend += upgrade; st.playerQualityLevel = 2;
                        }
                        if (st.CanMarketing) st.marketingBudget = st.day % 3 == 0 ? 300f : 100f;
                        if (st.priceWarActive)
                        {
                            st.price = st.competitor.price;
                            st.todayStrategyChoice = PlayerCompetitionChoice.FollowPrice.ToString();
                        }
                    }
                    st.Changed();
                    if (ui != null) ui.CloseAll();
                    Debug.Log("[SimDriver] morning day=" + st.day + " money=" + st.money.ToString("0") +
                              " materials=" + st.materials + " plan=" + st.planProduction +
                              " emp=" + st.employees + " cap=" + Eco.Capacity(st.employees) +
                              " prod=" + DaySimulator.Production(st) + " inv=" + st.inventory);
                    BusinessDirector.I.Begin();
                }
                else if (st.phase == GamePhase.Business)
                {
                    BusinessDirector.I.SkipToEnd();
                }
                else if (st.phase == GamePhase.Report)
                {
                    int settledDay = st.day;
                    ValidateEnterpriseRecord(st, settledDay);

                    // 财务危机走 V0.5.2 的多日恢复路径，不用旧的瞬间解除。
                    if (st.economy.financialCrisisActive && !st.experience.enterpriseRecovery.active)
                    {
                        st.employees = 1;
                        st.planProduction = Mathf.Min(st.planProduction, CostService.Capacity(st).actualCapacity);
                        st.marketingBudget = 0f;
                        st.todayStrategicSpend = 0f;
                        LongTermExperienceService.BeginEnterpriseRecovery(st, "tighten");
                    }

                    float governmentCashBefore = st.government.cash;
                    float governmentDebtBefore = st.government.debt;
                    if (ui != null) ui.CloseAll();
                    Require(GameBootstrap.I.TryNextDay(), "world settlement was blocked on day " + settledDay);
                    ValidateWorldSettlement(st, settledDay, governmentCashBefore, governmentDebtBefore);
                    validatedDays++;
                    ValidateLongTermBounds(st, settledDay);
                    if (chapterMode) ValidateTradeEconomyBounds(st, settledDay);
                    if (chapterMode)
                    {
                        ChapterDefinition current = ProgressionService.Current(st);
                        Require(st.progression.completedChapter <= st.progression.currentChapter - 1 ||
                                st.progression.mainlineComplete,
                            "completed chapter " + st.progression.completedChapter +
                            " outran current chapter " + st.progression.currentChapter);
                        Require(st.progression.pendingStageReports.Count <= st.progression.stageReports.Count,
                            "pending stage report queue is longer than the report archive");
                        if (st.progression.mainlineComplete)
                        {
                            ValidateChapterCompletion(st);
                            Stop("DONE chapters=" + ProgressionService.Chapters.Count +
                                 " validated=" + validatedDays + " history=" + st.history.Count +
                                 " stageReports=" + st.progression.stageReports.Count +
                                 " sandboxUnlocked=" + st.sandbox.unlocked +
                                 " fx=" + ExchangeRateService.Rate(st).ToString("0.00") +
                                 " reserves=" + st.centralBank.forexReservesBCurrency.ToString("0"));
                            return;
                        }
                        if (validatedDays >= ChapterRunthroughDayLimit)
                            Stop("TIMEOUT chapter=" + st.progression.currentChapter +
                                 " (signal=" + (current != null ? current.requiredSignal : "null") + ")");
                    }
                    else if (validatedDays >= TargetWorldDays)
                    {
                        Require(st.history.Count >= startHistoryCount + TargetWorldDays,
                            "enterprise history did not retain all 365 settled days");
                        Require(HasLateLongTermEvent(st), "no loop event was recorded after the first 100 simulated days");
                        Require(st.unlockedCards.Count > 0 && st.unlockedCards.Count <= CardLibrary.All.Count,
                            "knowledge-card state is empty or exceeds the dynamic library size");
                        Stop("DONE startDay=" + startWorldDay + " nextDay=" + st.day + " history=" + st.history.Count +
                             " validated=" + validatedDays + " events=" + st.experience.totalLongTermEvents +
                             " cards=" + st.unlockedCards.Count + "/" + CardLibrary.All.Count + " money=" + st.money.ToString("0") +
                             " totalProfit=" + st.TotalProfit.ToString("0"));
                    }
                }
            }
            catch (System.Exception e)
            {
                Stop("EXCEPTION: " + e.Message + " @ " + e.StackTrace);
            }
        }

        /// <summary>章节全程模式的准备：不强制完成主线，保留真实 progression 从第 1 章推进。</summary>
        static void PrepareChapterScenario(GameState state)
        {
            ProgressionService.Ensure(state);
            LongTermExperienceService.Ensure(state);
            Require(!state.progression.mainlineComplete, "chapter runthrough needs a fresh (non-completed) save");
            state.experience.settings.difficulty = DifficultyMode.Teaching;
            state.experience.settings.eventFrequency = .25f;
            state.money = Mathf.Max(state.money, 8000f);
            state.government.cash = Mathf.Max(state.government.cash, 8000f);
            state.materials = Mathf.Max(state.materials, 600);
            startWorldDay = state.day;
            startHistoryCount = state.history.Count;
            prepared = true;
        }

        /// <summary>
        /// 模拟当前章节所需的玩家行为。企业/市场篇的信号由每日经营自然覆盖（变价/雇人/品质/
        /// 广告/跟价已在 Morning 决策里）；需要 UI 交互或一次性动作的章节注入等价行为。
        /// </summary>
        static void InjectChapterBehavior(GameState st)
        {
            ChapterDefinition current = ProgressionService.Current(st);
            if (current == null) return;
            switch (current.requiredSignal)
            {
                case ProgressSignals.ResidentProfile:
                    if (!st.resident.profileCreated)
                        ResidentEconomy.CreateProfile(st, "主线居民", ResidentProfession.StoreClerk, ResidentPreference.Saver);
                    return;
                case ProgressSignals.MarketExperiment:
                    st.marketExperimentMask = 1;
                    return;
                case ProgressSignals.GovernmentOpened:
                    st.government.freeRiderExperimentDone = true;
                    return;
                // —— V0.7 第七篇注入(行为等价优先,面板类信号才直接记录) ——
                case ProgressSignals.PortOpened:
                    st.exportChannelEnabled = true;
                    // 出口吃的是产能余量,须同步调低当日计划(等价玩家决策)。
                    {
                        int capNow = CostService.Capacity(st).actualCapacity;
                        if (st.planProduction > capNow - 45)
                            st.planProduction = Mathf.Max(0, capNow - 45);
                        st.Changed();
                    }
                    return;   // 首单出货由收日管线自然产生并补信号
                case ProgressSignals.ComparativeAdvantageExperimentDone:
                case ProgressSignals.TrilemmaDemoDone:
                case ProgressSignals.ExchangeRateObserved:
                    ProgressionService.RecordSignal(st, current.requiredSignal);
                    return;
                case ProgressSignals.ImportConsumed:
                    InternationalTradeService.Ensure(st);
                    st.resident.cash += 50f;   // 保证购买可行(行为注入不造假数据)
                    var bought = ResidentEconomy.Consume(st, ResidentItemKind.ImportedSnack);
                    if (!bought.success) ProgressionService.RecordSignal(st, current.requiredSignal);
                    return;
                case ProgressSignals.TariffEnacted:
                    GovernmentSimulator.SetPolicy(st, PolicyType.ImportTariff, 5f);
                    return;
                case ProgressSignals.DepreciationExperienced:
                {
                    ExchangeRateState fx = st.worldTrade.exchangeRate;
                    float bumped = Mathf.Clamp(fx.equilibriumRate * 1.06f,
                        ExchangeRateService.MinRate, ExchangeRateService.MaxRate);
                    fx.equilibriumRate = bumped;
                    fx.rate = bumped;          // 教学剧本式外生贬值(PRD §72 章节演出等价物)
                    st.Changed();
                    return;
                }
                case ProgressSignals.CapitalFlowObserved:
                    // 阈值是单期 |KA|≥60 B币 ⇒ 需 carry≥1.34pp;注入 +2pp 保证当期越过。
                    st.centralBank.policyRate = st.worldTrade.countryB.policyRate +
                        st.worldTrade.capitalFlow.riskPremiumPercent + 2f;
                    InterestRateService.Ensure(st);
                    return;   // 高利差使下一收日 KA 超阈值,CaptureTradeSignals 自然记录
                case ProgressSignals.TradeWarResponseChosen:
                    ProgressionService.RecordSignal(st, current.requiredSignal,
                        "贸易战回应", "在 B 国关税升级面前做出了回应策略(自动推演)");
                    return;
                case ProgressSignals.ForeignRecessionFelt:
                    // 与测试同款:B 国衰退注入须压住趋势与动量,否则当夜即被动态回滚。
                    st.worldTrade.countryB.recessionActive = true;
                    st.worldTrade.countryB.gdpGrowthPeriod = -.4f;
                    st.worldTrade.countryB.growthMomentum = -.95f;
                    return;
                case ProgressSignals.ReserveDefenseTried:
                    if (!ExchangeRateService.TrySetRegime(st, ExchangeRegime.Fixed, out _))
                        ProgressionService.RecordSignal(st, current.requiredSignal);
                    else
                    {
                        ExchangeRateState peg = st.worldTrade.exchangeRate;
                        peg.equilibriumRate = Mathf.Clamp(peg.pegTarget * 1.12f,
                            ExchangeRateService.MinRate, ExchangeRateService.MaxRate);
                        st.Changed();   // 带外缺口将触发守带干预并在账本行留下负的官方干预
                    }
                    return;

                // —— V0.9.5 第八篇注入(行为等价优先;镜像补足时 householdDeposits 与 reserves 同加保平衡) ——
                case ProgressSignals.HousingRentPaid:
                    if (st.resident.savings < 200f)
                    {
                        float add = 500f - st.resident.savings;
                        st.resident.savings += add;
                        st.bank.reserves += add;
                        st.bank.householdDeposits += add;
                    }
                    return;   // 租金由收日管线自然产生(租房态默认)
                case ProgressSignals.HousingMortgageIssued:
                {
                    st.resident.savings = 40000f;
                    st.bank.reserves += 40000f - st.bank.householdDeposits;
                    st.bank.householdDeposits = 40000f;
                    st.resident.dailyIncome = 600f;
                    st.resident.todayIncome = 800f;
                    string msg;
                    if (!MortgageService.TryBuyHouse(st, out msg))
                        ProgressionService.RecordSignal(st, current.requiredSignal);
                    return;
                }
                case ProgressSignals.HousingPriceBreakdown:
                    HousingService.MarkPriceBreakdownRead(st);   // 面板读数等价
                    return;
                case ProgressSignals.HousingDrawdownSurvived:
                    // 真实路径=泡沫反转后 bp 尚在 40 以上时的深回撤(V093 反转测试/V095 #6 背书);
                    // 驱动器里 BubblePressure 每期重算,单期摆相位扛不过 UpdatePhase,故记录等价信号。
                    ProgressionService.RecordSignal(st, current.requiredSignal,
                        "扛过回撤", "房价自峰值回撤 ≥8% 期间月供未断(自动推演)");
                    return;
                case ProgressSignals.HousingAssetsDiversified:
                {
                    string msg;
                    LoanRecord mortgage = HousingService.CurrentMortgage(st);
                    if (mortgage != null)
                    {
                        float need = mortgage.remainingPrincipal + 20000f - st.resident.savings;
                        if (need > 0f)
                        {
                            st.resident.savings += need;
                            st.bank.householdDeposits += need;
                            st.bank.reserves += need;
                        }
                        MortgageService.TryEarlyPayoff(st, out msg);
                    }
                    st.resident.cash += 100f;
                    CreditService.DepositResidentCash(st, 100f, out msg);
                    AssetAllocationService.TryBuyTreasury(st, 2000f, out msg);
                    AssetAllocationService.TryBuyFund(st, 5000f, out msg);
                    return;
                }

                // 面板浏览类:V0.5/V0.6 既有信号——UI 驱动,注入等价记录。
                case ProgressSignals.ScarcityExperienced:
                case ProgressSignals.OpportunityCost:
                case ProgressSignals.DiminishingUtility:
                case ProgressSignals.BudgetChoice:
                case ProgressSignals.LaborLeisure:
                case ProgressSignals.ExternalityPolicy:
                case ProgressSignals.TaxPolicy:
                case ProgressSignals.MinimumWage:
                case ProgressSignals.PriceControl:
                case ProgressSignals.MacroOpened:
                case ProgressSignals.RealNominalObserved:
                case ProgressSignals.InflationObserved:
                case ProgressSignals.UnemploymentObserved:
                case ProgressSignals.RecessionObserved:
                case ProgressSignals.FiscalResponse:
                case ProgressSignals.OutputGapObserved:
                case ProgressSignals.ProductivityPolicy:
                case ProgressSignals.CompetitorOpened:
                case ProgressSignals.FiscalCycle:
                case ProgressSignals.MoneyFunctionsExperienced:
                case ProgressSignals.BankDepositMade:
                case ProgressSignals.BankBalanceViewed:
                case ProgressSignals.CreditCreated:
                case ProgressSignals.CompanyInvestmentFinanced:
                case ProgressSignals.InterestChoiceObserved:
                case ProgressSignals.CentralBankOpened:
                case ProgressSignals.PolicyRateCut:
                case ProgressSignals.PolicyRateRaised:
                case ProgressSignals.PolicyLagObserved:
                case ProgressSignals.BankRunResolved:
                case ProgressSignals.PolicyMixObserved:
                    ProgressionService.RecordSignal(st, current.requiredSignal);
                    return;
            }
        }

        /// <summary>主线完成时的终验：解锁传播、八篇报告、pending 队列消费、存档 round-trip。</summary>
        static void ValidateChapterCompletion(GameState st)
        {
            Require(st.progression.completedChapter == ProgressionService.Chapters.Count,
                "mainline complete but completedChapter=" + st.progression.completedChapter);
            Require(st.sandbox.unlocked, "sandbox.unlocked was not propagated on mainline completion");
            Require(st.progression.stageReports.Count == 8,
                "expected 8 stage reports, got " + st.progression.stageReports.Count);
            while (ProgressionService.GetPendingStageReport(st) != null)
                ProgressionService.MarkPendingStageReportViewed(st);
            Require(st.progression.pendingStageReports.Count == 0, "pending queue not fully consumable");
            SaveSystem.Save(st);
            Require(SaveSystem.Load(st), "chapter runthrough save round-trip failed");
            // 修复轮回I:硬编码 42 与首条动态断言(Chapters.Count)自相矛盾——主线跑通必在此误报
            // 「mainline progress lost」(V0.7 首条改动态时漏改此条);改同一动态口径。
            Require(st.progression.mainlineComplete && st.progression.completedChapter == ProgressionService.Chapters.Count,
                "mainline progress lost after save round-trip");
        }

        static void PrepareLongTermScenario(GameState state)
        {
            ProgressionService.Ensure(state);
            LongTermExperienceService.Ensure(state);
            InternationalTradeService.Ensure(state);
            // 主线口径派生化:V0.7 起章节数为动态值,任何硬编码都会在下一版本变成隐性重开。
            state.progression.completedChapter = ProgressionService.Chapters.Count;
            state.progression.currentChapter = ProgressionService.Chapters.Count + 1;
            state.progression.mainlineComplete = true;
            state.progression.sandboxUnlocked = true;
            state.progression.pendingStageReports.Clear();
            state.exportChannelEnabled = true;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            state.competitionIntroduced = true;
            state.economy.random.Reset(20260825);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            state.experience.settings.eventFrequency = 2f;
            state.money = Mathf.Max(state.money, 250000f);
            state.government.cash = Mathf.Max(state.government.cash, 250000f);
            state.materials = Mathf.Max(state.materials, 1000);
            if (!state.resident.profileCreated)
                ResidentEconomy.CreateProfile(state, "长期模拟居民", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            startWorldDay = state.day;
            startHistoryCount = state.history.Count;
            prepared = true;
        }

        static void ValidateEnterpriseRecord(GameState state, int expectedDay)
        {
            DayRecord record = state.LastRecord;
            Require(record != null, "missing enterprise record for day " + expectedDay);
            Require(record.day == expectedDay,
                "enterprise record day " + record.day + " != WorldDay " + expectedDay);
            Require(record.actualSales >= 0, "negative sales on day " + expectedDay);
            Require(record.actualSales <= record.availableSupply,
                "sales exceed available supply on day " + expectedDay);
            Require(record.actualSales <= record.desiredDemand,
                "sales exceed desired demand on day " + expectedDay);
            Require(record.sold == record.actualSales,
                "sold and actualSales diverge on day " + expectedDay);
            Require(record.demandStudents + record.demandWhitecollars + record.demandTourists == record.desiredDemand,
                "consumer-group demand does not sum to desired demand on day " + expectedDay);
            Require(record.demandEffects != null,
                "missing settled demand effect breakdown on day " + expectedDay);
            Require(record.demandEffects.IsBalanced,
                "demand effect breakdown does not add up on day " + expectedDay +
                ": modeled=" + record.demandEffects.ModeledTotal +
                " desired=" + record.demandEffects.desiredDemand);
            Require(record.demandEffects.desiredDemand == record.desiredDemand,
                "effect breakdown demand differs from settled demand on day " + expectedDay);

            if (record.competitionActive && record.actualSales + record.competitorSold > 0)
            {
                Require(Approximately(record.playerMarketShare + record.competitorMarketShare, 1f, .001f),
                    "market shares do not sum to 1 on day " + expectedDay);
            }
        }

        static void ValidateWorldSettlement(GameState state, int settledDay,
            float governmentCashBefore, float governmentDebtBefore)
        {
            Require(state.day == settledDay + 1,
                "WorldDay did not advance exactly once after day " + settledDay);
            Require(state.government.cycle == state.day && state.macro.period == state.day,
                "derived clocks are not synchronized after day " + settledDay);
            Require(Approximately(state.macro.businessConfidence,
                    state.government.enterpriseConfidence, .001f),
                "macro business confidence overwrote the government authority after day " + settledDay);

            Require(state.government.history.Count > 0, "missing government ledger for day " + settledDay);
            GovernmentCycleRecord government = state.government.history[state.government.history.Count - 1];
            Require(government.cycle == settledDay,
                "government ledger day " + government.cycle + " != settled day " + settledDay);
            Require(Approximately(government.taxRevenue - government.spending, government.budgetBalance, .01f),
                "government revenue - spending != budget balance on day " + settledDay);

            float cashChange = state.government.cash - governmentCashBefore;
            float financing = state.government.debt - governmentDebtBefore;
            Require(Approximately(cashChange, government.budgetBalance + financing, .01f),
                "government cash change != balance + financing on day " + settledDay);

            Require(state.macro.history.Count > 0, "missing macro ledger for day " + settledDay);
            MacroPeriodRecord macro = state.macro.history[state.macro.history.Count - 1];
            Require(macro.period == settledDay,
                "macro ledger day " + macro.period + " != settled day " + settledDay);
            float identityGdp = macro.consumption + macro.investment + macro.governmentSpending +
                                macro.exports - macro.imports;
            Require(Approximately(macro.nominalGdp, identityGdp, .01f),
                "GDP identity failed on day " + settledDay);

            Require(state.LastRecord != null && state.LastRecord.day == settledDay,
                "enterprise ledger lost settled day " + settledDay);
            if (state.resident.profileCreated)
            {
                Require(state.resident.history.Count > 0 &&
                        state.resident.history[state.resident.history.Count - 1].day == settledDay,
                    "resident ledger is not synchronized on day " + settledDay);
            }
        }

        static void ValidateLongTermBounds(GameState state, int settledDay)
        {
            Require(float.IsFinite(state.price) && state.price >= 1f && state.price <= 1000f,
                "invalid price after day " + settledDay);
            Require(float.IsFinite(state.money), "invalid enterprise cash after day " + settledDay);
            Require(float.IsFinite(state.macro.realGdp) && state.macro.realGdp >= 1f && state.macro.realGdp <= 100000000f,
                "invalid real GDP after day " + settledDay);
            Require(float.IsFinite(state.macro.inflation), "invalid inflation after day " + settledDay);
            Require(float.IsFinite(state.macro.unemploymentRate) && state.macro.unemploymentRate >= 0f &&
                state.macro.unemploymentRate <= 100f, "invalid unemployment after day " + settledDay);
            Require(float.IsFinite(state.government.debt) && state.government.debt >= 0f &&
                state.government.debt <= 100000000f, "invalid government debt after day " + settledDay);
            Require(state.inventory >= 0 && state.materials >= 0, "negative inventory after day " + settledDay);
            Require(state.government.population >= 1, "non-positive population after day " + settledDay);
            Require(state.playerBrand >= 0f && state.playerBrand <= 100f, "brand out of range after day " + settledDay);
        }

        /// <summary>
        /// V0.7 世界经济稳定性护栏(PRD §103):汇率有限有界、X/M 非负、NX 恒等、
        /// 外储不凭空增减(ExchangeRateService.AssertNoFreeMoney 也在银行收口处兜底)。
        /// </summary>
        static void ValidateTradeEconomyBounds(GameState st, int settledDay)
        {
            float rate = ExchangeRateService.Rate(st);
            Require(float.IsFinite(rate) && rate >= ExchangeRateService.MinRate - .0001f &&
                    rate <= ExchangeRateService.MaxRate + .0001f,
                "invalid exchange rate " + rate + " after day " + settledDay);
            float equilibrium = st.worldTrade.exchangeRate.equilibriumRate;
            Require(float.IsFinite(equilibrium), "non-finite equilibrium rate after day " + settledDay);

            TradeDayRecord row = InternationalTradeService.LastRecord(st);
            if (row == null || row.worldDay != settledDay) return;
            Require(row.exportsTotal >= 0f && row.importsTotal >= 0f,
                "negative trade flow after day " + settledDay +
                " X=" + row.exportsTotal + " M=" + row.importsTotal);
            Require(Approximately(row.netExports, row.exportsTotal - row.importsTotal, .01f),
                "NX identity broken after day " + settledDay);
            // 外储守恒双保险:账本行 Σ 干预 + 基线必须等于当前储备(逐日校验而非只靠终态)。
            // 围猎三(#8):对账口径与 AssertNoFreeMoney 一致——含 730 行截断的累计补偿。
            const float baseline = 800f;
            float ledgerSum = st.worldTrade.trimmedReserveChangeBCurrency;
            for (int i = 0; i < st.worldTrade.history.Count; i++)
                ledgerSum += st.worldTrade.history[i].reserveChangeBCurrency;
            Require(Mathf.Abs(st.centralBank.forexReservesBCurrency - (baseline + ledgerSum)) <= .10f,
                "forex reserves diverge from ledger after day " + settledDay +
                " reserves=" + st.centralBank.forexReservesBCurrency.ToString("0.00") +
                " ledger=" + ledgerSum.ToString("0.00"));
            Require(row.tariffRevenue >= -0.01f && row.tariffRateApplied >= 0f,
                "invalid tariff fields after day " + settledDay);
        }

        static bool HasLateLongTermEvent(GameState state)
        {
            if (state.experience == null || state.experience.eventHistory == null) return false;
            int threshold = startWorldDay + 100;
            for (int i = 0; i < state.experience.eventHistory.Count; i++)
                if (state.experience.eventHistory[i].worldDay >= threshold) return true;
            return false;
        }

        static bool Approximately(float actual, float expected, float tolerance)
        {
            return Mathf.Abs(actual - expected) <= tolerance;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }

        static void Stop(string msg)
        {
            log = msg;
            finished = true;
            if (active != null) { EditorApplication.update -= active; active = null; }
            SaveSystem.OverrideSaveDirectory(null);
            if (!string.IsNullOrEmpty(saveDir) && Directory.Exists(saveDir))
            {
                try { Directory.Delete(saveDir, true); }
                catch (System.Exception) { }
                saveDir = null;
            }
            Debug.Log("[SimDriver] " + msg);
        }
    }
}
#endif
