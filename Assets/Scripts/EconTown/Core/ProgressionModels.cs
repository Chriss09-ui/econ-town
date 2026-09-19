using System;
using System.Collections.Generic;

namespace EconTown
{
    public enum LearningPart
    {
        Enterprise = 1,
        Market = 2,
        Resident = 3,
        Government = 4,
        Macro = 5,
        Sandbox = 6,
        Finance = 7,
        Trade = 8,  // V0.7 第七篇「世界市场」。尾部追加不插队:序列化为整数,旧档数值不受影响。
        Housing = 9 // V0.9.5 第八篇「资产与杠杆」。
    }

    [Serializable]
    public class ChapterDefinition
    {
        public int number;
        public LearningPart part;
        public string title;
        public string task;
        public string learningGoal;
        public string requiredSignal;

        public ChapterDefinition(int number, LearningPart part, string title, string task,
            string learningGoal, string requiredSignal)
        {
            this.number = number;
            this.part = part;
            this.title = title;
            this.task = task;
            this.learningGoal = learningGoal;
            this.requiredSignal = requiredSignal;
        }
    }

    [Serializable]
    public class KeyDecisionRecord
    {
        public int worldDay;
        public string chapterId;
        public string title;
        public string summary;
    }

    [Serializable]
    public class StageReportRecord
    {
        public LearningPart part;
        public int completedWorldDay;
        public string resultSummary;
        public string learnedSummary;
        public string reviewSummary;
        public List<string> keyDecisions = new List<string>();
    }

    [Serializable]
    public class ProgressionState
    {
        public int currentChapter = 1;
        public int completedChapter;
        public int chapterStartedWorldDay = 1;
        public bool mainlineComplete;
        public bool sandboxUnlocked;
        // V0.5.2 及更早存档的单值字段：Ensure 时迁移进 pendingStageReports 后清零。
        public int pendingStageReport;
        // 待查看的阶段报告队列：一次 Evaluate 可能连续跨多个篇界（旧档迁移必现），
        // 单值会互相覆盖导致中间篇的结业弹窗被吞。
        public List<int> pendingStageReports = new List<int>();
        // V0.7.2 一次性场景演出队列：只在跨过章节现场写入，旧档不按 completedChapter 反推补播。
        public List<string> pendingCeremonies = new List<string>();
        public int positiveProfitStreak;
        // 该 streak 最近一次计数所依据的结算日：同一 DayRecord 不得重复 +1（读档重放日防双记）。
        public int positiveProfitStreakLastDay;
        public bool positiveProfitGoalComplete;
        public bool rivalPriceCutTriggered;
        // 已弹过"章节解锁"Toast 的最高章号：解锁提示只应弹一次，
        // 按 completedChapter 持续状态判定会每个无事件清晨重复弹同一条。
        public int notifiedUnlockChapter;
        public bool initialized;
        public List<string> signals = new List<string>();
        public List<KeyDecisionRecord> decisions = new List<KeyDecisionRecord>();
        public List<StageReportRecord> stageReports = new List<StageReportRecord>();
    }

    public static class ProgressSignals
    {
        public const string FirstBusiness = "enterprise.first_business";
        public const string PriceChanged = "enterprise.price_changed";
        public const string ProfitObserved = "enterprise.profit_observed";
        public const string EmployeeHired = "enterprise.employee_hired";
        public const string ProfitExperiment = "enterprise.profit_experiment";
        public const string CompetitorOpened = "market.competitor_opened";
        public const string MarketShareObserved = "market.share_observed";
        public const string DifferentiationUsed = "market.differentiation_used";
        public const string PriceWarResolved = "market.price_war_resolved";
        public const string MarketExperiment = "market.experiment_completed";
        public const string ResidentProfile = "resident.profile_created";
        public const string ScarcityExperienced = "resident.scarcity_experienced";
        public const string OpportunityCost = "resident.opportunity_cost";
        public const string DiminishingUtility = "resident.diminishing_utility";
        public const string BudgetChoice = "resident.budget_choice";
        public const string LaborLeisure = "resident.labor_leisure";
        public const string GovernmentOpened = "government.opened";
        public const string ExternalityPolicy = "government.externality_policy";
        public const string TaxPolicy = "government.tax_policy";
        public const string MinimumWage = "government.minimum_wage";
        public const string PriceControl = "government.price_control";
        public const string FiscalCycle = "government.fiscal_cycle";
        public const string MacroOpened = "macro.opened";
        public const string RealNominalObserved = "macro.real_nominal";
        public const string InflationObserved = "macro.inflation";
        public const string UnemploymentObserved = "macro.unemployment";
        public const string RecessionObserved = "macro.recession";
        public const string FiscalResponse = "macro.fiscal_response";
        public const string OutputGapObserved = "macro.output_gap";
        public const string ProductivityPolicy = "macro.productivity";
        public const string MoneyFunctionsExperienced = "finance.money_functions";
        public const string BankDepositMade = "finance.deposit_made";
        public const string BankBalanceViewed = "finance.balance_viewed";
        public const string CreditCreated = "finance.credit_created";
        public const string CompanyInvestmentFinanced = "finance.company_investment";
        public const string InterestChoiceObserved = "finance.interest_choice";
        public const string CentralBankOpened = "finance.central_bank_opened";
        public const string PolicyRateCut = "finance.policy_rate_cut";
        public const string PolicyRateRaised = "finance.policy_rate_raised";
        public const string PolicyLagObserved = "finance.policy_lag_observed";
        public const string BankRunTriggered = "finance.bank_run_triggered";
        public const string BankRunResolved = "finance.bank_run_resolved";
        public const string PolicyMixObserved = "finance.policy_mix";

        // —— V0.7 第七篇「世界市场」trade.* 行为信号 ——
        public const string PortOpened = "trade.port_opened";                       // 首次真实出货
        public const string ComparativeAdvantageExperimentDone = "trade.comparative_advantage";
        public const string ImportConsumed = "trade.import_consumed";               // 居民购买进口品
        public const string TariffEnacted = "trade.tariff_enacted";                 // 关税≠0 且有完税进口
        public const string ExchangeRateObserved = "trade.fx_observed";             // 打开外汇页
        public const string DepreciationExperienced = "trade.depreciation";         // 单日贬值 ≥0.35%
        public const string CapitalFlowObserved = "trade.capital_flow";             // |净流动| ≥60 B币
        public const string ForeignRecessionFelt = "trade.foreign_recession";
        public const string TradeWarResponseChosen = "trade.war_choice";
        public const string ReserveDefenseTried = "trade.reserve_defense";
        public const string TrilemmaDemoDone = "trade.trilemma";
        public const string PortOpeningCeremony = "ceremony.portOpen";
        // —— V0.9.5 第八篇「资产与杠杆」housing.* 行为信号（PRD §197.2，全部从真实账本/状态推导）——
        public const string HousingRentPaid = "housing.rentPaid";                     // 租房中且累计付过租金
        public const string HousingMortgageIssued = "housing.mortgageIssued";         // 玩家房贷放款入账
        public const string HousingPriceBreakdown = "housing.priceBreakdown";         // 读过一次房价分解
        public const string HousingDrawdownSurvived = "housing.drawdownSurvived";     // 在贷扛过峰值回撤 ≥8%
        public const string HousingAssetsDiversified = "housing.assetsDiversified";   // 房贷走完+储蓄三档各有动作
        // —— V0.7.3 三处新区的一次性落成演出（§137 队列+signals 双保险，§142）——
        public const string EastDistrictCeremony = "ceremony.eastDistrict";
        public const string CivicPlazaCeremony = "ceremony.civicPlaza";
        public const string FinancialStreetCeremony = "ceremony.financialStreet";
    }
}
