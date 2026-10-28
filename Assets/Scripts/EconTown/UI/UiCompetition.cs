using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.2 企业竞争：对手情报、市场中心、价格战、策略实验与市场结构短关卡。</summary>
    public partial class UIManager
    {
        public void ShowCompetitorShop()
        {
            GameState s = GameState.I;
            if (s == null || !s.CompetitionActive) { ShowToast("Sunny Tea 会在企业篇完成后开业"); return; }
            s.UnlockCard("imperfectinfo");

            CompetitorState cState = s.competitor;
            GameObject root = BeginPanel("Sunny Tea · 公开经营情报", 700, 590);
            RectTransform c = ContentOf(root);
            DayRecord last = s.LastRecord != null && s.LastRecord.competitionActive ? s.LastRecord : null;
            string queue = last == null ? "开业准备中" : (last.competitorSold >= 100 ? "排队很长" : last.competitorSold >= 60 ? "客流正常" : "门前清静");
            string stars = cState.EffectiveQuality >= 85f ? "★★★★★" : cState.EffectiveQuality >= 70f ? "★★★★" : "★★★";
            string body =
                "<b>Sunny Tea</b>\n\n" +
                "今日售价：" + cState.price.ToString("0") + " 元/杯\n" +
                "招牌产品：阳光果茶\n" +
                "产品品质：" + stars + "（约 " + cState.EffectiveQuality.ToString("0") + "）\n" +
                "店铺评分：" + cState.satisfaction.ToString("0") + " / 100    品牌：" + cState.brand.ToString("0") + "\n" +
                "门店客流：" + queue + (last != null ? "（昨日 " + last.competitorSold + " 人购买）" : "") + "\n" +
                "公开策略：" + CompetitionEco.StrategyName(cState.strategy) +
                (last != null ? "\n昨日市场份额：" + (last.competitorMarketShare * 100f).ToString("0.0") + "%" : "") +
                "\n\n<color=#d6b77a>你看不到：</color>对方的真实成本、库存和利润。\n只能根据价格、排队、评分和后续行动推测其经营状况。\n\n这就是企业竞争中的「信息不完全」。";
            Text t = UiTheme.MakeText(c, body, 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(10, 78);
            FitTextAboveFooter(t);

            Button close = UiTheme.MakeButton(c, "离开 Sunny Tea", CloseTop, 21);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(0.5f, 0);
            close.rectTransform().pivot = new Vector2(0.5f, 0f);
            close.rectTransform().anchoredPosition = new Vector2(0, 6);
            close.rectTransform().sizeDelta = new Vector2(240, 50);
        }

        public void ShowMarketDashboard()
        {
            GameState s = GameState.I;
            if (s == null || !s.CompetitionActive) { ShowToast("市场中心将在企业篇完成、竞争企业开业后开放"); return; }

            GameObject root = BeginPanel("市场中心", 900, 800);
            RectTransform c = ContentOf(root);
            DayRecord r = s.LastRecord != null && s.LastRecord.competitionActive ? s.LastRecord : null;

            if (r == null)
            {
                Text empty = UiTheme.MakeText(c,
                    "Sunny Tea 今天刚开业。\n完成一天营业后，这里会出现双方销量、市场份额与价格走势。",
                    24, UiTheme.TextMain, TextAnchor.MiddleCenter);
                empty.rectTransform.Stretch();
                empty.rectTransform.offsetMin = new Vector2(30, 150);
                empty.rectTransform.offsetMax = new Vector2(-30, -30);
            }
            else
            {
                float avgPrice = (r.price * r.sold + r.competitorPrice * r.competitorSold) / Mathf.Max(1, r.sold + r.competitorSold);
                ShopText(c, "第 " + r.day + " 天 · 饮品市场", 26, UiTheme.TextMain,
                    10f, 0f, 520f, 38f, TextAnchor.MiddleLeft, FontStyle.Bold);
                ShopText(c, "价格、品质与品牌正在共同决定顾客去哪一家店", 15, UiTheme.TextSub,
                    10f, 36f, 620f, 24f, TextAnchor.MiddleLeft);

                BuildMarketShareBar(c, r, 10f, 68f, 832f);
                BuildDashboardFirmCard(c, true, r, 10f, 154f, 405f, 156f);
                BuildDashboardFirmCard(c, false, r, 437f, 154f, 405f, 156f);

                BuildDashboardMetric(c, "总市场需求", r.totalMarketDemand + " 人", 10f, 326f, 260f);
                BuildDashboardMetric(c, "加权平均价格", "¥ " + avgPrice.ToString("0.0"), 291f, 326f, 260f);
                BuildDashboardMetric(c, "今天未购买", r.outsideDemand + " 人", 572f, 326f, 270f);

                Image trendCard = UiTheme.MakeImage(c, new Color32(0x3b, 0x2e, 0x27, 245), "marketTrendCard");
                PlaceTopLeft(trendCard.rectTransform, 10f, 414f, 832f, 160f);
                ShopText(trendCard.rectTransform, "最近五天 · 份额与利润", 17, UiTheme.Accent,
                    16f, 7f, 800f, 26f, TextAnchor.MiddleLeft, FontStyle.Bold);
                ShopText(trendCard.rectTransform, BuildMarketTrend(s), 15, UiTheme.TextSub,
                    16f, 34f, 800f, 118f, TextAnchor.UpperLeft);

                if (!string.IsNullOrEmpty(r.competitionEventName))
                    ShopText(c, "今日竞争事件 · " + r.competitionEventName, 15, new Color32(0xff, 0xc0, 0x78, 255),
                        10f, 580f, 832f, 24f, TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            DashboardButton(c, "查看 Sunny Tea", 0, () => { CloseTop(); ShowCompetitorShop(); });
            DashboardButton(c, "竞争曲线", 1, () => { CloseTop(); ShowCurves(3); });
            DashboardButton(c, "策略实验室", 2, () => { CloseTop(); ShowStrategyLab(); });
            DashboardButton(c, "市场结构实验", 3, () => { CloseTop(); ShowMarketExperiments(); });
        }

        void BuildMarketShareBar(RectTransform parent, DayRecord r, float x, float y, float width)
        {
            Image track = UiTheme.MakeImage(parent, new Color32(0x25, 0x20, 0x1b, 255), "marketShareBar");
            PlaceTopLeft(track.rectTransform, x, y, width, 68f);
            float playerShare = Mathf.Clamp01(r.playerMarketShare);

            Image player = UiTheme.MakeImage(track.transform, UiTheme.Accent, "playerShare");
            player.rectTransform.anchorMin = new Vector2(0f, 0f);
            player.rectTransform.anchorMax = new Vector2(playerShare, 1f);
            player.rectTransform.offsetMin = Vector2.zero;
            player.rectTransform.offsetMax = Vector2.zero;

            Image rival = UiTheme.MakeImage(track.transform, new Color32(0x3f, 0x8b, 0x86, 255), "competitorShare");
            rival.rectTransform.anchorMin = new Vector2(playerShare, 0f);
            rival.rectTransform.anchorMax = new Vector2(1f, 1f);
            rival.rectTransform.offsetMin = Vector2.zero;
            rival.rectTransform.offsetMax = Vector2.zero;

            ShopText(track.rectTransform, "你的店  " + (r.playerMarketShare * 100f).ToString("0.0") + "%",
                20, new Color32(0x3b, 0x2b, 0x21, 255), 14f, 0f, width * 0.48f, 68f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(track.rectTransform, "Sunny Tea  " + (r.competitorMarketShare * 100f).ToString("0.0") + "%",
                20, Color.white, width * 0.5f, 0f, width * 0.48f - 14f, 68f,
                TextAnchor.MiddleRight, FontStyle.Bold);
        }

        void BuildDashboardFirmCard(RectTransform parent, bool player, DayRecord r,
            float x, float y, float width, float height)
        {
            Color32 color = player ? new Color32(0x4a, 0x38, 0x2c, 255) : new Color32(0x2e, 0x55, 0x56, 255);
            Image card = UiTheme.MakeImage(parent, color, player ? "playerMarketCard" : "competitorMarketCard");
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            ShopText(card.rectTransform, player ? "你的果汁店" : "Sunny Tea", 21, Color.white,
                16f, 8f, width - 32f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform,
                "售价  ¥ " + (player ? r.price : r.competitorPrice).ToString("0") +
                "     销量  " + (player ? r.sold : r.competitorSold) + " 杯\n" +
                "品牌  " + (player ? r.playerBrand : r.competitorBrand).ToString("0") +
                "     品质  " + (player ? r.playerQuality : r.competitorQuality).ToString("0"),
                17, UiTheme.TextMain, 16f, 46f, width - 32f, 62f, TextAnchor.UpperLeft);
            string footer = player
                ? "今日利润  ¥ " + r.profit.ToString("0") + "   ·   定价能力 " +
                    (r.playerBrand >= 70f ? "强" : r.playerBrand >= 45f ? "正在形成" : "弱")
                : "经营状况  " + r.competitorStatus + "   ·   真实利润不可见";
            ShopText(card.rectTransform, footer, 15,
                player ? new Color32(0xff, 0xc9, 0x69, 255) : new Color32(0xb8, 0xe0, 0xd9, 255),
                16f, 116f, width - 32f, 26f, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        void BuildDashboardMetric(RectTransform parent, string label, string value,
            float x, float y, float width)
        {
            Image card = UiTheme.MakeImage(parent, new Color32(0x3b, 0x2e, 0x27, 245), "marketMetric_" + label);
            PlaceTopLeft(card.rectTransform, x, y, width, 72f);
            ShopText(card.rectTransform, label, 14, UiTheme.TextSub,
                14f, 5f, width - 28f, 22f, TextAnchor.MiddleLeft);
            ShopText(card.rectTransform, value, 23, UiTheme.TextMain,
                14f, 26f, width - 28f, 38f, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        string BuildMarketTrend(GameState s)
        {
            StringBuilder sb = new StringBuilder();
            int start = Mathf.Max(0, s.history.Count - 5);
            bool any = false;
            for (int i = start; i < s.history.Count; i++)
            {
                DayRecord r = s.history[i];
                if (!r.competitionActive) continue;
                any = true;
                if (sb.Length > 0) sb.Append("\n");
                sb.Append("Day ").Append(r.day).Append("   你 ").Append((r.playerMarketShare * 100f).ToString("0"))
                    .Append("% : 对手 ").Append((r.competitorMarketShare * 100f).ToString("0")).Append("%")
                    .Append("   ·   利润 ¥ ").Append(r.profit.ToString("0"));
            }
            if (!any) sb.Append("完成一天竞争营业后出现");
            return sb.ToString();
        }

        void DashboardButton(RectTransform c, string label, int index, System.Action action)
        {
            Button b = UiTheme.MakeButton(c, label, action, 20);
            b.rectTransform().anchorMin = b.rectTransform().anchorMax = new Vector2(0.5f, 0);
            b.rectTransform().pivot = new Vector2(0.5f, 0f);
            b.rectTransform().anchoredPosition = new Vector2((index % 2 == 0 ? -210 : 210), 72 - (index / 2) * 56);
            b.rectTransform().sizeDelta = new Vector2(390, 46);
        }

        public void ShowCompetitionIntro()
        {
            GameState s = GameState.I;
            if (s == null) return;
            GameObject root = BeginPanel("第二篇 · 新的创业者", 760f, 640f);
            RectTransform c = ContentOf(root);
            const float width = 712f;
            float y = EventBrief(c, "镇东的店今天开业",
                "Sunny Tea 开业了。昨天以前，想喝饮料的人只能来你的店。\n\n" +
                "现在他们会比较三件事：价格是否划算，品质是否更好，品牌是否值得信任。\n\n" +
                "先营业一天，再去市场中心看原来的顾客去了哪里。",
                0f, 0f, width, UiTheme.Market) + 20f;
            Button ok = UiTheme.MakeButton(c, "迎接竞争", () =>
            {
                ProgressionService.RecordSignal(s, ProgressSignals.CompetitorOpened,
                    "Sunny Tea 开业", "饮品市场从单店进入双店竞争");
                ClosePanel(root);
            }, 22);
            PlaceTopLeft(ok.rectTransform(), (width - 240f) * .5f, y, 240f, 52f);
            FitEventPanel(root, y + 52f);
        }

        public void ShowPriceWarChoice()
        {
            GameState s = GameState.I;
            if (s == null || !s.CompetitionActive) return;
            SaveSystem.SaveKeyMoment(s, "关键选择：价格战");
            GameObject root = BeginPanel("市场警报 · Sunny Tea 突然降价", 760f, 720f);
            MarkMandatoryDecision(root);
            HideFalseClose(root);
            RectTransform c = ContentOf(root);
            const float width = 712f;
            float y = EventBrief(c, "两边现在的价格",
                "Sunny Tea 降到了 ¥" + s.competitor.price.ToString("0") +
                "，你的标价是 ¥" + PriceService.PlayerListedPrice(s).ToString("0") +
                "。保持不动的话，一部分客人会去对面。",
                0f, 0f, width, UiTheme.Accent) + 16f;

            StrategyChoiceButton(c, 0, y, "跟随降价", "抢回份额，但每杯利润变薄", () =>
            {
                s.price = s.competitor.price;
                s.todayStrategyChoice = PlayerCompetitionChoice.FollowPrice.ToString();
                s.Changed(); ResolveMandatoryDecision(root);
            });
            StrategyChoiceButton(c, 1, y, "保持价格", "接受部分顾客流失，守住单杯利润", () =>
            {
                s.todayStrategyChoice = PlayerCompetitionChoice.HoldPrice.ToString();
                s.Changed(); ResolveMandatoryDecision(root);
            });
            int nextQuality = Mathf.Min(3, s.playerQualityLevel + 1);
            float qualityCost = CompetitionEco.UpgradeCost(nextQuality);
            bool canUpgrade = s.playerQualityLevel < 3 && s.money >= qualityCost;
            Button qualityChoice = StrategyChoiceButton(c, 2, y, "提高品质",
                canUpgrade ? "用差异化回应 · 成本 ¥" + qualityCost.ToString("0") :
                    s.playerQualityLevel >= 3 ? "品质已满级" : "资金不足 · 需要 ¥" + qualityCost.ToString("0"), () =>
            {
                if (TryUpgradeQuality(s))
                {
                    s.todayStrategyChoice = PlayerCompetitionChoice.ImproveQuality.ToString();
                    s.Changed(); ResolveMandatoryDecision(root);
                }
            });
            qualityChoice.interactable = canUpgrade;
            bool canAdvertise = s.money >= 300f;
            Button advertiseChoice = StrategyChoiceButton(c, 3, y, "增加广告",
                canAdvertise ? "花 ¥300 提高品牌与客流" : "资金不足 · 需要 ¥300", () =>
            {
                s.marketingBudget = Mathf.Max(300f, s.marketingBudget);
                s.todayStrategyChoice = PlayerCompetitionChoice.Advertise.ToString();
                s.Changed(); ResolveMandatoryDecision(root);
            });
            advertiseChoice.interactable = canAdvertise;
            FitEventPanel(root, y + 4f * 100f);
        }

        Button StrategyChoiceButton(RectTransform c, int index, float top, string title, string desc, System.Action action)
        {
            return EventChoice(c, title, desc, 0f, top + index * 100f, 712f, action);
        }

        public void ShowStrategyLab()
        {
            GameState s = GameState.I;
            if (s == null || !s.CanStrategyLab) { ShowToast("策略实验室需推进到市场篇第 9 章"); return; }
            CompetitionSimulator.EstimatePayoffMatrix(s, out CompetitionSimulator.ProfitEstimate bothHold,
                out CompetitionSimulator.ProfitEstimate playerCuts, out CompetitionSimulator.ProfitEstimate competitorCuts,
                out CompetitionSimulator.ProfitEstimate bothCut);
            s.UnlockCard("gametheory");
            if (s.priceWarRound >= 1) s.UnlockCard("prisonersdilemma");

            GameObject root = BeginPanel("策略实验室 · 价格博弈", 920, 720);
            RectTransform c = ContentOf(root);
            ShopText(c, "收益矩阵 · 你的真实模型 / 对手估算", 24, UiTheme.TextMain,
                10f, 0f, 650f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(c, "同一组市场条件，只改变双方是否降价", 15, UiTheme.TextSub,
                10f, 34f, 650f, 24f, TextAnchor.MiddleLeft);

            ShopText(c, "对手保持", 17, UiTheme.TextSub,
                210f, 72f, 285f, 30f, TextAnchor.MiddleCenter, FontStyle.Bold);
            ShopText(c, "对手降价", 17, UiTheme.TextSub,
                517f, 72f, 285f, 30f, TextAnchor.MiddleCenter, FontStyle.Bold);
            ShopText(c, "你保持", 17, UiTheme.TextSub,
                18f, 126f, 160f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            ShopText(c, "你降价", 17, UiTheme.TextSub,
                18f, 248f, 160f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);

            BuildStrategyMatrixCell(c, "bothHold", bothHold, 190f, 108f,
                new Color32(0x2e, 0x55, 0x56, 255), "双方保持 · 合计通常更高");
            BuildStrategyMatrixCell(c, "competitorCuts", competitorCuts, 497f, 108f,
                new Color32(0x4a, 0x38, 0x2c, 255), "对手先降 · 你被动承压");
            BuildStrategyMatrixCell(c, "playerCuts", playerCuts, 190f, 230f,
                new Color32(0x4a, 0x38, 0x2c, 255), "你先降 · 有抢客诱惑");
            BuildStrategyMatrixCell(c, "bothCut", bothCut, 497f, 230f,
                new Color32(0x6b, 0x3b, 0x35, 255), "双方降价 · 囚徒困境");

            Image lesson = UiTheme.MakeImage(c, new Color32(0x3b, 0x2e, 0x27, 245), "strategyLesson");
            PlaceTopLeft(lesson.rectTransform, 18f, 366f, 784f, 160f);
            ShopText(lesson.rectTransform, "为什么明知不好，双方仍可能继续降价？", 19, UiTheme.Accent,
                16f, 8f, 752f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(lesson.rectTransform,
                "如果双方都保持价格，行业合计利润往往更高。\n" +
                "但只要一方相信对手会保持，它就有降价抢客的诱惑；对手也会这样想。\n" +
                "当双方都不愿单方面停手时，就会落入价格战的「囚徒困境」。",
                17, UiTheme.TextMain, 16f, 42f, 752f, 106f, TextAnchor.UpperLeft);

            ShopText(c, "矩阵会随你当前的价格、品质、品牌、广告和产能实时变化。", 14, UiTheme.TextSub,
                18f, 536f, 784f, 24f, TextAnchor.MiddleCenter);
            Button exp = UiTheme.MakeButton(c, "进入市场结构实验", () => { CloseTop(); ShowMarketExperiments(); }, 21);
            exp.rectTransform().anchorMin = exp.rectTransform().anchorMax = new Vector2(0.5f, 0);
            exp.rectTransform().pivot = new Vector2(0.5f, 0f);
            exp.rectTransform().anchoredPosition = new Vector2(0, 8);
            exp.rectTransform().sizeDelta = new Vector2(300, 50);
        }

        void BuildStrategyMatrixCell(RectTransform parent, string name,
            CompetitionSimulator.ProfitEstimate estimate, float x, float y, Color32 color, string caption)
        {
            Image cell = UiTheme.MakeImage(parent, color, "payoff_" + name);
            PlaceTopLeft(cell.rectTransform, x, y, 285f, 108f);
            ShopText(cell.rectTransform,
                "你  ¥ " + estimate.playerProfit.ToString("0") +
                "     对手估算  ¥ " + estimate.competitorProfit.ToString("0"),
                16, Color.white, 14f, 8f, 257f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(cell.rectTransform,
                "合计  ¥ " + (estimate.playerProfit + estimate.competitorProfit).ToString("0"),
                16, new Color32(0xff, 0xd0, 0x86, 255), 14f, 40f, 257f, 25f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(cell.rectTransform, caption, 14, new Color32(0xe3, 0xd2, 0xba, 255),
                14f, 70f, 257f, 28f, TextAnchor.MiddleLeft);
        }

        public void ShowMarketExperiments()
        {
            GameState s = GameState.I;
            if (s == null || !s.CompetitionActive) { ShowToast("竞争市场开放后才能进入实验"); return; }
            GameObject root = BeginPanel("市场结构实验", 760, 620);
            RectTransform c = ContentOf(root);
            Text intro = UiTheme.MakeText(c, "三个短实验使用相同的饮品，但改变市场里的企业数量与产品差异。", 22, UiTheme.TextSub, TextAnchor.UpperLeft);
            intro.rectTransform.anchorMin = intro.rectTransform.anchorMax = new Vector2(0, 1);
            intro.rectTransform.pivot = new Vector2(0, 1);
            intro.rectTransform.anchoredPosition = new Vector2(8, -4);
            intro.rectTransform.sizeDelta = new Vector2(700, 70);
            float introShift = ExpandTopText(intro);
            GrowPanel(root, introShift);

            ExperimentButton(c, 0, 90f + introShift, "完全竞争 · 8 家相同摊位", "市场价 10 元，你要卖 10 还是 15？",
                (s.marketExperimentMask & 1) != 0, () => { CloseTop(); ShowPerfectCompetitionChoice(); });
            ExperimentButton(c, 1, 90f + introShift, "寡头 · 只有 3 家公司", "一次降价会引发另外两家的回应",
                (s.marketExperimentMask & 2) != 0, () => { CloseTop(); ShowOligopolyChoice(); });
            ExperimentButton(c, 2, 90f + introShift, "垄断 · 只剩你的店", "提高价格会增加利润，也会减少消费者剩余",
                (s.marketExperimentMask & 4) != 0, () => { CloseTop(); ShowMonopolyChoice(); });
        }

        void ExperimentButton(RectTransform c, int index, float top, string title, string desc, bool completed, System.Action action)
        {
            string status = completed ? "  ✓ 已完成" : "";
            Button b = UiTheme.MakeButton(c, title + status + "\n<size=17>" + desc + "</size>", action, 21);
            b.rectTransform().anchorMin = b.rectTransform().anchorMax = new Vector2(0.5f, 1);
            b.rectTransform().pivot = new Vector2(0.5f, 1);
            b.rectTransform().anchoredPosition = new Vector2(0, -(top + index * 115));
            b.rectTransform().sizeDelta = new Vector2(650, 98);
            if (completed) b.image.color = new Color32(0x9d, 0xc2, 0xa7, 255);
        }

        void ShowPerfectCompetitionChoice()
        {
            GameObject root = BeginPanel("完全竞争实验 · 你的定价", 760, 650);
            RectTransform c = ContentOf(root);
            Text t = UiTheme.MakeText(c, "街上有 8 家完全相同的饮品摊，其他摊位统一卖 10 元。你的饮品与他们没有品质或品牌差异。", 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(8, -4);
            t.rectTransform.sizeDelta = new Vector2(696, 74);
            float shift = ExpandTopText(t);
            GrowPanel(root, shift);
            BuildParticipantStrip(c,
                new[] { "你\n¥10", "摊位 2\n¥10", "摊位 3\n¥10", "摊位 4\n¥10", "摊位 5\n¥10", "摊位 6\n¥10", "摊位 7\n¥10", "摊位 8\n¥10" }, 88f + shift);
            ExperimentChoice(c, 220f + shift, "卖 10 元 · 接受市场价", () => CompleteExperiment(1, "完全竞争实验结果",
                "你按市场价 10 元出售，预计仍能卖出约 80 杯。利润不高，但可以继续参与市场。\n\n在完全竞争里，单个企业是价格接受者。"));
            ExperimentChoice(c, 304f + shift, "卖 15 元 · 尝试自己定价", () => CompleteExperiment(1, "完全竞争实验结果",
                "你的价格比另外 7 家高 50%，预计销量接近 0。顾客无需牺牲品质，就能立刻去隔壁购买。\n\n完全相同的产品让单个企业几乎没有定价能力。"));
            ExperimentFootnote(c, "8 家 · 产品相同 · 单个摊位几乎没有定价权", 402f + shift);
        }

        void ShowOligopolyChoice()
        {
            GameObject root = BeginPanel("寡头实验 · 只有三家公司", 760, 650);
            RectTransform c = ContentOf(root);
            Text t = UiTheme.MakeText(c, "市场只有你、A 公司和 B 公司，三家当前都卖 18 元。你可以保持，也可以突然降到 14 元抢客。", 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(8, -4);
            t.rectTransform.sizeDelta = new Vector2(696, 74);
            float shift = ExpandTopText(t);
            GrowPanel(root, shift);
            BuildParticipantStrip(c, new[] { "你\n¥18", "A 公司\n¥18", "B 公司\n¥18" }, 88f + shift);
            ExperimentChoice(c, 220f + shift, "保持 18 元", () => CompleteExperiment(2, "寡头实验结果",
                "三家都保持价格，份额约各占三分之一，利润相对稳定。但任何一家都有偷偷降价抢客的诱惑。\n\n少数企业彼此依赖，这就是寡头市场。"));
            ExperimentChoice(c, 304f + shift, "降到 14 元抢客", () => CompleteExperiment(2, "寡头实验结果",
                "你第一轮可能抢到约一半顾客；下一轮 A、B 两家公司全部跟随降价。你的份额回落，三家利润一起下降。\n\n在寡头市场，一家企业的行动会触发其他企业回应。"));
            ExperimentFootnote(c, "3 家 · 决策相互依赖 · 降价会触发连锁回应", 402f + shift);
        }

        void ShowMonopolyChoice()
        {
            GameObject root = BeginPanel("垄断实验 · 唯一供应者", 760, 650);
            RectTransform c = ContentOf(root);
            Text t = UiTheme.MakeText(c, "Sunny Tea 暂时退出市场，你成为唯一饮品供应者。很多居民最高愿意支付 20 元。你准备卖多少钱？", 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(8, -4);
            t.rectTransform.sizeDelta = new Vector2(696, 74);
            float shift = ExpandTopText(t);
            GrowPanel(root, shift);
            BuildParticipantStrip(c, new[] { "唯一供应者\n你的店" }, 88f + shift);
            ExperimentChoice(c, 220f + shift, "卖 15 元 · 更多居民买得到", () => CompleteExperiment(4, "垄断实验结果",
                "愿付 20 元的居民只支付 15 元，每杯获得 5 元消费者剩余。若最低供给成本为 8 元，生产者剩余约为 7 元/杯。\n\n较低价格让更多交易发生，总福利更高。"));
            ExperimentChoice(c, 304f + shift, "卖 25 元 · 使用定价权", () => CompleteExperiment(4, "垄断实验结果",
                "售价提高后单杯利润增加，但愿付价格只有 20 元的居民会退出，消费者剩余降到 0，销量也会下降。\n\n垄断带来定价能力，也可能减少消费者福利。"));
            ExperimentFootnote(c, "1 家 · 拥有定价权 · 利润与消费者福利发生冲突", 402f + shift);
        }

        void ExperimentChoice(RectTransform c, float y, string label, System.Action action)
        {
            Button b = UiTheme.MakeButton(c, label, action, 21);
            b.rectTransform().anchorMin = b.rectTransform().anchorMax = new Vector2(0.5f, 1);
            b.rectTransform().pivot = new Vector2(0.5f, 1);
            b.rectTransform().anchoredPosition = new Vector2(0, -y);
            b.rectTransform().sizeDelta = new Vector2(580, 68);
        }

        void BuildParticipantStrip(RectTransform parent, string[] labels, float y)
        {
            const float width = 696f;
            Image strip = UiTheme.MakeImage(parent, new Color32(0x25, 0x20, 0x1b, 255), "experimentParticipants");
            PlaceTopLeft(strip.rectTransform, 8f, y, width, 108f);

            float gap = labels.Length >= 6 ? 6f : 12f;
            float usableWidth = labels.Length == 1 ? 220f : width - 24f;
            float cellWidth = (usableWidth - gap * (labels.Length - 1)) / labels.Length;
            float startX = (width - usableWidth) * 0.5f;
            for (int i = 0; i < labels.Length; i++)
            {
                Color32 color = i == 0 ? UiTheme.Accent : new Color32(0x3f, 0x8b, 0x86, 255);
                Color textColor = i == 0 ? new Color32(0x3b, 0x2b, 0x21, 255) : Color.white;
                Image firm = UiTheme.MakeImage(strip.transform, color, "firm_" + i);
                PlaceTopLeft(firm.rectTransform, startX + i * (cellWidth + gap), 12f, cellWidth, 84f);
                ShopText(firm.rectTransform, labels[i], labels.Length >= 6 ? 14 : 18, textColor,
                    4f, 4f, cellWidth - 8f, 76f, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        void ExperimentFootnote(RectTransform parent, string value, float y = 402f)
        {
            Image note = UiTheme.MakeImage(parent, new Color32(0x3b, 0x2e, 0x27, 245), "experimentFootnote");
            PlaceTopLeft(note.rectTransform, 68f, y, 580f, 54f);
            ShopText(note.rectTransform, value, 16, UiTheme.TextSub,
                12f, 0f, 556f, 54f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void CompleteExperiment(int mask, string title, string body)
        {
            GameState s = GameState.I;
            s.marketExperimentMask |= mask;
            if (mask == 1)
            {
                s.UnlockCard("perfectcompetition");
                KnowledgeSystem.MarkExperimented(s, "perfectcompetition");
            }
            else if (mask == 2)
            {
                s.UnlockCard("oligopoly");
                KnowledgeSystem.MarkExperimented(s, "oligopoly", "gametheory");
            }
            else
            {
                s.UnlockCard("monopoly"); s.UnlockCard("consumersurplus"); s.UnlockCard("producersurplus");
                KnowledgeSystem.MarkExperimented(s, "monopoly", "consumersurplus", "producersurplus");
            }
            if (s.CanStrategyLab && s.CompletedMarketExperiments >= 2)
                s.UnlockCard("nashequilibrium");
            ProgressionService.RecordSignal(s, ProgressSignals.MarketExperiment,
                "完成市场结构实验", title);
            s.Changed();
            CloseTop();
            ShowInfoPopup(title, body);
        }

        public void ShowCounterfactual(DayRecord r)
        {
            if (r == null || !r.competitionActive) return;
            string body =
                "实际决策：\n价格 " + r.price.ToString("0") + " · 销量 " + r.sold + " · 市场份额 " + (r.playerMarketShare * 100f).ToString("0.0") + "% · 利润 " + r.profit.ToString("0") + "\n\n" +
                "如果保持上一价格 " + r.counterfactualPrice.ToString("0") + " 元：\n预计销量 " + r.counterfactualSold + " · 预计利润 " + r.counterfactualProfit.ToString("0") + "\n\n" +
                "这是基于当天对手、品质、品牌与市场情况的简化反事实估算。\n" +
                "抢到更多市场份额，不一定意味着赚到更多利润。";
            ShowInfoPopup("如果昨天做了另一种选择？", body);
        }

        public void ShowCompetitionWhy(DayRecord r)
        {
            if (r == null || !r.competitionActive) return;
            GameState s = GameState.I;
            DayRecord previous = s.Yesterday;
            StringBuilder sb = new StringBuilder();
            if (previous != null && previous.competitionActive)
            {
                if (r.competitorPrice < previous.competitorPrice)
                    sb.Append("你的价格没有单独决定今天的销量。Sunny Tea 从 ").Append(previous.competitorPrice.ToString("0"))
                        .Append(" 元降到 ").Append(r.competitorPrice.ToString("0")).Append(" 元，部分居民转向了对手。\n\n");
                else if (r.competitorPrice > previous.competitorPrice)
                    sb.Append("Sunny Tea 涨价后，你的商品相对更有吸引力，一部分顾客转向了你的店。\n\n");
            }
            sb.Append("今天有约 ").Append(r.switchedToCompetitor).Append(" 位新增顾客流向 Sunny Tea。")
                .Append("这体现了替代品之间的竞争关系。\n\n")
                .Append("你的份额 ").Append((r.playerMarketShare * 100f).ToString("0.0")).Append("%，对手份额 ")
                .Append((r.competitorMarketShare * 100f).ToString("0.0")).Append("%。");
            if (r.hasCrossPriceElasticity)
                sb.Append("\n\n交叉价格弹性约为 ").Append(r.crossPriceElasticity.ToString("0.00")).Append("：你的价格变化正在影响对手销量。");
            if (!string.IsNullOrEmpty(r.competitionEventName))
                sb.Append("\n\n今天还有竞争事件「").Append(r.competitionEventName).Append("」，因此不能把全部变化都归因于价格。");
            ShowInfoPopup("为什么顾客去了别人家？", sb.ToString());
        }
    }
}
