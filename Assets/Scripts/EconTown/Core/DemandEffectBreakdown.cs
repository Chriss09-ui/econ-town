using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。
    // [Serializable] 必须保留:它嵌在 DayRecord 里随企业历史进存档,丢了会被 JsonUtility 静默跳过。
    [Serializable]
    public class DemandEffectBreakdown
    {
        public float baseDemand;
        public float consumerTypeMultiplier = 1f;
        public float eventMultiplier = 1f;
        public float macroMultiplier = 1f;
        public float residentPreferenceMultiplier = 1f;
        public float priceEffectMultiplier = 1f;
        public float competitionShare = 1f;
        public float noPurchaseShare;

        // 以数量表示的顺序瀑布归因。每一项都来自本次消费者选择计算，
        // 可以与基础需求直接加总为 desiredDemand，不是日报阶段的事后猜测。
        public int segmentEffect;
        public int eventEffect;
        public int macroEffect;
        public int ownPriceEffect;
        public int competitorPriceEffect;
        public int policyPriceEffect;
        public int qualityEffect;
        public int brandEffect;
        public int competitorAppealEffect;
        public int marketingEffect;
        public int residentPreferenceEffect;
        public int competitionEventEffect;
        public int randomEffect;
        public int stockoutSwitchEffect;
        public int desiredDemand;

        public int ModeledTotal
        {
            get
            {
                return Mathf.RoundToInt(baseDemand) + segmentEffect + eventEffect + macroEffect +
                    ownPriceEffect + competitorPriceEffect + policyPriceEffect + qualityEffect +
                    brandEffect + competitorAppealEffect + marketingEffect + residentPreferenceEffect +
                    competitionEventEffect + randomEffect + stockoutSwitchEffect;
            }
        }

        public bool IsBalanced { get { return ModeledTotal == desiredDemand; } }

        public DemandEffectBreakdown Copy()
        {
            return (DemandEffectBreakdown)MemberwiseClone();
        }
    }

}
