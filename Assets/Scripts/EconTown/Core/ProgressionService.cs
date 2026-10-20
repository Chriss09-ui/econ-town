using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>主线唯一权威：六篇章节由经济行为推进，不由 WorldDay 推进。</summary>
    public static class ProgressionService
    {
        static readonly ChapterDefinition[] chapters =
        {
            Chapter(1, LearningPart.Enterprise, "第一天营业", "完成第一次经营", "认识一次完整的经营结算", ProgressSignals.FirstBusiness),
            Chapter(2, LearningPart.Enterprise, "为什么涨价以后人少了", "调整一次售价并完成营业", "观察需求规律", ProgressSignals.PriceChanged),
            Chapter(3, LearningPart.Enterprise, "卖得多不一定赚得多", "查看一次收入、成本与利润", "区分收入与利润", ProgressSignals.ProfitObserved),
            Chapter(4, LearningPart.Enterprise, "多雇一个人值不值", "雇佣至少一名新员工并营业", "理解边际产量", ProgressSignals.EmployeeHired),
            Chapter(5, LearningPart.Enterprise, "找到利润最高点", "用不同价格完成至少四次经营", "初步比较 MR 与 MC", ProgressSignals.ProfitExperiment),

            Chapter(6, LearningPart.Market, "新的竞争者", "查看 Sunny Tea 的开业信息", "认识市场竞争", ProgressSignals.CompetitorOpened),
            Chapter(7, LearningPart.Market, "顾客去哪了", "完成竞争后的营业并查看市场份额", "理解市场份额与替代品", ProgressSignals.MarketShareObserved),
            Chapter(8, LearningPart.Market, "便宜还是更好", "使用一次品质、品牌或广告差异化", "理解产品差异化", ProgressSignals.DifferentiationUsed),
            Chapter(9, LearningPart.Market, "价格战争", "处理 Sunny Tea 的降价选择", "体验策略相互依赖", ProgressSignals.PriceWarResolved),
            Chapter(10, LearningPart.Market, "市场不只有一种", "完成一次市场结构实验", "比较四种市场结构", ProgressSignals.MarketExperiment),

            Chapter(11, LearningPart.Resident, "工资到账了", "创建居民档案并开始生活", "从消费者视角观察经济", ProgressSignals.ResidentProfile),
            Chapter(12, LearningPart.Resident, "钱不够花", "完成一次受预算限制的消费日", "理解稀缺", ProgressSignals.ScarcityExperienced),
            Chapter(13, LearningPart.Resident, "今晚该干什么", "在两个会占用时间的活动之间选择", "理解机会成本", ProgressSignals.OpportunityCost),
            Chapter(14, LearningPart.Resident, "第一杯最好喝", "重复消费同类商品并比较边际效用", "理解边际效用递减", ProgressSignals.DiminishingUtility),
            Chapter(15, LearningPart.Resident, "100 元怎么花", "完成包含两类商品的预算选择", "理解预算约束", ProgressSignals.BudgetChoice),
            Chapter(16, LearningPart.Resident, "要钱还是要休息", "工作或休闲一次并完成当日", "理解劳动—闲暇选择", ProgressSignals.LaborLeisure),

            Chapter(17, LearningPart.Government, "路灯谁来出钱", "进入市政府并完成公共品实验", "理解公共品与搭便车", ProgressSignals.GovernmentOpened),
            Chapter(18, LearningPart.Government, "河水变黑了", "使用一次污染治理政策", "理解负外部性", ProgressSignals.ExternalityPolicy),
            Chapter(19, LearningPart.Government, "一杯饮料的税", "调整一次饮品税并观察税负", "理解税负归宿", ProgressSignals.TaxPolicy),
            Chapter(20, LearningPart.Government, "最低工资争议", "调整最低工资并结算一期", "理解劳动市场政策", ProgressSignals.MinimumWage),
            Chapter(21, LearningPart.Government, "东西太贵了", "尝试一次价格上限或价格下限", "理解短缺与过剩", ProgressSignals.PriceControl),
            Chapter(22, LearningPart.Government, "政府也会没钱", "完成一期财政结算并查看余额与债务", "区分赤字与债务", ProgressSignals.FiscalCycle),

            Chapter(23, LearningPart.Macro, "整个小镇赚了多少", "打开宏观仪表盘查看 GDP", "理解 GDP 核算", ProgressSignals.MacroOpened),
            Chapter(24, LearningPart.Macro, "为什么数字涨了但没更富", "比较名义 GDP 与实际 GDP", "区分价格与真实产出", ProgressSignals.RealNominalObserved),
            Chapter(25, LearningPart.Macro, "东西怎么都贵了", "查看 CPI 分类与通胀", "理解总体价格水平", ProgressSignals.InflationObserved),
            Chapter(26, LearningPart.Macro, "工作突然不好找", "查看就业与失业构成", "理解失业率", ProgressSignals.UnemploymentObserved),
            Chapter(27, LearningPart.Macro, "经济衰退", "观察一次衰退或主动运行衰退实验", "理解经济周期", ProgressSignals.RecessionObserved),
            Chapter(28, LearningPart.Macro, "政府要不要救市", "在衰退中使用一次财政政策", "理解财政刺激的收益与代价", ProgressSignals.FiscalResponse),
            Chapter(29, LearningPart.Macro, "经济过热", "查看一次正或负产出缺口", "理解产出缺口", ProgressSignals.OutputGapObserved),
            Chapter(30, LearningPart.Macro, "怎样真正变富", "实施或实验一次生产率提升", "理解长期增长", ProgressSignals.ProductivityPolicy),

            Chapter(31, LearningPart.Finance, "钱到底是什么", "完成以物易物与货币交换的对比", "理解货币的三项职能", ProgressSignals.MoneyFunctionsExperienced),
            Chapter(32, LearningPart.Finance, "把钱存进银行", "完成一次居民存款", "理解存款、利息与银行负债", ProgressSignals.BankDepositMade),
            Chapter(33, LearningPart.Finance, "银行把钱借给了谁", "查看银行资产负债表", "认识银行资产、负债与信贷", ProgressSignals.BankBalanceViewed),
            Chapter(34, LearningPart.Finance, "银行创造了钱吗", "完成一次信用创造实验", "理解存款货币、准备金与货币乘数", ProgressSignals.CreditCreated),
            Chapter(35, LearningPart.Finance, "借钱扩建值不值", "使用企业贷款完成设备投资", "理解利率如何影响投资", ProgressSignals.CompanyInvestmentFinanced),
            Chapter(36, LearningPart.Finance, "为什么大家突然更愿意存钱", "比较利率变化前后的储蓄与消费", "理解利率与储蓄、消费", ProgressSignals.InterestChoiceObserved),
            Chapter(37, LearningPart.Finance, "欢迎成为央行行长", "进入中央银行并阅读职责", "理解央行、政策利率与通胀目标", ProgressSignals.CentralBankOpened),
            Chapter(38, LearningPart.Finance, "第一次降息", "在需求走弱时下调政策利率", "观察货币政策传导", ProgressSignals.PolicyRateCut),
            Chapter(39, LearningPart.Finance, "通胀来了", "在通胀压力下上调政策利率", "理解紧缩性货币政策的取舍", ProgressSignals.PolicyRateRaised),
            Chapter(40, LearningPart.Finance, "为什么加息没有马上见效", "推进时间并查看政策传导阶段", "理解货币政策时滞", ProgressSignals.PolicyLagObserved),
            Chapter(41, LearningPart.Finance, "银行门口排起了长队", "触发挤兑并用紧急流动性稳定银行", "理解流动性、最后贷款人与金融稳定", ProgressSignals.BankRunResolved),
            Chapter(42, LearningPart.Finance, "财政与货币", "同时使用一次财政与货币工具", "区分两类宏观工具的传导与代价", ProgressSignals.PolicyMixObserved),

            // —— 第七篇 · 世界市场(V0.7) ——
            Chapter(43, LearningPart.Trade, "港口开放了", "开通出海通道并完成一次真实出货", "认识出口、进口与净出口 NX", ProgressSignals.PortOpened),
            Chapter(44, LearningPart.Trade, "为什么不是什么都自己做", "运行一次比较优势实验并比较两国消费组合", "绝对优势≠比较优势：看机会成本", ProgressSignals.ComparativeAdvantageExperimentDone),
            Chapter(45, LearningPart.Trade, "外国商品来了", "购买一件进口商品并查看它的价格构成", "到岸价 = 外币价 × 汇率 × (1 + 关税)", ProgressSignals.ImportConsumed),
            Chapter(46, LearningPart.Trade, "要不要加关税", "把进口关税调到非零并完成一天结算", "关税保护生产者、伤害消费者，并产生无谓损失", ProgressSignals.TariffEnacted),
            Chapter(47, LearningPart.Trade, "一块钱能换多少外国钱", "打开外汇市场页查看报价与本日分解", "汇率是两国货币的相对价格「1 B币 = X A币」", ProgressSignals.ExchangeRateObserved),
            Chapter(48, LearningPart.Trade, "本币突然贬值", "经历一次明显的 A币贬值日并观察各主体反应", "贬值利好出口企业、抬升进口成本（输入型通胀）", ProgressSignals.DepreciationExperienced),
            Chapter(49, LearningPart.Trade, "为什么加息会影响汇率", "观察一次利差驱动的资本流动及其汇率后果", "利差−风险溢价决定国际资本方向，资本流改变汇率", ProgressSignals.CapitalFlowObserved),
            Chapter(50, LearningPart.Trade, "外国经济衰退了", "在 B 国衰退期间查看自己的出口订单变化", "全球需求冲击会沿贸易渠道传导到小镇港口", ProgressSignals.ForeignRecessionFelt),
            Chapter(51, LearningPart.Trade, "贸易战", "在 B 国加征关税时做出回应策略选择", "报复有代价：保护本国产业 vs 消费者与供应链成本", ProgressSignals.TradeWarResponseChosen),
            Chapter(52, LearningPart.Trade, "外汇储备正在下降", "尝试用外储守住固定汇率并观察消耗", "干预有弹药上限：储备不足时固定汇率允许失守", ProgressSignals.ReserveDefenseTried),
            Chapter(53, LearningPart.Trade, "三个目标为什么不能全要", "完成「三元悖论」演示实验", "固定汇率、资本自由流动、独立货币政策三者难全", ProgressSignals.TrilemmaDemoDone),

            // —— 第八篇 · 资产与杠杆(V0.9.5) ——
            Chapter(54, LearningPart.Housing, "一个屋檐", "租下住处并完成一次租金结算", "租房是购买流动性：付租金换灵活，不必把存款锁进首付", ProgressSignals.HousingRentPaid),
            Chapter(55, LearningPart.Housing, "三十年的账", "买下自住房并让银行放款入账", "房贷用未来三十年的收入流换今天的资产，杠杆放大收益也放大风险", ProgressSignals.HousingMortgageIssued),
            Chapter(56, LearningPart.Housing, "房价是一头兽", "读一次房价分解，看清谁在推着房价走", "需求、供给、信贷与动能共同定价；涨过 CPI 的才是真涨", ProgressSignals.HousingPriceBreakdown),
            Chapter(57, LearningPart.Housing, "屋檐下的杠杆", "在房价从峰值回撤 ≥8% 时仍守住月供不违约", "杠杆把回撤放大成净资产崩塌；现金流才是安全垫", ProgressSignals.HousingDrawdownSurvived),
            Chapter(58, LearningPart.Housing, "钱放在哪里", "让房贷走到终点，并给储蓄找到三个去向", "存款、国债与指数基金：收益、风险与流动性的三角", ProgressSignals.HousingAssetsDiversified)
        };

        static ChapterDefinition Chapter(int number, LearningPart part, string title, string task,
            string learningGoal, string signal)
        {
            return new ChapterDefinition(number, part, title, task, learningGoal, signal);
        }

        public static IList<ChapterDefinition> Chapters { get { return chapters; } }

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.progression == null) state.progression = new ProgressionState();
            ProgressionState p = state.progression;
            if (p.signals == null) p.signals = new List<string>();
            if (p.decisions == null) p.decisions = new List<KeyDecisionRecord>();
            if (p.stageReports == null) p.stageReports = new List<StageReportRecord>();
            if (p.pendingStageReports == null) p.pendingStageReports = new List<int>();
            if (p.pendingCeremonies == null) p.pendingCeremonies = new List<string>();
            // V0.5.2 存档的单值 pendingStageReport 迁移进队列，未查看的结业弹窗不丢。
            if (p.pendingStageReport > 0 && !p.pendingStageReports.Contains(p.pendingStageReport))
                p.pendingStageReports.Add(p.pendingStageReport);
            p.pendingStageReport = 0;
            p.completedChapter = Mathf.Clamp(p.completedChapter, 0, chapters.Length);
            // V0.5.x 在第 30 章将主线标记为完成。V0.6 延长到 42 章时仅重开新篇，
            // 保留已解锁的自由模式与历史进度。
            if (p.mainlineComplete && p.completedChapter < chapters.Length)
            {
                p.mainlineComplete = false;
                p.currentChapter = p.completedChapter + 1;
            }
            p.currentChapter = p.mainlineComplete ? chapters.Length + 1 : Mathf.Clamp(p.currentChapter, 1, chapters.Length);
            if (p.chapterStartedWorldDay < 1) p.chapterStartedWorldDay = state.day;
            if (!p.initialized)
            {
                p.initialized = true;
                MigrateExistingProgress(state);
                p.pendingHandoffChapter = 0;
            }
            ApplyUnlocks(state);
            MainlineGuide.ApplyRole(state);
        }

        public static ChapterDefinition Current(GameState state)
        {
            Ensure(state);
            if (state == null || state.progression.mainlineComplete) return null;
            return chapters[Mathf.Clamp(state.progression.currentChapter - 1, 0, chapters.Length - 1)];
        }

        public static string PartTitle(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Enterprise: return "第一篇 · 成为企业主";
                case LearningPart.Market: return "第二篇 · 市场竞争";
                case LearningPart.Resident: return "第三篇 · 居民生活";
                case LearningPart.Government: return "第四篇 · 治理城市";
                case LearningPart.Macro: return "第五篇 · 管理整个经济";
                case LearningPart.Finance: return "第六篇 · 钱、银行与央行";
                case LearningPart.Trade: return "第七篇 · 世界市场";
                case LearningPart.Housing: return "第八篇 · 资产与杠杆";
                default: return "自由经济时代";
            }
        }

        /// <summary>四幕归属(V0.9.17 晋升线):八篇两篇一幕的幕名,纯文案层归属,零章号改动。</summary>
        public static string ActLabel(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Enterprise:
                case LearningPart.Market:
                    return "第一幕 · 一个人的生意";
                case LearningPart.Resident:
                case LearningPart.Government:
                    return "第二幕 · 一位公民与一座镇";
                case LearningPart.Macro:
                case LearningPart.Finance:
                    return "第三幕 · 整个经济的守夜人";
                case LearningPart.Trade:
                case LearningPart.Housing:
                    return "第四幕 · 世界很大,家很小";
                default:
                    return "";
            }
        }

        /// <summary>阶段报告的「下一程」委任文案(V0.9.17):八个转换点全覆盖,Housing 为收官语。
        /// 展示层现查,不写进 StageReportRecord——旧档已有报告零迁移自动有文案。</summary>
        public static string NextJourneyText(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Enterprise:
                    return "这条街认识你了。而街对面,有人在挂新的招牌……";
                case LearningPart.Market:
                    return "商战的硝烟散去,你在这条街站稳了脚跟。今晚回家——从明天起,你不只是店主,也是这座小镇的居民。";
                case LearningPart.Resident:
                    return "你过惯了一个居民的日子。而镇议会的灯,昨夜亮到很晚——他们在等一位懂账的人。";
                case LearningPart.Government:
                    return "一座镇的钱你管明白了。镇上的人开始问更大的问题:这一年,我们整体是更富了,还是只是更贵了?";
                case LearningPart.Macro:
                    return "数字的背后是钱在流动。老行长合上了最后一页账本——他翻遍了全镇,选中了你。";
                case LearningPart.Finance:
                    return "账本合上了最后一页。港口的汽笛声,明早会到你窗外。";
                case LearningPart.Trade:
                    return "商船进港,世界的账结清了。今晚回家,门牌下多了一张属于你的房契。";
                case LearningPart.Housing:
                    return "八篇讲完了。你的经济实验,才刚刚开始。";
                default:
                    return "";
            }
        }

        public static bool HasSignal(GameState state, string signal)
        {
            Ensure(state);
            return state != null && state.progression.signals.Contains(signal);
        }

        public static bool RecordSignal(GameState state, string signal, string decisionTitle = null,
            string decisionSummary = null)
        {
            if (state == null || string.IsNullOrEmpty(signal)) return false;
            Ensure(state);
            bool added = !state.progression.signals.Contains(signal);
            if (added) state.progression.signals.Add(signal);
            if (!string.IsNullOrEmpty(decisionTitle)) RecordDecision(state, decisionTitle, decisionSummary);
            bool advanced = Evaluate(state);
            if (added || advanced) state.Changed();
            return added || advanced;
        }

        public static void OnDaySettled(GameState state)
        {
            if (state == null) return;
            Ensure(state);
            CaptureEnterpriseSignals(state);
            CaptureMarketSignals(state);
            CaptureResidentSignals(state);
            CaptureGovernmentSignals(state);
            CaptureFinancialSignals(state);
            CaptureTradeSignals(state);
            CaptureHousingSignals(state);
            CaptureFreeGoal(state);
            Evaluate(state);
        }

        public static bool RequiresPriceWarChoice(GameState state)
        {
            ChapterDefinition current = Current(state);
            return state != null && current != null && current.number == 9 && state.CompetitionActive &&
                !CompetitionEco.HasPlayerChoice(state.todayStrategyChoice) &&
                !HasSignal(state, ProgressSignals.PriceWarResolved);
        }

        public static StageReportRecord GetPendingStageReport(GameState state)
        {
            Ensure(state);
            if (state == null || state.progression.pendingStageReports.Count == 0) return null;
            LearningPart part = (LearningPart)state.progression.pendingStageReports[0];
            for (int i = state.progression.stageReports.Count - 1; i >= 0; i--)
                if (state.progression.stageReports[i].part == part) return state.progression.stageReports[i];
            // 悬挂条目（对应报告缺失，如旧档残缺）：丢弃以免永远占着队首。
            state.progression.pendingStageReports.RemoveAt(0);
            return null;
        }

        /// <summary>待看报告的纯读窥视(悬挂条目原样保留,不清理)——过场判定等只读路径专用;
        /// 消费/悬挂清理仍走 GetPendingStageReport。修复(找BUG未验证#2):Get 的悬挂分支会 RemoveAt
        /// (写副作用),CutsceneService.ShouldPlay 经由它判定违反「过场判定零写」。</summary>
        public static StageReportRecord PeekPendingStageReport(GameState state)
        {
            Ensure(state);
            if (state == null || state.progression.pendingStageReports.Count == 0) return null;
            for (int i = 0; i < state.progression.pendingStageReports.Count; i++)
            {
                LearningPart part = (LearningPart)state.progression.pendingStageReports[i];
                for (int j = state.progression.stageReports.Count - 1; j >= 0; j--)
                    if (state.progression.stageReports[j].part == part) return state.progression.stageReports[j];
            }
            return null;
        }

        public static void MarkPendingStageReportViewed(GameState state)
        {
            Ensure(state);
            if (state == null || state.progression.pendingStageReports.Count == 0) return;
            state.progression.pendingStageReports.RemoveAt(0);
            state.Changed();
        }

        static bool Evaluate(GameState state)
        {
            ProgressionState p = state.progression;
            bool advanced = false;
            while (!p.mainlineComplete)
            {
                ChapterDefinition current = chapters[Mathf.Clamp(p.currentChapter - 1, 0, chapters.Length - 1)];
                if (!p.signals.Contains(current.requiredSignal)) break;
                CompleteChapter(state, current);
                advanced = true;
            }
            return advanced;
        }

        static void CompleteChapter(GameState state, ChapterDefinition chapter)
        {
            ProgressionState p = state.progression;
            p.completedChapter = Mathf.Max(p.completedChapter, chapter.number);
            if (chapter.number == InternationalTradeService.ExportUnlockChapter)
                QueueCeremony(p, ProgressSignals.PortOpeningCeremony);
            // V0.7.3 三处新区：完成落成章的现场写入次晨演出队列（旧档不按 completedChapter 反推补播，§143）。
            if (chapter.number == TownGrowthService.EastDistrictCompletionChapter)
                QueueCeremony(p, ProgressSignals.EastDistrictCeremony);
            if (chapter.number == TownGrowthService.CivicPlazaCompletionChapter)
                QueueCeremony(p, ProgressSignals.CivicPlazaCeremony);
            if (chapter.number == TownGrowthService.FinancialStreetCompletionChapter)
                QueueCeremony(p, ProgressSignals.FinancialStreetCeremony);
            bool partCompleted = chapter.number == 5 || chapter.number == 10 || chapter.number == 16 ||
                chapter.number == 22 || chapter.number == 30 || chapter.number == 42 ||
                chapter.number == 53 ||  // V0.7 第七篇末章
                chapter.number == 58;    // V0.9.5 第八篇末章
            if (partCompleted) CreateStageReport(state, chapter.part);
            // 篇中交接次晨弹一张卡片；篇末已有阶段报告，不再叠一张。
            p.pendingHandoffChapter = partCompleted ? 0 : chapter.number;

            if (chapter.number >= chapters.Length)
            {
                p.currentChapter = chapters.Length + 1;
                p.mainlineComplete = true;
                p.sandboxUnlocked = true;
            }
            else
            {
                p.currentChapter = chapter.number + 1;
                p.chapterStartedWorldDay = state.day;
            }
            ApplyUnlocks(state);
            if (Application.isPlaying)
                SaveSystem.SaveKeyMoment(state, "章节完成：Chapter " + chapter.number + " " + chapter.title);
        }

        static void ApplyUnlocks(GameState state)
        {
            ProgressionState p = state.progression;
            if (p.completedChapter >= 5)
            {
                state.EnsureEconomyState();
                state.economy.market.sunnyTea.isOpen = true;
            }
            // 主线完成时即时同步 sandbox.unlocked：SandboxService.Ensure 的单向传播只在
            // 启动/读档等时机跑，完成第 30 章的当次会话里挑战入口会保持灰色。
            if (p.sandboxUnlocked && state.sandbox != null) state.sandbox.unlocked = true;
            if (state.centralBank != null && p.completedChapter >= 36) state.centralBank.unlocked = true;
            if (p.currentChapter == 9 && !p.signals.Contains(ProgressSignals.PriceWarResolved))
            {
                state.priceWarActive = true;
                state.priceWarRound = Mathf.Max(1, state.priceWarRound);
                state.competitor.strategy = CompetitorStrategy.LowPriceExpansion;
                state.competitor.price = Mathf.Min(state.competitor.price, 14f);
            }
        }

        static void CaptureEnterpriseSignals(GameState state)
        {
            if (state.history == null || state.history.Count == 0) return;
            DayRecord last = state.LastRecord;
            // 围猎三(#41):歇业结算日(SettleClosedDay,shopOpen=false)不算「营业」——
            // FirstBusiness/PriceChanged/ProfitExperiment 只认开门日的记录,否则
            // 第 1/2/3/5 章可在从未开门时完成,绕过章节「开门营业」的教学意图。
            if (last != null && last.shopOpen) AddSignal(state, ProgressSignals.FirstBusiness);
            if (last != null) AddSignal(state, ProgressSignals.ProfitObserved);
            if (state.employees >= 2) AddSignal(state, ProgressSignals.EmployeeHired);
            HashSet<int> prices = new HashSet<int>();
            for (int i = 0; i < state.history.Count; i++)
                if (state.history[i].shopOpen)
                    prices.Add(Mathf.RoundToInt(state.history[i].listedPrice * 100f));
            if (prices.Count >= 2) AddSignal(state, ProgressSignals.PriceChanged);
            if (prices.Count >= 4) AddSignal(state, ProgressSignals.ProfitExperiment);
        }

        static void CaptureMarketSignals(GameState state)
        {
            DayRecord last = state.LastRecord;
            if (last != null && last.competitionActive) AddSignal(state, ProgressSignals.MarketShareObserved);
            if (state.playerQualityLevel > 1 || state.playerBrand > 35f || state.marketingBudget > 0f)
                AddSignal(state, ProgressSignals.DifferentiationUsed);
            if (CompetitionEco.HasPlayerChoice(state.todayStrategyChoice))
                AddSignal(state, ProgressSignals.PriceWarResolved);
            if (state.marketExperimentMask != 0) AddSignal(state, ProgressSignals.MarketExperiment);
        }

        static void CaptureResidentSignals(GameState state)
        {
            ResidentState r = state.resident;
            if (r == null || !r.profileCreated) return;
            AddSignal(state, ProgressSignals.ResidentProfile);
            if (r.history != null && r.history.Count > 0) AddSignal(state, ProgressSignals.ScarcityExperienced);
            if (r.workActions + r.leisureActions + r.studyActions > 0) AddSignal(state, ProgressSignals.OpportunityCost);
            if (r.foodCount >= 2 || r.drinkCount >= 2 || r.entertainmentCount >= 2 || r.householdCount >= 2)
                AddSignal(state, ProgressSignals.DiminishingUtility);
            if ((r.basicPurchases > 0 && r.premiumPurchases > 0) || (r.todayChoices != null && r.todayChoices.Count >= 2))
                AddSignal(state, ProgressSignals.BudgetChoice);
            if (r.workActions > 0 || r.leisureActions > 0) AddSignal(state, ProgressSignals.LaborLeisure);
        }

        static void CaptureGovernmentSignals(GameState state)
        {
            GovernmentState g = state.government;
            if (g == null) return;
            if (g.freeRiderExperimentDone) AddSignal(state, ProgressSignals.GovernmentOpened);
            PolicyState pollution = FindPolicy(g, PolicyType.PollutionTax);
            PolicyState green = FindPolicy(g, PolicyType.GreenEquipmentSubsidy);
            if (IsUsed(pollution) || IsUsed(green)) AddSignal(state, ProgressSignals.ExternalityPolicy);
            if (IsUsed(FindPolicy(g, PolicyType.DrinkTax))) AddSignal(state, ProgressSignals.TaxPolicy);
            if (IsUsed(FindPolicy(g, PolicyType.MinimumWage))) AddSignal(state, ProgressSignals.MinimumWage);
            if (IsUsed(FindPolicy(g, PolicyType.PriceCeiling)) || IsUsed(FindPolicy(g, PolicyType.PriceFloor)))
                AddSignal(state, ProgressSignals.PriceControl);
            if (g.history != null && g.history.Count > 0) AddSignal(state, ProgressSignals.FiscalCycle);
        }

        static void CaptureFinancialSignals(GameState state)
        {
            if (state.bank == null || state.centralBank == null) return;
            PolicyTransmissionState transmission = state.centralBank.transmission;
            if (transmission != null && transmission.decisionWorldDay >= 0 &&
                state.day - transmission.decisionWorldDay >= 3)
                AddSignal(state, ProgressSignals.PolicyLagObserved);
            if (state.macro != null && state.macro.fiscalStance != FiscalStance.Neutral &&
                state.centralBank.policyHistory != null && state.centralBank.policyHistory.Count > 0)
                AddSignal(state, ProgressSignals.PolicyMixObserved);
        }

        static void CaptureFreeGoal(GameState state)
        {
            DayRecord last = state.LastRecord;
            ProgressionState p = state.progression;
            // 同一结算日只计一次：读档会丢弃当日记录重玩，同一条盈利日不得把 streak 再 +1
            // （否则"连续 3 日正利润"自由目标可被读档重放伪造）。
            if (last != null && last.day == p.positiveProfitStreakLastDay) return;
            p.positiveProfitStreak = last != null && last.profit > 0f ? p.positiveProfitStreak + 1 : 0;
            p.positiveProfitStreakLastDay = last != null ? last.day : 0;
            if (p.positiveProfitStreak >= 3) p.positiveProfitGoalComplete = true;
        }

        /// <summary>
        /// V0.7 第七篇信号采集:全部从真实贸易账本读数推导,禁止为过章造数——
        /// 玩家不做对应行为,章节就真实地停在那里(行为驱动的课程承诺)。
        /// UI 驱动的三项(汇率页打开/两个实验完成)在各自面板里 RecordSignal。
        /// </summary>
        static void CaptureTradeSignals(GameState state)
        {
            if (state.worldTrade == null || !state.worldTrade.exchangeRate.initialized) return;
            TradeDayRecord row = InternationalTradeService.LastRecord(state);

            if (row != null)
            {
                if (row.playerExportUnits > 0f) AddSignal(state, ProgressSignals.PortOpened);
                if (row.consumerImportSpend > 0f) AddSignal(state, ProgressSignals.ImportConsumed);
                if (row.tariffBase > 0.01f && IsTariffPositive(state))
                    AddSignal(state, ProgressSignals.TariffEnacted);
                if (Mathf.Abs(row.capitalInflowBCurrency - row.capitalOutflowBCurrency) >= 60f)
                    AddSignal(state, ProgressSignals.CapitalFlowObserved);
                if (row.officialInterventionBCurrency < -.01f)
                    AddSignal(state, ProgressSignals.ReserveDefenseTried);
            }
            if (state.worldTrade.countryB.recessionActive)
                AddSignal(state, ProgressSignals.ForeignRecessionFelt);
            if (LastDailyMovePercent(state) >= .35f)
                AddSignal(state, ProgressSignals.DepreciationExperienced);
        }

        static bool IsTariffPositive(GameState state)
        {
            return GovernmentSimulator.PolicyValue(state, PolicyType.ImportTariff) > 0.01f;
        }

        /// <summary>
        /// V0.9.5 第八篇信号采集:全部从真实住房账本/银行贷款簿推导,禁止为过章造数。
        /// priceBreakdown 的计数只由 MarkPriceBreakdownRead(住房面板读数入口)写入——
        /// 采集器只读不增,防「采集自赠信号」。
        /// </summary>
        static void CaptureHousingSignals(GameState state)
        {
            HousingState h = state.housing;
            if (h == null) return;

            // 54 一个屋檐:租房中且累计付过租金(DayRecord.rentPaid 自尾扫早退)。
            if (h.renting)
                for (int i = state.history.Count - 1; i >= 0; i--)
                    if (state.history[i] != null && state.history[i].rentPaid > 0f)
                    {
                        AddSignal(state, ProgressSignals.HousingRentPaid);
                        break;
                    }

            // 55/58 共用一次贷款簿扫描:玩家按揭(居民+房贷用途,任意状态——含已结清/已违约)。
            // 围猎三复审:补 borrowerId 归属过滤(照 BackfillHousingCards 口径)——镇买方按揭
            // 同属 Resident+Mortgage,不得误发住房章节信号。
            bool hasMortgage = false, mortgageTerminal = false;
            List<LoanRecord> loans = state.bank != null ? state.bank.loans : null;
            for (int i = 0; loans != null && i < loans.Count; i++)
            {
                LoanRecord loan = loans[i];
                if (loan == null || loan.purpose != LoanPurpose.Mortgage ||
                    loan.borrowerType != LoanBorrowerType.Resident) continue;
                if (!string.IsNullOrEmpty(loan.borrowerId) &&
                    loan.borrowerId != state.resident.residentName) continue;
                hasMortgage = true;
                if (loan.status != LoanStatus.Current) mortgageTerminal = true;
            }
            if (hasMortgage) AddSignal(state, ProgressSignals.HousingMortgageIssued);

            // 56 房价是一头兽:读过一次房价分解且分解非空。
            if (h.decomposeReads >= 1 && HousingService.PriceBreakdown(state) != null)
                AddSignal(state, ProgressSignals.HousingPriceBreakdown);

            // 57 屋檐下的杠杆:在贷扛过峰值回撤 ≥8%。None 相位 peak 每期软锚重置,
            // 回撤只在离开 None 的周期相位里有意义(UpdatePhase 前提,R26)。
            if (HousingService.CurrentMortgage(state) != null && h.peakPriceIndex > 0f &&
                1f - HousingService.PriceIndexMirror(state) / h.peakPriceIndex >= HousingService.CrashDrawdown)
                AddSignal(state, ProgressSignals.HousingDrawdownSurvived);

            // 58 钱放在哪里:房贷走到终态(Paid/Defaulted)且储蓄三档各有一次动作
            // (存款动作=既有 savingsActions;国债/基金计数由 AssetAllocationService 写)。
            if (mortgageTerminal && state.resident != null && state.resident.savingsActions >= 1 &&
                h.treasuryBuys >= 1 && h.fundTrades >= 1)
                AddSignal(state, ProgressSignals.HousingAssetsDiversified);
        }

        static float LastDailyMovePercent(GameState state)
        {
            List<RateDayRecord> history = state.worldTrade.exchangeRate.history;
            if (history == null || history.Count < 2) return 0f;
            RateDayRecord last = history[history.Count - 1];
            RateDayRecord prev = history[history.Count - 2];
            if (prev.rate <= 0f) return 0f;
            return (last.rate - prev.rate) / prev.rate * 100f;
        }

        static PolicyState FindPolicy(GovernmentState state, PolicyType type)
        {
            if (state == null || state.policies == null) return null;
            for (int i = 0; i < state.policies.Count; i++) if (state.policies[i].type == type) return state.policies[i];
            return null;
        }

        static bool IsUsed(PolicyState policy)
        {
            return policy != null && policy.active && Mathf.Abs(policy.value) > 0.001f;
        }

        static void AddSignal(GameState state, string signal)
        {
            if (!state.progression.signals.Contains(signal)) state.progression.signals.Add(signal);
        }

        static void QueueCeremony(ProgressionState progression, string ceremony)
        {
            if (!progression.signals.Contains(ceremony) && !progression.pendingCeremonies.Contains(ceremony))
                progression.pendingCeremonies.Add(ceremony);
        }

        public static bool HasPendingCeremony(GameState state, string ceremony)
        {
            Ensure(state);
            return state != null && state.progression.pendingCeremonies.Contains(ceremony);
        }

        public static bool MarkCeremonyViewed(GameState state, string ceremony)
        {
            Ensure(state);
            if (state == null) return false;
            int index = state.progression.pendingCeremonies.IndexOf(ceremony);
            if (index < 0) return false;
            state.progression.pendingCeremonies.RemoveAt(index);
            AddSignal(state, ceremony);
            state.Changed();
            return true;
        }

        static void RecordDecision(GameState state, string title, string summary)
        {
            ProgressionState p = state.progression;
            string chapterId = p.mainlineComplete ? "sandbox" : "chapter." + p.currentChapter;
            p.decisions.Add(new KeyDecisionRecord
            {
                worldDay = state.day,
                chapterId = chapterId,
                title = title,
                summary = summary ?? ""
            });
            if (p.decisions.Count > 80) p.decisions.RemoveAt(0);
        }

        static void CreateStageReport(GameState state, LearningPart part)
        {
            ProgressionState p = state.progression;
            for (int i = 0; i < p.stageReports.Count; i++) if (p.stageReports[i].part == part) return;
            StageReportRecord report = new StageReportRecord
            {
                part = part,
                completedWorldDay = state.day,
                resultSummary = BuildResultSummary(state, part),
                learnedSummary = BuildLearnedSummary(part),
                reviewSummary = BuildReviewSummary(state, part)
            };
            for (int i = 0; i < p.decisions.Count; i++)
                if (p.decisions[i].chapterId.StartsWith("chapter."))
                    report.keyDecisions.Add("Day " + p.decisions[i].worldDay + " · " + p.decisions[i].title + "：" + p.decisions[i].summary);
            if (report.keyDecisions.Count > 5) report.keyDecisions.RemoveRange(0, report.keyDecisions.Count - 5);
            p.stageReports.Add(report);
            // 追加进待查看队列：一次 Evaluate 连跨多篇界时每篇都保留弹窗机会，不互相覆盖。
            if (!p.pendingStageReports.Contains((int)part)) p.pendingStageReports.Add((int)part);
        }

        static string BuildResultSummary(GameState state, LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Enterprise:
                    return "累计利润 ¥" + state.TotalProfit.ToString("0") + " · 累计销量 " + state.TotalSold + " 杯";
                case LearningPart.Trade:
                    TradeDayRecord trade = InternationalTradeService.LastRecord(state);
                    return trade == null ? "尚无贸易账本记录。" :
                        "汇率 1 B币 = " + ExchangeRateService.Rate(state).ToString("0.00") +
                        " A币 · 本期出口 ¥" + trade.exportsTotal.ToString("0") +
                        " · 进口 ¥" + trade.importsTotal.ToString("0") +
                        " · NX ¥" + trade.netExports.ToString("0");
                case LearningPart.Market:
                    return "平均市场份额 " + (state.AverageMarketShare * 100f).ToString("0.0") + "% · 品牌 " + state.playerBrand.ToString("0");
                case LearningPart.Resident:
                    return "居民现金 ¥" + state.resident.cash.ToString("0") + " · 储蓄 ¥" + state.resident.savings.ToString("0") +
                        " · 幸福 " + state.resident.happiness.ToString("0");
                case LearningPart.Government:
                    return "财政余额 ¥" + state.government.budgetBalance.ToString("0") + " · 债务 ¥" + state.government.debt.ToString("0") +
                        " · 支持率 " + state.government.approvalRating.ToString("0") + "%";
                case LearningPart.Finance:
                    return "存款 ¥" + state.bank.TotalDeposits.ToString("0") + " · 贷款 ¥" + state.bank.PerformingLoans.ToString("0") +
                        " · 政策利率 " + state.centralBank.policyRate.ToString("0.00") + "% · 金融压力 " + state.bank.financialStress.ToString("0");
                case LearningPart.Housing:
                    HousingState hs = state.housing;
                    return "房价指数 " + HousingService.PriceIndexMirror(state).ToString("0") +
                        " · 房产净值 ¥" + HousingService.HomeEquity(state).ToString("0") +
                        " · 国债 ¥" + hs.treasuryFace.ToString("0") +
                        " · 镇景指数 " + hs.indexLevel.ToString("0.0") +
                        "（基金 ¥" + (hs.indexUnits * hs.indexLevel / 100f).ToString("0") + "）";
                default:
                    return "实际 GDP " + state.macro.realGdp.ToString("0") + " · 通胀 " + state.macro.inflation.ToString("0.0") +
                        "% · 失业 " + state.macro.unemploymentRate.ToString("0.0") + "%";
            }
        }

        static string BuildLearnedSummary(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Enterprise: return "需求、成本、边际产量、利润最大化";
                case LearningPart.Market: return "替代品、市场份额、差异化、博弈论、市场结构";
                case LearningPart.Resident: return "稀缺、机会成本、边际效用、预算约束、劳动—闲暇";
                case LearningPart.Government: return "公共品、外部性、税负、最低工资、价格管制、财政";
                case LearningPart.Finance: return "货币职能、银行资产负债表、信用创造、利率、央行、政策时滞与金融稳定";
                case LearningPart.Trade: return "比较优势、关税与无谓损失、汇率与输入型通胀、资本流动、三元悖论";
                case LearningPart.Housing: return "首付与杠杆、月供与摊销、房价分解、泡沫三征与明斯基时刻、资产配置三角";
                default: return "GDP、通胀、失业、经济周期、财政政策、长期增长";
            }
        }

        static string BuildReviewSummary(GameState state, LearningPart part)
        {
            if (part == LearningPart.Enterprise && state.unlockedCards.Count < 8) return "建议复习：固定成本、边际成本与利润";
            if (part == LearningPart.Market && state.CompletedMarketExperiments < 2) return "建议复习：博弈论与市场结构实验";
            if (part == LearningPart.Resident && state.resident.experimentMask == 0) return "建议复习：预算约束与效用实验";
            if (part == LearningPart.Government && state.government.impacts.Count < 2) return "建议复习：政策影响路径";
            if (part == LearningPart.Trade && (state.worldTrade == null || state.worldTrade.history.Count < 7))
                return "建议复习：多结算几天，让比较优势实验与汇率分解都有真实数据支撑。";
            if (part == LearningPart.Housing && (state.housing == null || state.housing.history.Count < 3))
                return "建议复习：多结算几期，让房价分解与回撤读数都有真实数据支撑。";
            if (part == LearningPart.Macro && state.macro.history.Count < 3) return "建议复习：AD-AS 与经济周期";
            if (part == LearningPart.Finance && (state.bank.history == null || state.bank.history.Count < 3))
                return "建议复习：银行资产负债与利率传导";
            return "本篇核心行为均已完成，可随时在手册与实验室复习。";
        }

        static void MigrateExistingProgress(GameState state)
        {
            // 迁移只根据旧档中真实发生过的行为，不按旧 WorldDay 直接赠送章节。
            CaptureEnterpriseSignals(state);
            CaptureMarketSignals(state);
            CaptureResidentSignals(state);
            CaptureGovernmentSignals(state);
            CaptureFinancialSignals(state);
            // 两个只能由 V0.5.2 UI 交互记录的信号，按旧档状态等价推断：
            // ① 竞争已引入 = 玩家真实见过 Sunny Tea 开业（CompetitorOpened 否则章节 6 卡死，
            //    且要求玩家重新点一次开业介绍）；
            // ② 打过价格战回合且已结束 = 价格战已处理（否则已通关玩家被迫重打价格战）。
            if (state.competitionIntroduced) AddSignal(state, ProgressSignals.CompetitorOpened);
            if (state.competitionIntroduced && state.priceWarRound >= 1 && !state.priceWarActive)
                AddSignal(state, ProgressSignals.PriceWarResolved);
            Evaluate(state);
        }
    }
}
