#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>暴露 protected 面板操作给 EditMode(V09Bugfix/P0Fixes harness 同款手法,另起名避重)。</summary>
    public class V098UiHarness : UIManager
    {
        public GameObject OpenPanel(string title, float w, float h) { return BeginPanel(title, w, h); }
        public void CloseTopEx() { CloseTop(); }
    }

    /// <summary>围猎三(V0.9.8 第三轮全库 bug 围猎,14 维度 69 代理对抗校验)确认项的回归测试:
    /// P0×2(第51章贸易战死锁/升值守汇记账符号)与 P1×12(在途房款权益腿/无主债息 coverage/
    /// HouseholdIncome 口径/紧缩 G/冲击均值回归/双重汇率楔子/读档重放回滚/应急取款双边/
    /// 分叉竞争分流/todayStrategicSpend 对称/顾客排队点/面板栈模态化)逐条钉住。</summary>
    public class EconTownV098BugHuntTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;
        string tempDir;

        GameState NewWorld()
        {
            GameObject go = new GameObject("V098HuntWorld" + worlds.Count);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260902);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "围猎镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            state = NewWorld();
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV098HuntTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        // ————— P0 #2:第51章「贸易战」循环依赖死锁 —————

        [Test]
        public void Chapter51_ScriptedWarBanner_BreaksDeadlock()
        {
            state.progression.currentChapter = 51;
            state.progression.completedChapter = 50;
            state.progression.mainlineComplete = false;
            LongTermExperienceService.OnWorldDayStarted(state);
            Assert.IsTrue(state.worldTrade.pendingTradeWarChoice,
                "主线推进到第51章的清晨必须亮出战争横幅(剧情固定置位),不再依赖 mainlineComplete 旧门。");
            float tariff = state.worldTrade.countryB.tariffOnAExports;
            Assert.Greater(tariff, 0f, "关税须照 trade.warcho 同款入账(至少一次 +8%)。");
            LongTermExperienceService.OnWorldDayStarted(state);
            Assert.AreEqual(tariff, state.worldTrade.countryB.tariffOnAExports, .001f,
                "横幅已在时不得重复加关税(幂等)。");
            ProgressionService.RecordSignal(state, ProgressSignals.TradeWarResponseChosen, "围猎回归", null);
            ProgressionService.OnDaySettled(state);
            Assert.GreaterOrEqual(state.progression.completedChapter, 51,
                "做出回应选择后第51章必须可完成——三环死锁解除。");
        }

        // ————— P0 #1:升值方向守汇的储备记账符号 —————

        [Test]
        public void PegAppreciation_ReserveLedgerSign_MatchesIntervention()
        {
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string on), on);
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            Assert.IsTrue(ExchangeRateService.TrySetPeg(state, 8f, out string pegMsg), pegMsg);
            fx.rate = 7.2f;
            fx.equilibriumRate = 7.2f;      // 市价强于锚 10% ⇒ 带外,升值方向守汇
            WorldTradeState trade = state.worldTrade;
            trade.history.Add(new TradeDayRecord
            {
                worldDay = state.day,
                exportsTotal = 1600f,
                importsTotal = 500f,
                exchangeRateUsed = 7.2f
            });
            trade.history[trade.history.Count - 1].currentAccountBCurrency = (1600f - 500f) / 7.2f;
            InternationalTradeService.CloseTradeDay(state);
            TradeDayRecord last = trade.history[trade.history.Count - 1];
            Assert.Greater(last.officialInterventionBCurrency, 0f, "升值守汇=央行净买入外储,应为正。");
            Assert.AreEqual(last.officialInterventionBCurrency, last.reserveChangeBCurrency, .01f,
                "账本行 reserveChange 必须等于官方干预量(外储守恒对账不变量,旧硬编码 -actualB 把升值方向记反)。");
            Assert.AreEqual(800f + last.officialInterventionBCurrency,
                state.centralBank.forexReservesBCurrency, .10f, "储备增量与账本行一致。");
        }

        // ————— P1 #2:在途房款到账的权益短腿 —————

        [Test]
        public void PendingSaleProceeds_ShortTownPool_EquityLegKeepsBalanceGap()
        {
            float gap0 = state.bank.BalanceGap;
            Assert.AreEqual(0f, gap0, .01f);
            float equity0 = state.bank.equity;
            float savings0 = state.resident.savings;
            float household0 = state.bank.householdDeposits;
            state.housing.pendingSaleProceeds = 9240f;
            state.bank.townSectorDeposits = 100f;   // 池子已被镇买方按揭月供抽干
            state.bank.reserves += 100f;             // 测试注入的镇池负债须有资产背书,否则凭空漂 -100
            HousingService.SettlePendingSaleProceeds(state);
            Assert.AreEqual(savings0 + 9240f, state.resident.savings, .01f, "居民储蓄按全额入账。");
            Assert.AreEqual(household0 + 9240f, state.bank.householdDeposits, .01f);
            Assert.AreEqual(0f, state.bank.townSectorDeposits, .01f, "镇池钳位扣到 0。");
            Assert.AreEqual(equity0 - 9140f, state.bank.equity, .01f,
                "钳位差额 9240-100 必须由权益腿吸收。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f, "ΔA−ΔL−ΔE≡0(旧实现此处漂移 -9140)。");
        }

        // ————— P1 #3:认购不足的无主政府债务利息 —————

        [Test]
        public void GovernmentBondInterest_UnderSubscription_PaysOnlyOwnedShare()
        {
            state.bank.reserves = 1000f;
            state.bank.householdDeposits = 0f;
            float purchased = CreditService.PurchaseGovernmentBonds(state, 5000f, .04f);
            Assert.AreEqual(1000f, purchased, .01f, "认购额应被钳在超额准备金(1000/5000)。");
            float equity0 = state.bank.equity;
            float reserves0 = state.bank.reserves;
            CreditService.ReceiveGovernmentBondInterest(state, 100f, 5000f);
            Assert.AreEqual(20f, state.bank.equity - equity0, .01f,
                "银行只应收持券 20% 对应的利息——无主债务(4000)的利息不得凭空进权益。");
            Assert.AreEqual(20f, state.bank.reserves - reserves0, .01f);
        }

        // ————— P1 #4:HouseholdIncome 清晨口径 —————

        [Test]
        public void HouseholdIncome_MorningReads_TrailingThreeDayAverage()
        {
            for (int d = 1; d <= 3; d++)
            {
                state.history.Add(new DayRecord { day = d, profit = 600f });
                state.resident.history.Add(new ResidentDayRecord { day = d, income = 60f });
            }
            state.day = 4;
            state.resident.lifeDay = 4;
            state.resident.todayIncome = 0f;   // 收日链末尾已清零——旧口径此刻恒读 0
            Assert.AreEqual(660f, HousingService.HouseholdIncome(state), .01f,
                "清晨应读最近 3 期日均(600+60),而非 0。");
        }

        // ————— P1 #5:紧缩财政的直接购买进 G —————

        [Test]
        public void Austerity_DirectPurchase_KeepsSign_InGovernmentSpending()
        {
            state.macro.fiscalStance = FiscalStance.Contractionary;
            state.macro.plannedPublicInvestment = 2000f;
            state.macro.fiscalLagStage = 3;    // share=.30 ⇒ direct = -600
            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            Assert.AreEqual(-600f, record.capitalSpending, .01f,
                "紧缩直接购买须以负值进 capitalSpending(旧 Max(0,·) 把削减从 G 里抹掉)。");
        }

        // ————— P1 #6:冲击指数均值回归 —————

        [Test]
        public void ShockIndices_MeanRevert_Toward100()
        {
            state.macro.energyCostIndex = 138f;
            state.macro.externalDemandIndex = 70f;
            MacroEconomySimulator.AdvancePeriod(state);
            Assert.Less(state.macro.energyCostIndex, 138f, "能源冲击须逐期回落。");
            Assert.Greater(state.macro.energyCostIndex, 100f, "回落方向朝 100,不一步归位。");
            Assert.Greater(state.macro.externalDemandIndex, 70f, "外需冲击同样回升(剧情不再永久锁死)。");
        }

        // ————— P1 #7:中间投入进口不重复施加汇率楔子 —————

        [Test]
        public void IntermediateImport_NoSecondFxWedge_InLedger()
        {
            state.worldTrade.exchangeRate.rate = 8.4f;   // 进口价格指数=120,旧实现会在账本侧再乘 1.2
            state.planProduction = 100;
            DayRecord rec = DaySimulator.Settle(state, DaySimulator.RollDemand(state),
                DaySimulator.Production(state));
            InternationalTradeService.SettleDayTrade(state);
            TradeDayRecord row = state.worldTrade.history[state.worldTrade.history.Count - 1];
            float expected = Mathf.Max(0f, rec.materialCost) *
                InternationalTradeService.IntermediateImportShareOfMaterialCost;
            Assert.AreEqual(expected, row.intermediateImportSpend, Mathf.Max(.5f, expected * .02f),
                "账本侧不得再乘进口价格指数——汇率影响已由成本端唯一施加。");
        }

        // ————— P1 #8/#9:读档重放回滚(居民日薪+企业资源基线) —————

        [Test]
        public void ReplayDay_RollbacksResidentWageAndEnterpriseBaseline()
        {
            state.planProduction = 100;
            DayRecord rec = DaySimulator.Settle(state, DaySimulator.RollDemand(state),
                DaySimulator.Production(state));
            ResidentEconomy.EndDay(state);
            float residentCashStart = state.resident.history[state.resident.history.Count - 1].cashStart;
            Assert.AreNotEqual((double)state.startingMoney, (double)state.money, "护栏:当日结算确实改变了资金。");
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(state.startingMoney, state.money, .01f,
                "首日重放须回退到新局资金基线(旧实现保留日末值,重放营业=刷钱)。");
            Assert.AreEqual(300, state.materials, "原料回到新局口径。");
            Assert.AreEqual(residentCashStart, state.resident.cash, .01f,
                "居民现金回到当日 cashStart——重玩再收日不会二次发薪。");
            Assert.AreEqual(0f, state.resident.todayIncome);
            Assert.AreEqual(0, state.economy.financialHealthLastWorldDay,
                "财务健康防重闸一并复位,重玩日重新判定。");
        }

        // ————— 围猎三复审:重放回滚的组合路径(应急取款+当日存款+银行镜像) —————

        [Test]
        public void ReplayDay_EmergencyWithdrawalAndDaytimeDeposit_MirrorAndMoneyConserved()
        {
            // 储蓄 500,银行镜像背书
            state.bank.reserves += 500f - state.bank.householdDeposits;
            state.bank.householdDeposits = 500f;
            state.resident.savings = 500f;
            // 清晨应急取款(衰退+全失业,cash 20 → 取 10,双边扣银行)
            state.macro.cyclePhase = MacroCyclePhase.Recession;
            state.economy.labor.unemploymentRate = 100f;
            state.resident.reportReady = true;
            state.resident.cash = 20f;
            ResidentEconomy.BeginNextDay(state);
            Assert.AreEqual(490f, state.resident.savings, .01f);
            Assert.AreEqual(490f, state.bank.householdDeposits, .01f, "取款双边扣减。");
            // 白天存入 10(四腿记账;取款后现金 30,存 10)
            Assert.IsTrue(ResidentEconomy.Save(state, 10f).success);
            ResidentEconomy.EndDay(state);
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(20f, state.resident.cash, .01f,
                "现金回到当日 cashStart(应急取款前的清晨快照)。");
            Assert.AreEqual(500f, state.resident.savings, .01f,
                "应急取款退回、当日存款撤回——回滚后是真正的清晨起点(旧实现存款保留=凭空增钱可循环)。");
            Assert.AreEqual(state.resident.savings, state.bank.householdDeposits, .01f,
                "银行镜像两腿在 d.bank 装载后同向补齐,savings≡householdDeposits 不破。");
        }

        // ————— P1 #22:应急取款双边记账 —————

        [Test]
        public void EmergencyWithdrawal_KeepsBankMirror()
        {
            state.macro.cyclePhase = MacroCyclePhase.Recession;
            state.economy.labor.unemploymentRate = 100f;
            state.resident.reportReady = true;
            state.resident.cash = 20f;
            state.resident.savings = 500f;
            float deposits0 = state.bank.householdDeposits;
            ResidentEconomy.BeginNextDay(state);
            float withdrawal = Mathf.Min(500f, 30f - 20f);
            Assert.AreEqual(20f + withdrawal, state.resident.cash, .01f);
            Assert.AreEqual(500f - withdrawal, state.resident.savings, .01f);
            Assert.AreEqual(deposits0 - withdrawal, state.bank.householdDeposits, .01f,
                "银行存款负债必须同步扣减(旧单边写法让镜像永久脱钩)。");
        }

        // ————— P1 #12 + #39:分叉竞争分流与战略支出对称 —————

        [Test]
        public void Fork_CompetitionActive_UsesCompetitionPipeline_StrategicSpendOnce()
        {
            state.competitionIntroduced = true;
            state.economy.market.sunnyTea.isOpen = true;
            state.todayStrategicSpend = 900f;
            string before = WorldForkService.CaptureJson(state);
            ForkRunResult result = WorldForkService.RunClonedWorld(before, 3, null, null, fork =>
            {
                int hits = 0;
                for (int i = 0; i < fork.history.Count; i++)
                    if (fork.history[i].capitalInvestment >= 900f) hits++;
                Assert.LessOrEqual(hits, 1,
                    "战略支出只应计入一次(竞争路径计入即清零,旧实现 3 期各计一次)。");
                Assert.Greater(fork.competitor.lastSales, 0,
                    "竞争期分叉须走 CompetitionSimulator——对手真实卖货,不再冻结。");
            });
            Assert.IsTrue(result.completed, result.blockedReason);
            Assert.AreEqual(before, WorldForkService.CaptureJson(state), "主世界零扰动(§197.8 #1 判据)。");
        }

        [Test]
        public void DaySimulatorSettle_ClearsStrategicSpend()
        {
            state.todayStrategicSpend = 900f;
            state.planProduction = 100;
            DayRecord rec = DaySimulator.Settle(state, DaySimulator.RollDemand(state),
                DaySimulator.Production(state));
            Assert.GreaterOrEqual(rec.capitalInvestment, 900f, "当日计入资本形成。");
            Assert.AreEqual(0f, state.todayStrategicSpend, "日终清零,不漏到下一营业日。");
        }

        // ————— P1 #10:购买顾客排队点不落进店门碰撞带 —————

        [Test]
        public void SendBuying_QueueSpot_AlwaysBelowDoor()
        {
            NpcAgent npc = NpcAgent.Spawn("游客", new Vector3(0f, -5f, 0f), false, false);
            try
            {
                System.Reflection.FieldInfo field = typeof(NpcAgent).GetField("queueSpot",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(field);
                Vector3 door = new Vector3(-11f, -9.99f, 0f);   // 门点=碰撞带下沿-0.5
                for (int i = 0; i < 30; i++)
                {
                    npc.SendBuying(door);
                    Vector3 spot = (Vector3)field.GetValue(npc);
                    Assert.Less(spot.y, door.y - .55f, "排队点必须在门点下方(旧正负散布约半数落进碰撞带,顾客站桩锁死演出)。");
                    Assert.GreaterOrEqual(spot.y, door.y - 1.05f, "散布幅度不变(0.6–1.0)。");
                }
            }
            finally
            {
                if (npc != null) Object.DestroyImmediate(npc.gameObject);
            }
        }

        // ————— P1 #11:面板栈模态化 —————

        [Test]
        public void PanelStack_OnlyTopIsInteractable()
        {
            GameObject go = new GameObject("V098UiTest");
            V098UiHarness ui = go.AddComponent<V098UiHarness>();
            ui.InitializeForValidation();
            GameObject bottom = ui.OpenPanel("围猎底层面板", 500f, 400f);
            GameObject top = ui.OpenPanel("围猎顶层面板", 500f, 400f);
            CanvasGroup bottomGroup = bottom != null ? bottom.GetComponent<CanvasGroup>() : null;
            CanvasGroup topGroup = top != null ? top.GetComponent<CanvasGroup>() : null;
            Assert.IsNotNull(bottomGroup, "模态化须为面板挂 CanvasGroup。");
            Assert.IsNotNull(topGroup);
            Assert.IsFalse(bottomGroup.blocksRaycasts || bottomGroup.interactable,
                "非栈顶面板不可交互——旧实现可从解释弹窗下缘点穿「进入下一天」。");
            Assert.IsTrue(topGroup.blocksRaycasts && topGroup.interactable, "栈顶保持可交互。");
            ui.CloseTopEx();
            Assert.IsTrue(bottomGroup.blocksRaycasts && bottomGroup.interactable,
                "栈顶关闭后下层恢复交互。");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
