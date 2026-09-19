using System;
using System.Collections.Generic;

namespace EconTown
{
    [Serializable]
    public class ScenarioSettings
    {
        public string id = "scenario";
        public string label = "情景";
        public float governmentSpendingChangePercent;
        public float playerPriceChangePercent;
        public float taxRateChangePoints;
        public float minimumWageChangePercent;
        public float supplyInvestmentPercent;
    }

    [Serializable]
    public class ScenarioOutcome
    {
        public float realGdp;
        public float unemploymentRate;
        public float inflation;
        public float governmentDebt;
        public float businessProfit;
        public float residentPurchasingPower;
        public int randomState;
        public long randomCalls;
    }

    [Serializable]
    public class ScenarioComparisonRecord
    {
        public int sourceWorldDay;
        public int runDays;
        public int sharedSeed;
        public bool experimentMode = true;
        public ScenarioSettings scenarioA;
        public ScenarioSettings scenarioB;
        public ScenarioOutcome outcomeA;
        public ScenarioOutcome outcomeB;
    }

    public class ChallengeDefinition
    {
        public string id;
        public string title;
        public string role;
        public string premise;
        public string goal;
        public int days;
        public string[] actionIds;
        public string[] actionLabels;
    }

    [Serializable]
    public class ChallengeRunState
    {
        public string challengeId;
        public bool active;
        public bool completed;
        public bool failed;
        public int elapsedDays;
        public int remainingDays;
        public float cash;
        public float rivalCash;
        public int inventory;
        public int employees;
        public float price;
        public float competitorPrice;
        public float quality;
        public float costIndex = 1f;
        public float lastCashFlow;
        public float inflation;
        public float unemployment;
        public float gdpGrowth;
        public float debtRatio;
        public string lastAction;
        public string lastSummary;
        public DeterministicRandomState random = new DeterministicRandomState();
    }

    [Serializable]
    public class SandboxState
    {
        public bool unlocked;
        public bool experimentMode;
        public int scenarioRunCount;
        public List<ScenarioComparisonRecord> comparisons = new List<ScenarioComparisonRecord>();
        public ChallengeRunState activeChallenge;
        public List<string> completedChallengeIds = new List<string>();
    }
}
