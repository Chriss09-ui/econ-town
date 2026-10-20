#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownMainlineGuideTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("MainlineGuideTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
            if (GameState.I == state) GameState.I = null;
        }

        [Test]
        public void EarlyChaptersStayShopOwner_AndLockLaterRoles()
        {
            state.progression = new ProgressionState { currentChapter = 1, initialized = true };
            ProgressionService.Ensure(state);

            Assert.AreEqual(PlayerViewMode.Enterprise, state.currentView);
            Assert.IsFalse(MainlineGuide.CanEnter(state, PlayerViewMode.Resident));
            Assert.IsFalse(MainlineGuide.CanEnter(state, PlayerViewMode.Government));
            Assert.IsNotNull(MainlineGuide.DockBlockReason(state, 4));
            Assert.IsNull(MainlineGuide.DockBlockReason(state, 1));
            StringAssert.Contains("店主", MainlineGuide.RoleName(state));
        }

        [Test]
        public void ChapterElevenBecomesResident_ChapterThirtySevenBecomesCentralBanker()
        {
            state.progression = new ProgressionState
            {
                currentChapter = 11,
                completedChapter = 10,
                initialized = true
            };
            state.currentView = PlayerViewMode.Enterprise;
            ProgressionService.Ensure(state);
            Assert.AreEqual(PlayerViewMode.Resident, state.currentView);
            Assert.IsTrue(MainlineGuide.CanEnter(state, PlayerViewMode.Resident));
            Assert.IsFalse(MainlineGuide.CanEnter(state, PlayerViewMode.Government));

            state.progression.currentChapter = 37;
            state.progression.completedChapter = 36;
            ProgressionService.Ensure(state);
            Assert.AreEqual(PlayerViewMode.CentralBank, state.currentView);
            Assert.IsTrue(MainlineGuide.CanEnter(state, PlayerViewMode.CentralBank));
            Assert.IsFalse(MainlineGuide.CanEnterTrade(state));
        }

        [Test]
        public void ChapterFourCueTellsThePlayerToHire()
        {
            state.progression = new ProgressionState { currentChapter = 4, completedChapter = 3, initialized = true };
            state.employees = 1;
            MainlineGuide.Cue cue = MainlineGuide.Build(state);
            StringAssert.Contains("2", cue.line);
            Assert.AreEqual(MainlineDestination.Shop, cue.destination);
            Assert.AreEqual(1, cue.dockIndex);
        }

        [Test]
        public void FreePlayUnlocksEveryDock()
        {
            state.progression = new ProgressionState
            {
                currentChapter = 59,
                completedChapter = 58,
                mainlineComplete = true,
                initialized = true
            };
            state.currentView = PlayerViewMode.Government;
            ProgressionService.Ensure(state);
            Assert.AreEqual(PlayerViewMode.Government, state.currentView, "通关后不再强行改身份。");
            Assert.IsNull(MainlineGuide.DockBlockReason(state, 4));
            Assert.IsNull(MainlineGuide.DockBlockReason(state, 7));
            Assert.IsTrue(MainlineGuide.CanEnterTrade(state));
        }

        [Test]
        public void ResidentJobIsNotTheShopsHiredStaff()
        {
            Assert.AreEqual("市场帮工", ResidentEconomy.ProfessionName(ResidentProfession.StoreClerk));
            Assert.IsFalse(ResidentEconomy.ProfessionName(ResidentProfession.StoreClerk).Contains("店员"));
        }

        [Test]
        public void MidPartCompletionQueuesHandoff_PartEndingDoesNot()
        {
            state.progression = new ProgressionState { currentChapter = 1, initialized = true };
            ProgressionService.RecordSignal(state, ProgressSignals.FirstBusiness);
            Assert.AreEqual(2, state.progression.currentChapter);
            Assert.AreEqual(1, state.progression.pendingHandoffChapter);

            state.progression.pendingHandoffChapter = 0;
            state.progression.currentChapter = 5;
            state.progression.completedChapter = 4;
            ProgressionService.RecordSignal(state, ProgressSignals.ProfitExperiment);
            Assert.AreEqual(6, state.progression.currentChapter);
            Assert.AreEqual(0, state.progression.pendingHandoffChapter, "篇末交给阶段报告，不再叠一张交接卡。");
        }
    }
}
#endif
