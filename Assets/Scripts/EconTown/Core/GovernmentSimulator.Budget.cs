using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>周期结算、预算预览、福利计量、工厂与城市指标。（partial 拆分自 GovernmentSimulator.cs,逻辑未变。）</summary>
    public static partial class GovernmentSimulator
    {
        public static GovernmentCycleRecord AdvanceCycle(GameState state)
        {
            Ensure(state);
            GovernmentState government = state.government;
            WorldClock.SyncDerivedPeriods(state);
            if (government.history.Count > 0 && government.history[government.history.Count - 1].cycle == state.day)
                return government.history[government.history.Count - 1];
            AgePolicies(government);
            UpdateFactory(state);

            GovernmentBudgetPreview preview = PreviewBudget(state);
            float welfare = preview.welfareSpending;
            government.welfareEligibleResidents = WelfareEligibleResidents(state);
            government.welfarePerEligibleResident = WelfarePerEligibleResident(state);
            float publicService = preview.publicServiceSpending;
            float training = preview.trainingSpending;
            float directFiscalPurchase = preview.fiscalPurchase;
            float interest = preview.interestSpending;
            float income = preview.totalIncome;
            float spending = preview.totalSpending;
            float balance = preview.balance;
            float debtBefore = government.debt;
            government.newBondIssuance = 0f;

            government.cash += balance;
            government.interestPaid = interest;
            government.interestExpense = interest;
            if (government.cash < 0f)
            {
                float issuance = -government.cash;
                government.debt += issuance;
                government.cash = 0f;
                FinancialSystemService.RegisterGovernmentDebtIssuance(state, debtBefore, issuance);
            }
            else if (balance > 0f && government.debt > 0f)
            {
                float repayment = Mathf.Min(government.debt, Mathf.Min(government.cash, balance * 0.25f));
                government.debt -= repayment;
                government.cash -= repayment;
            }

            government.taxRevenue = income;
            government.spending = spending;
            government.budgetBalance = balance;
            UpdateCityMetrics(state, welfare, publicService, training);
            UpdateExternalities(state, training);
            UpdateImpacts(government);
            ProgressFiscalChapter(state);
            RollEvent(government);

            GovernmentCycleRecord record = new GovernmentCycleRecord
            {
                cycle = government.cycle,
                taxRevenue = income,
                spending = spending,
                budgetBalance = balance,
                cashEnd = government.cash,
                debtEnd = government.debt,
                interestPaid = interest,
                averageDebtRate = government.averageDebtRate,
                newBondIssuance = government.newBondIssuance,
                drinkTaxRevenue = government.pendingDrinkTaxRevenue,
                pollutionTaxRevenue = government.pendingPollutionTaxRevenue,
                tariffRevenue = government.pendingTariffRevenue,
                subsidySpending = government.pendingSubsidySpending + training,
                welfareSpending = welfare,
                publicServiceSpending = publicService,
                // 围猎三(#5):紧缩时 DirectPurchase 为负(预算端已按负支出改善赤字),这里旧钳
                // Max(0,·) 把削减从宏观 G 里抹掉——紧缩乘数被砍 1/multiplier,教学单向失真。
                // 保留符号进 record;G 求和端(MacroEconomySimulator)外层已有 Max(0,·) 兜底。
                capitalSpending = government.pendingCapitalSpending + directFiscalPurchase,
                averagePrice = government.averagePrice,
                averageWage = government.averageWage,
                employmentRate = government.employmentRate,
                residentHappiness = government.residentHappiness,
                enterpriseConfidence = government.enterpriseConfidence,
                environmentQuality = government.environmentQuality,
                publicServiceLevel = government.publicServiceLevel,
                approvalRating = government.approvalRating,
                factoryProduction = government.factory.production,
                factoryPollution = government.factory.pollution,
                eventName = government.currentEvent != null && !government.currentEvent.resolved ? government.currentEvent.title : null,
                summary = BuildCycleSummary(government, balance)
            };
            government.history.Add(record);
            FinancialSystemService.ReceiveGovernmentInterest(state, interest);
            state.UnlockCard("fiscalrevenue");
            state.UnlockCard("governmentspending");
            if (balance < 0f) state.UnlockCard("fiscaldeficit");
            if (government.debt > 0f) state.UnlockCard("governmentdebt");
            if (government.debt > 4000f) state.UnlockCard("governmentfailure");
            government.lastPolicySummary = record.summary;
            government.pendingDrinkTaxRevenue = 0f;
            government.pendingPollutionTaxRevenue = 0f;
            government.pendingTariffRevenue = 0f;
            government.lastTariffRecordedDay = 0;
            government.pendingSubsidySpending = 0f;
            government.pendingCapitalSpending = 0f;
            if (government.chapter >= 7 && government.history.Count >= 7) government.termReportReady = true;
            state.Changed();
            return record;
        }

        static void AgePolicies(GovernmentState government)
        {
            for (int i = 0; i < government.policies.Count; i++)
                if (government.policies[i].enactedCycle > 0) government.policies[i].ageCycles++;
        }

        static void UpdateFactory(GameState state)
        {
            GovernmentState government = state.government;
            FactoryState factory = government.factory;
            float pollutionTax = PolicyValue(state, PolicyType.PollutionTax);
            float greenSubsidy = PolicyValue(state, PolicyType.GreenEquipmentSubsidy);
            if (!factory.greenEquipmentInstalled && greenSubsidy > 0f)
            {
                factory.greenUpgradeProgress += 20f + greenSubsidy * 0.45f;
                if (factory.greenUpgradeProgress >= 100f)
                {
                    factory.greenEquipmentInstalled = true;
                    factory.greenUpgradeProgress = 100f;
                    government.pendingSubsidySpending += GreenEquipmentCost * greenSubsidy / 100f;
                }
            }

            float productionPenalty = pollutionTax * (factory.greenEquipmentInstalled ? 2f : 5f);
            float confidenceBonus = (government.enterpriseConfidence - 70f) * 0.25f;
            factory.production = Mathf.Clamp(120f - productionPenalty + confidenceBonus, 55f, 140f);
            float pollutionIntensity = factory.greenEquipmentInstalled ? 0.22f : 0.60f;
            if (IsProjectBuilt(government, PublicProjectType.GreenFacility)) pollutionIntensity *= 0.72f;
            factory.pollution = factory.production * pollutionIntensity;
            factory.jobs = Mathf.RoundToInt(factory.production / 4f);
            factory.profit = factory.production * 14f - factory.production * 7f -
                factory.pollution * pollutionTax - 400f;
            government.pendingPollutionTaxRevenue += factory.pollution * pollutionTax;
        }

        public static int WelfareEligibleResidents(GameState state)
        {
            if (state == null) return 0;
            Ensure(state);
            return Mathf.Max(0, Mathf.RoundToInt(
                state.government.population * state.government.lowIncomeShare / 100f));
        }

        /// <summary>
        /// 基础生活补贴的首批覆盖人数(最困难的 25%)。符合条件人数仍按低收入占比统计(128 人),
        /// 但财政只按首批覆盖支出——若全额覆盖,最低档 ¥25 就要 ¥3,200/周期,远超固定收入 ¥600,
        /// 任何非零档位都会立刻把政府打入债务螺旋,中间档位没有可玩空间(C4)。
        /// </summary>
        public static int WelfareEnrolledResidents(GameState state)
        {
            return Mathf.Max(0, Mathf.RoundToInt(WelfareEligibleResidents(state) * WelfareInitialCoverageRatio));
        }

        public static float WelfarePerEligibleResident(GameState state)
        {
            if (state == null) return 0f;
            int enrolled = WelfareEnrolledResidents(state);
            float automatic = FiscalPolicyService.AutomaticWelfare(state.macro);
            // 个人账本以整元结算，避免正常波动产生 ¥0.16 这类不可解释的小额转账。
            // 自动稳定器总额按首批覆盖人数摊分,与总支出口径一致。
            float automaticPerResident = enrolled > 0 ? Mathf.Floor(automatic / enrolled) : 0f;
            return Mathf.Max(0f, PolicyValue(state, PolicyType.BasicWelfare) + automaticPerResident);
        }

        public static float ProjectedWelfareSpending(GameState state)
        {
            return WelfareEnrolledResidents(state) * WelfarePerEligibleResident(state);
        }

        public static GovernmentBudgetPreview PreviewBudget(GameState state)
        {
            Ensure(state);
            GovernmentState government = state.government;
            GovernmentBudgetPreview preview = new GovernmentBudgetPreview
            {
                otherFeesRevenue = OtherFeesPerCycle,
                drinkTaxRevenue = government.pendingDrinkTaxRevenue,
                pollutionTaxRevenue = government.pendingPollutionTaxRevenue,
                tariffRevenue = government.pendingTariffRevenue,
                automaticTaxChange = FiscalPolicyService.AutomaticTaxChange(state.macro),
                administrativeSpending = AdministrativeSpendingPerCycle,
                subsidySpending = government.pendingSubsidySpending,
                welfareSpending = ProjectedWelfareSpending(state),
                publicServiceSpending = PolicyValue(state, PolicyType.PublicServiceBudget) + ProjectMaintenance(government),
                trainingSpending = TrainingSpending(state),
                interestSpending = government.debt * InterestRateService.PeriodRate(
                    government.averageDebtRate <= 0f ? 3f : government.averageDebtRate),
                capitalSpending = government.pendingCapitalSpending,
                fiscalPurchase = FiscalPolicyService.DirectPurchase(state.macro)
            };
            preview.totalIncome = preview.otherFeesRevenue +
                preview.drinkTaxRevenue + preview.pollutionTaxRevenue +
                preview.tariffRevenue + preview.automaticTaxChange;
            preview.totalSpending = Mathf.Max(0f, preview.administrativeSpending + preview.subsidySpending +
                preview.welfareSpending + preview.publicServiceSpending + preview.trainingSpending +
                preview.interestSpending + preview.capitalSpending + preview.fiscalPurchase);
            preview.balance = preview.totalIncome - preview.totalSpending;
            return preview;
        }

        static float TrainingSpending(GameState state)
        {
            float subsidy = PolicyValue(state, PolicyType.TrainingSubsidy);
            if (subsidy <= 0f) return 0f;
            float participants = 8f + subsidy * 1.4f;
            return participants * subsidy;
        }

        static float ProjectMaintenance(GovernmentState government)
        {
            float total = 0f;
            for (int i = 0; i < government.projects.Count; i++)
                if (government.projects[i].built) total += government.projects[i].maintenanceCost;
            return total;
        }

        static void UpdateCityMetrics(GameState state, float welfare, float publicService, float training)
        {
            GovernmentState government = state.government;
            float minimumWage = PolicyValue(state, PolicyType.MinimumWage);
            float wageIncrease = Mathf.Max(0f, minimumWage - BaselineHourlyWage);
            LaborMarketSnapshot labor = LaborMarketService.Evaluate(state,
                state.macro == null ? 0f : state.macro.outputGap,
                state.macro == null ? MacroShockType.None : state.macro.pendingShock);
            government.averageWage = labor.effectiveDailyWage / 8f + government.projectAverageWageOffset;
            government.employmentRate = Mathf.Clamp(100f - labor.unemploymentRate + government.eventEmploymentRateOffset, 0f, 100f);

            float welfarePerResident = welfare / Mathf.Max(1f, government.population);
            float serviceGain = Mathf.Sqrt(Mathf.Max(0f, publicService)) * 0.035f;
            float pollutionDamage = Mathf.Max(0f, government.factory.pollution - 25f) * 0.025f;
            float greenGain = government.factory.greenEquipmentInstalled ? 1.8f : 0f;
            government.environmentQuality = Mathf.Clamp(government.environmentQuality + greenGain - pollutionDamage, 10f, 100f);
            government.publicServiceLevel = Mathf.Clamp(government.publicServiceLevel + serviceGain - 0.45f, 0f, 100f);
            government.residentHappiness = Mathf.Clamp(62f + government.publicServiceLevel * 0.18f +
                government.environmentQuality * 0.12f + welfarePerResident * 0.8f +
                (government.averageWage - 25f) * 0.45f - (100f - government.employmentRate) * 0.35f +
                government.projectHappinessOffset, 20f, 100f);

            float tax = PolicyValue(state, PolicyType.DrinkTax);
            float pollutionTax = PolicyValue(state, PolicyType.PollutionTax);
            float subsidy = PolicyValue(state, PolicyType.DrinkSubsidy) +
                PolicyValue(state, PolicyType.GreenEquipmentSubsidy) * 0.02f;
            DayRecord enterprise = state.LastRecord;
            float profitSignal = enterprise == null ? 0f : Mathf.Clamp(enterprise.profit / 90f, -12f, 12f);
            float growthSignal = state.macro == null ? 0f : Mathf.Clamp(state.macro.gdpGrowth, -8f, 8f) * 1.2f;
            float energyPenalty = state.macro == null ? 0f :
                Mathf.Max(0f, state.macro.energyCostIndex - 100f) * .08f;
            // GovernmentState.enterpriseConfidence 是唯一权威值；宏观只读取并快照它。
            government.enterpriseConfidence = Mathf.Clamp(74f - tax * 2.2f - pollutionTax * 2.4f -
                wageIncrease * 1.2f + subsidy * 1.5f + training * 0.002f + profitSignal +
                growthSignal - energyPenalty + government.eventEnterpriseConfidenceOffset +
                government.projectEnterpriseConfidenceOffset, 15f, 100f);

            government.lowestGroupPurchasingPower = Mathf.Clamp(48f + welfarePerResident * 2.8f +
                wageIncrease * 1.4f - Mathf.Max(0f, government.averagePrice - 17f) * 2f, 5f, 100f);
            government.medianIncome = Mathf.Max(24f, 26f + wageIncrease * 0.55f);
            government.lowIncomeShare = Mathf.Clamp(40f - welfarePerResident * 0.35f - wageIncrease * 0.18f, 25f, 50f);
            government.highIncomeShare = Mathf.Clamp(15f + (40f - government.lowIncomeShare) * 0.18f, 12f, 24f);
            government.middleIncomeShare = 100f - government.lowIncomeShare - government.highIncomeShare;

            government.lowIncomeSupport = Mathf.Clamp(52f + welfarePerResident * 5f + wageIncrease * 1.8f - tax * 1.5f, 0f, 100f);
            government.businessSupport = Mathf.Clamp(government.enterpriseConfidence - pollutionTax * 1.5f, 0f, 100f);
            government.studentSupport = Mathf.Clamp(58f + government.publicServiceLevel * 0.28f +
                PolicyValue(state, PolicyType.TrainingSubsidy) * 1.3f - tax, 0f, 100f);
            government.residentSupport = Mathf.Clamp(government.residentHappiness + welfarePerResident - tax +
                government.eventResidentSupportOffset, 0f, 100f);
            government.approvalRating = Mathf.Clamp(government.residentSupport * 0.40f +
                government.lowIncomeSupport * 0.22f + government.businessSupport * 0.18f +
                government.studentSupport * 0.20f, 0f, 100f);
        }

        static void UpdateExternalities(GameState state, float trainingSpending)
        {
            GovernmentState government = state.government;
            for (int i = 0; i < government.externalities.Count; i++)
            {
                ExternalityState externality = government.externalities[i];
                if (externality.id == "factory_pollution")
                {
                    externality.privateCost = government.factory.production * 7f;
                    externality.socialCost = externality.privateCost + government.factory.pollution * 5f;
                    externality.privateBenefit = government.factory.profit;
                    externality.socialBenefit = government.factory.profit + government.factory.jobs * 18f;
                }
                else if (externality.id == "training_spillover")
                {
                    float schoolMultiplier = IsProjectBuilt(government, PublicProjectType.School) ? 1.25f : 1f;
                    externality.privateCost = trainingSpending * 0.55f;
                    externality.socialCost = trainingSpending;
                    externality.privateBenefit = trainingSpending * 0.8f;
                    externality.socialBenefit = trainingSpending * 1.65f * schoolMultiplier;
                }
            }
        }

        static bool IsProjectBuilt(GovernmentState government, PublicProjectType type)
        {
            if (government == null || government.projects == null) return false;
            for (int i = 0; i < government.projects.Count; i++)
                if (government.projects[i].type == type) return government.projects[i].built;
            return false;
        }

        static void UpdateImpacts(GovernmentState government)
        {
            for (int i = 0; i < government.impacts.Count; i++)
            {
                PolicyImpactRecord impact = government.impacts[i];
                int age = Mathf.Max(0, government.cycle - impact.cycleEnacted);
                impact.currentStage = Mathf.Clamp(age + 1, 1, 3);
                if (impact.currentStage == 1)
                    impact.observedSummary = "第一阶段 · " + impact.immediateEffect;
                else if (impact.currentStage == 2)
                    impact.observedSummary = "第二阶段 · 市场主体正在调整价格、产量、消费或招聘。";
                else
                    impact.observedSummary = "第三阶段 · " + impact.delayedEffect;
            }
        }

    }
}
