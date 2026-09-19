using System;
using System.Collections.Generic;

namespace EconTown
{
    /// <summary>V0.7 汇率制度。Floating=0 保证旧档缺省反序列化即浮动汇率。</summary>
    public enum ExchangeRegime { Floating = 0, Fixed = 1 }

    /// <summary>
    /// B 国(外部经济体)画像:只维护与 A 国互动所需的宏观与偏好参数,
    /// 每日由 InternationalTradeService 用简单趋势动态推进——不建完整第二经济体。
    /// </summary>
    [Serializable]
    public class CountryProfileState
    {
        public bool initialized;
        public string name = "海外经济体 B";
        public float incomeLevel = 100f;                     // 人均收入指数:驱动外国市场规模
        public float gdpGrowthPeriod = .18f;                 // 每期 GDP 增速(%)
        public float inflationPeriod = .12f;                 // 每期通胀(%)
        public float cpiLevel = 100f;
        public float policyRate = 4f;                        // B 国政策利率(%),利差 = 本国 − 此
        public float drinkBenchmarkPriceBCurrency = 1.05f;   // 一杯同类饮品的 B 国基准外币价
        public float preferenceDomestic = .62f;              // 对本国品牌偏好(压低进口吸引力)
        public float qualityWeight = .45f;
        public float brandWeight = .18f;
        public float outsideWeight = .38f;                   // 不购份额下限(代表 B 国本地替代品)
        public float tariffOnAExports = .08f;                // B 国对 A 产品从价关税(贸易战事件上调)
        public float growthVolatility = .35f;
        public bool recessionActive;
        public float growthMomentum;                          // 近期增长滚动和(衰退判定的平滑项)
    }

    /// <summary>汇率史一日快照:五项分解之和 ≈ 当日实际相对变动%(新闻归因的数据源)。</summary>
    [Serializable]
    public class RateDayRecord
    {
        public int worldDay;
        public float rate;
        public ExchangeRegime regime;
        public float tradeFlowEffect;
        public float capitalFlowEffect;
        public float expectationEffect;
        public float interventionEffect;
        public float anchorPullEffect;
        public float currentAccountBCurrency;
        public float capitalAccountBCurrency;
        public float forexReservesEndBCurrency;
    }

    /// <summary>
    /// 全项目唯一汇率权威数据(PRD §80)。报价约定恒为「1 B币 = X A币」:
    /// X 变大 = A币贬值,X 变小 = A币升值;任何页面不得反向报价或另行计算。
    /// </summary>
    [Serializable]
    public class ExchangeRateState
    {
        public bool initialized;
        public float baseRate = 7f;                          // 名义锚(教学对照基准)
        public float rate = 7f;                              // 今日生效报价(白天只读)
        public float equilibriumRate = 7f;                   // 无干预市场出清价(失守跳贬目标)
        public ExchangeRegime regime = ExchangeRegime.Floating;
        public float pegTarget = 7f;
        public float bandWidthPercent = .03f;                // 固定汇率偏离带 ±3%
        public float tradeFlowEffect;                        // 本期分解(百分点,Σ≈实际变动%)
        public float capitalFlowEffect;
        public float expectationEffect;
        public float interventionEffect;
        public float anchorPullEffect;                       // 向名义锚的温和回归(防长期贴边漂移)
        public int consecutiveDefenseDays;
        public int consecutiveLowReserveDays;
        public int lastAbandonPegWorldDay = -999;
        public int lastFormedWorldDay;
        public List<RateDayRecord> history = new List<RateDayRecord>();
    }

    /// <summary>金融账户(简化 KA):利差+风险溢价驱动,流入必须真实影响外汇需求(P0)。</summary>
    [Serializable]
    public class CapitalFlowState
    {
        public float interestDifferentialPercent;            // 本国 policyRate − B国 policyRate
        public float riskPremiumPercent = 1.2f;              // 贸易战/危机事件抬升
        public float credibilityMirror = 80f;                // centralBank.credibility 快照
        public float inflowBCurrency;
        public float outflowBCurrency;
        public float expectedDepreciationPercent;            // 贬值预期放大外逃
        public int lastSettledWorldDay;
    }

    /// <summary>
    /// 一个 WorldDay 的国际收支快照:X/M 三路全部来自真实交易(出口订单/居民进口消费/
    /// 中间投入进口),禁用合成系数;reserveChange 必须 ≈ CA + KA(SimDriver 终验对账)。
    /// </summary>
    [Serializable]
    public class TradeDayRecord
    {
        public int worldDay;
        public float exchangeRateUsed;                       // 本日冻结的汇率快照(单向依赖凭证)

        // —— 出口(X)两路真实流量 ——
        public float playerExportUnits;                      // 玩家实际出货杯数(产能配额池)
        public float playerExportRevenue;                    // =量×出厂 Fob 价(A币)
        public float playerShippingCost;                     // 运输楔子(玩家承担)
        public float townExportRevenue;                      // 镇集体其他出口商(同一条外国需求路径)

        // —— 进口(M)三路真实流量 ——
        public float consumerImportSpend;                    // 居民进口最终消费品·税前完税基(外币价×汇率,围猎三#10)
        public float intermediateImportSpend;                // 企业中间投入进口额(A币)
        public float townImportSpend;                        // 其他主体进口(A币)

        public float exportsTotal;                           // X
        public float importsTotal;                           // M
        public float netExports;                             // NX = X − M(宏观直接引用,不再合成)

        // —— 关税:收入 = 税率 × 实际发生并完税的进口额 ——
        public float tariffRateApplied;
        public float tariffBase;
        public float tariffRevenue;

        // —— 国际收支(B币口径) ——
        public float currentAccountBCurrency;                // CA ≈ NX 折算成 B币(教学简化口径)
        public float capitalInflowBCurrency;
        public float capitalOutflowBCurrency;
        public float capitalAccountBCurrency;                // KA = 私人流入 − 流出(不含官方干预)
        public float officialInterventionBCurrency;          // 央行净买入外储(+买入/−卖出,守汇为负)
        public float reserveChangeBCurrency;                 // 必须 == officialInterventionBCurrency(对账)
    }

    /// <summary>V0.7 世界贸易总状态:挂进 GameState 的 [Header("V0.7 国际经济")] 区块。</summary>
    [Serializable]
    public class WorldTradeState
    {
        public bool initialized;
        public CountryProfileState countryB = new CountryProfileState();
        public ExchangeRateState exchangeRate = new ExchangeRateState();
        public CapitalFlowState capitalFlow = new CapitalFlowState();

        public float dailyPortCapacityCups = 90f;            // 港口日处理上限(数量爆炸总闸)
        public float shippingCostPerUnitAcurrency = 2.2f;    // 出口运输楔子(A币/杯)
        // 当日居民进口消费品累计(A币,Consume 钩子累加):结算时并入 TradeDayRecord,
        // 读档丢弃当日记录时由 SaveSystem 一并清零(与 pendingTariff 同组)。
        public float todayConsumerImportSpend;
        public int lastTradeSettledWorldDay;                 // SettleDayTrade 同日幂等守卫
        public int lastTradeClosedWorldDay;                  // CloseTradeDay 同日幂等守卫
        public bool comparativeAdvantageExperimentDone;      // 第 44 章信号来源之一
        public bool trilemmaDemoDone;                        // 第 53 章信号来源之一
        // 贸易战升级事件的待回应标记:事件夜里置位,清晨由世界经济面板弹出三选一。
        public bool pendingTradeWarChoice;

        // 围猎三(#8):730 行截断时被裁行的 reserveChangeBCurrency 累计——外储守恒对账
        // 是跨全史口径,截断丢行会让 AssertNoFreeMoney 在长线存档里永久误报(狼来了)。
        public float trimmedReserveChangeBCurrency;

        public List<TradeDayRecord> history = new List<TradeDayRecord>();
    }
}
