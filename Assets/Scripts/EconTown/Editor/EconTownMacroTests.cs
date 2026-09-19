#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownMacroTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("MacroTestState");
            state = stateObject.AddComponent<GameState>();
            state.government = new GovernmentState();
            state.resident = new ResidentState();
            state.macro = new MacroEconomyState();
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            MacroEconomySimulator.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void GdpIdentityUsesFourExpenditureComponents()
        {
            Assert.AreEqual(1110f, MacroEconomySimulator.IdentityGdp(700f, 200f, 160f, 90f, 40f), .001f);
        }

        [Test]
        public void ValueAddedAvoidsIntermediateGoodsDoubleCounting()
        {
            Assert.AreEqual(10f, MacroEconomySimulator.ValueAddedGdp(2f, 5f, 10f), .001f);
            Assert.That(MacroEconomySimulator.ValueAddedGdp(2f, 5f, 10f), Is.Not.EqualTo(17f).Within(.001f));
        }

        [Test]
        public void BeveragePriceIsOnlyFivePercentOfCpiBasket()
        {
            float cpi = MacroEconomySimulator.WeightedCpi(100f, 100f, 100f, 140f, 100f, 100f);
            Assert.AreEqual(102f, cpi, .001f, "饮品单项上涨 40% 不应被误报成总体通胀 40%。");
        }

        [Test]
        public void LaborAccountingExcludesPeopleOutsideLaborForce()
        {
            state.government.population = 320;
            MacroPeriodRecord record = AdvanceMacroDay();
            Assert.AreEqual(record.employment + record.unemployment, record.laborForce);
            Assert.LessOrEqual(record.laborForce, record.workingAgePopulation);
            Assert.Less(record.workingAgePopulation, record.population);
        }

        [Test]
        public void EnterpriseAndResidentConsumptionAreNotAddedTwice()
        {
            state.resident.history.Add(new ResidentDayRecord { spending = 50f, income = 70f });
            state.history.Add(new DayRecord { sold = 100, price = 20f, consumerPrice = 20f, revenue = 2000f });
            MacroPeriodRecord withLargeEnterpriseSales = AdvanceMacroDay();

            state.macro = new MacroEconomyState();
            MacroEconomySimulator.Ensure(state);
            state.history.Clear();
            state.history.Add(new DayRecord { sold = 1, price = 20f, consumerPrice = 20f, revenue = 20f });
            MacroPeriodRecord withSmallEnterpriseSales = AdvanceMacroDay();

            Assert.AreEqual(withLargeEnterpriseSales.consumption, withSmallEnterpriseSales.consumption, .01f,
                "已有居民最终消费账本时，企业销售只能交叉核对，不能再次加进 C。" );
        }

        [Test]
        public void EnergyCrisisCreatesStagflationDirection()
        {
            AdvanceMacroDay();
            float baselineEnergy = state.macro.energyCostIndex;
            MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);
            MacroPeriodRecord crisis = AdvanceMacroDay();

            Assert.Greater(state.macro.energyCostIndex, baselineEnergy);
            Assert.Greater(crisis.inflation, 0f);
            Assert.Greater(crisis.unemploymentRate, 0f);
            StringAssert.Contains("失业↑", crisis.causalChain);
            StringAssert.Contains("通胀↑", crisis.causalChain);
        }

        [Test]
        public void FiscalExpansionHasMultiplierAndImplementationLag()
        {
            MacroEconomySimulator.SetFiscalPolicy(state, FiscalStance.Expansionary, 1000f, 0f);
            Assert.Greater(state.macro.fiscalMultiplier, 1f);
            StringAssert.Contains("时滞", state.macro.policyPreview);
            int before = state.macro.fiscalLagStage;
            AdvanceMacroDay();
            Assert.Greater(state.macro.fiscalLagStage, before);
            Assert.Less(state.government.budgetBalance, 0f);
            Assert.Less(state.government.cash, 12000f,
                "政府应先用现有现金覆盖赤字，现金耗尽后才新增债务。");
            Assert.Greater(state.macro.deficitToGdp, 0f);
        }

        [Test]
        public void SaveModelContainsMacroEconomyState()
        {
            MacroEconomySimulator.QueueShock(state, MacroShockType.AiRevolution);
            string json = JsonUtility.ToJson(state.macro);
            MacroEconomyState restored = JsonUtility.FromJson<MacroEconomyState>(json);
            Assert.NotNull(restored);
            Assert.AreEqual(MacroShockType.AiRevolution, restored.pendingShock);
            Assert.AreEqual(state.macro.marginalPropensityToConsume, restored.marginalPropensityToConsume, .001f);
        }

        [Test]
        public void ConsumptionContractionFlowsThroughSalesJobsIncomeAndGdp()
        {
            MacroPeriodRecord strong = RunScenario(72f, MacroShockType.None, FiscalStance.Neutral);
            float strongNextDemand = MacroEconomySimulator.DemandFeedback(state);
            MacroPeriodRecord weak = RunScenario(24f, MacroShockType.None, FiscalStance.Neutral);
            float weakNextDemand = MacroEconomySimulator.DemandFeedback(state);

            Assert.Less(weak.consumption, strong.consumption);
            Assert.Less(weak.realGdp, strong.realGdp);
            Assert.Greater(weak.unemploymentRate, strong.unemploymentRate);
            Assert.Less(weakNextDemand, strongNextDemand,
                "失业和信心必须反馈到下一营业日的企业需求。");
        }

        [Test]
        public void RecessionRescueRaisesOrdersJobsIncomeAndGdpWithFiscalCost()
        {
            MacroPeriodRecord neutral = RunScenario(30f, MacroShockType.None, FiscalStance.Neutral);
            MacroPeriodRecord rescue = RunScenario(30f, MacroShockType.None, FiscalStance.Expansionary);

            Assert.Greater(rescue.governmentSpending, neutral.governmentSpending);
            Assert.Greater(rescue.consumption, neutral.consumption);
            Assert.Greater(rescue.realGdp, neutral.realGdp);
            Assert.LessOrEqual(rescue.unemploymentRate, neutral.unemploymentRate);
            Assert.Greater(rescue.deficitToGdp, neutral.deficitToGdp);
            Assert.GreaterOrEqual(rescue.debtToGdp, neutral.debtToGdp,
                "有财政现金时扩张可先消耗现金，不应被宏观模块强制改写成新增债务。");
        }

        [Test]
        public void EnergySupplyShockLowersOutputAndRaisesUnemploymentAndInflation()
        {
            MacroPeriodRecord baseline = RunScenario(52f, MacroShockType.None, FiscalStance.Neutral);
            MacroPeriodRecord shock = RunScenario(52f, MacroShockType.EnergyCrisis, FiscalStance.Neutral);

            Assert.Less(shock.realGdp, baseline.realGdp);
            Assert.Greater(shock.unemploymentRate, baseline.unemploymentRate);
            Assert.Greater(shock.inflation, baseline.inflation);
            StringAssert.Contains("滞胀", shock.causalChain);
        }

        [Test]
        public void AdAsGeometryUsesCurrentOutputPriceAndPotentialCapacity()
        {
            state.macro.realGdp = 12340f;
            state.macro.potentialGdp = 15000f;
            state.macro.aggregateSupply = 13200f;
            state.macro.cpi = 114f;
            state.macro.energyCostIndex = 145f;
            state.macro.consumerConfidence = 44f;
            state.macro.productivity = 92f;

            AdAsGeometry geometry = MacroEconomyGraph.CalculateAdAsGeometry(state.macro);

            Assert.AreEqual(state.macro.realGdp, geometry.equilibriumOutput, .001f);
            Assert.AreEqual(state.macro.cpi, geometry.equilibriumPrice, .001f);
            Assert.AreEqual(state.macro.potentialGdp, geometry.potentialOutput, .001f);
            Assert.AreEqual(state.macro.aggregateSupply, geometry.aggregateSupplyOutput, .001f);
            Assert.Less(geometry.outputMin, geometry.equilibriumOutput);
            Assert.Greater(geometry.outputMax, geometry.potentialOutput);
            Assert.Greater(geometry.adPriceAtMinOutput, geometry.adPriceAtMaxOutput,
                "AD 必须随实际产出增加而向下倾斜。");
            Assert.Less(geometry.srasPriceAtMinOutput, geometry.srasPriceAtMaxOutput,
                "SRAS 必须随实际产出增加而向上倾斜。");
        }

        [Test]
        public void NineChaptersCanUnlockAllThirtyMacroCardsThroughChoices()
        {
            RunScenario(52f, MacroShockType.None, FiscalStance.Neutral);
            for (int period = 2; period <= 9; period++)
            {
                if (period == 6) MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);
                if (period == 7) MacroEconomySimulator.SetFiscalPolicy(state, FiscalStance.Expansionary, 1000f, -2f);
                if (period == 8) MacroEconomySimulator.SetFiscalPolicy(state, FiscalStance.Contractionary, 800f, 2f);
                if (period == 9) MacroEconomySimulator.QueueShock(state, MacroShockType.AiRevolution);
                AdvanceMacroDay();
            }
            string[] ids =
            {
                "gdp", "valueadded", "nominalgdp", "realgdp", "percapitagdp", "consumption", "investment",
                "governmentspending", "netexports", "cpi", "inflation", "unemploymentrate",
                "frictionalunemployment", "structuralunemployment", "cyclicalunemployment", "businesscycle",
                "potentialgdp", "outputgap", "aggregatedemand", "aggregatesupply", "demandshock", "supplyshock",
                "stagflation", "expansionaryfiscal", "contractionaryfiscal", "fiscalmultiplier", "mpc",
                "automaticstabilizer", "productivity", "longrungrowth"
            };
            for (int i = 0; i < ids.Length; i++)
                Assert.IsTrue(state.unlockedCards.Contains(ids[i]), "九章流程未解锁宏观卡：" + ids[i]);
        }

        [Test]
        public void DisasterReconstructionCanRaiseGdpWhileResidentWelfareFalls()
        {
            MacroPeriodRecord baseline = RunScenario(52f, MacroShockType.None, FiscalStance.Neutral);
            float baselineHappiness = state.government.residentHappiness;
            MacroPeriodRecord disaster = RunScenario(52f, MacroShockType.Disaster, FiscalStance.Neutral);

            Assert.Greater(disaster.governmentSpending, baseline.governmentSpending);
            Assert.Greater(disaster.nominalGdp, baseline.nominalGdp,
                "灾后重建属于最终政府购买，可能机械推高 GDP。");
            Assert.Less(state.government.residentHappiness, baselineHappiness,
                "GDP 上升不能掩盖灾害造成的福利损失。");
        }

        MacroPeriodRecord RunScenario(float residentSpending, MacroShockType shock, FiscalStance stance)
        {
            state.history.Clear();
            state.resident = new ResidentState();
            ResidentEconomy.Ensure(state);
            state.resident.history.Add(new ResidentDayRecord { day = 1, income = 70f, spending = residentSpending });
            state.government = new GovernmentState();
            GovernmentSimulator.Ensure(state);
            state.macro = new MacroEconomyState();
            MacroEconomySimulator.Ensure(state);
            state.history.Add(new DayRecord
            {
                day = 1, sold = Mathf.RoundToInt(residentSpending * 1.5f), price = 15f, consumerPrice = 15f,
                revenue = residentSpending * 20f, profit = residentSpending * 4f, materialCost = residentSpending * 5f,
                capitalInvestment = 120f, exports = 70f, imports = 90f
            });
            if (stance == FiscalStance.Expansionary)
                MacroEconomySimulator.SetFiscalPolicy(state, stance, 1500f, -2f);
            if (shock != MacroShockType.None) MacroEconomySimulator.QueueShock(state, shock);
            return AdvanceMacroDay();
        }

        MacroPeriodRecord AdvanceMacroDay()
        {
            MacroPeriodRecord record = MacroEconomySimulator.AdvancePeriod(state);
            FiscalPolicyService.AdvanceLag(state.macro);
            state.day++;
            return record;
        }
    }
}
#endif
