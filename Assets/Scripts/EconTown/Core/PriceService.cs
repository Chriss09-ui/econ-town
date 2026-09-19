using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public struct PriceQuote
    {
        public float listedPrice;
        public float uncontrolledConsumerPrice;
        public float consumerPrice;
        public float producerNetPrice;
        public float demandElasticity;
        public float supplyElasticity;
        public float consumerTaxShare;
        public bool priceCeilingBinding;
        public bool priceFloorBinding;
    }

    public static class PriceService
    {
        public static float PlayerListedPrice(GameState state)
        {
            return PlayerListedPrice(state, state == null ? Eco.BasePrice : state.price);
        }

        public static float PlayerListedPrice(GameState state, float baseListedPrice)
        {
            if (state == null) return Mathf.Max(1f, baseListedPrice);
            state.EnsureEconomyState();
            EconomyState economy = state.economy;
            bool active = economy.playerTemporaryPriceExpiresAfterDay >= state.day;
            float delta = active ? economy.playerTemporaryPriceDelta : 0f;
            return Mathf.Clamp(baseListedPrice + delta, 1f, 1000f);
        }

        public static float DemandElasticity(GameState state)
        {
            return ConsumerType.Student.share * ConsumerType.Student.elasticity +
                ConsumerType.WhiteCollar.share * ConsumerType.WhiteCollar.elasticity +
                ConsumerType.Tourist.share * ConsumerType.Tourist.elasticity;
        }

        public static float SupplyElasticity(GameState state)
        {
            if (state == null) return 1f;
            ProductionCapacity capacity = CostService.Capacity(state);
            float slack = capacity.actualCapacity <= 0 ? 0f :
                Mathf.Clamp01((capacity.actualCapacity - state.planProduction) / (float)capacity.actualCapacity);
            return Mathf.Lerp(1.30f, 2.30f, slack);
        }

        public static PriceQuote Quote(GameState state, float listedPrice)
        {
            return Quote(state, listedPrice, true);
        }

        public static PriceQuote Quote(GameState state, float listedPrice, bool applyPolicy)
        {
            float demandElasticity = DemandElasticity(state);
            float supplyElasticity = SupplyElasticity(state);
            float consumerTaxShare = supplyElasticity / Mathf.Max(.01f, supplyElasticity + demandElasticity);
            float tax = applyPolicy ? GovernmentSimulator.PolicyValue(state, PolicyType.DrinkTax) : 0f;
            float subsidy = applyPolicy ? GovernmentSimulator.PolicyValue(state, PolicyType.DrinkSubsidy) : 0f;
            float uncontrolled = Mathf.Max(1f, listedPrice + tax * consumerTaxShare - subsidy * consumerTaxShare);
            float ceiling = applyPolicy ? GovernmentSimulator.PolicyValue(state, PolicyType.PriceCeiling) : 0f;
            float floor = applyPolicy ? GovernmentSimulator.PolicyValue(state, PolicyType.PriceFloor) : 0f;
            bool ceilingBinding = ceiling > 0f && ceiling < uncontrolled - .001f;
            bool floorBinding = floor > 0f && floor > uncontrolled + .001f;
            float consumerPrice = uncontrolled;
            if (floorBinding) consumerPrice = floor;
            if (ceilingBinding) consumerPrice = ceiling;
            consumerPrice = Mathf.Max(1f, consumerPrice);
            return new PriceQuote
            {
                listedPrice = listedPrice,
                uncontrolledConsumerPrice = uncontrolled,
                consumerPrice = consumerPrice,
                producerNetPrice = Mathf.Max(0f, consumerPrice - tax + subsidy),
                demandElasticity = demandElasticity,
                supplyElasticity = supplyElasticity,
                consumerTaxShare = consumerTaxShare,
                priceCeilingBinding = ceilingBinding,
                priceFloorBinding = floorBinding
            };
        }
    }

}
