using System;
using System.Collections.Generic;

namespace EconTown
{
    public enum PolicyType
    {
        DrinkTax,
        DrinkSubsidy,
        PollutionTax,
        GreenEquipmentSubsidy,
        TrainingSubsidy,
        MinimumWage,
        PriceCeiling,
        PriceFloor,
        BasicWelfare,
        PublicServiceBudget,
        ImportTariff
    }

    public enum PublicProjectType
    {
        StreetLights,
        Park,
        BusStop,
        School,
        GreenFacility
    }

    public enum ExternalityType { Positive, Negative }

    [Serializable]
    public class PolicyState
    {
        public PolicyType type;
        public string name;
        public string target;
        public float value;
        public float minimum;
        public float maximum;
        public float step;
        public bool active;
        public int enactedCycle;
        public int ageCycles;
        public string immediateEffect;
        public string delayedEffect;
    }

    [Serializable]
    public class PolicyImpactRecord
    {
        public PolicyType type;
        public string policyName;
        public int cycleEnacted;
        public int currentStage;
        public string immediateEffect;
        public string delayedEffect;
        public string winners;
        public string losers;
        public string governmentEffect;
        public string causalChain;
        public string observedSummary;
    }

    [Serializable]
    public class ExternalityState
    {
        public string id;
        public string source;
        public string affectedGroup;
        public ExternalityType type;
        public float privateCost;
        public float socialCost;
        public float privateBenefit;
        public float socialBenefit;
    }

    [Serializable]
    public class FactoryState
    {
        public float production = 120f;
        public int jobs = 30;
        public float profit = 900f;
        public float pollution = 72f;
        public bool greenEquipmentInstalled;
        public float greenUpgradeProgress;
    }

    [Serializable]
    public class PublicProjectState
    {
        public PublicProjectType type;
        public string name;
        public float buildCost;
        public float maintenanceCost;
        public float serviceGain;
        public float happinessGain;
        public float businessGain;
        public bool built;
        public int builtCycle;
    }

    [Serializable]
    public class GovernmentEventState
    {
        public string id;
        public string title;
        public string description;
        public string optionA;
        public string optionB;
        public string optionC;
        public string chosenOption;
        public bool resolved;
    }

    [Serializable]
    public class GovernmentCycleRecord
    {
        public int cycle;
        public float taxRevenue;
        public float spending;
        public float budgetBalance;
        public float cashEnd;
        public float debtEnd;
        public float interestPaid;
        public float averageDebtRate;
        public float newBondIssuance;
        public float drinkTaxRevenue;
        public float pollutionTaxRevenue;
        public float tariffRevenue;
        public float subsidySpending;
        public float welfareSpending;
        public float publicServiceSpending;
        public float capitalSpending;
        public float averagePrice;
        public float averageWage;
        public float employmentRate;
        public float residentHappiness;
        public float enterpriseConfidence;
        public float environmentQuality;
        public float publicServiceLevel;
        public float approvalRating;
        public float factoryProduction;
        public float factoryPollution;
        public string eventName;
        public string summary;
    }

    public struct GovernmentBudgetPreview
    {
        public float otherFeesRevenue;
        public float drinkTaxRevenue;
        public float pollutionTaxRevenue;
        public float tariffRevenue;
        public float automaticTaxChange;
        public float totalIncome;
        public float administrativeSpending;
        public float subsidySpending;
        public float welfareSpending;
        public float publicServiceSpending;
        public float trainingSpending;
        public float interestSpending;
        public float capitalSpending;
        public float fiscalPurchase;
        public float totalSpending;
        public float balance;
    }

    [Serializable]
    public class GovernmentState
    {
        public int cycle = 1;
        public float cash = 12000f;
        public float taxRevenue;
        public float spending;
        public float budgetBalance;
        public float debt;
        public float interestPaid;
        public float averageDebtRate = 3f;
        public float interestExpense;
        public float newBondIssuance;

        public float pendingDrinkTaxRevenue;
        public float pendingPollutionTaxRevenue;
        public float pendingTariffRevenue;      // V0.7:进口关税挂账(照污染税模式周期入账)
        public float pendingSubsidySpending;
        public float pendingCapitalSpending;
        // 同日防重标记:企业记录有"一天一条"兜底,税/补贴基也必须防双计(C8)。
        public int lastDrinkTaxRecordedDay;
        public int lastAdditionalSalesDay;
        public int lastTariffRecordedDay;       // 关税按实际完税进口累记,同一 WorldDay 只结一次

        public int population = 320;
        // 与 UpdateCityMetrics 首期公式输出一致(100-7.66 / 有效日薪 200÷8),避免第一周期读数跳变。
        // bug搜#1复审:政府镜像随宏观不动点同步(100−16.67)——首期 UpdateCityMetrics 写 83.33,
        // 旧声明 92.34(=100−旧口径 7.66)会让政府面板就业率首个收日无解释跳变约 9 个百分点。
        public float employmentRate = 83.33f;
        public float averageWage = 25f;
        public float averagePrice = 17.3f;
        public float averageEnterpriseProfit = 680f;
        public float residentHappiness = 68f;
        public float enterpriseConfidence = 72f;
        public float environmentQuality = 61f;
        public float publicServiceLevel = 55f;
        public float approvalRating = 68f;

        public float residentSupport = 72f;
        public float lowIncomeSupport = 70f;
        public float businessSupport = 58f;
        public float studentSupport = 72f;

        // 已决议城市事件对派生指标的持久修正；旧存档缺失时默认均为 0。
        public float eventAveragePriceOffset;
        public float eventEmploymentRateOffset;
        public float eventResidentSupportOffset;
        public float eventEnterpriseConfidenceOffset;

        // 公共项目的一次性加成同样走持久修正:直接 += 的值会被 UpdateCityMetrics 的公式整体
        // 重算覆盖——学校的工资加成、项目的幸福/商业加成原本只活一周期(P2-3);旧档默认 0。
        public float projectAverageWageOffset;
        public float projectHappinessOffset;
        public float projectEnterpriseConfidenceOffset;

        public float lowIncomeShare = 40f;
        public float middleIncomeShare = 45f;
        public float highIncomeShare = 15f;
        public float medianIncome = 26f;
        public float lowestGroupPurchasingPower = 52f;
        public int welfareEligibleResidents;
        public float welfarePerEligibleResident;

        public int chapter = 1;
        public int chapterMask = 1;
        public bool termReportReady;
        public bool termReportViewed;
        public bool freeRiderExperimentDone;
        public int freeRiderContributors;
        public float freeRiderFundsRaised;
        public string lastNpcFeedback;
        public string lastPolicySummary;

        public FactoryState factory = new FactoryState();
        public GovernmentEventState currentEvent;
        public List<PolicyState> policies = new List<PolicyState>();
        public List<PolicyImpactRecord> impacts = new List<PolicyImpactRecord>();
        public List<ExternalityState> externalities = new List<ExternalityState>();
        public List<PublicProjectState> projects = new List<PublicProjectState>();
        public List<GovernmentCycleRecord> history = new List<GovernmentCycleRecord>();

        // 运行时缓存标记(不入存档):政策/项目/外部性结构补齐后,Ensure 热路径可早退(C2)。
        [NonSerialized] public bool structureEnsured;
    }
}
