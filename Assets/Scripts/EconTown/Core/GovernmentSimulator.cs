using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.4 政府与公共政策规则。核心规则保持纯数据计算，UI 只负责提交政策和展示结果。
    /// </summary>
    public static partial class GovernmentSimulator
    {
        public const float BaselineHourlyWage = 25f;
        public const float GreenEquipmentCost = 1000f;
        // 债务利息按"每周期"计(政府周期 = 1 游戏日)。数值等于名义年利率 3% 按 52 周折摊,
        // 但每日计息——30 天游戏内债务成本可感。此前叫 AnnualDebtInterestRate 且除以 52,
        // 名义与行为不符(C6):周期是一天而不是一周。
        public const float DebtInterestPerCycle = 0.03f / 52f;
        public const float WelfareEligibilityIncomeLine = 65f;
        public const float AdministrativeSpendingPerCycle = 400f;
        public const float OtherFeesPerCycle = 600f;
        // 基础生活补贴的首批覆盖率:符合者仍按低收入占比统计(128 人),预算先覆盖最困难的 25%(C4)。
        public const float WelfareInitialCoverageRatio = 0.25f;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.government == null) state.government = new GovernmentState();
            GovernmentState government = state.government;
            if (government.policies == null) government.policies = new List<PolicyState>();
            if (government.impacts == null) government.impacts = new List<PolicyImpactRecord>();
            if (government.externalities == null) government.externalities = new List<ExternalityState>();
            if (government.projects == null) government.projects = new List<PublicProjectState>();
            if (government.history == null) government.history = new List<GovernmentCycleRecord>();
            if (government.factory == null) government.factory = new FactoryState();
            if (government.cycle < 1) government.cycle = 1;
            if (government.population <= 0) government.population = 320;

            // 政策/项目/外部性的默认结构只在首次(或读档重建后)补齐一次。
            // PolicyValue 等热路径每次都经过这里,此前每次都全量扫描 10 政策+5 项目+2 外部性,
            // 一次 RollMarket 会产生数百次线性扫描(C2)。
            if (government.structureEnsured) return;

            EnsurePolicy(government, PolicyType.DrinkTax, "饮品单位税", "饮品市场", 0f, 0f, 5f, 1f,
                "消费者支付价与企业实收价开始分离。", "交易量、企业利润和政府税收逐步变化。");
            EnsurePolicy(government, PolicyType.DrinkSubsidy, "健康饮品补贴", "饮品市场", 0f, 0f, 5f, 1f,
                "消费者价格下降，企业每杯收入获得支持。", "销量上升，但政府补贴支出持续增加。");
            EnsurePolicy(government, PolicyType.PollutionTax, "污染税", "小型工厂", 0f, 0f, 6f, 1f,
                "污染型生产的私人成本上升。", "工厂减产或改造，污染与就业同时调整。");
            EnsurePolicy(government, PolicyType.GreenEquipmentSubsidy, "环保设备补贴", "小型工厂", 0f, 0f, 100f, 25f,
                "环保改造的企业自付成本下降。", "设备升级概率提高，长期污染强度下降。");
            EnsurePolicy(government, PolicyType.TrainingSubsidy, "职业培训补贴", "教育培训中心", 0f, 0f, 20f, 5f,
                "居民参加培训的价格下降。", "技能与生产率提高，正外部性逐步显现。");
            EnsurePolicy(government, PolicyType.MinimumWage, "最低时薪", "劳动市场", 20f, 20f, 40f, 5f,
                "低于最低标准的工资被抬高。", "收入、消费、成本与招聘会依利润空间共同调整。");
            EnsurePolicy(government, PolicyType.PriceCeiling, "饮品价格上限", "饮品市场", 0f, 0f, 30f, 5f,
                "高于上限的消费者价格被压低。", "若上限低于均衡价，需求增加而供给收缩，可能短缺。");
            EnsurePolicy(government, PolicyType.PriceFloor, "饮品价格下限", "饮品市场", 0f, 0f, 30f, 5f,
                "低于下限的市场价格被抬高。", "若下限高于均衡价，需求下降而供给增加，可能过剩。");
            EnsurePolicy(government, PolicyType.BasicWelfare, "基础生活补贴", "低收入居民", 0f, 0f, 100f, 25f,
                "低收入家庭可支配资金增加。", "消费与幸福改善，但财政支出和债务压力可能上升。");
            EnsurePolicy(government, PolicyType.PublicServiceBudget, "公共服务预算", "全体居民", 200f, 0f, 1000f, 100f,
                "维护与公共服务投入增加。", "公共服务、幸福与商业吸引力逐步改善。");
            EnsurePolicy(government, PolicyType.ImportTariff, "进口关税率", "进口消费品", 0f, 0f, 25f, 1f,
                "进口商品到岸价上升，消费转向国产。", "进口量收缩、国内同类企业受益，但消费者福利受损并出现无谓损失。");

            EnsureProject(government, PublicProjectType.StreetLights, "小镇路灯", 1000f, 60f, 5f, 3f, 1f);
            EnsureProject(government, PublicProjectType.Park, "公共公园", 1800f, 100f, 7f, 10f, 3f);
            EnsureProject(government, PublicProjectType.BusStop, "公交站", 2200f, 150f, 8f, 5f, 5f);
            EnsureProject(government, PublicProjectType.School, "社区学校", 3600f, 260f, 12f, 7f, 4f);
            EnsureProject(government, PublicProjectType.GreenFacility, "环保处理设施", 2800f, 180f, 6f, 4f, 2f);

            EnsureExternality(government, "factory_pollution", "小型工厂", "附近居民", ExternalityType.Negative);
            EnsureExternality(government, "training_spillover", "教育培训中心", "全体企业与居民", ExternalityType.Positive);
            government.structureEnsured = true;
        }

        static void EnsurePolicy(GovernmentState government, PolicyType type, string name, string target,
            float value, float minimum, float maximum, float step, string immediate, string delayed)
        {
            if (FindPolicy(government, type) != null) return;
            government.policies.Add(new PolicyState
            {
                type = type,
                name = name,
                target = target,
                value = value,
                minimum = minimum,
                maximum = maximum,
                step = step,
                active = IsPolicyActive(type, value),
                enactedCycle = 0,
                immediateEffect = immediate,
                delayedEffect = delayed
            });
        }

        static void EnsureProject(GovernmentState government, PublicProjectType type, string name,
            float cost, float maintenance, float service, float happiness, float business)
        {
            for (int i = 0; i < government.projects.Count; i++)
                if (government.projects[i].type == type) return;
            government.projects.Add(new PublicProjectState
            {
                type = type,
                name = name,
                buildCost = cost,
                maintenanceCost = maintenance,
                serviceGain = service,
                happinessGain = happiness,
                businessGain = business
            });
        }

        static void EnsureExternality(GovernmentState government, string id, string source,
            string affectedGroup, ExternalityType type)
        {
            for (int i = 0; i < government.externalities.Count; i++)
                if (government.externalities[i].id == id) return;
            government.externalities.Add(new ExternalityState
            {
                id = id,
                source = source,
                affectedGroup = affectedGroup,
                type = type
            });
        }

        public static PolicyState FindPolicy(GovernmentState government, PolicyType type)
        {
            if (government == null || government.policies == null) return null;
            for (int i = 0; i < government.policies.Count; i++)
                if (government.policies[i].type == type) return government.policies[i];
            return null;
        }

        public static PolicyState FindPolicy(GameState state, PolicyType type)
        {
            Ensure(state);
            return state == null ? null : FindPolicy(state.government, type);
        }

        public static float PolicyValue(GameState state, PolicyType type)
        {
            PolicyState policy = FindPolicy(state, type);
            return policy == null ? 0f : policy.value;
        }

        public static bool SetPolicy(GameState state, PolicyType type, float requestedValue)
        {
            Ensure(state);
            if (state == null) return false;
            PolicyState policy = FindPolicy(state.government, type);
            if (policy == null) return false;
            float value = Mathf.Clamp(requestedValue, policy.minimum, policy.maximum);
            if (policy.step > 0f) value = Mathf.Round(value / policy.step) * policy.step;
            if (!string.IsNullOrEmpty(PolicyConflictReasonForValue(state, type, value))) return false;
            if (Mathf.Approximately(value, policy.value)) return false;

            policy.value = value;
            policy.active = IsPolicyActive(type, value);
            policy.enactedCycle = state.government.cycle;
            policy.ageCycles = 0;
            PolicyImpactRecord impact = CreateImpact(policy, state.government.cycle);
            state.government.impacts.Add(impact);
            if (state.government.impacts.Count > 24) state.government.impacts.RemoveAt(0);
            state.government.lastPolicySummary = policy.name + "调整为 " + FormatPolicyValue(policy);
            state.government.lastNpcFeedback = FeedbackForPolicy(type, value);
            ProgressChapterForPolicy(state, type, value);
            UnlockPolicyCards(state, type, value);
            state.Changed();
            return true;
        }

        public static string PolicyConflictReason(GameState state, PolicyType type, float requestedValue)
        {
            Ensure(state);
            if (state == null) return "政府状态尚未初始化";
            PolicyState policy = FindPolicy(state.government, type);
            if (policy == null) return "未找到该政策";
            float value = Mathf.Clamp(requestedValue, policy.minimum, policy.maximum);
            if (policy.step > 0f) value = Mathf.Round(value / policy.step) * policy.step;
            return PolicyConflictReasonForValue(state, type, value);
        }

        static string PolicyConflictReasonForValue(GameState state, PolicyType type, float value)
        {
            float ceiling = type == PolicyType.PriceCeiling
                ? value : PolicyValue(state, PolicyType.PriceCeiling);
            float floor = type == PolicyType.PriceFloor
                ? value : PolicyValue(state, PolicyType.PriceFloor);
            return ceiling > 0f && floor > 0f && floor > ceiling
                ? "最低允许价格不能高于最高允许价格" : null;
        }

        static bool IsPolicyActive(PolicyType type, float value)
        {
            if (type == PolicyType.MinimumWage) return value > 20f;
            if (type == PolicyType.PublicServiceBudget) return value > 0f;
            return value > 0f;
        }

        static PolicyImpactRecord CreateImpact(PolicyState policy, int cycle)
        {
            PolicyImpactRecord impact = new PolicyImpactRecord
            {
                type = policy.type,
                policyName = policy.name,
                cycleEnacted = cycle,
                currentStage = 1,
                immediateEffect = policy.immediateEffect,
                delayedEffect = policy.delayedEffect,
                winners = WinnersFor(policy.type),
                losers = LosersFor(policy.type),
                governmentEffect = GovernmentEffectFor(policy.type),
                causalChain = CausalChainFor(policy.type),
                observedSummary = "第一阶段 · 政策已生效，正在观察市场反应。"
            };
            return impact;
        }

        public static float ConsumerTaxShare(GameState state)
        {
            return PriceService.Quote(state, state == null ? Eco.BasePrice : state.price).consumerTaxShare;
        }

        public static float DrinkConsumerPrice(GameState state, float shelfPrice)
        {
            return PriceService.Quote(state, shelfPrice).consumerPrice;
        }

        public static float DrinkProducerPrice(GameState state, float shelfPrice)
        {
            return PriceService.Quote(state, shelfPrice).producerNetPrice;
        }

        public static float EffectiveDailyWage(GameState state)
        {
            return LaborMarketService.EffectiveDailyWage(state);
        }

        public static float ResidentIncomeMultiplier(GameState state)
        {
            float minimumWage = PolicyValue(state, PolicyType.MinimumWage);
            return Mathf.Max(1f, minimumWage / BaselineHourlyWage);
        }

        public static int ApplySupplyResponse(GameState state, int plannedProduction, float shelfPrice)
        {
            if (state == null || plannedProduction <= 0) return Mathf.Max(0, plannedProduction);
            float producerPrice = DrinkProducerPrice(state, shelfPrice);
            float priceSignal = producerPrice / Mathf.Max(1f, shelfPrice);
            float wageSignal = EffectiveDailyWage(state) / Eco.WagePerEmployee;
            float factor = Mathf.Clamp(priceSignal / Mathf.Sqrt(wageSignal), 0.62f, 1.12f);
            return Mathf.Max(0, Mathf.RoundToInt(plannedProduction * factor));
        }

        public static void RecordDrinkSettlement(GameState state, DayRecord record, int sold, int baselineQuantity)
        {
            Ensure(state);
            if (state == null || record == null) return;
            GovernmentState government = state.government;
            // 同日防重:企业记录有"一天一条"兜底,税/补贴基也必须同样防双计——
            // 否则异常路径的双结算会出现记录去重了、政府账本却记两次(C8)。
            // 围猎五#3复审:防重只拦「挂账+全镇均值」这两类状态腿(重复跑会双计/双重 Lerp);
            // 当日记录的展示/教学字段无条件写——冻结窗口读档重放的新记录不该是全 0 的政策盲区。
            bool alreadyRecordedToday = government.lastDrinkTaxRecordedDay == state.day;
            government.lastDrinkTaxRecordedDay = state.day;
            float tax = PolicyValue(state, PolicyType.DrinkTax);
            float subsidy = PolicyValue(state, PolicyType.DrinkSubsidy);
            PriceQuote quote = PriceService.Quote(state, record.price);
            float consumerShare = quote.consumerTaxShare;
            float consumerPrice = quote.consumerPrice;
            float producerPrice = quote.producerNetPrice;
            float taxRevenue = sold * tax;
            float subsidyCost = sold * subsidy;
            int quantityLost = Mathf.Max(0, baselineQuantity - sold);
            int quantityAdded = Mathf.Max(0, sold - baselineQuantity);
            float deadweightLoss = 0.5f * tax * quantityLost + 0.5f * subsidy * quantityAdded;

            System.Action writeRecordFields = delegate
            {
                record.consumerPrice = consumerPrice;
                record.producerNetPrice = producerPrice;
                record.salesTaxPerUnit = tax;
                record.salesSubsidyPerUnit = subsidy;
                record.salesTaxRevenue = taxRevenue;
                record.subsidyCost = subsidyCost;
                record.consumerTaxBurden = tax * consumerShare;
                record.producerTaxBurden = tax * (1f - consumerShare);
                record.deadweightLoss = deadweightLoss;
                record.policyBaselineQuantity = baselineQuantity;
                record.priceCeiling = PolicyValue(state, PolicyType.PriceCeiling);
                record.priceFloor = PolicyValue(state, PolicyType.PriceFloor);
                record.priceCeilingBinding = quote.priceCeilingBinding;
                record.priceFloorBinding = quote.priceFloorBinding;
                record.policyShortage = quote.priceCeilingBinding
                    ? Mathf.Max(0, record.demandTotal - baselineQuantity) + Mathf.Max(0, record.planned - record.produced)
                    : 0;
                record.policySurplus = quote.priceFloorBinding
                    ? Mathf.Max(0, record.availableSupply - record.demandTotal)
                    : 0;
            };
            writeRecordFields();
            if (alreadyRecordedToday) return;   // 状态腿(挂账/均值)同日只跑一次

            government.pendingDrinkTaxRevenue += taxRevenue;
            government.pendingSubsidySpending += subsidyCost;
            float averagePriceBeforeEvent = government.averagePrice - government.eventAveragePriceOffset;
            government.averagePrice = Mathf.Lerp(averagePriceBeforeEvent, consumerPrice, 0.35f) +
                government.eventAveragePriceOffset;
            government.averageEnterpriseProfit = Mathf.Lerp(government.averageEnterpriseProfit, record.profit, 0.22f);
        }

        /// <summary>记录同一市场中其他企业的应税销量；企业报告只展示本店税额，政府账本汇总全市场。</summary>
        public static void RecordAdditionalDrinkSales(GameState state, int sold)
        {
            Ensure(state);
            if (state == null || sold <= 0) return;
            // 同日防重,与 RecordDrinkSettlement 的标记相互独立(竞争日两者都要各记一次)。
            if (state.government.lastAdditionalSalesDay == state.day) return;
            state.government.lastAdditionalSalesDay = state.day;
            state.government.pendingDrinkTaxRevenue += sold * PolicyValue(state, PolicyType.DrinkTax);
            state.government.pendingSubsidySpending += sold * PolicyValue(state, PolicyType.DrinkSubsidy);
        }

        public static bool RunFreeRiderExperiment(GameState state)
        {
            Ensure(state);
            if (state == null || state.government.freeRiderExperimentDone) return false;
            GovernmentState government = state.government;
            government.freeRiderExperimentDone = true;
            government.freeRiderContributors = 6;
            government.freeRiderFundsRaised = 300f;
            government.lastPolicySummary = "20 位居民中只有 6 位愿意出资，共募集 ¥300，路灯项目未能开工。";
            government.lastNpcFeedback = "大家都想要路灯，但不少人希望由别人出钱。";
            UnlockChapter(state, 2);
            state.UnlockCard("publicgood");
            state.UnlockCard("freerider");
            state.UnlockCard("marketfailure");
            state.Changed();
            return true;
        }

        public static bool BuildProject(GameState state, PublicProjectType type)
        {
            Ensure(state);
            if (state == null) return false;
            GovernmentState government = state.government;
            PublicProjectState project = null;
            for (int i = 0; i < government.projects.Count; i++)
                if (government.projects[i].type == type) { project = government.projects[i]; break; }
            // bug搜#5:现金只在收日经 pendingCapitalSpending 结算一次——原先此处先即时扣一笔,
            // 晚间 AdvanceCycle 的 balance 又含同一笔资本支出(cash += balance),同项目被扣两次:
            // ¥1000 路灯实耗 ¥2000、周期账本只记一次(对照 ResolveEvent 的事件治理支出即纯挂账)。
            // 守卫把当日已挂账资本支出计入占用,顶替原即时扣款防同日连建超发现金的隐性职责。
            if (project == null || project.built ||
                government.cash < project.buildCost + government.pendingCapitalSpending) return false;
            government.spending += project.buildCost;
            government.pendingCapitalSpending += project.buildCost;
            project.built = true;
            project.builtCycle = government.cycle;
            government.publicServiceLevel = Mathf.Clamp(government.publicServiceLevel + project.serviceGain, 0f, 100f);
            // 即时可见的当期反馈 + 持久 offset:只做前者会被下期 UpdateCityMetrics 的公式整体
            // 重算覆盖,高昂造价换来的加成只活一周期(P2-3)。
            government.projectHappinessOffset += project.happinessGain;
            government.projectEnterpriseConfidenceOffset += project.businessGain;
            government.residentHappiness = Mathf.Clamp(government.residentHappiness + project.happinessGain, 0f, 100f);
            government.enterpriseConfidence = Mathf.Clamp(government.enterpriseConfidence + project.businessGain, 0f, 100f);
            if (type == PublicProjectType.GreenFacility)
            {
                government.environmentQuality = Mathf.Clamp(government.environmentQuality + 8f, 0f, 100f);
                state.UnlockCard("negativeexternality");
            }
            else if (type == PublicProjectType.School)
            {
                government.projectAverageWageOffset += 1f;
                government.averageWage += 1f;
                state.UnlockCard("positiveexternality");
            }
            government.lastPolicySummary = "政府使用公共资金建设了「" + project.name + "」，所有居民都可使用。";
            state.UnlockCard("governmentspending");
            state.UnlockCard("publicgood");
            state.Changed();
            return true;
        }

    }
}
