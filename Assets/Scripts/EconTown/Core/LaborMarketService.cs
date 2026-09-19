using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public struct LaborMarketSnapshot
    {
        public int workingAgePopulation;
        public int laborForce;
        public int employment;
        public int unemployment;
        public float unemploymentRate;
        public float effectiveDailyWage;
        public bool minimumWageBinding;
    }

    /// <summary>政府、宏观、居民与企业共同读取的劳动市场口径。</summary>
    public static class LaborMarketService
    {
        public static float EffectiveDailyWage(GameState state)
        {
            float minimumHourlyWage = GovernmentSimulator.PolicyValue(state, PolicyType.MinimumWage);
            return Mathf.Max(Eco.WagePerEmployee, minimumHourlyWage * 8f);
        }

        public static LaborMarketSnapshot Evaluate(GameState state, float outputGap, MacroShockType shock)
        {
            state.EnsureEconomyState();
            GovernmentSimulator.Ensure(state);
            int population = Mathf.Max(1, state.government.population);
            int workingAge = Mathf.RoundToInt(population * .78f);
            float confidence = state.macro == null ? 68f : state.macro.consumerConfidence;
            float participation = Mathf.Clamp(.88f + (confidence - 60f) * .001f, .78f, .93f);
            int laborForce = Mathf.RoundToInt(workingAge * participation);
            float minimumHourlyWage = GovernmentSimulator.PolicyValue(state, PolicyType.MinimumWage);
            float baselineHourlyWage = Eco.WagePerEmployee / 8f;
            bool binding = minimumHourlyWage > baselineHourlyWage + .001f;
            float wageIncrease = Mathf.Max(0f, minimumHourlyWage - baselineHourlyWage);
            float profitBuffer = Mathf.Clamp01((state.government.averageEnterpriseProfit + 300f) / 1200f);
            float wagePenalty = binding ? wageIncrease * .55f * (1.15f - profitBuffer) : 0f;
            float gapPenalty = Mathf.Max(0f, -outputGap) * .55f;
            float shockPenalty = shock == MacroShockType.EnergyCrisis ? 4f : shock == MacroShockType.Disaster ? 3f : 0f;
            float employmentRate = Mathf.Clamp(94f - gapPenalty - shockPenalty - wagePenalty, 68f, 97f);
            int employment = Mathf.Clamp(Mathf.RoundToInt(laborForce * employmentRate / 100f), 0, laborForce);
            LaborMarketSnapshot snapshot = new LaborMarketSnapshot
            {
                workingAgePopulation = workingAge,
                laborForce = laborForce,
                employment = employment,
                unemployment = Mathf.Max(0, laborForce - employment),
                unemploymentRate = laborForce > 0 ? (laborForce - employment) * 100f / laborForce : 0f,
                effectiveDailyWage = EffectiveDailyWage(state),
                minimumWageBinding = binding
            };
            LaborMarketState labor = state.economy.labor;
            labor.workingAgePopulation = snapshot.workingAgePopulation;
            labor.laborForce = snapshot.laborForce;
            labor.employment = snapshot.employment;
            labor.unemployment = snapshot.unemployment;
            labor.unemploymentRate = snapshot.unemploymentRate;
            labor.effectiveDailyWage = snapshot.effectiveDailyWage;
            labor.minimumWageBinding = snapshot.minimumWageBinding;
            return snapshot;
        }
    }

}
