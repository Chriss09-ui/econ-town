using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public struct ProductionCapacity
    {
        public int laborPotentialCapacity;
        public int equipmentCapacity;
        public int actualCapacity;
        public int actualMarginalProduct;
        public bool equipmentIsBottleneck;
    }

    public struct CostBreakdown
    {
        public float effectiveInputUnitCost;
        public float materialCost;
        public float wageCost;
        public float fixedCost;
        public float marketingCost;
        public float strategicCost;
        public float totalCost;
        public float cashCostAfterPrepaidInputs;
    }

    public static class CostService
    {
        public static ProductionCapacity Capacity(GameState state)
        {
            int employees = state == null ? 0 : state.employees;
            int labor = Eco.Capacity(employees);
            int previousLabor = Eco.Capacity(Mathf.Max(0, employees - 1));
            int equipment = Eco.EquipmentCap + (state != null ? Mathf.Max(0, state.financedEquipmentCapacityBonus) : 0);
            int actual = Mathf.Min(labor, equipment);
            int previousActual = Mathf.Min(previousLabor, equipment);
            if (state != null)
            {
                state.EnsureEconomyState();
                state.economy.labor.employedWorkers = employees;
                state.economy.labor.effectiveDailyWage = GovernmentSimulator.EffectiveDailyWage(state);
                state.economy.labor.laborPotentialCapacity = labor;
                state.economy.labor.equipmentCapacity = equipment;
                state.economy.labor.actualCapacity = actual;
                state.economy.labor.actualMarginalProduct = Mathf.Max(0, actual - previousActual);
            }
            return new ProductionCapacity
            {
                laborPotentialCapacity = labor,
                equipmentCapacity = equipment,
                actualCapacity = actual,
                actualMarginalProduct = Mathf.Max(0, actual - previousActual),
                equipmentIsBottleneck = labor > equipment
            };
        }

        public static float EffectiveInputUnitCost(GameState state)
        {
            if (state == null) return Eco.MaterialUnitCost;
            float qualityCost = CompetitionEco.UnitCostForLevel(state.playerQualityLevel);
            // V0.7:中间投入含进口份额,本币贬值(汇率相对锚上行)放大采购成本——
            // 汇率经由这一个因子穿透采购价/marginalCost/库存估值,禁止在结算点另写乘法。
            return qualityCost * state.materialPriceMul *
                MacroEconomySimulator.SupplyCostFeedback(state) *
                InternationalPriceService.IntermediateImportFactor(state);
        }

        public static CostBreakdown Calculate(GameState state, int produced, bool includeCompetitionCosts)
        {
            float unitCost = EffectiveInputUnitCost(state);
            float material = Mathf.Max(0, produced) * unitCost;
            float wage = (state == null ? 0 : state.employees) * GovernmentSimulator.EffectiveDailyWage(state);
            float fixedCost = Eco.DailyRent;
            float marketing = includeCompetitionCosts && state != null ? Mathf.Max(0f, state.marketingBudget) : 0f;
            float strategic = includeCompetitionCosts && state != null ? Mathf.Max(0f, state.todayStrategicSpend) : 0f;
            return new CostBreakdown
            {
                effectiveInputUnitCost = unitCost,
                materialCost = material,
                wageCost = wage,
                fixedCost = fixedCost,
                marketingCost = marketing,
                strategicCost = strategic,
                totalCost = material + wage + fixedCost + marketing + strategic,
                cashCostAfterPrepaidInputs = wage + fixedCost + marketing
            };
        }
    }

}
