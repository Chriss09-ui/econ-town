using System;
using System.Collections.Generic;

namespace EconTown
{
    public enum MacroCyclePhase { Recovery, Expansion, Overheating, Slowdown, Recession, Trough }
    public enum MacroShockType
    {
        None, ConsumptionBoom, InvestmentBoom, EnergyCrisis,
        GlobalDemandFall, AiRevolution, Disaster, PopulationGrowth,
        TradeWarEscalation, CapitalSuddenStop, DevaluationSpike,
        // V0.9.3 杠杆周期(§196.3):住房引擎纯状态触发入队(单槽覆写),非外生按钮冲击
        HousingReversal
    }
    public enum FiscalStance { Neutral, Expansionary, Contractionary }

    [Serializable]
    public class MacroPeriodRecord
    {
        public int period;
        public float nominalGdp;
        public float realGdp;
        public float potentialGdp;
        public float gdpGrowth;
        public float perCapitaGdp;
        public float consumption;
        public float investment;
        public float governmentSpending;
        public float exports;
        public float imports;
        public float netExports;
        public float cpi;
        public float inflation;
        public int population;
        public int workingAgePopulation;
        public int laborForce;
        public int employment;
        public int unemployment;
        public float unemploymentRate;
        public int frictionalUnemployment;
        public int structuralUnemployment;
        public int cyclicalUnemployment;
        public float averageWage;
        public float consumerConfidence;
        public float businessConfidence;
        public float productivity;
        public float capitalStock;
        public float aggregateDemand;
        public float aggregateSupply;
        public float outputGap;
        public float deficitToGdp;
        public float debtToGdp;
        public float incomeInequality;
        public float policyRate;
        public float depositRate;
        public float companyLoanRate;
        public float creditGrowth;
        public float bankReserveRatio;
        public float bankEquity;
        public float financialStress;
        public float moneySupplyM0;
        public float moneySupplyM1;
        public float financialConsumptionEffect;
        public float financialInvestmentEffect;
        // V0.7 国际经济:本期汇率对通胀的贡献(p.p.,双滚分差法≥0)、当期汇率快照、当期关税收入。
        public float exchangeRateContributionPp;
        public float exchangeRate;
        public float tariffRevenue;
        public MacroCyclePhase cyclePhase;
        public MacroShockType shock;
        public string headline;
        public string causalChain;
    }

    [Serializable]
    public class MacroEconomyState
    {
        public int period = 1;
        public int chapter = 1;
        public int chapterMask = 1;

        // 以下声明初值 = MacroEconomySimulator.AdvancePeriod 对空记录首期跑出的公式输出
        // (由 EconTownP0FixesTests.MacroInitialDeclaredValuesMatchFirstFormulaPass 护栏锁定:
        // 改公式必须同步更新这里的初值,否则宏观面板首期读数会无解释跳变)。
        public float nominalGdp = 18225.83f;
        public float realGdp = 18258.69f;
        public float potentialGdp = 22582.81f;
        public float gdpGrowth;
        public float perCapitaGdp = 57.058f;
        public float consumption = 14664.96f;
        public float investment = 3152.816f;
        public float governmentSpending = 600f;
        public float exports = 680f;
        public float imports = 871.949f;
        public float netExports = -191.949f;

        public float cpi = 100f;
        public float inflation;
        public float foodPriceIndex = 100f;
        public float housingPriceIndex = 100f;
        public float transportPriceIndex = 100f;
        public float beveragePriceIndex = 100f;
        public float entertainmentPriceIndex = 100f;
        public float otherPriceIndex = 100f;

        public int workingAgePopulation = 250;
        public int laborForce = 222;
        // bug搜#1:声明值=首期公式输出的不动点(就业声明是消费/产出公式的输入,改六字段会级联
        // 移动 GDP 系输出——CodeDom 迭代 3 轮收敛):旧声明值是旧口径,开局失业率/产出缺口
        // 读数一日内无解释跳变(7.66%→16.67%),且护栏只钉 GDP 系 13 字段、就业六字段漏网。
        public int employment = 185;
        public int unemployment = 37;
        public float unemploymentRate = 16.67f;
        public int frictionalUnemployment = 6;
        public int structuralUnemployment = 4;
        public int cyclicalUnemployment = 27;

        public float averageWage = 25f;   // C4:与首期公式 EffectiveDailyWage/8(=200/8)一致,避免首期前读数跳变
        public float consumerConfidence = 68f;
        public float businessConfidence = 72f;
        public float productivity = 100f;
        public float capitalStock = 12000f;
        // 学校带来的生产率跃升只在建成后的第一期授予一次(C7),持久化以防读档后重复授予。
        public bool schoolProductivityBonusGranted;
        public float aggregateDemand = 18258.69f;
        public float aggregateSupply = 22582.81f;
        public float outputGap = -19.1478f;
        public float energyCostIndex = 100f;
        public float externalDemandIndex = 100f;
        public float importPriceIndex = 100f;
        public float exchangeRateSnapshot = 7f;   // 本期生效的「1 B币 = X A币」快照(面板读数用)
        // 本期汇率对 CPI 的净贡献(p.p.,可负=升值输入型通缩),双滚分差法计算,±1.5pt 钳制。
        public float exchangeRateInflationContribution;
        public float deficitToGdp;
        public float debtToGdp;
        public float incomeInequality = 35f;
        public float policyRate = 3f;
        public float depositRate = 2f;
        public float companyLoanRate = 5.5f;
        public float creditGrowth;
        public float bankReserveRatio = 1f;
        public float bankEquity;
        public float financialStress;
        public float moneySupplyM0;
        public float moneySupplyM1;
        public float financialConsumptionEffect;
        public float financialInvestmentEffect;
        public float marginalPropensityToConsume = .72f;
        // 与 MacroEconomySimulator.Ensure 的公式保持一致(1/(1-MPC)),避免首次 Ensure 后数值跳变。
        public float fiscalMultiplier = 1f / (1f - .72f);

        public float plannedPublicInvestment;
        public float plannedTaxChange;
        public float oneOffGovernmentPurchases;
        public FiscalStance fiscalStance = FiscalStance.Neutral;
        public int fiscalLagStage;
        public MacroShockType pendingShock;
        // bug搜#1复审:首期分类器 previous==null→growth=0、outputGap≈−19.1<−5 → Trough;
        // 声明 Recovery 会让宏观面板首期「复苏」在首个收日无解释跳成「谷底」(同族跳变)。
        public MacroCyclePhase cyclePhase = MacroCyclePhase.Trough;
        public string latestHeadline = "经济运行于潜在水平下方，存在明显产出缺口";
        public string latestCausalChain = "居民消费、企业投资、政府购买与净出口共同构成总需求。";
        public string policyPreview = "尚未选择宏观财政方案。";
        public string nationalStyle = "稳定型";

        public List<MacroPeriodRecord> history = new List<MacroPeriodRecord>();
        public List<string> news = new List<string>();
    }
}
