#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV051CoreTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V051CoreTestState");
            state = stateObject.AddComponent<GameState>();
            state.materials = 1000;
            state.planProduction = 120;
            state.employees = 2;
            state.price = 15f;
            state.competitor = new CompetitorState();
            GovernmentSimulator.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            DaySimulator.SetRandomSeed(20260824);
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void ZeroProductionCanSellInventoryAndStillChargesFixedCosts()
        {
            state.money = 2000f;
            state.inventory = 20;
            state.employees = 1;
            DaySimulator.DemandRoll demand = new DaySimulator.DemandRoll { students = 10 };

            DayRecord record = DaySimulator.Settle(state, demand, 0);

            Assert.AreEqual(10, record.sold);
            Assert.AreEqual(0f, record.materialCost, .001f);
            Assert.AreEqual(Eco.DailyRent, record.rentCost, .001f);
            Assert.AreEqual(Eco.WagePerEmployee, record.wageCost, .001f);
            Assert.AreEqual(2000f + record.revenue - Eco.DailyRent - Eco.WagePerEmployee,
                state.money, .001f);
        }

        [Test]
        public void ClosedShopStillPaysRentAndCommittedWages()
        {
            state.money = 2000f;
            state.employees = 2;

            DayRecord record = DaySimulator.SettleClosedDay(state);

            Assert.IsFalse(record.shopOpen);
            Assert.AreEqual(0, record.actualSales);
            Assert.AreEqual(Eco.DailyRent + Eco.WagePerEmployee * 2f, -record.profit, .001f);
            Assert.AreEqual(2000f + record.profit, state.money, .001f);
        }

        [Test]
        public void CompetitionClosedDayStillSettlesCompetitorAndKeepsOneRecordPerDay()
        {
            state.day = 16;
            state.money = 2000f;
            state.employees = 2;

            DayRecord record = CompetitionSimulator.SettleClosedDay(state);

            Assert.IsFalse(record.shopOpen);
            Assert.IsTrue(record.competitionActive);
            Assert.AreEqual(0, record.actualSales);
            Assert.AreEqual(Eco.DailyRent + Eco.WagePerEmployee * 2f, -record.profit, .001f);
            // 玩家关门不能让对手也"消失":对手照常销售并更新份额与产量计划输入。
            Assert.Greater(record.competitorSold, 0);
            Assert.AreEqual(record.competitorSold, state.competitor.lastSales);
            Assert.AreEqual(0f, record.playerMarketShare, .001f);
            Assert.AreEqual(1, state.history.Count);

            // 同一天的重复结算必须被兜底拦截,一天一条企业记录。
            CompetitionSimulator.SettleClosedDay(state);
            Assert.AreEqual(1, state.history.Count);
        }

        [Test]
        public void LaborMarginalProductIsVisibleBeforeEquipmentBecomesTheBottleneck()
        {
            state.employees = 3;
            ProductionCapacity threeWorkers = CostService.Capacity(state);
            state.employees = 5;
            ProductionCapacity fiveWorkers = CostService.Capacity(state);
            state.employees = 6;
            ProductionCapacity sixWorkers = CostService.Capacity(state);

            Assert.AreEqual(50, threeWorkers.actualMarginalProduct);
            Assert.AreEqual(15, fiveWorkers.actualMarginalProduct);
            Assert.IsTrue(fiveWorkers.equipmentIsBottleneck);
            Assert.AreEqual(0, sixWorkers.actualMarginalProduct);
            Assert.AreEqual(Eco.EquipmentCap, sixWorkers.actualCapacity);
        }

        [Test]
        public void NonBindingCeilingNeverMislabelsAnOperationalStockout()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.PriceCeiling, 30f);
            state.inventory = 0;
            state.planProduction = 0;
            DaySimulator.DemandRoll demand = new DaySimulator.DemandRoll { students = 50 };

            DayRecord record = DaySimulator.Settle(state, demand, 0);

            Assert.IsFalse(record.priceCeilingBinding);
            Assert.AreEqual(0, record.policyShortage);
            Assert.AreEqual(50, record.operationalStockout);
        }

        [Test]
        public void NonBindingPriceFloorDoesNotCreatePolicySurplus()
        {
            Assert.IsTrue(GovernmentSimulator.SetPolicy(state, PolicyType.PriceFloor, 5f));
            state.planProduction = 120;

            DayRecord record = DaySimulator.Settle(state,
                new DaySimulator.DemandRoll { students = 20 }, DaySimulator.Production(state));

            Assert.IsFalse(record.priceFloorBinding);
            Assert.AreEqual(0, record.policySurplus);
            Assert.Greater(record.operationalSurplus, 0,
                "企业自己生产过多必须只记为经营性过剩。");
        }

        [Test]
        public void PriceFloorCannotExceedPriceCeiling()
        {
            Assert.IsTrue(GovernmentSimulator.SetPolicy(state, PolicyType.PriceCeiling, 20f));

            Assert.IsFalse(GovernmentSimulator.SetPolicy(state, PolicyType.PriceFloor, 25f));
            Assert.AreEqual(0f, GovernmentSimulator.PolicyValue(state, PolicyType.PriceFloor), .001f);
            StringAssert.Contains("不能高于", GovernmentSimulator.PolicyConflictReason(
                state, PolicyType.PriceFloor, 25f));
        }

        [Test]
        public void TaxIncidenceReadsTheUnifiedConsumerElasticity()
        {
            PriceQuote quote = PriceService.Quote(state, state.price);
            float expectedDemandElasticity = ConsumerType.Student.share * ConsumerType.Student.elasticity +
                ConsumerType.WhiteCollar.share * ConsumerType.WhiteCollar.elasticity +
                ConsumerType.Tourist.share * ConsumerType.Tourist.elasticity;

            Assert.AreEqual(expectedDemandElasticity, quote.demandElasticity, .001f);
            Assert.AreEqual(quote.supplyElasticity /
                (quote.supplyElasticity + expectedDemandElasticity), quote.consumerTaxShare, .001f);
            Assert.AreEqual(quote.consumerTaxShare, GovernmentSimulator.ConsumerTaxShare(state), .001f);
        }

        [Test]
        public void CapacityBottleneckIsExplicitlyReportedToThePlayer()
        {
            state.employees = 6;
            ProductionCapacity capacity = CostService.Capacity(state);
            string explanation = Explainer.ExplainEmployees(state.employees);

            Assert.IsTrue(capacity.equipmentIsBottleneck);
            Assert.AreEqual(0, capacity.actualMarginalProduct);
            StringAssert.Contains("设备已经成为瓶颈", explanation);
            StringAssert.Contains("实际 +0", explanation);
        }

        [Test]
        public void MacroDemandFeedbackContinuesAfterSunnyTeaOpens()
        {
            state.day = 6;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            state.inventory = 1000;
            state.macro.consumerConfidence = 92f;
            state.macro.unemploymentRate = 2f;
            CompetitionSimulator.ProfitEstimate strong = CompetitionSimulator.Estimate(
                state, state.price, state.competitor.price);

            state.macro.consumerConfidence = 25f;
            state.macro.unemploymentRate = 24f;
            CompetitionSimulator.ProfitEstimate weak = CompetitionSimulator.Estimate(
                state, state.price, state.competitor.price);

            Assert.Greater(strong.playerSold, weak.playerSold);
            Assert.Greater(strong.competitorSold, weak.competitorSold);
        }

        [Test]
        public void CompetitorRequiresEnterprisePartCompletion()
        {
            state.day = 30;
            ConsumerChoiceResult beforeOpening = ConsumerDemandService.Evaluate(state,
                ConsumerType.WhiteCollar, state.price, state.competitor.price,
                true, 1f, 1f, 1f);

            Assert.IsFalse(state.CompetitionActive);
            Assert.AreEqual(0, beforeOpening.competitor,
                "调用方即使误传 competitorOpen=true，也必须服从唯一 MarketState。");

            state.progression.completedChapter = 5;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            ConsumerChoiceResult afterOpening = ConsumerDemandService.Evaluate(state,
                ConsumerType.WhiteCollar, state.price, state.competitor.price,
                true, 1f, 1f, 1f);

            Assert.IsTrue(state.CompetitionActive,
                "Sunny Tea 应由企业篇完成开启，而不是由第 16 天开启。");
            Assert.Greater(afterOpening.competitor, 0);
        }

        [Test]
        public void GovernmentAndMacroReadTheSameLaborMarketSnapshot()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.MinimumWage, 40f);
            MacroPeriodRecord macro = MacroEconomySimulator.AdvancePeriod(state);

            Assert.IsTrue(state.economy.labor.minimumWageBinding);
            Assert.AreEqual(GovernmentSimulator.EffectiveDailyWage(state) / 8f, macro.averageWage, .001f);
            Assert.AreEqual(state.economy.labor.employment, macro.employment);
            Assert.AreEqual(state.economy.labor.unemploymentRate, macro.unemploymentRate, .001f);
            Assert.AreEqual(100f - macro.unemploymentRate, state.government.employmentRate, .001f);
        }

        [Test]
        public void DemandRecordCarriesTheActualCausalBreakdown()
        {
            state.day = 16;
            state.demandMul = 1.2f;
            state.macro.consumerConfidence = 90f;
            state.macro.unemploymentRate = 3f;

            DayRecord record = CompetitionSimulator.Settle(state,
                CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));

            Assert.NotNull(record.demandEffects);
            Assert.AreEqual(1.2f, record.demandEffects.eventMultiplier, .001f);
            Assert.AreEqual(MacroEconomySimulator.DemandFeedback(state),
                record.demandEffects.macroMultiplier, .001f);
            Assert.AreEqual(record.desiredDemand, record.demandTotal);
            Assert.AreEqual(record.actualSales, record.sold);
            Assert.IsTrue(record.demandEffects.IsBalanced,
                "基础需求与各项数量影响必须可加总为最终意愿需求。");
            Assert.AreEqual(record.desiredDemand, record.demandEffects.ModeledTotal);
        }

        [Test]
        public void SimultaneousDemandShocksUseTheSettledSampleAndRemainAdditive()
        {
            state.day = 20;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            state.price = 22f;
            state.competitor.price = 11f;
            state.demandMul = .72f;
            state.studentMul = .65f;
            state.macro.consumerConfidence = 30f;
            state.macro.unemploymentRate = 18f;
            state.playerQualityLevel = 2;
            state.playerBrand = 42f;
            state.marketingBudget = 100f;
            state.competitor.quality = 82f;
            state.competitor.brand = 74f;
            state.competitor.marketing = 300f;
            state.economy.random.Reset(20260824);

            CompetitionSimulator.MarketRoll roll = CompetitionSimulator.RollMarket(state);
            DayRecord record = CompetitionSimulator.Settle(state, roll, DaySimulator.Production(state));
            DemandEffectBreakdown effects = record.demandEffects;

            Assert.NotNull(effects);
            Assert.AreEqual(record.desiredDemand, effects.desiredDemand);
            Assert.AreEqual(record.desiredDemand, effects.ModeledTotal,
                "当天随机样本、对手缺货转入与所有需求影响都必须在同一份账本中守恒。");
            Assert.Less(effects.eventEffect + effects.segmentEffect, 0);
            Assert.Less(effects.macroEffect, 0);
            Assert.Less(effects.ownPriceEffect, 0);
            Assert.Less(effects.competitorPriceEffect, 0);
            Assert.AreNotEqual(0, effects.randomEffect,
                "归因必须保存本次结算已使用的随机样本，而不是事后用均值重算。");
            StringAssert.Contains("自身价格", Explainer.CompactDemandEffects(effects, 12));
            StringAssert.Contains("模型估计", string.Join("\n", Explainer.ExplainSales(state, record).paragraphs));
        }

        [Test]
        public void ExtremePricesAllowConsumersToChooseNoPurchase()
        {
            state.day = 16;
            ConsumerChoiceResult choice = ConsumerDemandService.Evaluate(state, ConsumerType.Student,
                1000f, 1000f, true, 1f, 1f, 1f);

            Assert.Greater(choice.outside, choice.player + choice.competitor);
        }

        [Test]
        public void ElasticityUsesMidpointArcFormula()
        {
            DayRecord first = new DayRecord { price = 10f, demandTotal = 100 };
            DayRecord second = new DayRecord { price = 20f, demandTotal = 50 };

            Assert.AreEqual(-1f, DaySimulator.Elasticity(first, second), .001f);
        }

        [Test]
        public void ReloadingRandomStateReplaysTheSameWorldDayRoll()
        {
            state.economy.random.Reset(54321);
            string snapshot = JsonUtility.ToJson(state.economy);
            DaySimulator.DemandRoll first = DaySimulator.RollDemand(state);

            state.economy = JsonUtility.FromJson<EconomyState>(snapshot);
            DaySimulator.DemandRoll replay = DaySimulator.RollDemand(state);

            Assert.AreEqual(first.students, replay.students);
            Assert.AreEqual(first.whitecollars, replay.whitecollars);
            Assert.AreEqual(first.tourists, replay.tourists);
        }

        [Test]
        public void TemporaryResidentPriceModifierExpiresWithoutChangingPlayerPrice()
        {
            float basePrice = state.price;
            state.economy.SetTemporaryPlayerPrice(8f, state.day, "test");

            Assert.AreEqual(basePrice + 8f, PriceService.PlayerListedPrice(state), .001f);
            Assert.AreEqual(basePrice, state.price, .001f);

            state.day++;
            Assert.AreEqual(basePrice, PriceService.PlayerListedPrice(state), .001f);
            Assert.AreEqual(0f, state.economy.playerTemporaryPriceDelta, .001f);
        }

        [Test]
        public void WelfareUsesOnePerResidentEligibilityAndLedgerDefinition()
        {
            state.resident.profileCreated = true;
            state.resident.employedToday = false;
            state.resident.cash = 100f;
            state.resident.cashAtDayStart = 100f;
            state.macro.outputGap = 0f;
            GovernmentSimulator.SetPolicy(state, PolicyType.BasicWelfare, 20f);

            // 2026-08-24 C4:财政按"首批覆盖 25%"(32 人)支出;符合者统计仍是低收入占比(128 人)。
            int eligible = GovernmentSimulator.WelfareEligibleResidents(state);
            int enrolled = GovernmentSimulator.WelfareEnrolledResidents(state);
            Assert.Less(enrolled, eligible, "首批覆盖人数必须少于符合条件人数(覆盖率 25%)。");
            float perResident = GovernmentSimulator.WelfarePerEligibleResident(state);
            ResidentDayRecord residentRecord = ResidentEconomy.EndDay(state);

            Assert.AreEqual(GovernmentSimulator.PolicyValue(state, PolicyType.BasicWelfare), perResident, .001f);
            Assert.AreEqual(enrolled * perResident, GovernmentSimulator.ProjectedWelfareSpending(state), .001f,
                "预算口径:首批覆盖人数 × 每人金额(全额覆盖会让最低档就超出固定收入的 5 倍)。");
            Assert.AreEqual(perResident, residentRecord.income, .001f);
            Assert.AreEqual(100f + perResident, residentRecord.cashEnd, .001f);
        }

        [Test]
        public void GovernmentPreviewAndSettlementUseTheSameCompleteBudget()
        {
            state.government.pendingDrinkTaxRevenue = 120f;
            state.government.pendingPollutionTaxRevenue = 80f;
            state.government.pendingSubsidySpending = 40f;
            state.government.pendingCapitalSpending = 300f;
            GovernmentSimulator.SetPolicy(state, PolicyType.BasicWelfare, 25f);
            GovernmentSimulator.SetPolicy(state, PolicyType.PublicServiceBudget, 100f);

            GovernmentBudgetPreview preview = GovernmentSimulator.PreviewBudget(state);
            GovernmentCycleRecord settled = GovernmentSimulator.AdvanceCycle(state);

            Assert.AreEqual(preview.totalIncome, settled.taxRevenue, .001f);
            Assert.AreEqual(preview.totalSpending, settled.spending, .001f);
            Assert.AreEqual(preview.balance, settled.budgetBalance, .001f);
            Assert.AreEqual(preview.welfareSpending, settled.welfareSpending, .001f);
        }

        [Test]
        public void StudyRaisesFutureIncomeAndSavingsProvideUnemploymentBuffer()
        {
            state.resident.profileCreated = true;
            float skillBefore = state.resident.skillLevel;
            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);
            Assert.Greater(state.resident.skillLevel, skillBefore);

            state.resident.reportReady = true;
            state.resident.cash = 0f;
            state.resident.savings = 50f;
            // 围猎三(#22 复审):应急取款走双边记账并钳偿付能力——储蓄须有银行镜像背书
            // (照 V091 MirrorDeposits 先例,与真实对局同构)。
            state.bank.householdDeposits = 50f;
            state.bank.reserves += 50f;
            state.day++;
            state.macro.cyclePhase = MacroCyclePhase.Recession;
            state.economy.labor.unemploymentRate = 100f;
            state.economy.random.Reset(42);
            ResidentEconomy.BeginNextDay(state);

            Assert.IsFalse(state.resident.employedToday);
            Assert.AreEqual(30f, state.resident.todayEmergencyWithdrawal, .001f);
            Assert.AreEqual(30f, state.resident.cash, .001f);
            Assert.AreEqual(20f, state.resident.savings, .001f);
        }

        [Test]
        public void ThreeDistressDaysBlockAdvancementUntilCrisisDecision()
        {
            state.money = -1001f;
            for (int day = 1; day <= 3; day++)
            {
                DaySimulator.SettleClosedDay(state);
                if (day < 3)
                    Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state,
                        out int _, out string reason), reason);
            }

            Assert.IsTrue(state.economy.financialCrisisActive);
            if (state.government.currentEvent != null) state.government.currentEvent.resolved = true;
            Assert.IsFalse(WorldSettlementService.TrySettleAndAdvance(state,
                out int _, out string blockedReason));
            StringAssert.Contains("财务危机", blockedReason);

            FinancialHealthService.Resolve(state);
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state,
                out int _, out string resolvedReason), resolvedReason);
        }

        [Test]
        public void CrossElasticityCardUnlocksFromTwoValidObservationsWithoutRandomLuck()
        {
            state.day = 17;
            state.history.Add(new DayRecord
            {
                day = 16, competitionActive = true, price = 15f, competitorPrice = 18f,
                desiredDemand = 80, competitorDemand = 70
            });
            state.history.Add(new DayRecord
            {
                day = 17, competitionActive = true, price = 16f, competitorPrice = 18f,
                desiredDemand = 75, competitorDemand = 76
            });

            CardLibrary.CheckAll(state);

            Assert.IsTrue(state.unlockedCards.Contains("crosselasticity"));
        }

        [Test]
        public void OneSettlementWritesEverySystemToTheSameWorldDay()
        {
            state.resident.profileCreated = true;
            DaySimulator.Settle(state, new DaySimulator.DemandRoll { students = 10 }, 10);

            bool advanced = WorldSettlementService.TrySettleAndAdvance(state,
                out int _, out string blockedReason);

            Assert.IsTrue(advanced, blockedReason);
            Assert.AreEqual(2, state.day);
            Assert.AreEqual(2, state.resident.lifeDay);
            Assert.AreEqual(2, state.government.cycle);
            Assert.AreEqual(2, state.macro.period);
            Assert.AreEqual(1, state.LastRecord.day);
            Assert.AreEqual(1, state.resident.history[state.resident.history.Count - 1].day);
            Assert.AreEqual(1, state.government.history[state.government.history.Count - 1].cycle);
            Assert.AreEqual(1, state.macro.history[state.macro.history.Count - 1].period);
        }

        [Test]
        public void SameWorldDayCannotBeSettledTwiceByGovernmentOrMacro()
        {
            GovernmentSimulator.AdvanceCycle(state);
            GovernmentSimulator.AdvanceCycle(state);
            MacroEconomySimulator.AdvancePeriod(state);
            MacroEconomySimulator.AdvancePeriod(state);

            Assert.AreEqual(1, state.government.history.Count);
            Assert.AreEqual(1, state.macro.history.Count);
        }

        [Test]
        public void SpoilageBelongsToTheNewDayAndNeverBackwritesYesterday()
        {
            state.inventory = 10;
            DayRecord yesterday = DaySimulator.Settle(state, new DaySimulator.DemandRoll(), 0);

            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state,
                out int spoiled, out string blockedReason), blockedReason);
            DayRecord today = DaySimulator.SettleClosedDay(state);

            Assert.AreEqual(2, spoiled);
            Assert.AreEqual(0, yesterday.spoiled);
            Assert.AreEqual(2, today.spoiled);
        }

        [Test]
        public void MacroReadsButNeverRewritesTheSettledGovernmentLedger()
        {
            GovernmentSimulator.AdvanceCycle(state);
            float cash = state.government.cash;
            float debt = state.government.debt;
            float revenue = state.government.taxRevenue;
            float spending = state.government.spending;
            float balance = state.government.budgetBalance;

            MacroEconomySimulator.AdvancePeriod(state);

            Assert.AreEqual(cash, state.government.cash, .001f);
            Assert.AreEqual(debt, state.government.debt, .001f);
            Assert.AreEqual(revenue, state.government.taxRevenue, .001f);
            Assert.AreEqual(spending, state.government.spending, .001f);
            Assert.AreEqual(balance, state.government.budgetBalance, .001f);
            MacroEconomySimulator.SetFiscalPolicy(state, FiscalStance.Expansionary, 1000f, -12f);
            Assert.AreEqual(0f, state.macro.plannedTaxChange, .001f);
        }

        [Test]
        public void PlayerAndCompetitorSatisfactionUseTheSameServiceContract()
        {
            state.day = 16;
            state.playerQualityLevel = 2;
            state.playerBrand = 55f;
            state.competitor.quality = 70f;
            state.competitor.brand = 55f;
            state.inventory = 500;
            state.competitor.inventory = 500;

            CompetitionSimulator.MarketRoll market = CompetitionSimulator.RollMarket(state);
            DayRecord record = CompetitionSimulator.Settle(state, market, DaySimulator.Production(state));
            float expectedPlayer = SatisfactionService.Calculate(record.consumerPrice,
                record.desiredDemand, record.actualSales, state.PlayerQuality, 55f);
            float expectedCompetitor = SatisfactionService.Calculate(
                GovernmentSimulator.DrinkConsumerPrice(state, state.competitor.price),
                market.competitor.Total + record.switchedToCompetitor,
                record.competitorSold, state.competitor.EffectiveQuality, 55f);

            Assert.AreEqual(expectedPlayer, record.satisfaction, .001f);
            Assert.AreEqual(expectedCompetitor, state.competitor.satisfaction, .001f);
        }

        [Test]
        public void MacroKeepsGovernmentEnterpriseConfidenceAsTheSingleAuthority()
        {
            GovernmentSimulator.AdvanceCycle(state);
            float authoritativeConfidence = state.government.enterpriseConfidence;
            state.macro.businessConfidence = 5f;

            MacroPeriodRecord record = MacroEconomySimulator.AdvancePeriod(state);

            Assert.AreEqual(authoritativeConfidence, state.government.enterpriseConfidence, .001f,
                "宏观聚合不得反向覆盖主体层企业信心。");
            Assert.AreEqual(authoritativeConfidence, state.macro.businessConfidence, .001f);
            Assert.AreEqual(authoritativeConfidence, record.businessConfidence, .001f);
        }

        [Test]
        public void RecessionUnemploymentCanRemoveResidentDailyIncome()
        {
            state.resident.profileCreated = true;
            ResidentEconomy.EndDay(state);
            state.day++;
            state.macro.cyclePhase = MacroCyclePhase.Recession;
            state.macro.outputGap = 0f;
            state.economy.labor.unemploymentRate = 100f;
            state.economy.random.Reset(42);
            ResidentEconomy.BeginNextDay(state);

            ResidentDayRecord unemployedDay = ResidentEconomy.EndDay(state);

            Assert.IsFalse(state.resident.employedToday);
            Assert.AreEqual(0f, unemployedDay.income, .001f);
            Assert.Greater(state.resident.unemployedDays, 0);
        }
    }
}
#endif
