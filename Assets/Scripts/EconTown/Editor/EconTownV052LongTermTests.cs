#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV052LongTermTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V052LongTermTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.progression.completedChapter = ProgressionService.Chapters.Count;
            state.progression.currentChapter = ProgressionService.Chapters.Count + 1;
            state.progression.mainlineComplete = true;
            state.progression.sandboxUnlocked = true;
            state.economy.market.sunnyTea.isOpen = true;
            state.competitionIntroduced = true;
            state.economy.random.Reset(20260825);
            LongTermExperienceService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV052LongTermTests_" + System.Guid.NewGuid().ToString("N"));
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
        public void DifficultyChangesNoiseInsteadOfEconomicRules()
        {
            state.experience.settings.eventFrequency = 1f;
            state.experience.settings.difficulty = DifficultyMode.Teaching;
            float teaching = LongTermExperienceService.TownEventProbability(state);
            state.experience.settings.difficulty = DifficultyMode.Standard;
            float standard = LongTermExperienceService.TownEventProbability(state);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            float simulation = LongTermExperienceService.TownEventProbability(state);

            Assert.Less(teaching, standard);
            Assert.Less(standard, simulation);
            Assert.AreEqual(Eco.MaterialUnitCost, CostService.EffectiveInputUnitCost(state), .001f,
                "难度只改变噪声，不应偷偷改基础经济规则。");
        }

        [Test]
        public void EmptyLegacyLongTermEventDoesNotCreateBlankNewsUi()
        {
            state.experience.todayLongTermEvent = new LongTermEventRecord();
            state.experience.news.Add(new EconomicNewsItem());

            LongTermExperienceService.Ensure(state);

            Assert.IsNull(state.experience.todayLongTermEvent);
            Assert.IsEmpty(state.experience.news);
        }

        [Test]
        public void LoopEventsContinueAfterWorldDayOneHundredWithSpacing()
        {
            state.day = 101;
            state.resident.profileCreated = true;
            state.government.environmentQuality = 50f;
            state.government.lowestGroupPurchasingPower = 45f;
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            for (int day = 101; day <= 180; day++)
            {
                state.day = day;
                LongTermExperienceService.OnWorldDayStarted(state);
            }

            Assert.Greater(state.experience.totalLongTermEvents, 0);
            Assert.IsTrue(state.experience.eventHistory.Exists(item => item.worldDay > 100));
            for (int i = 1; i < state.experience.eventHistory.Count; i++)
                Assert.GreaterOrEqual(state.experience.eventHistory[i].worldDay -
                    state.experience.eventHistory[i - 1].worldDay, 2,
                    "长期事件至少间隔一天，不能连续弹窗轰炸。");
        }

        [Test]
        public void MetricNewsRequiresTheActualMetricThreshold()
        {
            state.macro.unemploymentRate = 9.2f;
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsTrue(state.experience.news.Exists(item => item.id == "metric.unemployment" &&
                item.headline.Contains("9.2")));

            state.experience.news.Clear();
            state.day += 8;
            state.macro.unemploymentRate = 4.5f;
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsFalse(state.experience.news.Exists(item => item.id == "metric.unemployment"));
        }

        [Test]
        public void EnterpriseCrisisNeedsTwoRealPositiveCashFlowDays()
        {
            state.money = -1200f;
            state.economy.financialCrisisActive = true;
            state.economy.financialDistressDays = 3;
            LongTermExperienceService.BeginEnterpriseRecovery(state, "tighten");
            Assert.IsTrue(state.experience.enterpriseRecovery.active);
            Assert.IsFalse(state.economy.financialCrisisActive);

            state.money = -900f;
            state.history.Add(new DayRecord { day = state.day, profit = 120f, moneyEnd = state.money });
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsTrue(state.experience.enterpriseRecovery.active);
            Assert.AreEqual(1, state.experience.enterpriseRecovery.positiveCashFlowStreak);

            state.day++;
            state.money = -650f;
            state.history.Add(new DayRecord { day = state.day, profit = 250f, moneyEnd = state.money });
            LongTermExperienceService.OnDaySettled(state);
            Assert.IsFalse(state.experience.enterpriseRecovery.active);
            Assert.IsNotNull(state.experience.pendingCrisisReview);
            StringAssert.Contains("连续 2 日", state.experience.pendingCrisisReview.result);
        }

        [Test]
        public void CriticalCheckpointPersistsReasonAndWorldState()
        {
            state.day = 77;
            state.money = 7654f;
            SaveSystem.SaveCheckpoint(state, "关键选择：测试事件");
            Assert.IsTrue(File.Exists(SaveSystem.CheckpointPath));

            state.day = 1;
            state.money = 1f;
            // V0.5.3 起 checkpoint 是独立于主档的可回滚副本（不再先覆盖主档），恢复走专用入口。
            Assert.IsTrue(SaveSystem.LoadCheckpoint(state));
            Assert.AreEqual(77, state.day);
            Assert.AreEqual(7654f, state.money);
            Assert.AreEqual("关键选择：测试事件", state.experience.lastCheckpointReason);
        }

        [Test]
        public void UnifiedWorldPipelineRemainsFiniteFor365WorldDays()
        {
            state.money = 10000000f;
            state.government.cash = 10000000f;
            state.materials = 100000;
            state.price = 15f;
            state.planProduction = 80;
            state.employees = 1;
            state.experience.settings.difficulty = DifficultyMode.Standard;
            state.experience.settings.eventFrequency = 1f;
            List<string> failures = new List<string>();

            for (int i = 0; i < 365; i++)
            {
                if (state.government.currentEvent != null && !state.government.currentEvent.resolved)
                    GovernmentSimulator.ResolveEvent(state, 1);
                if (state.economy.financialCrisisActive) FinancialHealthService.Resolve(state);
                state.phase = GamePhase.Morning;
                int settlingDay = state.day;
                float governmentCashBefore = state.government.cash;
                float governmentDebtBefore = state.government.debt;
                if (!WorldSettlementService.TrySettleAndAdvance(state, out int spoiled, out string blocked))
                {
                    failures.Add("Day " + settlingDay + " blocked: " + blocked);
                    break;
                }
                Assert.GreaterOrEqual(spoiled, 0);
                GovernmentCycleRecord government = state.government.history[state.government.history.Count - 1];
                Assert.AreEqual(government.cashEnd, state.government.cash, .01f,
                    "收日后不应有绕过政府账本的现金变动 @ Day " + settlingDay);
                Assert.AreEqual(government.budgetBalance + state.government.debt - governmentDebtBefore,
                    state.government.cash - governmentCashBefore, 1f,
                    "政府现金变化应等于预算结余加债务融资 @ Day " + settlingDay);
                AssertWorldFiniteAndBounded(state, settlingDay);
            }

            Assert.IsEmpty(failures);
            Assert.AreEqual(366, state.day);
            Assert.IsTrue(state.experience.eventHistory.Exists(item => item.worldDay > 100),
                "Day 100 后仍应持续出现循环事件。");
            Assert.Greater(state.experience.news.Count, 0);
        }

        static void AssertWorldFiniteAndBounded(GameState state, int day)
        {
            Assert.IsTrue(float.IsFinite(state.price), "price @ Day " + day);
            Assert.IsTrue(float.IsFinite(state.money), "money @ Day " + day);
            Assert.IsTrue(float.IsFinite(state.macro.realGdp), "GDP @ Day " + day);
            Assert.IsTrue(float.IsFinite(state.macro.inflation), "inflation @ Day " + day);
            Assert.IsTrue(float.IsFinite(state.macro.unemploymentRate), "unemployment @ Day " + day);
            Assert.IsTrue(float.IsFinite(state.government.debt), "debt @ Day " + day);
            Assert.That(state.price, Is.InRange(1f, 1000f));
            Assert.GreaterOrEqual(state.inventory, 0);
            Assert.GreaterOrEqual(state.materials, 0);
            Assert.GreaterOrEqual(state.government.population, 1);
            Assert.That(state.macro.realGdp, Is.InRange(1f, 100000000f));
            Assert.That(state.macro.unemploymentRate, Is.InRange(0f, 100f));
            Assert.That(state.government.debt, Is.InRange(0f, 100000000f));
            Assert.That(state.playerBrand, Is.InRange(0f, 100f));
            if (state.LastRecord != null && state.LastRecord.competitionActive)
            {
                Assert.That(state.LastRecord.playerMarketShare, Is.InRange(0f, 1f));
                Assert.That(state.LastRecord.competitorMarketShare, Is.InRange(0f, 1f));
            }
        }
    }
}

#endif
