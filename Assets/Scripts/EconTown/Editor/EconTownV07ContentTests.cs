#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.7 批次5:第七篇章节注册、国际知识分类、世界经济面板与教学内容实验。</summary>
    public class EconTownV07ContentTests
    {
        GameObject stateObject;
        GameState state;
        GameObject uiObject;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07ContentTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(555777);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07ContentTests_" + System.Guid.NewGuid().ToString("N"));
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
            if (uiObject != null) Object.DestroyImmediate(uiObject);
        }

        [Test]
        public void ChaptersRegisteredThrough58_TradePartSignalsLedgerDriven()
        {
            var chapters = ProgressionService.Chapters;
            Assert.AreEqual(58, chapters.Count, "主线应注册到第 58 章(V0.9.5 第八篇尾加)。");
            Assert.AreEqual(LearningPart.Trade, chapters[42].part, "第 43 章应属于第七篇。");
            Assert.AreEqual(LearningPart.Housing, chapters[53].part, "第 54 章应属于第八篇。");
            Assert.AreEqual(ProgressSignals.TrilemmaDemoDone, chapters[52].requiredSignal);

            // 行为驱动承诺:第七篇每个章节信号都必须以 trade. 前缀落账,
            // 由真实行为或两个实验室显式 RecordSignal 触发。
            for (int i = 42; i < 53; i++)
                StringAssert.StartsWith("trade.", chapters[i].requiredSignal,
                    "第 " + chapters[i].number + " 章的信号必须属于第七篇命名空间。");
            for (int i = 53; i < chapters.Count; i++)
                StringAssert.StartsWith("housing.", chapters[i].requiredSignal,
                    "第 " + chapters[i].number + " 章的信号必须属于第八篇命名空间。");
        }

        [Test]
        public void FullMainlineRun_CompletesWithEightStageReports_AndSandboxUnlock()
        {
            // 注入全部 58 个必答信号(等价于玩家真实完成所有行为),走 Evaluate 管线连过章。
            var chapters = ProgressionService.Chapters;
            for (int i = 0; i < chapters.Count; i++)
                ProgressionService.RecordSignal(state, chapters[i].requiredSignal);

            Assert.IsTrue(state.progression.mainlineComplete, "全信号注入后主线必须完成。");
            Assert.AreEqual(58, state.progression.completedChapter);
            Assert.IsTrue(state.sandbox.unlocked, "主线完成要即时传播沙盒解锁(P0-2 模式)。");
            Assert.AreEqual(8, state.progression.stageReports.Count, "八个篇界各出一份阶段报告。");
            StageReportRecord tradeReport = state.progression.stageReports.Find(
                r => r.part == LearningPart.Trade);
            Assert.IsNotNull(tradeReport, "第七篇报告必须存在。");
            StringAssert.Contains("比较优势", tradeReport.learnedSummary);
            StageReportRecord housingReport = state.progression.stageReports.Find(
                r => r.part == LearningPart.Housing);
            Assert.IsNotNull(housingReport, "V0.9.5 第八篇报告必须存在。");
            while (ProgressionService.GetPendingStageReport(state) != null)
                ProgressionService.MarkPendingStageReportViewed(state);
            Assert.AreEqual(0, state.progression.pendingStageReports.Count, "弹窗队列可被完整消费。");
        }

        [Test]
        public void Handbook_ListsInternationalCategory_WithWorldExperimentKey()
        {
            KnowledgeSystem.Ensure(state);
            int internationalCards = 0;
            foreach (CardDef def in CardLibrary.All)
            {
                KnowledgeMetadata meta = KnowledgeSystem.GetMetadata(def.id);
                if (def.id.StartsWith("intl."))
                {
                    internationalCards++;
                    Assert.AreEqual(KnowledgeCategory.International, meta.category,
                        def.id + " 必须归入国际分类。");
                    Assert.AreEqual("world", meta.experimentKey, def.id + " 应路由到世界经济面板实验。");
                }
            }
            Assert.GreaterOrEqual(internationalCards, 40, "PRD §78 要求至少 40 张国际卡。");

            // V0.6 遗留缺口回归:金融卡必须有路由目标,点"再试一次"不再白点。
            KnowledgeMetadata financeMeta = KnowledgeSystem.GetMetadata("policyrate");
            Assert.AreEqual("finance", financeMeta.experimentKey);
        }

        [Test]
        public void WorldTradePanel_OpensTabs_WhenUnlocked()
        {
            state.progression.completedChapter = InternationalTradeService.ExportUnlockChapter;
            uiObject = new GameObject("uiRoot");
            UIManager ui = uiObject.AddComponent<UIManager>();
            ui.InitializeForValidation();

            ui.ShowWorldEconomy(2);   // 外汇市场页
            Assert.IsNotNull(FindDescendant(ui.transform, "worldTab_2"), "tab 按钮结构存在。");
            ui.ShowWorldEconomy(3);
            Assert.IsNotNull(FindDescendant(ui.transform, "worldTab_3"));
        }

        [Test]
        public void MetricNews_ReserveWarningCarriesRealNumbers()
        {
            state.centralBank.forexReservesBCurrency = 210f;
            state.centralBank.forexReserveFloor = 120f;
            LongTermExperienceService.OnDaySettled(state);
            EconomicNewsItem found = state.experience.news.Find(n => n.id == "metric.reserveslow");
            Assert.IsNotNull(found, "外储预警新闻应生成。");
            StringAssert.Contains("210", found.cause + found.headline, "归因必须引用真实储备数字(P0 验收 §104)。");
        }

        [Test]
        public void ComparativeAdvantage_DefaultTable_BHasAdvantageDespiteAbsoluteDisadvantage()
        {
            // PRD §19/§20 默认表:A 饮品 1h / 机器 4h;B 饮品 3h / 机器 6h。
            float aMachineCost = 4f / 1f;      // A 做 1 台机器放弃 4 杯
            float bMachineCost = 6f / 3f;      // B 做 1 台机器放弃 2 杯
            Assert.Greater(aMachineCost, bMachineCost, "A 绝对优势在侧,但机器的机会成本反而更高。");
            Assert.AreEqual(4f, aMachineCost, .0001f);
            Assert.AreEqual(2f, bMachineCost, .0001f);
        }

        // —— 工具 ——
        static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}

#endif
