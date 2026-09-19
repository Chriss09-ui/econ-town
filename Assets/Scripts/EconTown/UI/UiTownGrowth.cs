using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// V0.7.3《生长的小镇》三处新区的只读投影面板（PRD §141 交互点，全部轻量、只读）：
    /// 书店门口小黑板（本周概念）/ 行政广场公告碑（市长称号与任期）/ 央行前政策利率牌。
    /// 只读权威状态，不记章节信号、不进导航栏；施工/未动工态照报关行黑板先例给说明文案。
    /// </summary>
    public partial class UIManager
    {
        /// <summary>点击入口（GrowthSignClick）与按 E 的统一分发。</summary>
        public void ShowGrowthPanel(string which)
        {
            switch (which)
            {
                case "stele": ShowCivicStele(); break;
                case "rateboard": ShowPolicyRateBoard(); break;
                default: ShowWeeklyConcept(); break;
            }
        }

        /// <summary>东街书店门口小黑板：「本周概念」——从已解锁知识卡中按周选 1 张展示（只读投影 CardLibrary）。</summary>
        public void ShowWeeklyConcept()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ProgressionService.Ensure(s);

            GameObject root = BeginPanel("东街书店 · 本周概念", 920f, 640f);
            RectTransform c = ContentOf(root);
            DistrictPhase phase = TownGrowthService.EastDistrict(
                s.progression != null ? s.progression.completedChapter : 0);
            if (phase != DistrictPhase.Completed)
            {
                RectTransform locked = WorldCard(c, "新区尚未落成", 16f, 20f, 840f, 380f, WorldGold);
                ShopText(locked, phase == DistrictPhase.UnderConstruction
                        ? "东商业新区施工中：书店、花店与两间临街铺面正在建设。\n\n完成第二篇「市场竞争」后，这里会在一个清晨落成，\n小黑板每周换一块知识卡，帮你把学过的概念捡起来。"
                        : "地图东侧规划为东商业新区。\n\n完成第一篇「成为企业主」后动工，完成第二篇后落成——\n小镇会随着你的学习进度一点点长大。",
                    20, UiTheme.TextMain, 32f, 70f, 776f, 280f, TextAnchor.UpperLeft);
                GrowthFooter(c, root);
                return;
            }

            var atm = s.economy != null ? s.economy.atmosphere : null;
            CardDef card = atm != null && atm.weeklyConceptCard >= 0 && atm.weeklyConceptCard < CardLibrary.All.Count
                ? CardLibrary.All[atm.weeklyConceptCard] : null;

            RectTransform head = WorldCard(c, "第 " + Mathf.Max(1, atm != null ? atm.conceptWeek : 1) + " 周 · 本周概念",
                16f, 20f, 840f, 380f, WorldGold);
            if (card == null)
                ShopText(head, "本周概念暂缺：还没有解锁的知识卡。\n\n继续经营小镇，知识卡会随章节与行为陆续到手——\n下周黑板会再换一张。",
                    20, UiTheme.TextSub, 32f, 70f, 776f, 280f, TextAnchor.UpperLeft);
            else
                ShopText(head,
                    "「" + card.title + "」\n\n" + card.body +
                    (string.IsNullOrEmpty(card.formula) ? "" : "\n\n" + card.formula) +
                    "\n\n——黑板每周一换；全部知识卡可在手册与实验室复习。",
                    20, UiTheme.TextMain, 32f, 56f, 776f, 300f, TextAnchor.UpperLeft);

            RectTransform note = WorldCard(c, "这块黑板是仪表，不是商店", 16f, 420f, 840f, 130f, WorldTeal);
            ShopText(note, "书店是小镇学习进度的纪念品：它陈列的不是商品，是你已经学过的概念。",
                18, UiTheme.TextSub, 28f, 52f, 780f, 60f, TextAnchor.MiddleLeft);
            GrowthFooter(c, root);
        }

        /// <summary>行政广场公告碑：市长称号与任期周期数（TermStyle 只读投影）+ 财政立场旗色图例。</summary>
        public void ShowCivicStele()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ProgressionService.Ensure(s);
            GovernmentSimulator.Ensure(s);

            GameObject root = BeginPanel("行政广场 · 公告碑", 920f, 640f);
            RectTransform c = ContentOf(root);
            DistrictPhase phase = TownGrowthService.CivicPlaza(
                s.progression != null ? s.progression.completedChapter : 0);
            if (phase != DistrictPhase.Completed)
            {
                RectTransform locked = WorldCard(c, "广场施工中", 16f, 20f, 840f, 380f, WorldGold);
                ShopText(locked, phase == DistrictPhase.UnderConstruction
                        ? "市政厅前正在广场化：旗杆、公告碑与绿化带施工中。\n\n完成第四篇「治理城市」后，旗杆会在这里升起。"
                        : "市政厅前的空地规划为行政广场。\n\n完成第三篇「居民生活」后动工，完成第四篇后落成。",
                    20, UiTheme.TextMain, 32f, 70f, 776f, 280f, TextAnchor.UpperLeft);
                GrowthFooter(c, root);
                return;
            }

            string title = GovernmentSimulator.TermStyle(s.government);
            int terms = s.government != null && s.government.history != null ? s.government.history.Count : 0;
            RectTransform record = WorldCard(c, "碑文 · 本任市长", 16f, 20f, 840f, 300f, WorldGold);
            ShopText(record,
                "市长风格：" + title + "\n\n已完成任期周期：" + terms + " 期\n\n碑文随你的执政记录更新——政策取向不同，称号也不同。",
                20, UiTheme.TextMain, 32f, 56f, 776f, 220f, TextAnchor.UpperLeft);

            string stance, stanceNote;
            Color stanceColor;
            switch (s.macro != null ? s.macro.fiscalStance : FiscalStance.Neutral)
            {
                case FiscalStance.Expansionary:
                    stance = "扩张性财政（旗面·红）"; stanceNote = "政府正在加大支出或减税刺激需求"; stanceColor = new Color32(0xc2, 0x3b, 0x2e, 255); break;
                case FiscalStance.Contractionary:
                    stance = "紧缩性财政（旗面·蓝）"; stanceNote = "政府正在收缩支出或增税给经济降温"; stanceColor = new Color32(0x2e, 0x5a, 0xa8, 255); break;
                default:
                    stance = "中性财政（旗面·绿）"; stanceNote = "财政政策保持观望，不刻意刺激也不刻意收紧"; stanceColor = new Color32(0x3a, 0x7d, 0x44, 255); break;
            }
            RectTransform flag = WorldCard(c, "旗杆图例 · 财政立场", 16f, 340f, 840f, 200f, stanceColor);
            ShopText(flag, "当前：" + stance + "\n" + stanceNote + "。\n\n旗面颜色实时反映财政立场——抬头看旗，就知道政府踩的是油门还是刹车。",
                19, UiTheme.TextMain, 28f, 56f, 780f, 128f, TextAnchor.UpperLeft);
            GrowthFooter(c, root);
        }

        /// <summary>央行前政策利率牌：只读显示当前政策利率与最近一次货币政策会议的决议方向（§141）。
        /// 游戏无议息日历（会议是玩家主动行为），不显示「距下次议息天数」。</summary>
        public void ShowPolicyRateBoard()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ProgressionService.Ensure(s);
            FinancialSystemService.Ensure(s);

            GameObject root = BeginPanel("中央银行 · 政策利率牌", 920f, 640f);
            RectTransform c = ContentOf(root);
            DistrictPhase phase = TownGrowthService.FinancialStreet(
                s.progression != null ? s.progression.completedChapter : 0);
            if (phase != DistrictPhase.Completed)
            {
                RectTransform locked = WorldCard(c, "金融街施工中", 16f, 20f, 840f, 380f, WorldGold);
                ShopText(locked, phase == DistrictPhase.UnderConstruction
                        ? "金融街改造施工中：银行外立面亮化、双街灯与利率牌安装中。\n\n完成第六篇「钱、银行与央行」后，利率牌会在这里亮起。"
                        : "商业银行与中央银行一带规划为金融街。\n\n完成第五篇「管理整个经济」后动工，完成第六篇后落成。",
                    20, UiTheme.TextMain, 32f, 70f, 776f, 280f, TextAnchor.UpperLeft);
                GrowthFooter(c, root);
                return;
            }

            var cb = s.centralBank;
            if (cb == null || !cb.unlocked)
            {
                RectTransform locked = WorldCard(c, "利率牌待启用", 16f, 20f, 840f, 300f, WorldGold);
                ShopText(locked, "中央银行尚未运作，利率牌暂时没有数字可显示。\n\n完成金融篇后，这里会常驻显示政策利率。",
                    20, UiTheme.TextMain, 32f, 70f, 776f, 200f, TextAnchor.UpperLeft);
                GrowthFooter(c, root);
                return;
            }

            RectTransform rate = WorldCard(c, "今日牌面", 16f, 20f, 840f, 220f, WorldGold);
            ShopText(rate, "政策利率：" + cb.policyRate.ToString("0.00") + "%\n通胀目标：" + cb.inflationTarget.ToString("0.0") + "%",
                24, UiTheme.TextMain, 32f, 60f, 776f, 130f, TextAnchor.UpperLeft);

            var tr = cb.transmission;
            string direction = tr != null && !string.IsNullOrEmpty(tr.direction) ? tr.direction : "保持";
            bool hasDecision = tr != null && tr.decisionWorldDay >= 0;
            RectTransform decision = WorldCard(c, "最近一次货币政策会议", 16f, 260f, 840f, 240f, WorldTeal);
            ShopText(decision, hasDecision
                    ? "决议方向：" + direction + "\n决议于第 " + tr.decisionWorldDay + " 天作出\n\n" + tr.summary +
                    "\n\n政策有传导时滞：牌上的利率变了，实体经济要过几天才感觉到。"
                    : "还没有任何货币政策决议——走进中央银行，第一次降息由你来做。",
                19, UiTheme.TextMain, 28f, 56f, 780f, 168f, TextAnchor.UpperLeft);
            GrowthFooter(c, root);
        }

        void GrowthFooter(RectTransform parent, GameObject root)
        {
            Button close = UiTheme.MakeButton(parent, "返回小镇", () => ClosePanel(root), 19);
            PlaceTopLeft(close.rectTransform(), 260f, 548f, 400f, 52f);
            UiTheme.SetButtonStyle(close, WorldTeal, Color.white);
        }
    }
}
