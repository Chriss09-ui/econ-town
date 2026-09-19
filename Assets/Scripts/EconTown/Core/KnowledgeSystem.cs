using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public enum KnowledgeCategory { Enterprise, Consumer, Market, Government, Macro, Finance, International, Housing }
    public enum KnowledgeLevel { Basic, Advanced, Extension }
    public enum KnowledgeStatus { Undiscovered, Discovered, Understood, Experimented }

    public class KnowledgeMetadata
    {
        public string cardId;
        public KnowledgeCategory category;
        public KnowledgeLevel level;
        public string experimentKey;
        public string keywords;
    }

    [Serializable]
    public class KnowledgeCaseRecord
    {
        public string cardId;
        public int worldDay;
        public string summary;
    }

    [Serializable]
    public class KnowledgeProgressState
    {
        public List<string> understoodCardIds = new List<string>();
        public List<string> experimentedCardIds = new List<string>();
        public List<string> explanationLogIds = new List<string>();
        public List<KnowledgeCaseRecord> cases = new List<KnowledgeCaseRecord>();
    }

    /// <summary>知识卡导航与学习状态。卡片正文仍由 CardLibrary 持有，稳定 id 是唯一存档键。</summary>
    public static class KnowledgeSystem
    {
        static readonly HashSet<string> consumerIds = Set(
            "preference", "utility", "totalutility", "marginalutility", "diminishingutility",
            "budgetconstraint", "budgetline", "indifferencecurve", "optimalbundle", "normalgood",
            "inferiorgood", "complement", "incomeeffect", "substitutioneffect", "laborleisure",
            "saving", "realpurchasingpower");

        static readonly HashSet<string> marketIds = Set(
            "substitute", "crosselasticity", "differentiation", "marketshare", "marketpower",
            "perfectcompetition", "monopolisticcompetition", "oligopoly", "monopoly", "gametheory",
            "prisonersdilemma", "nashequilibrium", "pricewar", "consumersurplus", "producersurplus",
            "imperfectinfo", "equilibrium", "elasticity");

        static readonly HashSet<string> governmentIds = Set(
            "marketfailure", "externality", "positiveexternality", "negativeexternality", "publicgood",
            "freerider", "commoditytax", "taxincidence", "taxelasticity", "subsidy", "deadweightloss",
            "priceceiling", "pricefloor", "shortage", "surplus", "minimumwage", "governmentspending",
            "fiscalrevenue", "fiscaldeficit", "governmentdebt", "equityefficiency", "governmentfailure");

        static readonly HashSet<string> macroIds = Set(
            "gdp", "valueadded", "nominalgdp", "realgdp", "percapitagdp", "consumption", "investment",
            "netexports", "cpi", "inflation", "unemploymentrate", "frictionalunemployment",
            "structuralunemployment", "cyclicalunemployment", "businesscycle", "potentialgdp",
            "outputgap", "aggregatedemand", "aggregatesupply", "demandshock", "supplyshock", "stagflation",
            "expansionaryfiscal", "contractionaryfiscal", "fiscalmultiplier", "mpc", "automaticstabilizer",
            "productivity", "longrungrowth");

        static readonly HashSet<string> financeIds = Set(
            "money", "mediumofexchange", "unitofaccount", "storeofvalue", "commercialbank",
            "bankbalancesheet", "bankdeposit", "loan", "reserves", "reserveratio", "creditcreation",
            "depositmoney", "moneysupply", "moneymultiplier", "credit", "interestrate", "nominalinterest",
            "realinterest", "riskpremium", "interestsaving", "interestconsumption", "interestinvestment",
            "centralbank", "policyrate", "expansionarymonetary", "contractionarymonetary",
            "monetarytransmission", "policylag", "inflationtarget", "inflationexpectations", "liquidity",
            "bankrun", "baddebt", "bankcapital", "lenderoflastresort", "financialstability", "creditcycle",
            "financialaccelerator", "governmentbond", "debtinterest");

        // V0.7 第七篇世界市场:intl.* 前缀的 40 张国际卡。
        static readonly HashSet<string> internationalIds = Set(
            "intl.trade", "intl.absoluteadvantage", "intl.comparativeadvantage", "intl.specialization",
            "intl.gainsfromtrade", "intl.exports", "intl.imports", "intl.netexports2",
            "intl.tradesurplus", "intl.tradedeficit", "intl.worldprice", "intl.tariffcard",
            "intl.quota", "intl.protectionism", "intl.tariffrevenue", "intl.tariffdwl",
            "intl.exchangerate", "intl.nominalfx", "intl.appreciation", "intl.depreciation",
            "intl.realfx", "intl.fxmarket", "intl.fxdemand", "intl.ratedifferential",
            "intl.capitalinflow", "intl.capitaloutflow", "intl.currentaccount", "intl.financialaccount",
            "intl.bop", "intl.importedinflation", "intl.fxpassthrough", "intl.jcurve",
            "intl.demandshockfx", "intl.supplychain", "intl.tradewar", "intl.reserves2",
            "intl.fxintervention", "intl.fixedrate", "intl.floatingrate", "intl.trilemma");

        // V0.9.5 第八篇资产与杠杆:housing.*/asset.* 前缀的 21 张卡(分类「资产」)。
        static readonly HashSet<string> housingIds = Set(
            "housing.downpayment", "housing.installment", "housing.annuity", "housing.ltv",
            "housing.collateral", "housing.amortization", "housing.rentVsBuy", "housing.priceBreakdown",
            "housing.supplyElasticity", "housing.capRate", "housing.wealthEffect", "housing.nominalReal",
            "housing.bubbleThree", "housing.minsky", "housing.deleveraging",
            "asset.allocation", "asset.riskReturn", "asset.liquidity", "asset.indexFund",
            "asset.transactionCost", "asset.treasury");

        static readonly HashSet<string> extensionIds = Set(
            "nashequilibrium", "indifferencecurve", "optimalbundle", "taxelasticity", "deadweightloss",
            "governmentfailure", "fiscalmultiplier", "automaticstabilizer", "outputgap", "stagflation",
            "moneymultiplier", "financialaccelerator", "creditcycle");

        static readonly HashSet<string> advancedIds = Set(
            "elasticity", "crosselasticity", "marginalcost", "marginalproduct", "profitmax", "gametheory",
            "prisonersdilemma", "consumersurplus", "producersurplus", "budgetline", "incomeeffect",
            "substitutioneffect", "taxincidence", "priceceiling", "pricefloor", "minimumwage",
            "nominalgdp", "realgdp", "potentialgdp", "aggregatedemand", "aggregatesupply", "bankbalancesheet",
            "creditcreation", "realinterest", "riskpremium", "monetarytransmission", "policylag", "bankrun",
            "bankcapital", "lenderoflastresort");

        static readonly Dictionary<string, KnowledgeMetadata> metadata = new Dictionary<string, KnowledgeMetadata>();

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.knowledge == null) state.knowledge = new KnowledgeProgressState();
            if (state.knowledge.understoodCardIds == null) state.knowledge.understoodCardIds = new List<string>();
            if (state.knowledge.experimentedCardIds == null) state.knowledge.experimentedCardIds = new List<string>();
            if (state.knowledge.explanationLogIds == null) state.knowledge.explanationLogIds = new List<string>();
            if (state.knowledge.cases == null) state.knowledge.cases = new List<KnowledgeCaseRecord>();
            EnsureMetadata();
        }

        public static KnowledgeMetadata GetMetadata(string cardId)
        {
            EnsureMetadata();
            KnowledgeMetadata result;
            return !string.IsNullOrEmpty(cardId) && metadata.TryGetValue(cardId, out result) ? result : null;
        }

        public static List<CardDef> Search(GameState state, string query, string categoryFilter)
        {
            Ensure(state);
            string needle = string.IsNullOrWhiteSpace(query) ? "" : query.Trim();
            List<CardDef> result = new List<CardDef>();
            for (int i = 0; i < CardLibrary.All.Count; i++)
            {
                CardDef card = CardLibrary.All[i];
                KnowledgeMetadata meta = GetMetadata(card.id);
                if (!MatchesCategory(meta, categoryFilter)) continue;
                if (needle.Length > 0 && !Contains(card.id, needle) && !Contains(card.title, needle) &&
                    !Contains(card.body, needle) && !Contains(card.formula, needle) &&
                    !Contains(meta.keywords, needle) && !Contains(CategoryName(meta.category), needle)) continue;
                result.Add(card);
            }
            return result;
        }

        public static KnowledgeStatus Status(GameState state, string cardId)
        {
            Ensure(state);
            if (state.knowledge.experimentedCardIds.Contains(cardId)) return KnowledgeStatus.Experimented;
            if (state.knowledge.understoodCardIds.Contains(cardId)) return KnowledgeStatus.Understood;
            if (state.unlockedCards.Contains(cardId)) return KnowledgeStatus.Discovered;
            return KnowledgeStatus.Undiscovered;
        }

        public static void MarkUnderstood(GameState state, string cardId)
        {
            if (state == null || CardLibrary.Get(cardId) == null) return;
            Ensure(state);
            state.UnlockCard(cardId);
            AddUnique(state.knowledge.understoodCardIds, cardId);
            AddUnique(state.knowledge.explanationLogIds, cardId);
            state.Changed();
        }

        public static void MarkExperimented(GameState state, params string[] cardIds)
        {
            if (state == null || cardIds == null) return;
            Ensure(state);
            for (int i = 0; i < cardIds.Length; i++)
            {
                if (CardLibrary.Get(cardIds[i]) == null) continue;
                state.UnlockCard(cardIds[i]);
                AddUnique(state.knowledge.understoodCardIds, cardIds[i]);
                AddUnique(state.knowledge.experimentedCardIds, cardIds[i]);
                AddUnique(state.knowledge.explanationLogIds, cardIds[i]);
            }
            state.Changed();
        }

        public static KnowledgeCaseRecord GetCase(GameState state, string cardId)
        {
            Ensure(state);
            if (state == null) return null;
            for (int i = state.knowledge.cases.Count - 1; i >= 0; i--)
                if (state.knowledge.cases[i].cardId == cardId) return state.knowledge.cases[i];
            return null;
        }

        public static void SyncPlayerCases(GameState state)
        {
            if (state == null) return;
            Ensure(state);
            DayRecord last = state.LastRecord;
            if (last != null)
            {
                PutCase(state, "profit", last.day, "售价 ¥" + last.price.ToString("0.##") + "，销量 " + last.sold +
                    " 杯，总收入 ¥" + last.revenue.ToString("0") + "，利润 ¥" + last.profit.ToString("0") + "。");
                PutCase(state, "demand", last.day, "售价 ¥" + last.price.ToString("0.##") + "，期望需求 " + last.desiredDemand +
                    "，实际售出 " + last.sold + " 杯。");
                if (last.competitionActive)
                    PutCase(state, "marketshare", last.day, "你的市场份额 " + (last.playerMarketShare * 100f).ToString("0.0") +
                        "%；Sunny Tea 为 " + (last.competitorMarketShare * 100f).ToString("0.0") + "%。");
                if (last.salesTaxPerUnit > 0f)
                    PutCase(state, "taxincidence", last.day, "单位税 ¥" + last.salesTaxPerUnit.ToString("0.##") +
                        "；消费者承担 ¥" + last.consumerTaxBurden.ToString("0.##") + "，企业承担 ¥" + last.producerTaxBurden.ToString("0.##") + "。");
            }
            if (state.history != null && state.history.Count >= 2)
            {
                DayRecord b = state.history[state.history.Count - 1];
                DayRecord a = null;
                for (int i = state.history.Count - 2; i >= 0; i--)
                    if (Mathf.Abs(state.history[i].price - b.price) > .01f) { a = state.history[i]; break; }
                if (a != null)
                {
                    float elasticity = DaySimulator.Elasticity(a, b);
                    if (!float.IsNaN(elasticity) && !float.IsInfinity(elasticity))
                        PutCase(state, "elasticity", b.day, "Day " + a.day + " → " + b.day + "：价格 ¥" + a.price.ToString("0.##") +
                            " → ¥" + b.price.ToString("0.##") + "，需求 " + a.desiredDemand + " → " + b.desiredDemand +
                            "，弧弹性 " + elasticity.ToString("0.00") + "。");
                }
            }
            if (state.resident != null && state.resident.history != null && state.resident.history.Count > 0)
            {
                ResidentDayRecord resident = state.resident.history[state.resident.history.Count - 1];
                PutCase(state, "utility", resident.day, "当日消费 ¥" + resident.spending.ToString("0") +
                    "，总效用 " + resident.totalUtility.ToString("0.0") + "，幸福 " + resident.happinessEnd.ToString("0") + "。");
                PutCase(state, "saving", resident.day, "当日收入 ¥" + resident.income.ToString("0") +
                    "，消费 ¥" + resident.spending.ToString("0") + "，储蓄 ¥" + resident.saved.ToString("0") + "。");
            }
            MacroPeriodRecord macro = state.macro != null && state.macro.history != null && state.macro.history.Count > 0
                ? state.macro.history[state.macro.history.Count - 1] : null;
            if (macro != null)
            {
                PutCase(state, "gdp", state.day, "名义 GDP " + macro.nominalGdp.ToString("0") + "，实际 GDP " +
                    macro.realGdp.ToString("0") + "，增长 " + macro.gdpGrowth.ToString("0.0") + "%。");
                PutCase(state, "inflation", state.day, "CPI " + macro.cpi.ToString("0.0") + "，通胀 " + macro.inflation.ToString("0.0") + "%。");
                PutCase(state, "unemploymentrate", state.day, "劳动力 " + macro.laborForce + "，失业 " + macro.unemployment +
                    "，失业率 " + macro.unemploymentRate.ToString("0.0") + "%。");
                PutCase(state, "outputgap", state.day, "实际 GDP " + macro.realGdp.ToString("0") + "，潜在 GDP " +
                    macro.potentialGdp.ToString("0") + "，产出缺口 " + macro.outputGap.ToString("0.0") + "%。");
            }
            if (state.bank != null && state.bank.initialized)
            {
                PutCase(state, "bankbalancesheet", state.day, "银行资产 ¥" + state.bank.TotalAssets.ToString("0") +
                    "，负债 ¥" + state.bank.TotalLiabilities.ToString("0") + "，资本 ¥" + state.bank.equity.ToString("0") +
                    "，平衡差额 ¥" + state.bank.BalanceGap.ToString("0.00") + "。");
                PutCase(state, "reserves", state.day, "实际准备金率 " + (state.bank.ActualReserveRatio * 100f).ToString("0.0") +
                    "%；法定要求 " + (state.bank.requiredReserveRatio * 100f).ToString("0.0") + "%；金融压力 " +
                    state.bank.financialStress.ToString("0") + "/100。");
                PutCase(state, "creditcreation", state.day, "贷款余额 ¥" + state.bank.PerformingLoans.ToString("0") +
                    "，存款总额 ¥" + state.bank.TotalDeposits.ToString("0") + "；贷款资产与存款负债双边入账。");
            }
            if (state.centralBank != null && state.centralBank.initialized)
            {
                PutCase(state, "policyrate", state.day, "政策利率 " + state.centralBank.policyRate.ToString("0.00") +
                    "%；存款利率 " + state.bank.depositRate.ToString("0.00") + "%；企业贷款利率 " +
                    state.bank.companyLoanRate.ToString("0.00") + "%。");
                PutCase(state, "monetarytransmission", state.day, InterestRateService.TransmissionStageText(state));
                PutCase(state, "realinterest", state.day, "名义存款利率 " + state.bank.depositRate.ToString("0.00") +
                    "% - 通胀 " + (state.macro != null ? state.macro.inflation : 0f).ToString("0.00") +
                    "% = 实际存款利率 " + InterestRateService.RealDepositRate(state).ToString("0.00") + "%。");
            }

            // —— V0.7 国际贸易:案例数据全部来自真实账本行 ——
            if (state.worldTrade != null && state.worldTrade.exchangeRate.initialized)
            {
                TradeDayRecord trade = InternationalTradeService.LastRecord(state);
                if (trade != null)
                {
                    PutCase(state, "intl.netexports2", state.day, "本日出口 ¥" + trade.exportsTotal.ToString("0") +
                        " − 进口 ¥" + trade.importsTotal.ToString("0") + " = NX ¥" + trade.netExports.ToString("0") +
                        "（占名义 GDP " + (state.macro != null && state.macro.nominalGdp > 1f
                            ? (Mathf.Abs(trade.netExports) / state.macro.nominalGdp * 100f).ToString("0.0") + "%"
                            : "—") + "）。");
                    PutCase(state, "intl.exports", state.day, "玩家出货 " + trade.playerExportUnits.ToString("0") +
                        " 杯、收入 ¥" + trade.playerExportRevenue.ToString("0") + "；镇集体商行出口 ¥" +
                        trade.townExportRevenue.ToString("0") + "。");
                    PutCase(state, "intl.imports", state.day, "居民进口消费 ¥" + trade.consumerImportSpend.ToString("0") +
                        "，企业中间投入进口 ¥" + trade.intermediateImportSpend.ToString("0") +
                        "，关税收入 ¥" + trade.tariffRevenue.ToString("0") + "。");
                    if (trade.tariffBase > 0.01f)
                        PutCase(state, "intl.tariffrevenue", state.day, "税率 " +
                            (trade.tariffRateApplied * 100f).ToString("0") + "% × 实际完税进口 ¥" +
                            trade.tariffBase.ToString("0") + " = 关税收 ¥" + trade.tariffRevenue.ToString("0") + "。");
                }
                RateDayRecord rateLast = state.worldTrade.exchangeRate.history.Count > 0
                    ? state.worldTrade.exchangeRate.history[state.worldTrade.exchangeRate.history.Count - 1] : null;
                if (rateLast != null)
                {
                    float move = rateLast.tradeFlowEffect + rateLast.capitalFlowEffect +
                        rateLast.expectationEffect + rateLast.interventionEffect + rateLast.anchorPullEffect;
                    PutCase(state, "intl.exchangerate", state.day, "今日 1 B币 = " + rateLast.rate.ToString("0.00") +
                        " A币（变动 ≈" + move.ToString("+0.00;-0.00") + "%）。");
                    PutCase(state, "intl.fxdemand", rateLast.worldDay, "分解：贸易流 " + rateLast.tradeFlowEffect.ToString("+0.00;-0.00") +
                        " / 资本流 " + rateLast.capitalFlowEffect.ToString("+0.00;-0.00") +
                        " / 预期 " + rateLast.expectationEffect.ToString("+0.00;-0.00") +
                        " / 干预 " + rateLast.interventionEffect.ToString("+0.00;-0.00") +
                        " / 锚回归 " + rateLast.anchorPullEffect.ToString("+0.00;-0.00") + "。");
                }
                if (state.macro != null)
                    PutCase(state, "intl.importedinflation", state.day, "本期汇率对 CPI 的贡献 " +
                        state.macro.exchangeRateInflationContribution.ToString("+0.00;-0.00") +
                        " 个百分点；进口价格指数 " + ExchangeRateService.ImportPressureIndex(state).ToString("0") +
                        "（锚=100）。");
                if (state.centralBank.forexReservesBCurrency > 0f)
                    PutCase(state, "intl.reserves2", state.day, "外汇储备 " +
                        state.centralBank.forexReservesBCurrency.ToString("0") + " B币；累计干预 " +
                        state.centralBank.cumulativeInterventionBCurrency.ToString("0") + " B币、" +
                        state.centralBank.interventionDays + " 天。");
            }
        }

        public static string UnlockHint(GameState state, string cardId)
        {
            if (cardId == "crosselasticity") return "完成两组不同竞争价格的营业观测。";
            if (cardId == "marginalproduct" || cardId == "diminishing") return "在企业篇雇佣员工并比较新增产量。";
            if (cardId == "gametheory" || cardId == "nashequilibrium") return "完成价格战选择，并在策略实验室比较至少两种策略。";
            if (cardId == "publicgood" || cardId == "freerider") return "进入政府篇并完成一次公共品筹资实验。";
            if (cardId == "priceceiling" || cardId == "pricefloor") return "在市政府调整价格管制，观察短缺或过剩。";
            if (cardId == "fiscalmultiplier" || cardId == "mpc") return "在宏观篇执行财政实验并观察政策时滞。";
            if (cardId == "housing.bubbleThree" || cardId == "housing.minsky" || cardId == "housing.deleveraging")
                return "经历一次完整的楼市相位（狂热→反转→下行），并查看泡沫仪表。";
            if (housingIds.Contains(cardId)) return "进入「居民生活」的我的住房与资产配置，完成一次对应的真实决策并查看读数。";
            KnowledgeMetadata meta = GetMetadata(cardId);
            return "进入“" + CategoryName(meta != null ? meta.category : KnowledgeCategory.Enterprise) +
                "”相关篇章，做出一次对应经济决策并查看结果。";
        }

        public static string CategoryName(KnowledgeCategory category)
        {
            switch (category)
            {
                case KnowledgeCategory.Consumer: return "消费者";
                case KnowledgeCategory.Market: return "市场";
                case KnowledgeCategory.Government: return "政府";
                case KnowledgeCategory.Macro: return "宏观";
                case KnowledgeCategory.Finance: return "金融";
                case KnowledgeCategory.International: return "国际";
                case KnowledgeCategory.Housing: return "资产";
                default: return "企业";
            }
        }

        public static string LevelName(KnowledgeLevel level)
        {
            return level == KnowledgeLevel.Extension ? "拓展" : level == KnowledgeLevel.Advanced ? "进阶" : "基础";
        }

        public static string StatusName(KnowledgeStatus status)
        {
            switch (status)
            {
                case KnowledgeStatus.Experimented: return "已完成实验";
                case KnowledgeStatus.Understood: return "已理解";
                case KnowledgeStatus.Discovered: return "已发现";
                default: return "未发现";
            }
        }

        static void EnsureMetadata()
        {
            for (int i = 0; i < CardLibrary.All.Count; i++)
            {
                CardDef card = CardLibrary.All[i];
                if (metadata.ContainsKey(card.id)) continue;
                metadata[card.id] = new KnowledgeMetadata
                {
                    cardId = card.id,
                    category = CategoryFor(card.id),
                    level = extensionIds.Contains(card.id) ? KnowledgeLevel.Extension : advancedIds.Contains(card.id)
                        ? KnowledgeLevel.Advanced : KnowledgeLevel.Basic,
                    experimentKey = ExperimentFor(card.id),
                    keywords = card.title + " " + card.id
                };
            }
        }

        static KnowledgeCategory CategoryFor(string id)
        {
            if (internationalIds.Contains(id)) return KnowledgeCategory.International;
            if (housingIds.Contains(id)) return KnowledgeCategory.Housing;
            if (consumerIds.Contains(id)) return KnowledgeCategory.Consumer;
            if (governmentIds.Contains(id)) return KnowledgeCategory.Government;
            if (macroIds.Contains(id)) return KnowledgeCategory.Macro;
            if (financeIds.Contains(id)) return KnowledgeCategory.Finance;
            if (marketIds.Contains(id)) return KnowledgeCategory.Market;
            return KnowledgeCategory.Enterprise;
        }

        static string ExperimentFor(string id)
        {
            if (id == "demand" || id == "elasticity" || id == "equilibrium") return "demand";
            if (id == "fixedcost" || id == "variablecost" || id == "averagecost" || id == "marginalcost" || id == "profit" || id == "profitmax") return "cost";
            if (consumerIds.Contains(id)) return "resident";
            if (id == "perfectcompetition" || id == "oligopoly" || id == "monopoly" || id == "gametheory" || id == "nashequilibrium" || id == "pricewar") return "market";
            if (id == "publicgood" || id == "freerider") return "publicgood";
            if (governmentIds.Contains(id)) return "government";
            if (macroIds.Contains(id)) return "macro";
            if (financeIds.Contains(id)) return "finance";
            if (internationalIds.Contains(id)) return "world";
            return null;
        }

        static bool MatchesCategory(KnowledgeMetadata meta, string filter)
        {
            if (meta == null || string.IsNullOrEmpty(filter) || filter == "全部") return true;
            if (filter == "实验") return !string.IsNullOrEmpty(meta.experimentKey);
            return CategoryName(meta.category) == filter;
        }

        static bool Contains(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static HashSet<string> Set(params string[] ids) { return new HashSet<string>(ids); }

        static void AddUnique(List<string> list, string value)
        {
            if (!list.Contains(value)) list.Add(value);
        }

        static void PutCase(GameState state, string cardId, int worldDay, string summary)
        {
            if (CardLibrary.Get(cardId) == null || string.IsNullOrEmpty(summary)) return;
            for (int i = 0; i < state.knowledge.cases.Count; i++)
            {
                if (state.knowledge.cases[i].cardId != cardId) continue;
                state.knowledge.cases[i].worldDay = worldDay;
                state.knowledge.cases[i].summary = summary;
                return;
            }
            state.knowledge.cases.Add(new KnowledgeCaseRecord { cardId = cardId, worldDay = worldDay, summary = summary });
        }
    }
}
