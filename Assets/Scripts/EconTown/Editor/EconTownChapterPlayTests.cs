#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.18 篇章直达：8 个大篇开局、篇首不误过章与双档隔离。</summary>
    public class EconTownChapterPlayTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(),
                "EconTownChapterPlayTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
            SaveSystem.UseSlot(SaveSlot.Story);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.UseSlot(SaveSlot.Story);
            SaveSystem.OverrideSaveDirectory(null);
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
        }

        GameState NewState(string name)
        {
            GameObject go = new GameObject(name);
            worlds.Add(go);
            GameState state = go.AddComponent<GameState>();
            GameState.I = state;
            return state;
        }

        [Test]
        public void Parts_RegisterExactlyEightBigRanges()
        {
            int[] starts = { 1, 6, 11, 17, 23, 31, 43, 54 };
            int[] ends = { 5, 10, 16, 22, 30, 42, 53, 58 };
            Assert.AreEqual(8, ChapterPlayService.Parts.Count);
            for (int i = 0; i < ChapterPlayService.Parts.Count; i++)
            {
                ChapterPlayPartDefinition part = ChapterPlayService.Parts[i];
                Assert.AreEqual(i + 1, part.number);
                Assert.AreEqual(starts[i], part.startChapter);
                Assert.AreEqual(ends[i], part.endChapter);
                Assert.AreEqual(part, ChapterPlayService.GetPartForChapter(starts[i]));
                Assert.AreEqual(part, ChapterPlayService.GetPartForChapter(ends[i]));
            }
        }

        [Test]
        public void EveryPartStart_HasPlayablePrerequisites_AndDoesNotAutoCompleteFirstChapter()
        {
            for (int i = 0; i < ChapterPlayService.Parts.Count; i++)
            {
                ChapterPlayPartDefinition part = ChapterPlayService.Parts[i];
                GameState state = NewState("ChapterPart" + part.number);
                ChapterPlayService.PreparePartStart(state, part);

                Assert.AreEqual(part.startChapter, state.progression.currentChapter, part.Title);
                Assert.AreEqual(part.startChapter - 1, state.progression.completedChapter, part.Title);
                Assert.AreEqual(part.number, state.chapterPlayEntryPart, part.Title);
                Assert.IsFalse(state.progression.mainlineComplete, part.Title);
                Assert.IsFalse(state.progression.signals.Contains(
                    ProgressionService.Chapters[part.startChapter - 1].requiredSignal),
                    part.Title + " 的首章不得进档即完成。");
                Assert.AreEqual(0, state.progression.pendingStageReports.Count, part.Title);
                Assert.AreEqual(0, state.progression.pendingCeremonies.Count, part.Title);
                Assert.AreEqual(0, state.progression.stageReports.Count, part.Title);
                Assert.AreEqual(GamePhase.Morning, state.phase, part.Title);

                Assert.AreEqual(part.number >= 2, state.CompetitionActive, part.Title);
                Assert.AreEqual(part.number >= 4, state.resident.profileCreated, part.Title);
                Assert.AreEqual(part.number >= 7, state.centralBank.unlocked, part.Title);
                Assert.IsFalse(state.exportChannelEnabled, part.Title + " 应让玩家亲自开通出海。");
                Assert.Less(Mathf.Abs(state.bank.BalanceGap), .1f, part.Title + " 银行资产负债表必须平衡。");

                if (part.number >= 5)
                {
                    Assert.Greater(state.government.history.Count, 0, part.Title + " 需要可读财政史。");
                    Assert.Greater(state.macro.history.Count, 0, part.Title + " 需要可读宏观史。");
                }
                if (part.number >= 7)
                    Assert.Greater(state.worldTrade.history.Count, 0, part.Title + " 需要可读贸易账本。");
                if (part.number == 8)
                {
                    Assert.IsTrue(state.housing.renting, "第八篇必须从租住起点开始。");
                    Assert.GreaterOrEqual(state.resident.savings, 20000f, "需要足够首付让房贷链可玩。");
                    for (int row = 0; row < state.history.Count; row++)
                        Assert.AreEqual(0f, state.history[row].rentPaid, .001f,
                            "标准开局不得预先完成第 54 章的租金事实。");
                }

                Object.DestroyImmediate(worlds[worlds.Count - 1]);
                worlds.RemoveAt(worlds.Count - 1);
                GameState.I = null;
            }
        }

        [Test]
        public void EveryPartStart_FirstChapterAdvancesThroughItsRealEntryAction()
        {
            for (int i = 0; i < ChapterPlayService.Parts.Count; i++)
            {
                ChapterPlayPartDefinition part = ChapterPlayService.Parts[i];
                GameState state = NewState("ChapterEntryAction" + part.number);
                ChapterPlayService.PreparePartStart(state, part);

                switch (part.number)
                {
                    case 1:
                        SettleOpenDay(state);
                        break;
                    case 2:
                        ProgressionService.RecordSignal(state, ProgressSignals.CompetitorOpened,
                            "Sunny Tea 开业", "查看开业介绍");
                        break;
                    case 3:
                        ResidentEconomy.CreateProfile(state, "篇章玩家", ResidentProfession.StoreClerk,
                            ResidentPreference.Saver);
                        ProgressionService.OnDaySettled(state);
                        break;
                    case 4:
                        Assert.IsTrue(GovernmentSimulator.RunFreeRiderExperiment(state));
                        ProgressionService.OnDaySettled(state);
                        break;
                    case 5:
                        ProgressionService.RecordSignal(state, ProgressSignals.MacroOpened,
                            "打开宏观仪表盘", "查看 GDP、物价与就业");
                        break;
                    case 6:
                        ProgressionService.RecordSignal(state, ProgressSignals.MoneyFunctionsExperienced,
                            "从以物易物到货币", "用货币完成交换");
                        break;
                    case 7:
                        state.exportChannelEnabled = true;
                        int capacity = CostService.Capacity(state).actualCapacity;
                        state.planProduction = Mathf.Max(0, capacity - 45);
                        SettleOpenDay(state);
                        TradeDayRecord trade = InternationalTradeService.LastRecord(state);
                        Assert.NotNull(trade, "第七篇首日必须写入贸易账本。");
                        Assert.Greater(trade.playerExportUnits, 0f, "第七篇须由真实产能余量产生首单出口。");
                        break;
                    case 8:
                        SettleOpenDay(state);
                        Assert.Greater(state.history[state.history.Count - 1].rentPaid, 0f,
                            "第八篇首日必须真实支付租金。");
                        break;
                }

                Assert.AreEqual(part.startChapter + 1, state.progression.currentChapter,
                    part.Title + " 完成篇首真实行为后应进入下一章。");
                Assert.AreEqual(part.startChapter, state.progression.completedChapter, part.Title);

                Object.DestroyImmediate(worlds[worlds.Count - 1]);
                worlds.RemoveAt(worlds.Count - 1);
                GameState.I = null;
            }
        }

        static void SettleOpenDay(GameState state)
        {
            state.phase = GamePhase.Morning;
            int produced = DaySimulator.Production(state);
            if (state.CompetitionActive)
            {
                CompetitorAI.PrepareDay(state);
                CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), produced);
            }
            else DaySimulator.Settle(state, DaySimulator.RollDemand(state), produced);
            state.phase = GamePhase.Report;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blockedReason),
                blockedReason);
        }

        [Test]
        public void StoryAndChapterSaveFiles_AreFullyIsolated()
        {
            GameState state = NewState("SlotIsolation");
            SaveSystem.UseSlot(SaveSlot.Story);
            state.day = 3;
            state.money = 3333f;
            SaveSystem.Save(state);
            string storyPath = SaveSystem.SavePathFor(SaveSlot.Story);
            string storyJson = File.ReadAllText(storyPath);

            SaveSystem.UseSlot(SaveSlot.ChapterPlay);
            state.day = 17;
            state.money = 1717f;
            state.chapterPlayEntryPart = 6;
            state.progression.currentChapter = 31;
            state.progression.completedChapter = 30;
            SaveSystem.Save(state);

            string chapterPath = SaveSystem.SavePathFor(SaveSlot.ChapterPlay);
            Assert.AreNotEqual(storyPath, chapterPath);
            Assert.IsTrue(File.Exists(storyPath));
            Assert.IsTrue(File.Exists(chapterPath));
            Assert.AreEqual(storyJson, File.ReadAllText(storyPath),
                "写篇章档不得改动正式剧情主档。");

            SaveSystem.Delete(SaveSlot.ChapterPlay);
            Assert.IsFalse(SaveSystem.HasSave(SaveSlot.ChapterPlay));
            Assert.IsTrue(SaveSystem.HasSave(SaveSlot.Story), "删除篇章档不得连带删除正式剧情。");

            SaveSystem.UseSlot(SaveSlot.Story);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(3, state.day);
            Assert.AreEqual(3333f, state.money, .01f);
            Assert.AreEqual(0, state.chapterPlayEntryPart);
        }
    }
}

#endif
