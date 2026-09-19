using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7 国际贸易编排者:每日出口撮合、进口汇总、关税累记、NX 供数与汇率市场撮合。
    /// 结算插在 WorldSettlementService.TrySettleAndAdvance 的两处固定位置:
    ///   SettleDayTrade(企业结算之后、政府 AdvanceCycle 之前——关税才能同期入账)
    ///   CloseTradeDay(政府/宏观结算之后——才能写出含当期 tariff/NX 的完整账本行)
    /// 真实交易三原则(PRD §116):
    ///   出口来自统一消费者模型的外国参数重入(EvaluateForeign);
    ///   进口=居民真实购买+企业中间投入+模式化镇集体基线(代表常设商行,系数集中本文件);
    ///   NX=X−M 只由这两类合计产生,宏观层只读不合成。
    /// 模拟随机一律走 economy.tradeRandom 独立流,绝不触碰共享流。
    /// </summary>
    public static class InternationalTradeService
    {
        // —— 镇集体"常设商行"教学基线(代表性主体建模,系数唯一出处;数值口径沿用 V0.5 外贸代理) ——
        public const float TownBulkExporterPerScaleUnit = 8.5f;   // ×scale(人口/4)×外需指数/100
        public const float TownOtherImportPerScaleUnit = 5.4f;    // ×scale×进口价格指数/100
        public const float ResidualConsumerImportShare = .03f;    // 其他居民杂项进口≈消费的 3%
        public const float IntermediateImportShareOfMaterialCost = .22f; // 原料中的进口能源/芯片份额
        const float PriceCompetitivenessExponent = .8f;
        /// <summary>出海通道解锁门槛:完成第六篇(42 章)即开放港口渠道,第 43 章以"首单真实出货"结课。</summary>
        public const int ExportUnlockChapter = 42;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.worldTrade == null) state.worldTrade = new WorldTradeState();
            WorldTradeState trade = state.worldTrade;
            ExchangeRateService.Ensure(state);
            if (trade.countryB != null && !trade.countryB.initialized) trade.countryB.initialized = true;
        }

        public static TradeDayRecord LastRecord(GameState state)
        {
            Ensure(state);
            if (state == null || state.worldTrade == null || state.worldTrade.history == null ||
                state.worldTrade.history.Count == 0) return null;
            return state.worldTrade.history[state.worldTrade.history.Count - 1];
        }

        /// <summary>
        /// 宏观 AdvancePeriod 的唯一取数口:本日已有结算行则用"当前宏观口径"刷新后返回,
        /// 尚无行时返回纯模式化基线。刷新是必要的——AdvancePeriod 先更新了 m.consumption,
        /// 基线里的"其他居民杂项进口"必须跟随同一口径,否则行值与宏观读数会各说各话。
        /// </summary>
        public static void TotalsOrBaseline(GameState state, out float exportsValue, out float importsValue)
        {
            Ensure(state);
            TradeDayRecord todays = TodayRow(state);
            if (todays != null)
            {
                FinalizeRowTotals(state, todays);
                todays.netExports = todays.exportsTotal - todays.importsTotal;
                exportsValue = todays.exportsTotal;
                importsValue = todays.importsTotal;
                return;
            }
            ComputeModeledBaseline(state, out exportsValue, out importsValue);
        }

        /// <summary>
        /// 日内撮合(管线位置:企业结算后、政府周期前)。生成当日贸易行并回填企业记录:
        /// 玩家出口占用"产能余量池"(actualCapacity − 已产量),不动库存与 actualSales 语义。
        /// </summary>
        public static void SettleDayTrade(GameState state)
        {
            Ensure(state);
            if (state == null || state.worldTrade == null) return;
            WorldTradeState trade = state.worldTrade;
            if (trade.lastTradeSettledWorldDay == state.day) return;   // 同日幂等

            TradeDayRecord row = GetOrCreateTodayRow(trade, state.day);

            // ① 玩家出口(产能配额池):开关+章节门槛+配额+港口吞吐+产能余量 四重钳制。
            int shippedUnits = ShipPlayerExports(state);
            float fob = InternationalPriceService.ProducerFobPerUnit(state);
            float shipping = InternationalPriceService.ShippingWedgePerUnit(state);
            row.playerExportUnits = shippedUnits;
            row.playerExportRevenue = shippedUnits * fob;
            row.playerShippingCost = shippedUnits * shipping;

            // ② 进口三路。
            DayRecord enterprise = state.LastRecord;
            bool settledEnterprise = enterprise != null && enterprise.day == state.day;
            float materialCost = settledEnterprise ? Mathf.Max(0f, enterprise.materialCost) : 0f;
            row.consumerImportSpend = Mathf.Max(0f, trade.todayConsumerImportSpend);
            trade.todayConsumerImportSpend = 0f;   // 已入账即归位,防累计值顺延污染次日
            // 围猎三(#7):中间投入的汇率楔子已由成本端唯一施加(CostService.EffectiveInputUnitCost
            // × IntermediateImportFactor,「楔子单一出处」契约)——这里旧的手写 Clamp 乘法把同一次
            // 贬值计了两次,M/NX/GDP 系统性偏低,贬值越深偏差越大。
            row.intermediateImportSpend = materialCost * IntermediateImportShareOfMaterialCost;
            ComputeModeledBaseline(state, out _, out float modeledImports);

            // ③ 关税:率 × 当期实际完税进口额(消费+中间投入),单源单闸,P0 恒等。
            GovernmentState government = state.government;
            float tariffRate = Mathf.Clamp(
                GovernmentSimulator.PolicyValue(state, PolicyType.ImportTariff), 0f, 60f) / 100f;
            float taxableBase = row.consumerImportSpend + row.intermediateImportSpend;
            float tariffRevenue = taxableBase * tariffRate;
            if (government.lastTariffRecordedDay != state.day && tariffRevenue > 0f)
                government.pendingTariffRevenue += tariffRevenue;
            government.lastTariffRecordedDay = state.day;
            row.tariffRateApplied = tariffRate;
            row.tariffBase = taxableBase;
            row.tariffRevenue = tariffRevenue;

            // ④ 合计并回填企业记录(MacroEconomySimulator 只读这些总值;UiLab 曲线读企业记录)。
            FinalizeRowTotals(state, row);
            if (settledEnterprise)
            {
                enterprise.exports = row.exportsTotal;
                enterprise.imports = row.importsTotal;
                if (shippedUnits > 0)
                {
                    enterprise.exportUnits = shippedUnits;
                    enterprise.exportRevenue = row.playerExportRevenue;
                    enterprise.exportShippingCost = row.playerShippingCost;
                    enterprise.revenue += row.playerExportRevenue;
                    enterprise.profit += row.playerExportRevenue - row.playerShippingCost;
                    enterprise.moneyEnd = state.money;
                }
            }

            trade.lastTradeSettledWorldDay = state.day;
        }

        /// <summary>
        /// 收日末段(政府/宏观结算后):折算 CA、交给汇率权威形成明日价、官方干预记回本行。
        /// 与玩家店开不开门解耦——歇业日照样有商行进出口与资本流动。
        /// </summary>
        public static void CloseTradeDay(GameState state)
        {
            Ensure(state);
            if (state == null || state.worldTrade == null) return;
            WorldTradeState trade = state.worldTrade;
            if (trade.lastTradeClosedWorldDay == state.day) return;   // 同日幂等

            TradeDayRecord row = GetOrCreateTodayRow(trade, state.day);
            FinalizeRowTotals(state, row);   // 兜底重算(SettleDayTrade 已跑过时为幂等覆写)

            // B 国每日简单动态(PRD §5 只做外部画像,不做完整第二经济体):
            // 增长=趋势+噪声(独立流),收入与 CPI 缓慢漂移;滚动增长转负进入衰退,回归后退出。
            AdvanceBCountry(state);

            // 冻结本日实际用汇快照:白天一切定价只准用它(P0 单向依赖凭证)。
            row.exchangeRateUsed = ExchangeRateService.Rate(state);
            row.currentAccountBCurrency =
                row.netExports / Mathf.Max(.01f, row.exchangeRateUsed);

            ExchangeRateService.ApplyDailyForm(state);   // 资本流动入账→形成明日价→干预写回本行

            trade.lastTradeClosedWorldDay = state.day;
            state.Changed();
        }

        // ———————— 内部实现 ————————

        static TradeDayRecord TodayRow(GameState state)
        {
            WorldTradeState trade = state.worldTrade;
            if (trade.history == null || trade.history.Count == 0) return null;
            TradeDayRecord last = trade.history[trade.history.Count - 1];
            return last.worldDay == state.day ? last : null;
        }

        static TradeDayRecord GetOrCreateTodayRow(WorldTradeState trade, int worldDay)
        {
            if (trade.history == null) trade.history = new System.Collections.Generic.List<TradeDayRecord>();
            if (trade.history.Count > 0 && trade.history[trade.history.Count - 1].worldDay == worldDay)
                return trade.history[trade.history.Count - 1];
            TradeDayRecord created = new TradeDayRecord { worldDay = worldDay };
            trade.history.Add(created);
            if (trade.history.Count > 730)
            {
                // 围猎三(#8):被裁行的外储干预量先累计进持久化字段,对账公式(AssertNoFreeMoney)
                // 以 800+截断累计+窗口内Σ 对账,截断不再制造永久漂移误报。
                trade.trimmedReserveChangeBCurrency += trade.history[0].reserveChangeBCurrency;
                trade.history.RemoveAt(0);
            }
            return created;
        }

        /// <summary>
        /// 模式化基线(代表常设商行的其他出口与其他居民杂项进口):
        /// 出口受汇率竞争力(rate↑ = A币便宜 = 出口↑),进口受进口价格指数放大。
        /// </summary>
        static void ComputeModeledBaseline(GameState state, out float exportsValue, out float importsValue)
        {
            MacroEconomyState macro = state.macro;
            float scale = Mathf.Max(1, state.government.population) / 4f;
            float externalIdx = macro != null ? Mathf.Clamp(macro.externalDemandIndex, 45f, 160f) : 100f;
            float rate = ExchangeRateService.Rate(state);
            float anchor = ExchangeRateService.AnchorRate(state);
            float competitiveness = Mathf.Clamp(
                Mathf.Pow(Mathf.Max(.01f, rate / anchor), PriceCompetitivenessExponent), .55f, 1.7f);
            exportsValue = scale * TownBulkExporterPerScaleUnit * externalIdx / 100f * competitiveness;

            float importIdx = ExchangeRateService.ImportPressureIndex(state);
            float otherConsumption = macro != null ? Mathf.Max(0f, macro.consumption) : 0f;
            importsValue = scale * TownOtherImportPerScaleUnit * importIdx / 100f +
                otherConsumption * ResidualConsumerImportShare;
        }

        static void FinalizeRowTotals(GameState state, TradeDayRecord row)
        {
            ComputeModeledBaseline(state, out float townExports, out float townOtherImports);
            row.townExportRevenue = townExports;
            row.townImportSpend = townOtherImports;
            row.exportsTotal = Mathf.Max(0f, row.playerExportRevenue + row.townExportRevenue);
            row.importsTotal = Mathf.Max(0f,
                row.consumerImportSpend + row.intermediateImportSpend + row.townImportSpend);
            row.netExports = row.exportsTotal - row.importsTotal;
        }

        /// <summary>玩家出海订单:B 国三类消费者同模型重入求和,再被配额/港口/产能余量钳制。</summary>
        static int ShipPlayerExports(GameState state)
        {
            WorldTradeState trade = state.worldTrade;
            bool unlocked = state.progression != null &&
                state.progression.completedChapter >= ExportUnlockChapter;
            if (!unlocked || !state.exportChannelEnabled) return 0;

            // 清晨决策节流:当日产能已被计划生产占满则无出口余量(不与内销争库存,只争人力产能)。
            CostService.Capacity(state);
            DayRecord enterprise = state.LastRecord;
            int producedToday = enterprise != null && enterprise.day == state.day ? enterprise.produced : 0;
            int capacitySlack = Mathf.Max(0,
                state.economy.labor.actualCapacity - producedToday);
            if (capacitySlack <= 0) return 0;

            int quota = Mathf.Clamp(state.exportTargetQuotaCups, 0, (int)trade.dailyPortCapacityCups);
            if (quota <= 0) return 0;

            // 同模型外国需求:价格敏感度走弹性、归因可拆——噪声用独立贸易流保证复现。
            float facePrice = InternationalPriceService.ForeignFacePricePerUnit(state);
            CountryProfileState b = trade.countryB;
            int total = 0;
            total += ConsumerDemandService.EvaluateForeign(state, b, ConsumerType.Student,
                facePrice, ForeignJitter(state)).desiredUnits;
            total += ConsumerDemandService.EvaluateForeign(state, b, ConsumerType.WhiteCollar,
                facePrice, ForeignJitter(state)).desiredUnits;
            total += ConsumerDemandService.EvaluateForeign(state, b, ConsumerType.Tourist,
                facePrice, ForeignJitter(state)).desiredUnits;
            int desired = Mathf.Max(0, total);

            int shipped = Mathf.Min(desired, Mathf.Min(quota, capacitySlack));
            float fob = InternationalPriceService.ProducerFobPerUnit(state);
            float shipping = InternationalPriceService.ShippingWedgePerUnit(state);
            if (shipped > 0)
            {
                state.UnlockCard("intl.trade");
                state.UnlockCard("intl.exports");
                state.UnlockCard("intl.netexports2");
                state.UnlockCard("intl.comparativeadvantage");
            }
            // 出货即回款:工厂交货价 Fob 实收,运费玩家承担(B 国关税在消费者端体现,不过玩家的手)。
            state.money += shipped * (fob - shipping);
            return shipped;
        }

        static float ForeignJitter(GameState state)
        {
            // 与国内 DailyPopulationJitter 同风格 ±6%,但全部落在独立 tradeRandom 流上。
            return .94f + state.economy.tradeRandom.Next01() * .12f;
        }

        /// <summary>B 国外部世界的一日演化:教学只需要"看得见的外需变化",不需要可操作的宏观面板。</summary>
        static void AdvanceBCountry(GameState state)
        {
            CountryProfileState b = state.worldTrade.countryB;
            if (b == null) return;
            float noise = (state.economy.tradeRandom.Next01() - .5f) * 2f * b.growthVolatility;
            float growth = b.gdpGrowthPeriod + noise;                 // 每期增速(%)
            b.growthMomentum = b.growthMomentum * .85f + growth;
            b.incomeLevel = Mathf.Clamp(b.incomeLevel * (1f + growth / 100f), 40f, 260f);
            b.cpiLevel = Mathf.Clamp(b.cpiLevel * (1f + Mathf.Max(0f, b.inflationPeriod) / 100f), 60f, 400f);

            // 衰退状态机:动量持续走负则进入(并压低趋势),回暖后退出(恢复常态趋势)。
            if (!b.recessionActive && b.growthMomentum < -.55f)
            {
                b.recessionActive = true;
                b.gdpGrowthPeriod = -.35f;
            }
            else if (b.recessionActive && b.growthMomentum > .18f)
            {
                b.recessionActive = false;
                b.gdpGrowthPeriod = .18f;
            }
        }
    }
}
