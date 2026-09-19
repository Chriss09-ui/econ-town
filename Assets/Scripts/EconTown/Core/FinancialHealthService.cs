using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public static class FinancialHealthService
    {
        public const float CrisisCashThreshold = -1000f;
        public const int CrisisDays = 3;

        public static void RecordSettledDay(GameState state)
        {
            if (state == null) return;
            state.EnsureEconomyState();
            EconomyState economy = state.economy;
            if (economy.financialHealthLastWorldDay == state.day) return;
            economy.financialHealthLastWorldDay = state.day;
            if (state.money < CrisisCashThreshold) economy.financialDistressDays++;
            else economy.financialDistressDays = 0;
            economy.financialCrisisActive = economy.financialDistressDays >= CrisisDays;
        }

        public static void Resolve(GameState state)
        {
            if (state == null) return;
            state.EnsureEconomyState();
            state.economy.financialCrisisActive = false;
            state.economy.financialDistressDays = 0;
        }
    }

}
