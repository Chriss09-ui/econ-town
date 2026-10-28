using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public enum MainlineDestination
    {
        None,
        Shop,
        Market,
        Resident,
        Government,
        Macro,
        Finance,
        CentralBank,
        World,
        Port,
        Housing,
        Assets,
        Competition,
        PriceWar,
        MarketExperiments,
        Comparative,
        Trilemma
    }

    /// <summary>
    /// 主线呈现的唯一入口：身份跟篇章走，每一步只说一件能去做的事。
    /// 打开面板不再改写 currentView。
    /// </summary>
    public static class MainlineGuide
    {
        public struct Cue
        {
            public string line;
            public string detail;
            public MainlineDestination destination;
            public int tab;
            public int dockIndex;
        }

        public static void ApplyRole(GameState state)
        {
            if (state == null || state.progression == null || state.progression.mainlineComplete) return;
            PlayerViewMode role = RoleFor(state);
            if (state.currentView != role) state.currentView = role;
        }

        public static PlayerViewMode RoleFor(GameState state)
        {
            if (state == null || state.progression == null || state.progression.mainlineComplete)
                return PlayerViewMode.Enterprise;
            // 不经过 Current/Ensure：ApplyRole 在 Ensure 末尾调用本方法，再进去会递归。
            IList<ChapterDefinition> chapters = ProgressionService.Chapters;
            int index = state.progression.currentChapter - 1;
            if (index < 0 || index >= chapters.Count) return PlayerViewMode.Enterprise;
            ChapterDefinition chapter = chapters[index];
            switch (chapter.part)
            {
                case LearningPart.Resident:
                case LearningPart.Housing:
                    return PlayerViewMode.Resident;
                case LearningPart.Government:
                    return PlayerViewMode.Government;
                case LearningPart.Macro:
                    return PlayerViewMode.Macro;
                case LearningPart.Finance:
                    return chapter.number >= 37 ? PlayerViewMode.CentralBank : PlayerViewMode.Bank;
                default:
                    return PlayerViewMode.Enterprise;
            }
        }

        public static bool CanEnter(GameState state, PlayerViewMode view)
        {
            if (state == null) return false;
            ProgressionService.Ensure(state);
            if (state.progression.mainlineComplete) return true;
            int chapter = state.progression.currentChapter;
            switch (view)
            {
                case PlayerViewMode.Enterprise: return true;
                case PlayerViewMode.Resident: return chapter >= 11;
                case PlayerViewMode.Government: return chapter >= 17;
                case PlayerViewMode.Macro: return chapter >= 23;
                case PlayerViewMode.Bank: return chapter >= 31;
                case PlayerViewMode.CentralBank: return chapter >= 37;
                default: return false;
            }
        }

        public static bool CanEnterTrade(GameState state)
        {
            if (state == null) return false;
            ProgressionService.Ensure(state);
            return state.progression.mainlineComplete || state.progression.currentChapter >= 43;
        }

        public static bool CanEnterHousing(GameState state)
        {
            if (state == null) return false;
            ProgressionService.Ensure(state);
            return state.progression.mainlineComplete || state.progression.currentChapter >= 54;
        }

        public static bool CanEnterAssets(GameState state)
        {
            if (state == null) return false;
            ProgressionService.Ensure(state);
            return state.progression.mainlineComplete || state.progression.currentChapter >= 58;
        }

        public static string LockReason(PlayerViewMode view)
        {
            switch (view)
            {
                case PlayerViewMode.Resident: return "第 11 章下班后，再回家当镇民";
                case PlayerViewMode.Government: return "第 17 章，镇议会才会把市政交给你";
                case PlayerViewMode.Macro: return "第 23 章，再去看整个小镇的账";
                case PlayerViewMode.Bank: return "第 31 章，再进银行";
                case PlayerViewMode.CentralBank: return "第 37 章，你才会坐上央行的位子";
                default: return "还没到这一步";
            }
        }

        public static string TradeLockReason()
        {
            return "第 43 章，港口才对你开放";
        }

        public static string DockBlockReason(GameState state, int dock)
        {
            if (state == null) return null;
            ProgressionService.Ensure(state);
            if (state.progression.mainlineComplete) return null;
            switch (dock)
            {
                case 4: return CanEnter(state, PlayerViewMode.Resident) ? null : LockReason(PlayerViewMode.Resident);
                case 5: return CanEnter(state, PlayerViewMode.Government) ? null : LockReason(PlayerViewMode.Government);
                case 6: return CanEnter(state, PlayerViewMode.Macro) ? null : LockReason(PlayerViewMode.Macro);
                case 7: return CanEnter(state, PlayerViewMode.Bank) ? null : LockReason(PlayerViewMode.Bank);
                default: return null;
            }
        }

        public static string RoleName(GameState state)
        {
            if (state != null && state.progression != null && state.progression.mainlineComplete) return "自由";
            switch (RoleFor(state))
            {
                case PlayerViewMode.Resident: return "镇民";
                case PlayerViewMode.Government: return "镇长";
                case PlayerViewMode.Macro: return "观察者";
                case PlayerViewMode.Bank: return "银行";
                case PlayerViewMode.CentralBank: return "央行";
                default:
                    ChapterDefinition chapter = state == null ? null : ProgressionService.Current(state);
                    if (chapter != null && chapter.part == LearningPart.Trade) return "店主 · 港口";
                    if (chapter != null && chapter.part == LearningPart.Market) return "店主 · 有对手";
                    return "店主";
            }
        }

        public static string RoleSentence(GameState state)
        {
            if (state != null && state.progression != null && state.progression.mainlineComplete)
                return "身份：自由经营。店、家、镇政、港口都可以去";
            switch (RoleFor(state))
            {
                case PlayerViewMode.Resident:
                    return "身份：镇民。口袋是另一本账，店还是你的";
                case PlayerViewMode.Government:
                    return "身份：镇长。今天处理的是小镇的公事";
                case PlayerViewMode.Macro:
                    return "身份：观察者。今天看的是全镇的账";
                case PlayerViewMode.Bank:
                    return "身份：银行柜台前。存和贷都记在银行账上";
                case PlayerViewMode.CentralBank:
                    return "身份：央行行长。政策利率由你定";
                default:
                    ChapterDefinition chapter = state == null ? null : ProgressionService.Current(state);
                    if (chapter != null && chapter.part == LearningPart.Trade)
                        return "身份：果汁店店主。货从港口走，人还是你";
                    if (chapter != null && chapter.part == LearningPart.Market)
                        return "身份：果汁店店主。街对面有了对手";
                    return "身份：果汁店店主";
            }
        }

        /// <summary>篇末阶段报告追加的身份交代。参数是刚结束的那一篇。</summary>
        public static string IdentityAfter(LearningPart finished)
        {
            switch (finished)
            {
                case LearningPart.Enterprise:
                    return "身份没变：你还是果汁店店主。下一篇要对付街对面的新店。";
                case LearningPart.Market:
                    return "身份变了：从明天起你是镇民。回家创建档案，底栏「生活」会亮起来。店里的钱和口袋里的钱是两本账。";
                case LearningPart.Resident:
                    return "身份变了：从明天起你是镇长。先去市政厅，底栏「市政」会亮起来。";
                case LearningPart.Government:
                    return "身份变了：从明天起看全镇的账。底栏「宏观」会亮起来。";
                case LearningPart.Macro:
                    return "身份变了：从明天起进银行。底栏「金融」会亮起来。";
                case LearningPart.Finance:
                    return "身份变了：你回到店里，港口对你开放。往南乘接驳车。";
                case LearningPart.Trade:
                    return "身份变了：回家安家。这一篇你是镇民，要处理房子和储蓄。";
                default:
                    return "八篇结束。身份不再锁死：店、家、镇政、银行和港口都可以去。";
            }
        }

        public static string HandoffText(GameState state)
        {
            if (state == null || state.progression == null) return "";
            int done = state.progression.pendingHandoffChapter;
            IList<ChapterDefinition> chapters = ProgressionService.Chapters;
            if (done <= 0 || done > chapters.Count) return "";
            ChapterDefinition finished = chapters[done - 1];
            string shift = done == 36
                ? "\n\n身份变了：从现在起你是央行行长。政策利率由你定。"
                : "";
            return "第 " + finished.number + " 章完成。「" + finished.title + "」\n弄清的是：" +
                finished.learningGoal + shift;
        }

        public static Cue Build(GameState state)
        {
            Cue cue = new Cue { dockIndex = -1 };
            if (state == null) return cue;
            ProgressionService.Ensure(state);
            if (state.progression.mainlineComplete)
            {
                cue.line = "八篇都走完了。店、家、镇政、港口都可以自己去";
                return cue;
            }
            ChapterDefinition chapter = ProgressionService.Current(state);
            if (chapter == null) return cue;
            cue = CueFor(state, chapter);
            if (state.day - Mathf.Max(1, state.progression.chapterStartedWorldDay) >= 2)
                cue.line = "还差这一步：" + cue.line;
            cue.dockIndex = DockIndex(cue.destination);
            return cue;
        }

        public static int DockIndex(MainlineDestination destination)
        {
            switch (destination)
            {
                case MainlineDestination.Shop: return 1;
                case MainlineDestination.Market:
                case MainlineDestination.Competition:
                case MainlineDestination.PriceWar:
                case MainlineDestination.MarketExperiments:
                    return 2;
                case MainlineDestination.Resident:
                case MainlineDestination.Housing:
                case MainlineDestination.Assets:
                    return 4;
                case MainlineDestination.Government: return 5;
                case MainlineDestination.Macro: return 6;
                case MainlineDestination.Finance:
                case MainlineDestination.CentralBank:
                    return 7;
                default: return -1;
            }
        }

        static Cue CueFor(GameState state, ChapterDefinition chapter)
        {
            switch (chapter.requiredSignal)
            {
                case ProgressSignals.FirstBusiness:
                    if (state.phase == GamePhase.Report)
                        return Line("打开今日报告，看这一天卖了多少、花了多少", MainlineDestination.Shop);
                    if (state.phase == GamePhase.Business)
                        return Line("店已经开了。等客人走完，再看今日报告", MainlineDestination.None);
                    return Line("走进果汁店，定好售价，然后开店", MainlineDestination.Shop);
                case ProgressSignals.PriceChanged:
                    return Line("打开店铺，把售价改成另一个价，再开一天店", MainlineDestination.Shop);
                case ProgressSignals.ProfitObserved:
                    if (state.phase == GamePhase.Report)
                        return Line("打开今日报告，分开看收入、成本和利润", MainlineDestination.Shop);
                    return Line("再开一天店。收工后打开报告，看利润", MainlineDestination.Shop);
                case ProgressSignals.EmployeeHired:
                    if (state.employees < 2)
                        return Line("打开店铺，把雇佣人手加到 2 人，然后开店", MainlineDestination.Shop);
                    if (state.phase == GamePhase.Report)
                        return Line("看报告：多一个人之后，利润变了多少", MainlineDestination.Shop);
                    return Line("人手已经到位。去店里开一天店", MainlineDestination.Shop);
                case ProgressSignals.ProfitExperiment:
                    return Line("再用一个没试过的售价开店（已试 " + ListedPriceKinds(state) + "/4 种价格）",
                        MainlineDestination.Shop);
                case ProgressSignals.CompetitorOpened:
                    return Line("去市场，看 Sunny Tea 的开业说明", MainlineDestination.Competition);
                case ProgressSignals.MarketShareObserved:
                    return Line("开一天店，然后打开市场，看你还剩多少份额", MainlineDestination.Market);
                case ProgressSignals.DifferentiationUsed:
                    return Line("打开店铺，升级一次品质，或投入今天的推广", MainlineDestination.Shop);
                case ProgressSignals.PriceWarResolved:
                    if (ProgressionService.RequiresPriceWarChoice(state))
                        return Line("Sunny Tea 降价了。在价格战里选一个回应", MainlineDestination.PriceWar);
                    return Line("先开一天店，等 Sunny Tea 逼你做价格战选择", MainlineDestination.Shop);
                case ProgressSignals.MarketExperiment:
                    return Line("打开市场里的结构实验，做完其中一种", MainlineDestination.MarketExperiments);
                case ProgressSignals.ResidentProfile:
                    return Line("打开「生活」，给镇民身份建档。这不会动店里的钱", MainlineDestination.Resident);
                case ProgressSignals.ScarcityExperienced:
                    return Line("打开「生活」，去商业街花一笔受预算限制的钱，再结束今天", MainlineDestination.Resident);
                case ProgressSignals.OpportunityCost:
                    return Line("打开「生活」，在「安排工作与闲暇」里选一件要花时间的事", MainlineDestination.Resident);
                case ProgressSignals.DiminishingUtility:
                    return Line("打开「生活」，去商业街把同一种东西再买一次，比较两次的满足感", MainlineDestination.Resident);
                case ProgressSignals.BudgetChoice:
                    return Line("打开「生活」，去商业街买两类不同的东西，再结束今天", MainlineDestination.Resident);
                case ProgressSignals.LaborLeisure:
                    return Line("打开「生活」，去工作或休息一次，然后结束今天", MainlineDestination.Resident);
                case ProgressSignals.GovernmentOpened:
                    return Line("去「公共建设」，点「运行自愿筹资实验」", MainlineDestination.Government, 5,
                        "这一步不在「财政总览」。\n" +
                        "1. 点屏幕底部的「市政」。清晨卡片上的「去做」会直接打开「公共建设」。\n" +
                        "2. 若停在别的页，看顶上那一排，点「公共建设」。左边那张卡片叫「免费搭便车实验」。\n" +
                        "3. 实验是这样的：20 位居民都想在街上装路灯，要自愿凑满 ¥1000。每个人可以出钱，也可以等别人出。\n" +
                        "4. 点卡片上的「运行自愿筹资实验」。这一下不花政府的钱，也不改税。\n" +
                        "5. 看出资人数和募集到的金额。路灯装上以后，没出钱的人也能用，所以很多人会等别人付钱，钱通常凑不满。看完这一页，本章就完成。");
                case ProgressSignals.ExternalityPolicy:
                    return Line("打开市政厅的环境治理，用一次污染治理政策", MainlineDestination.Government, 4);
                case ProgressSignals.TaxPolicy:
                    return Line("打开市政厅的税收页，改一次饮品税", MainlineDestination.Government, 1);
                case ProgressSignals.MinimumWage:
                    return Line("打开市政厅的劳动就业，改一次最低工资，再结算", MainlineDestination.Government, 3);
                case ProgressSignals.PriceControl:
                    return Line("打开市政厅的税收与限价，试一次价格上限或下限", MainlineDestination.Government, 1);
                case ProgressSignals.FiscalCycle:
                    return Line("打开市政厅，结算一期财政，看余额和债务", MainlineDestination.Government, 0);
                case ProgressSignals.MacroOpened:
                    return Line("打开宏观的国民经济页，看 GDP", MainlineDestination.Macro, 0);
                case ProgressSignals.RealNominalObserved:
                    return Line("打开宏观的国民经济页，比较名义 GDP 和实际 GDP", MainlineDestination.Macro, 0);
                case ProgressSignals.InflationObserved:
                    return Line("打开宏观的物价页，看 CPI 和通胀", MainlineDestination.Macro, 1);
                case ProgressSignals.UnemploymentObserved:
                    return Line("打开宏观的劳动就业页，看失业是怎么构成的", MainlineDestination.Macro, 2);
                case ProgressSignals.RecessionObserved:
                    return Line("打开宏观的总需求页，点一次衰退冲击（能源危机、海外需求下降或自然灾害）",
                        MainlineDestination.Macro, 3);
                case ProgressSignals.FiscalResponse:
                    return Line("打开宏观的财政政策页，在衰退里用一次财政工具", MainlineDestination.Macro, 4);
                case ProgressSignals.OutputGapObserved:
                    return Line("打开宏观的总需求页，看一次产出缺口", MainlineDestination.Macro, 3);
                case ProgressSignals.ProductivityPolicy:
                    return Line("打开宏观的总需求页，点「AI 革命」或人口增长，做一次生产率冲击",
                        MainlineDestination.Macro, 3);
                case ProgressSignals.MoneyFunctionsExperienced:
                    return Line("打开金融的银行大厅，点「用货币完成交换」", MainlineDestination.Finance, 0);
                case ProgressSignals.BankDepositMade:
                    return Line("打开金融的银行大厅，把一笔居民的钱存进去", MainlineDestination.Finance, 0);
                case ProgressSignals.BankBalanceViewed:
                    return Line("打开金融的资产负债页，看银行把钱借给了谁", MainlineDestination.Finance, 1);
                case ProgressSignals.CreditCreated:
                    return Line("打开银行大厅，批准一笔消费贷。贷款和存款会同时增加", MainlineDestination.Finance, 0);
                case ProgressSignals.CompanyInvestmentFinanced:
                    return Line("打开金融的企业融资页，用贷款做一次设备投资", MainlineDestination.Finance, 2);
                case ProgressSignals.InterestChoiceObserved:
                    return Line("打开金融的银行大厅，比较利率变化前后的储蓄和消费", MainlineDestination.Finance, 0);
                case ProgressSignals.CentralBankOpened:
                    return Line("打开金融的央行政策页，读完你的职责", MainlineDestination.CentralBank, 3);
                case ProgressSignals.PolicyRateCut:
                    return Line("打开央行政策页。需求走弱时，把政策利率调低", MainlineDestination.CentralBank, 3);
                case ProgressSignals.PolicyRateRaised:
                    return Line("打开央行政策页。有通胀压力时，把政策利率调高", MainlineDestination.CentralBank, 3);
                case ProgressSignals.PolicyLagObserved:
                    return Line("结算几天，再打开央行政策页，看传导走到了哪一阶段", MainlineDestination.CentralBank, 3);
                case ProgressSignals.BankRunResolved:
                    return Line("打开金融压力页，触发挤兑，再用紧急流动性把它稳住", MainlineDestination.Finance, 4);
                case ProgressSignals.PolicyMixObserved:
                    return Line("同一天里各用一次财政工具和货币工具，再打开金融看结果", MainlineDestination.Finance, 4);
                case ProgressSignals.PortOpened:
                    return Line("打开世界经济的「贸易与企业」，打开出海，再结算一天真实出货", MainlineDestination.World, 1);
                case ProgressSignals.ComparativeAdvantageExperimentDone:
                    return Line("打开比较优势实验，比较两国的消费组合", MainlineDestination.Comparative);
                case ProgressSignals.ImportConsumed:
                    return Line("打开「生活」，去商业街买一件进口商品，再看它的价格构成", MainlineDestination.Resident);
                case ProgressSignals.TariffEnacted:
                    return Line("打开世界经济的海关页，把进口关税调到不是 0，再结算一天", MainlineDestination.World, 3);
                case ProgressSignals.ExchangeRateObserved:
                    return Line("打开世界经济的外汇页，看「1 B币 = 多少 A币」", MainlineDestination.World, 2);
                case ProgressSignals.DepreciationExperienced:
                    return Line("打开外汇页。若今天还没明显贬值，先结算几天，直到出现一次贬值日，再看出口和进口价",
                        MainlineDestination.World, 2);
                case ProgressSignals.CapitalFlowObserved:
                    return Line("打开外汇页，看利差怎样推动资本流动，再结算一天", MainlineDestination.World, 2);
                case ProgressSignals.ForeignRecessionFelt:
                    return Line("打开世界经济概览，在 B 国衰退时看你的出口订单", MainlineDestination.World, 0);
                case ProgressSignals.TradeWarResponseChosen:
                    return Line("打开世界经济概览，对贸易战选一个回应", MainlineDestination.World, 0);
                case ProgressSignals.ReserveDefenseTried:
                    return Line("打开外汇页，试着用外汇储备守住汇率，看弹药怎么减少", MainlineDestination.World, 2);
                case ProgressSignals.TrilemmaDemoDone:
                    return Line("打开三元悖论演示，把三个目标拨一遍", MainlineDestination.Trilemma);
                case ProgressSignals.HousingRentPaid:
                    return Line("打开「我的住房」，租下住处，再结算一天把租金付掉", MainlineDestination.Housing);
                case ProgressSignals.HousingMortgageIssued:
                    return Line("打开「我的住房」，买下自住房，等银行放款入账", MainlineDestination.Housing);
                case ProgressSignals.HousingPriceBreakdown:
                    return Line("打开「我的住房」，读下面的房价分解", MainlineDestination.Housing);
                case ProgressSignals.HousingDrawdownSurvived:
                    return Line("打开「我的住房」。房价要从峰值跌下 8%，这期间把存款留够，月供不要断",
                        MainlineDestination.Housing);
                case ProgressSignals.HousingAssetsDiversified:
                    return Line("打开资产配置，让存款、国债、指数基金各做一笔，并让房贷走到终点",
                        MainlineDestination.Assets);
                default:
                    return Line(chapter.task, MainlineDestination.Shop);
            }
        }

        static Cue Line(string line, MainlineDestination destination, int tab = 0, string detail = null)
        {
            return new Cue { line = line, detail = detail, destination = destination, tab = tab, dockIndex = -1 };
        }

        static int ListedPriceKinds(GameState state)
        {
            HashSet<int> prices = new HashSet<int>();
            if (state.history == null) return 0;
            for (int i = 0; i < state.history.Count; i++)
            {
                DayRecord record = state.history[i];
                if (record != null && record.shopOpen)
                    prices.Add(Mathf.RoundToInt(record.listedPrice * 100f));
            }
            return prices.Count;
        }
    }
}
