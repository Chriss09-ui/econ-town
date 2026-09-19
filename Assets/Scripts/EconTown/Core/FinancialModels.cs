using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public enum LoanBorrowerType { Resident, Company }
    public enum LoanPurpose { Consumption, WorkingCapital, EquipmentInvestment, Mortgage }
    public enum LoanStatus { Current, Paid, Defaulted }

    [Serializable]
    public class LoanRecord
    {
        public string id;
        public LoanBorrowerType borrowerType;
        public string borrowerId;
        public LoanPurpose purpose;
        public float principal;
        public float remainingPrincipal;
        // UI 与存档统一使用年化百分比，例如 6.5 表示 6.5%。
        public float annualInterestRate;
        public int term;
        public int remainingTerm;
        public float paymentPerPeriod;
        public LoanStatus status = LoanStatus.Current;
        public float defaultProbability;
        public int originatedWorldDay;
        public int missedPayments;
        public float totalPrincipalPaid;
        public float totalInterestPaid;
        // V0.9.1 房贷:抵押物字段(Ensure 默认 0,旧档兼容)。
        public float collateralValue;
        public float ltvAtOrigination;
    }

    [Serializable]
    public class GovernmentBondLot
    {
        public string id;
        public float principal;
        public float annualInterestRate;
        public int issuedWorldDay;
        public int remainingTerm;
    }

    [Serializable]
    public class BankDayRecord
    {
        public int worldDay;
        public float reserves;
        public float householdDeposits;
        public float companyDeposits;
        public float townSectorDeposits;   // V0.9.1:镇住户部门存款(房东/卖方户头,入负债端平衡表)
        public float householdLoans;
        public float companyLoans;
        public float governmentBonds;
        public float centralBankBorrowing;
        public float equity;
        public float totalAssets;
        public float totalLiabilities;
        public float balanceGap;
        public float depositRate;
        public float householdLoanRate;
        public float companyLoanRate;
        public float requiredReserveRatio;
        public float actualReserveRatio;
        public float interestIncome;
        public float interestExpense;
        public float operatingCost;
        public float loanLoss;
        public float newCredit;
        public float companyInvestment;
        public float m0;
        public float m1;
        public float financialStress;
        public bool liquidityCrisis;
        public bool insolvent;
    }

    [Serializable]
    public class BankState
    {
        public bool initialized;
        public float cash;
        public float reserves;
        public float householdDeposits;
        public float companyDeposits;
        public float townSectorDeposits;   // V0.9.1:镇住户部门存款(租金/首付/卖房款的对手方,真实负债科目)
        public float centralBankBorrowing;
        public float equity;

        public float depositRate = 2f;
        public float householdLoanRate = 6f;
        public float companyLoanRate = 5.5f;
        public float requiredReserveRatio = .10f;

        public float interestIncome;
        public float interestExpense;
        public float operatingCost;
        public float loanLoss;
        public float totalInterestIncome;
        public float totalInterestExpense;
        public float totalLoanLoss;

        public float todayNewCredit;
        public float todayCompanyInvestment;
        // 围猎五(#4):当日供款中从经营现金/居民现金垫付的额(companyDeposits 不足时的腿)。
        // 供结算后窗口(章节存档)读档对账用——企业/居民现金腿会被基线回滚退回,而银行侧
        // 收款随档保留,按这两个流量把现金腿补扣回来,两侧重新对称(照 todayNewCredit 先例:
        // SettleContracts 真跑时清零,随存档往返)。
        public float todayCompanyLoanPaymentFromCash;
        public float todayResidentLoanPaymentFromCash;
        // 围猎五#4复审:当日企业供款中的利息部分——已结算窗口读档重放时,原时间线对当日
        // 记录的利息改写(SettleContracts 尾段)不随重放,按此流量在重放日补写进新日报
        // (利润与现金勾稽一致);谁消费谁清零(真跑改写后/早退补写后)。
        public float todayLoanInterestPaidCompany;
        public float previousPerformingLoans;
        public float creditGrowth;
        public float financialStress;
        public bool liquidityCrisis;
        public bool bankRunActive;
        public float bankRunUnmetWithdrawals;
        public int lastContractSettlementDay;
        public int lastCompletedSettlementDay;
        public int nextLoanSerial = 1;
        public int nextBondSerial = 1;

        public List<LoanRecord> loans = new List<LoanRecord>();
        public List<GovernmentBondLot> bonds = new List<GovernmentBondLot>();
        public List<BankDayRecord> history = new List<BankDayRecord>();

        public float HouseholdLoans { get { return LoanAssets(LoanBorrowerType.Resident); } }
        public float CompanyLoans { get { return LoanAssets(LoanBorrowerType.Company); } }
        public float PerformingLoans { get { return HouseholdLoans + CompanyLoans; } }
        public float GovernmentBonds
        {
            get
            {
                float total = 0f;
                if (bonds != null) for (int i = 0; i < bonds.Count; i++) total += Mathf.Max(0f, bonds[i].principal);
                return total;
            }
        }
        public float TotalDeposits
        {
            get { return Mathf.Max(0f, householdDeposits) + Mathf.Max(0f, companyDeposits) + Mathf.Max(0f, townSectorDeposits); }
        }
        public float TotalAssets { get { return Mathf.Max(0f, cash) + Mathf.Max(0f, reserves) + PerformingLoans + GovernmentBonds; } }
        public float TotalLiabilities { get { return TotalDeposits + Mathf.Max(0f, centralBankBorrowing); } }
        public float BalanceGap { get { return TotalAssets - TotalLiabilities - equity; } }
        public float RequiredReserves { get { return TotalDeposits * Mathf.Clamp(requiredReserveRatio, 0f, 1f); } }
        public float ExcessReserves { get { return Mathf.Max(0f, reserves - RequiredReserves); } }
        public float ActualReserveRatio { get { return TotalDeposits <= .001f ? 1f : reserves / TotalDeposits; } }
        public float CapitalAdequacy { get { return TotalAssets <= .001f ? 0f : equity / TotalAssets; } }
        public bool Insolvent { get { return equity < -.01f || TotalAssets + .01f < TotalLiabilities; } }

        float LoanAssets(LoanBorrowerType type)
        {
            float total = 0f;
            if (loans == null) return 0f;
            for (int i = 0; i < loans.Count; i++)
                if (loans[i] != null && loans[i].borrowerType == type && loans[i].status == LoanStatus.Current)
                    total += Mathf.Max(0f, loans[i].remainingPrincipal);
            return total;
        }
    }

    [Serializable]
    public class PolicyTransmissionState
    {
        public float fromPolicyRate = 3f;
        public float toPolicyRate = 3f;
        public int decisionWorldDay = -999;
        public int stage;
        public float bankPassThrough;
        public float realEconomyPassThrough;
        public string direction = "保持";
        public string summary = "尚无新的货币政策决定。";
    }

    [Serializable]
    public class CentralBankPolicyRecord
    {
        public int worldDay;
        public float oldRate;
        public float newRate;
        public float inflation;
        public float unemployment;
        public float gdpGrowth;
        public string direction;
    }

    [Serializable]
    public class CentralBankState
    {
        public bool initialized;
        public bool unlocked;
        public float policyRate = 3f;
        public float inflationTarget = 2f;
        public float requiredReserveRatio = .10f;
        public float emergencyLending;
        public float credibility = 80f;
        public float financialStress;
        public int lastPolicyDecisionDay = -999;
        public int meetingIntervalDays = 7;

        // V0.7 外汇储备(B币计价):固定汇率的干预弹药。卖出外储买入本币是唯一合法
        // 消耗路径;储备降至 floor 即守不住盯住,最终被迫失守(PRD §100-101,外储不可凭空增减)。
        public float forexReservesBCurrency = 800f;
        public float forexReserveFloor = 120f;
        public float maxDailyInterventionBCurrency = 60f;
        public float lastInterventionBCurrency;
        public float cumulativeInterventionBCurrency;
        public int interventionDays;
        public int lastFxSettledWorldDay = -999;

        public PolicyTransmissionState transmission = new PolicyTransmissionState();
        public List<CentralBankPolicyRecord> policyHistory = new List<CentralBankPolicyRecord>();
    }

    public struct CreditQuote
    {
        public bool approved;
        public float requestedAmount;
        public float approvedAmount;
        public float baseRate;
        public float riskPremium;
        public float termPremium;
        public float liquidityPremium;
        public float finalRate;
        public float defaultProbability;
        public int term;
        public string grade;
        public string reason;
    }

    public struct FinancialEffectBreakdown
    {
        public float baseValue;
        public float confidenceEffect;
        public float depositRateEffect;
        public float loanRateEffect;
        public float inflationExpectationEffect;
        public float financialStressEffect;
        public float creditAvailabilityEffect;
        public float finalValue;
    }
}
