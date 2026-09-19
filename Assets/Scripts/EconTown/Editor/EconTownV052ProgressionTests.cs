#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV052ProgressionTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V052ProgressionTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.progression = new ProgressionState();
            ProgressionService.Ensure(state);
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV052ProgressionTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            DaySimulator.LastEvent = null;
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void MainlineContainsSixOrderedPartsAndFortyTwoChapters()
        {
            Assert.AreEqual(58, ProgressionService.Chapters.Count);
            Assert.AreEqual(LearningPart.Housing, ProgressionService.Chapters[53].part,
                "V0.9.5 第八篇自第 54 章起尾加。");
            Assert.AreEqual(LearningPart.Trade, ProgressionService.Chapters[52].part);
            Assert.AreEqual(LearningPart.Trade, ProgressionService.Chapters[42].part);
            Assert.AreEqual(LearningPart.Enterprise, ProgressionService.Chapters[0].part);
            Assert.AreEqual(LearningPart.Market, ProgressionService.Chapters[5].part);
            Assert.AreEqual(LearningPart.Resident, ProgressionService.Chapters[10].part);
            Assert.AreEqual(LearningPart.Government, ProgressionService.Chapters[16].part);
            Assert.AreEqual(LearningPart.Macro, ProgressionService.Chapters[22].part);
            Assert.AreEqual(LearningPart.Finance, ProgressionService.Chapters[30].part);
            for (int i = 0; i < ProgressionService.Chapters.Count; i++)
                Assert.AreEqual(i + 1, ProgressionService.Chapters[i].number);
        }

        [Test]
        public void WorldDayAloneCannotAdvanceChapterOrOpenCompetitor()
        {
            state.day = 100;
            ProgressionService.Ensure(state);
            Assert.AreEqual(1, state.progression.currentChapter);
            Assert.AreEqual(0, state.progression.completedChapter);
            Assert.IsFalse(state.CompetitionActive, "V0.5.2 主线不得再靠熬到固定 Day 开放竞争者。");
        }

        [Test]
        public void EnterpriseBehaviorsOpenMarketWithoutWaitingForDaySixteen()
        {
            Assert.AreEqual(1, state.day);
            ProgressionService.RecordSignal(state, ProgressSignals.FirstBusiness);
            ProgressionService.RecordSignal(state, ProgressSignals.PriceChanged);
            ProgressionService.RecordSignal(state, ProgressSignals.ProfitObserved);
            ProgressionService.RecordSignal(state, ProgressSignals.EmployeeHired);
            ProgressionService.RecordSignal(state, ProgressSignals.ProfitExperiment);

            Assert.AreEqual(5, state.progression.completedChapter);
            Assert.AreEqual(6, state.progression.currentChapter);
            Assert.IsTrue(state.CompetitionActive, "完成企业篇应立即开放 Sunny Tea，而不是等待 Day 16。");
            Assert.AreEqual(1, state.day, "章节推进不得偷偷推进 WorldDay。");
        }

        [Test]
        public void ProgressionAndStageReportSurviveSaveRoundTrip()
        {
            ProgressionService.RecordSignal(state, ProgressSignals.FirstBusiness, "第一次营业", "完成首日结算");
            ProgressionService.RecordSignal(state, ProgressSignals.PriceChanged);
            ProgressionService.RecordSignal(state, ProgressSignals.ProfitObserved);
            ProgressionService.RecordSignal(state, ProgressSignals.EmployeeHired);
            ProgressionService.RecordSignal(state, ProgressSignals.ProfitExperiment);
            Assert.AreEqual(1, state.progression.stageReports.Count);

            SaveSystem.Save(state);
            state.progression = new ProgressionState();
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(6, state.progression.currentChapter);
            Assert.AreEqual(5, state.progression.completedChapter);
            Assert.AreEqual(1, state.progression.stageReports.Count);
            Assert.AreEqual(LearningPart.Enterprise, state.progression.stageReports[0].part);
            Assert.IsTrue(state.CompetitionActive);
        }

        [Test]
        public void PassingLegacyTotalDaysDoesNotEndTheWorld()
        {
            state.totalDays = 1;
            state.day = 100;
            state.phase = GamePhase.Report;
            bool advanced = WorldSettlementService.TrySettleAndAdvance(state, out _, out string reason);
            Assert.IsTrue(advanced, reason);
            Assert.AreEqual(101, state.day);
            Assert.AreNotEqual(GamePhase.Ended, state.phase, "V0.5.2 不得再在旧 totalDays 处强制终局。");
        }
    }
}

#endif
