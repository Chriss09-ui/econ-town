using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace EconTown
{
    /// <summary>
    /// UI 总管:Canvas/HUD/面板栈/提示/Toast/营业覆盖层。
    /// 各具体面板在 UiShopMarket.cs / UiReports.cs / UiLab.cs 中以 partial 实现。
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        public static UIManager I { get; private set; }

        RectTransform panelRoot;
        CanvasScaler canvasScaler;
        Image panelShade;
        Image phaseHintBg;   // V0.7.1 阶段提示第二行的底板(空提示时隐藏)
        Text dayText, moneyText, hintText, promptText, eventChipText, taskHudText;
        Image eventChipBg;
        Image competitionHudBg;
        Image taskHudBg;
        Button[] dockButtons;
        RectTransform bottomDockRt;
        Image promptBg;
        Text competitionHudText;
        RectTransform toastRoot;
        GameObject businessOverlay;
        Text bizCountText, bizTimeText;
        Image bizBar;
        GameState subscribedGameState;
        BusinessDirector subscribedBusinessDirector;
        bool initialized;
        bool dynamicFontRefreshPending;
        bool rebindingDynamicFont;
        string pendingPrompt = "";

        const int MaxVisibleToasts = 4;

        readonly List<GameObject> openPanels = new List<GameObject>();
        readonly HashSet<GameObject> mandatoryDecisionPanels = new HashSet<GameObject>();

        int lastPruneFrame = -1;

        public bool InputBlocked
        {
            get
            {
                // 每帧最多清理一次:该属性每帧被 PlayerController(FixedUpdate/Update)与提示刷新
                // 读取 3 次以上,而每次 Prune 都要对所有打开面板做 GetComponent/Canvas 查找(P2-12)。
                if (lastPruneFrame != Time.frameCount)
                {
                    lastPruneFrame = Time.frameCount;
                    PruneClosedPanels();
                }
                return openPanels.Count > 0 || CutsceneActive;
            }
        }
        public bool IsAnyPanelOpen { get { return InputBlocked; } }

        public static UIManager Create()
        {
            if (I != null) return I;
            GameObject go = new GameObject("UIManager");
            return go.AddComponent<UIManager>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            InitializeUi();
        }

        void InitializeUi()
        {
            if (initialized || (I != null && I != this)) return;
            I = this;
            initialized = true;
            Font.textureRebuilt -= OnFontTextureRebuilt;
            Font.textureRebuilt += OnFontTextureRebuilt;

            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // SDF 中文字体在非整数窗口比例下比像素对齐的位图文本更清晰。
            canvas.pixelPerfect = false;
            canvasScaler = canvasGo.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1280, 720);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            GameObject esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(transform, false);
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            BuildHud(canvasGo.transform);
        }

#if UNITY_EDITOR
        public void InitializeForValidation()
        {
            InitializeUi();
        }
#endif

        void Start()
        {
            SubscribeEvents();
            RefreshHud();
        }

        void OnEnable()
        {
            if (initialized) SubscribeEvents();
        }

        void SubscribeEvents()
        {
            GameState state = GameState.I;
            if (subscribedGameState != state)
            {
                if (subscribedGameState != null)
                {
                    subscribedGameState.OnStateChanged -= RefreshHud;
                    subscribedGameState.OnCardUnlocked -= OnCardUnlocked;
                }
                subscribedGameState = state;
                if (subscribedGameState != null)
                {
                    subscribedGameState.OnStateChanged += RefreshHud;
                    subscribedGameState.OnCardUnlocked += OnCardUnlocked;
                }
            }

            BusinessDirector director = BusinessDirector.I;
            if (subscribedBusinessDirector != director)
            {
                if (subscribedBusinessDirector != null)
                {
                    subscribedBusinessDirector.OnLive -= OnBusinessLive;
                    subscribedBusinessDirector.OnFinished -= OnBusinessFinished;
                }
                subscribedBusinessDirector = director;
                if (subscribedBusinessDirector != null)
                {
                    subscribedBusinessDirector.OnLive += OnBusinessLive;
                    subscribedBusinessDirector.OnFinished += OnBusinessFinished;
                }
            }
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            UnsubscribeEvents();
            if (I == this) I = null;
        }

        void UnsubscribeEvents()
        {
            if (subscribedGameState != null)
            {
                subscribedGameState.OnStateChanged -= RefreshHud;
                subscribedGameState.OnCardUnlocked -= OnCardUnlocked;
                subscribedGameState = null;
            }
            if (subscribedBusinessDirector != null)
            {
                subscribedBusinessDirector.OnLive -= OnBusinessLive;
                subscribedBusinessDirector.OnFinished -= OnBusinessFinished;
                subscribedBusinessDirector = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>仅供 EditMode 测试:重绑只应由字体图集重建回调触发,不应由开面板触发。</summary>
        public bool DynamicFontRefreshPendingForTest { get { return dynamicFontRefreshPending; } }
#endif

        void OnFontTextureRebuilt(Font rebuiltFont)
        {
            if (!rebindingDynamicFont) dynamicFontRefreshPending = true;
        }

        void LateUpdate()
        {
            RefreshMinimap();
            if (!dynamicFontRefreshPending || rebindingDynamicFont) return;
            dynamicFontRefreshPending = false;
            RebindDynamicFontTexts();
        }

        void RebindDynamicFontTexts()
        {
            Font font = UiTheme.GameFont;
            if (font == null) return;
            Text[] allTexts = GetComponentsInChildren<Text>(true);
            List<Text> targets = new List<Text>(allTexts.Length);
            for (int i = 0; i < allTexts.Length; i++)
                if (allTexts[i] != null && allTexts[i].font == font)
                    targets.Add(allTexts[i]);
            if (targets.Count == 0) return;

            rebindingDynamicFont = true;
            try
            {
                for (int i = 0; i < targets.Count; i++) targets[i].font = null;
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < targets.Count; i++)
                {
                    targets[i].font = font;
                    targets[i].SetAllDirty();
                }
                Canvas.ForceUpdateCanvases();
            }
            finally
            {
                rebindingDynamicFont = false;
            }
        }

        void OnCardUnlocked(string id, string title)
        {
            ShowToast("解锁知识卡：" + title);
        }

        // ————— HUD —————

        void BuildHud(Transform canvas)
        {
            // 顶部是一张紧凑的「小镇通行票」，不再用满屏大白条/大色块压住场景。
            Image top = UiTheme.MakeImage(canvas, UiTheme.BgDark, "topbar");
            UiTheme.ApplyPixelFrame(top);
            top.rectTransform.anchorMin = new Vector2(0, 1);
            top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            top.rectTransform.sizeDelta = new Vector2(-32f, 58f);

            Image topRule = UiTheme.MakeImage(top.transform, UiTheme.Accent, "brassRule");
            topRule.rectTransform.anchorMin = new Vector2(0f, 0f);
            topRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            topRule.rectTransform.pivot = new Vector2(.5f, 0f);
            topRule.rectTransform.anchoredPosition = new Vector2(0f, 3f);
            topRule.rectTransform.sizeDelta = new Vector2(-18f, 3f);

            Text brand = UiTheme.MakeText(top.transform, "ECON TOWN  /  经济小镇", 17,
                UiTheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            brand.name = "brand";
            brand.rectTransform.anchorMin = brand.rectTransform.anchorMax = new Vector2(0, 0.5f);
            brand.rectTransform.pivot = new Vector2(0f, 0.5f);
            brand.rectTransform.anchoredPosition = new Vector2(22f, 1f);
            brand.rectTransform.sizeDelta = new Vector2(246f, 44f);

            dayText = UiTheme.MakeText(top.transform, "Day 1", 19, UiTheme.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            dayText.name = "day";
            dayText.horizontalOverflow = HorizontalWrapMode.Overflow;
            dayText.rectTransform.anchorMin = dayText.rectTransform.anchorMax = new Vector2(0, 0.5f);
            dayText.rectTransform.pivot = new Vector2(0f, 0.5f);
            dayText.rectTransform.anchoredPosition = new Vector2(274f, 1f);
            dayText.rectTransform.sizeDelta = new Vector2(450f, 44f);

            moneyText = UiTheme.MakeText(top.transform, "资金  2000", 20, UiTheme.Accent,
                TextAnchor.MiddleRight, FontStyle.Bold);
            moneyText.name = "money";
            moneyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            moneyText.rectTransform.anchorMin = moneyText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            moneyText.rectTransform.pivot = new Vector2(1f, 0.5f);
            moneyText.rectTransform.anchoredPosition = new Vector2(-92f, 1f);
            moneyText.rectTransform.sizeDelta = new Vector2(460f, 44f);

            Button settings = UiTheme.MakeButton(top.transform, "设置", ShowSettings, 16);
            settings.rectTransform().anchorMin = settings.rectTransform().anchorMax = new Vector2(1f, .5f);
            settings.rectTransform().pivot = new Vector2(1f, .5f);
            settings.rectTransform().anchoredPosition = new Vector2(-14f, 1f);
            settings.rectTransform().sizeDelta = new Vector2(64f, 36f);
            UiTheme.SetButtonStyle(settings, UiTheme.PanelLight, UiTheme.TextMain);

            // 事件提示条
            eventChipBg = UiTheme.MakeImage(canvas, UiTheme.Bad, "eventChip");
            UiTheme.ApplyPixelFrame(eventChipBg);
            eventChipBg.rectTransform.anchorMin = eventChipBg.rectTransform.anchorMax = new Vector2(1f, 1f);
            eventChipBg.rectTransform.pivot = new Vector2(1f, .5f);
            eventChipBg.rectTransform.anchoredPosition = new Vector2(-18f, -128f);
            eventChipBg.rectTransform.sizeDelta = new Vector2(420f, 38f);
            eventChipText = UiTheme.MakeText(eventChipBg.transform, "", 17, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            eventChipText.name = "eventText";
            eventChipText.rectTransform.Stretch();
            eventChipText.rectTransform.offsetMin = new Vector2(12f, 2f);
            eventChipText.rectTransform.offsetMax = new Vector2(-12f, -2f);
            eventChipBg.gameObject.SetActive(false);

            // 当前章节/任务常驻 HUD：目标感不能依赖玩家主动打开某个面板。
            taskHudBg = UiTheme.MakeImage(canvas, UiTheme.Panel, "currentTaskHud");
            UiTheme.ApplyPixelFrame(taskHudBg);
            taskHudBg.rectTransform.anchorMin = taskHudBg.rectTransform.anchorMax = new Vector2(0f, 1f);
            taskHudBg.rectTransform.pivot = new Vector2(0f, .5f);
            taskHudBg.rectTransform.anchoredPosition = new Vector2(18f, -92f);
            taskHudBg.rectTransform.sizeDelta = new Vector2(560f, 72f);
            Image taskTab = UiTheme.MakeImage(taskHudBg.transform, UiTheme.Accent, "taskTab");
            taskTab.rectTransform.anchorMin = new Vector2(0f, 0f);
            taskTab.rectTransform.anchorMax = new Vector2(0f, 1f);
            taskTab.rectTransform.pivot = new Vector2(0f, .5f);
            taskTab.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            taskTab.rectTransform.sizeDelta = new Vector2(6f, -12f);
            taskHudText = UiTheme.MakeText(taskHudBg.transform, "", 16, UiTheme.TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            taskHudText.name = "currentTaskText";
            taskHudText.rectTransform.Stretch();
            taskHudText.rectTransform.offsetMin = new Vector2(20f, 6f);
            taskHudText.rectTransform.offsetMax = new Vector2(-12f, -6f);

            // 阶段提示
            hintText = UiTheme.MakeText(top.transform, "", 16, UiTheme.TextSub, TextAnchor.MiddleRight,
                FontStyle.Bold);
            hintText.name = "phaseHint";
            hintText.rectTransform.anchorMin = new Vector2(1, 1);
            hintText.rectTransform.anchorMax = new Vector2(1, 1);
            hintText.rectTransform.pivot = new Vector2(1f, 0.5f);
            hintText.rectTransform.anchoredPosition = new Vector2(-18f, -78f);
            hintText.rectTransform.sizeDelta = new Vector2(438f, 36f);
            phaseHintBg = UiTheme.MakeImage(top.transform, UiTheme.Panel, "phaseHintBg");
            UiTheme.ApplyPixelFrame(phaseHintBg);
            phaseHintBg.rectTransform.anchorMin = phaseHintBg.rectTransform.anchorMax = new Vector2(1, 1);
            phaseHintBg.rectTransform.pivot = new Vector2(1f, 0.5f);
            phaseHintBg.rectTransform.anchoredPosition = new Vector2(-14f, -78f);
            phaseHintBg.rectTransform.sizeDelta = new Vector2(456f, 40f);
            phaseHintBg.transform.SetSiblingIndex(hintText.transform.GetSiblingIndex());

            // 交互提示是一张短票据，只在有可交互对象时出现。
            promptBg = UiTheme.MakeImage(canvas, UiTheme.Panel, "interactionPromptBg");
            UiTheme.ApplyPixelFrame(promptBg);
            promptBg.gameObject.SetActive(false);
            promptText = UiTheme.MakeText(canvas, "", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            promptText.name = "interactionPrompt";

            // V0.2 竞争状态条（第 16 天后出现）
            competitionHudBg = UiTheme.MakeImage(canvas, UiTheme.Market, "competitionHud");
            UiTheme.ApplyPixelFrame(competitionHudBg);
            competitionHudBg.rectTransform.anchorMin = new Vector2(0f, 0f);
            competitionHudBg.rectTransform.anchorMax = new Vector2(1f, 0f);
            competitionHudBg.rectTransform.pivot = new Vector2(0.5f, 0f);
            competitionHudText = UiTheme.MakeText(competitionHudBg.transform, "", 16, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            competitionHudText.name = "competitionText";
            competitionHudText.rectTransform.Stretch();
            competitionHudText.rectTransform.offsetMin = new Vector2(10f, 2f);
            competitionHudText.rectTransform.offsetMax = new Vector2(-10f, -2f);
            competitionHudBg.gameObject.SetActive(false);

            // 底部只保留九个高频口袋按钮；消费实验和设置分别回到居民页/顶栏。
            Image dock = UiTheme.MakeImage(canvas, UiTheme.BgDark, "bottomDock");
            UiTheme.ApplyPixelFrame(dock);
            bottomDockRt = dock.rectTransform;
            bottomDockRt.anchorMin = new Vector2(0f, 0f);
            bottomDockRt.anchorMax = new Vector2(1f, 0f);
            bottomDockRt.pivot = new Vector2(0.5f, 0f);

            string[] nav = { "任务", "店铺", "市场", "曲线", "生活", "市政", "宏观", "金融", "手册" };
            dockButtons = new Button[nav.Length];
            float dockInner = 1280f - DockLeft - 12f;
            float step = (dockInner - 20f) / nav.Length;
            float buttonW = step - 8f;
            float start = -dockInner * 0.5f + 10f + step * 0.5f;
            for (int i = 0; i < nav.Length; i++)
            {
                int idx = i;
                Button b = UiTheme.MakeButton(dock.transform, nav[i], () => OpenDock(idx), 16);
                RectTransform br = b.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
                br.pivot = new Vector2(0.5f, 0f);
                br.anchoredPosition = new Vector2(start + i * step, 8f);
                br.sizeDelta = new Vector2(buttonW, 42f);
                UiTheme.SetButtonStyle(b, UiTheme.Paper, UiTheme.Ink);
                dockButtons[i] = b;
            }

            BuildMinimap(canvas);

            // Toast 容器
            GameObject tr = new GameObject("toasts", typeof(RectTransform), typeof(RectMask2D));
            tr.transform.SetParent(canvas, false);
            toastRoot = tr.GetComponent<RectTransform>();
            toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(1, 0);
            toastRoot.anchoredPosition = new Vector2(-18f, 88f);
            toastRoot.sizeDelta = new Vector2(420f, 610f);
            toastRoot.pivot = new Vector2(1, 0);

            // 面板容器
            GameObject pr = new GameObject("panels", typeof(RectTransform));
            pr.transform.SetParent(canvas, false);
            panelRoot = pr.GetComponent<RectTransform>();
            panelRoot.Stretch();
            panelRoot.SetAsLastSibling();

            panelShade = UiTheme.MakeImage(panelRoot, new Color32(0x0a, 0x18, 0x1c, 188), "modalShade");
            panelShade.rectTransform.Stretch();
            panelShade.raycastTarget = true;
            panelShade.gameObject.SetActive(false);

            // V0.7.4 过场层:全屏 320×180×4 插画盖在一切之上,由 CutsceneDirector 独占驱动,
            // 不入 openPanels 栈(避免被通用 Esc/CloseAll 摧毁绕过导演的「视为已看」口径)。
            GameObject cl = new GameObject("cutsceneLayer", typeof(RectTransform));
            cl.transform.SetParent(canvas, false);
            cutsceneLayer = cl.GetComponent<RectTransform>();
            cutsceneLayer.Stretch();
            cutsceneLayer.gameObject.SetActive(false);

            // 围猎三#18:Toast 是短暂全局提示,须浮在面板与过场层之上——toasts 先于两者创建,
            // 不在此压轴一次就会被 panelRoot(:423)/cutsceneLayer 的 SetAsLastSibling 盖住。
            toastRoot.SetAsLastSibling();
        }

        RectTransform cutsceneLayer;

        /// <summary>V0.7.4:过场播放中——与面板共用 InputBlocked 锁(§149),但 Esc/跳过只归导演管。</summary>
        public bool CutsceneActive { get; private set; }

        /// <summary>过场开始:取回全屏层并置输入锁。重复取回视为编程错误,直接复用并清空子物体。</summary>
        public RectTransform AcquireCutsceneLayer()
        {
            if (!initialized) InitializeUi();
            if (cutsceneLayer == null) return null;
            for (int i = cutsceneLayer.childCount - 1; i >= 0; i--)
            {
                Transform child = cutsceneLayer.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            CutsceneActive = true;
            cutsceneLayer.gameObject.SetActive(true);
            cutsceneLayer.SetAsLastSibling();
            return cutsceneLayer;
        }

        /// <summary>过场结束(自然播完或跳过):清层、解输入锁。</summary>
        public void ReleaseCutsceneLayer()
        {
            CutsceneActive = false;
            if (cutsceneLayer == null) return;
            for (int i = cutsceneLayer.childCount - 1; i >= 0; i--)
            {
                Transform child = cutsceneLayer.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            cutsceneLayer.gameObject.SetActive(false);
            if (toastRoot != null) toastRoot.SetAsLastSibling();
        }

        void LayoutBottomChrome()
        {
            float left = DockLeft;
            float bottom = DockBottom;
            if (bottomDockRt != null)
            {
                bottomDockRt.offsetMin = new Vector2(left, bottom);
                bottomDockRt.offsetMax = new Vector2(-12f, bottom + DockHeight);
            }
            bool share = competitionHudBg != null && competitionHudBg.gameObject.activeSelf;
            float row = bottom + DockHeight + ColumnGap;
            PlaceBottomSpan(competitionHudBg != null ? competitionHudBg.rectTransform : null, left, row, 34f);
            float promptY = share ? row + 34f + ColumnGap : row;
            PlaceBottomSpan(promptBg != null ? promptBg.rectTransform : null, left, promptY, 36f);
            PlaceBottomSpan(promptText != null ? promptText.rectTransform : null, left + 16f, promptY + 2f, 32f, -28f);
        }

        static void PlaceBottomSpan(RectTransform rt, float left, float y, float height, float right = -16f)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(right, y + height);
        }

        void RefreshHud()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            GovernmentSimulator.Ensure(s);
            bool residentView = s.currentView == PlayerViewMode.Resident && s.resident.profileCreated;
            bool governmentView = s.currentView == PlayerViewMode.Government;
            bool bankView = s.currentView == PlayerViewMode.Bank;
            bool centralBankView = s.currentView == PlayerViewMode.CentralBank;
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            ProgressionService.Ensure(s);
            if (dayText != null)
            {
                int day = Mathf.Max(1, s.day);
                int year = (day - 1) / 52 + 1;
                dayText.text = "Day " + s.day + " · 第" + year + "年·" +
                    AtmosphereService.SeasonName(AtmosphereService.SeasonIndex(day)) + " · " + MainlineGuide.RoleName(s);
            }
            MainlineGuide.Cue mainlineCue = MainlineGuide.Build(s);
            if (taskHudBg != null && taskHudText != null)
            {
                ChapterDefinition chapter = ProgressionService.Current(s);
                string modePrefix = ChapterPlayService.IsChapterPlay ? "篇章游玩 · " : "";
                taskHudText.text = chapter == null
                    ? "<color=#D8A84E>" + modePrefix + "自由经济时代</color>\n" + mainlineCue.line
                    : "<color=#D8A84E>" + modePrefix + "第 " + chapter.number + " 章 · " + chapter.title + "</color>\n现在：" + mainlineCue.line;
            }
            ApplyDockSpotlight(mainlineCue);
            if (moneyText != null)
            {
                string pocket = s.resident != null && s.resident.profileCreated
                    ? "  ·  口袋 ¥" + s.resident.cash.ToString("0") : "";
                moneyText.text = "店里 ¥" + s.money.ToString("0") + pocket;
            }
            if (eventChipBg != null)
            {
                bool hasTownEvent = !string.IsNullOrEmpty(s.todayEventName);
                bool hasCompetitionEvent = !string.IsNullOrEmpty(s.todayCompetitionEventName);
                bool hasLongTermEvent = s.experience != null && s.experience.todayLongTermEvent != null &&
                    !string.IsNullOrEmpty(s.experience.todayLongTermEvent.title);
                bool hasGovernmentEvent = governmentView && s.government.currentEvent != null && !s.government.currentEvent.resolved;
                bool has = hasGovernmentEvent || hasTownEvent || hasCompetitionEvent || hasLongTermEvent;
                eventChipBg.gameObject.SetActive(has);
                if (has)
                    eventChipText.text = hasGovernmentEvent ? "城市事件 · " + s.government.currentEvent.title :
                        hasLongTermEvent ? "经济新闻 · " + s.experience.todayLongTermEvent.title :
                        "今日事件 · " + (hasTownEvent ? s.todayEventName : s.todayCompetitionEventName) +
                        (hasTownEvent && hasCompetitionEvent ? "  ·  " + s.todayCompetitionEventName : "");
            }
            if (competitionHudBg != null)
            {
                competitionHudBg.gameObject.SetActive(!governmentView && !bankView && !centralBankView && (residentView || s.CompetitionActive));
                if (residentView)
                {
                    // 围猎三#3:-0.x% 四舍五入到 0 时 ToString("0") 仍带负号,先取整再按整数值判号。
                    int demandPct = Mathf.RoundToInt(s.ResidentDemandChangePercent);
                    competitionHudText.text = "镇民   ·   自由时间 " + s.resident.freeHours.ToString("0.##") + "h   ·   效用 " + s.resident.dailyUtility.ToString("0.0") +
                        "   ·   果汁需求 " + (demandPct >= 0 ? "+" : "") + demandPct + "%";
                }
                else if (s.CompetitionActive)
                {
                    float share = s.LastRecord != null && s.LastRecord.competitionActive
                        ? s.LastRecord.playerMarketShare * 100f : 50f;
                    competitionHudText.text = "市场份额 " + share.ToString("0.0") + "%   ·   品牌 " + s.playerBrand.ToString("0") +
                        "   ·   Sunny Tea ¥" + s.competitor.price.ToString("0");
                }
            }
            LayoutBottomChrome();
            if (hintText != null)
            {
                hintText.text = MainlineGuide.RoleSentence(s);
                if (phaseHintBg != null) phaseHintBg.gameObject.SetActive(!string.IsNullOrEmpty(hintText.text));
            }
        }

        // ————— 面板栈 —————

        protected GameObject BeginPanel(string title, float w, float h)
        {
            if (!initialized) InitializeUi();
            RectTransform content = UiTheme.MakePanel(panelRoot, title, w, h);
            GameObject root = content.parent.gameObject;
            root.SetActive(true);
            panelRoot.SetAsLastSibling();
            root.transform.SetAsLastSibling();
            if (toastRoot != null) toastRoot.SetAsLastSibling();   // 围猎三#18 复审:开面板置顶 panels 后 Toast 仍须压回其上,否则在场提示被全屏遮罩盖到淡出
            Canvas.ForceUpdateCanvases();
            if (!IsRenderablePanel(root))
            {
                Debug.LogError("[UIManager] 面板创建失败，未挂到可见 Canvas：" + title);
                // 失败面板不入栈:销毁后返回,避免留下一个隐形且永远关不掉的残留。
                // Play 模式 Destroy 延迟到帧末,本帧内调用方仍可安全取 content(P2-1)。
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
                return root;
            }
            openPanels.Add(root);
            UpdateModalPresentation();
            if (Application.isPlaying) StartCoroutine(FocusPanelAfterLayout(root));
            // 注意:不要在这里置位 dynamicFontRefreshPending——那会令每次开面板都全树重绑字体
            // 并强制刷新 Canvas 两遍。真正的触发时机是 OnFontTextureRebuilt(动态字体系扩充图集后)。
            return root;
        }

        /// <summary>正文比按钮上方的空位更高时，把面板加高，避免最后一行叠到按钮上。
        /// 调用前正文底边必须已经让开底栏按钮：加高拉伸区域时，按钮若锚在底部会留在原处。</summary>
        protected void FitTextAboveFooter(Text text)
        {
            if (text == null) return;
            RectTransform rect = text.rectTransform;
            float needed = MeasuredTextHeight(text);
            if (needed < 8f || rect.rect.width < 8f) return;
            float extra = needed + 8f - rect.rect.height;
            if (extra <= 1f) return;
            RectTransform content = rect.parent as RectTransform;
            GameObject panel = content != null && content.parent != null ? content.parent.gameObject : null;
            GrowPanel(panel, extra);
        }

        /// <summary>顶对齐的固定高度正文若比框更高，把框加高，返回下方控件需要下移的像素。</summary>
        protected float ExpandTopText(Text text)
        {
            if (text == null) return 0f;
            RectTransform rect = text.rectTransform;
            float needed = MeasuredTextHeight(text);
            float box = rect.rect.height > 8f ? rect.rect.height : rect.sizeDelta.y;
            if (needed < 8f || box < 8f) return 0f;
            float extra = needed + 8f - box;
            if (extra <= 1f) return 0f;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + extra);
            return extra;
        }

        protected void GrowPanel(GameObject root, float extra)
        {
            if (root == null || extra <= 1f) return;
            UiPanelViewportFitter fitter = root.GetComponent<UiPanelViewportFitter>();
            RectTransform panel = root.GetComponent<RectTransform>();
            if (fitter == null && root.transform.parent != null)
            {
                fitter = root.transform.parent.GetComponent<UiPanelViewportFitter>();
                panel = root.transform.parent.GetComponent<RectTransform>();
            }
            if (panel == null) return;
            Vector2 size = panel.sizeDelta + new Vector2(0f, extra);
            panel.sizeDelta = size;
            if (fitter != null) fitter.Configure(size.x, size.y);
        }

        protected float MeasuredTextHeight(Text text)
        {
            if (text == null) return 0f;
            Canvas.ForceUpdateCanvases();
            CrispTextMirror mirror = text.GetComponent<CrispTextMirror>();
            if (mirror != null)
            {
                float visible = mirror.VisiblePreferredHeight();
                if (visible > 1f) return visible;
            }
            return text.preferredHeight;
        }

        static bool IsRenderablePanel(GameObject panel)
        {
            if (panel == null || !panel.activeInHierarchy) return false;
            RectTransform rt = panel.GetComponent<RectTransform>();
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            return rt != null && rt.rect.width > 1f && rt.rect.height > 1f &&
                canvas != null && canvas.isActiveAndEnabled;
        }

        void PruneClosedPanels()
        {
            bool pruned = false;
            for (int i = openPanels.Count - 1; i >= 0; i--)
                if (!IsRenderablePanel(openPanels[i]))
                {
                    GameObject panel = openPanels[i];
                    mandatoryDecisionPanels.Remove(panel);
                    openPanels.RemoveAt(i);
                    // 不可渲染的面板要连同 GameObject 一起清理:只出栈不销毁会留下永久的屏幕残留,
                    // 且强制拦截被解除后玩家再也回不到该决策(P2-1)。
                    if (panel != null)
                    {
                        if (Application.isPlaying) Destroy(panel);
                        else DestroyImmediate(panel);
                    }
                    pruned = true;
                }
            // 围猎三(#11):清理后新栈顶要重新获得交互权(见 UpdateModalPresentation 的模态化)。
            if (pruned) UpdateModalPresentation();
        }

        protected void MarkMandatoryDecision(GameObject panel)
        {
            if (panel != null) mandatoryDecisionPanels.Add(panel);
        }

        /// <summary>必须点选才能离开的事件窗：右上角的叉点了也不会关，留着只像坏按钮。</summary>
        protected void HideFalseClose(GameObject panel)
        {
            if (panel == null) return;
            Transform bar = panel.transform.Find("titlebar");
            if (bar == null) return;
            for (int i = 0; i < bar.childCount; i++)
            {
                Text label = bar.GetChild(i).GetComponentInChildren<Text>();
                if (label != null && label.text == "×")
                    bar.GetChild(i).gameObject.SetActive(false);
            }
        }

        static readonly Color EventInkSoft = new Color32(0x5a, 0x4e, 0x42, 255);

        /// <summary>事件纸页。返回高度。正文按实际行数撑开。</summary>
        protected float EventBrief(RectTransform parent, string kicker, string body, float x, float y, float width, Color accent)
        {
            float textW = Mathf.Max(80f, width - 52f);
            Image card = UiTheme.MakeImage(parent, UiTheme.Paper, "eventBrief");
            UiTheme.ApplyTicketFrame(card);
            PlaceTopLeft(card.rectTransform, x, y, width, 120f);

            Text copy = UiTheme.MakeText(card.transform, body, 20, UiTheme.Ink, TextAnchor.UpperLeft);
            PlaceTopLeft(copy.rectTransform, 28f, 44f, textW, 64f);
            float bodyH = Mathf.Max(48f, MeasuredTextHeight(copy) + 6f);
            copy.rectTransform.sizeDelta = new Vector2(textW, bodyH);

            float cardH = 44f + bodyH + 16f;
            card.rectTransform.sizeDelta = new Vector2(width, cardH);

            Image stripe = UiTheme.MakeImage(card.transform, accent, "eventStripe");
            PlaceTopLeft(stripe.rectTransform, 8f, 12f, 6f, cardH - 24f);

            Text kick = UiTheme.MakeText(card.transform, kicker, 15, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopLeft(kick.rectTransform, 28f, 14f, textW, 24f);
            return cardH;
        }

        protected Button EventChoice(RectTransform parent, string title, string detail, float x, float y, float width, System.Action action)
        {
            const float height = 88f;
            Button button = UiTheme.MakeButton(parent, title, action, 20);
            PlaceTopLeft(button.rectTransform(), x, y, width, height);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.fontStyle = FontStyle.Bold;
                label.color = UiTheme.Ink;
                PlaceTopLeft(label.rectTransform, 18f, 12f, width - 36f, 32f);
            }
            Text sub = UiTheme.MakeText(button.transform, detail, 16, EventInkSoft, TextAnchor.MiddleLeft);
            PlaceTopLeft(sub.rectTransform, 18f, 46f, width - 36f, 28f);
            return button;
        }

        protected void FitEventPanel(GameObject root, float contentBottom)
        {
            if (root == null) return;
            RectTransform panel = root.GetComponent<RectTransform>();
            if (panel == null) return;
            float extra = contentBottom + 16f - (panel.sizeDelta.y - 88f);
            if (extra > 1f) GrowPanel(root, extra);
        }

        protected void ResolveMandatoryDecision(GameObject panel)
        {
            if (panel != null) mandatoryDecisionPanels.Remove(panel);
            ClosePanel(panel);
        }

        protected RectTransform ContentOf(GameObject root)
        {
            return root.transform.Find("content") as RectTransform;
        }

        public void CloseTop()
        {
            PruneClosedPanels();
            if (openPanels.Count == 0) return;
            ClosePanel(openPanels[openPanels.Count - 1]);
        }

        public void ClosePanel(GameObject panel)
        {
            PruneClosedPanels();
            int index = openPanels.IndexOf(panel);
            if (index < 0)
            {
                // 未入栈的面板(BeginPanel 失败路径等)也要能被 × 按钮关掉:降级为直接销毁,
                // 否则它永远留在场景里、不计入 InputBlocked、也无人能再触碰(P2-1)。
                if (panel != null)
                {
                    if (Application.isPlaying) Destroy(panel);
                    else DestroyImmediate(panel);
                }
                UpdateModalPresentation();
                return;
            }
            if (mandatoryDecisionPanels.Contains(panel))
            {
                ShowToast("这是本日关键决策，选择一项方案后才能离开");
                return;
            }

            bool wasTop = index == openPanels.Count - 1;
            openPanels.RemoveAt(index);
            mandatoryDecisionPanels.Remove(panel);
            if (panel != null)
            {
                if (Application.isPlaying) Destroy(panel);
                else DestroyImmediate(panel);
            }
            UpdateModalPresentation();
            if (Application.isPlaying && wasTop && openPanels.Count > 0)
                StartCoroutine(FocusPanelAfterLayout(openPanels[openPanels.Count - 1]));
        }

        public void CloseAll()
        {
            while (openPanels.Count > 0)
            {
                int before = openPanels.Count;
                CloseTop();
                if (openPanels.Count == before) break;
            }
        }

        void UpdateModalPresentation()
        {
            bool hasPanel = openPanels.Count > 0;
            if (panelShade != null) panelShade.gameObject.SetActive(hasPanel);
            if (promptText != null) promptText.text = hasPanel ? "" : pendingPrompt;
            if (promptBg != null) promptBg.gameObject.SetActive(!hasPanel && !string.IsNullOrEmpty(pendingPrompt));
            // 围猎三(#11):面板栈模态化——只有栈顶可交互。此前叠加面板只是视觉部分遮挡,
            // 下层未被盖住的按钮(如解释弹窗下缘之外仍裸露的「进入下一天 →」)可以点穿,
            // CloseTop 关掉的是弹窗而动作已执行:一天被误推进、陈旧日报可对未营业日二次结算。
            for (int i = 0; i < openPanels.Count; i++)
            {
                GameObject panel = openPanels[i];
                if (panel == null) continue;
                CanvasGroup group = panel.GetComponent<CanvasGroup>();
                if (group == null) group = panel.AddComponent<CanvasGroup>();
                bool top = i == openPanels.Count - 1;
                group.blocksRaycasts = top;
                group.interactable = top;
            }
            if (!hasPanel && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        IEnumerator FocusPanelAfterLayout(GameObject panel)
        {
            yield return null;
            if (panel == null || !panel.activeInHierarchy || EventSystem.current == null) yield break;
            RectTransform content = ContentOf(panel);
            Button[] buttons = content != null ? content.GetComponentsInChildren<Button>(true) : panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].IsActive() || !buttons[i].interactable) continue;
                EventSystem.current.SetSelectedGameObject(buttons[i].gameObject);
                yield break;
            }
        }

        // ————— 提示 —————

        public void ShowPrompt(string msg)
        {
            pendingPrompt = msg == null ? "" : msg;
            if (promptText != null) promptText.text = InputBlocked ? "" : pendingPrompt;
            if (promptBg != null) promptBg.gameObject.SetActive(!InputBlocked && !string.IsNullOrEmpty(pendingPrompt));
        }

        public void ShowToast(string msg)
        {
            if (toastRoot == null) return;
            GameObject go = new GameObject("toast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(toastRoot, false);
            Image bg = go.GetComponent<Image>();
            bg.color = UiTheme.PanelLight;
            UiTheme.ApplyPixelFrame(bg);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = Vector2.zero;
            int lines = EstimateToastLines(msg, 20);
            float toastHeight = Mathf.Clamp(28f + lines * 27f, 58f, 136f);
            rt.sizeDelta = new Vector2(420f, toastHeight);
            Image tab = UiTheme.MakeImage(go.transform, UiTheme.Accent, "toastTab");
            tab.rectTransform.anchorMin = new Vector2(0f, 0f);
            tab.rectTransform.anchorMax = new Vector2(0f, 1f);
            tab.rectTransform.pivot = new Vector2(0f, .5f);
            tab.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            tab.rectTransform.sizeDelta = new Vector2(6f, -12f);
            Text t = UiTheme.MakeText(go.transform, msg, 18, UiTheme.TextMain, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(22f, 8f);
            t.rectTransform.offsetMax = new Vector2(-16f, -8f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            if (CutsceneActive && cutsceneLayer != null)
                cutsceneLayer.SetAsLastSibling();
            else
                toastRoot.SetAsLastSibling();
            TrimToasts();
            ReflowToasts();
            if (Application.isPlaying) StartCoroutine(FadeOut(go.transform, Mathf.Max(3.2f, 1.8f + lines * 0.8f)));
        }

        void TrimToasts()
        {
            if (toastRoot == null) return;
            int activeCount = 0;
            for (int i = 0; i < toastRoot.childCount; i++)
                if (toastRoot.GetChild(i).gameObject.activeSelf) activeCount++;
            for (int i = 0; i < toastRoot.childCount && activeCount > MaxVisibleToasts; i++)
            {
                GameObject oldest = toastRoot.GetChild(i).gameObject;
                if (!oldest.activeSelf) continue;
                oldest.SetActive(false);
                activeCount--;
                if (Application.isPlaying) Destroy(oldest);
                else DestroyImmediate(oldest);
            }
        }

        static int EstimateToastLines(string message, int charactersPerLine)
        {
            if (string.IsNullOrEmpty(message)) return 1;
            int lines = 0;
            string[] paragraphs = message.Split('\n');
            for (int i = 0; i < paragraphs.Length; i++)
                lines += Mathf.Max(1, Mathf.CeilToInt(paragraphs[i].Length / (float)charactersPerLine));
            return Mathf.Clamp(lines, 1, 4);
        }

        void ReflowToasts()
        {
            if (toastRoot == null) return;
            float y = 0f;
            for (int i = toastRoot.childCount - 1; i >= 0; i--)
            {
                RectTransform child = toastRoot.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                child.anchoredPosition = new Vector2(0f, y);
                y += child.sizeDelta.y + 8f;
            }
        }

        IEnumerator FadeOut(Transform tf, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tf == null) yield break;
            float t = 0f;
            CanvasGroup cg = tf.gameObject.AddComponent<CanvasGroup>();
            while (t < 0.5f)
            {
                if (tf == null || cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = 1f - t / 0.5f;
                yield return null;
            }
            if (tf != null) Destroy(tf.gameObject);
            yield return null;
            ReflowToasts();
        }

        // ————— 营业覆盖层 —————

        void OnBusinessLive()
        {
            BusinessDirector d = BusinessDirector.I;
            if (d == null || businessOverlay == null) return;
            if (bizCountText != null)
                bizCountText.text = "已接待 " + d.ServedSoFar + " 位 · 今日需求 " + d.DemandTotal + " 位 · 库存 " + d.Available;
            if (bizTimeText != null) bizTimeText.text = Mathf.CeilToInt((1f - d.Progress01) * 100) + "%";
            if (bizBar != null) bizBar.fillAmount = d.Progress01;
        }

        void OnBusinessFinished(DayRecord r)
        {
            if (businessOverlay != null) Destroy(businessOverlay);
            businessOverlay = null;
            ShowReport(r);
        }

        public void BuildBusinessOverlay()
        {
            GameObject root = new GameObject("bizOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(panelRoot, false);
            businessOverlay = root;
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -78f);
            rt.sizeDelta = new Vector2(660f, 122f);

            Image bg = root.GetComponent<Image>();
            bg.color = UiTheme.BgDark;
            UiTheme.ApplyPixelFrame(bg);

            Image liveTab = UiTheme.MakeImage(root.transform, UiTheme.Accent, "liveTab");
            liveTab.rectTransform.anchorMin = new Vector2(0f, 0f);
            liveTab.rectTransform.anchorMax = new Vector2(0f, 1f);
            liveTab.rectTransform.pivot = new Vector2(0f, .5f);
            liveTab.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            liveTab.rectTransform.sizeDelta = new Vector2(7f, -14f);

            Text liveTitle = UiTheme.MakeText(root.transform, "果汁店营业中", 17, UiTheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            liveTitle.name = "businessTitle";
            liveTitle.rectTransform.anchorMin = liveTitle.rectTransform.anchorMax = new Vector2(0, 1);
            liveTitle.rectTransform.offsetMin = new Vector2(24, -31);
            liveTitle.rectTransform.offsetMax = new Vector2(280, -7);

            bizCountText = UiTheme.MakeText(root.transform, "准备开店……", 20, UiTheme.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            bizCountText.name = "businessCount";
            bizCountText.rectTransform.anchorMin = bizCountText.rectTransform.anchorMax = new Vector2(0, 1);
            bizCountText.rectTransform.pivot = new Vector2(0f, 1f);
            bizCountText.rectTransform.anchoredPosition = new Vector2(24f, -34f);
            bizCountText.rectTransform.sizeDelta = new Vector2(510f, 30f);

            Image barTrack = UiTheme.MakeImage(root.transform, UiTheme.PanelLight, "barTrack");
            barTrack.rectTransform.anchorMin = new Vector2(0f, 1f);
            barTrack.rectTransform.anchorMax = new Vector2(1f, 1f);
            barTrack.rectTransform.pivot = new Vector2(0.5f, 1f);
            barTrack.rectTransform.anchoredPosition = new Vector2(0f, -64f);
            barTrack.rectTransform.sizeDelta = new Vector2(-48f, 12f);

            bizBar = UiTheme.MakeImage(barTrack.transform, UiTheme.Accent, "barFill");
            bizBar.type = Image.Type.Filled;
            bizBar.fillMethod = Image.FillMethod.Horizontal;
            bizBar.color = UiTheme.Accent;
            bizBar.rectTransform.Stretch();

            bizTimeText = UiTheme.MakeText(root.transform, "100%", 17, UiTheme.TextSub, TextAnchor.MiddleRight, FontStyle.Bold);
            bizTimeText.name = "businessProgress";
            bizTimeText.rectTransform.anchorMin = bizTimeText.rectTransform.anchorMax = new Vector2(1, 1);
            bizTimeText.rectTransform.offsetMin = new Vector2(-108, -31);
            bizTimeText.rectTransform.offsetMax = new Vector2(-22, -7);

            Text cadence = UiTheme.MakeText(root.transform, "代表性顾客会慢慢到店，同屏只出现一位", 16,
                UiTheme.TextSub, TextAnchor.MiddleLeft);
            cadence.name = "businessCadence";
            cadence.rectTransform.anchorMin = cadence.rectTransform.anchorMax = new Vector2(0, 0);
            cadence.rectTransform.offsetMin = new Vector2(24, 10);
            cadence.rectTransform.offsetMax = new Vector2(420, 42);

            Button skip = UiTheme.MakeButton(root.transform, "跳过营业", () =>
            {
                if (BusinessDirector.I != null) BusinessDirector.I.SkipToEnd();
            }, 19);
            skip.rectTransform().anchorMin = skip.rectTransform().anchorMax = new Vector2(1, 0);
            skip.rectTransform().offsetMin = new Vector2(-166, 10);
            skip.rectTransform().offsetMax = new Vector2(-22, 46);
        }
        // ————— 开局 —————

        public void ShowFirstRunChoice()
        {
            ShowStartMenu();
        }

        /// <summary>读档成功后的世界收尾(围猎三#14/29):刷新公共设施/季节视觉/氛围缓存并补发
        /// pending 阶段报告——开局「继续经营」与游戏内「读取存档/从关键节点恢复」三条路径共用。
        /// 旧版后两条路径只刷公共设施:跨季旧档的雪帽/氛围画面与该弹的报告要等下一次收日才自愈。</summary>
        public void PostLoadRefresh(GameState s)
        {
            MapBuilder.RefreshGovernmentProjects(s);
            MapBuilder.ApplySeason(s);
            AtmosphereDirector.NotifyStateChanged();
            StageReportRecord pendingReport = ProgressionService.GetPendingStageReport(s);
            if (pendingReport != null) DeliverPendingStageReport(pendingReport);
            else if (s.experience.pendingCrisisReview != null)
                ShowCrisisReview(s.experience.pendingCrisisReview);
        }

        public void ShowConfirmation(string title, string message, System.Action confirmed,
            string confirmLabel = "确认删除并重新开始")
        {
            GameObject root = BeginPanel(title, 640, 360);
            RectTransform content = ContentOf(root);
            Text body = UiTheme.MakeText(content, message, 21, UiTheme.TextMain, TextAnchor.MiddleCenter);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(28f, 84f);
            body.rectTransform.offsetMax = new Vector2(-28f, -12f);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            FitTextAboveFooter(body);

            Button cancel = UiTheme.MakeButton(content, "取消", CloseTop, 20);
            cancel.rectTransform().anchorMin = cancel.rectTransform().anchorMax = new Vector2(0f, 0f);
            cancel.rectTransform().pivot = new Vector2(0f, 0f);
            cancel.rectTransform().anchoredPosition = new Vector2(36f, 16f);
            cancel.rectTransform().sizeDelta = new Vector2(250f, 52f);

            Button confirm = UiTheme.MakeButton(content, confirmLabel, () =>
            {
                CloseAll();
                if (confirmed != null) confirmed();
            }, 18);
            confirm.rectTransform().anchorMin = confirm.rectTransform().anchorMax = new Vector2(1f, 0f);
            confirm.rectTransform().pivot = new Vector2(1f, 0f);
            confirm.rectTransform().anchoredPosition = new Vector2(-36f, 16f);
            confirm.rectTransform().sizeDelta = new Vector2(250f, 52f);
            UiTheme.SetButtonStyle(confirm, UiTheme.Bad, Color.white);
        }
    }

    public static class ButtonEx
    {
        public static RectTransform rectTransform(this Button b) { return b.GetComponent<RectTransform>(); }
    }
}
