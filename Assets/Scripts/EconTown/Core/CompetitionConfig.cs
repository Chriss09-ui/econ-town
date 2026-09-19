using UnityEngine;

namespace EconTown
{
    public enum CompetitorStrategy { Stable, LowPriceExpansion, PriceFollower, Differentiation }
    public enum PlayerCompetitionChoice { None, FollowPrice, HoldPrice, ImproveQuality, Advertise }

    public class CompetitionConsumerPrefs
    {
        public float priceWeight;
        public float qualityWeight;
        public float brandWeight;
        public float outsideWeight;
        public float playerDistanceBonus;
        public float competitorDistanceBonus;
    }

    /// <summary>V0.2 竞争市场参数。数值集中在这里，方便后续平衡而不污染 UI。</summary>
    public static class CompetitionEco
    {
        public const int BaseMarketDemand = 180;
        public const float MinPrice = 5f;
        public const float MaxPrice = 30f;

        public static readonly CompetitionConsumerPrefs Student = new CompetitionConsumerPrefs
        {
            priceWeight = 0.50f, qualityWeight = 0.20f, brandWeight = 0.10f, outsideWeight = 0.20f,
            playerDistanceBonus = 0.12f, competitorDistanceBonus = 0.08f
        };
        public static readonly CompetitionConsumerPrefs WhiteCollar = new CompetitionConsumerPrefs
        {
            priceWeight = 0.25f, qualityWeight = 0.40f, brandWeight = 0.25f, outsideWeight = 0.10f,
            playerDistanceBonus = 0.10f, competitorDistanceBonus = 0.10f
        };
        public static readonly CompetitionConsumerPrefs Tourist = new CompetitionConsumerPrefs
        {
            priceWeight = 0.15f, qualityWeight = 0.30f, brandWeight = 0.40f, outsideWeight = 0.15f,
            playerDistanceBonus = 0.08f, competitorDistanceBonus = 0.12f
        };

        public static float QualityForLevel(int level)
        {
            if (level <= 1) return 50f;
            if (level == 2) return 70f;
            return 85f;
        }

        public static float UnitCostForLevel(int level)
        {
            if (level <= 1) return 6f;
            if (level == 2) return 8f;
            return 11f;
        }

        public static float UpgradeCost(int nextLevel)
        {
            if (nextLevel == 2) return 900f;
            if (nextLevel == 3) return 1600f;
            return 0f;
        }

        public static string StrategyName(CompetitorStrategy strategy)
        {
            switch (strategy)
            {
                case CompetitorStrategy.LowPriceExpansion: return "低价扩张";
                case CompetitorStrategy.PriceFollower: return "激进跟降";
                case CompetitorStrategy.Differentiation: return "差异化经营";
                default: return "稳定经营";
            }
        }

        public static string ChoiceName(string choice)
        {
            PlayerCompetitionChoice parsed;
            if (!System.Enum.TryParse(choice, out parsed)) return "自由决策";
            switch (parsed)
            {
                case PlayerCompetitionChoice.FollowPrice: return "跟随降价";
                case PlayerCompetitionChoice.HoldPrice: return "保持原价";
                case PlayerCompetitionChoice.ImproveQuality: return "提高品质";
                case PlayerCompetitionChoice.Advertise: return "增加广告";
                default: return "自由决策";
            }
        }

        public static bool HasPlayerChoice(string choice)
        {
            PlayerCompetitionChoice parsed;
            return System.Enum.TryParse(choice, out parsed) && parsed != PlayerCompetitionChoice.None;
        }
    }

    /// <summary>规则驱动的竞争对手。每天清晨只决策一次，避免 UI 与结算阶段改写策略。</summary>
    public static class CompetitorAI
    {
        public static void PrepareDay(GameState s)
        {
            if (s == null || !s.CompetitionActive) return;
            if (s.competitor == null) s.competitor = new CompetitorState();

            CompetitorState c = s.competitor;
            c.qualityBonus = 0f;
            c.attractionMul = 1f;
            s.playerAttractionMul = 1f;
            s.todayCompetitionEventName = null;
            s.todayStrategyChoice = PlayerCompetitionChoice.None.ToString();

            // 两天促销结束后回到促销前价格，避免每天在折后价上再打八折。
            if (c.promoDays <= 0 && c.promoBasePrice > 0f)
            {
                c.price = c.promoBasePrice;
                c.promoBasePrice = 0f;
            }

            if (!s.competitionIntroduced)
            {
                s.competitionIntroduced = true;
                c.strategy = CompetitorStrategy.Stable;
                c.price = 18f;
                c.quality = 72f;
                c.brand = 55f;
                c.marketing = 100f;
                c.plannedProduction = 130;
            }
            else if (s.progression != null && s.progression.currentChapter >= 8 &&
                !s.progression.rivalPriceCutTriggered)
            {
                // 用 >= 而非 ==：DifferentiationUsed 可在玩家多行为并行时提前入库，
                // Evaluate 一次连过 7、8 两章后 currentChapter 直接到 9——严格等值会让
                // 这个教学节点的对手降价永远不触发。rivalPriceCutTriggered 保证只演一次。
                s.progression.rivalPriceCutTriggered = true;
                c.strategy = CompetitorStrategy.LowPriceExpansion;
                c.price = Mathf.Clamp(Mathf.Min(c.price - 3f, s.price - 1f), 12f, 18f);
                c.marketing = 200f;
            }
            else if (ProgressionService.RequiresPriceWarChoice(s))
            {
                c.strategy = CompetitorStrategy.LowPriceExpansion;
                c.price = 14f;
                c.marketing = 300f;
                s.priceWarActive = true;
                s.priceWarRound = 1;
            }
            else if (s.priceWarActive)
            {
                DayRecord last = s.LastRecord;
                bool playerFollowed = last != null && last.competitionActive && last.price <= last.competitorPrice + 1f;
                if (playerFollowed && s.priceWarRound < 4)
                {
                    c.strategy = CompetitorStrategy.PriceFollower;
                    c.price = Mathf.Max(9f, last.price - 1f);
                    s.priceWarRound++;
                }
                else
                {
                    c.strategy = CompetitorStrategy.Differentiation;
                    c.price = Mathf.Min(22f, c.price + 1f);
                    c.marketing = 300f;
                    c.quality = Mathf.Min(90f, c.quality + 2f);
                    // 玩家不跟价时，对手转向差异化，不应把后续每天都记为价格战。
                    s.priceWarActive = false;
                }
            }
            else
            {
                ChooseFreeCompetitionStrategy(s, c);
            }

            if (c.promoDays > 0)
            {
                float basePrice = c.promoBasePrice > 0f ? c.promoBasePrice : c.price;
                c.price = Mathf.Max(9f, Mathf.Round(basePrice * 0.8f));
                c.promoDays--;
                s.todayCompetitionEventName = "对手促销";
            }
            else if (s.history.Count >= 8 && DaySimulator.NextRandom01(s) < CompetitionEventProbability(s))
            {
                RollCompetitionEvent(s, c);
            }

            LongTermExperienceService.Ensure(s);
            c.marketing = Mathf.Clamp(c.marketing * s.experience.settings.competitionIntensity, 0f, 1000f);
            c.unitCost = Mathf.Lerp(6f, 11f, Mathf.Clamp01((c.EffectiveQuality - 50f) / 50f));
            c.plannedProduction = c.lastSales > 0
                ? Mathf.Clamp(c.lastSales + 18, 90, c.capacity)
                : Mathf.Clamp(c.plannedProduction, 110, c.capacity);
        }

        static float CompetitionEventProbability(GameState state)
        {
            LongTermExperienceService.Ensure(state);
            float difficulty = state.experience.settings.difficulty == DifficultyMode.Teaching ? .55f :
                state.experience.settings.difficulty == DifficultyMode.Simulation ? 1.45f : 1f;
            return Mathf.Clamp01(.28f * difficulty * state.experience.settings.eventFrequency *
                state.experience.settings.competitionIntensity);
        }

        static void ChooseFreeCompetitionStrategy(GameState s, CompetitorState c)
        {
            if (c.marketShare < 0.38f)
            {
                c.strategy = CompetitorStrategy.LowPriceExpansion;
                c.price = Mathf.Max(10f, s.price - 2f);
                c.marketing = 300f;
            }
            else if (c.brand < s.playerBrand - 8f)
            {
                c.strategy = CompetitorStrategy.Differentiation;
                c.price = Mathf.Clamp(s.price + 2f, 14f, 24f);
                c.marketing = 500f;
                c.quality = Mathf.Min(92f, c.quality + 1f);
            }
            else if (s.LastRecord != null && s.price < s.LastRecord.price - 0.5f)
            {
                c.strategy = CompetitorStrategy.PriceFollower;
                c.price = Mathf.Max(10f, s.price + 1f);
                c.marketing = 100f;
            }
            else
            {
                c.strategy = CompetitorStrategy.Stable;
                c.price = Mathf.Clamp(c.price + (c.lastProfit < 0f ? 1f : 0f), 13f, 22f);
                c.marketing = 100f;
            }
        }

        static void RollCompetitionEvent(GameState s, CompetitorState c)
        {
            int roll = DaySimulator.NextRandom(s, 0, 5);
            if (roll == 0)
            {
                c.qualityBonus = 15f;
                s.todayCompetitionEventName = "对手新品发布";
            }
            else if (roll == 1)
            {
                c.promoBasePrice = c.price;
                c.promoDays = 1;
                c.price = Mathf.Max(9f, Mathf.Round(c.promoBasePrice * 0.8f));
                s.todayCompetitionEventName = "对手促销";
            }
            else if (roll == 2)
            {
                c.brand = Mathf.Min(100f, c.brand + 15f);
                s.todayCompetitionEventName = "网红推荐竞争店";
            }
            else if (roll == 3)
            {
                s.playerBrand = Mathf.Min(100f, s.playerBrand + 10f);
                s.todayCompetitionEventName = "玩家获得好评";
            }
            else
            {
                bool playerHit = DaySimulator.NextRandom01(s) < 0.5f;
                if (playerHit)
                {
                    s.playerBrand = Mathf.Max(0f, s.playerBrand - 30f);
                    s.playerAttractionMul = 0.75f;
                    s.todayCompetitionEventName = "你的店发生食品质量事故";
                }
                else
                {
                    c.brand = Mathf.Max(0f, c.brand - 30f);
                    c.attractionMul = 0.75f;
                    s.todayCompetitionEventName = "Sunny Tea 发生食品质量事故";
                }
            }
        }
    }
}
