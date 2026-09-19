#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV052SandboxTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V052SandboxTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.day = 42;
            state.money = 4321f;
            state.progression.sandboxUnlocked = true;
            state.macro.realGdp = 12000f;
            state.macro.potentialGdp = 12500f;
            state.macro.unemploymentRate = 6.5f;
            state.macro.inflation = 3.2f;
            state.macro.cpi = 108f;
            state.macro.governmentSpending = 2200f;
            state.macro.fiscalMultiplier = 1.4f;
            state.government.cash = 18000f;
            state.government.debt = 2500f;
            state.economy.random.Reset(20260825);
            state.history.Add(new DayRecord { day = 41, profit = 128f });
            SandboxService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV052SandboxTests_" + System.Guid.NewGuid().ToString("N"));
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
        public void ReleaseContainsAtLeastThreePlayableChallenges()
        {
            Assert.GreaterOrEqual(ChallengeService.All.Count, 3);
            for (int i = 0; i < ChallengeService.All.Count; i++)
            {
                ChallengeDefinition challenge = ChallengeService.All[i];
                Assert.IsFalse(string.IsNullOrEmpty(challenge.id));
                Assert.Greater(challenge.days, 0);
                Assert.AreEqual(challenge.actionIds.Length, challenge.actionLabels.Length);
                Assert.GreaterOrEqual(challenge.actionIds.Length, 2);
            }
        }

        [Test]
        public void IdenticalScenariosProduceIdenticalOutcomes()
        {
            ScenarioSettings settings = new ScenarioSettings
            {
                governmentSpendingChangePercent = 12f,
                playerPriceChangePercent = -4f,
                taxRateChangePoints = 1.5f,
                minimumWageChangePercent = 3f,
                supplyInvestmentPercent = 8f
            };
            ScenarioComparisonRecord result = SandboxService.RunComparison(state, settings, settings, 18);

            AssertOutcomesEqual(result.outcomeA, result.outcomeB);
            Assert.AreEqual(result.outcomeA.randomState, result.outcomeB.randomState);
            Assert.AreEqual(result.outcomeA.randomCalls, result.outcomeB.randomCalls);
        }

        [Test]
        public void DifferentScenariosStillConsumeTheSameRandomSequence()
        {
            ScenarioComparisonRecord result = SandboxService.RunComparison(state,
                new ScenarioSettings { label = "不干预" },
                new ScenarioSettings
                {
                    label = "积极财政与供给投资",
                    governmentSpendingChangePercent = 25f,
                    supplyInvestmentPercent = 15f
                }, 30);

            Assert.AreEqual(result.outcomeA.randomState, result.outcomeB.randomState);
            Assert.AreEqual(result.outcomeA.randomCalls, result.outcomeB.randomCalls);
            Assert.AreEqual(60, result.outcomeA.randomCalls);
            Assert.AreNotEqual(result.outcomeA.realGdp, result.outcomeB.realGdp);
        }

        [Test]
        public void ExperimentDoesNotMutateTheMainWorldEconomy()
        {
            int day = state.day;
            float money = state.money;
            float gdp = state.macro.realGdp;
            float debt = state.government.debt;
            int historyCount = state.history.Count;
            int randomState = state.economy.random.state;
            long randomCalls = state.economy.random.calls;

            SandboxService.RunComparison(state, new ScenarioSettings(),
                new ScenarioSettings { governmentSpendingChangePercent = 50f }, 30);

            Assert.AreEqual(day, state.day);
            Assert.AreEqual(money, state.money);
            Assert.AreEqual(gdp, state.macro.realGdp);
            Assert.AreEqual(debt, state.government.debt);
            Assert.AreEqual(historyCount, state.history.Count);
            Assert.AreEqual(randomState, state.economy.random.state);
            Assert.AreEqual(randomCalls, state.economy.random.calls);
        }

        [Test]
        public void ChallengeActionsStayInsideTheChallengeWorld()
        {
            float money = state.money;
            float gdp = state.macro.realGdp;
            float governmentCash = state.government.cash;
            int randomState = state.economy.random.state;

            ChallengeRunState run = ChallengeService.Start(state, "price_war");
            Assert.IsNotNull(run);
            ChallengeService.ApplyAction(state, "differentiate");

            Assert.AreEqual(money, state.money);
            Assert.AreEqual(gdp, state.macro.realGdp);
            Assert.AreEqual(governmentCash, state.government.cash);
            Assert.AreEqual(randomState, state.economy.random.state);
            Assert.AreEqual(1, state.sandbox.activeChallenge.elapsedDays);
        }

        [Test]
        public void SandboxArchiveAndActiveChallengeSurviveSaveRoundTrip()
        {
            SandboxService.RunComparison(state, new ScenarioSettings(),
                new ScenarioSettings { taxRateChangePoints = 3f }, 7);
            ChallengeService.Start(state, "high_inflation");
            ChallengeService.ApplyAction(state, "supply");
            SaveSystem.Save(state);

            state.sandbox = new SandboxState();
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(1, state.sandbox.comparisons.Count);
            Assert.AreEqual(7, state.sandbox.comparisons[0].runDays);
            Assert.IsNotNull(state.sandbox.activeChallenge);
            Assert.AreEqual("high_inflation", state.sandbox.activeChallenge.challengeId);
            Assert.AreEqual(1, state.sandbox.activeChallenge.elapsedDays);
        }

        static void AssertOutcomesEqual(ScenarioOutcome a, ScenarioOutcome b)
        {
            Assert.AreEqual(a.realGdp, b.realGdp);
            Assert.AreEqual(a.unemploymentRate, b.unemploymentRate);
            Assert.AreEqual(a.inflation, b.inflation);
            Assert.AreEqual(a.governmentDebt, b.governmentDebt);
            Assert.AreEqual(a.businessProfit, b.businessProfit);
            Assert.AreEqual(a.residentPurchasingPower, b.residentPurchasingPower);
        }
    }
}

#endif
