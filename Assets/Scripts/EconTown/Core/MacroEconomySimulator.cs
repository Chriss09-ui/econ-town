using UnityEngine;

namespace EconTown
{
    /// <summary>V0.5 宏观经济规则层。只从既有主体记录取数，不依赖 UI。</summary>
    public static class MacroEconomySimulator
    {
        const float FoodWeight = .30f;
        const float HousingWeight = .30f;
        const float TransportWeight = .15f;
        const float BeverageWeight = .05f;
        const float EntertainmentWeight = .10f;
        const float OtherWeight = .10f;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.macro == null) state.macro = new MacroEconomyState();
            MacroEconomyState m = state.macro;
            if (m.history == null) m.history = new System.Collections.Generic.List<MacroPeriodRecord>();
            if (m.news == null) m.news = new System.Collections.Generic.List<string>();
            if (m.workingAgePopulation <= 0) m.workingAgePopulation = Mathf.RoundToInt(state.government.population * .78f);
            if (m.laborForce <= 0) m.laborForce = Mathf.RoundToInt(m.workingAgePopulation * .88f);
            if (m.employment <= 0) m.employment = Mathf.RoundToInt(m.laborForce * .91f);
            RecalculateLabor(m);
            m.fiscalMultiplier = 1f / Mathf.Max(.2f, 1f - Mathf.Clamp(m.marginalPropensityToConsume, .35f, .88f));
            if (state.government != null) m.businessConfidence = state.government.enterpriseConfidence;
        }

        public static MacroPeriodRecord AdvancePeriod(GameState state)
        {
            Ensure(state);
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            FinancialSystemService.Ensure(state);
            WorldClock.SyncDerivedPeriods(state);
            MacroEconomyState m = state.macro;
            if (m.history.Count > 0 && m.history[m.history.Count - 1].period == state.day)
                return m.history[m.history.Count - 1];
            if (state.government.history.Count == 0 ||
                state.government.history[state.government.history.Count - 1].cycle != state.day)
                GovernmentSimulator.AdvanceCycle(state);
            MacroPeriodRecord previous = m.history.Count > 0 ? m.history[m.history.Count - 1] : null;

            ApplyPendingShock(state, m);
            // 围猎三(#6):冲击指数温和均值回归——旧实现只有冲击写点、无任何回归,水平冲击被
            // UpdatePrices 当成每期速率复利施加(一次能源危机=交通分项每天 +2.28% 直到钳死,
            // 总供给永久 -28%),外需两次冲击钳 45 后 trade.bulkorder 剧情永久锁死。
            // 每期向 100 收回 3%(纯算术,不碰随机流)。
            m.energyCostIndex = Mathf.Lerp(m.energyCostIndex, 100f, .03f);
            m.importPriceIndex = Mathf.Lerp(m.importPriceIndex, 100f, .03f);
            m.externalDemandIndex = Mathf.Lerp(m.externalDemandIndex, 100f, .03f);
            int population = Mathf.Max(1, state.government.population);
            float scale = population / 4f;
            float directFiscalPurchase = FiscalPolicyService.DirectPurchase(m);
            float inducedFiscalDemand = FiscalPolicyService.InducedDemand(m, directFiscalPurchase);

            // C 以最终消费为口径。居民账本优先；果汁店销售只用于没有居民账本时校准，绝不重复相加。
            ResidentDayRecord resident = state.resident.history != null && state.resident.history.Count > 0
                ? state.resident.history[state.resident.history.Count - 1] : null;
            DayRecord enterprise = state.LastRecord;
            float representativeSpending = resident != null ? resident.spending : EstimateFinalConsumption(enterprise);
            float employmentFactor = Mathf.Lerp(.78f, 1.05f, Mathf.Clamp01(m.employment / Mathf.Max(1f, m.laborForce)));
            float consumptionBase = representativeSpending * scale * 3.8f * employmentFactor;
            FinancialEffectBreakdown consumptionFinance = InterestRateService.ResidentConsumptionBreakdown(state, consumptionBase);
            m.financialConsumptionEffect = consumptionFinance.finalValue - consumptionFinance.baseValue;
            m.consumption = Mathf.Max(400f, consumptionFinance.finalValue + inducedFiscalDemand * .72f);

            // I 只计资本品：品质/设备投入、竞争企业投资与信心驱动的其余私营部门投资。
            float playerCapital = enterprise != null ? Mathf.Max(0f, enterprise.capitalInvestment) : 0f;
            float profitSignal = enterprise != null ? Mathf.Clamp(enterprise.profit / 600f, -.4f, .6f) : 0f;
            float investmentBase = scale * (22f + m.businessConfidence * .23f) * (1f + profitSignal);
            FinancialEffectBreakdown investmentFinance = InterestRateService.CompanyInvestmentBreakdown(state, investmentBase);
            m.financialInvestmentEffect = investmentFinance.finalValue - investmentFinance.baseValue;
            m.investment = Mathf.Max(220f, playerCapital + investmentFinance.finalValue + inducedFiscalDemand * .28f);

            GovernmentCycleRecord government = state.government.history != null && state.government.history.Count > 0
                ? state.government.history[state.government.history.Count - 1] : null;
            float recordedG = government != null
                ? government.publicServiceSpending + government.capitalSpending + GovernmentSimulator.AdministrativeSpendingPerCycle
                : GovernmentSimulator.AdministrativeSpendingPerCycle + GovernmentSimulator.PolicyValue(state, PolicyType.PublicServiceBudget);
            // G 只记政府直接购买；乘数带来的后续消费和投资分别进入 C 与 I。
            m.governmentSpending = Mathf.Max(0f, recordedG + m.oneOffGovernmentPurchases);

            // V0.7:P0 恒等——NX 只来自真实贸易账本(本日结算行;尚无则用模式化基线兜底),
            // 宏观层不再有任何合成系数(PRD §98「禁止宏观模块自己再生成一个 NX」)。
            InternationalTradeService.TotalsOrBaseline(state, out float tradeExports, out float tradeImports);
            m.exports = tradeExports;
            m.imports = tradeImports;
            m.netExports = m.exports - m.imports;
            m.nominalGdp = IdentityGdp(m.consumption, m.investment, m.governmentSpending, m.exports, m.imports);

            UpdatePrices(state, m, enterprise);
            float priceDeflator = Mathf.Max(.5f, m.cpi / 100f);
            m.realGdp = m.nominalGdp / priceDeflator;
            UpdateProductiveCapacity(state, m);
            m.outputGap = (m.realGdp - m.potentialGdp) / Mathf.Max(1f, m.potentialGdp) * 100f;
            m.aggregateDemand = m.realGdp;
            m.aggregateSupply = m.potentialGdp * Mathf.Clamp(100f / Mathf.Max(70f, m.energyCostIndex), .72f, 1.08f);
            UpdateLaborAndCycle(state, m, previous);

            m.gdpGrowth = previous == null ? 0f : (m.realGdp - previous.realGdp) / Mathf.Max(1f, previous.realGdp) * 100f;
            m.perCapitaGdp = m.realGdp / population;
            float deficit = Mathf.Max(0f, -state.government.budgetBalance);
            m.deficitToGdp = deficit / Mathf.Max(1f, m.nominalGdp) * 100f;
            m.debtToGdp = state.government.debt / Mathf.Max(1f, m.nominalGdp) * 100f;
            m.incomeInequality = Mathf.Clamp(55f - state.government.lowestGroupPurchasingPower * .45f +
                state.government.highIncomeShare * .25f, 15f, 65f);
            m.policyRate = state.centralBank.policyRate;
            m.depositRate = state.bank.depositRate;
            m.companyLoanRate = state.bank.companyLoanRate;
            m.creditGrowth = state.bank.creditGrowth;
            m.bankReserveRatio = state.bank.ActualReserveRatio;
            m.bankEquity = state.bank.equity;
            m.financialStress = state.bank.financialStress;
            m.moneySupplyM0 = FinancialSystemService.MoneySupplyM0(state);
            m.moneySupplyM1 = FinancialSystemService.MoneySupplyM1(state);
            UpdateConfidence(state, m);
            BuildHeadline(state, m);

            MacroPeriodRecord record = Snapshot(state, m);
            m.history.Add(record);
            m.news.Insert(0, "第 " + m.period + " 期｜" + record.headline);
            if (m.news.Count > 12) m.news.RemoveAt(m.news.Count - 1);
            UnlockLearning(state, m);
            m.pendingShock = MacroShockType.None;
            m.oneOffGovernmentPurchases = 0f;
            state.Changed();
            return record;
        }

        public static float IdentityGdp(float consumption, float investment, float governmentSpending, float exports, float imports)
        {
            return consumption + investment + governmentSpending + exports - imports;
        }

        public static float ValueAddedGdp(float rawValue, float intermediateValue, float finalValue)
        {
            float farm = Mathf.Max(0f, rawValue);
            float mill = Mathf.Max(0f, intermediateValue - rawValue);
            float bakery = Mathf.Max(0f, finalValue - intermediateValue);
            return farm + mill + bakery;
        }

        public static float WeightedCpi(float food, float housing, float transport, float beverages, float entertainment, float other)
        {
            return food * FoodWeight + housing * HousingWeight + transport * TransportWeight +
                beverages * BeverageWeight + entertainment * EntertainmentWeight + other * OtherWeight;
        }

        public static void SetFiscalPolicy(GameState state, FiscalStance stance, float publicInvestment, float taxChange)
        {
            Ensure(state);
            MacroEconomyState m = state.macro;
            m.fiscalStance = stance;
            m.plannedPublicInvestment = Mathf.Clamp(publicInvestment, 0f, 3200f);
            // V0.5.1 不允许宏观面板暗中改税；税收只能由 GovernmentState 的显式政策产生。
            m.plannedTaxChange = 0f;
            m.fiscalLagStage = stance == FiscalStance.Neutral ? 0 : 1;
            m.policyPreview = PreviewFiscalPolicy(m, stance, m.plannedPublicInvestment, m.plannedTaxChange);
            state.Changed();
        }

        public static string PreviewFiscalPolicy(MacroEconomyState m, FiscalStance stance, float publicInvestment, float taxChange)
        {
            float multiplier = m == null ? 1.5f : m.fiscalMultiplier;
            if (stance == FiscalStance.Expansionary)
                return "方向：总需求、订单与就业上升。风险：赤字、债务与通胀压力上升。投入完全传导后约形成 " +
                    CivicBooks.Money(publicInvestment * multiplier) + " 的需求拉动，但审批和施工存在时滞。";
            if (stance == FiscalStance.Contractionary)
                return "方向：给过热需求降温。风险：短期订单、就业与增长放缓；紧缩过强可能把经济推入衰退。";
            return "保持中性：不主动放大或压低总需求，自动稳定器仍会随经济变化。";
        }

        public static void QueueShock(GameState state, MacroShockType shock)
        {
            Ensure(state);
            state.macro.pendingShock = shock;
            state.Changed();
        }

        public static float DemandFeedback(GameState state)
        {
            if (state == null || state.macro == null) return 1f;
            MacroEconomyState m = state.macro;
            float confidence = Mathf.Lerp(.82f, 1.10f, m.consumerConfidence / 100f);
            float jobs = Mathf.Lerp(.82f, 1.06f, Mathf.Clamp01(1f - m.unemploymentRate / 25f));
            return Mathf.Clamp(confidence * jobs, .68f, 1.18f);
        }

        public static float SupplyCostFeedback(GameState state)
        {
            if (state == null || state.macro == null) return 1f;
            return Mathf.Clamp(state.macro.energyCostIndex / 100f, .82f, 1.75f);
        }

        static float EstimateFinalConsumption(DayRecord enterprise)
        {
            if (enterprise == null) return 48f;
            float player = enterprise.sold * Mathf.Max(1f, enterprise.consumerPrice > 0f ? enterprise.consumerPrice : enterprise.price);
            float rival = enterprise.competitorSold * Mathf.Max(1f, enterprise.competitorPrice);
            return Mathf.Max(24f, (player + rival) / 18f);
        }

        static void UpdatePrices(GameState state, MacroEconomyState m, DayRecord enterprise)
        {
            float oldCpi = Mathf.Max(1f, m.cpi);
            float outputPressure = Mathf.Clamp((m.nominalGdp - m.potentialGdp) / Mathf.Max(1f, m.potentialGdp), -.12f, .15f);
            float energyPressure = (m.energyCostIndex - 100f) / 100f;
            float drinkPrice = enterprise != null
                ? Mathf.Max(4f, enterprise.consumerPrice > 0f ? enterprise.consumerPrice : enterprise.price)
                : state.price;
            m.beveragePriceIndex = Mathf.Lerp(m.beveragePriceIndex, drinkPrice / Eco.BasePrice * 100f, .35f);
            m.transportPriceIndex = Mathf.Clamp(m.transportPriceIndex * (1f + energyPressure * .06f), 70f, 220f);
            m.foodPriceIndex = Mathf.Clamp(m.foodPriceIndex * (1f + energyPressure * .025f + outputPressure * .03f), 70f, 220f);
            // V0.9.2 起住房分项唯一权威=HousingService.AdvancePeriod(PRD §194.4 单一写点接管);
            // 这里不再演化 housingPriceIndex,当期 CPI 直接读住房引擎刚写好的新值。
            m.entertainmentPriceIndex = Mathf.Clamp(m.entertainmentPriceIndex * (1f + outputPressure * .035f), 70f, 200f);
            m.otherPriceIndex = Mathf.Clamp(m.otherPriceIndex * (1f + outputPressure * .025f), 70f, 200f);

            // —— V0.7 输入型通胀(PRD §15):双滚分差法 ——
            // 先记录不含汇率压力的 CPI,再把"进口价格指数相对锚的偏离"追加传导到三类
            // 进口敏感分项(交通/食物/其他)。两轮之差就是汇率对本期通胀的净贡献,
            // 贬值为正、升值为负,教学上"国内通胀不一定来自国内需求"。
            float cpiWithoutFx = WeightedCpi(m.foodPriceIndex, m.housingPriceIndex, m.transportPriceIndex,
                m.beveragePriceIndex, m.entertainmentPriceIndex, m.otherPriceIndex);
            ExchangeRateService.SyncMacroSnapshot(state);
            float fxPressure = (ExchangeRateService.ImportPressureIndex(state) - 100f) / 100f;
            m.transportPriceIndex = Mathf.Clamp(m.transportPriceIndex * (1f + fxPressure * .08f), 70f, 220f);
            m.foodPriceIndex = Mathf.Clamp(m.foodPriceIndex * (1f + fxPressure * .05f), 70f, 220f);
            m.otherPriceIndex = Mathf.Clamp(m.otherPriceIndex * (1f + fxPressure * .03f), 70f, 200f);
            m.cpi = WeightedCpi(m.foodPriceIndex, m.housingPriceIndex, m.transportPriceIndex,
                m.beveragePriceIndex, m.entertainmentPriceIndex, m.otherPriceIndex);
            m.exchangeRateInflationContribution =
                Mathf.Clamp((m.cpi - cpiWithoutFx) / oldCpi * 100f, -1.5f, 1.5f);
            m.inflation = (m.cpi - oldCpi) / oldCpi * 100f;
        }

        static void UpdateProductiveCapacity(GameState state, MacroEconomyState m)
        {
            float schoolBonus = 0f;
            bool schoolBuilt = false;
            for (int i = 0; i < state.government.projects.Count; i++)
                if (state.government.projects[i].type == PublicProjectType.School && state.government.projects[i].built)
                    schoolBuilt = true;
            if (schoolBuilt) schoolBonus = .32f;
            // 学校是产能水平的持久跃升:加成只在建成后的第一期一次性授予并记下持久化标志。
            // 此前每期都 +0.32,30 期会白赚 +9.6,建得越早越赚(C7)。
            bool grantSchoolBonus = schoolBuilt && !m.schoolProductivityBonusGranted;
            if (grantSchoolBonus) m.schoolProductivityBonusGranted = true;
            float techGain = m.pendingShock == MacroShockType.AiRevolution ? 4.5f : 0f;
            m.productivity = Mathf.Clamp(m.productivity + (grantSchoolBonus ? schoolBonus : 0f) + techGain + m.investment / 18000f, 75f, 180f);
            m.capitalStock = Mathf.Max(1000f, m.capitalStock * .985f + m.investment);
            float laborCapacity = m.laborForce * m.productivity * 0.55f;
            float capitalCapacity = Mathf.Sqrt(m.capitalStock) * 72f;
            m.potentialGdp = Mathf.Max(5000f, laborCapacity + capitalCapacity + state.government.publicServiceLevel * 28f);
        }

        static void UpdateLaborAndCycle(GameState state, MacroEconomyState m, MacroPeriodRecord previous)
        {
            LaborMarketSnapshot labor = LaborMarketService.Evaluate(state, m.outputGap, m.pendingShock);
            m.workingAgePopulation = labor.workingAgePopulation;
            m.laborForce = labor.laborForce;
            m.employment = labor.employment;
            RecalculateLabor(m);
            m.frictionalUnemployment = Mathf.Min(m.unemployment, Mathf.RoundToInt(m.laborForce * .025f));
            m.structuralUnemployment = Mathf.Min(m.unemployment - m.frictionalUnemployment,
                Mathf.RoundToInt(m.laborForce * (m.pendingShock == MacroShockType.AiRevolution ? .045f : .018f)));
            m.cyclicalUnemployment = Mathf.Max(0, m.unemployment - m.frictionalUnemployment - m.structuralUnemployment);
            m.averageWage = labor.effectiveDailyWage / 8f;

            float growth = previous == null ? 0f : (m.realGdp - previous.realGdp) / Mathf.Max(1f, previous.realGdp) * 100f;
            if (m.outputGap > 4f && m.inflation > 2.5f) m.cyclePhase = MacroCyclePhase.Overheating;
            else if (growth < -1.5f && m.outputGap < -4f) m.cyclePhase = MacroCyclePhase.Recession;
            else if (growth < 0f) m.cyclePhase = MacroCyclePhase.Slowdown;
            else if (m.outputGap < -5f && growth >= 0f) m.cyclePhase = MacroCyclePhase.Trough;
            else if (m.outputGap < 0f) m.cyclePhase = MacroCyclePhase.Recovery;
            else m.cyclePhase = MacroCyclePhase.Expansion;
        }

        static void RecalculateLabor(MacroEconomyState m)
        {
            m.employment = Mathf.Clamp(m.employment, 0, m.laborForce);
            m.unemployment = Mathf.Max(0, m.laborForce - m.employment);
            m.unemploymentRate = m.laborForce > 0 ? m.unemployment * 100f / m.laborForce : 0f;
        }

        static void UpdateConfidence(GameState state, MacroEconomyState m)
        {
            float growthSignal = Mathf.Clamp(m.gdpGrowth, -8f, 8f);
            // V0.9.2 财富效应单闸门(PRD §195.1):房价影响消费只走信心这一个入口,不开旁门——
            // 持房正贡献/无房负贡献已含在内;消费侧仍只经 DemandFeedback 的既有 Lerp。
            float wealthEffect = HousingService.ConfidenceContribution(state);
            m.consumerConfidence = Mathf.Clamp(m.consumerConfidence + growthSignal * .6f - m.inflation * .2f - m.unemploymentRate * .035f + wealthEffect, 20f, 95f);
            // 企业信心的权威定义在 GovernmentState；宏观记录只保存同值快照。
            m.businessConfidence = Mathf.Clamp(state.government.enterpriseConfidence, 15f, 100f);
            state.government.employmentRate = Mathf.Clamp(100f - m.unemploymentRate +
                state.government.eventEmploymentRateOffset, 0f, 100f);
        }

        static void ApplyPendingShock(GameState state, MacroEconomyState m)
        {
            switch (m.pendingShock)
            {
                case MacroShockType.ConsumptionBoom:
                    m.consumerConfidence = Mathf.Clamp(m.consumerConfidence + 14f, 0f, 100f);
                    break;
                case MacroShockType.InvestmentBoom:
                    state.government.enterpriseConfidence += 18f;
                    m.capitalStock += 900f;
                    break;
                case MacroShockType.EnergyCrisis:
                    m.energyCostIndex = Mathf.Clamp(m.energyCostIndex + 38f, 80f, 220f);
                    m.importPriceIndex = Mathf.Clamp(m.importPriceIndex + 22f, 80f, 220f);
                    state.government.enterpriseConfidence -= 10f;
                    break;
                case MacroShockType.GlobalDemandFall:
                    m.externalDemandIndex = Mathf.Clamp(m.externalDemandIndex - 30f, 45f, 160f);
                    state.government.enterpriseConfidence -= 8f;
                    break;
                case MacroShockType.AiRevolution:
                    state.government.enterpriseConfidence += 9f;
                    break;
                case MacroShockType.Disaster:
                    m.capitalStock *= .93f;
                    m.consumerConfidence -= 12f;
                    m.oneOffGovernmentPurchases += 900f;
                    state.government.residentHappiness = Mathf.Clamp(state.government.residentHappiness - 10f, 0f, 100f);
                    break;
                case MacroShockType.PopulationGrowth:
                    state.government.population += 24;
                    break;

                // —— V0.7 国际经济冲击 ——
                case MacroShockType.TradeWarEscalation:
                    if (state.worldTrade != null)
                    {
                        CountryProfileState b = state.worldTrade.countryB;
                        b.tariffOnAExports = Mathf.Clamp(b.tariffOnAExports + .08f, 0f, .6f);
                        state.worldTrade.capitalFlow.riskPremiumPercent =
                            Mathf.Clamp(state.worldTrade.capitalFlow.riskPremiumPercent + 1f, 0f, 6f);
                    }
                    m.externalDemandIndex = Mathf.Clamp(m.externalDemandIndex - 12f, 45f, 160f);
                    state.government.enterpriseConfidence -= 10f;
                    break;
                case MacroShockType.CapitalSuddenStop:
                    if (state.worldTrade != null)
                    {
                        state.worldTrade.capitalFlow.riskPremiumPercent =
                            Mathf.Clamp(state.worldTrade.capitalFlow.riskPremiumPercent + 2.5f, 0f, 6f);
                        state.worldTrade.capitalFlow.expectedDepreciationPercent =
                            Mathf.Clamp(state.worldTrade.capitalFlow.expectedDepreciationPercent + 6f, 0f, 25f);
                    }
                    state.government.enterpriseConfidence -= 8f;
                    break;
                case MacroShockType.DevaluationSpike:
                    // 失守跳贬的后续传导(汇率侧已在 AbandonPeg 处理可信度与风险溢价)。
                    m.importPriceIndex = Mathf.Clamp(m.importPriceIndex + 26f, 80f, 220f);
                    state.government.enterpriseConfidence -= 12f;
                    break;

                // —— V0.9.3 杠杆周期(§196.3)——
                case MacroShockType.HousingReversal:
                    // 相位钉入 Deflating,下行链经既有公式自动展开(Demand 罚/估值跟随/财富效应
                    // 反号/法拍坏账)。带数字新闻行在 BuildHeadline 首分支。
                    if (state.housing != null) state.housing.leveragePhase = LeveragePhase.Deflating;
                    break;
            }
            m.consumerConfidence = Mathf.Clamp(m.consumerConfidence, 15f, 98f);
            state.government.enterpriseConfidence = Mathf.Clamp(state.government.enterpriseConfidence, 15f, 100f);
            m.businessConfidence = state.government.enterpriseConfidence;
        }

        static void BuildHeadline(GameState state, MacroEconomyState m)
        {
            // V0.9.3 反转新闻行(§196.3):带当前回撤/泡沫压力数字——新闻要能当仪表读
            if (m.pendingShock == MacroShockType.HousingReversal && state.housing != null)
            {
                HousingState h = state.housing;
                float index = h.townHousePrice / HousingService.BaseHousePrice * 100f;
                float drawdown = h.peakPriceIndex > 0f ? Mathf.Max(0f, 1f - index / h.peakPriceIndex) : 0f;
                m.latestHeadline = "房贷市场反转：房价从峰值回撤 " + (drawdown * 100f).ToString("0.0") +
                                   "%｜泡沫压力 " + h.bubblePressure.ToString("0");
                m.latestCausalChain = "泡沫压力≥60 + 紧缩触发 → 相位 Deflating → 需求罚 + 抵押物缩水 → " +
                                      "银行惜贷(LTVeff↓) → 房价与财富效应同降 → 消费与 GDP 衰退";
            }
            else if (m.pendingShock == MacroShockType.EnergyCrisis)
            {
                m.latestHeadline = "能源冲击推高物价，产出与就业同时承压";
                m.latestCausalChain = "能源价格↑ → 企业成本↑ → 总供给左移 → 产出↓、失业↑、通胀↑（滞胀）";
            }
            else if (m.fiscalStance == FiscalStance.Expansionary && m.fiscalLagStage > 1)
            {
                m.latestHeadline = "公共投资开始传导，订单与就业回升";
                m.latestCausalChain = "政府购买↑ → 建设订单↑ → 就业与收入↑ → 居民消费↑ → 实际 GDP↑；同时赤字与通胀风险↑";
            }
            else if (m.gdpGrowth < 0f)
            {
                m.latestHeadline = "总需求走弱，经济进入" + CycleName(m.cyclePhase);
                m.latestCausalChain = "消费/投资信心↓ → 企业销售↓ → 生产与招聘↓ → 失业↑ → 收入与消费进一步下降";
            }
            else
            {
                m.latestHeadline = "实际 GDP " + (m.gdpGrowth >= 0f ? "增长" : "下降") + " " + Mathf.Abs(m.gdpGrowth).ToString("0.0") + "%";
                m.latestCausalChain = "C " + CivicBooks.Money(m.consumption) + " + I " + CivicBooks.Money(m.investment) +
                    " + G " + CivicBooks.Money(m.governmentSpending) + " + NX " + CivicBooks.Signed(m.netExports) + " = 名义 GDP";
            }
        }

        static MacroPeriodRecord Snapshot(GameState state, MacroEconomyState m)
        {
            return new MacroPeriodRecord
            {
                period = m.period, nominalGdp = m.nominalGdp, realGdp = m.realGdp, potentialGdp = m.potentialGdp,
                gdpGrowth = m.gdpGrowth, perCapitaGdp = m.perCapitaGdp, consumption = m.consumption,
                investment = m.investment, governmentSpending = m.governmentSpending, exports = m.exports,
                imports = m.imports, netExports = m.netExports, cpi = m.cpi, inflation = m.inflation,
                population = state.government.population, workingAgePopulation = m.workingAgePopulation,
                laborForce = m.laborForce, employment = m.employment, unemployment = m.unemployment,
                unemploymentRate = m.unemploymentRate, frictionalUnemployment = m.frictionalUnemployment,
                structuralUnemployment = m.structuralUnemployment, cyclicalUnemployment = m.cyclicalUnemployment,
                averageWage = m.averageWage, consumerConfidence = m.consumerConfidence,
                businessConfidence = m.businessConfidence, productivity = m.productivity, capitalStock = m.capitalStock,
                aggregateDemand = m.aggregateDemand, aggregateSupply = m.aggregateSupply, outputGap = m.outputGap,
                deficitToGdp = m.deficitToGdp, debtToGdp = m.debtToGdp, incomeInequality = m.incomeInequality,
                policyRate = m.policyRate, depositRate = m.depositRate, companyLoanRate = m.companyLoanRate,
                creditGrowth = m.creditGrowth, bankReserveRatio = m.bankReserveRatio, bankEquity = m.bankEquity,
                financialStress = m.financialStress, moneySupplyM0 = m.moneySupplyM0, moneySupplyM1 = m.moneySupplyM1,
                financialConsumptionEffect = m.financialConsumptionEffect,
                financialInvestmentEffect = m.financialInvestmentEffect,
                exchangeRateContributionPp = m.exchangeRateInflationContribution,
                exchangeRate = ExchangeRateService.Rate(state),
                tariffRevenue = LastSettledTariff(state),
                cyclePhase = m.cyclePhase,
                shock = m.pendingShock, headline = m.latestHeadline, causalChain = m.latestCausalChain
            };
        }

        static void UnlockLearning(GameState state, MacroEconomyState m)
        {
            string[] basic = { "gdp", "valueadded", "consumption", "investment", "governmentspending",
                "netexports", "nominalgdp", "realgdp", "percapitagdp" };
            for (int i = 0; i < basic.Length; i++) state.UnlockCard(basic[i]);
            if (m.period >= 2) { state.UnlockCard("cpi"); state.UnlockCard("inflation"); }
            if (m.period >= 3)
            {
                state.UnlockCard("unemploymentrate"); state.UnlockCard("frictionalunemployment");
                state.UnlockCard("structuralunemployment"); state.UnlockCard("cyclicalunemployment");
                state.UnlockCard("businesscycle");
            }
            if (m.period >= 4) { state.UnlockCard("potentialgdp"); state.UnlockCard("outputgap"); }
            if (m.period >= 5)
            {
                state.UnlockCard("aggregatedemand"); state.UnlockCard("demandshock");
                state.UnlockCard("aggregatesupply");
            }
            if (m.pendingShock == MacroShockType.EnergyCrisis)
            {
                state.UnlockCard("supplyshock"); state.UnlockCard("aggregatesupply"); state.UnlockCard("stagflation");
            }
            if (m.pendingShock == MacroShockType.AiRevolution)
            {
                state.UnlockCard("productivity"); state.UnlockCard("structuralunemployment");
            }
            if (m.fiscalStance != FiscalStance.Neutral)
            {
                state.UnlockCard(m.fiscalStance == FiscalStance.Expansionary ? "expansionaryfiscal" : "contractionaryfiscal");
                state.UnlockCard("fiscalmultiplier"); state.UnlockCard("mpc"); state.UnlockCard("automaticstabilizer");
            }
            if (m.period >= 9) { state.UnlockCard("productivity"); state.UnlockCard("longrungrowth"); }
            m.chapter = Mathf.Clamp(1 + m.history.Count, 1, 9);
            m.chapterMask |= 1 << (m.chapter - 1);
        }

        public static string CycleName(MacroCyclePhase phase)
        {
            switch (phase)
            {
                case MacroCyclePhase.Expansion: return "扩张";
                case MacroCyclePhase.Overheating: return "过热";
                case MacroCyclePhase.Slowdown: return "放缓";
                case MacroCyclePhase.Recession: return "衰退";
                case MacroCyclePhase.Trough: return "谷底";
                default: return "复苏";
            }
        }

        /// <summary>本周期入账的关税收(周期记录里才有;batch3 起由 AdvanceCycle 快照)。</summary>
        static float LastSettledTariff(GameState state)
        {
            if (state.government == null || state.government.history == null ||
                state.government.history.Count == 0) return 0f;
            GovernmentCycleRecord last = state.government.history[state.government.history.Count - 1];
            return last.cycle == state.day ? Mathf.Max(0f, last.tariffRevenue) : 0f;
        }
    }
}
