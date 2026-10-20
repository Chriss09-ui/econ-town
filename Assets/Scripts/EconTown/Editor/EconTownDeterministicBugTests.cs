#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EconTown
{
    public sealed class DeterministicBugTestUIManager : UIManager
    {
        public GameObject OpenPanel(string title, float width, float height)
        {
            return BeginPanel(title, width, height);
        }
    }

    public class EconTownDeterministicBugTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            DestroySceneComponents<UIManager>();
            DestroySceneComponents<BusinessDirector>();
            DestroySceneComponents<GameState>();

            stateObject = new GameObject("DeterministicBugTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            ResetSimulationState();
        }

        [TearDown]
        public void TearDown()
        {
            DestroySceneComponents<UIManager>();
            DestroySceneComponents<BusinessDirector>();
            if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
            GameState.I = null;
        }

        [Test]
        public void PanelCloseButtonClosesItsOwnPanelInsteadOfTheTopPanel()
        {
            DeterministicBugTestUIManager ui = CreateTestUi();
            GameObject lower = ui.OpenPanel("下层", 420f, 300f);
            GameObject upper = ui.OpenPanel("上层", 420f, 300f);
            Button lowerClose = lower.transform.Find("titlebar/btn_×").GetComponent<Button>();

            lowerClose.onClick.Invoke();

            Assert.IsTrue(lower == null, "点击下层面板的 × 应关闭下层面板自身。");
            Assert.IsTrue(upper != null && upper.activeInHierarchy, "上层面板不应被下层面板的 × 误关。");
            Assert.IsTrue(ui.InputBlocked, "上层面板仍存在时，场景输入必须继续被阻挡。");
        }

        [Test]
        public void ToastStackKeepsNewestFourInsideItsSafeContainer()
        {
            DeterministicBugTestUIManager ui = CreateTestUi();
            string longMessage = new string('测', 100);
            for (int i = 0; i < 5; i++) ui.ShowToast(longMessage + i);
            Canvas.ForceUpdateCanvases();

            RectTransform toastRoot = ui.transform.Find("Canvas/toasts") as RectTransform;
            RectTransform dock = ui.transform.Find("Canvas/bottomDock") as RectTransform;
            Assert.NotNull(toastRoot);
            Assert.NotNull(toastRoot.GetComponent<RectMask2D>());
            Assert.AreEqual(4, toastRoot.childCount, "超过上限时应淘汰最旧 Toast，保留最新四条。");

            float dockTop = dock.anchoredPosition.y + dock.rect.height;
            Assert.GreaterOrEqual(toastRoot.anchoredPosition.y, dockTop,
                "Toast 安全区底边不得侵入底部导航。");
            for (int i = 0; i < toastRoot.childCount; i++)
            {
                RectTransform toast = toastRoot.GetChild(i) as RectTransform;
                Assert.GreaterOrEqual(toast.anchoredPosition.y, 0f);
                Assert.LessOrEqual(toast.anchoredPosition.y + toast.sizeDelta.y, toastRoot.rect.height + .01f,
                    "Toast 堆叠不得越出裁剪容器。");
            }
        }

        [Test]
        public void UiManagerCreateIsIdempotentAndDestroyUnsubscribesBusinessEvents()
        {
            BusinessDirector director = BusinessDirector.Create();
            EnsureAwake(director);
            UIManager first = UIManager.Create();
            first.InitializeForValidation();
            UIManager second = UIManager.Create();
            Assert.AreSame(first, second, "重复 Create 不得生成第二套 Canvas/EventSystem。");

            InvokePrivate(first, "Start");
            FieldInfo liveField = typeof(BusinessDirector).GetField("OnLive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo finishedField = typeof(BusinessDirector).GetField("OnFinished",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue(ContainsTarget(liveField.GetValue(director) as Delegate, first));
            Assert.IsTrue(ContainsTarget(finishedField.GetValue(director) as Delegate, first));

            InvokePrivate(first, "OnDisable");

            Assert.IsFalse(ContainsTarget(liveField.GetValue(director) as Delegate, first),
                "UIManager 停用/销毁时必须退订 OnLive。");
            Assert.IsFalse(ContainsTarget(finishedField.GetValue(director) as Delegate, first),
                "UIManager 停用/销毁时必须退订 OnFinished。");
            UnityEngine.Object.DestroyImmediate(first.gameObject);
        }

        [Test]
        public void GovernmentEventMetricOffsetsSurviveDerivedMetricRecalculation()
        {
            float baseAveragePrice = state.government.averagePrice;
            SetGovernmentEvent("shortage");
            Assert.IsTrue(GovernmentSimulator.ResolveEvent(state, 2));
            DayRecord settlement = new DayRecord { price = 15f, profit = 100f };
            float consumerPrice = GovernmentSimulator.DrinkConsumerPrice(state, settlement.price);
            GovernmentSimulator.RecordDrinkSettlement(state, settlement, 1, 1);
            Assert.AreEqual(Mathf.Lerp(baseAveragePrice, consumerPrice, .35f) + 2f,
                state.government.averagePrice, .001f,
                "市场价格平滑更新后仍应保留短缺事件的持久价格修正。");

            ResetSimulationState();
            SetGovernmentEvent("layoff");
            Assert.IsTrue(GovernmentSimulator.ResolveEvent(state, 1));
            LaborMarketSnapshot labor = LaborMarketService.Evaluate(state, state.macro.outputGap, state.macro.pendingShock);
            GovernmentSimulator.AdvanceCycle(state);
            Assert.AreEqual(Mathf.Clamp(100f - labor.unemploymentRate - 4f, 0f, 100f),
                state.government.employmentRate, .001f,
                "裁员冲击不得在城市指标重算时消失。");
            Assert.AreEqual(79f, state.government.enterpriseConfidence, .001f,
                "减负选项的企业信心加成应叠加在公式基线之上。");
            MacroEconomySimulator.AdvancePeriod(state);
            Assert.AreEqual(Mathf.Clamp(100f - state.macro.unemploymentRate - 4f, 0f, 100f),
                state.government.employmentRate, .001f,
                "宏观同步就业率时不得覆盖裁员事件修正。");

            ResetSimulationState();
            SetGovernmentEvent("price_protest");
            Assert.IsTrue(GovernmentSimulator.ResolveEvent(state, 2));
            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            float expectedSupport = Mathf.Clamp(state.government.residentHappiness +
                record.welfareSpending / state.government.population - 5f, 0f, 100f);
            Assert.AreEqual(expectedSupport, state.government.residentSupport, .001f,
                "居民抗议的支持率后果不得在城市指标重算时消失。");

            string json = JsonUtility.ToJson(state.government);
            GovernmentState restored = JsonUtility.FromJson<GovernmentState>(json);
            Assert.AreEqual(-5f, restored.eventResidentSupportOffset, .001f,
                "事件修正必须随政府状态存档。");
        }

        [Test]
        public void SoldTypeAllocationNeverBorrowsCapacityFromAnotherConsumerType()
        {
            for (int studentDemand = 0; studentDemand <= 8; studentDemand++)
            for (int whitecollarDemand = 0; whitecollarDemand <= 8; whitecollarDemand++)
            for (int touristDemand = 0; touristDemand <= 8; touristDemand++)
            {
                DaySimulator.DemandRoll demand = new DaySimulator.DemandRoll
                {
                    students = studentDemand,
                    whitecollars = whitecollarDemand,
                    tourists = touristDemand
                };
                int total = demand.Total;
                for (int sold = 0; sold <= total + 2; sold++)
                {
                    DaySimulator.AllocateSoldByType(demand, sold,
                        out int students, out int whitecollars, out int tourists);
                    Assert.LessOrEqual(students, studentDemand);
                    Assert.LessOrEqual(whitecollars, whitecollarDemand,
                        "白领销量不得借用游客需求上限。");
                    Assert.LessOrEqual(tourists, touristDemand);
                    Assert.AreEqual(Mathf.Min(sold, total), students + whitecollars + tourists,
                        "各人群销量之和必须等于受总需求限制后的实际销量。");
                }
            }
        }

        [Test]
        public void RepeatedEnergyCrisesCannotRaiseImportPriceIndexBeyondDesignRange()
        {
            for (int i = 0; i < 8; i++)
            {
                MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);
                MacroEconomySimulator.AdvancePeriod(state);
                state.day++;
            }

            // 围猎三(#6):冲击指数每期向 100 温和回归(3%)后,连续危机下指数在「冲击推升 vs 回落」
            // 间平衡,不再恰好钉死上限——封顶护栏语义不变(≤220),回归则保证冲击不再永久驻留。
            Assert.LessOrEqual(state.macro.importPriceIndex, 220f + .001f,
                "进口价格指数应与其它价格指数一样封顶，不能无限传导进 CPI。");
            Assert.Greater(state.macro.importPriceIndex, 100f,
                "连续能源危机下指数仍应处于高位冲击区(回归是温和的,不抵消连续冲击)。");
        }

        [Test]
        public void DemandFeedbackStopsExtrapolatingBeyondTwentyFivePercentUnemployment()
        {
            state.macro.consumerConfidence = 68f;
            state.macro.unemploymentRate = 25f;
            float atBoundary = MacroEconomySimulator.DemandFeedback(state);
            state.macro.unemploymentRate = 80f;
            float beyondBoundary = MacroEconomySimulator.DemandFeedback(state);

            Assert.AreEqual(atBoundary, beyondBoundary, .001f,
                "失业率超过 25% 后就业因子应停在设计下限，而不是继续 Lerp 外插。");
        }

        [Test]
        public void FirstRunChoiceCannotBeDismissedWithoutAnswering()
        {
            // P0-1 回归:有存档时的「欢迎回来」面板必须被回答(Esc/E/×/CloseAll 都关不掉),
            // 否则会以"未读档的默认状态"进入游戏,推进一天即用假新局覆盖玩家真实存档。
            string tempDir = Path.Combine(Path.GetTempPath(), "EconTownFirstRunTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
            try
            {
                state.day = 7;
                state.money = 4321f;
                SaveSystem.Save(state);
                Assert.IsTrue(SaveSystem.HasSave(), "前置:合成存档已就位。");

                DeterministicBugTestUIManager ui = CreateTestUi();
                ui.ShowFirstRunChoice();
                Assert.IsTrue(ui.InputBlocked, "有存档时必须弹出「欢迎回来」面板。");

                ui.CloseAll();                              // Esc 全局脱困键路径
                ui.CloseTop();                              // E 关闭顶层路径
                Button close = FindButton(ui, "btn_×");     // 面板右上角 × 路径
                Assert.NotNull(close, "前置:面板自带关闭按钮。");
                close.onClick.Invoke();
                Assert.IsTrue(ui.InputBlocked,
                    "Esc/E/× 都不得关闭欢迎面板——未做选择就进入游戏会以固定默认种子运行并覆盖真实存档。");

                Button cont = FindButton(ui, "btn_继续经营");
                Assert.NotNull(cont, "前置:「继续经营」按钮存在。");
                cont.onClick.Invoke();
                Assert.IsFalse(ui.InputBlocked, "点「继续经营」必须解除强制面板并关闭它。");
                Assert.AreEqual(7, state.day, "点「继续经营」必须实际加载存档进度,而不是停留在默认第 1 天。");
            }
            finally
            {
                SaveSystem.OverrideSaveDirectory(null);
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch (Exception) { }
            }
        }

        static Button FindButton(UIManager ui, string name)
        {
            Button[] buttons = ui.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].name == name) return buttons[i];
            return null;
        }

        static void InvokeUiMethod(UIManager ui, string methodName, params object[] args)
        {
            typeof(UIManager).GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, args);
        }

        static GameObject FindDescendant(Transform root, string name)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                if (children[i].name == name) return children[i].gameObject;
            return null;
        }

        [Test]
        public void SettlingFromAllPerspectivesKeepsCriticalPanelsAlive()
        {
            // 政府/居民/宏观视角收日必须"先关自己、再结算"，
            // 否则旧面板残栈会让昨天的结算按钮复活，造成二次跳日。
            string tempDir = Path.Combine(Path.GetTempPath(), "EconTownSettleTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);    // TryNextDay 成功路径会写盘
            GameObject bootGo = new GameObject("TestBootstrap");
            GameBootstrap boot = bootGo.AddComponent<GameBootstrap>();
            InvokePrivate(boot, "Awake");                  // EditMode 不走 Awake,单例手动挂接(与 GameState 同一套约定)
            try
            {
                DeterministicBugTestUIManager ui = CreateTestUi();

                for (int perspective = 0; perspective < 3; perspective++)
                {
                    ResetSimulationState();
                    state.day = 15;
                    GameObject own = ui.OpenPanel("视角面板", 420f, 300f);
                    switch (perspective)
                    {
                        case 0: InvokeUiMethod(ui, "AdvanceGovernmentCycle", 0); break;
                        case 1: InvokeUiMethod(ui, "AdvanceResidentCycle"); break;
                        case 2: InvokeUiMethod(ui, "AdvanceMacroPeriod", 0); break;
                    }
                    Assert.AreEqual(16, state.day, "视角 " + perspective + ":统一结算应只推进一天。");
                    Assert.IsTrue(own == null,
                        "视角 " + perspective + ":结算前自己的旧面板必须已关闭——残栈会让昨天的结算按钮复活造成二次跳日。");
                    ui.CloseAll();
                }

                // V0.5.2 不再以第 30 天作为强制终局，跨过旧边界必须继续经营。
                {
                    ResetSimulationState();
                    state.day = state.totalDays;
                    ui.OpenPanel("视角面板", 420f, 300f);
                    InvokeUiMethod(ui, "AdvanceGovernmentCycle", 0);
                    Assert.AreEqual(31, state.day);
                    Assert.AreNotEqual(GamePhase.Ended, state.phase,
                        "V0.5.2 应在第 30 天后继续长期游玩。");
                    Assert.IsNull(FindDescendant(ui.transform, "panel_30 天经营结束 · 总评"));
                    ui.CloseAll();
                }
            }
            finally
            {
                SaveSystem.OverrideSaveDirectory(null);
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch (Exception) { }
                if (bootGo != null)
                {
                    InvokePrivate(boot, "OnDestroy");      // EditMode 不走 OnDestroy,手动清掉单例
                    UnityEngine.Object.DestroyImmediate(bootGo);
                }
            }
        }

        [Test]
        public void TextInputFocusedGuardsConfirmAndCancelWhileTyping()
        {
            // P1-3 回归:输入框聚焦时 E(拼音必含)与 Esc(退出输入法)必须放行给文本编辑,
            // 否则直读键盘的关面板逻辑会把整个面板连同已输入内容一起销毁。
            DeterministicBugTestUIManager ui = CreateTestUi();
            // EditMode 下 AddComponent 不触发 OnEnable,EventSystem.current 为 null:直接取实例注入。
            EventSystem es = ui.GetComponentInChildren<EventSystem>();
            Assert.IsNotNull(es, "前置:测试 UI 自带 EventSystem。");
            GameObject inputGo = new GameObject("nameInput");
            inputGo.AddComponent<InputField>();
            GameObject buttonGo = new GameObject("btn_plain");
            buttonGo.AddComponent<Button>();
            try
            {
                Assert.IsFalse(PlayerController.TextInputFocused(es), "无聚焦时不得拦截 E/Esc。");
                es.SetSelectedGameObject(buttonGo);
                Assert.IsFalse(PlayerController.TextInputFocused(es), "聚焦普通按钮时不得拦截 E/Esc。");
                es.SetSelectedGameObject(inputGo);
                Assert.IsTrue(PlayerController.TextInputFocused(es), "聚焦输入框时 E/Esc 必须放行给文本编辑。");
                es.SetSelectedGameObject(null);
                Assert.IsFalse(PlayerController.TextInputFocused(es), "取消聚焦后 E/Esc 恢复关面板语义。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputGo);
                UnityEngine.Object.DestroyImmediate(buttonGo);
            }
        }

        [Test]
        public void ContinueGameRestoresBuiltProjectsOntoMap()
        {
            // P1-4 回归:"继续经营"读档后,存档里已建成的公共设施必须刷回地图——
            // 启动时 MapBuilder.Build 用的是读档前的空状态,不补刷新老档玩家永远看不到设施。
            GameObject mapRoot = new GameObject("TestMapRoot");
            string tempDir = Path.Combine(Path.GetTempPath(), "EconTownMapTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
            try
            {
                MapBuilder.Build(mapRoot.transform);
                PublicProjectState lamp = state.government.projects.Find(
                    p => p.type == PublicProjectType.StreetLights);
                Assert.NotNull(lamp, "前置:Ensure 后项目列表包含路灯。");
                Assert.IsNotNull(FindDescendant(mapRoot.transform, "public_light_west"),
                    "V0.8.3:灯杆常驻(素头),与建成与否无关(PRD §176)。");
                Assert.IsNull(FindDescendant(mapRoot.transform, "head_glow"),
                    "前置:未建成时地图上没有路灯夜辉(路灯项目=夜晚亮灯)。");
                lamp.built = true;
                state.day = 9;
                SaveSystem.Save(state);

                DeterministicBugTestUIManager ui = CreateTestUi();
                ui.ShowFirstRunChoice();
                FindButton(ui, "btn_继续经营").onClick.Invoke();

                Assert.IsNotNull(FindDescendant(mapRoot.transform, "public_light_west"),
                    "读档成功后已建成的路灯必须刷回地图。");
                Assert.IsNotNull(FindDescendant(mapRoot.transform, "head_glow"),
                    "读档刷回后,建成的路灯应带夜辉。");
            }
            finally
            {
                SaveSystem.OverrideSaveDirectory(null);
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch (Exception) { }
                UnityEngine.Object.DestroyImmediate(mapRoot);
            }
        }

        [Test]
        public void PanelLifecycleClosesPrunedAndUnregisteredPanels()
        {
            // P2-1 回归:不可渲染面板要连同 GameObject 清理(不能留屏幕残留并解除强制拦截);
            // 未入栈面板的 ClosePanel 要降级为直接销毁(否则 × 按钮永远点不到任何效果)。
            DeterministicBugTestUIManager ui = CreateTestUi();
            GameObject panel = ui.OpenPanel("待清理", 420f, 300f);
            panel.SetActive(false);
            Assert.IsFalse(ui.InputBlocked, "唯一面板失活后不应再阻挡输入。");
            Assert.IsTrue(panel == null, "不可渲染面板必须连同 GameObject 一起销毁,不能留屏幕残留。");

            GameObject orphan = new GameObject("orphan_panel");
            ui.ClosePanel(orphan);
            Assert.IsTrue(orphan == null, "未入栈面板的 ClosePanel 必须直接销毁,否则永远关不掉。");
        }

        [Test]
        public void GovernmentEventSpendingIsLedgeredAndCanRunDeficit()
        {
            // P2-4 回归:事件现金支出必须记入资本支出科目;现金不足走赤字(债务),
            // 不再 Max(0) 截断——旧代码里治理效果全额生效但支出凭空缩水,账本也对不上。
            SetGovernmentEvent("pollution_accident");
            state.government.cash = 300f;
            float pendingBefore = state.government.pendingCapitalSpending;
            Assert.IsTrue(GovernmentSimulator.ResolveEvent(state, 0));

            Assert.AreEqual(pendingBefore + 1000f, state.government.pendingCapitalSpending, 0.01f,
                "治理支出必须记入资本支出挂账,由收日统一结算。");
            Assert.AreEqual(300f, state.government.cash, 0.01f,
                "事件回调不得直接动现金——记账与扣款都发生在收日结算,否则双计。");

            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            Assert.AreEqual(1000f, record.capitalSpending, 0.01f,
                "支出必须出现在周期账本的资本支出科目里。");
            Assert.Less(record.budgetBalance, 0f, "现金不足以覆盖支出时,预算应形成赤字而不是被吞掉。");
            Assert.Greater(state.government.debt, 0f, "赤字应转化为政府债务。");
        }

        [Test]
        public void GovernmentEventReportsSkippedPolicyChange()
        {
            // P2-5 回归:SetPolicy 分支静默失败(目标与现状冲突/相同)时,事件摘要必须写明
            // "政策未调整"——否则玩家以为政策生效了,实际无任何变化、无任何反馈。
            GovernmentSimulator.Ensure(state);
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkSubsidy, 5f);
            SetGovernmentEvent("shortage");
            Assert.IsTrue(GovernmentSimulator.ResolveEvent(state, 0));   // 补贴供给:目标 Max(2, 5)=5 与现状相同
            StringAssert.Contains("政策未调整", state.government.lastPolicySummary,
                "补贴已不低于目标值时,选择「补贴供给」必须告知玩家政策实际没有变化。");
        }

        [Test]
        public void ProjectBonusesSurviveCityMetricRecalculation()
        {
            // P2-3 回归:公共项目的一次性加成此前被 UpdateCityMetrics 的公式整体重算覆盖,
            // 学校的工资加成、项目的幸福/商业加成只活一周期。
            GovernmentSimulator.Ensure(state);
            state.government.cash = 20000f;
            Assert.IsTrue(GovernmentSimulator.BuildProject(state, PublicProjectType.School));
            Assert.AreEqual(1f, state.government.projectAverageWageOffset, 0.001f,
                "学校的工资加成应记入持久修正。");

            GovernmentSimulator.AdvanceCycle(state);   // 触发 UpdateCityMetrics 整体重算
            LaborMarketSnapshot labor = LaborMarketService.Evaluate(state, state.macro.outputGap, state.macro.pendingShock);
            Assert.AreEqual(labor.effectiveDailyWage / 8f + 1f, state.government.averageWage, 0.001f,
                "城市指标重算后学校的工资加成必须仍在(旧代码:+= 的值被公式覆盖)。");

            Assert.IsTrue(GovernmentSimulator.BuildProject(state, PublicProjectType.Park));
            Assert.Greater(state.government.projectHappinessOffset, 0f, "公园的幸福加成应记入持久修正。");
            Assert.Greater(state.government.projectEnterpriseConfidenceOffset, 0f, "公园的商业信心加成应记入持久修正。");
            string json = JsonUtility.ToJson(state.government);
            GovernmentState restored = JsonUtility.FromJson<GovernmentState>(json);
            Assert.AreEqual(state.government.projectHappinessOffset, restored.projectHappinessOffset, 0.001f,
                "项目持久修正必须随政府状态存档。");
            Assert.AreEqual(state.government.projectAverageWageOffset, restored.projectAverageWageOffset, 0.001f);
        }

        [Test]
        public void PanelsDoNotChangeStoryRole()
        {
            // 身份只跟篇章走。打开生活/市政/宏观/店铺不能把人从店主改成镇民。
            DeterministicBugTestUIManager ui = CreateTestUi();
            ProgressionService.Ensure(state);
            state.currentView = PlayerViewMode.Resident;
            ProgressionService.Ensure(state);
            Assert.AreEqual(PlayerViewMode.Enterprise, state.currentView, "第 1 章必须是店主，即使之前被写成镇民。");

            ui.ShowResidentHome();
            ui.ShowGovernmentDashboard(0);
            ui.ShowMacroDashboard(0);
            ui.ShowShop();
            Assert.AreEqual(PlayerViewMode.Enterprise, state.currentView, "面板不得改写身份。");

            state.progression.currentChapter = 17;
            state.progression.completedChapter = 16;
            ProgressionService.Ensure(state);
            Assert.AreEqual(PlayerViewMode.Government, state.currentView, "第 17 章身份是镇长。");
            ui.ShowShop();
            Assert.AreEqual(PlayerViewMode.Government, state.currentView, "回店里看经营，人仍然是镇长。");
        }

        [Test]
        public void KenneyArtCacheResetCreatesFreshSprites()
        {
            // P2-8 回归:禁用 Domain Reload 时静态纹理缓存必须在每次进 Play 时清理,
            // 否则旧的原生纹理/精灵逐次累积泄漏。
            Sprite first = KenneyArt.TileSprite(597);
            Assume.That(first, Is.Not.Null);   // StreamingAssets 素材缺失的环境跳过
            typeof(KenneyArt).GetMethod("ResetCache",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Sprite second = KenneyArt.TileSprite(597);
            Assert.IsFalse(ReferenceEquals(first, second),
                "重置后再次取图必须创建新精灵——缓存已被清空,而不是跨会话复用旧的原生资源。");
        }

        DeterministicBugTestUIManager CreateTestUi()
        {
            GameObject go = new GameObject("DeterministicBugTestUI");
            DeterministicBugTestUIManager ui = go.AddComponent<DeterministicBugTestUIManager>();
            ui.InitializeForValidation();
            return ui;
        }

        void ResetSimulationState()
        {
            state.economy = new EconomyState();
            state.competitor = new CompetitorState();
            state.resident = new ResidentState();
            state.government = new GovernmentState();
            state.macro = new MacroEconomyState();
            state.history.Clear();
            state.unlockedCards.Clear();
            state.phase = GamePhase.Morning;
            state.day = 1;
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            MacroEconomySimulator.Ensure(state);
        }

        void SetGovernmentEvent(string id)
        {
            state.government.currentEvent = new GovernmentEventState
            {
                id = id,
                title = id,
                description = "test",
                optionA = "A",
                optionB = "B",
                optionC = "C"
            };
        }

        static void EnsureAwake(BusinessDirector director)
        {
            if (BusinessDirector.I != director) InvokePrivate(director, "Awake");
        }

        static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }

        static bool ContainsTarget(Delegate handlers, object target)
        {
            if (handlers == null) return false;
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
                if (ReferenceEquals(invocationList[i].Target, target)) return true;
            return false;
        }

        static void DestroySceneComponents<T>() where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene.IsValid())
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }
    }
}
#endif
