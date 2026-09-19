using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>长期事件、新闻、危机与数值护栏。只操作显式保存的世界状态。</summary>
    public static class LongTermExperienceService
    {
        class EventDefinition
        {
            public string id;
            public NewsCategory category;
            public string title;
            public int cooldown;
            public float weight;
            public string viewTarget;
            public Func<GameState, bool> condition;
            public Func<GameState, string> cause;
            public Func<GameState, string> apply;
        }

        static readonly EventDefinition[] events =
        {
            Event("enterprise.raw", NewsCategory.Enterprise, "水果供应趋紧", 8, 1f, "enterprise",
                s => s.materialPriceMul < 1.15f,
                s => "上游供给偏紧，当前原料成本指数为 " + s.macro.importPriceIndex.ToString("0"),
                s => { s.materialPriceMul *= 1.22f; return "今日原料单位成本上升 22%。"; }),
            Event("enterprise.viral", NewsCategory.Enterprise, "本地博主推荐果汁店", 10, .8f, "enterprise",
                s => s.playerBrand < 82f,
                s => "品牌仍在成长，当前品牌值 " + s.playerBrand.ToString("0"),
                s => { s.demandMul *= 1.18f; s.playerBrand = Mathf.Min(100f, s.playerBrand + 2f); return "今日需求提高 18%，品牌 +2。"; }),
            Event("enterprise.labour", NewsCategory.Enterprise, "熟练员工短缺", 12, .7f, "enterprise",
                s => s.macro.unemploymentRate < 5.5f && s.employees >= 2,
                s => "失业率仅 " + s.macro.unemploymentRate.ToString("0.0") + "% ，招聘市场偏紧",
                s => { s.materialPriceMul *= 1.08f; return "用工与交付成本今日提高约 8%。"; }),

            Event("resident.bonus", NewsCategory.Resident, "居民收到绩效奖金", 11, .8f, "resident",
                s => s.resident.profileCreated && s.resident.employedToday,
                s => "居民仍在就业，劳动收入渠道正常",
                s => { s.resident.cash += 120f; return "居民现金 +¥120，可选择消费或储蓄。"; }),
            Event("resident.training", NewsCategory.Resident, "公益培训名额开放", 10, .7f, "resident",
                s => s.resident.profileCreated && (s.resident.unemployedDays > 0 || s.resident.skillLevel < 2f),
                s => s.resident.unemployedDays > 0 ? "居民已连续失业 " + s.resident.unemployedDays + " 天" : "居民技能仍有提升空间",
                s => { s.resident.skillLevel += .35f; return "居民技能 +0.35，后续就业机会改善。"; }),
            Event("resident.expense", NewsCategory.Resident, "家庭临时维修支出", 9, .6f, "resident",
                s => s.resident.profileCreated && s.resident.cash > 80f,
                s => "家庭设施需要维护，当前现金能够承担部分支出",
                s => { s.resident.cash = Mathf.Max(0f, s.resident.cash - 45f); return "居民现金 -¥45。"; }),

            Event("government.environment", NewsCategory.Government, "河畔环境抗议", 14, 1f, "government",
                s => s.government.environmentQuality < 58f,
                s => "环境质量已降至 " + s.government.environmentQuality.ToString("0"),
                s => { s.government.approvalRating -= 3f; return "支持率 -3；污染治理成为今日待办。"; }),
            Event("government.lowincome", NewsCategory.Government, "低收入群体生活压力上升", 12, .9f, "government",
                s => s.government.lowestGroupPurchasingPower < 52f,
                s => "最低收入组购买力仅 " + s.government.lowestGroupPurchasingPower.ToString("0"),
                s => { s.government.residentSupport -= 2f; return "居民支持 -2；福利与物价政策压力增加。"; }),
            Event("government.fiscal", NewsCategory.Government, "公共设施维护账单到期", 15, .7f, "government",
                s => s.macro.debtToGdp > 45f || s.government.cash > 2000f,
                s => "设施持续使用，政府需支付维护成本",
                s =>
                {
                    // 事件在收日后触发：记入下期明确支出，避免绕过政府账本直接改现金。
                    s.government.pendingCapitalSpending += 420f;
                    return "下期公共设施维护支出 +¥420。";
                }),

            Event("macro.external", NewsCategory.Macro, "海外需求走弱", 14, .9f, "macro",
                s => s.macro.pendingShock == MacroShockType.None && s.macro.externalDemandIndex > 55f,
                s => "企业信心为 " + s.macro.businessConfidence.ToString("0") + "，外需存在下行风险",
                s => { s.macro.pendingShock = MacroShockType.GlobalDemandFall; return "下一期外需下降冲击已进入经济系统。"; }),
            Event("macro.consumption", NewsCategory.Macro, "消费信心升温", 12, .8f, "macro",
                s => s.macro.pendingShock == MacroShockType.None && s.macro.consumerConfidence > 68f,
                s => "消费者信心达到 " + s.macro.consumerConfidence.ToString("0"),
                s => { s.macro.pendingShock = MacroShockType.ConsumptionBoom; return "下一期消费需求冲击已进入经济系统。"; }),
            Event("macro.technology", NewsCategory.Macro, "生产技术取得突破", 18, .55f, "macro",
                s => s.macro.pendingShock == MacroShockType.None && s.macro.productivity < 180f,
                s => "资本与培训积累为技术应用创造了条件",
                s => { s.macro.pendingShock = MacroShockType.AiRevolution; s.macro.productivity += 1.5f; return "生产率 +1.5，下一期观察产能与就业结构。"; }),
            Event("macro.energy", NewsCategory.Macro, "国际能源价格冲击", 20, .5f, "macro",
                s => s.macro.pendingShock == MacroShockType.None && s.macro.energyCostIndex < 145f,
                s => "进口投入占企业成本，能源价格变化会沿供应链传导",
                s => { s.macro.pendingShock = MacroShockType.EnergyCrisis; return "下一期能源供给冲击已进入经济系统。"; }),

            // —— V0.7 世界市场(新闻归因全部引用真实账本数据;viewTarget 先挂宏观,世界经济面板落地后切换) ——
            Event("trade.warcho", NewsCategory.Macro, "B 国考虑上调对小镇产品关税", 16, .8f, "macro",
                s => InternationalTradeService.LastRecord(s) != null &&
                     !s.worldTrade.pendingTradeWarChoice && s.progression.completedChapter >= 43,
                s => "当前 B 国对 A 产品关税为 " + (s.worldTrade.countryB.tariffOnAExports * 100f).ToString("0") + "%",
                s =>
                {
                    s.worldTrade.countryB.tariffOnAExports =
                        Mathf.Clamp(s.worldTrade.countryB.tariffOnAExports + .08f, 0f, .6f);
                    s.worldTrade.pendingTradeWarChoice = true;
                    return "B 国关税升至 " + (s.worldTrade.countryB.tariffOnAExports * 100f).ToString("0") +
                           "%，出口竞争力承压；是否回应，需要在贸易中心做出选择。";
                }),
            Event("trade.reservelow", NewsCategory.International, "外汇储备逼近安全线", 12, .9f, "macro",
                s => s.centralBank.forexReservesBCurrency <
                     s.centralBank.forexReserveFloor + 160f &&
                     s.worldTrade.exchangeRate.regime == ExchangeRegime.Fixed,
                s => "外储仅剩 " + s.centralBank.forexReservesBCurrency.ToString("0") +
                     " B币（底线 " + s.centralBank.forexReserveFloor.ToString("0") + "）",
                s => "守住固定汇率的弹药越来越少：继续干预可能失守。"),
            Event("trade.fxwave", NewsCategory.International, "汇率单日波动放大", 10, .7f, "macro",
                s => s.worldTrade.exchangeRate.history.Count >= 2 &&
                     Mathf.Abs((s.worldTrade.exchangeRate.history[^1].rate -
                                s.worldTrade.exchangeRate.history[^2].rate) /
                               Mathf.Max(.01f, s.worldTrade.exchangeRate.history[^2].rate)) >= .004f,
                s => "本日 1 B币 = " + ExchangeRateService.Rate(s).ToString("0.00") +
                     " A币，资本与贸易流正在重新定价",
                s => "进出口商与资本市场都在消化新的汇率水平。"),
            Event("trade.bulkorder", NewsCategory.International, "港口出现海外大订单", 14, .6f, "macro",
                s => s.macro.externalDemandIndex > 78f,
                s => "B 国居民收入指数达到 " + s.worldTrade.countryB.incomeLevel.ToString("0"),
                s => { s.worldTrade.countryB.drinkBenchmarkPriceBCurrency *= 1.03f;
                       return "外国基准报价上浮 3%，出口出厂价随之走高。"; }),
            Event("trade.supplycut", NewsCategory.International, "上游芯片出口国限供", 18, .45f, "macro",
                s => s.macro.importPriceIndex < 150f,
                s => "进口价格指数现为 " + s.macro.importPriceIndex.ToString("0"),
                s => { s.macro.importPriceIndex = Mathf.Clamp(s.macro.importPriceIndex + 14f, 80f, 220f);
                       return "中间投入进口成本上行，警惕输入型通胀沿成本链传导。"; }),
            Event("trade.brecession", NewsCategory.International, "B 国经济确认陷入衰退", 22, .85f, "macro",
                s => s.worldTrade.countryB.recessionActive,
                s => "B 国增长动量为 " + s.worldTrade.countryB.growthMomentum.ToString("0.00") +
                     "%，消费者正在捂紧钱包",
                s => "外需收缩已经计入贸易账本：观察本周出口订单与净出口。")
        };

        static EventDefinition Event(string id, NewsCategory category, string title, int cooldown, float weight,
            string viewTarget, Func<GameState, bool> condition, Func<GameState, string> cause,
            Func<GameState, string> apply)
        {
            return new EventDefinition
            {
                id = id, category = category, title = title, cooldown = cooldown, weight = weight,
                viewTarget = viewTarget, condition = condition, cause = cause, apply = apply
            };
        }

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.experience == null) state.experience = new LongTermExperienceState();
            LongTermExperienceState x = state.experience;
            if (x.settings == null) x.settings = new WorldExperienceSettings();
            if (x.portPresentation == null) x.portPresentation = new PortPresentationState();
            if (x.portPresentation.lastArrivalTier < -1 || x.portPresentation.lastArrivalTier > 3)
                x.portPresentation.lastArrivalTier = -1;
            if (x.eventCooldowns == null) x.eventCooldowns = new List<EventCooldownState>();
            if (x.eventHistory == null) x.eventHistory = new List<LongTermEventRecord>();
            if (x.news == null) x.news = new List<EconomicNewsItem>();
            if (x.economicHistory == null) x.economicHistory = new List<EconomicHistoryEntry>();
            if (x.enterpriseRecovery == null) x.enterpriseRecovery = new EnterpriseRecoveryState();
            if (x.enterpriseRecovery.actions == null) x.enterpriseRecovery.actions = new List<string>();
            if (x.crisisReviews == null) x.crisisReviews = new List<CrisisReviewRecord>();
            // V0.7.4:旧档缺失的过场标记与首播档案走声明默认值复活(空表=一部都没看过)。
            if (x.viewedCutscenes == null) x.viewedCutscenes = new List<int>();
            if (x.cutsceneRecords == null) x.cutsceneRecords = new List<CutsceneRecord>();
            // JsonUtility 读取缺少新字段的旧存档时，可能生成一个所有字段都为空的事件对象。
            // 这不是真实事件，否则待办卡和 HUD 会只显示一行空的“原因：”。
            if (x.todayLongTermEvent != null &&
                (string.IsNullOrEmpty(x.todayLongTermEvent.id) || string.IsNullOrEmpty(x.todayLongTermEvent.title)))
                x.todayLongTermEvent = null;
            // 同一坑的另一个入口：JsonUtility 序列化 null 的 class 字段会写成 {}，
            // 读档后 pendingCrisisReview 变成全默认实例、判 null 恒真——
            // 收日/读档补发会弹出一张空白的“危机复盘”面板。按 id 判空清洗。
            if (x.pendingCrisisReview != null && string.IsNullOrEmpty(x.pendingCrisisReview.id))
                x.pendingCrisisReview = null;
            x.news.RemoveAll(item => item == null || string.IsNullOrEmpty(item.headline));
            x.settings.eventFrequency = Mathf.Clamp(x.settings.eventFrequency <= 0f ? 1f : x.settings.eventFrequency, .25f, 2f);
            x.settings.competitionIntensity = Mathf.Clamp(x.settings.competitionIntensity <= 0f ? 1f : x.settings.competitionIntensity, .5f, 1.5f);
            x.settings.marketScale = Mathf.Clamp(x.settings.marketScale <= 0f ? 1f : x.settings.marketScale, .5f, 2f);
            if (string.IsNullOrEmpty(x.settings.numericUnit)) x.settings.numericUnit = "自动";
        }

        public static float TownEventProbability(GameState state)
        {
            Ensure(state);
            float baseChance = state.experience.settings.difficulty == DifficultyMode.Teaching ? .30f :
                state.experience.settings.difficulty == DifficultyMode.Simulation ? .78f : .60f;
            return Mathf.Clamp01(baseChance * state.experience.settings.eventFrequency);
        }

        public static void OnDaySettled(GameState state)
        {
            if (state == null) return;
            Ensure(state);
            UpdateEnterpriseRecovery(state);
            UpdateResidentCrisis(state);
            UpdateFiscalPressure(state);
            UpdateMacroCrisis(state);
            CaptureMilestones(state);
            GenerateMetricNews(state);
            ClampWorld(state);
        }

        public static void OnWorldDayStarted(GameState state)
        {
            if (state == null) return;
            Ensure(state);
            state.experience.todayLongTermEvent = null;
            // 围猎三(#2):第51章「贸易战」死锁修复——pendingTradeWarChoice 的唯一生产写点是长期事件
            // trade.warcho,而长期事件掷发被 mainlineComplete 旧门(V0.5.2)锁死;第51章的完成信号
            // 又只能由战争横幅记录,三者互为前置,58 章主线/沙盒/自由模式/长期事件全部不可达
            // (自动化测试直接 RecordSignal 注入,测不出)。改为剧情固定置位:推进到第51章且当前
            // 不在抉择中时,清晨直接亮横幅,关税照 trade.warcho 同款 +8% 入账——不掷随机流,
            // 不动三条确定性流的取数顺序。
            if (state.progression != null && state.progression.currentChapter == 51 &&
                state.worldTrade != null && !state.worldTrade.pendingTradeWarChoice)
            {
                state.worldTrade.countryB.tariffOnAExports = Mathf.Clamp(
                    state.worldTrade.countryB.tariffOnAExports + .08f, 0f, .6f);
                state.worldTrade.pendingTradeWarChoice = true;
            }
            if (state.progression != null && state.progression.mainlineComplete) TryTriggerLongTermEvent(state);
            ClampWorld(state);
        }

        public static string TodayEventTitle(GameState state)
        {
            Ensure(state);
            LongTermEventRecord record = state != null ? state.experience.todayLongTermEvent : null;
            return record == null ? null : record.title;
        }

        public static string CombinedEventTitle(GameState state)
        {
            if (state == null) return null;
            string longTerm = TodayEventTitle(state);
            if (string.IsNullOrEmpty(longTerm)) return state.todayEventName;
            return string.IsNullOrEmpty(state.todayEventName) ? longTerm : state.todayEventName + " / " + longTerm;
        }

        public static IList<string> BuildTodayTodos(GameState state)
        {
            List<string> result = new List<string>();
            if (state == null) return result;
            Ensure(state);
            if (state.inventory < Mathf.Max(20, state.planProduction / 4)) result.Add("企业：商品库存偏低，可检查生产计划与原料");
            if (state.experience.enterpriseRecovery.active) result.Add("企业：恢复计划进行中，需要连续 2 日正现金流");
            if (state.resident.profileCreated && state.resident.todayChoices.Count == 0) result.Add("居民：今天还没有安排消费或晚间活动");
            if (state.experience.residentCrisisActive) result.Add("居民：生活危机，可用储蓄、兼职或福利恢复");
            if (state.government.currentEvent != null && !state.government.currentEvent.resolved) result.Add("政府：城市事件等待确认");
            if (state.experience.fiscalPressureActive) result.Add("政府：债务压力较高，新增项目空间受到约束");
            if (state.macro.history.Count > 0) result.Add("宏观：本期经济报告已生成，可查看因果链");
            if (result.Count == 0) result.Add("今日没有紧急事项；可以自由经营、复习或创建实验");
            return result;
        }

        public static void BeginEnterpriseRecovery(GameState state, string planId)
        {
            if (state == null) return;
            Ensure(state);
            EnterpriseRecoveryState recovery = state.experience.enterpriseRecovery;
            // 恢复计划是一次性决策：已进入恢复期后再次调用（如从今日简报重开面板误点）不得重复执行
            // 救援效果——否则"出售一档设备"每次点击白得 ¥520，构成无限刷钱。
            if (recovery.active) return;
            recovery.active = true;
            recovery.startedWorldDay = state.day;
            recovery.cause = BuildEnterpriseCrisisCause(state);
            recovery.positiveCashFlowStreak = 0;
            recovery.actions.Clear();
            recovery.selectedPlan = planId;
            string action;
            if (planId == "liquidate")
            {
                float cash = state.inventory * Mathf.Max(1f, PriceService.PlayerListedPrice(state) * .35f);
                state.inventory = 0;
                state.price = Mathf.Clamp(Mathf.Max(CostService.EffectiveInputUnitCost(state) * 1.2f, state.price * .9f), 1f, 1000f);
                state.money += cash;
                action = "Day " + state.day + "：清库存回笼 ¥" + cash.ToString("0") + "，并把售价调到 ¥" + state.price.ToString("0.0");
            }
            else if (planId == "tighten")
            {
                state.employees = Mathf.Max(1, state.employees - 1);
                state.marketingBudget = 0f;
                state.todayStrategicSpend = 0f;
                state.planProduction = Mathf.Min(state.planProduction, CostService.Capacity(state).actualCapacity);
                action = "Day " + state.day + "：缩编至 " + state.employees + " 人并停止广告和扩张";
            }
            else
            {
                float proceeds = state.playerQualityLevel > 1 ? 900f : 520f;
                state.playerQualityLevel = Mathf.Max(1, state.playerQualityLevel - 1);
                state.money += proceeds;
                action = "Day " + state.day + "：出售设备获得 ¥" + proceeds.ToString("0") + "，品质等级降至 " + state.playerQualityLevel;
            }
            recovery.actions.Add(action);
            recovery.latestStatus = "恢复中：需要连续 2 个结算日现金流为正，且现金高于 ¥-1000。";
            FinancialHealthService.Resolve(state);
            AddHistory(state, "crisis.enterprise." + recovery.startedWorldDay, "企业进入财务危机", recovery.cause);
            state.Changed();
        }

        public static void PauseAndRestartEnterprise(GameState state)
        {
            if (state == null) return;
            Ensure(state);
            EnterpriseRecoveryState recovery = state.experience.enterpriseRecovery;
            int started = recovery.startedWorldDay > 0 ? recovery.startedWorldDay : state.day;
            string cause = string.IsNullOrEmpty(recovery.cause) ? BuildEnterpriseCrisisCause(state) : recovery.cause;
            state.money = 800f;
            state.inventory = 0;
            state.materials = 160;
            state.employees = 1;
            state.planProduction = 80;
            state.marketingBudget = 0f;
            state.todayStrategicSpend = 0f;
            FinancialHealthService.Resolve(state);
            recovery.active = false;
            CrisisReviewRecord review = new CrisisReviewRecord
            {
                id = "enterprise.restart." + state.day, kind = "企业重整", startedWorldDay = started,
                recoveredWorldDay = state.day, cause = cause,
                recovery = "暂停原企业并以基础设备、1 名员工和 ¥800 周转金重整。",
                result = "居民、政府和宏观进度保留；企业经营可以重新开始。"
            };
            state.experience.crisisReviews.Add(review);
            state.experience.pendingCrisisReview = review;
            state.Changed();
        }

        public static void ApplyResidentRecovery(GameState state, string action)
        {
            if (state == null) return;
            Ensure(state);
            ResidentState resident = state.resident;
            if (action == "gig") { resident.cash += 35f; resident.energy = Mathf.Max(0f, resident.energy - 12f); }
            else if (action == "savings")
            {
                // 围猎三复审:危机动用储蓄同应急取款一样钳在偿付能力内(reserves 无非负钳位)。
                float amount = Mathf.Min(Mathf.Min(120f, resident.savings),
                    Mathf.Max(0f, state.bank.householdDeposits), Mathf.Max(0f, state.bank.reserves));
                resident.savings -= amount;
                resident.cash += amount;
                // 围猎三复审(cross-pipeline):累计进当日应急取款计数器——读档重放的
                // savings += emergencyWithdrawal 退回腿据此覆盖本渠道,不再销毁金额。
                resident.todayEmergencyWithdrawal += amount;
                // 围猎三(#22):危机恢复的「动用储蓄」同应急取款一样走双边记账,保住
                // resident.savings ≡ bank.householdDeposits 镜像(修复轮回A 的口径)。
                state.bank.householdDeposits -= amount;
                state.bank.reserves -= amount;
            }
            else if (action == "welfare")
            {
                resident.cash += 80f;
                // 政府侧走 pendingSubsidySpending 记账（周期结算时进 cash += balance），
                // 不能直扣 cash——那会绕过政府账本，破坏"现金变化==结余+债务融资"恒等式。
                state.government.pendingSubsidySpending += 80f;
            }
            else
            {
                // 危机判定线是 satiety < 25：恢复值必须越过该线，否则"最低消费"永远无法解除危机。
                resident.satiety = Mathf.Max(resident.satiety, 26f);
                resident.happiness = Mathf.Max(resident.happiness, 30f);
            }
            UpdateResidentCrisis(state);
            state.Changed();
        }

        public static string DifficultyName(DifficultyMode mode)
        {
            return mode == DifficultyMode.Teaching ? "教学" : mode == DifficultyMode.Simulation ? "模拟" : "标准";
        }

        public static string GuidanceName(GuidanceLevel level)
        {
            return level == GuidanceLevel.Detailed ? "详细" : level == GuidanceLevel.Concise ? "简洁" : "标准";
        }

        static void TryTriggerLongTermEvent(GameState state)
        {
            if (state.day - state.experience.lastLongTermEventWorldDay < 2) return;
            float probability = TownEventProbability(state) * .62f;
            if (state.economy.random.Next01() >= probability) return;
            List<EventDefinition> eligible = new List<EventDefinition>();
            float totalWeight = 0f;
            for (int i = 0; i < events.Length; i++)
            {
                EventDefinition definition = events[i];
                if (NextEligibleDay(state, definition.id) > state.day || !definition.condition(state)) continue;
                eligible.Add(definition);
                totalWeight += definition.weight;
            }
            if (eligible.Count == 0) return;
            float roll = state.economy.random.Next01() * totalWeight;
            EventDefinition chosen = eligible[eligible.Count - 1];
            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= eligible[i].weight;
                if (roll <= 0f) { chosen = eligible[i]; break; }
            }
            string cause = chosen.cause(state);
            string effect = chosen.apply(state);
            LongTermEventRecord record = new LongTermEventRecord
            {
                id = chosen.id, worldDay = state.day, category = chosen.category, title = chosen.title,
                cause = cause, effect = effect, viewTarget = chosen.viewTarget
            };
            state.experience.todayLongTermEvent = record;
            state.experience.eventHistory.Add(record);
            if (state.experience.eventHistory.Count > 120) state.experience.eventHistory.RemoveAt(0);
            state.experience.totalLongTermEvents++;
            state.experience.lastLongTermEventWorldDay = state.day;
            SetCooldown(state, chosen.id, state.day + chosen.cooldown);
            AddNews(state, "event." + chosen.id + "." + state.day, chosen.category, chosen.title, effect, cause, chosen.viewTarget, false);
            AddHistory(state, "event." + chosen.id + "." + state.day, chosen.title, effect);
        }

        static void UpdateEnterpriseRecovery(GameState state)
        {
            EnterpriseRecoveryState recovery = state.experience.enterpriseRecovery;
            if (!recovery.active) return;
            DayRecord record = state.LastRecord;
            // 同一结算日只计一次（读档重放日防双记），与政府/宏观/FinancialHealth 的同日幂等护栏对齐。
            if (record != null && record.day == recovery.positiveCashFlowStreakLastDay) return;
            if (record != null && record.day == state.day && record.profit > 0f && state.money > -1000f)
                recovery.positiveCashFlowStreak++;
            else recovery.positiveCashFlowStreak = 0;
            recovery.positiveCashFlowStreakLastDay = record != null ? record.day : 0;
            FinancialHealthService.Resolve(state);
            recovery.latestStatus = "恢复进度 " + recovery.positiveCashFlowStreak + "/2 个正现金流日；当前资金 ¥" + state.money.ToString("0") + "。";
            if (recovery.positiveCashFlowStreak < 2) return;
            recovery.active = false;
            string actions = recovery.actions.Count == 0 ? "通过日常经营调整恢复" : string.Join("；", recovery.actions.ToArray());
            CrisisReviewRecord review = new CrisisReviewRecord
            {
                id = "enterprise.recovered." + state.day, kind = "企业财务危机",
                startedWorldDay = recovery.startedWorldDay, recoveredWorldDay = state.day,
                cause = recovery.cause, recovery = actions,
                result = "连续 2 日现金流为正，资金恢复至 ¥" + state.money.ToString("0") + "。"
            };
            state.experience.crisisReviews.Add(review);
            state.experience.pendingCrisisReview = review;
            AddHistory(state, review.id, "企业走出财务危机", review.result);
        }

        static void UpdateResidentCrisis(GameState state)
        {
            ResidentState resident = state.resident;
            bool wasActive = state.experience.residentCrisisActive;
            state.experience.residentCrisisActive = resident.profileCreated && resident.cash <= 0f &&
                resident.satiety < 25f && (!resident.employedToday || resident.unemployedDays > 0);
            if (!wasActive && state.experience.residentCrisisActive)
            {
                AddNews(state, "crisis.resident." + state.day, NewsCategory.Resident, "居民进入生活危机",
                    "现金、基本生活和就业同时承压，可通过兼职、储蓄或福利恢复。",
                    "现金为 ¥" + resident.cash.ToString("0") + "，饱腹 " + resident.satiety.ToString("0") + "，失业 " + resident.unemployedDays + " 天",
                    "resident", true);
            }
            else if (wasActive && !state.experience.residentCrisisActive)
                AddHistory(state, "resident.recovered." + state.day, "居民生活恢复", "现金或基本生活状态已经脱离危机线。 ");
        }

        static void UpdateFiscalPressure(GameState state)
        {
            bool wasActive = state.experience.fiscalPressureActive;
            // 围猎三(#40):进入/退出加迟滞带——进入线 ≥80(或债务绝对额>1000万),退出须债务/GDP
            // <65 且债务额回落。旧实现无迟滞:79.5↔80.5 波动时每日重刷「进入财政压力」新闻
            // (日号 id 绕过新闻去重),且 65-80 区间退出既不保持状态也永不写「缓解」历史。
            bool pressure = state.macro.debtToGdp >= 80f || state.government.debt > 10000000f;
            bool relieved = state.macro.debtToGdp < 65f && state.government.debt <= 10000000f;
            state.experience.fiscalPressureActive = relieved ? false : (wasActive || pressure);
            if (state.experience.fiscalPressureActive)
            {
                state.government.enterpriseConfidence = Mathf.Max(0f, state.government.enterpriseConfidence - .25f);
                state.government.residentSupport = Mathf.Max(0f, state.government.residentSupport - .2f);
                if (!wasActive) AddNews(state, "crisis.fiscal." + state.day, NewsCategory.Government, "政府进入财政压力状态",
                    "利息与既有项目占用更多预算空间，企业和居民信心承压。",
                    "债务 / GDP 达到 " + state.macro.debtToGdp.ToString("0.0") + "%", "government", true);
            }
            else if (wasActive && state.macro.debtToGdp < 65f)
                AddHistory(state, "fiscal.recovered." + state.day, "财政压力缓解", "债务 / GDP 回落至 " + state.macro.debtToGdp.ToString("0.0") + "% 。");
        }

        static void UpdateMacroCrisis(GameState state)
        {
            MacroPeriodRecord previous = state.macro.history.Count >= 2 ? state.macro.history[state.macro.history.Count - 2] : null;
            bool falling = state.macro.gdpGrowth < 0f && previous != null && state.macro.unemploymentRate >= previous.unemploymentRate;
            if (falling) { state.experience.recessionStreak++; state.experience.recoveryStreak = 0; }
            else if (state.macro.gdpGrowth > 0f)
            {
                state.experience.recoveryStreak++;
                state.experience.recessionStreak = 0;
            }
            if (state.experience.recessionStreak >= 4) state.experience.macroCrisisStatus = "深度衰退";
            else if (state.experience.recessionStreak >= 2) state.experience.macroCrisisStatus = "衰退";
            else if (state.experience.recoveryStreak >= 2) state.experience.macroCrisisStatus = "复苏";
            else state.experience.macroCrisisStatus = MacroEconomySimulator.CycleName(state.macro.cyclePhase);
            if (state.experience.recessionStreak == 2)
                AddNews(state, "crisis.recession." + state.day, NewsCategory.Macro, "小镇经济确认进入衰退",
                    "连续实际 GDP 下降且失业上升；街上行人明显变少了。恢复需要需求企稳、停止裁员并带动消费。",
                    "本期增长 " + state.macro.gdpGrowth.ToString("0.0") + "% ，失业 " + state.macro.unemploymentRate.ToString("0.0") + "%", "macro", true);
            if (state.experience.recoveryStreak == 2)
                AddHistory(state, "macro.recovery." + state.day, "经济进入复苏",
                    "实际 GDP 连续增长，街头又热闹起来——但失业率仍达 " + state.macro.unemploymentRate.ToString("0.0") +
                    "% ，就业恢复往往滞后于增长：人多起来不等于人人有工作。 ");
        }

        static void CaptureMilestones(GameState state)
        {
            if (state.CompetitionActive) AddHistoryOnce(state, "market.competitor", "Sunny Tea 开业", "饮品市场从单店进入企业竞争。 ");
            if (state.priceWarRound > 0) AddHistoryOnce(state, "market.pricewar", "第一次价格战", "玩家需要在价格、品质与品牌之间选择。 ");
            if (state.progression != null && state.progression.mainlineComplete)
                AddHistoryOnce(state, "mainline.complete", "进入自由经济时代", "八篇主线完成，世界市场、沙盒、挑战和长期事件开放。 ");
        }

        static void GenerateMetricNews(GameState state)
        {
            // V0.7.1 过热对冲文案(PRD §126):街景热闹 ≠ 单纯的好消息——第 29 章「该降温了」的画面侧伏笔
            if (state.macro.cyclePhase == MacroCyclePhase.Overheating)
                AddNews(state, "metric.overheat", NewsCategory.Macro,
                    "小镇经济偏热",
                    "街上人头攒动，但物价也在抬头——热闹到极点时，降温就在倒计时。产出已超潜在水平，该考虑给需求降温了。",
                    "产出缺口 " + (state.macro.potentialGdp > 0f ? ((state.macro.realGdp - state.macro.potentialGdp) / state.macro.potentialGdp * 100f).ToString("0.0") : "0.0") +
                    "% ，周期相位 " + MacroEconomySimulator.CycleName(state.macro.cyclePhase), "macro", false);
            if (state.macro.unemploymentRate >= 8f)
                AddNews(state, "metric.unemployment", NewsCategory.Macro,
                    "小镇失业率升至 " + state.macro.unemploymentRate.ToString("0.0") + "%",
                    "就业市场承压，可下钻查看摩擦性、结构性与周期性失业。",
                    "实际劳动力数据已达到新闻阈值", "macro", false);
            if (Mathf.Abs(state.macro.inflation) >= 5f)
                AddNews(state, "metric.inflation", NewsCategory.Macro,
                    "通胀达到 " + state.macro.inflation.ToString("0.0") + "%",
                    "食品、能源、娱乐与饮品共同形成 CPI，需要区分需求拉动和成本推动。",
                    "实际 CPI 记录超过 5% 阈值", "macro", false);
            if (state.LastRecord != null && state.LastRecord.profit < 0f)
                AddNews(state, "metric.loss", NewsCategory.Enterprise, "果汁店本日出现亏损",
                    "打开经营日报可查看销量、价格和成本的真实归因。",
                    "本日利润为 ¥" + state.LastRecord.profit.ToString("0"), "enterprise", false);

            // —— V0.7 贸易指标新闻(全部从真实账本行取数,P0 归因验收) ——
            TradeDayRecord trade = InternationalTradeService.LastRecord(state);
            if (trade != null && Mathf.Abs(trade.netExports) >= 300f)
            {
                bool surplus = trade.netExports > 0f;
                AddNews(state, surplus ? "metric.tradesurplus" : "metric.tradedeficit",
                    NewsCategory.International,
                    surplus ? "小镇录得贸易顺差" : "小镇录得贸易逆差",
                    surplus
                        ? "出口 ¥" + trade.exportsTotal.ToString("0") + " > 进口 ¥" + trade.importsTotal.ToString("0") +
                          "。顺差意味着本国在对外放贷：资源真的被别国用走了吗？"
                        : "进口 ¥" + trade.importsTotal.ToString("0") + " > 出口 ¥" + trade.exportsTotal.ToString("0") +
                          "。若进口的是机器与能源，逆差也可能是在为未来产能投资。",
                    "本期 NX = ¥" + trade.netExports.ToString("0") +
                    "（汇率 " + trade.exchangeRateUsed.ToString("0.00") + "）", "macro", false);
            }
            CentralBankState central = state.centralBank;
            if (central.forexReservesBCurrency < central.forexReserveFloor + 150f)
                AddNews(state, "metric.reserveslow", NewsCategory.International,
                    "外汇储备进入预警区间",
                    "外储是守住汇率的弹药；固定汇率制度下每一次干预都在消耗它。",
                    "当前储备 " + central.forexReservesBCurrency.ToString("0") + " B币 · 底线 " +
                    central.forexReserveFloor.ToString("0"), "macro", false);
        }

        static string BuildEnterpriseCrisisCause(GameState state)
        {
            DayRecord last = state.LastRecord;
            return "资金连续 " + FinancialHealthService.CrisisDays + " 日低于 ¥" + FinancialHealthService.CrisisCashThreshold.ToString("0") +
                (last == null ? "。" : "；最近一日利润 ¥" + last.profit.ToString("0") + "，库存 " + state.inventory + "，员工 " + state.employees + "。 ");
        }

        static void AddNews(GameState state, string id, NewsCategory category, string headline, string detail,
            string cause, string viewTarget, bool always)
        {
            if (!always)
                for (int i = 0; i < state.experience.news.Count; i++)
                    if (state.experience.news[i].id == id && state.day - state.experience.news[i].worldDay < 7) return;
            state.experience.news.Insert(0, new EconomicNewsItem
            {
                id = id, worldDay = state.day, category = category, headline = headline,
                detail = detail, cause = cause, viewTarget = viewTarget
            });
            if (state.experience.news.Count > 80) state.experience.news.RemoveAt(state.experience.news.Count - 1);
        }

        static void AddHistoryOnce(GameState state, string id, string title, string summary)
        {
            for (int i = 0; i < state.experience.economicHistory.Count; i++)
                if (state.experience.economicHistory[i].id == id) return;
            AddHistory(state, id, title, summary);
        }

        static void AddHistory(GameState state, string id, string title, string summary)
        {
            state.experience.economicHistory.Add(new EconomicHistoryEntry
            {
                id = id, worldDay = state.day, title = title, summary = summary
            });
            if (state.experience.economicHistory.Count > 160) state.experience.economicHistory.RemoveAt(0);
        }

        static int NextEligibleDay(GameState state, string id)
        {
            for (int i = 0; i < state.experience.eventCooldowns.Count; i++)
                if (state.experience.eventCooldowns[i].eventId == id)
                    return state.experience.eventCooldowns[i].nextEligibleWorldDay;
            return 0;
        }

        static void SetCooldown(GameState state, string id, int nextDay)
        {
            for (int i = 0; i < state.experience.eventCooldowns.Count; i++)
                if (state.experience.eventCooldowns[i].eventId == id)
                {
                    state.experience.eventCooldowns[i].nextEligibleWorldDay = nextDay;
                    return;
                }
            state.experience.eventCooldowns.Add(new EventCooldownState { eventId = id, nextEligibleWorldDay = nextDay });
        }

        public static void ClampWorld(GameState state)
        {
            if (state == null) return;
            state.price = Safe(state.price, 15f, 1f, 1000f);
            state.money = Safe(state.money, 0f, -1000000f, 100000000f);
            state.materials = Mathf.Clamp(state.materials, 0, 1000000);
            state.inventory = Mathf.Clamp(state.inventory, 0, 1000000);
            state.employees = Mathf.Clamp(state.employees, 0, Eco.MaxEmployees);
            state.playerBrand = Safe(state.playerBrand, 30f, 0f, 100f);
            if (state.competitor != null)
            {
                state.competitor.price = Safe(state.competitor.price, 18f, 1f, 1000f);
                state.competitor.brand = Safe(state.competitor.brand, 55f, 0f, 100f);
                state.competitor.quality = Safe(state.competitor.quality, 72f, 0f, 100f);
                state.competitor.satisfaction = Safe(state.competitor.satisfaction, 76f, 0f, 100f);
                state.competitor.inventory = Mathf.Clamp(state.competitor.inventory, 0, 1000000);
                state.competitor.marketShare = Safe(state.competitor.marketShare, .5f, 0f, 1f);
            }
            if (state.resident != null)
            {
                state.resident.cash = Safe(state.resident.cash, 0f, -100000f, 100000000f);
                state.resident.savings = Safe(state.resident.savings, 0f, 0f, 100000000f);
                state.resident.energy = Safe(state.resident.energy, 50f, 0f, 100f);
                state.resident.happiness = Safe(state.resident.happiness, 50f, 0f, 100f);
                state.resident.satiety = Safe(state.resident.satiety, 50f, 0f, 100f);
            }
            if (state.government != null)
            {
                state.government.population = Mathf.Clamp(state.government.population, 1, 10000000);
                state.government.cash = Safe(state.government.cash, 0f, -100000000f, 100000000f);
                state.government.debt = Safe(state.government.debt, 0f, 0f, 100000000f);
                state.government.approvalRating = Safe(state.government.approvalRating, 50f, 0f, 100f);
                state.government.enterpriseConfidence = Safe(state.government.enterpriseConfidence, 50f, 0f, 100f);
                state.government.residentHappiness = Safe(state.government.residentHappiness, 50f, 0f, 100f);
                state.government.environmentQuality = Safe(state.government.environmentQuality, 50f, 0f, 100f);
            }
            if (state.macro != null)
            {
                state.macro.realGdp = Safe(state.macro.realGdp, 100f, 1f, 100000000f);
                state.macro.nominalGdp = Safe(state.macro.nominalGdp, state.macro.realGdp, 1f, 100000000f);
                state.macro.potentialGdp = Safe(state.macro.potentialGdp, state.macro.realGdp, 1f, 100000000f);
                state.macro.cpi = Safe(state.macro.cpi, 100f, 20f, 1000f);
                state.macro.inflation = Safe(state.macro.inflation, 0f, -30f, 100f);
                state.macro.unemploymentRate = Safe(state.macro.unemploymentRate, 8f, 0f, 100f);
                state.macro.debtToGdp = Safe(state.macro.debtToGdp, 0f, 0f, 10000f);
                state.macro.consumerConfidence = Safe(state.macro.consumerConfidence, 50f, 0f, 100f);
                state.macro.businessConfidence = Safe(state.macro.businessConfidence, 50f, 0f, 100f);
                state.macro.productivity = Safe(state.macro.productivity, 100f, 10f, 10000f);
                state.macro.capitalStock = Safe(state.macro.capitalStock, 100f, 1f, 100000000f);
                state.macro.energyCostIndex = Safe(state.macro.energyCostIndex, 100f, 20f, 1000f);
                state.macro.externalDemandIndex = Safe(state.macro.externalDemandIndex, 100f, 10f, 1000f);
                state.macro.importPriceIndex = Safe(state.macro.importPriceIndex, 100f, 20f, 1000f);
            }
        }

        static float Safe(float value, float fallback, float minimum, float maximum)
        {
            return float.IsFinite(value) ? Mathf.Clamp(value, minimum, maximum) : Mathf.Clamp(fallback, minimum, maximum);
        }
    }
}
