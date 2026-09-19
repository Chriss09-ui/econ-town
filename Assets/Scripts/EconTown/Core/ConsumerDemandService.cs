using System;
using UnityEngine;

namespace EconTown
{
    // 拆分自 EconomyState.cs(2026-08-24 C1):存档结构文件不再混装服务,一类一文件。

    public struct ConsumerChoiceResult
    {
        public int player;
        public int competitor;
        public int outside;
        public DemandEffectBreakdown playerEffects;
    }

    /// <summary>外国(B国)市场需求评估结果:玩家产品获得的杯数与逐因子归因。</summary>
    public struct ForeignDemandResult
    {
        public int desiredUnits;
        public int outside;
        public DemandEffectBreakdown effects;
    }

    /// <summary>营业前后共用的消费者选择模型；价格敏感度只读取 ConsumerType.elasticity。</summary>
    public static class ConsumerDemandService
    {
        struct ChoiceInputs
        {
            public float segmentMultiplier;
            public float eventMultiplier;
            public float macroMultiplier;
            public float playerConsumerPrice;
            public float competitorConsumerPrice;
            public float playerQuality;
            public float competitorQuality;
            public float playerBrand;
            public float competitorBrand;
            public float playerMarketing;
            public float competitorMarketing;
            public float playerMarketingMomentum;
            public float playerPreference;
            public float competitorPreference;
            public float playerAttraction;
            public float competitorAttraction;
            public float populationJitter;
            public float playerJitter;
            public float competitorJitter;
        }

        struct ChoiceCore
        {
            public int player;
            public int competitor;
            public int outside;
            public float playerPriceEffect;
            public float playerShare;
            public float outsideShare;
        }

        public static CompetitionConsumerPrefs Preferences(ConsumerType type)
        {
            if (ReferenceEquals(type, ConsumerType.Student)) return CompetitionEco.Student;
            if (ReferenceEquals(type, ConsumerType.Tourist)) return CompetitionEco.Tourist;
            return CompetitionEco.WhiteCollar;
        }

        public static ConsumerChoiceResult Evaluate(GameState state, ConsumerType type,
            float playerListedPrice, float competitorListedPrice, bool competitorOpen,
            float populationJitter, float playerJitter, float competitorJitter, bool applyPolicy = true)
        {
            competitorOpen = state != null && state.CompetitionActive && competitorOpen;
            CompetitionConsumerPrefs prefs = Preferences(type);
            float typeMultiplier = ReferenceEquals(type, ConsumerType.Student) ? state.studentMul :
                ReferenceEquals(type, ConsumerType.Tourist) ? state.touristMul : 1f;
            float macro = MacroEconomySimulator.DemandFeedback(state);
            float playerWithoutPolicy = PriceService.Quote(state, playerListedPrice, false).consumerPrice;
            float competitorWithoutPolicy = competitorOpen
                ? PriceService.Quote(state, competitorListedPrice, false).consumerPrice : Eco.BasePrice;
            float playerWithPolicy = PriceService.Quote(state, playerListedPrice, applyPolicy).consumerPrice;
            float competitorWithPolicy = competitorOpen
                ? PriceService.Quote(state, competitorListedPrice, applyPolicy).consumerPrice : Eco.BasePrice;

            ChoiceInputs actual = new ChoiceInputs
            {
                segmentMultiplier = typeMultiplier,
                eventMultiplier = state.demandMul,
                macroMultiplier = macro,
                playerConsumerPrice = playerWithPolicy,
                competitorConsumerPrice = competitorWithPolicy,
                playerQuality = state.PlayerQuality,
                competitorQuality = state.competitor.EffectiveQuality,
                playerBrand = state.playerBrand,
                competitorBrand = state.competitor.brand,
                playerMarketing = state.marketingBudget,
                competitorMarketing = state.competitor.marketing,
                playerMarketingMomentum = state.playerMarketingMomentum,
                playerPreference = Mathf.Clamp(state.residentPlayerDemandMul, .55f, 1.5f),
                competitorPreference = Mathf.Clamp(state.residentCompetitorDemandMul, .55f, 1.5f),
                playerAttraction = state.playerAttractionMul,
                competitorAttraction = state.competitor.attractionMul,
                populationJitter = Mathf.Max(0f, populationJitter),
                playerJitter = Mathf.Max(0f, playerJitter),
                competitorJitter = Mathf.Max(0f, competitorJitter)
            };

            ChoiceInputs scenario = NeutralInputs();
            ChoiceCore previous = EvaluateCore(type, prefs, competitorOpen, scenario);
            DemandEffectBreakdown effects = new DemandEffectBreakdown { baseDemand = previous.player };

            scenario.segmentMultiplier = actual.segmentMultiplier;
            ChoiceCore next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.segmentEffect = next.player - previous.player; previous = next;

            scenario.eventMultiplier = actual.eventMultiplier;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.eventEffect = next.player - previous.player; previous = next;

            scenario.macroMultiplier = actual.macroMultiplier;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.macroEffect = next.player - previous.player; previous = next;

            scenario.playerConsumerPrice = playerWithoutPolicy;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.ownPriceEffect = next.player - previous.player; previous = next;

            scenario.competitorConsumerPrice = competitorWithoutPolicy;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.competitorPriceEffect = next.player - previous.player; previous = next;

            scenario.playerConsumerPrice = actual.playerConsumerPrice;
            scenario.competitorConsumerPrice = actual.competitorConsumerPrice;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.policyPriceEffect = next.player - previous.player; previous = next;

            scenario.playerQuality = actual.playerQuality;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.qualityEffect = next.player - previous.player; previous = next;

            scenario.playerBrand = actual.playerBrand;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.brandEffect = next.player - previous.player; previous = next;

            scenario.competitorQuality = actual.competitorQuality;
            scenario.competitorBrand = actual.competitorBrand;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.competitorAppealEffect = next.player - previous.player; previous = next;

            scenario.playerMarketing = actual.playerMarketing;
            scenario.competitorMarketing = actual.competitorMarketing;
            scenario.playerMarketingMomentum = actual.playerMarketingMomentum;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.marketingEffect = next.player - previous.player; previous = next;

            scenario.playerPreference = actual.playerPreference;
            scenario.competitorPreference = actual.competitorPreference;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.residentPreferenceEffect = next.player - previous.player; previous = next;

            scenario.playerAttraction = actual.playerAttraction;
            scenario.competitorAttraction = actual.competitorAttraction;
            next = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.competitionEventEffect = next.player - previous.player; previous = next;

            scenario.populationJitter = actual.populationJitter;
            scenario.playerJitter = actual.playerJitter;
            scenario.competitorJitter = actual.competitorJitter;
            ChoiceCore final = EvaluateCore(type, prefs, competitorOpen, scenario);
            effects.randomEffect = final.player - previous.player;
            effects.consumerTypeMultiplier = actual.segmentMultiplier;
            effects.eventMultiplier = actual.eventMultiplier;
            effects.macroMultiplier = actual.macroMultiplier;
            effects.residentPreferenceMultiplier = actual.playerPreference;
            effects.priceEffectMultiplier = final.playerPriceEffect;
            effects.competitionShare = final.playerShare;
            effects.noPurchaseShare = final.outsideShare;
            effects.desiredDemand = final.player;
            return new ConsumerChoiceResult
            {
                player = final.player,
                competitor = final.competitor,
                outside = final.outside,
                playerEffects = effects
            };
        }

        public static DemandEffectBreakdown Combine(GameState state, params DemandEffectBreakdown[] parts)
        {
            DemandEffectBreakdown result = new DemandEffectBreakdown();
            float weight = 0f;
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                DemandEffectBreakdown part = parts[i];
                if (part == null) continue;
                float w = Mathf.Max(1f, part.desiredDemand);
                result.baseDemand += part.baseDemand;
                result.segmentEffect += part.segmentEffect;
                result.eventEffect += part.eventEffect;
                result.macroEffect += part.macroEffect;
                result.ownPriceEffect += part.ownPriceEffect;
                result.competitorPriceEffect += part.competitorPriceEffect;
                result.policyPriceEffect += part.policyPriceEffect;
                result.qualityEffect += part.qualityEffect;
                result.brandEffect += part.brandEffect;
                result.competitorAppealEffect += part.competitorAppealEffect;
                result.marketingEffect += part.marketingEffect;
                result.residentPreferenceEffect += part.residentPreferenceEffect;
                result.competitionEventEffect += part.competitionEventEffect;
                result.randomEffect += part.randomEffect;
                result.stockoutSwitchEffect += part.stockoutSwitchEffect;
                result.desiredDemand += part.desiredDemand;
                result.consumerTypeMultiplier += (part.consumerTypeMultiplier - 1f) * w;
                result.priceEffectMultiplier += (part.priceEffectMultiplier - 1f) * w;
                result.competitionShare += (part.competitionShare - 1f) * w;
                result.noPurchaseShare += part.noPurchaseShare * w;
                weight += w;
            }
            if (weight > 0f)
            {
                result.consumerTypeMultiplier = 1f + (result.consumerTypeMultiplier - 1f) / weight;
                result.priceEffectMultiplier = 1f + (result.priceEffectMultiplier - 1f) / weight;
                result.competitionShare = 1f + (result.competitionShare - 1f) / weight;
                result.noPurchaseShare /= weight;
            }
            result.eventMultiplier = state == null ? 1f : state.demandMul;
            result.macroMultiplier = state == null ? 1f : MacroEconomySimulator.DemandFeedback(state);
            result.residentPreferenceMultiplier = state == null ? 1f : state.residentPlayerDemandMul;
            return result;
        }

        public static int ExpectedPlayerDemand(GameState state, ConsumerType type)
        {
            return ExpectedPlayerDemand(state, type, true);
        }

        public static int ExpectedPlayerDemand(GameState state, ConsumerType type, bool applyPolicy)
        {
            bool competitorOpen = state != null && state.CompetitionActive;
            float competitorPrice = competitorOpen ? state.competitor.price : Eco.BasePrice;
            return Evaluate(state, type, PriceService.PlayerListedPrice(state), competitorPrice,
                competitorOpen, 1f, 1f, 1f, applyPolicy).player;
        }

        /// <summary>
        /// V0.7 出口需求:B 国消费者参数重入同一核心模型(PRD §12「沿用统一消费者选择模型」)。
        /// B 国没有第二家完整竞品状态机——本地替代用抬高 outsideScore 表达;B 国不吃 A 国的
        /// 饮料税/限价(competitorOpen=false 路径,无政策楔子);jitter 由 economy.tradeRandom 提供。
        /// </summary>
        public static ForeignDemandResult EvaluateForeign(GameState state, CountryProfileState b,
            ConsumerType type, float facePriceAcurrency, float jitter)
        {
            CompetitionConsumerPrefs prefs = new CompetitionConsumerPrefs
            {
                qualityWeight = Mathf.Clamp(Mathf.Max(0f, b.qualityWeight), 0f, 1f),
                brandWeight = Mathf.Clamp(Mathf.Max(0f, b.brandWeight), 0f, 1f),
                outsideWeight = Mathf.Clamp(b.outsideWeight, .05f, .9f),
                playerDistanceBonus = .02f,       // 港口渠道轻微便利
                competitorDistanceBonus = 0f
            };
            float incomeScale = Mathf.Clamp(Mathf.Max(1f, b.incomeLevel) / 100f, .5f, 2f);
            ChoiceInputs actual = new ChoiceInputs
            {
                segmentMultiplier = incomeScale,
                eventMultiplier = 1f,
                macroMultiplier = b.recessionActive ? .82f : 1f,
                playerConsumerPrice = Mathf.Max(1f, facePriceAcurrency),
                competitorConsumerPrice = Eco.BasePrice,   // 无竞品路径不消费该值
                playerQuality = state.PlayerQuality,
                competitorQuality = 50f,
                playerBrand = state.playerBrand,
                competitorBrand = 50f,
                playerMarketing = state.marketingBudget,
                competitorMarketing = 0f,
                playerMarketingMomentum = state.playerMarketingMomentum,
                // 文化距离:对 B 国本国品牌的偏好折损进口吸引力(PRD §19 比较优势之外的偏好摩擦)。
                playerPreference = Mathf.Clamp(1f - b.preferenceDomestic * .55f, .3f, 1f),
                competitorPreference = 1f,
                playerAttraction = 1f,
                competitorAttraction = 1f,
                populationJitter = Mathf.Max(0f, jitter) * incomeScale,
                playerJitter = 1f,
                competitorJitter = 1f
            };

            ChoiceInputs scenario = NeutralInputs();
            ChoiceCore previous = EvaluateCore(type, prefs, false, scenario);
            DemandEffectBreakdown effects = new DemandEffectBreakdown { baseDemand = previous.player };

            scenario.macroMultiplier = actual.macroMultiplier;
            ChoiceCore next = EvaluateCore(type, prefs, false, scenario);
            effects.macroEffect = next.player - previous.player; previous = next;

            scenario.playerConsumerPrice = actual.playerConsumerPrice;
            next = EvaluateCore(type, prefs, false, scenario);
            effects.ownPriceEffect = next.player - previous.player; previous = next;

            scenario.playerQuality = actual.playerQuality;
            next = EvaluateCore(type, prefs, false, scenario);
            effects.qualityEffect = next.player - previous.player; previous = next;

            scenario.playerBrand = actual.playerBrand;
            next = EvaluateCore(type, prefs, false, scenario);
            effects.brandEffect = next.player - previous.player; previous = next;

            scenario.playerMarketing = actual.playerMarketing;
            scenario.playerMarketingMomentum = actual.playerMarketingMomentum;
            next = EvaluateCore(type, prefs, false, scenario);
            effects.marketingEffect = next.player - previous.player; previous = next;

            scenario.playerPreference = actual.playerPreference;
            next = EvaluateCore(type, prefs, false, scenario);
            effects.residentPreferenceEffect = next.player - previous.player; previous = next;

            scenario.populationJitter = actual.populationJitter;
            ChoiceCore final = EvaluateCore(type, prefs, false, scenario);
            effects.randomEffect = final.player - previous.player;
            effects.desiredDemand = final.player;
            effects.competitionShare = final.playerShare;
            effects.noPurchaseShare = final.outsideShare;
            return new ForeignDemandResult
            {
                desiredUnits = final.player,
                outside = final.outside,
                effects = effects
            };
        }

        static ChoiceInputs NeutralInputs()
        {
            return new ChoiceInputs
            {
                segmentMultiplier = 1f,
                eventMultiplier = 1f,
                macroMultiplier = 1f,
                playerConsumerPrice = Eco.BasePrice,
                competitorConsumerPrice = Eco.BasePrice,
                playerQuality = 50f,
                competitorQuality = 50f,
                playerBrand = 50f,
                competitorBrand = 50f,
                playerMarketing = 0f,
                competitorMarketing = 0f,
                playerMarketingMomentum = 0f,
                playerPreference = 1f,
                competitorPreference = 1f,
                playerAttraction = 1f,
                competitorAttraction = 1f,
                populationJitter = 1f,
                playerJitter = 1f,
                competitorJitter = 1f
            };
        }

        static ChoiceCore EvaluateCore(ConsumerType type, CompetitionConsumerPrefs prefs,
            bool competitorOpen, ChoiceInputs inputs)
        {
            float marketScale = competitorOpen ? CompetitionEco.BaseMarketDemand / (float)Eco.BaseDemand : 1f;
            float baseDemand = Eco.BaseDemand * marketScale * type.share;
            float playerPriceEffect = Mathf.Pow(Eco.BasePrice /
                Mathf.Max(1f, inputs.playerConsumerPrice), type.elasticity);
            float competitorPriceEffect = competitorOpen
                ? Mathf.Pow(Eco.BasePrice / Mathf.Max(1f, inputs.competitorConsumerPrice), type.elasticity)
                : 0f;
            float categoryPriceEffect = competitorOpen
                ? Mathf.Lerp(1f, Mathf.Max(playerPriceEffect, competitorPriceEffect), .35f)
                : playerPriceEffect;
            float population = baseDemand * inputs.segmentMultiplier * inputs.eventMultiplier *
                inputs.macroMultiplier * Mathf.Clamp(categoryPriceEffect, .05f, 2.5f) *
                Mathf.Max(0f, inputs.populationJitter);
            int total = Mathf.Max(0, Mathf.RoundToInt(population));
            float playerNonPrice = NonPriceScore(inputs.playerQuality, inputs.playerBrand,
                prefs.qualityWeight, prefs.brandWeight, prefs.playerDistanceBonus);
            float competitorNonPrice = competitorOpen
                ? NonPriceScore(inputs.competitorQuality, inputs.competitorBrand,
                    prefs.qualityWeight, prefs.brandWeight, prefs.competitorDistanceBonus)
                : 0f;
            float playerAwareness = 1f + Mathf.Sqrt(Mathf.Max(0f, inputs.playerMarketing) / 100f) * .06f +
                Mathf.Clamp(inputs.playerMarketingMomentum, 0f, 4f) * .025f;
            float competitorAwareness = competitorOpen
                ? 1f + Mathf.Sqrt(Mathf.Max(0f, inputs.competitorMarketing) / 100f) * .06f
                : 0f;
            float playerScore = playerPriceEffect * playerNonPrice * playerAwareness *
                inputs.playerAttraction * inputs.playerPreference * Mathf.Max(0f, inputs.playerJitter);
            float competitorScore = competitorPriceEffect * competitorNonPrice * competitorAwareness *
                inputs.competitorAttraction * inputs.competitorPreference * Mathf.Max(0f, inputs.competitorJitter);
            float outsideScore = competitorOpen ? Mathf.Max(.08f, prefs.outsideWeight) : .025f;
            float sum = Mathf.Max(.0001f, playerScore + competitorScore + outsideScore);
            int player = Mathf.Clamp(Mathf.RoundToInt(total * playerScore / sum), 0, total);
            int competitor = Mathf.Clamp(Mathf.RoundToInt(total * competitorScore / sum), 0, total - player);
            int outside = Mathf.Max(0, total - player - competitor);
            return new ChoiceCore
            {
                player = player,
                competitor = competitor,
                outside = outside,
                playerPriceEffect = playerPriceEffect,
                playerShare = playerScore / sum,
                outsideShare = outsideScore / sum
            };
        }

        static float NonPriceScore(float quality, float brand, float qualityWeight, float brandWeight, float distanceBonus)
        {
            return .55f + Mathf.Clamp01(quality / 100f) * qualityWeight +
                Mathf.Clamp01(brand / 100f) * brandWeight + distanceBonus;
        }
    }

}
