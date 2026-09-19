using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7 国际贸易价格服务(PRD §86)。统一计算:外国消费者到手价、进口到岸价、
    /// 关税后价格、本币折算价格。所有楔子(运输、B 国关税、本国进口关税、汇率)只在
    /// 这里出现一次,禁止散落在各结算点手写乘法。纯函数读层——不写任何 GameState 数值。
    /// </summary>
    public static class InternationalPriceService
    {
        /// <summary>玩家实收出厂价(Fob):B 国基准外币价折回本币。本币贬值(A币变便宜)则 Fob 升。</summary>
        public static float ProducerFobPerUnit(GameState state)
        {
            if (state == null || state.worldTrade == null || state.worldTrade.countryB == null)
                return 1.05f * ExchangeRateService.DefaultBaseRate;
            return Mathf.Max(.1f, state.worldTrade.countryB.drinkBenchmarkPriceBCurrency) *
                ExchangeRateService.Rate(state);
        }

        /// <summary>出口运输楔子(A币/杯),由玩家企业承担。</summary>
        public static float ShippingWedgePerUnit(GameState state)
        {
            return state != null && state.worldTrade != null ? state.worldTrade.shippingCostPerUnitAcurrency : 2.2f;
        }

        /// <summary>B 国消费者到手价 = Fob + 运输 + Fob×B 国关税(完全转嫁的教学简化)。</summary>
        public static float ForeignFacePricePerUnit(GameState state)
        {
            if (state == null || state.worldTrade == null || state.worldTrade.countryB == null)
                return ProducerFobPerUnit(null) + ShippingWedgePerUnit(null);
            CountryProfileState b = state.worldTrade.countryB;
            float fob = ProducerFobPerUnit(state);
            return fob + ShippingWedgePerUnit(state) + fob * Mathf.Max(0f, b.tariffOnAExports);
        }

        /// <summary>本国对进口消费品的从价关税率(0~25%,ImportTariff 政策值 ÷100)。</summary>
        public static float TariffRateConsumer(GameState state)
        {
            return Mathf.Clamp(GovernmentSimulator.PolicyValue(state, PolicyType.ImportTariff), 0f, 60f) / 100f;
        }

        /// <summary>
        /// 居民进口最终消费品到岸价 = 外币标价 × 汇率 × (1 + 本国关税率)。
        /// 本币贬值(汇率↑)直接推高进口品价格——输入型通胀的微观入口。
        /// </summary>
        public static float ImportedSkuConsumerPrice(GameState state, float baseForeignPrice)
        {
            float foreign = Mathf.Max(.1f, baseForeignPrice);
            return foreign * ExchangeRateService.Rate(state) * (1f + TariffRateConsumer(state));
        }

        /// <summary>
        /// 中间投入进口因子 ∈ [.9,1.3]:本币贬值(汇率相对锚上行)放大原料采购成本。
        /// rate=锚时恰为 1;指数 -.55 使 ±20% 的汇率波动映射到约 -11%~+11% 的成本变化。
        /// </summary>
        public static float IntermediateImportFactor(GameState state)
        {
            float anchor = DefaultAnchor(state);
            float rate = ExchangeRateService.Rate(state);
            return Mathf.Clamp(Mathf.Pow(anchor / Mathf.Max(.01f, rate), -.55f), .9f, 1.3f);
        }

        public static float DefaultAnchor(GameState state)
        {
            if (state == null || state.worldTrade == null || state.worldTrade.exchangeRate == null ||
                state.worldTrade.exchangeRate.baseRate <= 0f)
                return ExchangeRateService.DefaultBaseRate;
            return state.worldTrade.exchangeRate.baseRate;
        }
    }
}
