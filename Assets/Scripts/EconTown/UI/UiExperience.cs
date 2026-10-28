using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        public void ShowTodayBriefing()
        {
            GameState state = GameState.I;
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            GameObject root = BeginPanel("今日待办 · 经济新闻", 1040f, 760f);
            RectTransform content = ContentOf(root);

            LongTermEventRecord currentEvent = state.experience.todayLongTermEvent;
            float briefH = EventBrief(content,
                currentEvent == null ? "今天没有新的长期事件" : currentEvent.title,
                currentEvent == null
                    ? "企业、居民、政府与宏观仍在照常运行。"
                    : currentEvent.effect + "\n" + currentEvent.cause,
                8f, 0f, 976f, currentEvent == null ? UiTheme.Market : UiTheme.Accent);
            float y = briefH + 16f;

            RectTransform todos = ExperienceCard(content, "今天可以做的事", 8f, y, 478f, 300f, UiTheme.Market);
            IList<string> todoItems = LongTermExperienceService.BuildTodayTodos(state);
            StringBuilder todoText = new StringBuilder();
            for (int i = 0; i < todoItems.Count; i++) todoText.Append("• ").Append(todoItems[i]).Append('\n').Append('\n');
            ExperienceText(todos, todoText.ToString(), 18, 18f, 42f, 442f, 256f, UiTheme.TextMain);

            RectTransform news = ExperienceCard(content, "最新新闻", 506f, y, 478f, 300f, UiTheme.Accent);
            if (state.experience.news.Count == 0)
                ExperienceText(news, "完成几次结算后，这里会根据真实利润、通胀、失业、债务与事件生成新闻。",
                    18, 18f, 54f, 442f, 90f, UiTheme.TextSub);
            else
            {
                int visible = Mathf.Min(4, state.experience.news.Count);
                for (int i = 0; i < visible; i++)
                {
                    EconomicNewsItem item = state.experience.news[i];
                    Button button = UiTheme.MakeButton(news, "Day " + item.worldDay + " · " + item.headline,
                        () => OpenExperienceTarget(item.viewTarget), 16);
                    PlaceTopLeft(button.rectTransform(), 14f, 44f + i * 63f, 450f, 52f);
                    UiTheme.SetButtonStyle(button, new Color32(0x3c, 0x32, 0x2b, 255), UiTheme.TextMain);
                }
            }

            string crisisLine = "企业  " + (state.experience.enterpriseRecovery.active
                ? state.experience.enterpriseRecovery.latestStatus : "正常") +
                "\n居民  " + (state.experience.residentCrisisActive ? "生活危机" : "正常") +
                "    财政  " + (state.experience.fiscalPressureActive ? "压力" : "正常") +
                "    宏观  " + state.experience.macroCrisisStatus;
            float statusY = y + 316f;
            RectTransform status = ExperienceCard(content, "危机与恢复", 8f, statusY, 976f, 108f,
                state.experience.enterpriseRecovery.active || state.experience.residentCrisisActive ? UiTheme.Bad : UiTheme.Good);
            ExperienceText(status, crisisLine, 17, 16f, 36f, 944f, 62f, UiTheme.TextMain);

            float buttonY = statusY + 124f;
            Button allNews = UiTheme.MakeButton(content, "新闻中心", () => { ClosePanel(root); ShowNewsCenter(); }, 18);
            PlaceTopLeft(allNews.rectTransform(), 100f, buttonY, 180f, 48f);
            Button history = UiTheme.MakeButton(content, "小镇经济史", () => { ClosePanel(root); ShowEconomicHistory(); }, 18);
            PlaceTopLeft(history.rectTransform(), 302f, buttonY, 180f, 48f);
            Button crisis = UiTheme.MakeButton(content,
                state.experience.residentCrisisActive ? "处理生活危机" : state.experience.enterpriseRecovery.active ? "查看恢复计划" : "危机复盘",
                () =>
                {
                    ClosePanel(root);
                    if (state.experience.residentCrisisActive) ShowResidentCrisis();
                    else if (state.experience.enterpriseRecovery.active) ShowFinancialCrisis();
                    else ShowCrisisReviewArchive();
                }, 18);
            PlaceTopLeft(crisis.rectTransform(), 504f, buttonY, 190f, 48f);
            Button close = UiTheme.MakeButton(content, "返回小镇", () => ClosePanel(root), 18);
            PlaceTopLeft(close.rectTransform(), 716f, buttonY, 180f, 48f);
            FitEventPanel(root, buttonY + 48f);
        }

        public void ShowNewsCenter(int filter = -1)
        {
            GameState state = GameState.I;
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            GameObject root = BeginPanel("经济新闻中心", 1040f, 820f);
            RectTransform content = ContentOf(root);
            string[] labels = { "全部", "企业", "居民", "政策", "宏观", "特殊" };
            for (int i = 0; i < labels.Length; i++)
            {
                int selected = i - 1;
                Button tab = UiTheme.MakeButton(content, labels[i], () =>
                {
                    ClosePanel(root);
                    ShowNewsCenter(selected);
                }, 17);
                PlaceTopLeft(tab.rectTransform(), 8f + i * 163f, 0f, 151f, 42f);
                UiTheme.SetButtonStyle(tab, filter == selected ? UiTheme.Accent : UiTheme.Paper,
                    filter == selected ? UiTheme.Ink : UiTheme.Ink);
                tab.interactable = filter != selected;
            }

            int row = 0;
            for (int i = 0; i < state.experience.news.Count && row < 5; i++)
            {
                EconomicNewsItem item = state.experience.news[i];
                if (filter >= 0 && (int)item.category != filter) continue;
                RectTransform card = ExperienceCard(content, "Day " + item.worldDay + " · " + CategoryName(item.category),
                    8f, 58f + row * 112f, 976f, 100f, NewsColor(item.category));
                Text line = ExperienceText(card, "<b>" + item.headline + "</b>\n" + item.detail,
                    16, 14f, 28f, 760f, 64f, UiTheme.TextMain);
                line.verticalOverflow = VerticalWrapMode.Overflow;
                Button view = UiTheme.MakeButton(card, "查看数据", () => OpenExperienceTarget(item.viewTarget), 15);
                PlaceTopLeft(view.rectTransform(), 800f, 30f, 156f, 42f);
                row++;
            }
            if (row == 0)
                ExperienceText(content, "当前分类还没有新闻。新闻只会在真实状态满足条件时出现。",
                    20, 120f, 220f, 752f, 80f, UiTheme.TextSub, TextAnchor.MiddleCenter);
            Button close = UiTheme.MakeButton(content, "返回", () => ClosePanel(root), 18);
            PlaceTopLeft(close.rectTransform(), 396f, 640f, 200f, 48f);
        }

        public void ShowEconomicHistory(int tab = 0)
        {
            GameState state = GameState.I;
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            GameObject root = BeginPanel("小镇经济史", 920f, 720f);
            RectTransform content = ContentOf(root);
            // V0.7.4 双页签:0 编年(原样) / 1 大事记(过场归档,§150 可无限回看)
            string[] tabs = { "编年", "大事记" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int selected = i;
                Button tabButton = UiTheme.MakeButton(content, tabs[i], () =>
                {
                    ClosePanel(root);
                    ShowEconomicHistory(selected);
                }, 18);
                PlaceTopLeft(tabButton.rectTransform(), 8f + i * 128f, 0f, 118f, 42f);
                UiTheme.SetButtonStyle(tabButton, tab == selected ? UiTheme.Accent : UiTheme.Paper, UiTheme.Ink);
                tabButton.interactable = tab != selected;
            }
            if (tab == 1)
            {
                BuildChronicleTab(content, state, root);
                return;
            }
            StringBuilder body = new StringBuilder();
            int start = Mathf.Max(0, state.experience.economicHistory.Count - 14);
            for (int i = state.experience.economicHistory.Count - 1; i >= start; i--)
            {
                EconomicHistoryEntry entry = state.experience.economicHistory[i];
                body.Append("<b>Day ").Append(entry.worldDay).Append("　").Append(entry.title).Append("</b>\n")
                    .Append(entry.summary).Append("\n\n");
            }
            if (body.Length == 0) body.Append("重要的开业、价格战、政策、冲击、危机与复苏会按真实发生顺序记录在这里。");
            Image scrollArea = UiTheme.MakeImage(content, new Color32(0x32, 0x2b, 0x27, 255), "historyScroll");
            PlaceTopLeft(scrollArea.rectTransform, 18f, 54f, 836f, 500f);
            scrollArea.raycastTarget = true;
            scrollArea.gameObject.AddComponent<RectMask2D>();
            GameObject historyObject = new GameObject("historyContent", typeof(RectTransform));
            historyObject.transform.SetParent(scrollArea.transform, false);
            RectTransform historyContent = historyObject.GetComponent<RectTransform>();
            historyContent.anchorMin = new Vector2(0f, 1f);
            historyContent.anchorMax = new Vector2(1f, 1f);
            historyContent.pivot = new Vector2(0.5f, 1f);
            historyContent.anchoredPosition = Vector2.zero;
            Text history = UiTheme.MakeText(historyContent, body.ToString(), 18, UiTheme.TextMain, TextAnchor.UpperLeft);
            history.horizontalOverflow = HorizontalWrapMode.Wrap;
            history.verticalOverflow = VerticalWrapMode.Overflow;
            history.rectTransform.anchorMin = new Vector2(0f, 1f);
            history.rectTransform.anchorMax = new Vector2(1f, 1f);
            history.rectTransform.pivot = new Vector2(0.5f, 1f);
            history.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            history.rectTransform.sizeDelta = new Vector2(-28f, 100f);
            float historyHeight = Mathf.Max(476f, MeasuredTextHeight(history));
            history.rectTransform.sizeDelta = new Vector2(-28f, historyHeight);
            historyContent.sizeDelta = new Vector2(0f, historyHeight + 24f);
            ScrollRect historyScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            historyScroll.viewport = scrollArea.rectTransform;
            historyScroll.content = historyContent;
            historyScroll.horizontal = false;
            historyScroll.vertical = true;
            historyScroll.movementType = ScrollRect.MovementType.Clamped;
            historyScroll.scrollSensitivity = 32f;
            historyScroll.verticalNormalizedPosition = 1f;
            Button close = UiTheme.MakeButton(content, "返回", () => ClosePanel(root), 19);
            PlaceTopLeft(close.rectTransform(), 330f, 570f, 220f, 50f);
        }

        public void ShowResidentCrisis()
        {
            GameState state = GameState.I;
            if (state == null || !state.experience.residentCrisisActive) return;
            GameObject root = BeginPanel("居民生活危机 · 选择恢复路径", 860f, 620f);
            MarkMandatoryDecision(root);
            HideFalseClose(root);
            RectTransform content = ContentOf(root);
            ResidentState resident = state.resident;
            const float width = 812f;
            float y = EventBrief(content, "家里同时缺钱、缺吃的、也没班上",
                "现金 ¥" + resident.cash.ToString("0") + "    储蓄 ¥" + resident.savings.ToString("0") +
                "    吃饱程度 " + resident.satiety.ToString("0") + "    失业 " + resident.unemployedDays + " 天\n" +
                "存档还在。用时间、存款，或者政府的一笔钱，先把这一天撑过去。",
                0f, 0f, width, UiTheme.Bad) + 16f;
            float col = (width - 16f) * .5f;
            EventChoice(content, "找一份临时兼职", "现金 +¥35，精力 -12", 0f, y, col, () => ResolveResidentCrisis(root, "gig"));
            EventChoice(content, "动用储蓄", "最多取出 ¥120", col + 16f, y, col, () => ResolveResidentCrisis(root, "savings"));
            EventChoice(content, "申请临时福利", "居民 +¥80，政府预算 -¥80", 0f, y + 100f, col, () => ResolveResidentCrisis(root, "welfare"));
            EventChoice(content, "先只买必需", "先把吃饱和情绪拉回来", col + 16f, y + 100f, col, () => ResolveResidentCrisis(root, "cut"));
            FitEventPanel(root, y + 200f);
        }

        void ResolveResidentCrisis(GameObject root, string action)
        {
            LongTermExperienceService.ApplyResidentRecovery(GameState.I, action);
            ResolveMandatoryDecision(root);
            ShowToast(GameState.I.experience.residentCrisisActive ? "状态有所改善，但生活危机仍未解除" : "居民已经脱离生活危机");
        }

        public void ShowCrisisReview(CrisisReviewRecord review)
        {
            if (review == null) return;
            GameObject root = BeginPanel("危机复盘 · " + review.kind, 820f, 560f);
            RectTransform content = ContentOf(root);
            const float width = 772f;
            float y = EventBrief(content, "危机发生", "Day " + review.startedWorldDay + "\n" + review.cause,
                0f, 0f, width, UiTheme.Bad) + 12f;
            y += EventBrief(content, "你做了什么", review.recovery, 0f, y, width, UiTheme.Accent) + 12f;
            y += EventBrief(content, "结果", "Day " + review.recoveredWorldDay + "\n" + review.result,
                0f, y, width, UiTheme.Good) + 20f;
            Button close = UiTheme.MakeButton(content, "看完了", () =>
            {
                if (GameState.I.experience.pendingCrisisReview == review)
                    GameState.I.experience.pendingCrisisReview = null;
                ClosePanel(root);
            }, 20);
            PlaceTopLeft(close.rectTransform(), (width - 240f) * .5f, y, 240f, 52f);
            FitEventPanel(root, y + 52f);
        }

        void ShowCrisisReviewArchive()
        {
            GameState state = GameState.I;
            if (state == null) return;
            if (state.experience.crisisReviews.Count == 0)
            {
                ShowInfoPopup("危机复盘", "目前还没有完成的危机恢复记录。危机发生后，系统会记录成因、采取的行动和恢复结果。");
                return;
            }
            ShowCrisisReview(state.experience.crisisReviews[state.experience.crisisReviews.Count - 1]);
        }

        void OpenExperienceTarget(string target)
        {
            CloseAll();
            if (target == "resident")
            {
                if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Resident))
                    ShowToast(MainlineGuide.LockReason(PlayerViewMode.Resident));
                else ShowResidentHome();
            }
            else if (target == "government")
            {
                if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Government))
                    ShowToast(MainlineGuide.LockReason(PlayerViewMode.Government));
                else ShowGovernmentDashboard();
            }
            else if (target == "macro")
            {
                if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Macro))
                    ShowToast(MainlineGuide.LockReason(PlayerViewMode.Macro));
                else ShowMacroDashboard();
            }
            else ShowShop();
        }

        RectTransform ExperienceCard(RectTransform parent, string title, float x, float y, float width, float height, Color accent)
        {
            Image card = UiTheme.MakeImage(parent, new Color32(0x3b, 0x31, 0x2a, 255), "experienceCard_" + title);
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            Image stripe = UiTheme.MakeImage(card.transform, accent, "stripe");
            PlaceTopLeft(stripe.rectTransform, 0f, 0f, 6f, height);
            ExperienceText(card.rectTransform, title, 16, 16f, 5f, width - 28f, 26f, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            return card.rectTransform;
        }

        Text ExperienceText(RectTransform parent, string value, int size, float x, float y, float width, float height,
            Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            Text text = UiTheme.MakeText(parent, value, size, color, anchor, style);
            PlaceTopLeft(text.rectTransform, x, y, width, height);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static string CategoryName(NewsCategory category)
        {
            if (category == NewsCategory.Enterprise) return "企业";
            if (category == NewsCategory.Resident) return "居民";
            if (category == NewsCategory.Government) return "政策";
            if (category == NewsCategory.Macro) return "宏观";
            return "特殊事件";
        }

        static Color NewsColor(NewsCategory category)
        {
            if (category == NewsCategory.Enterprise) return UiTheme.Accent;
            if (category == NewsCategory.Resident) return new Color32(0x6b, 0xb7, 0xae, 255);
            if (category == NewsCategory.Government) return new Color32(0x79, 0xae, 0x88, 255);
            if (category == NewsCategory.Macro) return new Color32(0x78, 0xa9, 0xe6, 255);
            return UiTheme.Bad;
        }
    }
}
