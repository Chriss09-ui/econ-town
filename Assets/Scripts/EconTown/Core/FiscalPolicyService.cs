using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public static class FiscalPolicyService
    {
        public static float DirectPurchase(MacroEconomyState macro)
        {
            if (macro == null || macro.fiscalStance == FiscalStance.Neutral || macro.fiscalLagStage <= 0) return 0f;
            float[] spendingShare = { 0f, .04f, .16f, .30f, .30f, .20f };
            int stage = Mathf.Clamp(macro.fiscalLagStage, 1, 6);
            if (stage >= spendingShare.Length) return 0f;
            float sign = macro.fiscalStance == FiscalStance.Expansionary ? 1f : -1f;
            return sign * macro.plannedPublicInvestment * spendingShare[stage];
        }

        public static float InducedDemand(MacroEconomyState macro, float directPurchase)
        {
            if (macro == null || macro.fiscalStance == FiscalStance.Neutral || macro.fiscalLagStage <= 0) return 0f;
            return directPurchase * Mathf.Max(0f, macro.fiscalMultiplier - 1f);
        }

        public static float AutomaticTaxChange(MacroEconomyState macro)
        {
            if (macro == null) return 0f;
            return -Mathf.Max(0f, -macro.outputGap) * 5f + Mathf.Max(0f, macro.outputGap) * 6f;
        }

        public static float AutomaticWelfare(MacroEconomyState macro)
        {
            return macro == null ? 0f : Mathf.Max(0f, -macro.outputGap) * 7.5f;
        }

        public static void AdvanceLag(MacroEconomyState macro)
        {
            if (macro == null || macro.fiscalStance == FiscalStance.Neutral || macro.fiscalLagStage <= 0) return;
            macro.fiscalLagStage = Mathf.Min(6, macro.fiscalLagStage + 1);
        }
    }
}
