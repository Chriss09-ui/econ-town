using UnityEngine;

namespace EconTown
{
    /// <summary>独立反事实模拟器。只读取主世界快照，结果写入实验档案，不调用主世界结算管线。</summary>
    public static class SandboxService
    {
        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.sandbox == null) state.sandbox = new SandboxState();
            if (state.sandbox.comparisons == null) state.sandbox.comparisons = new System.Collections.Generic.List<ScenarioComparisonRecord>();
            if (state.sandbox.completedChallengeIds == null) state.sandbox.completedChallengeIds = new System.Collections.Generic.List<string>();
            if (state.progression != null && state.progression.sandboxUnlocked) state.sandbox.unlocked = true;
        }

        public static ScenarioComparisonRecord RunComparison(GameState state, ScenarioSettings scenarioA,
            ScenarioSettings scenarioB, int days)
        {
            if (state == null) return null;
            Ensure(state);
            MacroEconomySimulator.Ensure(state);
            GovernmentSimulator.Ensure(state);
            state.EnsureEconomyState();
            days = Mathf.Clamp(days, 1, 30);
            ScenarioSettings a = CloneSettings(scenarioA, "A", "Scenario A");
            ScenarioSettings b = CloneSettings(scenarioB, "B", "Scenario B");
            int sharedSeed = state.economy.random != null ? state.economy.random.seed : 20260825;
            int sharedState = state.economy.random != null ? state.economy.random.state : sharedSeed;
            long sharedCalls = state.economy.random != null ? state.economy.random.calls : 0;

            ScenarioComparisonRecord record = new ScenarioComparisonRecord
            {
                sourceWorldDay = state.day,
                runDays = days,
                sharedSeed = sharedSeed,
                scenarioA = a,
                scenarioB = b,
                outcomeA = RunScenario(state, a, days, sharedSeed, sharedState, sharedCalls),
                outcomeB = RunScenario(state, b, days, sharedSeed, sharedState, sharedCalls)
            };
            state.sandbox.experimentMode = true;
            state.sandbox.scenarioRunCount++;
            state.sandbox.comparisons.Add(record);
            if (state.sandbox.comparisons.Count > 20) state.sandbox.comparisons.RemoveAt(0);
            KnowledgeSystem.MarkExperimented(state, "gdp", "unemploymentrate", "inflation", "outputgap");
            state.Changed();
            return record;
        }

        public static ScenarioComparisonRecord LastComparison(GameState state)
        {
            Ensure(state);
            return state == null || state.sandbox.comparisons.Count == 0
                ? null : state.sandbox.comparisons[state.sandbox.comparisons.Count - 1];
        }

        static ScenarioOutcome RunScenario(GameState source, ScenarioSettings settings, int days,
            int seed, int randomState, long randomCalls)
        {
            DeterministicRandomState random = new DeterministicRandomState
            {
                seed = seed,
                state = randomState,
                calls = randomCalls
            };
            float gdp = Mathf.Max(100f, source.macro.realGdp);
            float potential = Mathf.Max(gdp * .5f, source.macro.potentialGdp);
            float unemployment = Mathf.Clamp(source.macro.unemploymentRate, 0f, 100f);
            float inflation = Mathf.Clamp(source.macro.inflation, -20f, 50f);
            float debt = Mathf.Max(0f, source.government.debt);
            float profit = source.LastRecord != null ? source.LastRecord.profit : 0f;
            float purchasingPower = 100f / Mathf.Max(.2f, source.macro.cpi / 100f);
            float baseGovernmentSpending = Mathf.Max(1f, source.macro.governmentSpending);
            float multiplier = Mathf.Clamp(source.macro.fiscalMultiplier, .5f, 5f);

            for (int day = 0; day < days; day++)
            {
                float commonDemandNoise = (random.Next01() - .5f) * .012f;
                float commonSupplyNoise = (random.Next01() - .5f) * .009f;
                float spendingDelta = baseGovernmentSpending * settings.governmentSpendingChangePercent / 100f;
                float priceShock = settings.playerPriceChangePercent / 100f;
                float taxShock = settings.taxRateChangePoints / 100f;
                float wageShock = settings.minimumWageChangePercent / 100f;
                float supplyInvestment = potential * settings.supplyInvestmentPercent / 100f * .01f;

                potential = Mathf.Max(100f, potential * (1f + .0008f + commonSupplyNoise * .08f) + supplyInvestment);
                float fiscalDemand = spendingDelta * multiplier * .18f;
                float privateDemand = gdp * (commonDemandNoise - priceShock * .012f - taxShock * .01f);
                float wageCostDrag = gdp * Mathf.Max(0f, wageShock) * .003f;
                float target = gdp + fiscalDemand + privateDemand - wageCostDrag;
                float previousGdp = gdp;
                gdp = Mathf.Clamp(Mathf.Lerp(gdp, target, .42f), potential * .45f, potential * 1.35f);
                float growth = (gdp - previousGdp) / Mathf.Max(1f, previousGdp) * 100f;
                float gap = (gdp - potential) / potential * 100f;
                unemployment = Mathf.Clamp(unemployment - growth * .22f + Mathf.Max(0f, wageShock) * .10f, 0f, 40f);
                inflation = Mathf.Clamp(inflation + gap * .025f + settings.governmentSpendingChangePercent * .004f +
                    priceShock * .7f - settings.supplyInvestmentPercent * .012f, -10f, 40f);
                debt = Mathf.Clamp(debt + Mathf.Max(0f, spendingDelta) + Mathf.Max(0f, -taxShock * gdp * .003f), 0f, 100000000f);
                profit = Mathf.Clamp(profit + growth * 8f + priceShock * 45f - wageShock * 25f - taxShock * 20f,
                    -1000000f, 1000000f);
                purchasingPower = Mathf.Clamp(purchasingPower * (1f - inflation / 10000f) + growth * .02f, 10f, 300f);
            }

            return new ScenarioOutcome
            {
                realGdp = gdp,
                unemploymentRate = unemployment,
                inflation = inflation,
                governmentDebt = debt,
                businessProfit = profit,
                residentPurchasingPower = purchasingPower,
                randomState = random.state,
                randomCalls = random.calls
            };
        }

        static ScenarioSettings CloneSettings(ScenarioSettings source, string id, string fallbackLabel)
        {
            source = source ?? new ScenarioSettings();
            return new ScenarioSettings
            {
                id = string.IsNullOrEmpty(source.id) ? id : source.id,
                label = string.IsNullOrEmpty(source.label) ? fallbackLabel : source.label,
                governmentSpendingChangePercent = Mathf.Clamp(source.governmentSpendingChangePercent, -100f, 300f),
                playerPriceChangePercent = Mathf.Clamp(source.playerPriceChangePercent, -90f, 500f),
                taxRateChangePoints = Mathf.Clamp(source.taxRateChangePoints, -100f, 100f),
                minimumWageChangePercent = Mathf.Clamp(source.minimumWageChangePercent, -100f, 300f),
                supplyInvestmentPercent = Mathf.Clamp(source.supplyInvestmentPercent, 0f, 300f)
            };
        }
    }
}
