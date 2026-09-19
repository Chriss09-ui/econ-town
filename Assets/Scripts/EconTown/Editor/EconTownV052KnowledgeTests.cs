#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV052KnowledgeTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V052KnowledgeTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.knowledge = new KnowledgeProgressState();
            KnowledgeSystem.Ensure(state);
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV052KnowledgeTests_" + System.Guid.NewGuid().ToString("N"));
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

        [Test]
        public void EveryCardHasUniqueStableIdAndMetadata()
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < CardLibrary.All.Count; i++)
            {
                CardDef card = CardLibrary.All[i];
                Assert.IsFalse(string.IsNullOrEmpty(card.id));
                Assert.IsTrue(ids.Add(card.id), "卡片稳定 ID 重复：" + card.id);
                Assert.IsNotNull(KnowledgeSystem.GetMetadata(card.id), "卡片缺少分类元数据：" + card.id);
            }
            Assert.AreEqual(CardLibrary.All.Count, ids.Count, "总数必须动态来自 CardLibrary，而不是写死历史数字。");
        }

        [Test]
        public void SearchFindsElasticityAcrossMultipleCategories()
        {
            List<CardDef> results = KnowledgeSystem.Search(state, "弹性", "全部");
            CollectionAssert.IsSubsetOf(new[] { "elasticity", "crosselasticity", "taxelasticity" },
                results.ConvertAll(card => card.id));
            Assert.Contains(CardLibrary.Get("elasticity"), KnowledgeSystem.Search(state, "弹性", "市场"));
            Assert.Contains(CardLibrary.Get("taxelasticity"), KnowledgeSystem.Search(state, "弹性", "政府"));
        }

        [Test]
        public void PrimaryCategoryCountsAlwaysSumToDynamicLibraryCount()
        {
            int total = 0;
            string[] categories = { "企业", "消费者", "市场", "政府", "宏观", "金融", "国际", "资产" };
            for (int i = 0; i < categories.Length; i++) total += KnowledgeSystem.Search(state, "", categories[i]).Count;
            Assert.AreEqual(CardLibrary.All.Count, total);
        }

        [Test]
        public void KnowledgeStatusMovesFromUndiscoveredToExperimented()
        {
            Assert.AreEqual(KnowledgeStatus.Undiscovered, KnowledgeSystem.Status(state, "elasticity"));
            state.UnlockCard("elasticity");
            Assert.AreEqual(KnowledgeStatus.Discovered, KnowledgeSystem.Status(state, "elasticity"));
            KnowledgeSystem.MarkUnderstood(state, "elasticity");
            Assert.AreEqual(KnowledgeStatus.Understood, KnowledgeSystem.Status(state, "elasticity"));
            KnowledgeSystem.MarkExperimented(state, "elasticity");
            Assert.AreEqual(KnowledgeStatus.Experimented, KnowledgeSystem.Status(state, "elasticity"));
        }

        [Test]
        public void PlayerElasticityCaseUsesOwnHistory()
        {
            state.history.Add(new DayRecord { day = 7, price = 15f, listedPrice = 15f, desiredDemand = 100, demandTotal = 100, sold = 100 });
            state.history.Add(new DayRecord { day = 8, price = 18f, listedPrice = 18f, desiredDemand = 80, demandTotal = 80, sold = 80 });
            KnowledgeSystem.SyncPlayerCases(state);
            KnowledgeCaseRecord ownCase = KnowledgeSystem.GetCase(state, "elasticity");
            Assert.IsNotNull(ownCase);
            Assert.AreEqual(8, ownCase.worldDay);
            StringAssert.Contains("15", ownCase.summary);
            StringAssert.Contains("18", ownCase.summary);
            StringAssert.Contains("弧弹性", ownCase.summary);
        }

        [Test]
        public void KnowledgeStateAndCasesSurviveSaveRoundTrip()
        {
            state.UnlockCard("inflation");
            KnowledgeSystem.MarkUnderstood(state, "inflation");
            KnowledgeSystem.MarkExperimented(state, "elasticity");
            state.macro.history.Add(new MacroPeriodRecord { period = 1, cpi = 105f, inflation = 5f });
            KnowledgeSystem.SyncPlayerCases(state);
            SaveSystem.Save(state);

            state.knowledge = new KnowledgeProgressState();
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(KnowledgeStatus.Understood, KnowledgeSystem.Status(state, "inflation"));
            Assert.AreEqual(KnowledgeStatus.Experimented, KnowledgeSystem.Status(state, "elasticity"));
            Assert.IsNotNull(KnowledgeSystem.GetCase(state, "inflation"));
        }
    }
}

#endif
