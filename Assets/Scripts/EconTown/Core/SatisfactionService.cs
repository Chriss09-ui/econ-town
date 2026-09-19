using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public static class SatisfactionService
    {
        public static float Calculate(float consumerPrice, int desiredDemand, int actualSales)
        {
            return Calculate(consumerPrice, desiredDemand, actualSales, 50f, 50f);
        }

        public static float Calculate(float consumerPrice, int desiredDemand, int actualSales,
            float quality, float brand)
        {
            float satisfaction = 73f + Mathf.Clamp(quality, 0f, 100f) * .20f +
                Mathf.Clamp(brand, 0f, 100f) * .08f;
            float difference = consumerPrice - Eco.BasePrice;
            satisfaction += difference > 0f ? -difference * 3.2f : -difference * 1.2f;
            float fulfillment = desiredDemand <= 0 ? 1f :
                Mathf.Clamp01(actualSales / (float)desiredDemand);
            satisfaction -= (1f - fulfillment) * 12f;
            return Mathf.Clamp(satisfaction, 40f, 100f);
        }
    }
}
