#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.5.3 深度审查批次 2 回归测试（章节管线逻辑）：
    /// 1) pendingStageReport 队列化：一次 Evaluate 连跨多篇界时每个结业弹窗都保留；
    /// 2) V0.5.2 存档单值 pendingStageReport 迁移进队列；
    /// 3) 旧档迁移按真实状态补 CompetitorOpened / PriceWarResolved 信号（防功能回锁/价格战重打）；
    /// 4) streak 同日防重 + 读档丢弃当日记录后回滚（防"连续 N 日"被读档重放伪造）；
    /// 5) 章节 8 对手降价分支在连跳后（currentChapter=9）仍能触发。
    /// </summary>
    public class EconTownV053PipelineTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V053PipelineTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.economy.random.Reset(20260826);
            LongTermExperienceService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV053PipelineTests_" + System.Guid.NewGuid().ToString("N"));
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

        static void AddSignals(GameState s, params string[] signals)
        {
            for (int i = 0; i < signals.Length; i++)
                if (!s.progression.signals.Contains(signals[i])) s.progression.signals.Add(signals[i]);
        }

        [Test]
        public void MultiPartCompletionKeepsAllPendingStageReports()
        {
            // 一次性给足 1-10 章全部信号（旧档迁移/多行为并行的等价情形），
            // Evaluate 将在一次调用里连过两个篇界（第 5、10 章）。
            AddSignals(state,
                ProgressSignals.FirstBusiness, ProgressSignals.ProfitObserved, ProgressSignals.EmployeeHired,
                ProgressSignals.PriceChanged, ProgressSignals.ProfitExperiment,
                ProgressSignals.MarketShareObserved, ProgressSignals.CompetitorOpened,
                ProgressSignals.DifferentiationUsed, ProgressSignals.PriceWarResolved,
                ProgressSignals.MarketExperiment);
            ProgressionService.RecordSignal(state, "test.force.evaluate"); // 触发 Evaluate

            Assert.AreEqual(10, state.progression.completedChapter, "信号齐全应连过 10 章");
            Assert.AreEqual(2, state.progression.pendingStageReports.Count,
                "跨 Enterprise/Market 两个篇界，队列应保留两条待查看报告");
            Assert.AreEqual((int)LearningPart.Enterprise, state.progression.pendingStageReports[0], "先弹企业篇");

            StageReportRecord first = ProgressionService.GetPendingStageReport(state);
            Assert.AreEqual(LearningPart.Enterprise, first.part);
            ProgressionService.MarkPendingStageReportViewed(state);
            StageReportRecord second = ProgressionService.GetPendingStageReport(state);
            Assert.AreEqual(LearningPart.Market, second.part, "第二篇弹窗不得被第一篇覆盖吞掉");
            ProgressionService.MarkPendingStageReportViewed(state);
            Assert.IsNull(ProgressionService.GetPendingStageReport(state));
        }

        [Test]
        public void LegacySinglePendingStageReportMigratesIntoQueue()
        {
            // 模拟 V0.5.2 存档读入：CreateStageReport 总是同时写 stageReports 与单值 pending。
            state.progression.stageReports.Add(new StageReportRecord { part = LearningPart.Resident });
            state.progression.pendingStageReport = (int)LearningPart.Resident;
            ProgressionService.Ensure(state);
            Assert.AreEqual(0, state.progression.pendingStageReport, "旧单值字段应清零");
            Assert.IsTrue(state.progression.pendingStageReports.Contains((int)LearningPart.Resident),
                "未查看的结业弹窗应迁入队列不丢失");
            Assert.AreEqual(LearningPart.Resident, ProgressionService.GetPendingStageReport(state).part);
        }

        [Test]
        public void MigrationInfersCompetitionSignalsFromLegacyState()
        {
            // initialized=false 触发 MigrateExistingProgress；两个 UI 信号按旧档状态推断。
            state.progression.initialized = false;
            state.progression.signals.Clear();
            state.competitionIntroduced = true;
            state.priceWarRound = 2;
            state.priceWarActive = false;
            ProgressionService.Ensure(state);
            Assert.IsTrue(state.progression.signals.Contains(ProgressSignals.CompetitorOpened),
                "旧档已引入竞争应视作见过 Sunny Tea 开业，否则章节 6 卡死");
            Assert.IsTrue(state.progression.signals.Contains(ProgressSignals.PriceWarResolved),
                "打过价格战且已结束的旧档不得被迫重打价格战");
        }

        [Test]
        public void PositiveProfitStreakCountsEachSettledDayOnlyOnce()
        {
            DayRecord record = new DayRecord { day = state.day, profit = 50f, shopOpen = true };
            state.history.Add(record);
            ProgressionService.OnDaySettled(state);
            Assert.AreEqual(1, state.progression.positiveProfitStreak, "首个盈利结算日 streak=1");

            // 同一结算日重复推送（读档重放路径的等价情形）不得双记。
            ProgressionService.OnDaySettled(state);
            Assert.AreEqual(1, state.progression.positiveProfitStreak, "同一 DayRecord 不得把 streak 再 +1");
        }

        [Test]
        public void LoadingDroppedTodayRecordRollsBackStreak()
        {
            DayRecord record = new DayRecord { day = state.day, profit = 50f, shopOpen = true };
            state.history.Add(record);
            ProgressionService.OnDaySettled(state);
            Assert.AreEqual(1, state.progression.positiveProfitStreak);
            SaveSystem.Save(state); // 存档含当日已结算记录（Report 相位写盘的等价情形）

            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(0, state.progression.positiveProfitStreak,
                "读档丢弃当日盈利记录后 streak 应回滚（重玩日重新判定）");
            Assert.AreEqual(0, state.progression.positiveProfitStreakLastDay,
                "lastDay 必须复位，否则重玩日会被防重逻辑挡住无法重新计数");

            // 重玩同一天且盈利：应重新计为 1，而不是 2。
            state.history.Add(new DayRecord { day = state.day, profit = 60f, shopOpen = true });
            ProgressionService.OnDaySettled(state);
            Assert.AreEqual(1, state.progression.positiveProfitStreak);
        }

        [Test]
        public void RivalPriceCutStillTriggersAfterSkippingPastChapter8()
        {
            // 信号提前入库导致 Evaluate 连过 7、8 两章：currentChapter 直接到 9。
            state.progression.currentChapter = 9;
            state.progression.completedChapter = 8;
            state.progression.rivalPriceCutTriggered = false;
            state.competitionIntroduced = true;
            state.economy.market.sunnyTea.isOpen = true;
            state.playerBrand = 40f;
            float priceBefore = state.competitor.price = 18f;

            CompetitorAI.PrepareDay(state);

            Assert.IsTrue(state.progression.rivalPriceCutTriggered,
                "连跳过章节 8 后对手降价教学节点仍应触发（>= 8 而非 == 8）");
            Assert.Less(state.competitor.price, priceBefore, "对手应真的降价");
        }
    }
}

#endif
