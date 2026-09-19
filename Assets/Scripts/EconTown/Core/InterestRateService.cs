using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.6 单一利率权威：政策利率先传到银行负债与贷款定价，
    /// 再通过居民消费和企业投资影响实体经济。任何模块不得自行硬编政策利率。
    /// </summary>
    public static class InterestRateService
    {
        public const float PeriodsPerYear = 52f;
        public const float NeutralPolicyRate = 3f;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.bank == null) state.bank = new BankState();
            if (state.centralBank == null) state.centralBank = new CentralBankState();
            BankState bank = state.bank;
            CentralBankState central = state.centralBank;
            if (bank.loans == null) bank.loans = new System.Collections.Generic.List<LoanRecord>();
            if (bank.bonds == null) bank.bonds = new System.Collections.Generic.List<GovernmentBondLot>();
            if (bank.history == null) bank.history = new System.Collections.Generic.List<BankDayRecord>();
            if (central.transmission == null) central.transmission = new PolicyTransmissionState();
            if (central.policyHistory == null) central.policyHistory = new System.Collections.Generic.List<CentralBankPolicyRecord>();

            if (!central.initialized)
            {
                central.initialized = true;
                central.policyRate = central.policyRate <= 0f ? NeutralPolicyRate : Mathf.Clamp(central.policyRate, .25f, 20f);
                central.inflationTarget = central.inflationTarget <= 0f ? 2f : Mathf.Clamp(central.inflationTarget, 0f, 10f);
                central.requiredReserveRatio = central.requiredReserveRatio <= 0f ? .10f :
                    Mathf.Clamp(central.requiredReserveRatio, .03f, .40f);
                central.meetingIntervalDays = Mathf.Max(1, central.meetingIntervalDays);
            }

            // V0.7 外储坏档兜底:V0.6 旧档缺字段会落回类默认 800;显式归零只可能是坏档,
            // 正常玩法下外储最低停在 floor(120)——干预到 floor 即触发失守,不会继续消耗。
            central.forexReserveFloor = Mathf.Clamp(central.forexReserveFloor, 0f, 400f);
            central.maxDailyInterventionBCurrency =
                Mathf.Clamp(central.maxDailyInterventionBCurrency <= 0f ? 60f : central.maxDailyInterventionBCurrency,
                    5f, 200f);
            if (central.forexReservesBCurrency < 1f) central.forexReservesBCurrency = 800f;

            if (!bank.initialized)
            {
                ResidentEconomy.Ensure(state);
                bank.initialized = true;
                bank.householdDeposits = Mathf.Max(0f, state.resident.savings);
                bank.reserves = Mathf.Max(8000f, bank.householdDeposits + 3000f);
                bank.depositRate = DepositRateTarget(state);
                bank.householdLoanRate = HouseholdLoanRateTarget(state);
                bank.companyLoanRate = CompanyLoanRateTarget(state);
                bank.requiredReserveRatio = central.requiredReserveRatio;
                bank.equity = bank.TotalAssets - bank.TotalLiabilities;
                bank.previousPerformingLoans = bank.PerformingLoans;
            }

            // 找 BUG 修复轮回A:坏档自愈——历史脱钩档可能把存款负债扣成负数,而 TotalDeposits 的
            // Max(0,·) 钳位会截断负债端令 BalanceGap 永续漂移;把不可能的负负债归零后按当前表
            // 重算权益(与首初始化 :55 同口径,健康档恒等无扰)——幽灵差额由资本承担。
            if (bank.householdDeposits < 0f)
            {
                bank.householdDeposits = 0f;
                bank.equity = bank.TotalAssets - bank.TotalLiabilities;
            }

            bank.requiredReserveRatio = central.requiredReserveRatio;
            central.unlocked = central.unlocked || (state.progression != null && state.progression.completedChapter >= 36);
        }

        public static float PeriodRate(float annualPercent)
        {
            return Mathf.Max(0f, annualPercent) / 100f / PeriodsPerYear;
        }

        public static float DepositRateTarget(GameState state)
        {
            float policy = state != null && state.centralBank != null ? state.centralBank.policyRate : NeutralPolicyRate;
            return Mathf.Clamp(policy - 1f, .10f, 12f);
        }

        public static float HouseholdLoanRateTarget(GameState state)
        {
            float policy = state != null && state.centralBank != null ? state.centralBank.policyRate : NeutralPolicyRate;
            return Mathf.Clamp(policy + 3f + LiquidityPremium(state), 1f, 30f);
        }

        public static float CompanyLoanRateTarget(GameState state)
        {
            float policy = state != null && state.centralBank != null ? state.centralBank.policyRate : NeutralPolicyRate;
            return Mathf.Clamp(policy + 2.5f + LiquidityPremium(state), 1f, 30f);
        }

        public static float GovernmentNewDebtRate(GameState state)
        {
            Ensure(state);
            return Mathf.Clamp(state.centralBank.policyRate + 1.25f + LiquidityPremium(state) * .35f, .5f, 25f);
        }

        public static float RealDepositRate(GameState state)
        {
            Ensure(state);
            float inflation = state.macro != null ? state.macro.inflation : 0f;
            return state.bank.depositRate - inflation;
        }

        public static bool TrySetPolicyRate(GameState state, float newAnnualRate, out string message, bool experiment = false)
        {
            message = null;
            if (state == null) { message = "经济世界尚未初始化。"; return false; }
            Ensure(state);
            CentralBankState central = state.centralBank;
            if (!experiment && !central.unlocked)
            {
                message = "完成银行基础章节后才能参加货币政策会议。";
                return false;
            }
            int daysSince = state.day - central.lastPolicyDecisionDay;
            if (!experiment && daysSince < central.meetingIntervalDays)
            {
                message = "下次正式货币政策会议还有 " + (central.meetingIntervalDays - daysSince) + " 天。";
                return false;
            }

            float oldRate = central.policyRate;
            float next = Mathf.Clamp(Mathf.Round(newAnnualRate * 4f) / 4f, .25f, 20f);
            if (Mathf.Abs(next - oldRate) < .001f)
            {
                next = oldRate;
            }
            string direction = next > oldRate ? "加息" : next < oldRate ? "降息" : "保持";
            central.policyRate = next;
            central.lastPolicyDecisionDay = state.day;
            central.transmission = new PolicyTransmissionState
            {
                fromPolicyRate = oldRate,
                toPolicyRate = next,
                decisionWorldDay = state.day,
                stage = 0,
                bankPassThrough = 0f,
                realEconomyPassThrough = 0f,
                direction = direction,
                summary = direction + "：" + oldRate.ToString("0.00") + "% → " + next.ToString("0.00") + "%"
            };
            central.policyHistory.Add(new CentralBankPolicyRecord
            {
                worldDay = state.day,
                oldRate = oldRate,
                newRate = next,
                inflation = state.macro != null ? state.macro.inflation : 0f,
                unemployment = state.macro != null ? state.macro.unemploymentRate : 0f,
                gdpGrowth = state.macro != null ? state.macro.gdpGrowth : 0f,
                direction = direction
            });
            if (central.policyHistory.Count > 80) central.policyHistory.RemoveAt(0);
            if (!experiment)
            {
                if (direction == "加息") ProgressionService.RecordSignal(state, ProgressSignals.PolicyRateRaised,
                    "货币政策会议加息", central.transmission.summary);
                else if (direction == "降息") ProgressionService.RecordSignal(state, ProgressSignals.PolicyRateCut,
                    "货币政策会议降息", central.transmission.summary);
            }
            state.Changed();
            message = direction == "保持" ? "政策利率保持不变。" :
                central.transmission.summary + "。银行利率会先变化，消费、投资和通胀会在更后面逐步体现。";
            return true;
        }

        public static void AdvanceTransmission(GameState state)
        {
            Ensure(state);
            BankState bank = state.bank;
            PolicyTransmissionState transmission = state.centralBank.transmission;
            int age = state.day - transmission.decisionWorldDay;
            if (age <= 0) return;

            transmission.bankPassThrough = Mathf.Clamp01(age / 4f);
            transmission.realEconomyPassThrough = Mathf.Clamp01((age - 2f) / 6f);
            transmission.stage = age < 2 ? 1 : age < 5 ? 2 : age < 9 ? 3 : 4;
            bank.depositRate = Mathf.Lerp(bank.depositRate, DepositRateTarget(state), .35f);
            bank.householdLoanRate = Mathf.Lerp(bank.householdLoanRate, HouseholdLoanRateTarget(state), .35f);
            bank.companyLoanRate = Mathf.Lerp(bank.companyLoanRate, CompanyLoanRateTarget(state), .35f);
        }

        public static FinancialEffectBreakdown ResidentConsumptionBreakdown(GameState state, float baseValue)
        {
            Ensure(state);
            float pass = RealEconomyPassThrough(state);
            float confidence = state.macro != null ? (state.macro.consumerConfidence - 68f) * .0035f * baseValue : 0f;
            float deposit = -(state.bank.depositRate - 2f) * .022f * pass * baseValue;
            float loan = -(state.bank.householdLoanRate - 6f) * .010f * pass * baseValue;
            float expectation = state.macro != null ? Mathf.Clamp(state.macro.inflation, -2f, 12f) * .004f * pass * baseValue : 0f;
            float stress = -baseValue * Mathf.Clamp01(state.bank.financialStress / 100f) * .06f;
            float availability = state.bank.Insolvent ? -baseValue * .08f :
                state.bank.liquidityCrisis ? -baseValue * .04f : 0f;
            return new FinancialEffectBreakdown
            {
                baseValue = baseValue,
                confidenceEffect = confidence,
                depositRateEffect = deposit,
                loanRateEffect = loan,
                inflationExpectationEffect = expectation,
                financialStressEffect = stress,
                creditAvailabilityEffect = availability,
                finalValue = Mathf.Max(0f, baseValue + confidence + deposit + loan + expectation + stress + availability)
            };
        }

        public static FinancialEffectBreakdown CompanyInvestmentBreakdown(GameState state, float baseValue)
        {
            Ensure(state);
            float pass = RealEconomyPassThrough(state);
            float confidence = state.macro != null ? (state.macro.businessConfidence - 72f) * .005f * baseValue : 0f;
            float loan = -(state.bank.companyLoanRate - 5.5f) * .045f * pass * baseValue;
            float stress = -baseValue * Mathf.Clamp01(state.bank.financialStress / 100f) * .14f;
            float availability = state.bank.Insolvent ? -baseValue * .38f :
                state.bank.liquidityCrisis ? -baseValue * .22f : 0f;
            return new FinancialEffectBreakdown
            {
                baseValue = baseValue,
                confidenceEffect = confidence,
                loanRateEffect = loan,
                financialStressEffect = stress,
                creditAvailabilityEffect = availability,
                finalValue = Mathf.Max(0f, baseValue + confidence + loan + stress + availability)
            };
        }

        public static string TransmissionStageText(GameState state)
        {
            Ensure(state);
            PolicyTransmissionState t = state.centralBank.transmission;
            if (t.decisionWorldDay < 0) return "尚无新政策：银行和实体经济处于既有利率环境。";
            switch (t.stage)
            {
                case 0: return "直接影响：政策利率已公布；银行报价尚未调整。";
                case 1: return "正在传导：银行存贷款利率开始调整。";
                case 2: return "正在传导：企业融资与居民储蓄/消费决策开始变化。";
                case 3: return "尚未完全体现：总需求已变化，GDP、就业和通胀仍在滞后调整。";
                default: return "政策已大部分传导；仍需结合新冲击观察结果。";
            }
        }

        static float LiquidityPremium(GameState state)
        {
            if (state == null || state.bank == null || !state.bank.initialized) return 0f;
            float shortfall = state.bank.requiredReserveRatio - state.bank.ActualReserveRatio;
            return Mathf.Clamp(shortfall * 35f + (state.bank.liquidityCrisis ? 2f : 0f), 0f, 6f);
        }

        static float RealEconomyPassThrough(GameState state)
        {
            PolicyTransmissionState transmission = state.centralBank.transmission;
            return transmission == null || transmission.decisionWorldDay < 0 ? 1f : transmission.realEconomyPassThrough;
        }
    }
}
