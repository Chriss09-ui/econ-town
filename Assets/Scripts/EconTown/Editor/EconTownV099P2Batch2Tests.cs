#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>围猎三第二批(P2 存档卫生+读档收尾+金融口径,10 项)的回归测试:
    /// checkpoint 原子写+半截档拒读/liquidityCrisis 日终重算/镇买方按揭不污染信用分与利率/
    /// 贸易账 730 行截断外储对账/居民日报提前快照的收日回填/进口关税税前基/进口 WTP 本币锚/
    /// 财政压力迟滞/歇业日不发企业篇信号 逐条钉住(读档收尾 PostLoadRefresh 为纯接线,由编译与复审覆盖)。</summary>
    public class EconTownV099P2Batch2Tests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;
        string tempDir;

        GameState NewWorld()
        {
            GameObject go = new GameObject("V099P2World" + worlds.Count);
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
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV099P2Tests_" + System.Guid.NewGuid().ToString("N"));
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

        static int CountFiscalNews(GameState s)
        {
            int n = 0;
            for (int i = 0; i < s.experience.news.Count; i++)
                if (s.experience.news[i].id != null && s.experience.news[i].id.StartsWith("crisis.fiscal")) n++;
            return n;
        }

        static bool HistoryHas(GameState s, string idPrefix)
        {
            for (int i = 0; i < s.experience.economicHistory.Count; i++)
                if (s.experience.economicHistory[i].id != null &&
                    s.experience.economicHistory[i].id.StartsWith(idPrefix)) return true;
            return false;
        }

        // ————— P2 #12:checkpoint 原子写 + 半截档拒读 —————

        [Test]
        public void SaveCheckpoint_AtomicOverwrite_NoTempFile_LatestWins()
        {
            SaveSystem.SaveCheckpoint(state, "第一节点");
            state.day = 12;
            state.money = 4321f;
            SaveSystem.SaveCheckpoint(state, "第二节点");
            Assert.IsFalse(File.Exists(SaveSystem.CheckpointPath + ".tmp"),
                "原子落位后不得残留 .tmp 临时文件(断电中间态会被下一次恢复读到)。");
            string json = File.ReadAllText(SaveSystem.CheckpointPath);
            Assert.IsTrue(json.Contains("\"day\": 12") || json.Contains("\"day\":12"),
                "checkpoint 须是最新一次的完整内容。");
        }

        [Test]
        public void LoadCheckpoint_TruncatedJson_Rejected_StatePreserved()
        {
            state.day = 30;
            state.money = 7777f;
            SaveSystem.SaveCheckpoint(state, "完整节点");
            string json = File.ReadAllText(SaveSystem.CheckpointPath);
            int cut = json.IndexOf("\"economy\"");
            Assert.Greater(cut, 0, "护栏:完整档存在 economy 字段(位于 day 之后)。");
            // 模拟断电半截档:day 已写入、economy 及其后全部丢失——JsonUtility 不保证抛异常。
            File.WriteAllText(SaveSystem.CheckpointPath, json.Substring(0, cut));
            state.money = 12345f;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "关键节点存档无法读取。");
            Assert.IsFalse(SaveSystem.LoadCheckpoint(state),
                "半截档必须拒绝恢复(旧实现 economy=null 落 ?? new EconomyState() 默认种子世界)。");
            Assert.AreEqual(12345f, state.money, .01f, "拒绝后当前进度不得被残废档覆盖。");
        }

        // ————— P2 #7:liquidityCrisis 日终重算 —————

        [Test]
        public void LiquidityCrisis_RecomputesDaily_NotSticky()
        {
            BankState bank = state.bank;
            bank.requiredReserveRatio = .1f;
            bank.householdDeposits = 500f;
            bank.companyDeposits = 0f;
            bank.townSectorDeposits = 0f;
            bank.reserves = 10f;                              // Required=50,缺 40
            bank.equity = bank.TotalAssets - bank.TotalLiabilities;   // 手工配平,gap≡0
            FinancialSystemService.CompleteSettlement(state);
            Assert.IsTrue(bank.liquidityCrisis, "准备金低于法定线当日应置位。");
            bank.reserves = 500f;                            // 自然回补(存款/还款/法拍)
            bank.equity = bank.TotalAssets - bank.TotalLiabilities;
            state.day++;
            FinancialSystemService.CompleteSettlement(state);
            Assert.IsFalse(bank.liquidityCrisis,
                "回补后日终重算必须清除(旧 OR 累加只能置位,溢价与「流动性紧张」永错报)。");
        }

        // ————— P2 #6:镇买方按揭不污染玩家信用分/平均利率 —————

        [Test]
        public void TownBuyerLoans_ExcludedFromResidentCreditScoreAndAvgRate()
        {
            float baseline = CreditService.ResidentCreditScore(state);
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = MortgageService.TownBuyerId,
                status = LoanStatus.Current,
                remainingPrincipal = 8000f,
                annualInterestRate = .09f,
                missedPayments = 2
            });
            Assert.AreEqual(baseline, CreditService.ResidentCreditScore(state), .01f,
                "镇买方按揭欠款不得污染玩家信用分(旧口径每期 -45 分,两期 -90)。");
            Assert.AreEqual(0f, CreditService.AverageLoanRate(state, LoanBorrowerType.Resident,
                state.resident.residentName), .001f, "玩家无贷款时平均利率为 0(镇买方 9% 不得混入展示)。");
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                status = LoanStatus.Current,
                remainingPrincipal = 500f,
                annualInterestRate = .06f,
                missedPayments = 1
            });
            Assert.Less(CreditService.ResidentCreditScore(state), baseline - 40f,
                "护栏:玩家自己的欠款照常扣分(过滤按借款人归属,不是全滤)。");
        }

        // ————— P2 #8:贸易账 730 行截断的外储对账 —————

        [Test]
        public void TrimmedTradeLedger_ForexReconcile_NoFalseAlarm()
        {
            state.worldTrade.exchangeRate.initialized = true;
            for (int i = 0; i < 730; i++)
                state.worldTrade.history.Add(new TradeDayRecord { worldDay = i + 1 });
            state.worldTrade.history[0].reserveChangeBCurrency = -35f;   // 史上守汇的旧行
            state.centralBank.forexReservesBCurrency = 800f - 35f;      // 实际储备与真实干预相符
            System.Reflection.MethodInfo method = typeof(InternationalTradeService).GetMethod(
                "GetOrCreateTodayRow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { state.worldTrade, 731 });  // 触发 730 上限裁行
            Assert.AreEqual(-35f, state.worldTrade.trimmedReserveChangeBCurrency, .01f,
                "被裁行的干预量必须累计进持久化字段。");
            Assert.AreEqual(730, state.worldTrade.history.Count);
            Assert.AreEqual(2, state.worldTrade.history[0].worldDay, "最旧行已被裁掉。");
            // 修复前此处 LogError「外汇储备账实不符 漂移=35」会让测试失败;修复后静默通过对账。
            ExchangeRateService.AssertNoFreeMoney(state);
        }

        // ————— P2 #9:居民日报提前快照的收日回填 —————

        [Test]
        public void EarlyResidentReport_SettlementRefillsSnapshot()
        {
            GameState manual = NewWorld();
            foreach (GameState s in new[] { state, manual })
            {
                s.bank.reserves += 500f - s.bank.householdDeposits;   // 镜像背书
                s.bank.householdDeposits = 500f;
                s.resident.savings = 500f;
                s.planProduction = 100;
            }
            // A 路径:居民面板「完成今日生活并生成日报」先抢跑(真实旁路),再统一收日
            ResidentEconomy.EndDay(manual);
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(manual, out _, out string ma), ma);
            // B 路径:直接统一收日
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string mb), mb);
            ResidentDayRecord a = manual.resident.history[manual.resident.history.Count - 1];
            ResidentDayRecord b = state.resident.history[state.resident.history.Count - 1];
            Assert.Greater(b.interestIncome, 0f, "护栏:当日确有存款利息可回填。");
            Assert.AreEqual(b.interestIncome, a.interestIncome, .01f,
                "抢跑日报的利息快照须被收日回填(旧实现清零后这笔利息从任何日报消失)。");
            Assert.AreEqual(b.interestExpense, a.interestExpense, .01f);
            Assert.AreEqual(b.savingsEnd, a.savingsEnd, .01f,
                "储蓄终值须含当日租金/供款后的真实终态。");
            Assert.AreEqual(b.cashEnd, a.cashEnd, .01f);
            Assert.AreEqual(b.income, a.income, .01f, "护栏:日薪不重发,income 口径一致。");
        }

        // ————— P2 #10:进口零食按税前完税基入账 —————

        [Test]
        public void ImportedSnack_RecordsPreTariffBase_NoTaxOnTax()
        {
            Assert.IsTrue(GovernmentSimulator.SetPolicy(state, PolicyType.ImportTariff, 25f));
            state.worldTrade.exchangeRate.rate = 7f;
            float price = ResidentEconomy.Price(state, ResidentItemKind.ImportedSnack);
            Assert.AreEqual(.85f * 7f * 1.25f, price, .01f, "护栏:实付仍是含税到岸价。");
            ResidentActionResult action = ResidentEconomy.Consume(state, ResidentItemKind.ImportedSnack);
            Assert.IsTrue(action.success, action.message);
            Assert.AreEqual(.85f * 7f, state.worldTrade.todayConsumerImportSpend, .01f,
                "账本累计=税前完税基——SettleDayTrade 以此基征关税,旧含税基等于对关税再征关税(税上税)。");
        }

        // ————— P2 #11:进口品 WTP 的本币参考价 —————

        [Test]
        public void ImportedSnack_Surplus_IndependentOfExchangeRate()
        {
            // 关税 0(默认值,SetPolicy 对相同值返回 false 故不设置):实付价与 WTP 本币锚随汇率
            // 同幅缩放,消费者剩余=纯效用项,与汇率无关;旧写法把 B币标价 0.85 当 ¥0.85 进 WTP,
            // 剩余随汇率漂移(7 vs 6 差 0.85)。
            Assert.AreEqual(0f, InternationalPriceService.TariffRateConsumer(state), .001f, "护栏:默认零关税。");
            GameState weak = NewWorld();
            weak.worldTrade.exchangeRate.rate = 7f;
            GameState strong = NewWorld();
            strong.worldTrade.exchangeRate.rate = 6f;
            ResidentActionResult rWeak = ResidentEconomy.Consume(weak, ResidentItemKind.ImportedSnack);
            ResidentActionResult rStrong = ResidentEconomy.Consume(strong, ResidentItemKind.ImportedSnack);
            Assert.IsTrue(rWeak.success && rStrong.success,
                "repro: weak=" + rWeak.message + " || strong=" + rStrong.message +
                " || cash=" + weak.resident.cash + "/" + strong.resident.cash +
                " free=" + weak.resident.freeHours + "/" + strong.resident.freeHours +
                " ready=" + weak.resident.reportReady + "/" + strong.resident.reportReady);
            float sWeak = LastSurplus(weak), sStrong = LastSurplus(strong);
            Assert.Greater(sWeak, 0f, "护栏:剩余为正,口径比较有意义。");
            Assert.AreEqual(sWeak, sStrong, .01f,
                "消费者剩余不得随汇率漂移(WTP 锚已折本币;效用项唯一)。");
        }

        static float LastSurplus(GameState s)
        {
            List<ResidentChoiceRecord> choices = s.resident.todayChoices;
            return choices[choices.Count - 1].consumerSurplus;
        }

        // ————— P2 #40:财政压力迟滞 —————

        [Test]
        public void FiscalPressure_Hysteresis_NoNewsSpam_ReliefWritten()
        {
            state.macro.debtToGdp = 85f;
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsTrue(state.experience.fiscalPressureActive);
            Assert.AreEqual(1, CountFiscalNews(state), "首次进入发一条新闻。");

            state.day += 1;
            state.macro.debtToGdp = 79.5f;      // 80 线下波动(旧实现每日重刷「进入」新闻)
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsTrue(state.experience.fiscalPressureActive, "迟滞带 [65,80) 内保持压力状态。");
            Assert.AreEqual(1, CountFiscalNews(state), "波动不得重刷「进入财政压力」新闻。");

            state.day += 1;
            state.macro.debtToGdp = 60f;
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsFalse(state.experience.fiscalPressureActive, "跌破 65 退出。");
            Assert.IsTrue(HistoryHas(state, "fiscal.recovered"), "退出时写「财政压力缓解」历史。");

            state.day += 1;
            state.macro.debtToGdp = 79.5f;      // 回弹但未达进入线
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsFalse(state.experience.fiscalPressureActive, "退出后 65-80 区间不重进。");
            Assert.AreEqual(1, CountFiscalNews(state));
        }

        // ————— 复审补修:重放日的清晨事件现金流/当日升级与贷款/挤兑 unmet 腿 —————

        [Test]
        public void ReplayDay_MorningWageEvent_CashRestoredNotVanished()
        {
            // lifeDay=4 → BeginNextDay 推进到 5;「围猎镇民」(名长4) → 事件号 |5*37+4*11|%6 = 1
            // = 意外奖金日(清晨 +¥30,不依赖就业状态)。断言按「清晨终态」对照,不赌具体事件。
            state.resident.lifeDay = 4;
            state.day = 4;
            state.resident.employedToday = true;
            state.resident.reportReady = true;      // 上一日已完成——BeginNextDay 的推进闸
            float cashBeforeMorning = state.resident.cash;
            ResidentEconomy.BeginNextDay(state);    // 清晨:快照(cashStart)+确定性事件入账
            float cashAfterMorning = state.resident.cash;
            float incomeAfterMorning = state.resident.todayIncome;
            bool advanced = state.resident.wageAdvanced;
            Assert.AreNotEqual(cashBeforeMorning, cashAfterMorning,
                "护栏:落在有清晨现金流的事件日(意外奖金 +30)。");
            ResidentEconomy.EndDay(state);          // 居民面板抢跑生成日报
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(cashAfterMorning, state.resident.cash, .01f,
                "重放日的清晨事件现金流必须按确定性事件号补回——cashStart 是事件前快照,旧回滚把这笔钱从世界抹掉。");
            Assert.AreEqual(advanced, state.resident.wageAdvanced,
                "wageAdvanced 状态与清晨终态一致(补回后 EndDay 不重发)。");
            Assert.AreEqual(incomeAfterMorning, state.resident.todayIncome, .01f);
        }

        [Test]
        public void ReplayDay_DayUpgradeAndNewLoans_RolledBack()
        {
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            float deposits0 = state.bank.companyDeposits;
            state.playerQualityLevelAtDayStart = 1;   // 模拟 AdvanceDay 清晨快照(新档字段)
            state.playerQualityLevel = 2;             // 当日升级(费用已扣,由 money 基线回滚覆盖)
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Company,
                borrowerId = "player.company",
                status = LoanStatus.Current,
                principal = 400f,
                remainingPrincipal = 400f,
                originatedWorldDay = state.day
            });
            state.bank.companyDeposits += 400f;
            state.bank.reserves += 400f;               // 贷款创造双边(测试注入背书)
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(1, state.playerQualityLevel,
                "当日升级随重放回滚——费用已退,等级不得留下(白嫖循环)。");
            Assert.AreEqual(0, state.bank.loans.Count, "当日新发贷款随重放撤销(债留钱走)。");
            Assert.AreEqual(deposits0, state.bank.companyDeposits, .01f,
                "贷款创造的存款同双边撤销。");
        }

        [Test]
        public void LiquidityCrisis_BankRunUnmetWithdrawals_KeepCrisisFlag()
        {
            BankState bank = state.bank;
            bank.requiredReserveRatio = .1f;
            bank.householdDeposits = 10000f;
            state.resident.savings = 10000f;
            bank.companyDeposits = 0f;
            bank.townSectorDeposits = 0f;
            bank.reserves = 1000f;
            bank.equity = bank.TotalAssets - bank.TotalLiabilities;
            CreditService.TriggerBankRun(state, .5f);   // paid=1000,unmet=4000
            Assert.Greater(bank.bankRunUnmetWithdrawals, .01f);
            bank.reserves = 1000f;                      // 存款回流恰好回到法定线(Required=900)
            bank.equity = bank.TotalAssets - bank.TotalLiabilities;
            FinancialSystemService.CompleteSettlement(state);
            Assert.IsTrue(bank.liquidityCrisis,
                "挤兑未满足提款挂账期间,日终重算不得清除危机标志(复审补的 unmet 腿)。");
        }

        // ————— P2 #41:歇业日不计企业篇信号 —————

        [Test]
        public void ClosedDays_DoNotAdvanceEnterpriseSignals()
        {
            float[] prices = { 12f, 15f, 18f, 20f };   // 四档挂牌价全在歇业日变更
            for (int i = 0; i < prices.Length; i++)
            {
                state.price = prices[i];
                DaySimulator.SettleClosedDay(state);
                state.day++;
            }
            ProgressionService.OnDaySettled(state);
            Assert.IsFalse(state.progression.signals.Contains(ProgressSignals.FirstBusiness),
                "从未开门不得发首营信号(歇业结算日 shopOpen=false)。");
            Assert.IsFalse(state.progression.signals.Contains(ProgressSignals.PriceChanged),
                "歇业日变更挂牌价不算「试价」。");
            Assert.IsFalse(state.progression.signals.Contains(ProgressSignals.ProfitExperiment));

            GameState open = NewWorld();
            open.price = 12f;
            open.planProduction = 100;
            DaySimulator.Settle(open, DaySimulator.RollDemand(open), DaySimulator.Production(open));
            ProgressionService.OnDaySettled(open);
            Assert.IsTrue(open.progression.signals.Contains(ProgressSignals.FirstBusiness),
                "护栏:开门日信号照常采集(过滤只认 shopOpen,不是全滤)。");
        }
    }
}
#endif
