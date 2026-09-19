using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public static class WorldClock
    {
        public static void SyncDerivedPeriods(GameState state)
        {
            if (state == null) return;
            state.EnsureEconomyState();
            int worldDay = state.economy.worldDay;
            if (state.resident != null) state.resident.lifeDay = worldDay;
            if (state.government != null) state.government.cycle = worldDay;
            if (state.macro != null) state.macro.period = worldDay;
        }
    }

}
