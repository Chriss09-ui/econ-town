#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownGovernmentTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("GovernmentTestState");
            state = stateObject.AddComponent<GameState>();
            state.materials = 1000;
            state.planProduction = 120;
            state.employees = 2;
            state.price = 15f;
            state.competitor = new CompetitorState();
            GovernmentSimulator.Ensure(state);
            DaySimulator.SetRandomSeed(20260823);
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void DefaultGovernmentKeepsExistingMarketBaseline()
        {
            Assert.AreEqual(0f, GovernmentSimulator.PolicyValue(state, PolicyType.DrinkTax), .001f);
            Assert.AreEqual(15f, GovernmentSimulator.DrinkConsumerPrice(state, 15f), .001f);
            Assert.AreEqual(15f, GovernmentSimulator.DrinkProducerPrice(state, 15f), .001f);
            Assert.AreEqual(Eco.WagePerEmployee, GovernmentSimulator.EffectiveDailyWage(state), .001f);
            Assert.AreEqual(12000f, state.government.cash, .001f);
        }

        [Test]
        public void UnitTaxSplitsBurdenAndLowersDemand()
        {
            int demandBefore = DaySimulator.ExpectedDemand(state, ConsumerType.WhiteCollar);
            Assert.IsTrue(GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f));
            float consumerPrice = GovernmentSimulator.DrinkConsumerPrice(state, 15f);
            float producerPrice = GovernmentSimulator.DrinkProducerPrice(state, 15f);
            int demandAfter = DaySimulator.ExpectedDemand(state, ConsumerType.WhiteCollar);

            Assert.Greater(consumerPrice, 15f);
            Assert.Less(producerPrice, 15f);
            Assert.AreEqual(3f, consumerPrice - producerPrice, .001f);
            Assert.Less(demandAfter, demandBefore);
            Assert.Greater(GovernmentSimulator.ConsumerTaxShare(state), .5f);
        }

        [Test]
        public void TaxSettlementCreatesRevenueAndDeadweightLoss()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            DayRecord record = DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));

            Assert.AreEqual(record.sold * 3f, record.salesTaxRevenue, .001f);
            Assert.AreEqual(record.salesTaxRevenue, state.government.pendingDrinkTaxRevenue, .001f);
            Assert.Greater(record.consumerPrice, record.producerNetPrice);
            Assert.GreaterOrEqual(record.deadweightLoss, 0f);
            Assert.AreEqual(record.salesTaxPerUnit,
                record.consumerTaxBurden + record.producerTaxBurden, .001f);
        }

        [Test]
        public void DrinkSubsidyLowersConsumerPriceAndCostsGovernment()
        {
            int before = DaySimulator.ExpectedDemand(state, ConsumerType.Student);
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkSubsidy, 2f);
            int after = DaySimulator.ExpectedDemand(state, ConsumerType.Student);
            DayRecord record = DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));

            Assert.Less(record.consumerPrice, state.price);
            Assert.Greater(record.producerNetPrice, record.consumerPrice);
            Assert.Greater(after, before);
            Assert.AreEqual(record.sold * 2f, state.government.pendingSubsidySpending, .001f);
        }

        [Test]
        public void MinimumWageHasThresholdInsteadOfFixedUnemploymentResult()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.MinimumWage, 25f);
            float baselineWage = GovernmentSimulator.EffectiveDailyWage(state);
            int baselineSupply = GovernmentSimulator.ApplySupplyResponse(state, 100, state.price);
            GovernmentSimulator.SetPolicy(state, PolicyType.MinimumWage, 40f);
            float highWage = GovernmentSimulator.EffectiveDailyWage(state);
            int highWageSupply = GovernmentSimulator.ApplySupplyResponse(state, 100, state.price);

            Assert.AreEqual(Eco.WagePerEmployee, baselineWage, .001f,
                "最低工资没有超过原工资时不应产生机械冲击。");
            Assert.Greater(highWage, baselineWage);
            Assert.Less(highWageSupply, baselineSupply);
        }

        [Test]
        public void BindingPriceCeilingCreatesShortage()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.PriceCeiling, 10f);
            DaySimulator.DemandRoll demand = DaySimulator.RollDemand(state);
            int produced = DaySimulator.Production(state);
            DayRecord record = DaySimulator.Settle(state, demand, produced);

            Assert.AreEqual(10f, record.consumerPrice, .001f);
            Assert.Greater(demand.Total, produced);
            Assert.Greater(record.policyShortage, 0);
        }

        [Test]
        public void BindingPriceFloorCreatesObservableSurplus()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.PriceFloor, 30f);
            DayRecord record = DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));

            Assert.AreEqual(30f, record.consumerPrice, .001f);
            Assert.Greater(record.produced, record.sold);
            Assert.AreEqual(record.produced - record.sold, record.policySurplus);
            Assert.AreEqual(0, record.policyShortage);
        }

        [Test]
        public void PollutionTaxReducesFactoryProductionAndPollution()
        {
            GovernmentCycleRecord baseline = AdvanceGovernmentDay();
            float baselineProduction = baseline.factoryProduction;
            float baselinePollution = baseline.factoryPollution;
            GovernmentSimulator.SetPolicy(state, PolicyType.PollutionTax, 4f);
            GovernmentCycleRecord taxed = AdvanceGovernmentDay();

            Assert.Less(taxed.factoryProduction, baselineProduction);
            Assert.Less(taxed.factoryPollution, baselinePollution);
            Assert.Greater(taxed.pollutionTaxRevenue, 0f);
        }

        [Test]
        public void GreenSubsidyEventuallyInstallsCleanerEquipment()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.GreenEquipmentSubsidy, 50f);
            for (int i = 0; i < 3; i++) AdvanceGovernmentDay();

            Assert.IsTrue(state.government.factory.greenEquipmentInstalled);
            Assert.AreEqual(100f, state.government.factory.greenUpgradeProgress, .001f);
            Assert.Less(state.government.factory.pollution,
                state.government.factory.production * .6f);
        }

        [Test]
        public void FreeRiderExperimentFailsWithoutCollectiveFunding()
        {
            Assert.IsTrue(GovernmentSimulator.RunFreeRiderExperiment(state));

            Assert.AreEqual(6, state.government.freeRiderContributors);
            Assert.AreEqual(300f, state.government.freeRiderFundsRaised, .001f);
            Assert.Less(state.government.freeRiderFundsRaised, 1000f);
            Assert.AreEqual(2, state.government.chapter);
            Assert.IsTrue(state.unlockedCards.Contains("freerider"));
        }

        [Test]
        public void GovernmentCanUseTaxFundsToBuildNonExcludableService()
        {
            float cashBefore = state.government.cash;
            Assert.IsTrue(GovernmentSimulator.BuildProject(state, PublicProjectType.StreetLights));

            PublicProjectState lights = state.government.projects.Find(x => x.type == PublicProjectType.StreetLights);
            Assert.IsTrue(lights.built);
            // bug搜#5:拨款只挂账不即时扣现金,由收日结算一次(旧行为:即时扣+结算再扣=双扣)。
            Assert.AreEqual(cashBefore, state.government.cash, .001f,
                "建设拨款不得即时扣现金——记账与扣款都发生在收日结算,否则双计。");
            Assert.Greater(state.government.pendingCapitalSpending, 0f);
            Assert.Greater(state.government.publicServiceLevel, 55f);
            GovernmentCycleRecord record = AdvanceGovernmentDay();
            Assert.AreEqual(cashBefore + record.budgetBalance, state.government.cash, .01f,
                "现金只经收日结算变动一次:结算后现金=建设前现金+本期余额(造价恰含一次)。");
        }

        [Test]
        public void GreenFacilityReducesPollutionIntensityBeyondTaxAlone()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.PollutionTax, 3f);
            AdvanceGovernmentDay();
            float before = state.government.factory.pollution / state.government.factory.production;
            Assert.IsTrue(GovernmentSimulator.BuildProject(state, PublicProjectType.GreenFacility));
            AdvanceGovernmentDay();
            float after = state.government.factory.pollution / state.government.factory.production;

            Assert.Less(after, before);
            Assert.Greater(state.government.environmentQuality, 0f);
        }

        [Test]
        public void DeficitIsFlowAndDebtIsAccumulatedStock()
        {
            state.government.cash = 100f;
            GovernmentSimulator.SetPolicy(state, PolicyType.BasicWelfare, 100f);
            GovernmentSimulator.SetPolicy(state, PolicyType.PublicServiceBudget, 1000f);
            GovernmentCycleRecord first = AdvanceGovernmentDay();
            float firstDebt = state.government.debt;
            GovernmentCycleRecord second = AdvanceGovernmentDay();

            Assert.Less(first.budgetBalance, 0f);
            Assert.Greater(firstDebt, 0f);
            Assert.Greater(second.debtEnd, firstDebt);
            Assert.AreNotEqual(second.budgetBalance, second.debtEnd,
                "赤字是当期流量，债务是累计存量，不能显示成同一个数。" );
        }

        [Test]
        public void PolicyImpactAdvancesThroughThreeObservationStages()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            PolicyImpactRecord impact = state.government.impacts[state.government.impacts.Count - 1];
            Assert.AreEqual(1, impact.currentStage);

            AdvanceGovernmentDay();
            Assert.AreEqual(1, impact.currentStage);
            AdvanceGovernmentDay();
            Assert.AreEqual(2, impact.currentStage);
            AdvanceGovernmentDay();
            Assert.AreEqual(3, impact.currentStage);
        }

        [Test]
        public void ResidentAndEnterpriseUseTheSameTaxAdjustedDrinkPrice()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            float governmentPrice = GovernmentSimulator.DrinkConsumerPrice(state, state.price);
            float residentPrice = ResidentEconomy.Price(state, ResidentItemKind.PlayerJuice);

            Assert.AreEqual(governmentPrice, residentPrice, .001f);
            Assert.Greater(governmentPrice, state.price);
        }

        [Test]
        public void GovernmentLedgerAggregatesBothCompetingStores()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            DayRecord playerRecord = new DayRecord { price = state.price, profit = 100f, demandTotal = 50, sold = 40 };
            GovernmentSimulator.RecordDrinkSettlement(state, playerRecord, 40, 45);
            GovernmentSimulator.RecordAdditionalDrinkSales(state, 30);

            Assert.AreEqual(70f * 3f, state.government.pendingDrinkTaxRevenue, .001f);
            Assert.AreEqual(40f * 3f, playerRecord.salesTaxRevenue, .001f,
                "企业日报只展示本店税额，政府账本汇总整个饮品市场。" );
        }

        [Test]
        public void MainChaptersProgressFromActionsInsteadOfFixedDays()
        {
            Assert.AreEqual(1, state.government.chapter);
            GovernmentSimulator.RunFreeRiderExperiment(state);
            GovernmentSimulator.SetPolicy(state, PolicyType.PollutionTax, 3f);
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            GovernmentSimulator.SetPolicy(state, PolicyType.MinimumWage, 30f);
            GovernmentSimulator.SetPolicy(state, PolicyType.PriceCeiling, 15f);

            Assert.AreEqual(6, state.government.chapter);
            state.government.cash = 0f;
            GovernmentSimulator.SetPolicy(state, PolicyType.BasicWelfare, 100f);
            AdvanceGovernmentDay();
            Assert.AreEqual(7, state.government.chapter);
            Assert.Greater(state.government.debt, 0f);
        }

        GovernmentCycleRecord AdvanceGovernmentDay()
        {
            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            state.day++;
            return record;
        }
    }
}
#endif
