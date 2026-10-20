using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.4《小镇大事记》过场判定(PRD §147–153)。纯逻辑无画面:
    /// 触发条件逐一镜像各宿主弹窗/结算现场的判定(§148),叠加 viewedCutscenes 一次性标记
    /// (§137 队列+标记双保险中的标记侧;过场在宿主弹窗之前当场播放,不走次晨排队);
    /// 文案引用当档真实账本数字,禁止通用模板句(§151);绝不调用 SaveKeyMoment 充当标记(§150)。
    /// </summary>
    public static class CutsceneService
    {
        public const int Count = 11;  // V0.9.5 增第六段;V0.9.17 增五段篇间晋升过场(7–11)

        /// <summary>大事记页签的编年展示序(V0.9.17):枚举 int 已随存档落盘不能按时间线重排,
        /// id 序(1–6 旧段在前)与真实时间线序错位,展示层按本数组排——
        /// 开业(6章)→价格战(9)→搬进(11晨)→危机(前期)→聘书(17晨)→钥匙(23晨)→委任状(31晨)
        /// →贸易战(42后)→狂热(后期)→房契(54晨)→收官(58)。</summary>
        public static readonly int[] ChronicleOrder = { 1, 2, 7, 3, 8, 9, 10, 4, 6, 11, 5 };

        /// <summary>篇末阶段报告 part → 晋升过场 id(V0.9.17):0=该篇无晋升过场(Housing 由 Finale 收弧)。</summary>
        public static int PromotionCutsceneFor(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Market: return (int)CutsceneId.MoveInTown;
                case LearningPart.Resident: return (int)CutsceneId.CouncilAppointment;
                case LearningPart.Government: return (int)CutsceneId.BureauHandover;
                case LearningPart.Macro: return (int)CutsceneId.BankMandate;
                case LearningPart.Trade: return (int)CutsceneId.Homecoming;
                default: return 0;
            }
        }

        // ————— 展示名与档案 —————

        public static string Title(int id)
        {
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening: return "开业剪彩";
                case (int)CutsceneId.PriceWarErupts: return "价格战爆发";
                case (int)CutsceneId.CrisisClouds: return "乌云压城";
                case (int)CutsceneId.TradeWarHeadline: return "贸易战头版";
                case (int)CutsceneId.Finale: return "八篇完成";
                case (int)CutsceneId.HousingMania: return "楼市狂热头版";
                case (int)CutsceneId.MoveInTown: return "搬进小镇";
                case (int)CutsceneId.CouncilAppointment: return "镇议会的聘书";
                case (int)CutsceneId.BureauHandover: return "从小镇到整个经济";
                case (int)CutsceneId.BankMandate: return "老行长的委任状";
                case (int)CutsceneId.Homecoming: return "一纸房契";
                default: return "";
            }
        }

        /// <summary>首播当日的真实数字摘要,写入 CutsceneRecord 供大事记回看引用(§151)。</summary>
        public static string Caption(GameState state, int id)
        {
            if (state == null) return "";
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                    return "Sunny Tea 的开业定价是 ¥" + state.competitor.price.ToString("0");
                case (int)CutsceneId.PriceWarErupts:
                    return "对手把价格降到了 ¥" + state.competitor.price.ToString("0");
                case (int)CutsceneId.CrisisClouds:
                    return "现金连续 " + Mathf.Max(3, state.economy.financialDistressDays) +
                           " 天低于 ¥" + FinancialHealthService.CrisisCashThreshold.ToString("0");
                case (int)CutsceneId.TradeWarHeadline:
                    return "B 国关税升至 " +
                           (state.worldTrade.countryB.tariffOnAExports * 100f).ToString("0") + "%";
                case (int)CutsceneId.Finale:
                    return "第 " + state.economy.worldDay + " 世界日 · 名义 GDP " +
                           (state.macro != null && state.macro.history.Count > 0
                               ? CivicBooks.Money(state.macro.history[state.macro.history.Count - 1].nominalGdp)
                               : CivicBooks.Money(TownGrowthService.GdpBaselineYuan));
                case (int)CutsceneId.HousingMania:
                    return "镇房价年化 " + HousingService.AnnualPriceGrowth(state).ToString("+0;-0") +
                           "% · 泡沫压力 " + (state.housing != null ? state.housing.bubblePressure : 0f).ToString("0") + "/100";
                case (int)CutsceneId.MoveInTown:
                    return "双店竞争后平均份额 " + (state.AverageMarketShare * 100f).ToString("0.0") +
                           "% · 开业至今第 " + state.economy.worldDay + " 天";
                case (int)CutsceneId.CouncilAppointment:
                    return "小店累计净利 ¥" + state.TotalProfit.ToString("0") +
                           " · 居民幸福 " + (state.resident != null ? state.resident.happiness : 0f).ToString("0");
                case (int)CutsceneId.BureauHandover:
                    return "财政余额 " + CivicBooks.Signed(state.government.budgetBalance) + " · 名义 GDP " +
                           (state.macro != null && state.macro.history.Count > 0
                               ? CivicBooks.Money(state.macro.history[state.macro.history.Count - 1].nominalGdp)
                               : CivicBooks.Money(TownGrowthService.GdpBaselineYuan));
                case (int)CutsceneId.BankMandate:
                    return "存款 ¥" + state.bank.TotalDeposits.ToString("0") +
                           " · 贷款 ¥" + state.bank.PerformingLoans.ToString("0") +
                           " · 政策利率 " + (state.centralBank != null ? state.centralBank.policyRate : 3f).ToString("0.00") + "%";
                case (int)CutsceneId.Homecoming:
                {
                    float exports = 0f;
                    List<TradeDayRecord> tradeHistory = state.worldTrade != null ? state.worldTrade.history : null;
                    for (int i = 0; tradeHistory != null && i < tradeHistory.Count; i++)
                        if (tradeHistory[i] != null) exports += tradeHistory[i].exportsTotal;
                    return "累计出口 " + CivicBooks.Money(exports) +
                           " · 居民存款 ¥" + (state.resident != null ? state.resident.savings.ToString("0") : "0");
                }
                default: return "";
            }
        }

        /// <summary>过场打字机正文(2–4 行,因果教学口径,引用当档数字)。index=0 的行先于 index=1。</summary>
        public static string[] Lines(GameState state, int id)
        {
            string caption = Caption(state, id);
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                    return new[]
                    {
                        "镇东的铺子揭下幕布——Sunny Tea 今天开业。",
                        caption + "。",
                        "从今天起,顾客会在两家店之间比较价格、品质与品牌。",
                        "你不再只问「卖多少钱最赚钱」,还要问:对手会怎么回应?"
                    };
                case (int)CutsceneId.PriceWarErupts:
                    return new[]
                    {
                        "《镇民日报》头版:Sunny Tea 突然降价!",
                        caption + "。",
                        "降价能抢走你的顾客,也会压薄它自己的利润。",
                        "回应的方式不止一种:价格、品质、品牌,都是筹码。"
                    };
                case (int)CutsceneId.CrisisClouds:
                    return new[]
                    {
                        "乌云压城——小镇报刊出你的店门口的告示。",
                        caption + ",企业财务危机成立。",
                        "危机不是一天发生的:连续亏损的账,每天都在警告。",
                        "处理危机的三条路,去「处理财务危机」面板做出选择。"
                    };
                case (int)CutsceneId.TradeWarHeadline:
                    return new[]
                    {
                        "B 国《海关公报》头版:上调对小镇产品的关税!",
                        caption + ",出口竞争力承压。",
                        "关税是国与国之间的价格干预:它保护 B 国生产者,代价由两国消费者分担。",
                        "是否回应、如何回应,去国际贸易港《世界经济》页做出选择。"
                    };
                case (int)CutsceneId.Finale:
                    return new[]
                    {
                        "港口全景:小镇的产品正从这里驶向世界。",
                        "汇率的稳定、利率的独立、资本的自由——三元悖论里只能同时抓住两个。",
                        caption + "。",
                        "八篇完成。屋檐与杠杆也讲完了,你的经济实验才刚刚开始。"
                    };
                case (int)CutsceneId.HousingMania:
                    return new[]
                    {
                        "《镇民日报》头版:镇房价又创新高,售楼处门口排起了长队!",
                        caption + "——租金却没跟上。",
                        "价格脱离租金、涨速陡峭、信贷扩张:泡沫三征齐备,崩盘只差一根针。",
                        "去「我的住房」看泡沫仪表,算算你的月供扛得住多大的回撤。"
                    };
                case (int)CutsceneId.MoveInTown:
                    return new[]
                    {
                        "天黑收摊。你把店门锁好,走向广场东侧那间亮着灯的屋子。",
                        caption + "。",
                        "从明天起,你不只是这条街的店主——也是这座小镇的居民。",
                        "回家创建你的居民档案:用消费者的眼睛,再看一遍这个经济。"
                    };
                case (int)CutsceneId.CouncilAppointment:
                    return new[]
                    {
                        "市政厅的灯亮到深夜。长桌尽头,留着一把空椅子。",
                        caption + "——议会想起那个把一条街经营得井井有条的人。",
                        "路灯谁来出钱,路灯的事就交给谁管。清晨,一纸聘书送到你门口。",
                        "去市政府,完成你的第一个公共决策。"
                    };
                case (int)CutsceneId.BureauHandover:
                    return new[]
                    {
                        "你管好了一座镇的钱。今晚,桌上多了一把数据局的钥匙。",
                        caption + "。",
                        "镇上的人开始问更大的问题:这一年,我们整体是更富了,还是只是更贵了?",
                        "宏观仪表盘已经打开:GDP、物价、就业——整个经济,从此是你的报表。"
                    };
                case (int)CutsceneId.BankMandate:
                    return new[]
                    {
                        "银行打烊后,老行长把私人办公室的灯留给了你。",
                        caption + "。",
                        "「钱是什么,你比谁都清楚。」——既存过钱也借过钱,既见过通胀也见过衰退的人,接手这本账。",
                        "从柜台做起。哪天央行的门开了,你就是行长。"
                    };
                case (int)CutsceneId.Homecoming:
                    return new[]
                    {
                        "商船进港,世界的账合上了。你走了这么远——从一杯果汁,到一个汇率。",
                        caption + "。",
                        "今晚回家,门牌下多了一张属于你的房契。",
                        "是时候想想:赚来的钱,该放在哪里?安家吧。"
                    };
                default: return new string[0];
            }
        }

        /// <summary>旧档预填条目的通用文案:不引用当档数字,标注「存档前的记录」(§150)。</summary>
        public static string PrefillSummary(int id)
        {
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                    return "镇东的 Sunny Tea 揭幕开业,小镇从单店进入双店竞争。(存档前的记录)";
                case (int)CutsceneId.PriceWarErupts:
                    return "Sunny Tea 突然降价,价格战爆发,你做出了自己的回应。(存档前的记录)";
                case (int)CutsceneId.CrisisClouds:
                    return "企业现金连续破线,财务危机成立,你带着小店走出了低谷。(存档前的记录)";
                case (int)CutsceneId.TradeWarHeadline:
                    return "B 国上调关税,贸易战头版见报,小镇第一次直面国际市场。(存档前的记录)";
                case (int)CutsceneId.Finale:
                    return "第八篇完成,八篇主线收官,小镇的故事讲完了。(存档前的记录)";
                case (int)CutsceneId.HousingMania:
                    return "镇房价持续上涨,楼市狂热头版见报,小镇第一次直面资产泡沫。(存档前的记录)";
                case (int)CutsceneId.MoveInTown:
                    return "双店之争落定,你在镇上站稳脚跟,搬进小镇开始了居民生活。(存档前的记录)";
                case (int)CutsceneId.CouncilAppointment:
                    return "镇议会送来聘书,你从居民成为公共决策者,接手了小镇的财政。(存档前的记录)";
                case (int)CutsceneId.BureauHandover:
                    return "数据局的钥匙交到你手上,你开始以整个经济的尺度看数字。(存档前的记录)";
                case (int)CutsceneId.BankMandate:
                    return "老行长退任,委任状与银行一起交到你手上,你从柜台走向央行。(存档前的记录)";
                case (int)CutsceneId.Homecoming:
                    return "世界的账结清,一纸房契安了家,你开始打理自己的资产。(存档前的记录)";
                default: return "";
            }
        }

        // ————— 触发判定(镜像宿主条件,§148) —————

        /// <summary>宿主弹窗即将出现的瞬间是否该播过场:一次性标记在此闭环(§137 双保险的标记侧)。</summary>
        public static bool ShouldPlay(GameState state, int id)
        {
            if (state == null || IsViewed(state, id)) return false;
            // 旧档读回后 experience/economy 可能尚未 Ensure:这里先补齐,判定必须只读不写账本。
            state.EnsureEconomyState();
            LongTermExperienceService.Ensure(state);
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                {
                    ChapterDefinition current = ProgressionService.Current(state);
                    return current != null && current.number == 6 &&
                           !ProgressionService.HasSignal(state, ProgressSignals.CompetitorOpened);
                }
                case (int)CutsceneId.PriceWarErupts:
                    return ProgressionService.RequiresPriceWarChoice(state);
                case (int)CutsceneId.CrisisClouds:
                    return state.economy.financialCrisisActive &&
                           !state.experience.enterpriseRecovery.active;
                case (int)CutsceneId.TradeWarHeadline:
                    return state.worldTrade != null && state.worldTrade.pendingTradeWarChoice;
                case (int)CutsceneId.HousingMania:
                    // V0.9.5:镇房价首次进入狂热相位(Inflating)的次日清晨;IsViewed 前置保证一次性。
                    return state.housing != null && state.housing.leveragePhase == LeveragePhase.Inflating;
                case (int)CutsceneId.Finale:
                {
                    // V0.9.5:Finale 随第八篇收弧迁至 Housing 报告(与 GameBootstrap 宿主条件镜像,§197.7)。
                    // 修复(找BUG未验证#2):改用纯读窥视 Peek——Get 的悬挂分支会 RemoveAt,判定必须零写。
                    StageReportRecord pending = ProgressionService.PeekPendingStageReport(state);
                    return pending != null && pending.part == LearningPart.Housing;
                }
                case (int)CutsceneId.MoveInTown:
                case (int)CutsceneId.CouncilAppointment:
                case (int)CutsceneId.BureauHandover:
                case (int)CutsceneId.BankMandate:
                case (int)CutsceneId.Homecoming:
                {
                    // V0.9.17 晋升线:镜像宿主(DeliverPendingStageReport 的报告交付瞬间)——
                    // 队首待看报告的 part 与本段映射一致即播;Peek 零写(同 Finale 先例)。
                    // 旧档已看过的报告不在 pending 队列,天然不补播。
                    StageReportRecord pending = ProgressionService.PeekPendingStageReport(state);
                    return pending != null && PromotionCutsceneFor(pending.part) == id;
                }
                default: return false;
            }
        }

        public static bool IsViewed(GameState state, int id)
        {
            return state != null && state.experience != null &&
                   state.experience.viewedCutscenes != null &&
                   state.experience.viewedCutscenes.Contains(id);
        }

        /// <summary>
        /// 过场开始即视为已看(跳过与自然播完口径一致,§149);同时把当档数字记入档案。
        /// 只写 experience 字段,绝不调用 SaveKeyMoment(§150)。
        /// </summary>
        public static void MarkViewed(GameState state, int id)
        {
            if (state == null || state.experience == null) return;
            LongTermExperienceService.Ensure(state);
            if (!state.experience.viewedCutscenes.Contains(id))
                state.experience.viewedCutscenes.Add(id);
            if (FindRecord(state, id) == null)
            {
                CutsceneRecord record = new CutsceneRecord
                {
                    id = id,
                    worldDay = state.economy.worldDay,
                    caption = Caption(state, id)
                };
                state.experience.cutsceneRecords.Add(record);
            }
            state.Changed();
        }

        public static CutsceneRecord FindRecord(GameState state, int id)
        {
            if (state == null || state.experience == null || state.experience.cutsceneRecords == null)
                return null;
            List<CutsceneRecord> records = state.experience.cutsceneRecords;
            for (int i = 0; i < records.Count; i++)
                if (records[i] != null && records[i].id == id) return records[i];
            return null;
        }

        // ————— 大事记页签分级(§150:已看过可回看 / 旧档预填 / 尚未发生) —————

        public enum ChronicleTier { Locked = 0, Prefilled = 1, Viewed = 2 }

        /// <summary>节点是否已在历史中真实发生过(按 completedChapter 与账本历史,不看过场标记)。</summary>
        public static bool Happened(GameState state, int id)
        {
            if (state == null || state.progression == null) return false;
            int cc = state.progression.completedChapter;
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                    return cc >= 6 || ProgressionService.HasSignal(state, ProgressSignals.CompetitorOpened);
                case (int)CutsceneId.PriceWarErupts:
                    return cc >= 9 || state.priceWarActive ||
                           ProgressionService.HasSignal(state, ProgressSignals.PriceWarResolved);
                case (int)CutsceneId.CrisisClouds:
                {
                    if (state.economy != null &&
                        (state.economy.financialCrisisActive || state.economy.financialDistressDays > 0))
                        return true;
                    List<CrisisReviewRecord> reviews = state.experience.crisisReviews;
                    for (int i = 0; reviews != null && i < reviews.Count; i++)
                        if (reviews[i] != null && reviews[i].kind != null &&
                            reviews[i].kind.Contains("企业")) return true;
                    return false;
                }
                case (int)CutsceneId.TradeWarHeadline:
                {
                    if (state.worldTrade != null && state.worldTrade.pendingTradeWarChoice) return true;
                    List<LongTermEventRecord> history = state.experience.eventHistory;
                    for (int i = 0; history != null && i < history.Count; i++)
                        if (history[i] != null && history[i].id == "trade.warcho") return true;
                    return false;
                }
                case (int)CutsceneId.Finale:
                    return cc >= 58;   // V0.9.5:Finale 随第八篇收弧迁至 58(§197.7)
                case (int)CutsceneId.MoveInTown:
                    return cc >= 10;   // V0.9.17 复审#2:篇末章(10/16/22/30/53)完成即已跨过转换点——
                case (int)CutsceneId.CouncilAppointment:  // 停在篇界且报告已看的旧档(常态)按「已完成转换章」
                    return cc >= 16;                       // 预填;若要求下一篇开篇章完成,这段窗口里
                case (int)CutsceneId.BureauHandover:       // 大事记会误显「尚未发生」且过场永不补播。
                    return cc >= 22;
                case (int)CutsceneId.BankMandate:
                    return cc >= 30;
                case (int)CutsceneId.Homecoming:
                    return cc >= 53;
                case (int)CutsceneId.HousingMania:
                {
                    if (state.housing != null && state.housing.leveragePhase == LeveragePhase.Inflating) return true;
                    List<HousingPeriodRecord> housingHistory = state.housing != null ? state.housing.history : null;
                    for (int i = 0; housingHistory != null && i < housingHistory.Count; i++)
                        if (housingHistory[i] != null && housingHistory[i].phase == LeveragePhase.Inflating) return true;
                    return false;
                }
                default: return false;
            }
        }

        public static ChronicleTier Tier(GameState state, int id)
        {
            if (IsViewed(state, id)) return ChronicleTier.Viewed;
            return Happened(state, id) ? ChronicleTier.Prefilled : ChronicleTier.Locked;
        }
    }
}
