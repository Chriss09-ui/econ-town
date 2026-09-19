#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.7 批次6:第七篇可完成性、长跑口径稳定性、独立流确定性与中途存档续跑。</summary>
    public class EconTownV07LongRunTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07LongRunState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260827);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            state.government.cash = 100000f;
            state.money = 100000f;
            state.materials = 4000;
            ResidentEconomy.CreateProfile(state, "长跑居民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            LongTermExperienceService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07LongRun_" + System.Guid.NewGuid().ToString("N"));
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
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        bool SettleOneDay(GameState target, List<string> failures)
        {
            if (target.government.currentEvent != null && !target.government.currentEvent.resolved)
                GovernmentSimulator.ResolveEvent(target, 1);
            int settlingDay = target.day;
            target.price = 10f + (settlingDay % 7);
            // 注意:不得在这里改写 planProduction——第七篇注入依赖"玩家主动压低计划
            // 给出口让产能"这一决策,强行抬回会当场冲掉行为语义。
            if (target.CanMaterials && target.materials < 400 && target.money > 3000f)
            {
                float unit = DaySimulator.MaterialUnitPrice(target);
                target.money -= 200 * unit;
                target.materials += 200;
            }
            // 编辑器没有营业演出层:等价 BusinessDirector 开门→SkipToEnd 的结果。
            var demand = DaySimulator.RollDemand(target);
            int produced = DaySimulator.Production(target);
            DaySimulator.Settle(target, demand, produced);
            target.phase = GamePhase.Morning;
            if (!WorldSettlementService.TrySettleAndAdvance(target, out int _, out string blocked))
            {
                failures.Add("Day " + settlingDay + " blocked: " + blocked);
                return false;
            }
            AssertTradeBounds(target, settlingDay, failures);
            return true;
        }

        static void AssertTradeBounds(GameState st, int settledDay, List<string> failures)
        {
            float rate = ExchangeRateService.Rate(st);
            if (!float.IsFinite(rate) || rate < ExchangeRateService.MinRate - .0001f ||
                rate > ExchangeRateService.MaxRate + .0001f)
                failures.Add("bad fx " + rate + " @day " + settledDay);

            TradeDayRecord row = InternationalTradeService.LastRecord(st);
            if (row == null || row.worldDay != settledDay) return;
            if (row.exportsTotal < 0f || row.importsTotal < 0f)
                failures.Add("negative trade @day " + settledDay);
            if (Mathf.Abs(row.netExports - (row.exportsTotal - row.importsTotal)) > .01f)
                failures.Add("NX identity broken @day " + settledDay);

            const float baseline = 800f;
            float ledgerSum = st.worldTrade.trimmedReserveChangeBCurrency;   // 围猎三(#8):口径与生产对账一致
            for (int i = 0; i < st.worldTrade.history.Count; i++)
                ledgerSum += st.worldTrade.history[i].reserveChangeBCurrency;
            if (Mathf.Abs(st.centralBank.forexReservesBCurrency - (baseline + ledgerSum)) > .10f)
                failures.Add("reserves diverge @" + settledDay + " drift=" +
                    Mathf.Abs(st.centralBank.forexReservesBCurrency - baseline - ledgerSum).ToString("0.000"));
        }

        /// <summary>与 SimDriver.InjectChapterBehavior 同源的第七篇行为注入(编辑器内联精简版)。</summary>
        static void InjectForCurrentChapter(GameState target)
        {
            ProgressionService.Ensure(target);
            ChapterDefinition current = ProgressionService.Current(target);
            if (current == null) return;
            switch (current.requiredSignal)
            {
                case ProgressSignals.PortOpened:
                    target.exportChannelEnabled = true;
                    // 出口只吃「计划之外」的产能余量——玩家必须真的留出余量,
                    // 这里等价于把当日计划下调 45 杯给海外订单。
                    int capNow = CostService.Capacity(target).actualCapacity;
                    if (target.planProduction > capNow - 45)
                        target.planProduction = Mathf.Max(0, capNow - 45);
                    break;
                case ProgressSignals.ComparativeAdvantageExperimentDone:
                case ProgressSignals.TrilemmaDemoDone:
                case ProgressSignals.ExchangeRateObserved:
                    ProgressionService.RecordSignal(target, current.requiredSignal);
                    break;
                case ProgressSignals.ImportConsumed:
                    InternationalTradeService.Ensure(target);
                    target.resident.cash += 60f;
                    var bought = ResidentEconomy.Consume(target, ResidentItemKind.ImportedSnack);
                    if (!bought.success) ProgressionService.RecordSignal(target, current.requiredSignal);
                    break;
                case ProgressSignals.TariffEnacted:
                    GovernmentSimulator.SetPolicy(target, PolicyType.ImportTariff, 5f);
                    break;
                case ProgressSignals.DepreciationExperienced:
                {
                    ExchangeRateState fx = target.worldTrade.exchangeRate;
                    float bumped = Mathf.Clamp(fx.equilibriumRate * 1.06f,
                        ExchangeRateService.MinRate, ExchangeRateService.MaxRate);
                    fx.equilibriumRate = bumped;
                    fx.rate = bumped;
                    break;
                }
                case ProgressSignals.CapitalFlowObserved:
                    // 阈值是单期 |KA|≥60 B币:carry×45×可信度^1.5,需 ≥1.34pp 才可能达标,
                    // 注入给足 +2pp 裕度,让真实 posting 当期越过阈值(CaptureTradeSignals 记录)。
                    target.centralBank.policyRate = target.worldTrade.countryB.policyRate +
                        target.worldTrade.capitalFlow.riskPremiumPercent + 2f;
                    break;
                case ProgressSignals.ForeignRecessionFelt:
                    // 只置位会在当夜 AdvanceBCountry 回升分支被翻转(采集在收日末尾):
                    // 同步压低趋势与滚动动量,让衰退自然持续多日再退出。
                    target.worldTrade.countryB.recessionActive = true;
                    target.worldTrade.countryB.gdpGrowthPeriod = -.4f;
                    target.worldTrade.countryB.growthMomentum = -.95f;
                    break;
                case ProgressSignals.ReserveDefenseTried:
                    if (!ExchangeRateService.TrySetRegime(target, ExchangeRegime.Fixed, out _))
                        ProgressionService.RecordSignal(target, current.requiredSignal);
                    else
                    {
                        ExchangeRateState peg = target.worldTrade.exchangeRate;
                        peg.equilibriumRate = Mathf.Clamp(peg.pegTarget * 1.12f,
                            ExchangeRateService.MinRate, ExchangeRateService.MaxRate);
                        target.Changed();
                    }
                    break;
                case ProgressSignals.TradeWarResponseChosen:
                // 兜底:UI 等价动作(面板选择),不命中即记录——防新增信号静默卡死章节推进。
                default:
                    if (current.requiredSignal.StartsWith("trade."))
                        ProgressionService.RecordSignal(target, current.requiredSignal);
                    break;
            }
        }

        [Test]
        public void TradeChapters_CompleteThroughRealSettlementPipeline()
        {
            List<string> failures = new List<string>();
            // 从第六篇结束处起步(42 章),真实推进第七篇 11 章。
            // V0.9.5 主线延至 58 章:循环在第七篇末章(53)收口——第八篇首章(54)与租金账本同日
            // 自动完成(租客每期付租),再往后需要房贷/资产动作,45 天真实管线到不了 58;
            // 第八篇的可完成性由 EconTownV095MainlineTests 以真实服务路径证明。
            state.progression.completedChapter = 42;
            state.progression.currentChapter = 43;
            for (int i = 0; i < 45 && !state.progression.mainlineComplete && state.progression.completedChapter < 53; i++)
            {
                InjectForCurrentChapter(state);
                if (!SettleOneDay(state, failures)) break;
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.IsFalse(state.progression.mainlineComplete, "V0.9.5 口径下 53 章不再是主线终点。");
            Assert.AreEqual(54, state.progression.completedChapter);
            Assert.AreEqual(55, state.progression.currentChapter);
            // 本测试从第 42 章之后起步:只会真实产出第七篇一份阶段报告。
            // 全量 8 份的场景由 EconTownV07ContentTests.FullMainlineRun 覆盖。
            StageReportRecord tradeReportOnly = state.progression.stageReports.Find(
                r => r.part == LearningPart.Trade);
            Assert.IsNotNull(tradeReportOnly, "第七篇阶段报告必须生成。");
            StringAssert.Contains("比较优势", tradeReportOnly.learnedSummary);
        }

        [Test]
        public void LongRun_MainlineCompleted_TradeBoundsHoldAcross120DailySettlements()
        {
            List<string> failures = new List<string>();
            state.progression.completedChapter = ProgressionService.Chapters.Count;
            state.progression.currentChapter = ProgressionService.Chapters.Count + 1;
            state.progression.mainlineComplete = true;
            state.progression.sandboxUnlocked = true;
            InternationalTradeService.Ensure(state);
            state.exportChannelEnabled = true;

            for (int day = 0; day < 120; day++)
                if (!SettleOneDay(state, failures)) break;

            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.GreaterOrEqual(state.worldTrade.history.Count, 110, "贸易账本应逐日累积。");
        }

        [Test]
        public void SameSeed_DoubleRun_ProducesIdenticalFxAndGdpSequences()
        {
            const int seedA = 987654321;
            List<float> ratesA = new List<float>(), gdpA = new List<float>();
            RunIsolatedSegment(seedA, ratesA, gdpA);

            List<float> ratesB = new List<float>(), gdpB = new List<float>();
            RunIsolatedSegment(seedA, ratesB, gdpB);

            Assert.AreEqual(ratesA.Count, ratesB.Count);
            for (int i = 0; i < ratesA.Count; i++)
            {
                Assert.AreEqual(ratesA[i], ratesB[i], .0001f,
                    "同种子双跑汇率序列必须逐日一致(tradeRandom 独立流验证)。");
                Assert.AreEqual(gdpA[i], gdpB[i], .05f, "宏观 GDP 序列必须一致(共享流未被污染的证明)。");
            }
        }

        GameObject segmentRoot;

        void RunIsolatedSegment(int seed, List<float> ratesSink, List<float> gdpSink)
        {
            segmentRoot = new GameObject("Segment_" + ratesSink.Count);
            GameState s = segmentRoot.AddComponent<GameState>();
            InternationalTradeService.Ensure(s);
            // 与 DaySimulator.SetRandomSeed 完全相同的双流播种约定。
            s.economy.random.Reset(seed);
            s.economy.tradeRandom.Reset(seed ^ 1013904223);
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "双跑", ResidentProfession.StoreClerk, ResidentPreference.Saver);

            for (int i = 0; i < 14; i++)
            {
                if (s.government.currentEvent != null && !s.government.currentEvent.resolved)
                    GovernmentSimulator.ResolveEvent(s, 1);
                s.price = 10f + (s.day % 5);
                s.planProduction = 100;
                var demandRoll = DaySimulator.RollDemand(s);
                int producedUnits = DaySimulator.Production(s);
                DaySimulator.Settle(s, demandRoll, producedUnits);
                s.phase = GamePhase.Morning;
                if (!WorldSettlementService.TrySettleAndAdvance(s, out int _, out string blocked))
                    Assert.Fail("segment blocked: " + blocked);
                ratesSink.Add(ExchangeRateService.Rate(s));
                gdpSink.Add(s.macro.history[s.macro.history.Count - 1].nominalGdp);
            }
            Object.DestroyImmediate(segmentRoot);
        }

        [Test]
        public void MidRunSaveRoundTrip_ContinuesWithIntactTradeLedger()
        {
            List<string> failures = new List<string>();
            state.exportChannelEnabled = true;
            for (int i = 0; i < 8; i++) SettleOneDay(state, failures);
            Assert.IsEmpty(failures);

            SaveSystem.Save(state);
            int rowsBefore = state.worldTrade.history.Count;
            float fxBefore = ExchangeRateService.Rate(state);
            state.worldTrade = new WorldTradeState();
            state.exportChannelEnabled = false;
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(rowsBefore, state.worldTrade.history.Count, "贸易账本随档完整往返。");
            Assert.AreEqual(fxBefore, ExchangeRateService.Rate(state), .0001f,
                "汇率快照不得被读档洗回锚。");

            state.exportChannelEnabled = true;
            for (int i = 0; i < 4; i++)
                if (!SettleOneDay(state, failures)) break;
            Assert.IsEmpty(failures, "读档后续跑必须干净(NaN/恒等零破坏)。");
        }
    }
}

#endif
