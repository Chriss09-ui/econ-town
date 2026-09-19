using System;
using UnityEngine;

namespace EconTown
{
    public enum PortCraneTier { Idle = 0, Slow = 1, Medium = 2, Full = 3 }

    /// <summary>
    /// 港区只读投影。所有字段都来自 WorldTradeState 的真实账本与外部经济状态，
    /// 不保存第二份贸易数据，也不允许画面反写经济账。
    /// </summary>
    public sealed class PortDisplaySnapshot
    {
        public TradeDayRecord row;
        public int settledWorldDay;
        public float playerExportCups;
        public float townExportValue;
        public float importsLastThreeDays;
        public float playerExportsLastSevenDays;
        public float importsLastSevenDays;
        public int exportContainerCount;
        public int importContainerCount;
        public PortCraneTier craneTier;
        public float cranePeriodSeconds;
        public bool shipShouldDock;
        public bool recessionActive;
        public bool tariffStripe;
        public bool redPortFlag;
        public float exchangeRate;
        public int exportQuotaCups;
    }

    /// <summary>V0.7.2 港区呈现的唯一取数层（PRD §132–136）。</summary>
    public static class PortPresentationService
    {
        public const float BaselineForeignTariff = .08f;
        public const int MaxExportContainers = 18;
        public const int MaxImportContainers = 6;

        public static PortDisplaySnapshot Read(GameState state)
        {
            var result = new PortDisplaySnapshot();
            if (state == null || state.worldTrade == null) return result;

            WorldTradeState trade = state.worldTrade;
            TradeDayRecord row = trade.history != null && trade.history.Count > 0
                ? trade.history[trade.history.Count - 1] : null;
            result.row = row;
            result.settledWorldDay = row != null ? row.worldDay : 0;
            result.playerExportCups = row != null ? Mathf.Max(0f, row.playerExportUnits) : 0f;
            result.townExportValue = row != null ? Mathf.Max(0f, row.exportsTotal) : 0f;
            result.exchangeRate = row != null && row.exchangeRateUsed > 0f
                ? row.exchangeRateUsed
                : trade.exchangeRate != null ? trade.exchangeRate.rate : 0f;
            result.exportQuotaCups = Mathf.Max(0, state.exportTargetQuotaCups);

            if (trade.history != null && trade.history.Count > 0)
            {
                int lastDay = trade.history[trade.history.Count - 1].worldDay;
                int sevenDayStart = lastDay - 6;
                int threeDayStart = lastDay - 2;
                for (int i = trade.history.Count - 1; i >= 0; i--)
                {
                    TradeDayRecord item = trade.history[i];
                    if (item == null) continue;
                    if (item.worldDay < sevenDayStart) break;
                    result.playerExportsLastSevenDays += Mathf.Max(0f, item.playerExportUnits);
                    result.importsLastSevenDays += Mathf.Max(0f, item.importsTotal);
                    if (item.worldDay >= threeDayStart)
                        result.importsLastThreeDays += Mathf.Max(0f, item.importsTotal);
                }
            }

            result.exportContainerCount = ExportContainerCount(result.playerExportsLastSevenDays);
            result.importContainerCount = ImportContainerCount(result.importsLastSevenDays);
            result.craneTier = CraneTier(result.playerExportCups);
            result.cranePeriodSeconds = CranePeriodSeconds(result.craneTier);
            result.shipShouldDock = row != null && row.playerExportUnits > 0f && row.worldDay % 3 == 0;
            result.recessionActive = trade.countryB != null && trade.countryB.recessionActive;
            result.tariffStripe = trade.countryB != null &&
                trade.countryB.tariffOnAExports > BaselineForeignTariff + .0001f;
            result.redPortFlag = trade.pendingTradeWarChoice || result.tariffStripe;
            return result;
        }

        public static PortCraneTier CraneTier(float shippedCups)
        {
            if (shippedCups <= 0f) return PortCraneTier.Idle;
            if (shippedCups <= 30f) return PortCraneTier.Slow;
            if (shippedCups <= 60f) return PortCraneTier.Medium;
            return PortCraneTier.Full;
        }

        public static float CranePeriodSeconds(PortCraneTier tier)
        {
            switch (tier)
            {
                case PortCraneTier.Slow: return 4f;
                case PortCraneTier.Medium: return 2.5f;
                case PortCraneTier.Full: return 1.6f;
                default: return 8f;
            }
        }

        public static int ExportContainerCount(float sevenDayPlayerExports)
        {
            if (sevenDayPlayerExports <= 0f) return 0;
            return Mathf.Min(2 + Mathf.FloorToInt(sevenDayPlayerExports / 35f), MaxExportContainers);
        }

        public static int ImportContainerCount(float sevenDayImports)
        {
            return Mathf.Min(Mathf.FloorToInt(Mathf.Max(0f, sevenDayImports) / 900f), MaxImportContainers);
        }

        public static bool ShouldPlayFullArrival(PortDisplaySnapshot snapshot,
            PortPresentationState presentation, WorldExperienceSettings settings, bool allowThisMorning)
        {
            if (!allowThisMorning || snapshot == null || !snapshot.shipShouldDock ||
                presentation == null || settings == null || !settings.atmosphereEffects ||
                !settings.fullPortAnimations) return false;
            return !presentation.hasSeenArrival || presentation.lastArrivalTier != (int)snapshot.craneTier;
        }

        public static string NetExportsTeachingLine(PortDisplaySnapshot snapshot)
        {
            float nx = snapshot != null && snapshot.row != null ? snapshot.row.netExports : 0f;
            if (nx >= 0f)
                return "NX = +" + nx.ToString("0") + "：小镇把这部分产出卖给了国外，国内可消费的相应减少。";
            return "NX = " + nx.ToString("0") + "：正在享用国外的产出，代价是外债或贬值压力。";
        }
    }
}
