using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>商店经营面板 / 原料市场 / 广场公告(见 PRD §27)。</summary>
    public partial class UIManager
    {
        /// <summary>通用信息弹窗(标题 + 正文)。</summary>
        public void ShowInfoPopup(string title, string body)
        {
            GameObject root = BeginPanel(title, 640, 420);
            RectTransform c = ContentOf(root);
            Text t = UiTheme.MakeText(c, body, 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(8f, 72f);
            t.rectTransform.offsetMax = new Vector2(-8f, -8f);
            Button ok = UiTheme.MakeButton(c, "知道了", CloseTop, 22);
            ok.rectTransform().anchorMin = ok.rectTransform().anchorMax = new Vector2(0.5f, 0);
            ok.rectTransform().pivot = new Vector2(0.5f, 0f);
            ok.rectTransform().anchoredPosition = new Vector2(0, 6);
            ok.rectTransform().sizeDelta = new Vector2(200, 52);
        }

        Text RowLabel(RectTransform c, string label, float y, int size = 24)
        {
            Text t = UiTheme.MakeText(c, label, size, UiTheme.TextMain, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(6, -y);
            t.rectTransform.sizeDelta = new Vector2(220, 40);
            return t;
        }

        Button SquareBtn(RectTransform c, string label, System.Action onClick, float x, float y, float w = 54, float h = 46, int fs = 26)
        {
            Button b = UiTheme.MakeButton(c, label, onClick, fs);
            b.rectTransform().anchorMin = b.rectTransform().anchorMax = new Vector2(0, 1);
            b.rectTransform().pivot = new Vector2(0, 1);
            b.rectTransform().anchoredPosition = new Vector2(x, -y);
            b.rectTransform().sizeDelta = new Vector2(w, h);
            return b;
        }

        Text ValueText(RectTransform c, string s, float x, float y, float w, Color? col = null, int fs = 24)
        {
            Text t = UiTheme.MakeText(c, s, fs, col ?? UiTheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            t.rectTransform.sizeDelta = new Vector2(w, 40);
            return t;
        }

        static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        Text ShopText(RectTransform parent, string value, int size, Color color,
            float x, float y, float w, float h, TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            Text text = UiTheme.MakeText(parent, value, size, color, alignment, style);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK 的行高会略大于字号；经营卡片的 18–30px 紧凑标签若截断，
            // WebGL 会把整行裁掉。长段落在各自创建处仍可显式改回 Truncate。
            text.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTopLeft(text.rectTransform, x, y, w, h);
            return text;
        }

        RectTransform ShopCard(RectTransform parent, string name, float x, float y, float w, float h)
        {
            Image card = UiTheme.MakeImage(parent, new Color32(0x4a, 0x38, 0x2c, 255), name);
            card.sprite = null;
            card.type = Image.Type.Simple;
            PlaceTopLeft(card.rectTransform, x, y, w, h);
            return card.rectTransform;
        }

        void ShopCardHeader(RectTransform card, string title, string caption)
        {
            Image marker = UiTheme.MakeImage(card, UiTheme.Accent, "sectionMarker");
            PlaceTopLeft(marker.rectTransform, 18f, 10f, 5f, 34f);
            ShopText(card, title, 22, UiTheme.TextMain, 36f, 4f,
                card.sizeDelta.x - 54f, 28f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card, caption, 13, UiTheme.TextSub, 36f, 29f,
                card.sizeDelta.x - 54f, 18f, TextAnchor.MiddleLeft);
        }

        RectTransform ShopRow(RectTransform card, float y)
        {
            Image row = UiTheme.MakeImage(card, new Color32(0x3b, 0x2e, 0x27, 245), "controlRow");
            PlaceTopLeft(row.rectTransform, 16f, y, card.sizeDelta.x - 32f, 56f);
            return row.rectTransform;
        }

        Text ShopStepper(RectTransform card, string label, string note, float y, string value,
            System.Action onMinus, System.Action onPlus)
        {
            RectTransform row = ShopRow(card, y);
            float width = row.sizeDelta.x;
            ShopText(row, label, 21, UiTheme.TextMain, 14f, 2f, 260f, 30f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(row, note, 14, UiTheme.TextSub, 14f, 29f, 280f, 20f);

            Button minus = UiTheme.MakeButton(row, "-", onMinus, 24);
            PlaceTopLeft(minus.rectTransform(), width - 176f, 6f, 42f, 44f);
            Text valueText = ShopText(row, value, 22, UiTheme.Accent,
                width - 130f, 6f, 80f, 44f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Button plus = UiTheme.MakeButton(row, "+", onPlus, 24);
            PlaceTopLeft(plus.rectTransform(), width - 46f, 6f, 42f, 44f);
            return valueText;
        }

        Text ShopActionRow(RectTransform card, string label, string note, float y, string value,
            string actionLabel, System.Action onAction, out Button actionButton)
        {
            RectTransform row = ShopRow(card, y);
            float width = row.sizeDelta.x;
            ShopText(row, label, 21, UiTheme.TextMain, 14f, 2f, 230f, 30f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(row, note, 14, UiTheme.TextSub, 14f, 29f, 260f, 20f);
            Text valueText = ShopText(row, value, 20, UiTheme.Accent,
                width - 286f, 6f, 110f, 44f, TextAnchor.MiddleCenter, FontStyle.Bold);
            actionButton = UiTheme.MakeButton(row, actionLabel, onAction, 17);
            PlaceTopLeft(actionButton.rectTransform(), width - 168f, 7f, 160f, 42f);
            return valueText;
        }

        void ShopLockedRow(RectTransform card, string label, string reason, float y)
        {
            RectTransform row = ShopRow(card, y);
            float width = row.sizeDelta.x;
            ShopText(row, label, 20, new Color32(0xc8, 0xb2, 0x94, 255),
                14f, 3f, 290f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(row, "完成前几天的经营后开放", 14, new Color32(0x91, 0x7d, 0x68, 255),
                14f, 30f, 300f, 19f);
            Image badge = UiTheme.MakeImage(row, new Color32(0x65, 0x50, 0x40, 255), "unlockBadge");
            PlaceTopLeft(badge.rectTransform, width - 134f, 11f, 122f, 34f);
            ShopText(badge.rectTransform, reason, 16, UiTheme.TextSub,
                0f, 0f, 122f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        public void ShowShop()
        {
            GameState s = GameState.I;
            if (s == null) return;
            SyncCurrentView(s, PlayerViewMode.Enterprise);
            if (s.phase == GamePhase.Report) { ShowReport(s.LastRecord); return; }
            if (s.phase != GamePhase.Morning) { ShowToast("现在不是营业准备时间"); return; }

            bool hasEvent = !string.IsNullOrEmpty(s.todayEventName) ||
                !string.IsNullOrEmpty(s.todayCompetitionEventName);
            float panelHeight = 710f;
            if (hasEvent) panelHeight += 60f;
            if (s.CompetitionActive) panelHeight += 60f;
            GameObject root = BeginPanel("果汁店 · 今日经营", 1120, panelHeight);
            RectTransform c = ContentOf(root);

            Text prodVal = null;
            Text empVal = null;
            Text materialVal = null;
            Text qualityVal = null;
            Text marketingVal = null;
            Button qualityButton = null;

            ShopText(c, "第 " + s.day + " 天 · 开店准备", 30, UiTheme.TextMain,
                0f, 0f, 620f, 38f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(c, "设置售价、备货和店务，确认后开门迎客", 14, UiTheme.TextSub,
                0f, 36f, 620f, 22f, TextAnchor.MiddleLeft);

            Image cashChip = UiTheme.MakeImage(c, UiTheme.Accent, "cashChip");
            PlaceTopLeft(cashChip.rectTransform, 812f, 4f, 260f, 48f);
            Text cashValue = ShopText(cashChip.rectTransform, "", 21,
                new Color32(0x3b, 0x2b, 0x21, 255), 0f, 0f, 260f, 48f,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            float cardsY = 64f;
            if (hasEvent)
            {
                string eventName = !string.IsNullOrEmpty(s.todayEventName)
                    ? s.todayEventName : s.todayCompetitionEventName;
                Image eventBanner = UiTheme.MakeImage(c, new Color32(0x9d, 0x4d, 0x3f, 255), "todayEvent");
                PlaceTopLeft(eventBanner.rectTransform, 0f, cardsY, 1072f, 42f);
                ShopText(eventBanner.rectTransform, "今日事件 · " + eventName + "  —  广场公告有详细说明",
                    18, Color.white, 16f, 0f, 1040f, 42f, TextAnchor.MiddleLeft, FontStyle.Bold);
                cardsY += 54f;
            }

            const float leftWidth = 640f;
            const float rightX = 660f;
            const float rightWidth = 412f;

            RectTransform menuCard = ShopCard(c, "menuCard", 0f, cardsY, leftWidth, 190f);
            ShopCardHeader(menuCard, "今日菜单", "先定价格，再决定备货");
            System.Func<string> priceDisplay = () =>
            {
                float effective = PriceService.PlayerListedPrice(s);
                return Mathf.Approximately(effective, s.price)
                    ? "¥ " + s.price.ToString("0")
                    : "基础 ¥" + s.price.ToString("0") + " · 今日 ¥" + effective.ToString("0");
            };
            Text priceVal = ShopStepper(menuCard, "鲜榨果汁售价",
                "每杯售价，范围 " + CompetitionEco.MinPrice.ToString("0") + "—" + CompetitionEco.MaxPrice.ToString("0") + " 元", 50f,
                priceDisplay(),
                () => { s.price = Mathf.Max(CompetitionEco.MinPrice, s.price - 1f); s.Changed(); },
                () => { s.price = Mathf.Min(CompetitionEco.MaxPrice, s.price + 1f); s.Changed(); });

            if (s.CanProduction)
            {
                prodVal = ShopStepper(menuCard, "计划制作", "每次调整 10 杯", 112f,
                    s.planProduction + " 杯",
                    () => { s.planProduction = Mathf.Max(0, s.planProduction - 10); s.Changed(); },
                    () => { s.planProduction = Mathf.Min(200, s.planProduction + 10); s.Changed(); });
            }
            else ShopLockedRow(menuCard, "计划制作", "完成企业篇第 1 章解锁", 112f);

            float operationsHeight = s.CompetitionActive ? 338f : 214f;
            RectTransform operationsCard = ShopCard(c, "operationsCard", 0f, cardsY + 208f,
                leftWidth, operationsHeight);
            ShopCardHeader(operationsCard, "店铺配置", "人手、原料与推广");

            float operationY = 50f;
            if (s.CanEmployees)
            {
                empVal = ShopStepper(operationsCard, "当班店员", "人越多，服务上限越高", operationY,
                    s.employees + " 人",
                    () => { s.employees = Mathf.Max(1, s.employees - 1); s.Changed(); },
                    () => { s.employees = Mathf.Min(Eco.MaxEmployees, s.employees + 1); s.Changed(); });
            }
            else ShopLockedRow(operationsCard, "当班店员", "完成企业篇第 3 章解锁", operationY);
            operationY += 62f;

            if (s.CanMaterials)
            {
                Button marketButton;
                materialVal = ShopActionRow(operationsCard, "原料库存", "采购后可留到以后使用", operationY,
                    s.materials + " 份", "去原料市场", () => { CloseTop(); ShowMarket(); }, out marketButton);
            }
            else ShopLockedRow(operationsCard, "原料采购", "完成企业篇第 2 章解锁", operationY);
            operationY += 62f;

            if (s.CompetitionActive)
            {
                if (s.CanQuality)
                {
                    qualityVal = ShopActionRow(operationsCard, "果汁品质", "升级会提高吸引力和单位成本", operationY,
                        "Lv." + s.playerQualityLevel, s.playerQualityLevel >= 3 ? "已满级" : "升级品质",
                        () => TryUpgradeQuality(s), out qualityButton);
                    qualityButton.interactable = s.playerQualityLevel < 3;
                }
                else ShopLockedRow(operationsCard, "果汁品质", "推进到市场篇第 8 章解锁", operationY);
                operationY += 62f;

                if (s.CanMarketing)
                {
                    marketingVal = ShopStepper(operationsCard, "今日推广", "仅扣今天，广告余温会逐日衰减", operationY,
                        "¥ " + s.marketingBudget.ToString("0"),
                        () => AdjustMarketing(s, -1), () => AdjustMarketing(s, 1));
                }
                else ShopLockedRow(operationsCard, "今日推广", "推进到市场篇第 8 章解锁", operationY);
            }

            if (s.CanEmployees)
            {
                Button capacityInfo = UiTheme.MakeButton(operationsCard, "查看员工产能说明",
                    () => ShowInfoPopup("边际产量与边际产量递减", Explainer.ExplainEmployees(s.employees)), 14);
                PlaceTopLeft(capacityInfo.rectTransform(), leftWidth - 178f,
                    operationsHeight - 36f, 160f, 28f);
            }

            RectTransform previewCard = ShopCard(c, "previewCard", rightX, cardsY, rightWidth, 310f);
            ShopCardHeader(previewCard, "今日预估", "随设置实时更新");
            Image productionMetric = UiTheme.MakeImage(previewCard,
                new Color32(0x3b, 0x2e, 0x27, 255), "productionMetric");
            PlaceTopLeft(productionMetric.rectTransform, 20f, 54f, 176f, 72f);
            ShopText(productionMetric.rectTransform, "预计出杯", 14, UiTheme.TextSub,
                12f, 4f, 152f, 22f, TextAnchor.MiddleLeft);
            Text productionNumber = ShopText(productionMetric.rectTransform, "0 杯", 27, UiTheme.TextMain,
                12f, 25f, 152f, 40f, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image costMetric = UiTheme.MakeImage(previewCard,
                new Color32(0x3b, 0x2e, 0x27, 255), "costMetric");
            PlaceTopLeft(costMetric.rectTransform, 216f, 54f, 176f, 72f);
            ShopText(costMetric.rectTransform, "预计成本", 14, UiTheme.TextSub,
                12f, 4f, 152f, 22f, TextAnchor.MiddleLeft);
            Text costNumber = ShopText(costMetric.rectTransform, "¥ 0", 27, UiTheme.Accent,
                12f, 25f, 152f, 40f, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image divider = UiTheme.MakeImage(previewCard, new Color32(0x8e, 0x68, 0x45, 190), "divider");
            PlaceTopLeft(divider.rectTransform, 20f, 133f, rightWidth - 40f, 2f);
            Text detailText = ShopText(previewCard, "", 17, UiTheme.TextSub,
                22f, 145f, rightWidth - 44f, 82f, TextAnchor.UpperLeft);
            Image marginTag = UiTheme.MakeImage(previewCard, new Color32(0x3b, 0x2e, 0x27, 255), "marginTag");
            PlaceTopLeft(marginTag.rectTransform, 20f, 246f, rightWidth - 40f, 44f);
            Text marginText = ShopText(marginTag.rectTransform, "", 18, UiTheme.TextMain,
                12f, 0f, rightWidth - 64f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);

            float actionHeight = s.CompetitionActive ? 278f : 226f;
            RectTransform actionCard = ShopCard(c, "actionCard", rightX, cardsY + 328f,
                rightWidth, actionHeight);
            ShopCardHeader(actionCard, "开店检查", "确认后开始营业");
            Text readinessText = ShopText(actionCard, "", 18, UiTheme.TextMain,
                20f, 52f, rightWidth - 40f, 46f, TextAnchor.UpperLeft, FontStyle.Bold);
            readinessText.horizontalOverflow = HorizontalWrapMode.Wrap;
            readinessText.verticalOverflow = VerticalWrapMode.Truncate;

            if (s.CompetitionActive)
            {
                Image rivalStrip = UiTheme.MakeImage(actionCard, new Color32(0x3b, 0x2e, 0x27, 255), "rivalStrip");
                PlaceTopLeft(rivalStrip.rectTransform, 18f, 102f, rightWidth - 36f, 56f);
                ShopText(rivalStrip.rectTransform,
                    "SUNNY TEA  ¥" + s.competitor.price.ToString("0") +
                    "  ·  品质 " + s.competitor.EffectiveQuality.ToString("0") +
                    "\n今日策略 · " + CompetitionEco.StrategyName(s.competitor.strategy),
                    14, UiTheme.TextSub, 12f, 0f, rightWidth - 60f, 56f,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            float noteY = s.CompetitionActive ? 166f : 104f;
            ShopText(actionCard, "营业约 28 秒 · 代表性顾客会慢慢到店",
                15, UiTheme.TextSub, 20f, noteY, rightWidth - 40f, 24f,
                TextAnchor.MiddleCenter);

            Button go = UiTheme.MakeButton(actionCard, "开始营业", () =>
            {
                CloseTop();
                BusinessDirector d = BusinessDirector.I;
                if (d == null) { ShowToast("营业系统未就绪"); return; }
                BuildBusinessOverlay();
                d.Begin();
            }, 25);
            PlaceTopLeft(go.rectTransform(), 20f, actionHeight - 70f, rightWidth - 40f, 54f);
            go.image.sprite = null;
            go.image.color = UiTheme.Accent;

            System.Action refresh = null;
            refresh = () =>
            {
                if (priceVal != null) priceVal.text = priceDisplay();
                if (prodVal != null) prodVal.text = s.planProduction + " 杯";
                if (empVal != null) empVal.text = s.employees + " 人";
                if (materialVal != null) materialVal.text = s.materials + " 份";
                if (qualityVal != null) qualityVal.text = "Lv." + s.playerQualityLevel;
                if (qualityButton != null)
                {
                    qualityButton.interactable = s.playerQualityLevel < 3;
                    Text qualityButtonText = qualityButton.GetComponentInChildren<Text>();
                    if (qualityButtonText != null) qualityButtonText.text = s.playerQualityLevel >= 3 ? "已满级" : "升级品质";
                }
                if (marketingVal != null) marketingVal.text = "¥ " + s.marketingBudget.ToString("0");
                cashValue.text = "可用资金  ¥ " + s.money.ToString("0");
                int prod = DaySimulator.Production(s);
                int available = prod + Mathf.Max(0, s.inventory);
                bool canOpen = available > 0;
                CostBreakdown costs = CostService.Calculate(s, prod, s.CompetitionActive);
                ProductionCapacity production = CostService.Capacity(s);
                productionNumber.text = prod.ToString("0") + " 杯";
                costNumber.text = "¥ " + costs.totalCost.ToString("0");
                string capacity = s.CanEmployees ? production.laborPotentialCapacity + " 杯" : "暂未开放";
                detailText.text =
                    "原料 ¥" + costs.materialCost.ToString("0") + "  ·  工资 ¥" + costs.wageCost.ToString("0") +
                    "  ·  固定 ¥" + costs.fixedCost.ToString("0") + "\n" +
                    "广告 ¥" + costs.marketingCost.ToString("0") + "  ·  策略 ¥" + costs.strategicCost.ToString("0") +
                    "  ·  合计 ¥" + costs.totalCost.ToString("0") + "\n" +
                    "人力产能   " + capacity + "   ·   设备上限 " + Eco.EquipmentCap + " 杯";
                marginText.text = "每多做 1 杯，成本约增加  ¥ " + costs.effectiveInputUnitCost.ToString("0.0");
                int demandPct = Mathf.RoundToInt(s.ResidentDemandChangePercent);   // 围猎三#3:先取整再判号,-0.x% 不再显示「-0%」
                string residentSignal = (demandPct >= 0 ? "+" : "") + demandPct + "%";
                readinessText.text = (prod > 0
                    ? "准备就绪：制作 " + prod + " 杯 · 可售上限 " + available + " 杯"
                    : s.inventory > 0
                        ? "准备就绪：今日不生产，将销售现有库存 " + s.inventory + " 杯。"
                        : "还不能开店：今日不生产且没有可售库存。") +
                    "\n居民选择传导：果汁需求 " + residentSignal;
                readinessText.color = canOpen ? UiTheme.Good : UiTheme.Bad;
                go.interactable = canOpen;
            };
            refresh();

            System.Action onShop = () =>
            {
                if (root == null) return;
                refresh();
            };
            s.OnStateChanged += onShop;
            // 面板销毁时退订
            root.AddComponent<PanelDisposeHook>().onDispose = () => s.OnStateChanged -= onShop;
        }

        public void ShowMarket()
        {
            GameState s = GameState.I;
            if (s == null || s.phase != GamePhase.Morning) { ShowToast("只有清晨营业前可以采购"); return; }
            if (!s.CanMaterials) { ShowToast("原料采购需先完成企业篇第 2 章"); return; }

            GameObject root = BeginPanel("原料市场", 620, 560);
            RectTransform c = ContentOf(root);

            float unit = DaySimulator.MaterialUnitPrice(s);
            Text head = ValueText(c, "今日原料单价:" + unit.ToString("0.0") + " 元/杯份" +
                (s.materialPriceMul > 1.001f ? "  ·  今日涨价" : ""), 0, 4, 560, UiTheme.TextMain, 23);
            head.alignment = TextAnchor.UpperLeft;

            Text stock = ValueText(c, "当前持有:" + s.materials + " 份   资金 " + s.money.ToString("0"), 0, 52, 560, UiTheme.TextSub, 22);
            stock.alignment = TextAnchor.UpperLeft;

            System.Action<int> buy = null;
            buy = (n) =>
            {
                float cost = n * unit;
                if (s.money < cost) { ShowToast("资金不足,买不起 " + n + " 份"); return; }
                s.money -= cost;
                s.materials += n;
                s.Changed();
                ShowToast("采购成功:+" + n + " 份原料,花了 " + cost.ToString("0") + " 元(这是机会成本的一部分)");
                stock.text = "当前持有:" + s.materials + " 份   资金 " + s.money.ToString("0");
            };

            for (int i = 0; i < 3; i++)
            {
                int n = 50 * (i + 1);
                float cost = n * unit;
                Button b = UiTheme.MakeButton(c, "买 " + n + " 份(花 " + cost.ToString("0") + " 元)", () => buy(n), 21);
                b.rectTransform().anchorMin = b.rectTransform().anchorMax = new Vector2(0, 1);
                b.rectTransform().pivot = new Vector2(0, 1);
                b.rectTransform().anchoredPosition = new Vector2(40, -(120 + i * 64));
                b.rectTransform().sizeDelta = new Vector2(500, 52);
            }

            Text tip = UiTheme.MakeText(c, "原料是可变成本:买多少取决于你打算生产多少。\n原料放不坏,但果汁会——别生产太多卖不掉。", 19, UiTheme.TextSub, TextAnchor.UpperLeft);
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0, 1);
            tip.rectTransform.pivot = new Vector2(0, 1);
            tip.rectTransform.anchoredPosition = new Vector2(20, -330);
            tip.rectTransform.sizeDelta = new Vector2(540, 70);

            Button close = UiTheme.MakeButton(c, "离开市场", CloseTop, 21);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(0.5f, 0);
            close.rectTransform().pivot = new Vector2(0.5f, 0f);
            close.rectTransform().anchoredPosition = new Vector2(0, 6);
            close.rectTransform().sizeDelta = new Vector2(220, 50);
        }

        bool TryUpgradeQuality(GameState s, bool notify = true)
        {
            if (s == null || !s.CanQuality) return false;
            if (s.playerQualityLevel >= 3)
            {
                if (notify) ShowToast("品质已经达到最高等级");
                return false;
            }
            int next = s.playerQualityLevel + 1;
            float cost = CompetitionEco.UpgradeCost(next);
            if (s.money < cost)
            {
                if (notify) ShowToast("品质升级需要 " + cost.ToString("0") + " 元，资金不足");
                return false;
            }
            s.money -= cost;
            s.todayStrategicSpend += cost;
            s.playerQualityLevel = next;
            s.Changed();
            if (notify) ShowToast("品质升级到 Lv." + next + "：品质 " + s.PlayerQuality.ToString("0") + "，单位成本也提高了");
            return true;
        }

        void AdjustMarketing(GameState s, int direction)
        {
            float[] options = { 0f, 100f, 300f, 500f };
            int current = 0;
            float bestDistance = Mathf.Abs(s.marketingBudget - options[0]);
            for (int i = 1; i < options.Length; i++)
            {
                float distance = Mathf.Abs(s.marketingBudget - options[i]);
                if (distance < bestDistance) { bestDistance = distance; current = i; }
            }
            current = Mathf.Clamp(current + direction, 0, options.Length - 1);
            s.marketingBudget = options[current];
            s.Changed();
        }

        public void ShowPlaza()
        {
            GameState s = GameState.I;
            if (s == null) return;
            GameObject root = BeginPanel("小镇广场 · 公告栏", 600, 480);
            RectTransform c = ContentOf(root);

            string body;
            TownEvent ev = DaySimulator.LastEvent;
            if (!string.IsNullOrEmpty(s.todayEventName) && ev != null && ev.name == s.todayEventName)
            {
                body = "📌 今日小镇事件:" + ev.name + "\n\n" + ev.desc + "\n\n想一想:这件事会让你的需求曲线“移动”,还是沿曲线移动?";
            }
            else
            {
                body = "📌 今天小镇风平浪静。\n\n居民们照常生活:学生上学、白领上班、游客拍照。\n平静的日子适合观察“正常”的需求数据。";
            }

            float historySat = 0f, sevenDaySat = 0f;
            int n = s.history.Count;
            int sevenStart = Mathf.Max(0, n - 7);
            for (int i = 0; i < n; i++)
            {
                historySat += s.history[i].satisfaction;
                if (i >= sevenStart) sevenDaySat += s.history[i].satisfaction;
            }
            if (n > 0)
            {
                int sevenCount = n - sevenStart;
                body += "\n\n今日顾客满意度：" + s.history[n - 1].satisfaction.ToString("0") +
                    "  ·  7 日平均：" + (sevenDaySat / sevenCount).ToString("0") +
                    "  ·  历史平均：" + (historySat / n).ToString("0");
            }

            // V0.7.1 橱窗档位切换当日的对冲提示(PRD §125):防止学生把橱窗变暗/转让牌误读成倒闭公告
            var atm = s.economy != null ? s.economy.atmosphere : null;
            if (atm != null && atm.windowTierChangedToday)
                body += "\n\n镇上商铺经营情绪：" + s.government.enterpriseConfidence.ToString("0") +
                    " ——橱窗与木牌是情绪，不是倒闭公告。";

            Text t = UiTheme.MakeText(c, body, 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.Stretch();

            Button ok = UiTheme.MakeButton(c, "关闭", CloseTop, 21);
            ok.rectTransform().anchorMin = ok.rectTransform().anchorMax = new Vector2(0.5f, 0);
            ok.rectTransform().pivot = new Vector2(0.5f, 0f);
            ok.rectTransform().anchoredPosition = new Vector2(0, 6);
            ok.rectTransform().sizeDelta = new Vector2(200, 50);
        }
    }

    /// <summary>面板销毁时执行退订,避免悬空委托。</summary>
    public class PanelDisposeHook : MonoBehaviour
    {
        public System.Action onDispose;
        void OnDestroy() { if (onDispose != null) onDispose(); }
    }
}
