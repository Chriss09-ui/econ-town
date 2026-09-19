using UnityEngine;

namespace EconTown
{
    /// <summary>汇率日度四+一项分解(百分点):Σ各分量 ≈ 当日实际相对变动%。新闻归因直接读取。</summary>
    public struct RateDecomposition
    {
        public float tradeFlow;        // 经常账户推力(顺差→本币升值=负分量)
        public float capitalFlow;      // 利差−风险溢价推力(净流入→升值)
        public float expectation;      // 贬值/升值预期的自强化(含微小市场噪声)
        public float intervention;     // 央行干预修正(守汇为正:压低 X = 买本币)
        public float anchorPull;       // 向名义锚的温和回归
        public float totalMovePercent;
    }

    /// <summary>
    /// V0.7 全项目唯一汇率权威(PRD §85)。报价约定恒为「1 B币 = X A币」——X 变大 = A币贬值。
    /// 汇率形成(ApplyDailyForm)、换算、固定汇率干预与资本流动影响全部只经由此处,
    /// 其他模块禁止自行推算汇率或另建第二套汇率状态。
    ///
    /// 时序规则(鸡生蛋一次性了断):白天一切定价与订单撮合只用"今日 rate 只读快照";
    /// 收日 CloseTradeDay 把本日实际用到的汇率冻结进 TradeDayRecord.exchangeRateUsed 并落账
    /// CA/KA/官方干预;随后 ApplyDailyForm 只决定"明日"开盘价。当天流量绝不参与当天定价。
    ///
    /// 分解恒等(P0):rate_new = rate_prev × (1 + Σ五分量/100)。clamp 命中区间边界时例外——
    /// 测试只用内点验证"分解和≈实际变动"。
    /// </summary>
    public static class ExchangeRateService
    {
        public const float MinRate = 5.6f;
        public const float MaxRate = 8.4f;
        public const float DefaultBaseRate = 7f;

        // 形成系数:一切均为"每日百分点"。教学可感、但任一分量每日贡献都被钳在 ±0.10pt。
        const float TradeImpactScalePp = .22f;   // tanh(CA/CA参考规模)×此值
        const float CaReferenceScaleBCurrency = 260f;
        const float CapitalPerCarryPointPp = .006f; // 每 1pp 净利差对应的日变动
        const float ExpectationContinuationFactor = .30f; // 上日变动的 30% 自强化
        const float ExpectationMaxPp = .05f;
        const float AnchorPullPerGapRatio = .020f;   // 每偏离锚 1% 回归 2%/日
        const float ComponentClampPp = .10f;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.worldTrade == null) state.worldTrade = new WorldTradeState();
            WorldTradeState trade = state.worldTrade;
            if (trade.countryB == null) trade.countryB = new CountryProfileState();
            if (trade.capitalFlow == null) trade.capitalFlow = new CapitalFlowState();
            if (trade.history == null) trade.history = new System.Collections.Generic.List<TradeDayRecord>();
            if (trade.dailyPortCapacityCups <= 0f || trade.dailyPortCapacityCups > 90f)
                trade.dailyPortCapacityCups = 90f;
            if (trade.shippingCostPerUnitAcurrency < 0f || trade.shippingCostPerUnitAcurrency > 8f)
                trade.shippingCostPerUnitAcurrency = 2.2f;

            CountryProfileState b = trade.countryB;
            // B 国参数坏档兜底:缺字段/手改档一律拉回教学默认,防止价格幂运算或利差被 NaN 污染。
            b.incomeLevel = b.incomeLevel > 0f ? Mathf.Clamp(b.incomeLevel, 40f, 260f) : 100f;
            b.policyRate = b.policyRate >= .25f && b.policyRate <= 20f ? b.policyRate : 4f;
            b.drinkBenchmarkPriceBCurrency =
                b.drinkBenchmarkPriceBCurrency >= .1f && b.drinkBenchmarkPriceBCurrency <= 10f
                    ? b.drinkBenchmarkPriceBCurrency : 1.05f;
            b.preferenceDomestic = Mathf.Clamp(Mathf.Max(0f, b.preferenceDomestic), 0f, 1f);
            b.qualityWeight = Mathf.Clamp(Mathf.Max(0f, b.qualityWeight), 0f, 1f);
            b.brandWeight = Mathf.Clamp(Mathf.Max(0f, b.brandWeight), 0f, 1f);
            b.outsideWeight = Mathf.Clamp(b.outsideWeight, .05f, .9f);
            b.tariffOnAExports = Mathf.Clamp(b.tariffOnAExports, 0f, .6f);
            b.gdpGrowthPeriod = Mathf.Clamp(b.gdpGrowthPeriod, -3f, 3f);
            b.inflationPeriod = Mathf.Clamp(b.inflationPeriod, -2f, 6f);
            b.growthVolatility = Mathf.Clamp(b.growthVolatility, 0f, 3f);

            CapitalFlowState capital = trade.capitalFlow;
            capital.riskPremiumPercent = Mathf.Clamp(capital.riskPremiumPercent, 0f, 6f);
            capital.credibilityMirror = Mathf.Clamp(capital.credibilityMirror, 0f, 100f);

            ExchangeRateState fx = trade.exchangeRate;
            if (fx == null) { fx = new ExchangeRateState(); trade.exchangeRate = fx; }
            if (!fx.initialized)
            {
                // 首初始化:名义锚与盯住目标都落回基准报价,浮动制起步。
                fx.initialized = true;
                fx.baseRate = DefaultBaseRate;
                fx.rate = DefaultBaseRate;
                fx.equilibriumRate = DefaultBaseRate;
                fx.pegTarget = DefaultBaseRate;
                fx.regime = ExchangeRegime.Floating;
            }
            if (fx.baseRate < MinRate || fx.baseRate > MaxRate) fx.baseRate = DefaultBaseRate;
            fx.rate = fx.rate < MinRate || fx.rate > MaxRate ? fx.baseRate : fx.rate;
            fx.equilibriumRate = fx.equilibriumRate < MinRate || fx.equilibriumRate > MaxRate
                ? fx.rate : fx.equilibriumRate;
            fx.pegTarget = fx.pegTarget < MinRate || fx.pegTarget > MaxRate ? fx.baseRate : fx.pegTarget;
            fx.bandWidthPercent = Mathf.Clamp(fx.bandWidthPercent, .01f, .08f);
        }

        /// <summary>白天唯一读点:今日生效报价「1 B币 = X A币」。</summary>
        public static float Rate(GameState state)
        {
            Ensure(state);
            return state != null && state.worldTrade != null && state.worldTrade.exchangeRate != null
                ? state.worldTrade.exchangeRate.rate : DefaultBaseRate;
        }

        public static string RegimeName(GameState state)
        {
            Ensure(state);
            return state != null && state.worldTrade != null &&
                   state.worldTrade.exchangeRate.regime == ExchangeRegime.Fixed ? "固定汇率" : "浮动汇率";
        }

        /// <summary>换算互逆是 P0 不变量:ConvertBToA(x)/ConvertAToB(结果) 必须还原 x。</summary>
        public static float ConvertBToA(GameState state, float amountBCurrency)
        {
            return amountBCurrency * Rate(state);
        }

        public static float ConvertAToB(GameState state, float amountACurrency)
        {
            return amountACurrency / Rate(state);
        }

        /// <summary>
        /// 进口价格指数(锚=100):汇率↑(A币贬值)→进口品涨价→指数上行。CPI 输入型通胀的数据源。
        /// </summary>
        public static float ImportPressureIndex(GameState state)
        {
            return Mathf.Clamp(Rate(state) / AnchorRate(state) * 100f, 70f, 130f);
        }

        /// <summary>名义锚(fx.baseRate,坏值兜底回默认 7)。</summary>
        public static float AnchorRate(GameState state)
        {
            Ensure(state);
            ExchangeRateState fx = Fx(state);
            return fx != null && fx.baseRate > MinRate && fx.baseRate < MaxRate
                ? fx.baseRate : DefaultBaseRate;
        }

        /// <summary>把今日汇率快照给宏观面板读数用(每期结算时同步一次)。</summary>
        public static void SyncMacroSnapshot(GameState state)
        {
            if (state == null || state.macro == null) return;
            state.macro.exchangeRateSnapshot = Rate(state);
        }

        /// <summary>最近一次形成的分解(优先取史册末条;无史册时返回零分解)。</summary>
        public static RateDecomposition LastDecomposition(GameState state)
        {
            RateDecomposition d = default;
            ExchangeRateState fx = Fx(state);
            if (fx != null && fx.history != null && fx.history.Count > 0)
            {
                RateDayRecord last = fx.history[fx.history.Count - 1];
                d.tradeFlow = last.tradeFlowEffect;
                d.capitalFlow = last.capitalFlowEffect;
                d.expectation = last.expectationEffect;
                d.intervention = last.interventionEffect;
                d.anchorPull = last.anchorPullEffect;
                d.totalMovePercent =
                    d.tradeFlow + d.capitalFlow + d.expectation + d.intervention + d.anchorPull;
            }
            return d;
        }

        /// <summary>
        /// 收日末段调用:由昨日冻结的国际收支与资本流形成明日开盘价,写入当日分解与汇率史。
        /// Float 制直接采用市场出清价;Fixed 制先算市场价,再按偏离带消耗外储把成交价拉回。
        /// </summary>
        public static void ApplyDailyForm(GameState state)
        {
            WorldTradeState trade = state != null ? state.worldTrade : null;
            if (trade == null || trade.exchangeRate == null || !trade.exchangeRate.initialized)
            {
                Ensure(state);
                if (state == null || state.worldTrade == null) return;
                trade = state.worldTrade;
            }
            ExchangeRateState fx = trade.exchangeRate;
            int day = state.day;
            if (fx.lastFormedWorldDay >= day) return;   // 幂等:同一天只形成一次

            TradeDayRecord flows = trade.history != null && trade.history.Count > 0
                ? trade.history[trade.history.Count - 1] : null;
            float ca = flows != null ? flows.currentAccountBCurrency : 0f;
            float ka = flows != null ? flows.capitalAccountBCurrency : 0f;

            // 先入账本日资本流动(冻结利差与可信度),再用它形成价格——避免用到上一期陈旧利差。
            PostCapitalFlow(state, trade);
            ka = trade.capitalFlow.inflowBCurrency - trade.capitalFlow.outflowBCurrency;
            if (flows != null && flows.worldDay == day) flows.capitalAccountBCurrency = ka;

            RateDecomposition d = new RateDecomposition
            {
                // 逆差(CA<0)=本币供给过剩=贬值(X↑);顺差反之。
                tradeFlow = -(float)System.Math.Tanh(ca / CaReferenceScaleBCurrency) * TradeImpactScalePp,
                // 利差高于风险溢价→资本流入→升值(X↓=负分量)。单位:每 1pp 净利差 0.006pp/日。
                capitalFlow = -Mathf.Clamp(CapitalCarry(trade) * CapitalPerCarryPointPp,
                    -ComponentClampPp, ComponentClampPp),
                anchorPull = Mathf.Clamp(
                    -(fx.equilibriumRate / AnchorRate(state) - 1f) * 100f * AnchorPullPerGapRatio,
                    -ComponentClampPp, ComponentClampPp)
            };
            // 预期项:上一日实际变动方向的温和自强化 × 可信度折扣,再叠加 ±0.01pt 的市场噪声
            // (走 economy.tradeRandom 独立流,保证 SimDriver 双跑可复现)。
            float noise = (state.economy != null && state.economy.tradeRandom != null
                    ? state.economy.tradeRandom.Next01() : .5f) * .02f - .01f;
            d.expectation = Mathf.Clamp(
                (fx.LastMovePercent() * ExpectationContinuationFactor +
                 noise) *
                (60f / Mathf.Max(40f, state.centralBank.credibility)),
                -ExpectationMaxPp, ExpectationMaxPp);

            float marketMovePct = d.tradeFlow + d.capitalFlow + d.expectation + d.anchorPull;
            marketMovePct = Mathf.Clamp(marketMovePct, -1.5f, 1.5f);
            float marketPrice = ClampRate(fx.equilibriumRate * (1f + marketMovePct / 100f));
            fx.equilibriumRate = marketPrice;

            if (fx.regime == ExchangeRegime.Fixed)
            {
                float deviation = (marketPrice - fx.pegTarget) / fx.pegTarget;   // + = 本币承压贬值方向
                if (Mathf.Abs(deviation) <= fx.bandWidthPercent)
                {
                    fx.consecutiveDefenseDays = 0;
                    fx.consecutiveLowReserveDays = 0;
                    // bug搜#6:固定制下守住承诺本身在积累信誉(+2/日,封顶 80)——此前可信度
                    // 全项目只有失守 −25 一个写点,只降不升,固定汇率功能可被一次性永久锁死。
                    state.centralBank.credibility =
                        Mathf.Min(80f, state.centralBank.credibility + 2f);
                    // 带内:小幅向锚回归,保留真实市场噪声但不做储备交易。
                    fx.interventionEffect = 0f;
                    float repegPct = -(marketPrice - fx.pegTarget) / fx.pegTarget * 50f;
                    marketPrice = ClampRate(marketPrice * (1f + repegPct / 100f));
                    RecordHistory(state, fx, trade, day, marketPrice, ref d, ca, ka, 0f, 0f);
                    return;
                }

                // 带外:必须消耗外储把成交价拉回。消耗量按缺口等比折算:
                //   每日干预效果(pp) = -缺口% × 实际消耗/所需
                // 即"有多少弹药就补多少缺口",弹药不足按比例少补——永不过冲到锚另一侧。
                CentralBankState central = state.centralBank;
                float requiredB = Mathf.Abs(deviation) * 1200f;
                float availableB = Mathf.Max(0f, central.forexReservesBCurrency - central.forexReserveFloor);
                float actualB = Mathf.Min(requiredB, Mathf.Min(availableB, central.maxDailyInterventionBCurrency));
                float closingRatio = Mathf.Clamp01(actualB / Mathf.Max(1f, requiredB));
                fx.interventionEffect = -deviation * 100f * closingRatio;
                d.intervention = fx.interventionEffect;
                float officialNetPurchase = deviation > 0f ? -actualB : actualB;   // 守汇=卖出外储
                central.forexReservesBCurrency = Mathf.Max(0f,
                    central.forexReservesBCurrency + officialNetPurchase);
                central.lastInterventionBCurrency = officialNetPurchase;
                central.cumulativeInterventionBCurrency += Mathf.Abs(actualB);
                central.interventionDays += actualB > 0f ? 1 : 0;
                central.lastFxSettledWorldDay = day;

                marketPrice = ClampRate(marketPrice * (1f + fx.interventionEffect / 100f));
                fx.consecutiveDefenseDays += 1;
                fx.consecutiveLowReserveDays = availableB <= .01f ? fx.consecutiveLowReserveDays + 1 : 0;

                // 失守判定:P0 教学不变量"固定汇率允许失守"。连续 3 日无弹药或缺口超过 15%。
                if ((availableB <= .01f && fx.consecutiveLowReserveDays >= 3) ||
                    Mathf.Abs((marketPrice - fx.pegTarget) / fx.pegTarget) > .15f)
                {
                    AbandonPeg(state, fx, trade, d);
                    // 围猎三(#1):账本行的 reserveChange 必须等于 officialNetPurchase
                    // (WorldTradeModels 对账不变量)——升值方向(deviation<0,央行净买入)旧硬编码
                    // -actualB 把符号记反,AssertNoFreeMoney 漂移随守汇天数线性累加。
                    RecordHistory(state, fx, trade, day, fx.rate, ref d, ca, ka, officialNetPurchase,
                        officialNetPurchase);
                    return;
                }
                RecordHistory(state, fx, trade, day, marketPrice, ref d, ca, ka, officialNetPurchase,
                    officialNetPurchase);
                // bug搜#6:带外消耗弹药守住,同样是兑现承诺(+2/日,封顶 80)。
                central.credibility = Mathf.Min(80f, central.credibility + 2f);
                fx.lastFormedWorldDay = day;
                state.Changed();
                return;
            }

            // Floating:市价即成交价,官方零干预。
            // bug搜#6:浮动制下信誉缓释修复(+1/日,封顶 60=再固定的门槛)——失守 −25 后唯一
            // 的重建途径:浮动期不折腾地攒回 60,才可能重新盯住(想要更高的 80 得靠守汇积累);
            // 已在 60 以上(如从未失守的 80)只维持不降。
            if (state.centralBank.credibility < 60f)
                state.centralBank.credibility = Mathf.Min(60f, state.centralBank.credibility + 1f);
            fx.interventionEffect = 0f;
            RecordHistory(state, fx, trade, day, marketPrice, ref d, ca, ka, 0f, 0f);
        }

        /// <summary>
        /// 切换汇率制度(PRD §50)。固定制不是把 exchangeRate 写死——需要可信度、外储与冷静期
        /// 三道门槛;资源不足时拒绝建立盯住,失守后 7 天内不得重新盯住。
        /// </summary>
        public static bool TrySetRegime(GameState state, ExchangeRegime regime, out string message)
        {
            Ensure(state);
            message = "";
            if (state == null || state.worldTrade == null || state.worldTrade.exchangeRate == null) return false;
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            CentralBankState central = state.centralBank;
            if (regime == fx.regime) { message = "已经是" + RegimeName(state); return true; }

            if (regime == ExchangeRegime.Fixed)
            {
                if (central.credibility < 60f)
                {
                    message = "央行可信度不足（" + central.credibility.ToString("0") +
                        "/60）：市场不会相信你能守住承诺，先重建信誉。";
                    return false;
                }
                if (central.forexReservesBCurrency < 500f)
                {
                    message = "外汇储备不足（" + central.forexReservesBCurrency.ToString("0") +
                        " B币 < 500）：守汇需要弹药，先补充储备再固定。";
                    return false;
                }
                if (fx.lastAbandonPegWorldDay >= 0 && state.day - fx.lastAbandonPegWorldDay < 7)
                {
                    int wait = 7 - (state.day - fx.lastAbandonPegWorldDay);
                    message = "刚经历失守，市场仍在风声鹤唳（还需 " + wait + " 天冷静期）。";
                    return false;
                }
                fx.regime = ExchangeRegime.Fixed;
                fx.pegTarget = ClampRate(fx.equilibriumRate);   // 从当前市价起锚
                message = "宣布固定汇率：1 B币 = " + fx.pegTarget.ToString("0.00") +
                    " A币。偏离带 ±" + (fx.bandWidthPercent * 100f).ToString("0") + "%，需要用外储维持。";
                state.Changed();
                return true;
            }

            fx.regime = ExchangeRegime.Floating;
            fx.consecutiveDefenseDays = 0;
            fx.consecutiveLowReserveDays = 0;
            message = "转入浮动汇率：让市场供求决定「1 B币」值多少 A币，央行不再承诺点位。";
            state.Changed();
            return true;
        }

        /// <summary>调整盯住目标价(仅固定制下有意义;教学旋钮,不校验经济合理性——失守会教他)。</summary>
        public static bool TrySetPeg(GameState state, float target, out string message)
        {
            Ensure(state);
            message = "";
            if (state == null || state.worldTrade == null || state.worldTrade.exchangeRate == null) return false;
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            if (fx.regime != ExchangeRegime.Fixed)
            {
                message = "当前是浮动汇率；先在制度面板切换到固定汇率。";
                return false;
            }
            float clamped = Mathf.Clamp(target, MinRate, MaxRate);
            if (Mathf.Abs(clamped - fx.pegTarget) < .001f) { message = "盯住价未变化。"; return true; }
            fx.pegTarget = clamped;
            message = "新盯住目标：1 B币 = " + clamped.ToString("0.00") + " A币。偏离带将围绕新锚计算。";
            state.Changed();
            return true;
        }

        /// <summary>
        /// 外储守恒对账(P0:外储不可凭空增减):史上所有干预记账之和必须等于
        /// 当前储备 − 初始基线(800 B币)。银行日账本收口时调用。
        /// </summary>
        public static void AssertNoFreeMoney(GameState state)
        {
            if (state == null || state.worldTrade == null || state.worldTrade.history == null) return;
            WorldTradeState trade = state.worldTrade;
            if (!trade.exchangeRate.initialized) return;
            const float initialBaseline = 800f;
            // 围猎三(#8):账本 730 行截断后,被裁行的干预量累计在 trimmed 字段——
            // 全史对账 = 截断累计 + 窗口内Σ,不因裁行永久误报。
            float ledgerSum = trade.trimmedReserveChangeBCurrency;
            for (int i = 0; i < trade.history.Count; i++)
                ledgerSum += trade.history[i].reserveChangeBCurrency;
            float drift = Mathf.Abs(state.centralBank.forexReservesBCurrency - (initialBaseline + ledgerSum));
            // 干预量按 0.05 元粒度折算,累积浮点误差上限与天数线性无关地被 .10 吸收。
            if (drift > .10f)
                Debug.LogError("外汇储备账实不符：储备=" +
                    state.centralBank.forexReservesBCurrency.ToString("0.00") +
                    " 账面(基线800+Σ干预)=" + (initialBaseline + ledgerSum).ToString("0.00") +
                    " 漂移=" + drift.ToString("0.000"));
        }

        /// <summary>失守:一次性跳贬到市场出清价并转浮动。后续冲击与冷却由 MacroShocks/新闻层响应。</summary>
        static void AbandonPeg(GameState state, ExchangeRateState fx, WorldTradeState trade,
            RateDecomposition d)
        {
            fx.regime = ExchangeRegime.Floating;
            fx.rate = fx.equilibriumRate;
            fx.consecutiveDefenseDays = 0;
            fx.consecutiveLowReserveDays = 0;
            fx.lastAbandonPegWorldDay = state.day;
            state.centralBank.credibility = Mathf.Clamp(state.centralBank.credibility - 25f, 0f, 100f);
            trade.capitalFlow.riskPremiumPercent = Mathf.Clamp(
                trade.capitalFlow.riskPremiumPercent + 1.8f, 0f, 6f);
            trade.capitalFlow.expectedDepreciationPercent =
                Mathf.Clamp((fx.rate - fx.pegTarget) / fx.pegTarget * 100f, 0f, 25f);
            MacroEconomySimulator.QueueShock(state, MacroShockType.DevaluationSpike);
        }

        static void RecordHistory(GameState state, ExchangeRateState fx, WorldTradeState trade, int day,
            float formedPrice, ref RateDecomposition d, float ca, float ka, float officialNetPurchase,
            float reserveChange)
        {
            fx.rate = formedPrice;
            fx.tradeFlowEffect = d.tradeFlow;
            fx.capitalFlowEffect = d.capitalFlow;
            fx.expectationEffect = d.expectation;
            fx.anchorPullEffect = d.anchorPull;
            fx.history.Add(new RateDayRecord
            {
                worldDay = day,
                rate = formedPrice,
                regime = fx.regime,
                tradeFlowEffect = d.tradeFlow,
                capitalFlowEffect = d.capitalFlow,
                expectationEffect = d.expectation,
                interventionEffect = d.intervention,
                anchorPullEffect = d.anchorPull,
                currentAccountBCurrency = ca,
                capitalAccountBCurrency = ka,
                forexReservesEndBCurrency = state.centralBank.forexReservesBCurrency
            });
            if (fx.history.Count > 730) fx.history.RemoveAt(0);

            // 官方干预入当日贸易账本(它与今日已完成结算的记录是同一行:CloseTradeDay 先落账、后形成)。
            TradeDayRecord todays = trade.history != null && trade.history.Count > 0 &&
                trade.history[trade.history.Count - 1].worldDay == day
                ? trade.history[trade.history.Count - 1] : null;
            if (todays != null)
            {
                todays.officialInterventionBCurrency = officialNetPurchase;
                todays.reserveChangeBCurrency = reserveChange;
            }
            fx.lastFormedWorldDay = day;
            state.Changed();
        }

        /// <summary>
        /// 金融账户简化 posting(PRD §35-37):净利差−风险溢价 × 可信度^1.5 × 贬值预期挤压。
        /// 结果写进资本流动状态并冻结当日 KA——必须真实参与外汇需求(P0),不允许只给汇率加 buff。
        /// </summary>
        static void PostCapitalFlow(GameState state, WorldTradeState trade)
        {
            CapitalFlowState capital = trade.capitalFlow;
            CentralBankState central = state.centralBank;
            capital.interestDifferentialPercent = central.policyRate - trade.countryB.policyRate;
            capital.credibilityMirror = central.credibility;
            float carry = CapitalCarry(trade);
            float raw = carry * 45f;                                   // B币/期,教学量级
            raw *= Mathf.Pow(Mathf.Clamp(central.credibility, 20f, 100f) / 80f, 1.5f);
            raw *= Mathf.Clamp01(1f - capital.expectedDepreciationPercent / 15f);
            float netInflow = Mathf.Clamp(raw, -90f, 120f);
            capital.inflowBCurrency = Mathf.Max(0f, netInflow);
            capital.outflowBCurrency = Mathf.Max(0f, -netInflow);

            TradeDayRecord todays = trade.history != null && trade.history.Count > 0 &&
                trade.history[trade.history.Count - 1].worldDay == state.day
                ? trade.history[trade.history.Count - 1] : null;
            if (todays != null)
            {
                todays.capitalInflowBCurrency = capital.inflowBCurrency;
                todays.capitalOutflowBCurrency = capital.outflowBCurrency;
                todays.capitalAccountBCurrency = capital.inflowBCurrency - capital.outflowBCurrency;
            }
            capital.lastSettledWorldDay = state.day;
        }

        static float CapitalCarry(WorldTradeState trade)
        {
            return trade.capitalFlow.interestDifferentialPercent - trade.capitalFlow.riskPremiumPercent;
        }

        static float ClampRate(float value)
        {
            return Mathf.Clamp(value, MinRate, MaxRate);
        }

        static ExchangeRateState Fx(GameState state)
        {
            return state != null && state.worldTrade != null ? state.worldTrade.exchangeRate : null;
        }
    }

    internal static class ExchangeRateStateExtensions
    {
        /// <summary>最近一日实际相对变动%(无史册时 0)。</summary>
        public static float LastMovePercent(this ExchangeRateState fx)
        {
            if (fx.history == null || fx.history.Count < 1) return 0f;
            RateDayRecord last = fx.history[fx.history.Count - 1];
            RateDayRecord prev = fx.history.Count >= 2 ? fx.history[fx.history.Count - 2] : null;
            float previousRate = prev != null ? prev.rate : ExchangeRateService.DefaultBaseRate;
            if (previousRate <= 0f) return 0f;
            return (last.rate - previousRate) / previousRate * 100f;
        }
    }
}
