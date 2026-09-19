using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// 每日经济结算。需求模型(见 PRD §9/§23):
    /// 某类型需求量 = 基础需求 × 类型占比 × 类型/事件乘数 × 价格影响(幂弹性) × 随机波动
    /// </summary>
    public static class DaySimulator
    {
        public struct DemandRoll
        {
            public int students, whitecollars, tourists;
            public DemandEffectBreakdown effects;
            public int Total { get { return students + whitecollars + tourists; } }
        }

        static readonly DeterministicRandomState fallbackRandom = new DeterministicRandomState();

        public static void SetRandomSeed(int seed)
        {
            fallbackRandom.Reset(seed);
            if (GameState.I != null)
            {
                GameState.I.EnsureEconomyState();
                GameState.I.economy.random.Reset(seed);
                // V0.7 国际流同步重播种(固定异或偏移解耦):贸易事件序列可复现且不随共享流位移。
                GameState.I.economy.tradeRandom.Reset(seed ^ 1013904223);
                // V0.7.1 氛围流同步重播种(第三固定异或偏移):天气/转让牌序列可复现且与另两条流互不移位。
                GameState.I.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
            }
        }

        static long newGameSeedCalls;

        /// <summary>本类静态量快照(R10,世界分叉用):LastEvent、fallbackRandom 三元组
        /// (seed/state/calls,照 SandboxService 三元组先例)、newGameSeedCalls——
        /// 三条游戏随机流是 per-state 字段且随存档入档,分叉往返自动保真,不在此列。</summary>
        public struct ForkStatics
        {
            public TownEvent lastEvent;
            public int fallbackSeed, fallbackState;
            public long fallbackCalls, newGameSeedCalls;
        }

        /// <summary>分叉开始:快照本类全部静态量并返回,由 WorldForkService 在 finally 里恢复。</summary>
        public static ForkStatics BeginForkRun()
        {
            return new ForkStatics
            {
                lastEvent = LastEvent,
                fallbackSeed = fallbackRandom.seed,
                fallbackState = fallbackRandom.state,
                fallbackCalls = fallbackRandom.calls,
                newGameSeedCalls = newGameSeedCalls
            };
        }

        /// <summary>分叉结束:把快照值写回静态量(fork 内的漂移全部抹掉)。</summary>
        public static void EndForkRun(ForkStatics s)
        {
            LastEvent = s.lastEvent;
            fallbackRandom.seed = s.fallbackSeed;
            fallbackRandom.state = s.fallbackState;
            fallbackRandom.calls = s.fallbackCalls;
            newGameSeedCalls = s.newGameSeedCalls;
        }

        /// <summary>新开一局时以当前时间派生种子:固定默认种子会让每局的事件/竞争/失业序列完全相同。</summary>
        public static int SeedNewGame(GameState s)
        {
            newGameSeedCalls++;
            int seed = unchecked((int)((System.DateTime.Now.Ticks + newGameSeedCalls * 7919L) % int.MaxValue));
            s.EnsureEconomyState();
            s.economy.random.Reset(seed);
            s.economy.tradeRandom.Reset(seed ^ 1013904223);
            s.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
            return seed;
        }

        internal static float NextRandom01() { return NextRandom01(GameState.I); }
        internal static float NextRandom01(GameState state)
        {
            if (state == null) return fallbackRandom.Next01();
            state.EnsureEconomyState();
            return state.economy.random.Next01();
        }
        internal static int NextRandom(int min, int max) { return NextRandom(GameState.I, min, max); }
        internal static int NextRandom(GameState state, int min, int max)
        {
            if (state == null) return fallbackRandom.Next(min, max);
            state.EnsureEconomyState();
            return state.economy.random.Next(min, max);
        }

        /// <summary>某类型在当前价格下的期望需求(不含随机波动)。</summary>
        public static int ExpectedDemand(GameState s, ConsumerType t)
        {
            return ConsumerDemandService.ExpectedPlayerDemand(s, t);
        }

        /// <summary>忽略政府价格楔子时的需求，用于计算政策前基线与无谓损失。</summary>
        public static int ExpectedDemandWithoutPolicy(GameState s, ConsumerType t)
        {
            return ConsumerDemandService.ExpectedPlayerDemand(s, t, false);
        }

        /// <summary>开盘前掷出今天的实际需求(含 ±8% 随机波动)。</summary>
        public static DemandRoll RollDemand(GameState s)
        {
            float playerPrice = PriceService.PlayerListedPrice(s);
            float competitorPrice = s.CompetitionActive ? s.competitor.price : Eco.BasePrice;
            ConsumerChoiceResult student = ConsumerDemandService.Evaluate(s, ConsumerType.Student,
                playerPrice, competitorPrice, s.CompetitionActive, DailyPopulationJitter(s), 1f, 1f);
            ConsumerChoiceResult white = ConsumerDemandService.Evaluate(s, ConsumerType.WhiteCollar,
                playerPrice, competitorPrice, s.CompetitionActive, DailyPopulationJitter(s), 1f, 1f);
            ConsumerChoiceResult tourist = ConsumerDemandService.Evaluate(s, ConsumerType.Tourist,
                playerPrice, competitorPrice, s.CompetitionActive, DailyPopulationJitter(s), 1f, 1f);
            return new DemandRoll
            {
                students = student.player,
                whitecollars = white.player,
                tourists = tourist.player,
                effects = ConsumerDemandService.Combine(s,
                    student.playerEffects, white.playerEffects, tourist.playerEffects)
            };
        }

        public static void AllocateSoldByType(DemandRoll demand, int sold,
            out int students, out int whitecollars, out int tourists)
        {
            int studentDemand = Mathf.Max(0, demand.students);
            int whitecollarDemand = Mathf.Max(0, demand.whitecollars);
            int touristDemand = Mathf.Max(0, demand.tourists);
            int totalDemand = studentDemand + whitecollarDemand + touristDemand;
            int target = Mathf.Clamp(sold, 0, totalDemand);
            students = whitecollars = tourists = 0;
            if (target == 0) return;

            float factor = target / (float)totalDemand;
            students = Mathf.Min(studentDemand, Mathf.FloorToInt(studentDemand * factor));
            whitecollars = Mathf.Min(whitecollarDemand, Mathf.FloorToInt(whitecollarDemand * factor));
            tourists = Mathf.Min(touristDemand, Mathf.FloorToInt(touristDemand * factor));

            int missing = target - students - whitecollars - tourists;
            int add = Mathf.Min(missing, studentDemand - students);
            students += add;
            missing -= add;
            add = Mathf.Min(missing, whitecollarDemand - whitecollars);
            whitecollars += add;
            missing -= add;
            tourists += Mathf.Min(missing, touristDemand - tourists);
        }

        static float DailyPopulationJitter(GameState state)
        {
            return .92f + NextRandom01(state) * .16f;
        }

        /// <summary>今天实际能生产的数量:受计划、原料、人力产能、设备上限约束。</summary>
        public static int Production(GameState s)
        {
            int cap = CostService.Capacity(s).actualCapacity;
            int feasible = Mathf.Max(0, Mathf.Min(s.planProduction, s.materials, cap));
            return Mathf.Min(feasible, GovernmentSimulator.ApplySupplyResponse(s, feasible,
                PriceService.PlayerListedPrice(s)));
        }

        /// <summary>今日单位变动成本(原料),事件可推高。</summary>
        public static float MaterialUnitPrice(GameState s)
        {
            return CostService.EffectiveInputUnitCost(s);
        }

        /// <summary>
        /// 收盘结算:写入 DayRecord、扣钱、扣料、更新库存。sold 按类型比例分配。
        /// </summary>
        public static DayRecord Settle(GameState s, DemandRoll demand, int produced)
        {
            float effectiveListedPrice = PriceService.PlayerListedPrice(s);
            int available = produced + s.inventory;
            int demandTotal = demand.Total;
            int sold = Mathf.Min(demandTotal, available);

            AllocateSoldByType(demand, sold, out int sSold, out int wSold, out int tSold);

            CostBreakdown costs = CostService.Calculate(s, produced, false);
            float matUnit = costs.effectiveInputUnitCost;
            float materialCost = costs.materialCost;
            float wageCost = costs.wageCost;
            float rentCost = costs.fixedCost;
            float totalCost = costs.totalCost;
            float revenue = sold * GovernmentSimulator.DrinkProducerPrice(s, effectiveListedPrice);
            float profit = revenue - totalCost;

            s.materials -= produced;
            // 原料在市场采购时已支付，这里只发生营业现金流，避免重复扣款。
            s.money += revenue - costs.cashCostAfterPrepaidInputs;
            int inventoryEnd = available - sold;
            s.inventory = inventoryEnd;

            int lost = demandTotal - sold;
            int stockout = Mathf.Max(0, demandTotal - available);

            DayRecord r = new DayRecord
            {
                day = s.day,
                shopOpen = true,
                listedPrice = effectiveListedPrice,
                price = effectiveListedPrice,
                planned = s.planProduction,
                produced = produced,
                desiredDemand = demandTotal,
                availableSupply = available,
                actualSales = sold,
                operationalStockout = stockout,
                operationalSurplus = Mathf.Max(0, available - demandTotal),
                demandTotal = demandTotal,
                demandStudents = demand.students,
                demandWhitecollars = demand.whitecollars,
                demandTourists = demand.tourists,
                soldStudents = sSold,
                soldWhitecollars = wSold,
                soldTourists = tSold,
                sold = sold,
                lostCustomers = lost,
                stockoutLost = stockout,
                revenue = revenue,
                materialCost = materialCost,
                wageCost = wageCost,
                rentCost = rentCost,
                totalCost = totalCost,
                profit = profit,
                inventoryEnd = inventoryEnd,
                materialsEnd = s.materials,
                spoiled = s.economy.spoilageAtDayStart,
                satisfaction = SatisfactionService.Calculate(
                    PriceService.Quote(s, effectiveListedPrice).consumerPrice,
                    demandTotal, sold, s.PlayerQuality, s.playerBrand),
                eventName = LongTermExperienceService.CombinedEventTitle(s),
                moneyEnd = s.money,
                marginalCost = matUnit
            };
            r.demandEffects = demand.effects ?? AggregateEffects(s);
            r.capitalInvestment = Mathf.Max(0f, s.todayStrategicSpend) + Mathf.Max(0f, s.pendingFinancedInvestment);
            s.pendingFinancedInvestment = 0f;
            // 围猎三(#39):计入当日资本形成后即清零——与 CompetitionSimulator.Settle 对称,
            // 否则旧值会漏到后续营业日重复计入(fork 走本路径时每期都重复计)。
            s.todayStrategicSpend = 0f;
            // V0.7:r.exports/r.imports 改由 InternationalTradeService.SettleDayTrade 以真实贸易流回填。
            int policyBaseline = ExpectedDemandWithoutPolicy(s, ConsumerType.Student) +
                ExpectedDemandWithoutPolicy(s, ConsumerType.WhiteCollar) +
                ExpectedDemandWithoutPolicy(s, ConsumerType.Tourist);
            GovernmentSimulator.RecordDrinkSettlement(s, r, sold, policyBaseline);
            // 兜底:同一天绝不写入第二条企业记录(防异常路径双结算污染环比与曲线)。
            if (s.history.Count > 0 && s.history[s.history.Count - 1].day == r.day)
                s.history.RemoveAt(s.history.Count - 1);
            s.history.Add(r);
            FinancialHealthService.RecordSettledDay(s);
            s.economy.spoilageAtDayStart = 0;
            return r;
        }

        /// <summary>顾客满意度:价格越公道越高,缺货扣分。</summary>
        public static float Satisfaction(GameState s, int sold, int stockout)
        {
            return SatisfactionService.Calculate(PriceService.Quote(s, PriceService.PlayerListedPrice(s)).consumerPrice,
                sold + Mathf.Max(0, stockout), sold, s.PlayerQuality, s.playerBrand);
        }

        public static DayRecord SettleClosedDay(GameState s)
        {
            float effectiveListedPrice = PriceService.PlayerListedPrice(s);
            DemandRoll demand = RollDemand(s);
            CostBreakdown costs = CostService.Calculate(s, 0, false);
            s.money -= costs.cashCostAfterPrepaidInputs;
            DayRecord record = new DayRecord
            {
                day = s.day,
                shopOpen = false,
                listedPrice = effectiveListedPrice,
                price = effectiveListedPrice,
                planned = s.planProduction,
                desiredDemand = demand.Total,
                demandTotal = demand.Total,
                availableSupply = s.inventory,
                actualSales = 0,
                sold = 0,
                lostCustomers = demand.Total,
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
                moneyEnd = s.money,
                marginalCost = costs.effectiveInputUnitCost,
                demandEffects = demand.effects ?? AggregateEffects(s),
                capitalInvestment = Mathf.Max(0f, s.pendingFinancedInvestment)
            };
            s.pendingFinancedInvestment = 0f;
            // 策略性支出已在升级时支付,歇业日不计成本,也不允许漏到下一个营业日
            // (围猎三(#39),与 CompetitionSimulator.SettleClosedDay 同款)。
            s.todayStrategicSpend = 0f;
            GovernmentSimulator.RecordDrinkSettlement(s, record, 0,
                ExpectedDemandWithoutPolicy(s, ConsumerType.Student) +
                ExpectedDemandWithoutPolicy(s, ConsumerType.WhiteCollar) +
                ExpectedDemandWithoutPolicy(s, ConsumerType.Tourist));
            // 兜底:同一天绝不写入第二条企业记录。
            if (s.history.Count > 0 && s.history[s.history.Count - 1].day == record.day)
                s.history.RemoveAt(s.history.Count - 1);
            s.history.Add(record);
            FinancialHealthService.RecordSettledDay(s);
            s.economy.spoilageAtDayStart = 0;
            return record;
        }

        internal static DemandEffectBreakdown AggregateEffects(GameState s)
        {
            float playerPrice = PriceService.PlayerListedPrice(s);
            ConsumerChoiceResult student = ConsumerDemandService.Evaluate(s, ConsumerType.Student,
                playerPrice, s.competitor.price, s.CompetitionActive, 1f, 1f, 1f);
            ConsumerChoiceResult white = ConsumerDemandService.Evaluate(s, ConsumerType.WhiteCollar,
                playerPrice, s.competitor.price, s.CompetitionActive, 1f, 1f, 1f);
            ConsumerChoiceResult tourist = ConsumerDemandService.Evaluate(s, ConsumerType.Tourist,
                playerPrice, s.competitor.price, s.CompetitionActive, 1f, 1f, 1f);
            return ConsumerDemandService.Combine(s,
                student.playerEffects, white.playerEffects, tourist.playerEffects);
        }

        /// <summary>进入新的一天:应用隔夜损耗、重置事件。返回损耗数量。</summary>
        public static int AdvanceDay(GameState s)
        {
            int spoiled = Mathf.FloorToInt(s.inventory * Eco.SpoilageRate);
            s.inventory -= spoiled;

            int competitorSpoiled = 0;
            if (s.competitor != null)
            {
                competitorSpoiled = Mathf.FloorToInt(s.competitor.inventory * Eco.SpoilageRate);
                s.competitor.inventory -= competitorSpoiled;
            }

            s.day++;
            // 围猎三复审:新一天清晨的品质等级快照——读档重放日据此回滚当日升级
            // (升级费随 money 基线回退,等级不退=「升级→checkpoint→恢复」每轮白拿一级)。
            s.playerQualityLevelAtDayStart = s.playerQualityLevel;
            s.economy.spoilageAtDayStart = spoiled;
            s.economy.competitorSpoilageAtDayStart = competitorSpoiled;
            s.demandMul = 1f; s.studentMul = 1f; s.touristMul = 1f; s.materialPriceMul = 1f;
            s.todayEventName = null;
            s.todayEventId = null;
            // 广告预算是“今日决策”，不能静默变成每天自动续费；品牌余温则逐日衰减。
            s.marketingBudget = 0f;
            s.playerMarketingMomentum *= 0.55f;
            // 居民选择形成市场口碑，但不会永久锁死需求；每天逐渐回归常态。
            s.residentPlayerDemandMul = Mathf.Lerp(s.residentPlayerDemandMul, 1f, .18f);
            s.residentCompetitorDemandMul = Mathf.Lerp(s.residentCompetitorDemandMul, 1f, .18f);

            if (s.EventsActive)
            {
                TownEvent ev = RollTownEvent(s);
                if (ev != null)
                {
                    s.todayEventName = ev.name;
                    s.todayEventId = ev.id;
                    s.demandMul = ev.demandMul;
                    s.studentMul = ev.studentMul;
                    s.touristMul = ev.touristMul;
                    s.materialPriceMul = ev.materialMul;
                    LastEvent = ev;
                    return spoiled;
                }
            }
            LastEvent = null;
            return spoiled;
        }

        public static TownEvent LastEvent;

        static TownEvent RollTownEvent(GameState s)
        {
            if (NextRandom01(s) >= LongTermExperienceService.TownEventProbability(s)) return null;
            float total = 0f;
            for (int i = 0; i < TownEvents.All.Length; i++) total += TownEvents.All[i].weight;
            float roll = NextRandom01(s) * total;
            for (int i = 0; i < TownEvents.All.Length; i++)
            {
                roll -= TownEvents.All[i].weight;
                if (roll <= 0f) return TownEvents.All[i];
            }
            return null;
        }

        /// <summary>用最近两天记录计算需求价格弹性(弧弹性近似)。</summary>
        public static float Elasticity(DayRecord a, DayRecord b)
        {
            if (a == null || b == null) return float.NaN;
            if (Mathf.Approximately(a.price, b.price) || a.demandTotal == 0 || b.demandTotal == 0) return float.NaN;
            float averageQ = (a.demandTotal + b.demandTotal) * .5f;
            float averageP = (a.price + b.price) * .5f;
            if (Mathf.Approximately(averageQ, 0f) || Mathf.Approximately(averageP, 0f)) return float.NaN;
            float dQ = (b.demandTotal - a.demandTotal) / averageQ;
            float dP = (b.price - a.price) / averageP;
            return dQ / dP;
        }
    }
}
