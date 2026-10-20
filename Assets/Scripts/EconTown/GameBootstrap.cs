using UnityEngine;
using UnityEngine.SceneManagement;

namespace EconTown
{
    /// <summary>装配入口:挂在场景中一个空物体上,运行时搭建地图/玩家/NPC/UI。</summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap I { get; private set; }

        /// <summary>V0.7.1 全局 2D 光:时段色调的唯一调制入口(只染世界不染 HUD,ScreenSpaceOverlay 不受 Light2D 影响)。</summary>
        public static UnityEngine.Rendering.Universal.Light2D GlobalLight2D { get; private set; }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Start()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("EconTown.Volume", 1f);
            if (GameState.I == null)
            {
                new GameObject("GameState").AddComponent<GameState>();
            }
            GameState s = GameState.I;
            // 无条件播种:SeedNewGame 只重置随机源,而读档会用存档里的 economy(含随机状态)整体
            // 覆盖它,两条路径互不干扰。若只在无档时播种,"有档但未点继续经营"(面板被绕过/坏档
            // 读不回来)会落在固定默认种子 20260824 的假新局上——每局事件序列雷同,推进一天
            // 还会用假进度覆盖真实存档(2026-08-25 深度审查 P0-1)。
            DaySimulator.SeedNewGame(s);
            GovernmentSimulator.Ensure(s);
            ProgressionService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            // 选档/选篇在地图创建前落定，后面的城区、设施与交互点一次就按目标世界搭建。
            ChapterPlayStartupResult startup = ChapterPlayService.ApplyPendingStartup(s);

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 7.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xa7, 0xca, 0xb8, 255);
            cam.transform.position = new Vector3(0f, 2f, -10f);

            // URP 2D 渲染器需要一盏全局光,否则精灵全黑
            GameObject lightGo = new GameObject("GlobalLight2D");
            UnityEngine.Rendering.Universal.Light2D light2d = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light2d.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light2d.intensity = 0.94f;
            light2d.color = new Color32(0xff, 0xf1, 0xd3, 255);
            GlobalLight2D = light2d;

            MapBuilder.Build(transform);

            GameObject playerGo = new GameObject("Player");
            // 从自己的店门外开局，第一眼就能看到核心经营入口和南部商业街。
            playerGo.transform.position = MapBuilder.ShopDoor + new Vector3(0f, -.8f, 0f);
            playerGo.AddComponent<PlayerController>();
            cam.transform.position = playerGo.transform.position + new Vector3(0f, 0.35f, -10f);

            GameObject followGo = new GameObject("CameraFollow");
            CameraFollow follow = followGo.AddComponent<CameraFollow>();
            follow.target = playerGo.transform;
            follow.cam = cam;

            PortSceneController.Create(playerGo.transform, follow, cam);
            ShopInteriorController.Create(playerGo.transform, follow, cam);   // V0.9.7 店内场景门

            SpawnNpcs();
            AtmosphereDirector.Create();
            WeatherParticles.Create();

            BusinessDirector.Create();
            UIManager.Create();
            PortDistrictDirector.Create();
            TownGrowthDirector.Create();   // V0.7.3:新区演出/尘土/旗色/银行亮化的每帧驱动
            CutsceneDirector.Create();     // V0.7.4 六段全屏过场(§147–153;V0.9.5 增楼市狂热)

            RegisterInteractables();

            s.Changed();
            if (startup.showStartMenu)
            {
                UIManager.I.ShowFirstRunChoice();
                if (!string.IsNullOrEmpty(startup.message)) UIManager.I.ShowToast(startup.message);
            }
            else
            {
                if (startup.loadedState || startup.startedPart) UIManager.I.PostLoadRefresh(s);
                if (startup.startedPart && startup.part != null)
                    UIManager.I.ShowChapterPlayIntro(startup.part);
                else UIManager.I.TryShowMorningMainline(s);
                if (!string.IsNullOrEmpty(startup.message)) UIManager.I.ShowToast(startup.message);
            }
        }

        static void SpawnNpcs()
        {
            string[] plan = { "学生", "学生", "学生", "学生", "白领", "白领", "白领", "白领", "游客", "游客", "工厂主", "居民代表" };
            for (int i = 0; i < plan.Length; i++)
                NpcAgent.Spawn(plan[i], TownLayoutData.InitialNpcSpawns[i]);
        }

        static void RegisterInteractables()
        {
            // V0.9.7:门口按 E/点击建筑=进店;营业面板移到店内收银台(OpenShopPanel)。
            // 底部 dock「店铺」按钮与经济新闻深链仍直开面板(不经门,零改动)。
            Interactables.Register("进入我的果汁店", MapBuilder.ShopDoor, ShopInteriorController.EnterShopInterior);
            // 复审(V0911):这两条必须用 shop 快照而非 InteriorCounterSpot/ExitSpot 动态属性——
            // 后者随 ActiveInteriorKind 查表,店内「重新开始」LoadScene 重载后该静态残留为进店前的 kind,
            // 注册会落到错店坐标(果汁店收银台失效、市场台被果汁店条目遮蔽)
            Interactables.Register("收银台 · 营业", InteriorCatalog.Get("shop").CounterWorld, OpenShopPanel, WorldArea.ShopInterior);
            Interactables.Register("走出果汁店", InteriorCatalog.Get("shop").ExitWorld,
                ShopInteriorController.ExitShopInterior, WorldArea.ShopInterior);
            // V0.9.11 打样三店:门口按 E=进店(原直开面板的回调原样搬到店内 counter 点,条件门随面板保留);
            // 果汁店保持上方显式三条(V097 按标签断言),其余店走目录循环注册
            Interactables.Register("进入原料市场", MapBuilder.MarketDoor,
                () => ShopInteriorController.EnterInterior("market"));
            for (int i = 0; i < InteriorCatalog.All.Length; i++)
            {
                InteriorSpec s = InteriorCatalog.All[i];
                if (s.kind == "shop") continue;                     // 果汁店已逐条注册
                Interactables.Register(s.counterLabel, s.CounterWorld, InteriorCounterUse(s.kind), WorldArea.ShopInterior);
                Interactables.Register(s.exitLabel, s.ExitWorld,
                    ShopInteriorController.ExitInterior, WorldArea.ShopInterior);
            }
            Interactables.Register("进入 SUNNY TEA", MapBuilder.CompetitorDoor, () =>
            {
                // V0.9.12:进店,茶台在店内。复审(gates):装修期门口照旧拦(原文案)——店内常驻茶客与
                // 营业态陈设若提前可见,会剧透第 5 章开业揭示(第 6 章清晨「Sunny Tea 今天开业了」)
                if (GameState.I.CompetitionActive) ShopInteriorController.EnterInterior("competitor");
                else UIManager.I.ShowToast("这间铺子正在装修——完成企业篇后会有新竞争者开业");
            });
            Interactables.Register("查看广场公告", MapBuilder.PlazaCenter, () => UIManager.I.ShowPlaza());
            Interactables.Register("回到我的家", MapBuilder.HomeDoor,
                () => EnterRoleBuilding(PlayerViewMode.Resident, "home"));
            Interactables.Register("进入暖炉餐厅", MapBuilder.RestaurantDoor,
                () => EnterRoleBuilding(PlayerViewMode.Resident, "restaurant"));
            Interactables.Register("进入星光电影院", MapBuilder.CinemaDoor,
                () => EnterRoleBuilding(PlayerViewMode.Resident, "cinema"));
            Interactables.Register("进入便利屋", MapBuilder.ConvenienceDoor,
                () => EnterRoleBuilding(PlayerViewMode.Resident, "convenience"));
            // V0.9.12 装饰铺转正:零嘴亭/花店门口可进店(店内交互点是纯氛围文案,零经济改动)
            Interactables.Register("进入零嘴亭", MapBuilder.DecorADoor,
                () => ShopInteriorController.EnterInterior("decor_a"));
            Interactables.Register("进入花店", MapBuilder.DecorBDoor,
                () => ShopInteriorController.EnterInterior("decor_b"));
            Interactables.Register("进入小镇银行", MapBuilder.BankDoor,
                () => EnterRoleBuilding(PlayerViewMode.Bank, "bank"));
            Interactables.Register("进入中央银行", MapBuilder.CentralBankDoor,
                () => EnterRoleBuilding(PlayerViewMode.CentralBank, "centralbank"));
            Interactables.Register("乘接驳车前往国际贸易港", MapBuilder.PortDoor,
                OpenPortIfReady, WorldArea.Town);
            Interactables.Register("返回经济小镇", MapBuilder.PortExitDoor,
                PortSceneController.ExitPort, WorldArea.Port);
            Interactables.Register("查看报关行黑板", MapBuilder.CustomsDoor,
                () => UIManager.I.ShowCustomsBoard(), WorldArea.Port);
            Interactables.Register("进入市政厅", MapBuilder.HallDoor,
                () => EnterRoleBuilding(PlayerViewMode.Government, "hall"));
            // label 随转正从「查看河畔工厂」改「进入河畔工厂」(原直开环境治理分页)
            Interactables.Register("进入河畔工厂", MapBuilder.FactoryDoor,
                () => EnterRoleBuilding(PlayerViewMode.Government, "factory"));
            Interactables.Register("进入职业培训中心", MapBuilder.TrainingDoor,
                () => EnterRoleBuilding(PlayerViewMode.Government, "training"));
            RegisterDistrictInteractables();
        }

        // V0.7.3 新区交互点:动工后注册,按标签内容去重(幂等;Build 清表后可安全重注册,
        // 落成前面板给「施工中」说明,报关行黑板先例)
        public static void RegisterDistrictInteractables()
        {
            GameState s = GameState.I;
            if (s == null || s.progression == null) return;
            int cc = s.progression.completedChapter;
            if (TownGrowthService.EastDistrict(cc) != DistrictPhase.NotStarted)
            {
                RegisterOnce("阅读本周概念", MapBuilder.ConceptBoardSpot, () => UIManager.I.ShowWeeklyConcept());
                // V0.9.12 东街书店转正:随东商业新区动工解锁进店(建筑本体全量预建,门注册守章节)。
                // 复审(wiring):换场条目必须双向对账——读档回滚到未动工的旧档时若只增不减,
                // 空草地上残留的「按 E 进书店」会把玩家传进一间该存档状态下不存在的店内
                RegisterOnce("进入东街书店", MapBuilder.BookstoreDoor,
                    () => ShopInteriorController.EnterInterior("bookstore"));
            }
            else
            {
                Interactables.Remove("进入东街书店");
                Interactables.Remove("阅读本周概念");
            }
            // bug搜#13:公告碑/利率牌与书店同生命周期(区段门控),回滚注销同样要对偶——
            // 残留条目指向已随 GrowthRoot 隐藏的牌子,玩家能在空草地上按 E 打开未解锁内容。
            if (TownGrowthService.CivicPlaza(cc) != DistrictPhase.NotStarted)
                RegisterOnce("查看公告碑", MapBuilder.CivicSteleSpot, () => UIManager.I.ShowCivicStele());
            else
                Interactables.Remove("查看公告碑");
            if (TownGrowthService.FinancialStreet(cc) != DistrictPhase.NotStarted)
                RegisterOnce("查看政策利率牌", MapBuilder.RateBoardSpot, () => UIManager.I.ShowPolicyRateBoard());
            else
                Interactables.Remove("查看政策利率牌");
        }

        static void RegisterOnce(string label, Vector3 pos, System.Action onUse)
        {
            for (int i = 0; i < Interactables.All.Count; i++)
                if (Interactables.All[i].label == label) return;
            Interactables.Register(label, pos, onUse);
        }

        /// <summary>点击建筑触发(与走到门口按 E 等价)。</summary>
        public void OnBuildingClicked(string kind)
        {
            if (UIManager.I == null || UIManager.I.InputBlocked) return;
            switch (kind)
            {
                case "shop":
                    ShopInteriorController.EnterShopInterior();   // V0.9.7:点击建筑与门口按 E 等价(进店)
                    break;
                case "market": ShopInteriorController.EnterInterior("market"); break;   // V0.9.11:点击=进店
                case "competitor":   // V0.9.12:点击=进店;复审(gates):装修期照旧拦(原点击文案),开业后再进店
                    if (GameState.I.CompetitionActive) ShopInteriorController.EnterInterior("competitor");
                    else UIManager.I.ShowToast("Sunny Tea 尚未开业——完成企业篇后进入市场竞争");
                    break;
                case "bookstore": ShopInteriorController.EnterInterior("bookstore"); break;     // V0.9.12 装饰铺转正
                case "decor_a": ShopInteriorController.EnterInterior("decor_a"); break;        // V0.9.12 装饰铺转正
                case "decor_b": ShopInteriorController.EnterInterior("decor_b"); break;        // V0.9.12 装饰铺转正
                case "bank": EnterRoleBuilding(PlayerViewMode.Bank, "bank"); break;
                case "centralbank": EnterRoleBuilding(PlayerViewMode.CentralBank, "centralbank"); break;
                case "port": OpenPortIfReady(); break;
                case "hall": EnterRoleBuilding(PlayerViewMode.Government, "hall"); break;
                case "factory": EnterRoleBuilding(PlayerViewMode.Government, "factory"); break;
                case "training": EnterRoleBuilding(PlayerViewMode.Government, "training"); break;
                case "home": EnterRoleBuilding(PlayerViewMode.Resident, "home"); break;
                case "restaurant": EnterRoleBuilding(PlayerViewMode.Resident, "restaurant"); break;
                case "cinema": EnterRoleBuilding(PlayerViewMode.Resident, "cinema"); break;
                case "convenience": EnterRoleBuilding(PlayerViewMode.Resident, "convenience"); break;
            }
        }

        /// <summary>店内收银台永远是店主的经营面板。镇民买东西走餐厅、影院和便利屋。</summary>
        static void OpenShopPanel()
        {
            UIManager.I.ShowShop();
        }

        static void EnterRoleBuilding(PlayerViewMode role, string kind)
        {
            if (!MainlineGuide.CanEnter(GameState.I, role))
            {
                if (UIManager.I != null) UIManager.I.ShowToast(MainlineGuide.LockReason(role));
                return;
            }
            ShopInteriorController.EnterInterior(kind);
        }

        static void OpenPortIfReady()
        {
            if (!MainlineGuide.CanEnterTrade(GameState.I))
            {
                if (UIManager.I != null) UIManager.I.ShowToast(MainlineGuide.TradeLockReason());
                return;
            }
            PortSceneController.EnterPort();
        }

        static void OpenRoleCounter(PlayerViewMode role, System.Action open)
        {
            if (!MainlineGuide.CanEnter(GameState.I, role))
            {
                if (UIManager.I != null) UIManager.I.ShowToast(MainlineGuide.LockReason(role));
                return;
            }
            if (open != null) open();
        }

        /// <summary>V0.9.11 店内面板交互点的回调接线(行为留 Bootstrap,InteriorCatalog 只放数据):
        /// 各店原门口回调原样搬入——清晨/章节/建档等条件门都在面板函数内部,搬动零丢失。
        /// V0.9.12 追加六间:三家功能店搬原面板;装饰铺三间(书店/花店/零嘴亭)原无交互,
        /// 店内点是纯氛围文案 toast——零经济改动(装饰铺转正的呈现层边界)。
        /// V0.9.13 追加五间(机构产业):银行/央行搬原回调(央行 unlocked 门与 37 章信号
        /// 在 ShowFinanceCenter(3) 面板内,搬动零丢失);政府三间开各自政府面板分页。</summary>
        static System.Action InteriorCounterUse(string kind)
        {
            switch (kind)
            {
                case "restaurant": return () => OpenRoleCounter(PlayerViewMode.Resident, () => UIManager.I.ShowResidentCommerce("食物"));
                case "cinema": return () => OpenRoleCounter(PlayerViewMode.Resident, () => UIManager.I.ShowResidentCommerce("娱乐"));
                case "market": return () => UIManager.I.ShowMarket();
                case "competitor": return () => UIManager.I.ShowCompetitorShop();
                case "home": return () => OpenRoleCounter(PlayerViewMode.Resident, () => UIManager.I.ShowResidentHome());
                case "convenience": return () => OpenRoleCounter(PlayerViewMode.Resident, () => UIManager.I.ShowResidentCommerce("日用品"));
                case "bookstore": return () => UIManager.I.ShowToast("随手翻了几页——这周新到的散文集，墨香还没散");
                case "decor_b": return () => UIManager.I.ShowToast("挑了一束洋甘菊——店家说今天买花的人特别多");
                case "decor_a": return () => UIManager.I.ShowToast("从窗口买了一包跳跳糖——老板多送了一颗话梅");
                case "bank": return OpenBank;                                  // 方法组:身份门在 OpenBank 内
                case "centralbank": return OpenCentralBank;                    // 方法组:身份门在 OpenCentralBank 内
                case "hall": return () => OpenRoleCounter(PlayerViewMode.Government, () => UIManager.I.ShowGovernmentDashboard());
                case "training": return () => OpenRoleCounter(PlayerViewMode.Government, () => UIManager.I.ShowGovernmentDashboard(2));
                case "factory": return () => OpenRoleCounter(PlayerViewMode.Government, () => UIManager.I.ShowGovernmentDashboard(4));
                default: return OpenShopPanel;
            }
        }

        static void OpenBank()
        {
            OpenRoleCounter(PlayerViewMode.Bank, () => UIManager.I.ShowFinanceCenter(0));
        }

        static void OpenCentralBank()
        {
            OpenRoleCounter(PlayerViewMode.CentralBank, () => UIManager.I.ShowFinanceCenter(3));
        }

        /// <summary>报告关闭后进入下一天。</summary>
        public void NextDay()
        {
            TryNextDay();
        }

        public bool TryNextDay()
        {
            GameState s = GameState.I;
            int dayBefore = s.day;
            bool marketingBudgetBefore = s.marketingBudget > 0f;
            if (!WorldSettlementService.TrySettleAndAdvance(s, out int spoiled, out string blockedReason))
            {
                UIManager.I.ShowToast(blockedReason);
                if (s.government.currentEvent != null && !s.government.currentEvent.resolved)
                    UIManager.I.ShowGovernmentEvent();
                else if (ProgressionService.RequiresPriceWarChoice(s))
                    // V0.7.4 过场 2:首次价格战面板之前(§148-2;阻塞路径与清晨路径同一入口)
                    CutsceneDirector.TryPlay(s, (int)CutsceneId.PriceWarErupts, UIManager.I.ShowPriceWarChoice);
                else if (s.economy.financialCrisisActive)
                    // V0.7.4 过场 3:「处理财务危机」面板弹出之前(§148-3,报告阶段收日被阻塞处)
                    CutsceneDirector.TryPlay(s, (int)CutsceneId.CrisisClouds, UIManager.I.ShowFinancialCrisis);
                return false;
            }

            s.phase = GamePhase.Morning;
            AtmosphereDirector.PlayDawn();   // V0.7.1:收日主线的时间流逝感演出(夜→晨 1.2s,任意输入可打断)
            PortDistrictDirector.BeginMorning(s);   // V0.7.2:离港渐隐与次晨靠港/开港演出并行，不阻塞收日
            TownGrowthDirector.BeginMorning(s);     // V0.7.3:新区两态刷新与落成演出(结业报告>开港>落成,上限 2 段)
            if (s.CompetitionActive) CompetitorAI.PrepareDay(s);
            s.Changed();
            if (s.experience.settings.autoSave) SaveSystem.Save(s);

            StageReportRecord stageReport = ProgressionService.GetPendingStageReport(s);
            if (stageReport != null)
            {
                // V0.7.4 过场 4:贸易战待决标记首次成立的次日清晨,先于一切面板(§148-4)。
                // 与阶段报告撞同一清晨时过场先行,报告保持 pending 次晨照常弹出——顺序由构造保证。
                if (CutsceneDirector.TryPlay(s, (int)CutsceneId.TradeWarHeadline, null)) return true;
                // V0.9.17:Finale(第八篇收弧)与五段篇间晋升过场统一收敛进 DeliverPendingStageReport
                // (读档补发与晨闸共用同一交付口,修复轮回G 的意图顺延)——授予在前、成绩单在后。
                UIManager.I.DeliverPendingStageReport(stageReport);
                return true;
            }
            // 过场 4 的无宿主路径:红卡横幅在世界经济页之内,过场只补「事件感」,播完继续正常清晨。
            if (CutsceneDirector.TryPlay(s, (int)CutsceneId.TradeWarHeadline, null)) return true;
            // V0.9.5 过场 6:镇房价首次进入狂热相位(Inflating)的次日清晨,无宿主弹窗——播完继续正常清晨。
            if (CutsceneDirector.TryPlay(s, (int)CutsceneId.HousingMania, null)) return true;

            if (s.experience.pendingCrisisReview != null)
            {
                UIManager.I.ShowCrisisReview(s.experience.pendingCrisisReview);
                return true;
            }

            if (spoiled > 0) UIManager.I.ShowToast("一夜过去,库存损耗了 " + spoiled + " 杯果汁");
            if (marketingBudgetBefore && s.history.Count > 0)
            {
                DayRecord settled = s.history[s.history.Count - 1];
                if (settled.day == dayBefore && !settled.shopOpen)
                    UIManager.I.ShowToast("第 " + dayBefore + " 天没有开门营业:广告预算未投放、未扣款,也不产生任何品牌效果");
            }
            if (!string.IsNullOrEmpty(s.todayEventName))
                UIManager.I.ShowToast("今日事件：" + s.todayEventName + "（广场公告可查看详情）");
            else if (s.day == TimelineConfig.TownEventsStartDay) UIManager.I.ShowToast("第 " + s.day + " 天起:随机市场事件登场!");
            // 章节解锁提示按"尚未通知过该章"判定（幂等）；连过数章时同清晨各弹一条，不吞提示。
            if (s.progression.completedChapter >= 1 && s.progression.notifiedUnlockChapter < 1)
            {
                UIManager.I.ShowToast("章节解锁：计划生产——供给由你决定");
                s.progression.notifiedUnlockChapter = 1;
            }
            if (s.progression.completedChapter >= 2 && s.progression.notifiedUnlockChapter < 2)
            {
                UIManager.I.ShowToast("章节解锁：原料采购——开始管理成本");
                s.progression.notifiedUnlockChapter = 2;
            }
            if (s.progression.completedChapter >= 3 && s.progression.notifiedUnlockChapter < 3)
            {
                UIManager.I.ShowToast("章节解锁：招聘员工——观察边际产量");
                s.progression.notifiedUnlockChapter = 3;
            }

            if (!string.IsNullOrEmpty(s.todayCompetitionEventName))
                UIManager.I.ShowToast("竞争事件：" + s.todayCompetitionEventName);
            if (s.experience.todayLongTermEvent != null)
                UIManager.I.ShowToast("经济新闻：" + s.experience.todayLongTermEvent.title + " · " +
                    s.experience.todayLongTermEvent.effect);

            ChapterDefinition currentChapter = ProgressionService.Current(s);
            bool morningTaken = false;
            if (currentChapter != null && currentChapter.number == 6 &&
                !ProgressionService.HasSignal(s, ProgressSignals.CompetitorOpened))
                // V0.7.4 过场 1:第 6 章清晨,Sunny Tea 开业介绍弹窗之前(§148-1)
                morningTaken = CutsceneDirector.TryPlay(s, (int)CutsceneId.SunnyTeaOpening, UIManager.I.ShowCompetitionIntro);
            else if (currentChapter != null && currentChapter.number == 7)
                UIManager.I.ShowToast("顾客正在两家店之间选择——去市场中心查看你的份额");
            else if (currentChapter != null && currentChapter.number == 8)
                UIManager.I.ShowToast("Sunny Tea 主动降价了：可以用价格，也可以用品质和品牌回应");
            else if (ProgressionService.RequiresPriceWarChoice(s))
                // V0.7.4 过场 2:第 9 章首次弹出价格战面板之前(§148-2,清晨路径)
                morningTaken = CutsceneDirector.TryPlay(s, (int)CutsceneId.PriceWarErupts, UIManager.I.ShowPriceWarChoice);
            if (!morningTaken) UIManager.I.TryShowMorningMainline(s);
            return true;
        }

        public void RestartGame()
        {
            // 围猎五(#1):进店/进港时 active scene 已切到 CreateScene 的运行时场景
            // (EconTown_*_Runtime,buildIndex=-1),按 GetActiveScene().buildIndex 加载只会在
            // Console 报错而不重载——而存档已先被 Delete,玩家面对删档后的旧世界原地不动。
            // 按 Build Settings 主场景名加载,与当前 active scene 解耦。
            SceneManager.LoadScene(RestartSceneName);
        }

        /// <summary>重开/读档回主场景的目标名(Build Settings 唯一持久场景,永不随 active scene 变)。</summary>
        public const string RestartSceneName = "Main";
    }

    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Camera cam;

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            Vector3 p = target.position;
            p.z = -10f;
            p.y += 0.35f;
            // 视野不得越出地图(含围墙):按当前窗口纵横比动态计算半宽/半高再 clamp。
            // 旧的固定 ±18 / (-10.5,12) 在 16:9 下东西越界 4.5 单位、南北越界 2~3.6 单位,
            // 沿边走能看到地图外的纯色背景(P2-10)。
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            bool interior = ShopInteriorController.IsInsideShopInterior;   // V0.9.7:店内优先于港区
            bool port = !interior && PortSceneController.IsInsidePort;
            float minX = interior ? MapBuilder.InteriorMinX : port ? MapBuilder.PortWorldMinX : MapBuilder.WorldMinX;
            float maxX = interior ? MapBuilder.InteriorMaxX : port ? MapBuilder.PortWorldMaxX : MapBuilder.WorldMaxX;
            float minY = interior ? MapBuilder.InteriorMinY : port ? MapBuilder.PortWorldMinY : MapBuilder.WorldMinY;
            float maxY = interior ? MapBuilder.InteriorMaxY : port ? MapBuilder.PortWorldMaxY : MapBuilder.WorldMaxY;
            p.x = ClampInsideMap(p.x, halfW, minX, maxX);
            p.y = ClampInsideMap(p.y, halfH, minY, maxY);
            p = Vector3.Lerp(cam.transform.position, p, 4.5f * Time.deltaTime);
            p.x = Mathf.Round(p.x * PixelArt.PPU) / PixelArt.PPU;
            p.y = Mathf.Round(p.y * PixelArt.PPU) / PixelArt.PPU;
            cam.transform.position = p;
        }

        public void SnapToTarget()
        {
            if (target == null || cam == null) return;
            Vector3 p = target.position + new Vector3(0f, .35f, -10f);
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            bool interior = ShopInteriorController.IsInsideShopInterior;   // V0.9.7:店内优先于港区
            bool port = !interior && PortSceneController.IsInsidePort;
            p.x = ClampInsideMap(p.x, halfW,
                interior ? MapBuilder.InteriorMinX : port ? MapBuilder.PortWorldMinX : MapBuilder.WorldMinX,
                interior ? MapBuilder.InteriorMaxX : port ? MapBuilder.PortWorldMaxX : MapBuilder.WorldMaxX);
            p.y = ClampInsideMap(p.y, halfH,
                interior ? MapBuilder.InteriorMinY : port ? MapBuilder.PortWorldMinY : MapBuilder.WorldMinY,
                interior ? MapBuilder.InteriorMaxY : port ? MapBuilder.PortWorldMaxY : MapBuilder.WorldMaxY);
            p.x = Mathf.Round(p.x * PixelArt.PPU) / PixelArt.PPU;
            p.y = Mathf.Round(p.y * PixelArt.PPU) / PixelArt.PPU;
            cam.transform.position = p;
        }

        public static float ClampInsideMap(float value, float halfExtent, float minimum, float maximum)
        {
            float low = minimum + halfExtent;
            float high = maximum - halfExtent;
            // 窗口比当前场景还宽时按该场景真实中心夹取。
            if (low > high) return (minimum + maximum) * .5f;
            return Mathf.Clamp(value, low, high);
        }
    }
}
