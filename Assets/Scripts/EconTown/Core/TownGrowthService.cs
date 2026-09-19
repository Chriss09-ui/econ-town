using UnityEngine;

namespace EconTown
{
    /// <summary>新区生命周期：未开工 → 施工(围挡) → 落成(完工态)。</summary>
    public enum DistrictPhase { NotStarted = 0, UnderConstruction = 1, Completed = 2 }

    /// <summary>
    /// V0.7.3《生长的小镇》(PRD §140–146) 新区状态推导。
    /// 铁律：一切状态都是 completedChapter 的纯函数（§145-1），
    /// 本文件不写任何游戏状态；一次性演出走 pendingCeremonies 队列（§137 双保险先例）。
    /// </summary>
    public static class TownGrowthService
    {
        // —— 三处新区的施工/落成里程碑（完成触发章开始施工，完成落成章次晨交付）——
        public const int EastDistrictConstructionChapter = 5;    // 东商业新区：完成第 5 章开工
        public const int EastDistrictCompletionChapter = 10;     // 完成第 10 章落成
        public const int CivicPlazaConstructionChapter = 16;     // 行政广场：完成第 16 章开工
        public const int CivicPlazaCompletionChapter = 22;       // 完成第 22 章落成
        public const int FinancialStreetConstructionChapter = 36;// 金融街：完成第 36 章开工
        public const int FinancialStreetCompletionChapter = 42;  // 完成第 42 章落成

        /// <summary>「本周」口径（PRD §123C）：本周 = ⌊(WorldDay−1)/7⌋+1。书店每周概念按它切换。</summary>
        public static int WeekOf(int worldDay)
        {
            return (Mathf.Max(1, worldDay) - 1) / 7 + 1;
        }

        public static DistrictPhase EastDistrict(int completedChapter)
        {
            return Phase(completedChapter, EastDistrictConstructionChapter, EastDistrictCompletionChapter);
        }

        public static DistrictPhase CivicPlaza(int completedChapter)
        {
            return Phase(completedChapter, CivicPlazaConstructionChapter, CivicPlazaCompletionChapter);
        }

        public static DistrictPhase FinancialStreet(int completedChapter)
        {
            return Phase(completedChapter, FinancialStreetConstructionChapter, FinancialStreetCompletionChapter);
        }

        public static DistrictPhase Phase(int completedChapter, int constructionChapter, int completionChapter)
        {
            if (completedChapter >= completionChapter) return DistrictPhase.Completed;
            return completedChapter >= constructionChapter ? DistrictPhase.UnderConstruction : DistrictPhase.NotStarted;
        }

        // —— GDP 装饰彩蛋（§144，P2）：名义 GDP 较开局基准每 +50% 一档，共 6 档封顶 ——

        /// <summary>开局基准 = MacroEconomyState.nominalGdp 的声明初值（MacroInitialDeclaredValues 锁定）。</summary>
        public const float GdpBaselineYuan = 18570.12f;
        public const int GdpTierMax = 6;

        /// <summary>0 = 未达 +50%；+50% → 1 … +300% → 6，超过封顶不再升档。纯读宏观账本推导。</summary>
        public static int GdpTierIndex(float nominalGdp)
        {
            if (nominalGdp <= GdpBaselineYuan) return 0;
            float ratio = nominalGdp / GdpBaselineYuan - 1f;
            if (ratio < 0.5f) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(ratio / 0.5f), 1, GdpTierMax);
        }

        /// <summary>档位名（HUD/验收用）：与 §144 六档一一对应，0 档无彩蛋。</summary>
        public static string GdpTierName(int tier)
        {
            switch (tier)
            {
                case 1: return "路灯彩灯";
                case 2: return "广场喷泉灯光秀";
                case 3: return "公园雕塑";
                case 4: return "公交站台广告牌";
                case 5: return "居民区屋顶花园";
                case 6: return "全镇灯带";
                default: return "";
            }
        }
    }
}
