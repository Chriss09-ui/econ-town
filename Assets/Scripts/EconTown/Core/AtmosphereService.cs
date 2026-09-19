using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.1《小镇的呼吸》氛围取数状态机（PRD §123C/§124/§125）。
    /// 唯一职责：在收日管线 AdvanceDay 之后按固定顺序一次性算好"新一天的全部视觉状态"
    /// 并落 economy.atmosphere 缓存；一切 UI 与渲染只读缓存。
    ///
    /// 取数纪律（铁律）：
    /// - 氛围随机只消费 economy.atmosphereRandom，绝不碰 random/tradeRandom；
    /// - 本方法是全项目唯一消费点，由 WorldSettlementService 在固定位置调用一次；
    /// - 事件日不掷装饰骰：天气档直接采用事件绑定档（分支内部消耗次数各自固定，序列仍可复现）。
    /// </summary>
    public static class AtmosphereService
    {
        public const int WeatherClear = 0, WeatherCloudy = 1, WeatherRain = 2, WeatherSnow = 3;

        /// <summary>装饰转让牌的常驻装饰铺数（decor_a/decor_b；东商业新区两间空铺落成后经
        /// DecorativeShopPoolSize 追加，见下方）。</summary>
        public const int DecorativeShopCount = 2;

        // ---- 纯函数（供测试与场景层复用）----

        /// <summary>季节索引 0春 / 1夏 / 2秋 / 3冬。一年=52 世界日，每 13 日一季（PRD §122）。day≥1。</summary>
        public static int SeasonIndex(int day) { return (day - 1) / 13 % 4; }

        public static string SeasonName(int season)
        {
            switch (season) { case 0: return "春"; case 1: return "夏"; case 2: return "秋"; default: return "冬"; }
        }

        /// <summary>企业信心 → 橱窗灯光三档（政府侧权威值，15–100）。≥60 全亮 / 40–60 七成 / <40 四成。</summary>
        public static int WindowTier(float enterpriseConfidence)
        {
            if (enterpriseConfidence >= 60f) return 2;
            return enterpriseConfidence >= 40f ? 1 : 0;
        }

        /// <summary>街景目标人数 = 失业率仪表 + 相位微调（PRD §124）：主锚失业率（连续单调），
        /// 过热 +1、谷底 −1；夹 [5,16]。开局失业率 ≈7.7 时恰为 12，与现状衔接。</summary>
        public static int StreetTarget(float unemploymentRate, MacroCyclePhase phase)
        {
            int target = Mathf.Clamp(Mathf.RoundToInt(12f + (7.7f - unemploymentRate)), 5, 16);
            if (phase == MacroCyclePhase.Overheating) target++;
            else if (phase == MacroCyclePhase.Trough) target--;
            return Mathf.Clamp(target, 5, 16);
        }

        // ---- 收日管线唯一取数点 ----

        public static void ResolveDaily(GameState s)
        {
            s.EnsureEconomyState();
            var atm = s.economy.atmosphere;
            if (atm == null) { s.economy.Ensure(); atm = s.economy.atmosphere; }

            // 1) 换季检测（AdvanceDay 已推进 worldDay；收日时 day≥2，day-1≥1 安全）
            int today = Mathf.Max(1, s.day);
            if (SeasonIndex(today) != SeasonIndex(Mathf.Max(1, today - 1))) atm.seasonRequest = true;

            // 重置当日事件视觉位
            atm.gloomOverlay = false;
            atm.festivalDecor = false;
            atm.heatWave = false;

            // 2) 天气：事件绑定优先（AdvanceDay 内 RollTownEvent 已预掷今日事件），事件日不掷装饰骰
            string eventId = s.todayEventId;
            bool winter = SeasonIndex(today) == 3;
            bool summer = SeasonIndex(today) == 1;
            if (eventId == TownEvents.HotWeather.id)
            {
                atm.weather = WeatherClear;
                atm.heatWave = true;
            }
            else if (eventId == TownEvents.MaterialUp.id)
            {
                atm.weather = WeatherCloudy;
                atm.gloomOverlay = true;   // 事件阴天=云层+灰蒙，与装饰阴天只画云可区分
            }
            else if (eventId == TownEvents.SchoolOff.id)
            {
                atm.weather = WeatherCloudy;
            }
            else if (eventId == TownEvents.Festival.id)
            {
                atm.weather = WeatherClear;
                atm.festivalDecor = true;
            }
            else
            {
                atm.weather = RollDecorWeather(s.economy.atmosphereRandom, summer, winter);
            }

            // 3) 橱窗档（企业信心投影）+ 切换日标记（-1 未初始化的首次不算切换，不触发公告提示）
            float confidence = s.government != null ? s.government.enterpriseConfidence : 72f;
            int newTier = WindowTier(confidence);
            atm.windowTierChangedToday = atm.windowTier >= 0 && newTier != atm.windowTier;
            atm.windowTier = newTier;

            // 4) 转让牌：仅信心 <40 时从固定装饰铺池选一间挂出；其余日撤牌。
            //    池 = 两间临街小铺(0/1)，东商业新区落成后追加两间空铺(2/3)(PRD §125/§141)。
            int pool = DecorativeShopPoolSize(s.progression != null ? s.progression.completedChapter : 0);
            atm.transferSignShop = atm.windowTier == 0 && pool > 0
                ? s.economy.atmosphereRandom.Next(0, pool)
                : -1;

            // 5) 街景人数仪表：目标随失业率重算，在场人数每日 ±2 渐变逼近（衰退是慢慢冷清下来的）
            float unemployment = s.macro != null ? s.macro.unemploymentRate : 7.7f;
            atm.streetTarget = StreetTarget(unemployment, s.macro != null ? s.macro.cyclePhase : MacroCyclePhase.Recovery);
            atm.streetCurrent += Mathf.Clamp(atm.streetTarget - atm.streetCurrent, -2, 2);

            // 6) 书店「本周概念」(V0.7.3 §141)：仅东商业新区落成后、仅周切换日在本固定点掷一次
            //    （PRD §123C 顺序：天气 → 转让牌 → 每周概念）。未落成不占周号，落成当日即掷首期。
            bool eastBuilt = TownGrowthService.EastDistrict(
                s.progression != null ? s.progression.completedChapter : 0) == DistrictPhase.Completed;
            int week = TownGrowthService.WeekOf(today);
            if (!eastBuilt)
            {
                atm.conceptWeek = 0;
                atm.weeklyConceptCard = -1;
            }
            else if (atm.conceptWeek != week)
            {
                atm.conceptWeek = week;
                atm.weeklyConceptCard = PickWeeklyConcept(s);
            }
        }

        /// <summary>装饰转让牌选择池：临街小铺 ×2 常驻，东商业新区两间空铺落成后入池（§125）。</summary>
        public static int DecorativeShopPoolSize(int completedChapter)
        {
            return 2 + (TownGrowthService.EastDistrict(completedChapter) == DistrictPhase.Completed ? 2 : 0);
        }

        /// <summary>从已解锁知识卡中按 atmosphereRandom 选 1 张（CardLibrary.All 顺序为稳定序）。
        /// 无已解锁卡返回 -1。只在 ResolveDaily 固定点调用，流推进次数与玩家操作序列无关。</summary>
        static int PickWeeklyConcept(GameState s)
        {
            if (s.unlockedCards == null || s.unlockedCards.Count == 0) return -1;
            int count = 0;
            for (int i = 0; i < CardLibrary.All.Count; i++)
                if (s.unlockedCards.Contains(CardLibrary.All[i].id)) count++;
            if (count == 0) return -1;
            int pick = s.economy.atmosphereRandom.Next(0, count);
            for (int i = 0; i < CardLibrary.All.Count; i++)
            {
                if (!s.unlockedCards.Contains(CardLibrary.All[i].id)) continue;
                if (pick == 0) return i;
                pick--;
            }
            return -1;
        }

        /// <summary>装饰性天气掷骰（纯视觉不影响任何数值）：晴 55/多云 25/雨 15/雪 5（雪仅冬，
        /// 夏季晴上浮至 65 从多云雨各扣 5）。非冬掷出雪重掷一次为多云（重掷后仍落雪段视为多云）。</summary>
        static int RollDecorWeather(DeterministicRandomState rng, bool summer, bool winter)
        {
            float clearP = summer ? .65f : .55f;
            float cloudyP = summer ? .20f : .25f;
            float rainP = summer ? .10f : .15f;
            float r = rng.Next01();
            if (r < clearP) return WeatherClear;
            if (r < clearP + cloudyP) return WeatherCloudy;
            if (r < clearP + cloudyP + rainP) return WeatherRain;
            if (!winter)
            {
                rng.Next01();           // 非冬重掷（PRD §123B），固定次数保证确定性
                return WeatherCloudy;
            }
            return WeatherSnow;
        }
    }
}
