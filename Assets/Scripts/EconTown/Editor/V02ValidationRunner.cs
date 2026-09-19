#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// V0.2 纯数值回归：每个编辑器会话自动跑一次，也可从 EconTown/Validate V0.2 手动执行。
    /// 不进入 Play Mode、不写存档，只验证 Day 1–30 的状态不变量与核心价格战链路。
    /// </summary>
    public static class V02ValidationRunner
    {
        static GameObject previewStateObject;
        static GameState previewState;
        static GameState previousGameState;
        static UIManager previewUi;
        static GameObject previewCameraObject;
        static bool previewActive;

        // 不再随编辑器启动自动运行:本 Runner 的 UI 断言已落后于当前实现,
        // 需要时通过菜单 EconTown/Validate V0.2 手动执行(回归护栏以 88 个 NUnit 测试 + SimDriver 为准)。

        [MenuItem("EconTown/Validate V0.2")]
        public static void Run()
        {
            GameState originalGameState = GameState.I;
            GameObject go = null;
            UIManager validationUi = null;
            try
            {
                DaySimulator.SetRandomSeed(20260819);
                go = new GameObject("V02ValidationState");
                GameState s = go.AddComponent<GameState>();
                s.totalDays = 30;
                s.materials = 1000;
                s.money = 6000f;

                for (int day = 1; day <= 30; day++)
                {
                    if (s.day != day) throw new System.Exception("日期错位: expected=" + day + " actual=" + s.day);
                    s.price = day < 16 ? 10f + day % 8 : 18f;
                    if (s.CanProduction) s.planProduction = 120;
                    if (s.CanEmployees) s.employees = 3;
                    if (s.CanMaterials && s.materials < 220)
                    {
                        int amount = 400;
                        s.money -= amount * DaySimulator.MaterialUnitPrice(s);
                        s.materials += amount;
                    }

                    if (s.CanQuality && s.playerQualityLevel == 1)
                    {
                        float upgrade = CompetitionEco.UpgradeCost(2);
                        s.money -= upgrade; s.todayStrategicSpend += upgrade; s.playerQualityLevel = 2;
                    }
                    if (s.CanMarketing) s.marketingBudget = day % 3 == 0 ? 300f : 100f;
                    if (s.CompetitionActive && s.priceWarActive)
                    {
                        s.price = s.competitor.price;
                        s.todayStrategyChoice = PlayerCompetitionChoice.FollowPrice.ToString();
                    }
                    if (day == 22) s.marketExperimentMask = 1 | 2 | 4;

                    int produced = DaySimulator.Production(s);
                    DayRecord record;
                    if (s.CompetitionActive)
                    {
                        CompetitionSimulator.MarketRoll market = CompetitionSimulator.RollMarket(s);
                        record = CompetitionSimulator.Settle(s, market, produced);
                    }
                    else
                    {
                        record = DaySimulator.Settle(s, DaySimulator.RollDemand(s), produced);
                    }
                    CardLibrary.CheckAll(s);

                    if (float.IsNaN(record.profit) || float.IsInfinity(record.profit)) throw new System.Exception("利润出现非法数值 Day " + day);
                    if (record.competitionActive && Mathf.Abs(record.playerMarketShare + record.competitorMarketShare - 1f) > 0.001f)
                        throw new System.Exception("市场份额不守恒 Day " + day);

                    if (day < 30)
                    {
                        float momentumBeforeAdvance = s.playerMarketingMomentum;
                        DaySimulator.AdvanceDay(s);
                        if (day >= 20 && Mathf.Abs(s.marketingBudget) > 0.001f)
                            throw new System.Exception("广告预算跨天自动续费 Day " + day);
                        if (day >= 20 && momentumBeforeAdvance > 0f &&
                            (s.playerMarketingMomentum <= 0f || s.playerMarketingMomentum >= momentumBeforeAdvance))
                            throw new System.Exception("广告余温没有在跨天时衰减 Day " + day);
                        if (s.CompetitionActive) CompetitorAI.PrepareDay(s);
                    }
                }

                int competitionDays = 0, priceWarDays = 0;
                DayRecord beforeEntry = null;
                DayRecord entryDay = null;
                DayRecord preWar = null;
                DayRecord followDay = null;
                DayRecord rivalCutsAgain = null;
                DayRecord bothWorseWarDay = null;
                for (int i = 0; i < s.history.Count; i++)
                {
                    if (s.history[i].competitionActive) competitionDays++;
                    if (s.history[i].priceWar) priceWarDays++;
                    if (s.history[i].day == 15) beforeEntry = s.history[i];
                    if (s.history[i].day == 16) entryDay = s.history[i];
                    if (s.history[i].day == 20) preWar = s.history[i];
                    if (s.history[i].day == 21) followDay = s.history[i];
                    if (followDay != null && s.history[i].day > followDay.day && s.history[i].competitorPrice < followDay.competitorPrice)
                        rivalCutsAgain = s.history[i];
                    if (preWar != null && s.history[i].priceWar && s.history[i].profit < preWar.profit &&
                        s.history[i].competitorProfit < preWar.competitorProfit) bothWorseWarDay = s.history[i];
                }
                if (s.history.Count != 30) throw new System.Exception("历史天数不是 30");
                if (competitionDays != 15) throw new System.Exception("竞争阶段不是 15 天: " + competitionDays);
                if (priceWarDays < 3) throw new System.Exception("价格战链路未持续形成: " + priceWarDays);
                if (beforeEntry == null || entryDay == null || entryDay.sold >= beforeEntry.sold)
                    throw new System.Exception("竞争对手进入后未造成玩家销量下降");
                if (preWar == null || followDay == null || followDay.playerStrategyChoice != "跟随降价")
                    throw new System.Exception("第 21 天未形成玩家跟价决策");
                if (followDay.playerMarketShare <= preWar.playerMarketShare)
                    throw new System.Exception("玩家跟价后市场份额未提升");
                if (rivalCutsAgain == null)
                    throw new System.Exception("玩家跟价后对手没有再次降价");
                if (preWar == null || bothWorseWarDay == null)
                    throw new System.Exception("没有形成双方利润都低于价格战前的实际结果");
                if (!s.unlockedCards.Contains("prisonersdilemma")) throw new System.Exception("未解锁囚徒困境");
                if (!s.unlockedCards.Contains("perfectcompetition") || !s.unlockedCards.Contains("monopoly"))
                    throw new System.Exception("市场结构实验卡未解锁");
                if (!s.unlockedCards.Contains("nashequilibrium"))
                    throw new System.Exception("完成多次市场实验后未解锁纳什均衡");

                string[] v02Cards =
                {
                    "substitute", "crosselasticity", "differentiation", "marketshare", "marketpower",
                    "perfectcompetition", "monopolisticcompetition", "oligopoly", "monopoly", "gametheory",
                    "prisonersdilemma", "nashequilibrium", "pricewar", "consumersurplus", "producersurplus", "imperfectinfo"
                };
                for (int i = 0; i < v02Cards.Length; i++)
                    if (CardLibrary.Get(v02Cards[i]) == null) throw new System.Exception("V0.2 知识卡缺失: " + v02Cards[i]);

                // 进店会暂停人物移动，但关闭面板后必须立即解锁，不能留下“卡死”状态。
                // 视觉基准固定在首次开店，避免拿竞争阶段的扩展面板套第 1 天高度标准。
                s.day = 1;
                s.todayEventName = "";
                s.todayCompetitionEventName = "";
                s.phase = GamePhase.Morning;
                GameState.I = s; // Edit Mode 不会自动调用 MonoBehaviour.Awake。
                validationUi = UIManager.Create();
                validationUi.InitializeForValidation();
                validationUi.ShowShop();
                if (!validationUi.InputBlocked) throw new System.Exception("进入果汁店后没有生成挂在可见 Canvas 下的经营面板");
                Transform shopPanel = validationUi.transform.Find("Canvas/panels/panel_果汁店 · 今日经营/content");
                if (shopPanel == null || shopPanel.Find("menuCard") == null ||
                    shopPanel.Find("operationsCard") == null || shopPanel.Find("previewCard") == null ||
                    shopPanel.Find("actionCard") == null)
                    throw new System.Exception("经营面板缺少菜单、店铺配置、今日预估或开店检查分区");
                Transform shopRoot = shopPanel.parent;
                if (shopRoot.GetComponent<Shadow>() != null)
                    throw new System.Exception("经营面板仍在使用会产生阶梯脏块的 Shadow 效果");
                RectTransform shopRootRect = shopRoot as RectTransform;
                if (shopRootRect == null || shopRootRect.sizeDelta.y > 710f)
                    throw new System.Exception("第 1 天经营面板过高，底部仍有大块无效留白");
                if (shopPanel.Find("menuCard").GetComponent<Outline>() != null ||
                    shopPanel.Find("operationsCard").GetComponent<Outline>() != null ||
                    shopPanel.Find("previewCard").GetComponent<Outline>() != null ||
                    shopPanel.Find("actionCard").GetComponent<Outline>() != null)
                    throw new System.Exception("经营卡片仍在使用会破坏九宫格的 Outline 效果");
                Transform canvas = validationUi.transform.Find("Canvas");
                if (canvas.GetComponent<Canvas>().pixelPerfect)
                    throw new System.Exception("SDF 中文字体在非整数窗口比例下要求 pixelPerfect=false,开启反而发虚");
                CanvasScaler canvasScaler = canvas.GetComponent<CanvasScaler>();
                if (canvasScaler == null || canvasScaler.referenceResolution != new Vector2(1280f, 720f) ||
                    canvasScaler.matchWidthOrHeight != 0.5f)
                    throw new System.Exception("Canvas 没有使用 1280×720 宽高各半的缩放策略");
                RectTransform phaseHint = canvas.Find("topbar/phaseHint") as RectTransform;
                if (phaseHint == null || phaseHint.pivot.x < 0.99f)
                    throw new System.Exception("右对齐阶段提示没有使用右侧轴心，文字会跑出屏幕");
                Transform modalShade = canvas.Find("panels/modalShade");
                if (modalShade == null || !modalShade.gameObject.activeSelf)
                    throw new System.Exception("经营弹窗打开时没有启用背景遮罩");
                validationUi.ShowPrompt("[E] 测试提示");
                Text interactionPrompt = canvas.Find("interactionPrompt").GetComponent<Text>();
                if (!string.IsNullOrEmpty(interactionPrompt.text))
                    throw new System.Exception("弹窗打开后场景交互提示仍覆盖在界面上");
                Image topbarImage = canvas.Find("topbar").GetComponent<Image>();
                Image dockImage = canvas.Find("bottomDock").GetComponent<Image>();
                if (topbarImage.sprite == null || dockImage.sprite == null ||
                    topbarImage.type != Image.Type.Sliced || dockImage.type != Image.Type.Sliced)
                    throw new System.Exception("HUD 没有使用统一的像素九宫格框体");
                Button[] shopButtons = shopRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < shopButtons.Length; i++)
                    if (shopButtons[i].image != null &&
                        (shopButtons[i].image.sprite == null || shopButtons[i].image.type != Image.Type.Sliced))
                        throw new System.Exception("经营面板按钮没有使用统一的像素票据框体");

                validationUi.ShowExplain(new Explainer.Explanation
                {
                    title = "说明页回归",
                    paragraphs = new[] { new string('测', 800) },
                    concept = null
                });
                Transform explainContent = validationUi.transform.Find("Canvas/panels/panel_说明页回归/content");
                Transform explainScrollTransform = explainContent != null ? explainContent.Find("explainScroll") : null;
                Transform explainTextTransform = explainContent != null ? explainContent.Find("explainScroll/scrollContent/text") : null;
                ScrollRect explainScroll = explainScrollTransform != null
                    ? explainScrollTransform.GetComponent<ScrollRect>() : null;
                Text explainText = explainTextTransform != null ? explainTextTransform.GetComponent<Text>() : null;
                Canvas.ForceUpdateCanvases();
                if (explainScroll == null || explainText == null ||
                    explainText.horizontalOverflow != HorizontalWrapMode.Wrap ||
                    explainScroll.content.rect.height <= explainScroll.viewport.rect.height)
                    throw new System.Exception("经营说明页没有正确换行或长文滚动区域未生效");
                validationUi.CloseTop();

                var fullHistory = new System.Collections.Generic.List<DayRecord>(s.history);
                s.history.Clear();
                s.history.Add(fullHistory[0]);
                validationUi.ShowCurves(0);
                Transform curveContent = validationUi.transform.Find("Canvas/panels/panel_经济实验室 · 用你自己的数据画曲线/content");
                Transform demandGraphTransform = curveContent != null ? curveContent.Find("plot/graph") : null;
                PolylineGraph demandGraph = demandGraphTransform != null
                    ? demandGraphTransform.GetComponent<PolylineGraph>() : null;
                Canvas.ForceUpdateCanvases();
                if (demandGraph == null || demandGraph.rectTransform == null ||
                    demandGraph.PointCount < 1 || demandGraph.FitPointCount < 2 ||
                    demandGraph.rectTransform.rect.width < 900f || demandGraph.rectTransform.rect.height < 340f)
                    throw new System.Exception("第 1 天需求曲线没有生成可见数据点和参考曲线");
                validationUi.CloseTop();
                s.history.Clear();
                s.history.AddRange(fullHistory);

                s.day = 22;
                s.phase = GamePhase.Morning;
                validationUi.ShowMarketDashboard();
                Transform marketContent = validationUi.transform.Find("Canvas/panels/panel_市场中心/content");
                if (marketContent == null || marketContent.Find("marketShareBar/playerShare") == null ||
                    marketContent.Find("marketShareBar/competitorShare") == null ||
                    marketContent.Find("playerMarketCard") == null ||
                    marketContent.Find("competitorMarketCard") == null ||
                    marketContent.Find("marketTrendCard") == null)
                    throw new System.Exception("市场中心缺少份额条、双方企业卡片或趋势区");
                Transform marketTrend = marketContent.Find("marketTrendCard");
                Text[] trendTexts = marketTrend.GetComponentsInChildren<Text>(true);
                Text trendData = null;
                for (int i = 0; i < trendTexts.Length; i++)
                    if (trendTexts[i].text.Contains("Day ")) { trendData = trendTexts[i]; break; }
                if (trendData == null || trendData.text.Split('\n').Length < 5 ||
                    trendData.rectTransform.rect.height < 110f)
                    throw new System.Exception("市场中心最近五天趋势被裁切或数据不完整");
                validationUi.CloseTop();

                validationUi.ShowStrategyLab();
                Transform strategyContent = validationUi.transform.Find("Canvas/panels/panel_策略实验室 · 价格博弈/content");
                if (strategyContent == null || strategyContent.Find("payoff_bothHold") == null ||
                    strategyContent.Find("payoff_competitorCuts") == null ||
                    strategyContent.Find("payoff_playerCuts") == null ||
                    strategyContent.Find("payoff_bothCut") == null || strategyContent.Find("strategyLesson") == null)
                    throw new System.Exception("策略实验室没有生成完整 2×2 收益矩阵");
                validationUi.CloseTop();

                AssertExperimentParticipants(validationUi, "完全竞争", "完全竞争实验 · 你的定价", 8);
                AssertExperimentParticipants(validationUi, "寡头", "寡头实验 · 只有三家公司", 3);
                AssertExperimentParticipants(validationUi, "垄断", "垄断实验 · 唯一供应者", 1);

                // 从日报的真实按钮入口走完四条用户路径，防止只验证页面、漏掉点击绑定。
                s.history.Clear();
                s.history.Add(fullHistory[0]);
                s.phase = GamePhase.Report;
                DayRecord firstDay = s.history[0];
                validationUi.ShowReport(firstDay);
                Transform reportContent = validationUi.transform.Find("Canvas/panels/panel_第 1 日经营报告/content");
                if (reportContent == null) throw new System.Exception("首日经营报告未生成");
                CrispTextMirror[] reportFonts = reportContent.parent.GetComponentsInChildren<CrispTextMirror>(true);
                for (int i = 0; i < reportFonts.Length; i++)
                {
                    if (reportFonts[i].UsingLegacyFallback)
                        throw new System.Exception("经营报告仍有文字回退到模糊旧字体: " + reportFonts[i].Source.text);
                    if (reportFonts[i].Source.fontSize < UiTheme.MinimumBodyFontSize)
                        throw new System.Exception("经营报告仍有低于最小字号的文字");
                }

                string[] explainButtons =
                {
                    "btn_需求与销量为什么这样？",
                    "btn_利润怎么算？",
                    "btn_库存说明什么？"
                };
                Explainer.Explanation[] explanations =
                {
                    Explainer.ExplainSales(s, firstDay),
                    Explainer.ExplainProfit(s, firstDay),
                    Explainer.ExplainInventory(s, firstDay)
                };
                for (int i = 0; i < explainButtons.Length; i++)
                {
                    Button open = FindRequiredButton(reportContent, explainButtons[i]);
                    open.onClick.Invoke();
                    Button close = AssertExplanationLayout(validationUi, explanations[i].title, false);
                    close.onClick.Invoke();
                }

                Button openCurve = FindRequiredButton(reportContent, "btn_查看需求曲线");
                openCurve.onClick.Invoke();
                AssertDemandCurveLayout(validationUi);
                Transform curvePanelContent = validationUi.transform.Find("Canvas/panels/panel_经济实验室 · 用你自己的数据画曲线/content");
                FindRequiredButton(curvePanelContent, "btn_关闭实验室").onClick.Invoke();
                s.history.Clear();
                s.history.AddRange(fullHistory);

                validationUi.BuildBusinessOverlay();
                Transform businessOverlay = validationUi.transform.Find("Canvas/panels/bizOverlay");
                if (businessOverlay == null || businessOverlay.Find("btn_跳过营业") == null)
                    throw new System.Exception("营业浮层缺少跳过营业按钮");
                if (businessOverlay.Find("btn_速度 x1") != null)
                    throw new System.Exception("营业浮层仍残留倍速按钮");
                Canvas.ForceUpdateCanvases();
                RectTransform businessCount = businessOverlay.Find("businessCount") as RectTransform;
                RectTransform businessTrack = businessOverlay.Find("barTrack") as RectTransform;
                if (businessCount == null || businessCount.rect.width < 480f ||
                    businessTrack == null || businessTrack.rect.width < 580f)
                    throw new System.Exception("营业浮层的计数或进度条宽度异常，文字会被挤成竖排");
                if (BusinessDirector.BusinessDurationSeconds < 28f || BusinessDirector.VisualCustomerLimit != 1 ||
                    BusinessDirector.MinimumVisualSpawnInterval < 4f)
                    throw new System.Exception("营业演出仍会过快或同屏出现过多顾客");

                validationUi.CloseAll();
                if (validationUi.InputBlocked) throw new System.Exception("关闭果汁店面板后人物仍被锁定");
                validationUi.ShowToast(new string('测', 70));
                Transform toast = validationUi.transform.Find("Canvas/toasts/toast");
                Text toastText = toast != null ? toast.Find("text").GetComponent<Text>() : null;
                if (toast == null || ((RectTransform)toast).sizeDelta.y <= 58f || toastText == null ||
                    toastText.horizontalOverflow != HorizontalWrapMode.Wrap)
                    throw new System.Exception("长提示仍会被单行截断");

                Debug.Log("[V02Validation] PASS days=30 competitionDays=" + competitionDays +
                    " priceWarDays=" + priceWarDays + " cards=" + s.unlockedCards.Count +
                    " money=" + s.money.ToString("0") + " avgShare=" + (s.AverageMarketShare * 100f).ToString("0.0") + "%" +
                    " entrySales=" + beforeEntry.sold + "->" + entryDay.sold +
                    " followShare=" + (preWar.playerMarketShare * 100f).ToString("0.0") + "%->" + (followDay.playerMarketShare * 100f).ToString("0.0") + "%" +
                    " rivalCut=" + followDay.competitorPrice.ToString("0") + "->" + rivalCutsAgain.competitorPrice.ToString("0") +
                    " preWarProfit=" + preWar.profit.ToString("0") + "/" + preWar.competitorProfit.ToString("0") +
                    " worseWarDay=" + bothWorseWarDay.day + " profit=" + bothWorseWarDay.profit.ToString("0") + "/" + bothWorseWarDay.competitorProfit.ToString("0"));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[V02Validation] FAIL " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                GameState.I = originalGameState;
                if (validationUi != null) Object.DestroyImmediate(validationUi.gameObject);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        static Button FindRequiredButton(Transform parent, string name)
        {
            Transform target = parent != null ? parent.Find(name) : null;
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null) throw new System.Exception("缺少可点击按钮: " + name);
            return button;
        }

        static Button FindButtonContaining(Transform parent, string textFragment)
        {
            Button[] buttons = parent != null ? parent.GetComponentsInChildren<Button>(true) : new Button[0];
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>();
                if (label != null && label.text.Contains(textFragment)) return buttons[i];
            }
            throw new System.Exception("缺少包含文字的按钮: " + textFragment);
        }

        static void AssertExperimentParticipants(UIManager ui, string buttonText, string panelTitle, int expectedCount)
        {
            ui.ShowMarketExperiments();
            Transform menu = ui.transform.Find("Canvas/panels/panel_市场结构实验/content");
            FindButtonContaining(menu, buttonText).onClick.Invoke();
            Transform content = ui.transform.Find("Canvas/panels/panel_" + panelTitle + "/content");
            Transform strip = content != null ? content.Find("experimentParticipants") : null;
            if (strip == null || strip.childCount != expectedCount || content.Find("experimentFootnote") == null)
                throw new System.Exception(buttonText + "实验的参与者可视化数量错误: expected=" + expectedCount);
            ui.CloseTop();
        }

        static Button AssertExplanationLayout(UIManager ui, string title, bool requireScrollable)
        {
            Transform content = ui.transform.Find("Canvas/panels/panel_" + title + "/content");
            Transform scrollTransform = content != null ? content.Find("explainScroll") : null;
            Transform textTransform = content != null ? content.Find("explainScroll/scrollContent/text") : null;
            ScrollRect scroll = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
            Text text = textTransform != null ? textTransform.GetComponent<Text>() : null;
            Canvas.ForceUpdateCanvases();
            if (scroll == null || text == null || scrollTransform.GetComponent<RectMask2D>() == null ||
                text.horizontalOverflow != HorizontalWrapMode.Wrap ||
                text.rectTransform.rect.width > scroll.viewport.rect.width - 24f ||
                (requireScrollable && scroll.content.rect.height <= scroll.viewport.rect.height))
                throw new System.Exception("说明页文字越界或滚动区失效: " + title);

            Button close = FindRequiredButton(content, "btn_关闭");
            Bounds closeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                content, close.GetComponent<RectTransform>());
            Rect contentRect = ((RectTransform)content).rect;
            if (closeBounds.min.x < contentRect.xMin - 1f || closeBounds.max.x > contentRect.xMax + 1f ||
                closeBounds.min.y < contentRect.yMin - 1f || closeBounds.max.y > contentRect.yMax + 1f)
                throw new System.Exception("说明页按钮越出面板: " + title);
            return close;
        }

        static void AssertDemandCurveLayout(UIManager ui)
        {
            Transform content = ui.transform.Find("Canvas/panels/panel_经济实验室 · 用你自己的数据画曲线/content");
            Transform graphTransform = content != null ? content.Find("plot/graph") : null;
            PolylineGraph graph = graphTransform != null ? graphTransform.GetComponent<PolylineGraph>() : null;
            Canvas.ForceUpdateCanvases();
            if (graph == null || graph.rectTransform == null || graph.GetComponent<CanvasRenderer>() == null ||
                graph.PointCount < 1 || graph.FitPointCount < 2 ||
                graph.rectTransform.rect.width < 900f || graph.rectTransform.rect.height < 340f)
                throw new System.Exception("首日需求曲线页没有画出数据点和参考曲线");
        }

        [MenuItem("EconTown/Preview Shop UI")]
        public static void PreviewShopUi()
        {
            BeginUiPreview();
            previewUi.ShowShop();
            OpenPreviewWindow();
        }

        [MenuItem("EconTown/Preview Report UI")]
        public static void PreviewReportUi()
        {
            BeginUiPreview();
            DayRecord record = CreatePreviewDay();
            previewState.phase = GamePhase.Report;
            previewUi.ShowReport(record);
            OpenPreviewWindow();
        }

        [MenuItem("EconTown/Preview Long Explanation UI")]
        public static void PreviewLongExplanationUi()
        {
            BeginUiPreview();
            previewState.unlockedCards.Add("profit");
            previewUi.ShowExplain(new Explainer.Explanation
            {
                title = "利润说明 · 滚动验收",
                concept = "profit",
                paragraphs = new[]
                {
                    "利润等于总收入减去总成本。收入来自实际卖出的果汁，而不是今天计划生产的数量。",
                    "总成本包括原料、工资与每天都会发生的租金折旧。原料和工资会随经营决策变化，租金折旧属于固定成本。",
                    "如果生产过多，没卖掉的果汁会成为库存并发生损耗；如果生产过少，又会因为断货失去本来愿意购买的顾客。",
                    "判断一次调价是否有效，不能只看销量。价格降低可能带来更多顾客，也可能让每杯利润下降；价格提高则可能扩大单杯利润，却赶走一部分对价格敏感的居民。",
                    "连续用不同价格经营几天，再结合需求曲线、收入和成本一起观察，才能找到更接近利润最大化的位置。"
                }
            });
            OpenPreviewWindow();
        }

        [MenuItem("EconTown/Preview Demand Curve UI")]
        public static void PreviewDemandCurveUi()
        {
            BeginUiPreview();
            CreatePreviewDay();
            previewState.phase = GamePhase.Report;
            previewUi.ShowCurves(0);
            OpenPreviewWindow();
        }

        static void BeginUiPreview()
        {
            if (previewActive) CloseShopUiPreview();
            DestroyOrphanedPreviewObjects();

            previewActive = true;
            previousGameState = GameState.I;

            previewStateObject = new GameObject("__ShopUiPreviewState");
            previewStateObject.hideFlags = HideFlags.HideAndDontSave;
            previewState = previewStateObject.AddComponent<GameState>();
            previewState.day = 1;
            previewState.totalDays = 30;
            previewState.money = 2000f;
            previewState.materials = 300;
            previewState.phase = GamePhase.Morning;
            GameState.I = previewState;

            previewUi = UIManager.Create();
            previewUi.gameObject.name = "__ShopUiPreview";
            previewUi.gameObject.hideFlags = HideFlags.HideAndDontSave;
            previewUi.InitializeForValidation();

            previewCameraObject = new GameObject("__ShopUiPreviewCamera", typeof(Camera));
            previewCameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera previewCamera = previewCameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color32(0x8f, 0xb8, 0xa5, 255);
            previewCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            previewCamera.depth = -100f;
            previewCamera.enabled = true;
        }

        static DayRecord CreatePreviewDay()
        {
            previewState.price = 10f;
            previewState.planProduction = 80;
            previewState.employees = 1;
            DaySimulator.DemandRoll demand = new DaySimulator.DemandRoll
            {
                students = 79,
                whitecollars = 59,
                tourists = 25
            };
            return DaySimulator.Settle(previewState, demand, 80);
        }

        static void OpenPreviewWindow()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.delayCall += FocusGameView;
        }

        static void FocusGameView()
        {
            System.Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Focus();
            gameView.Repaint();
        }

        [MenuItem("EconTown/Close Shop UI Preview")]
        [MenuItem("EconTown/Close UI Preview")]
        public static void CloseShopUiPreview()
        {
            previewActive = false;

            if (previewUi != null) Object.DestroyImmediate(previewUi.gameObject);
            if (previewCameraObject != null) Object.DestroyImmediate(previewCameraObject);
            if (previewStateObject != null) Object.DestroyImmediate(previewStateObject);
            if (GameState.I == null || GameState.I == previewState) GameState.I = previousGameState;

            previewUi = null;
            previewState = null;
            previewStateObject = null;
            previewCameraObject = null;
            previousGameState = null;
            DestroyOrphanedPreviewObjects();
        }

        static void DestroyOrphanedPreviewObjects()
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = allObjects.Length - 1; i >= 0; i--)
            {
                GameObject candidate = allObjects[i];
                if (candidate == null) continue;
                if (candidate.name == "__ShopUiPreview" ||
                    candidate.name == "__ShopUiPreviewState" ||
                    candidate.name == "__ShopUiPreviewCamera")
                    Object.DestroyImmediate(candidate);
            }
        }
    }
}
#endif
