using UnityEngine;

namespace EconTown
{
    /// <summary>V0.2 双企业市场：消费者选择、份额、竞争结算与反事实估算。</summary>
    public static class CompetitionSimulator
    {
        /// <summary>单个人群的一次随机扰动(各分量 1 表示无扰动)。</summary>
        public struct GroupJitter
        {
            public float population;
            public float player;
            public float competitor;
        }

        static GroupJitter RollJitter(GameState state)
        {
            return new GroupJitter
            {
                population = 0.94f + DaySimulator.NextRandom01(state) * 0.12f,
                player = 0.97f + DaySimulator.NextRandom01(state) * 0.06f,
                competitor = 0.97f + DaySimulator.NextRandom01(state) * 0.06f
            };
        }

        public struct MarketRoll
        {
            public DaySimulator.DemandRoll player;
            public DaySimulator.DemandRoll competitor;
            public int outside;
            public GroupJitter[] jitters;
            public DemandEffectBreakdown playerEffects;
            public int Total { get { return player.Total + competitor.Total + outside; } }
        }

        public struct ProfitEstimate
        {
            public int playerSold;
            public int competitorSold;
            public float playerProfit;
            public float competitorProfit;
        }

        public static MarketRoll RollMarket(GameState s)
        {
            MarketRoll roll = new MarketRoll();
            roll.jitters = new GroupJitter[3];
            roll.jitters[0] = RollJitter(s);
            roll.jitters[1] = RollJitter(s);
            roll.jitters[2] = RollJitter(s);
            AllocateGroup(s, ConsumerType.Student.share, s.studentMul, CompetitionEco.Student, -1f, -1f, roll.jitters[0],
                out roll.player.students, out roll.competitor.students, out int outsideStudent, out DemandEffectBreakdown studentEffects);
            AllocateGroup(s, ConsumerType.WhiteCollar.share, 1f, CompetitionEco.WhiteCollar, -1f, -1f, roll.jitters[1],
                out roll.player.whitecollars, out roll.competitor.whitecollars, out int outsideWhite, out DemandEffectBreakdown whiteEffects);
            AllocateGroup(s, ConsumerType.Tourist.share, s.touristMul, CompetitionEco.Tourist, -1f, -1f, roll.jitters[2],
                out roll.player.tourists, out roll.competitor.tourists, out int outsideTourist, out DemandEffectBreakdown touristEffects);
            roll.outside = outsideStudent + outsideWhite + outsideTourist;
            roll.playerEffects = ConsumerDemandService.Combine(s, studentEffects, whiteEffects, touristEffects);
            roll.player.effects = roll.playerEffects;
            return roll;
        }

        public static DayRecord Settle(GameState s, MarketRoll market, int produced)
        {
            CompetitorState c = s.competitor;
            float effectivePlayerPrice = PriceService.PlayerListedPrice(s);
            int playerAvailable = produced + s.inventory;
            int competitorPlanned = GovernmentSimulator.ApplySupplyResponse(s, c.plannedProduction, c.price);
            int competitorProduced = Mathf.Min(competitorPlanned, c.capacity);
            int competitorAvailable = competitorProduced + c.inventory;

            int playerDirectSold = Mathf.Min(market.player.Total, playerAvailable);
            int competitorDirectSold = Mathf.Min(market.competitor.Total, competitorAvailable);
            int playerSold = playerDirectSold;
            int competitorSold = competitorDirectSold;

            // 缺货顾客有一部分会临时转向另一家店，体现替代关系。
            int playerStockout = Mathf.Max(0, market.player.Total - playerAvailable);
            int competitorSpare = Mathf.Max(0, competitorAvailable - competitorSold);
            int switchToCompetitor = Mathf.Min(competitorSpare, Mathf.RoundToInt(playerStockout * 0.35f));
            competitorSold += switchToCompetitor;

            int competitorStockout = Mathf.Max(0, market.competitor.Total - competitorAvailable);
            int playerSpare = Mathf.Max(0, playerAvailable - playerSold);
            int switchToPlayer = Mathf.Min(playerSpare, Mathf.RoundToInt(competitorStockout * 0.35f));
            playerSold += switchToPlayer;

            DaySimulator.AllocateSoldByType(market.player, playerDirectSold, out int sSold, out int wSold, out int tSold);
            int switchedStudents = 0, switchedWhitecollars = 0, switchedTourists = 0;
            if (switchToPlayer > 0)
            {
                DaySimulator.AllocateSoldByType(market.competitor, switchToPlayer,
                    out switchedStudents, out switchedWhitecollars, out switchedTourists);
                sSold += switchedStudents; wSold += switchedWhitecollars; tSold += switchedTourists;
            }
            int playerDesiredDemand = market.player.Total + switchToPlayer;

            CostBreakdown costs = CostService.Calculate(s, produced, true);
            float matUnit = costs.effectiveInputUnitCost;
            float materialCost = costs.materialCost;
            float wageCost = costs.wageCost;
            float rentCost = costs.fixedCost;
            float marketingCost = costs.marketingCost;
            float totalCost = costs.totalCost;
            float revenue = playerSold * GovernmentSimulator.DrinkProducerPrice(s, effectivePlayerPrice);
            float profit = revenue - totalCost;

            float competitorMaterialCost = competitorProduced * c.unitCost * MacroEconomySimulator.SupplyCostFeedback(s);
            float competitorRevenue = competitorSold * GovernmentSimulator.DrinkProducerPrice(s, c.price);
            float competitorCost = competitorMaterialCost + c.fixedCost + c.marketing;
            float competitorProfit = competitorRevenue - competitorCost;

            // 反事实必须使用同一个营业前库存与产能，否则结算后的残量会低估“如果保持原价”。
            DayRecord previous = s.LastRecord;
            float previousPrice = previous != null && previous.competitionActive ? previous.price : s.price;
            ProfitEstimate counterfactual = Estimate(s, previousPrice, c.price, market.jitters);
            float crossElasticity = CrossPriceElasticity(previous, effectivePlayerPrice, c.price,
                competitorSold, LongTermExperienceService.CombinedEventTitle(s), s.todayCompetitionEventName);

            s.materials -= produced;
            // 原料与品质升级已在采购/升级时支付，结算只发生营业现金流。
            s.money += revenue - costs.cashCostAfterPrepaidInputs;
            s.inventory = playerAvailable - playerSold;
            c.inventory = competitorAvailable - competitorSold;

            float marketSales = Mathf.Max(1, playerSold + competitorSold);
            float playerShare = playerSold / marketSales;
            float competitorShare = competitorSold / marketSales;
            float playerSatisfaction = SatisfactionService.Calculate(
                GovernmentSimulator.DrinkConsumerPrice(s, effectivePlayerPrice),
                playerDesiredDemand, playerSold, s.PlayerQuality, s.playerBrand);
            float competitorConsumerPrice = GovernmentSimulator.DrinkConsumerPrice(s, c.price);
            int competitorDesiredDemand = market.competitor.Total + switchToCompetitor;
            float competitorSatisfaction = SatisfactionService.Calculate(
                competitorConsumerPrice, competitorDesiredDemand, competitorSold,
                c.EffectiveQuality, c.brand);

            DayRecord r = new DayRecord
            {
                day = s.day,
                shopOpen = true,
                listedPrice = effectivePlayerPrice,
                price = effectivePlayerPrice,
                planned = s.planProduction,
                produced = produced,
                desiredDemand = playerDesiredDemand,
                availableSupply = playerAvailable,
                actualSales = playerSold,
                operationalStockout = Mathf.Max(0, playerDesiredDemand - playerAvailable),
                operationalSurplus = Mathf.Max(0, playerAvailable - playerDesiredDemand),
                demandTotal = playerDesiredDemand,
                demandStudents = market.player.students + switchedStudents,
                demandWhitecollars = market.player.whitecollars + switchedWhitecollars,
                demandTourists = market.player.tourists + switchedTourists,
                soldStudents = sSold,
                soldWhitecollars = wSold,
                soldTourists = tSold,
                sold = playerSold,
                lostCustomers = Mathf.Max(0, playerDesiredDemand - playerSold),
                stockoutLost = playerStockout,
                revenue = revenue,
                materialCost = materialCost,
                wageCost = wageCost,
                rentCost = rentCost,
                totalCost = totalCost,
                profit = profit,
                inventoryEnd = s.inventory,
                materialsEnd = s.materials,
                spoiled = s.economy.spoilageAtDayStart,
                satisfaction = playerSatisfaction,
                eventName = LongTermExperienceService.CombinedEventTitle(s),
                moneyEnd = s.money,
                marginalCost = matUnit,
                competitionActive = true,
                playerQuality = s.PlayerQuality,
                playerBrand = s.playerBrand,
                playerMarketing = s.marketingBudget,
                competitorPrice = c.price,
                competitorQuality = c.EffectiveQuality,
                competitorBrand = c.brand,
                competitorMarketing = c.marketing,
                competitorDemand = market.competitor.Total,
                competitorSold = competitorSold,
                competitorRevenue = competitorRevenue,
                competitorProfit = competitorProfit,
                competitorStatus = StatusForProfit(competitorProfit),
                totalMarketDemand = market.Total,
                outsideDemand = market.outside + Mathf.Max(0, playerStockout - switchToCompetitor) + Mathf.Max(0, competitorStockout - switchToPlayer),
                playerMarketShare = playerShare,
                competitorMarketShare = competitorShare,
                switchedToCompetitor = EstimateSwitchers(previous, effectivePlayerPrice, c.price, competitorSold),
                competitorStrategy = CompetitionEco.StrategyName(c.strategy),
                playerStrategyChoice = CompetitionEco.ChoiceName(s.todayStrategyChoice),
                priceWar = s.priceWarActive,
                priceWarRound = s.priceWarRound,
                competitionEventName = s.todayCompetitionEventName,
                crossPriceElasticity = float.IsNaN(crossElasticity) ? 0f : crossElasticity,
                hasCrossPriceElasticity = !float.IsNaN(crossElasticity),
                strategicSpend = s.todayStrategicSpend,
                counterfactualPrice = previousPrice,
                counterfactualSold = counterfactual.playerSold,
                counterfactualProfit = counterfactual.playerProfit
            };
            DemandEffectBreakdown settledEffects = market.playerEffects != null
                ? market.playerEffects.Copy() : DaySimulator.AggregateEffects(s);
            settledEffects.stockoutSwitchEffect += switchToPlayer;
            settledEffects.desiredDemand = playerDesiredDemand;
            r.demandEffects = settledEffects;
            r.capitalInvestment = Mathf.Max(0f, s.todayStrategicSpend) + Mathf.Max(0f, s.pendingFinancedInvestment);
            s.pendingFinancedInvestment = 0f;
            // V0.7:r.exports/r.imports 改由 InternationalTradeService.SettleDayTrade 以真实贸易流回填
            // (对手产量的进出口口径并入镇集体商行基线,不再用 .2f/.35f 系数)。

            float policyPrice = GovernmentSimulator.DrinkConsumerPrice(s, effectivePlayerPrice);
            int policyBaseline = Mathf.Max(0, Mathf.RoundToInt(market.player.Total *
                Mathf.Pow(policyPrice / Mathf.Max(1f, effectivePlayerPrice), 1.1f)));
            GovernmentSimulator.RecordDrinkSettlement(s, r, playerSold, policyBaseline);
            GovernmentSimulator.RecordAdditionalDrinkSales(s, competitorSold);
            // 兜底:同一天绝不写入第二条企业记录(防异常路径双结算污染环比与曲线)。
            if (s.history.Count > 0 && s.history[s.history.Count - 1].day == r.day)
                s.history.RemoveAt(s.history.Count - 1);
            s.history.Add(r);
            FinancialHealthService.RecordSettledDay(s);
            s.playerBrand = UpdateBrand(s.playerBrand, s.marketingBudget, playerSatisfaction, s.PlayerQuality);
            s.playerMarketingMomentum = Mathf.Clamp(
                s.playerMarketingMomentum + Mathf.Sqrt(Mathf.Max(0f, s.marketingBudget) / 100f) * 0.8f,
                0f, 4f);
            c.brand = UpdateBrand(c.brand, c.marketing, competitorSatisfaction, c.EffectiveQuality);
            c.satisfaction = competitorSatisfaction;
            c.lastSales = competitorSold;
            c.lastRevenue = competitorRevenue;
            c.lastProfit = competitorProfit;
            c.marketShare = competitorShare;
            s.todayStrategicSpend = 0f;
            s.economy.spoilageAtDayStart = 0;
            s.economy.competitorSpoilageAtDayStart = 0;
            return r;
        }

        /// <summary>
        /// 竞争期歇业日结算:玩家关门(只付工资租金),对手照常营业。
        /// 份额、税基与对手次日产量计划都依赖这条记录,缺了它市场历史会"消失一天"。
        /// </summary>
        public static DayRecord SettleClosedDay(GameState s)
        {
            CompetitorState c = s.competitor;
            MarketRoll market = RollMarket(s);
            float effectivePlayerPrice = PriceService.PlayerListedPrice(s);

            int competitorPlanned = GovernmentSimulator.ApplySupplyResponse(s, c.plannedProduction, c.price);
            int competitorProduced = Mathf.Min(competitorPlanned, c.capacity);
            int competitorAvailable = competitorProduced + c.inventory;

            // 玩家关门等价于全额缺货,老主顾按结算规则的一部分(35%)转向对手。
            int playerStockout = market.player.Total;
            int competitorDirect = Mathf.Min(market.competitor.Total, competitorAvailable);
            int competitorSpare = Mathf.Max(0, competitorAvailable - competitorDirect);
            int switchToCompetitor = Mathf.Min(competitorSpare, Mathf.RoundToInt(playerStockout * 0.35f));
            int competitorSold = competitorDirect + switchToCompetitor;
            int competitorStockout = Mathf.Max(0, market.competitor.Total - competitorAvailable);

            CostBreakdown costs = CostService.Calculate(s, 0, false);
            s.money -= costs.cashCostAfterPrepaidInputs;

            float competitorMaterialCost = competitorProduced * c.unitCost * MacroEconomySimulator.SupplyCostFeedback(s);
            float competitorRevenue = competitorSold * GovernmentSimulator.DrinkProducerPrice(s, c.price);
            float competitorCost = competitorMaterialCost + c.fixedCost + c.marketing;
            float competitorProfit = competitorRevenue - competitorCost;
            c.inventory = competitorAvailable - competitorSold;

            float marketSales = Mathf.Max(1, competitorSold);
            float competitorConsumerPrice = GovernmentSimulator.DrinkConsumerPrice(s, c.price);
            int competitorDesiredDemand = market.competitor.Total + switchToCompetitor;
            float competitorSatisfaction = SatisfactionService.Calculate(
                competitorConsumerPrice, competitorDesiredDemand, competitorSold,
                c.EffectiveQuality, c.brand);

            DayRecord r = new DayRecord
            {
                day = s.day,
                shopOpen = false,
                listedPrice = effectivePlayerPrice,
                price = effectivePlayerPrice,
                planned = s.planProduction,
                produced = 0,
                desiredDemand = market.player.Total,
                availableSupply = s.inventory,
                actualSales = 0,
                sold = 0,
                demandTotal = market.player.Total,
                demandStudents = market.player.students,
                demandWhitecollars = market.player.whitecollars,
                demandTourists = market.player.tourists,
                lostCustomers = market.player.Total,
                stockoutLost = 0,
                operationalStockout = 0,
                operationalSurplus = s.inventory,
                wageCost = costs.wageCost,
                rentCost = costs.fixedCost,
                totalCost = costs.totalCost,
                profit = -costs.totalCost,
                inventoryEnd = s.inventory,
                materialsEnd = s.materials,
                spoiled = s.economy.spoilageAtDayStart,
                satisfaction = 40f,
                eventName = LongTermExperienceService.CombinedEventTitle(s),
                moneyEnd = s.money,
                marginalCost = costs.effectiveInputUnitCost,
                competitionActive = true,
                playerQuality = s.PlayerQuality,
                playerBrand = s.playerBrand,
                playerMarketing = s.marketingBudget,
                competitorPrice = c.price,
                competitorQuality = c.EffectiveQuality,
                competitorBrand = c.brand,
                competitorMarketing = c.marketing,
                competitorDemand = market.competitor.Total,
                competitorSold = competitorSold,
                competitorRevenue = competitorRevenue,
                competitorProfit = competitorProfit,
                competitorStatus = StatusForProfit(competitorProfit),
                totalMarketDemand = market.Total,
                outsideDemand = market.outside + Mathf.Max(0, playerStockout - switchToCompetitor) + competitorStockout,
                playerMarketShare = 0f,
                competitorMarketShare = competitorSold / marketSales,
                competitorStrategy = CompetitionEco.StrategyName(c.strategy),
                playerStrategyChoice = CompetitionEco.ChoiceName(s.todayStrategyChoice),
                priceWar = s.priceWarActive,
                priceWarRound = s.priceWarRound,
                competitionEventName = s.todayCompetitionEventName,
                strategicSpend = 0f
            };
            DemandEffectBreakdown closedEffects = market.playerEffects != null
                ? market.playerEffects.Copy() : DaySimulator.AggregateEffects(s);
            closedEffects.desiredDemand = market.player.Total;
            r.demandEffects = closedEffects;
            r.capitalInvestment = Mathf.Max(0f, s.pendingFinancedInvestment);
            s.pendingFinancedInvestment = 0f;
            // V0.7:歇业竞争日同样由 InternationalTradeService 回填真实贸易流。

            float policyPrice = GovernmentSimulator.DrinkConsumerPrice(s, effectivePlayerPrice);
            int policyBaseline = Mathf.Max(0, Mathf.RoundToInt(market.player.Total *
                Mathf.Pow(policyPrice / Mathf.Max(1f, effectivePlayerPrice), 1.1f)));
            GovernmentSimulator.RecordDrinkSettlement(s, r, 0, policyBaseline);
            GovernmentSimulator.RecordAdditionalDrinkSales(s, competitorSold);
            // 兜底:同一天绝不写入第二条企业记录。
            if (s.history.Count > 0 && s.history[s.history.Count - 1].day == r.day)
                s.history.RemoveAt(s.history.Count - 1);
            s.history.Add(r);
            FinancialHealthService.RecordSettledDay(s);
            c.brand = UpdateBrand(c.brand, c.marketing, competitorSatisfaction, c.EffectiveQuality);
            c.satisfaction = competitorSatisfaction;
            c.lastSales = competitorSold;
            c.lastRevenue = competitorRevenue;
            c.lastProfit = competitorProfit;
            c.marketShare = competitorSold / marketSales;
            // 策略性支出已在升级时支付,歇业日不计成本,也不允许漏到下一个营业日。
            s.todayStrategicSpend = 0f;
            s.economy.spoilageAtDayStart = 0;
            s.economy.competitorSpoilageAtDayStart = 0;
            return r;
        }

        public static ProfitEstimate Estimate(GameState s, float playerPrice, float competitorPrice)
        {
            return Estimate(s, playerPrice, competitorPrice, null);
        }

        /// <summary>带当日随机因子的估算:jitters 传结算 RollMarket 生成的同一组因子,反事实与实际结果才只差价格、不差噪声。</summary>
        public static ProfitEstimate Estimate(GameState s, float playerPrice, float competitorPrice, GroupJitter[] jitters)
        {
            float effectivePlayerPrice = PriceService.PlayerListedPrice(s, playerPrice);
            GroupJitter none = new GroupJitter { population = 1f, player = 1f, competitor = 1f };
            GroupJitter j0 = jitters != null && jitters.Length > 0 ? jitters[0] : none;
            GroupJitter j1 = jitters != null && jitters.Length > 1 ? jitters[1] : none;
            GroupJitter j2 = jitters != null && jitters.Length > 2 ? jitters[2] : none;
            int pDemand = 0, cDemand = 0;
            EstimateGroup(s, ConsumerType.Student.share, s.studentMul, CompetitionEco.Student, effectivePlayerPrice, competitorPrice, j0, ref pDemand, ref cDemand);
            EstimateGroup(s, ConsumerType.WhiteCollar.share, 1f, CompetitionEco.WhiteCollar, effectivePlayerPrice, competitorPrice, j1, ref pDemand, ref cDemand);
            EstimateGroup(s, ConsumerType.Tourist.share, s.touristMul, CompetitionEco.Tourist, effectivePlayerPrice, competitorPrice, j2, ref pDemand, ref cDemand);

            int playerCap = DaySimulator.Production(s) + s.inventory;
            int competitorCap = s.competitor.plannedProduction + s.competitor.inventory;
            int pSold = Mathf.Min(pDemand, playerCap);
            int cSold = Mathf.Min(cDemand, competitorCap);
            float playerCost = DaySimulator.Production(s) * s.PlayerUnitCost +
                s.employees * GovernmentSimulator.EffectiveDailyWage(s) +
                Eco.DailyRent + s.marketingBudget + s.todayStrategicSpend;
            float competitorCost = s.competitor.plannedProduction * s.competitor.unitCost + s.competitor.fixedCost + s.competitor.marketing;
            return new ProfitEstimate
            {
                playerSold = pSold,
                competitorSold = cSold,
                playerProfit = pSold * GovernmentSimulator.DrinkProducerPrice(s, effectivePlayerPrice) - playerCost,
                competitorProfit = cSold * GovernmentSimulator.DrinkProducerPrice(s, competitorPrice) - competitorCost
            };
        }

        public static void EstimatePayoffMatrix(GameState s, out ProfitEstimate bothHold, out ProfitEstimate playerCuts,
            out ProfitEstimate competitorCuts, out ProfitEstimate bothCut)
        {
            float p = s.price, c = s.competitor.price;
            float pCut = Mathf.Max(CompetitionEco.MinPrice, p - 3f);
            float cCut = Mathf.Max(CompetitionEco.MinPrice, c - 3f);
            bothHold = Estimate(s, p, c);
            playerCuts = Estimate(s, pCut, c);
            competitorCuts = Estimate(s, p, cCut);
            bothCut = Estimate(s, pCut, cCut);
        }

        static void EstimateGroup(GameState s, float share, float typeMul, CompetitionConsumerPrefs prefs,
            float playerPrice, float competitorPrice, GroupJitter jitter, ref int pDemand, ref int cDemand)
        {
            AllocateGroup(s, share, typeMul, prefs, playerPrice, competitorPrice, jitter,
                out int p, out int c, out int _, out DemandEffectBreakdown _);
            pDemand += p; cDemand += c;
        }

        static void AllocateGroup(GameState s, float share, float typeMul, CompetitionConsumerPrefs prefs,
            float playerPriceOverride, float competitorPriceOverride, GroupJitter jitter,
            out int player, out int competitor, out int outside, out DemandEffectBreakdown effects)
        {
            float playerShelfPrice = playerPriceOverride > 0f ? playerPriceOverride : PriceService.PlayerListedPrice(s);
            float competitorShelfPrice = competitorPriceOverride > 0f ? competitorPriceOverride : s.competitor.price;
            ConsumerType type = ReferenceEquals(prefs, CompetitionEco.Student) ? ConsumerType.Student :
                ReferenceEquals(prefs, CompetitionEco.Tourist) ? ConsumerType.Tourist : ConsumerType.WhiteCollar;
            ConsumerChoiceResult result = ConsumerDemandService.Evaluate(s, type, playerShelfPrice,
                competitorShelfPrice, true, jitter.population, jitter.player, jitter.competitor);
            player = result.player;
            competitor = result.competitor;
            outside = result.outside;
            effects = result.playerEffects;
        }

        static float UpdateBrand(float brand, float marketing, float satisfaction, float quality)
        {
            float ad = marketing <= 0f ? -0.5f : Mathf.Sqrt(marketing / 100f) * 1.8f;
            float wordOfMouth = (satisfaction - 70f) * 0.035f;
            float qualitySignal = (quality - 50f) * 0.012f;
            return Mathf.Clamp(brand + ad + wordOfMouth + qualitySignal, 0f, 100f);
        }

        static int EstimateSwitchers(DayRecord previous, float currentPlayerPrice, float currentCompetitorPrice, int currentCompetitorSold)
        {
            if (previous == null || !previous.competitionActive) return 0;
            // 对手自身调价带来的销量变化不能记作“被我涨价挤走的顾客”。
            if (!Mathf.Approximately(previous.competitorPrice, currentCompetitorPrice)) return 0;
            float previousRelativePrice = previous.price - previous.competitorPrice;
            float currentRelativePrice = currentPlayerPrice - currentCompetitorPrice;
            if (currentRelativePrice <= previousRelativePrice + 0.01f) return 0;
            return Mathf.Max(0, currentCompetitorSold - previous.competitorSold);
        }

        static float CrossPriceElasticity(DayRecord previous, float currentPlayerPrice, float currentCompetitorPrice,
            int currentCompetitorSold, string marketEvent, string competitionEvent)
        {
            if (previous == null || !previous.competitionActive || previous.competitorSold <= 0 || Mathf.Approximately(previous.price, currentPlayerPrice))
                return float.NaN;
            // 对手自己也调过价时，它的销量变化无法归因于我的价格，交叉弹性不可算。
            if (!Mathf.Approximately(previous.competitorPrice, currentCompetitorPrice)) return float.NaN;
            // 需求曲线移动的日子不把变化错误归因于价格。
            if (!string.IsNullOrEmpty(marketEvent) || !string.IsNullOrEmpty(competitionEvent) ||
                !string.IsNullOrEmpty(previous.eventName) || !string.IsNullOrEmpty(previous.competitionEventName)) return float.NaN;
            float dq = (currentCompetitorSold - previous.competitorSold) / (float)previous.competitorSold;
            float dp = (currentPlayerPrice - previous.price) / previous.price;
            return Mathf.Approximately(dp, 0f) ? float.NaN : dq / dp;
        }

        static string StatusForProfit(float profit)
        {
            if (profit >= 900f) return "优秀";
            if (profit >= 300f) return "正常";
            if (profit >= 0f) return "承压";
            return "亏损";
        }
    }
}
