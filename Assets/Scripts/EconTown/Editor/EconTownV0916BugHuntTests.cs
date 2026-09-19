#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.9.16 围猎五第一批(P1 五条)回归:店内重开/施工尘土漏进店/政府已结算窗口税费
    /// 双计/银行已结算窗口供款白退/围挡×喷泉碰撞断言空转——逐条钉死。(编译标记 v4:重开断言静态化)
    /// V0.9.16b 第二批(P2)回归:阶段报告档案滚动/央行会议冷却置灰。(编译标记 v5:using UI)</summary>
    public class EconTownV0916BugHuntTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;
        string tempDir;

        GameState NewWorld(string name)
        {
            GameObject go = new GameObject(name);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260904);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "围猎五镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            state = NewWorld("V0916World");
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV0916Tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        /// <summary>护栏助手:政府 history 末条应为给定周期(读档重放不删政府记录)。</summary>
        static void GovHistoryEndsWith(int cycle)
        {
            GovernmentState g = GameState.I.government;
            Assert.Greater(g.history.Count, 0, "护栏:政府周期记录存在。");
            Assert.AreEqual(cycle, g.history[g.history.Count - 1].cycle,
                "政府 history 末条应为本日已结算的周期。");
        }

        // ————— 围猎五#1:重开按主场景名,与 active scene 解耦 —————

        [Test]
        public void RestartScene_NameBased_NotActiveSceneBuildIndex()
        {
            // 旧实现 LoadScene(GetActiveScene().buildIndex) 在店内/港区静默失败(运行时场景
            // buildIndex 恒 -1)。EditMode 无法安全执行 LoadScene(会报错且可能重载测试场景),
            // RestartGame 的行为面没有自动化锁定——留 Play 冒烟人工覆盖(HANDOFF 冒烟清单:
            // 店内/港区点设置→重新开始,应删档并回到 Day 1);这里钉死可静态验证的两条:
            // ① 重开目标是 Build Settings 主场景名(与 active scene 解耦);② 该名字真实在
            // Build Settings 里可解析(LoadScene(名字) 路径成立的前提)。
            Assert.AreEqual("Main", GameBootstrap.RestartSceneName,
                "重开目标必须是 Build Settings 主场景名,与当前 active scene 解耦。");
            bool mainInBuildSettings = false;
            UnityEditor.EditorBuildSettingsScene[] scenes = UnityEditor.EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
                if (name == GameBootstrap.RestartSceneName) { mainInBuildSettings = true; break; }
            }
            Assert.IsTrue(mainInBuildSettings,
                "RestartSceneName 必须能在 Build Settings 解析到场景,否则 LoadScene(名字) 同样失败。");
        }

        // ————— 围猎五#2:店内早退清尘土(市民广场施工区与果汁店店内矩形同域重叠) —————

        [Test]
        public void GrowthDust_ClearedWhenInsideShopInterior()
        {
            GameObject mapObject = new GameObject("V0916DustMap");
            GameObject playerObject = new GameObject("V0916DustPlayer");
            GameObject cameraObject = new GameObject("V0916DustCamera");
            GameObject followObject = new GameObject("V0916DustFollow");
            TownGrowthDirector.Create();
            try
            {
                MapBuilder.Build(mapObject.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                CameraFollow follow = followObject.AddComponent<CameraFollow>();
                follow.target = playerObject.transform;
                follow.cam = camera;
                playerObject.transform.position = MapBuilder.ShopDoor + new Vector3(0f, -.8f, 0f);
                // EditMode 的 AddComponent 不跑 Awake(项目实测,V0911 同因)——I 手动设置。
                TownGrowthDirector director = Object.FindFirstObjectByType<TownGrowthDirector>();
                Assert.IsNotNull(director, "护栏:Director 已创建。");
                PropertyInfo iProp = typeof(TownGrowthDirector).GetProperty("I");
                iProp.SetValue(null, director);
                FieldInfo dustField = typeof(TownGrowthDirector).GetField("dust",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo spawnDust = typeof(TownGrowthDirector).GetMethod("SpawnDust",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo update = typeof(TownGrowthDirector).GetMethod("Update",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(dustField); Assert.IsNotNull(spawnDust); Assert.IsNotNull(update);

                PortSceneController.Create(playerObject.transform, follow, camera);
                ShopInteriorController.Create(playerObject.transform, follow, camera);
                spawnDust.Invoke(director, null);
                spawnDust.Invoke(director, null);
                var dustBefore = (System.Collections.IList)dustField.GetValue(director);
                Assert.AreEqual(2, dustBefore.Count, "护栏:进店前尘土在场。");

                ShopInteriorController.EnterInterior("shop");
                Assert.IsTrue(ShopInteriorController.IsInsideShopInterior, "护栏:已进店。");
                update.Invoke(director, null);
                var dustAfter = (System.Collections.IList)dustField.GetValue(director);
                Assert.AreEqual(0, dustAfter.Count,
                    "店内 Update 必须清光在场尘土——旧代码不早退,施工尘土漏进店内(order 930 盖过全部家具)。");
                // 注:尘土 GameObject 用 Destroy()(Play 正确语义),EditMode 反射调 Update 无帧循环
                // 不会即时销毁——对象回收断言不做,列表清空即行为口径。
            }
            finally
            {
                if (TownGrowthDirector.I != null) Object.DestroyImmediate(TownGrowthDirector.I.gameObject);
                MapBuilder.SetActiveInteriorKind("shop");   // kind 残留会污染后续用例(V0911 复审同因)
                if (PortSceneController.I != null) Object.DestroyImmediate(PortSceneController.I.gameObject);
                if (ShopInteriorController.I != null) Object.DestroyImmediate(ShopInteriorController.I.gameObject);
                Object.DestroyImmediate(followObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 围猎五#3:政府已结算窗口读档,重放日税费不再双计 —————

        [Test]
        public void SaveLoad_AfterGovernmentSettled_ReplayTaxNotDoubleCounted()
        {
            // 建立前一日记录(基线),再进入 day2 的「政府已结算」窗口:挂账→结算→存档。
            state.planProduction = 100;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked1),
                "护栏:day1 收日不被拦截。blocked=" + blocked1);
            int settleDay = state.day;

            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 2f);
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            Assert.IsNotNull(state.LastRecord, "护栏:当日企业记录已建立。");
            // 挂账护栏由 Settle 内部的 RecordDrinkSettlement 按实际销量满足(复审:手动再调
            // 会被同日防重拦住,是死操作——删掉,防「护栏与构造脱钩」)。
            Assert.Greater(state.government.pendingDrinkTaxRevenue, 0f, "护栏:饮品税已挂账(Settle 内部按实际销量)。");

            GovernmentSimulator.AdvanceCycle(state);   // 窗口内:政府已结算(cycle 记录入史、挂账清零)
            float cashSettled = state.government.cash;
            Assert.AreEqual(0f, state.government.pendingDrinkTaxRevenue, "护栏:结算已消费挂账。");
            GovHistoryEndsWith(settleDay);

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state), "护栏:读档成功。");
            GovHistoryEndsWith(settleDay);   // 读档后政府侧保持已结算

            // 核心:防重标记不被复位——重放日再卖同量杯子,挂账被同日防重拦住(不再双计)。
            Assert.AreEqual(settleDay, state.government.lastDrinkTaxRecordedDay,
                "已结算窗口读档必须冻结防重标记(旧代码复位为 0,重放日再挂账→次日周期双计)。");
            DayRecord replayRecord = new DayRecord { day = settleDay, price = 5f };
            GovernmentSimulator.RecordDrinkSettlement(state, replayRecord, 150, 100);
            Assert.AreEqual(0f, state.government.pendingDrinkTaxRevenue,
                "重放日的税被防重标记拦住,不再挂账(政府已按原时间线收过当日税;展示字段照写是#3复审的口径,此处钉挂账腿)。");
            // 复审:展示字段必须照写——防重若退回「拦在全部赋值之前」的旧形态,重放日日报
            // 政策列全 0,而挂账断言照样绿(零回归覆盖)。
            Assert.AreEqual(2f, replayRecord.salesTaxPerUnit,
                "重放日新记录的税率字段必须照写(防重只拦挂账,不拦展示)。");
            Assert.Greater(replayRecord.consumerPrice, 0f, "消费者价格字段照写。");
            Assert.AreEqual(cashSettled, state.government.cash, .01f, "政府现金不受读档重放影响。");
        }

        // ————— 围猎五#4:银行已结算窗口读档,企业供款现金腿不再被基线回滚白退 —————

        [Test]
        public void SaveLoad_AfterBankSettled_CompanyLoanPaymentNotRefunded()
        {
            // 手动构造企业贷(照 V0915b 先例,绕开审批),放款腿按信用创造镜像
            // (贷款资产+存款负债同增,否则全量收日的 BalanceGap 恒等校验报 gap=1000);
            // 随后存款转经营现金 → 供款全额走现金腿。
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Company,
                borrowerId = "player.company",
                purpose = LoanPurpose.WorkingCapital,
                status = LoanStatus.Current,
                principal = 1000f,
                remainingPrincipal = 1000f,
                annualInterestRate = 6f,
                term = 24,
                remainingTerm = 24,
                originatedWorldDay = state.day,
            });
            state.bank.companyDeposits += 1000f;
            Assert.IsTrue(CreditService.TransferCompanyDepositToCash(state, 1000f, out string transferMsg),
                "护栏:存款转经营现金。" + transferMsg);
            Assert.AreEqual(0f, state.bank.companyDeposits, .01f, "护栏:后续供款全部走现金腿。");

            state.planProduction = 100;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked1),
                "护栏:day1 收日(含首期供款)不被拦截。blocked=" + blocked1);

            // day2 窗口:企业记录已建、银行已结算(供款已扣)——章节完成的 SaveKeyMoment 同款状态。
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            FinancialSystemService.SettleContracts(state);
            Assert.AreEqual(state.day, state.bank.lastContractSettlementDay, "护栏:银行已结算当日。");
            Assert.Greater(state.bank.todayCompanyLoanPaymentFromCash, 0f,
                "护栏:本期供款有现金垫付腿(companyDeposits=0)。");
            // 读档重放的口径(围猎三#9):营业流退回重放是故意的;只有「不随重放重跑的腿」
            // (银行已结算的供款)要补扣——期望值=回滚基线 prevDay.moneyEnd − 供款现金腿。
            float baseline = state.history[state.history.Count - 2].moneyEnd;
            float fromCashLeg = state.bank.todayCompanyLoanPaymentFromCash;

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state), "护栏:读档成功。");
            Assert.AreEqual(baseline - fromCashLeg, state.money, .01f,
                "已结算窗口读档,供款的经营现金腿必须被补扣(旧代码回到 prevDay.moneyEnd=多出整期供款,M0 增发)。");
            Assert.AreEqual(fromCashLeg, state.bank.todayCompanyLoanPaymentFromCash, .01f,
                "当日流量字段随存档往返保真。");
        }

        [Test]
        public void SaveLoad_BankNotSettledToday_NoDeduction()
        {
            // 复审落账:补扣的窗口条件(false 分支)对照用例——「当日企业记录已写而银行未结算
            // 当日」的档(收日被事件阻塞在合约结算前/将来新增中途存档点),腿字段残留昨日整期
            // 供款,窗口条件被误删时读档会错扣一期供款现金。腿>0 由企业贷构造(同上用例)。
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Company,
                borrowerId = "player.company",
                purpose = LoanPurpose.WorkingCapital,
                status = LoanStatus.Current,
                principal = 1000f,
                remainingPrincipal = 1000f,
                annualInterestRate = 6f,
                term = 24,
                remainingTerm = 24,
                originatedWorldDay = state.day,
            });
            state.bank.companyDeposits += 1000f;
            Assert.IsTrue(CreditService.TransferCompanyDepositToCash(state, 1000f, out string transferMsg),
                "护栏:存款转经营现金。" + transferMsg);

            state.planProduction = 100;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked1),
                "护栏:day1 收日(含首期供款,腿字段残留)不被拦截。blocked=" + blocked1);
            Assert.Greater(state.bank.todayCompanyLoanPaymentFromCash, 0f,
                "护栏:腿字段残留昨日整期供款(隔日未清)。");

            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            // 不跑 SettleContracts——银行未结算当日(lastSettle=昨日),腿字段残留昨日供款。
            Assert.AreNotEqual(state.day, state.bank.lastContractSettlementDay, "护栏:银行未结算当日。");
            float baseline = state.history[state.history.Count - 2].moneyEnd;

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state), "护栏:读档成功。");
            Assert.AreEqual(baseline, state.money, .01f,
                "银行未结算当日时不得补扣——腿字段是昨日整期供款的残留,窗口条件是防双扣的活护栏。");
        }

        // ————— 围猎五#4 复审:居民供款现金腿补扣同族覆盖(todayResidentLoanPaymentFromCash) —————

        [Test]
        public void SaveLoad_AfterBankSettled_ResidentLoanPaymentAlsoDeducted()
        {
            // 居民贷放款镜像(贷款+存款同增保 BalanceGap),再用「付货款给企业」把存款池清空
            // (负债内挪 household→company),此后供款全额走居民现金垫付腿。
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                purpose = LoanPurpose.Consumption,
                status = LoanStatus.Current,
                principal = 500f,
                remainingPrincipal = 500f,
                annualInterestRate = 6f,
                term = 24,
                remainingTerm = 24,
                originatedWorldDay = state.day,
            });
            state.bank.householdDeposits += 500f;
            state.resident.savings += 500f;
            state.bank.householdDeposits -= 500f;      // 付货款给企业(负债内挪,平)
            state.bank.companyDeposits += 500f;
            state.resident.savings -= 500f;

            state.planProduction = 100;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked1),
                "护栏:day1 收日(含首期供款)不被拦截。blocked=" + blocked1);

            // day2 窗口:居民清晨记录已由 BeginNextDay 建立(residentReplay 前提),企业记录
            // 与银行结算随后完成——章节完成的 SaveKeyMoment 同款状态。
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            // Saver 偏好日结会自动存款抬池——结算前把存款池再挪空(负债内挪,保 BalanceGap),
            // 迫使本期供款全额走居民现金垫付腿。
            float pool = state.bank.householdDeposits;
            state.bank.householdDeposits -= pool;
            state.bank.companyDeposits += pool;
            state.resident.savings = Mathf.Max(0f, state.resident.savings - pool);
            FinancialSystemService.SettleContracts(state);
            // 真实窗口里 EndDay(写当日居民记录)在合约结算之后、章节存档之前——不跑它则
            // residentReplay=false,居民侧回滚与补扣整段不进(编辑器实证)。
            ResidentEconomy.EndDay(state);
            Assert.AreEqual(state.day, state.bank.lastContractSettlementDay, "护栏:银行已结算当日。");
            Assert.Greater(state.bank.todayResidentLoanPaymentFromCash, 0f,
                "护栏:本期居民供款有现金垫付腿(householdDeposits=0)。");
            float residentCashStart = state.resident.history[state.resident.history.Count - 1].cashStart;
            float fromCashLeg = state.bank.todayResidentLoanPaymentFromCash;

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state), "护栏:读档成功。");
            Assert.AreEqual(residentCashStart - fromCashLeg, state.resident.cash, .01f,
                "居民供款的现金垫付腿必须被补扣(旧代码 cash 回滚到 cashStart=垫付白退;该腿此前零测试覆盖——复审落账)。");
        }

        // ————— 围猎五#7(V0.9.16b):阶段报告档案 8 条可滚动,第八篇报告可回看 —————

        [Test]
        public void StageReportArchive_EightEntriesScrollable()
        {
            // 八篇主线恒 8 条档案(幂等只增),固定 98px 行距 > 面板可用高——必须走滚动容器。
            for (int i = 0; i < 8; i++)
                state.progression.stageReports.Add(new StageReportRecord
                {
                    part = (LearningPart)(i + 1),
                    completedWorldDay = 10 + i,
                    resultSummary = "测试摘要 " + i,
                });
            WithFreshUi(ui =>
            {
                ui.ShowStageReportArchive();
                Canvas.ForceUpdateCanvases();
                // 复审:必须锚定到档案滚动容器(按名字),全局 FindFirstObjectByType 会在
                // 场景出现第二个 ScrollRect 时拿错对象(断言全部寄生在取到的对象上)。
                ScrollRect scroll = null;
                ScrollRect[] all = ui.GetComponentsInChildren<ScrollRect>(true);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].gameObject.name == "stageArchiveScroll") { scroll = all[i]; break; }
                Assert.IsNotNull(scroll, "档案列表必须有滚动容器(8 条×98px 溢出面板 content 可用高 592)。");
                RectTransform content = scroll.content;
                Assert.IsNotNull(content);
                Assert.GreaterOrEqual(content.childCount, 8, "八条报告按钮都挂滚动内容之下。");
                // 几何腿(V0.9.15c 复审先例):内容高必须大于视口高,否则第 8 条够不着。
                Assert.Greater(content.rect.height, scroll.viewport.rect.height,
                    "内容高(" + content.rect.height + ")必须大于视口高(" + scroll.viewport.rect.height + "),否则第八篇报告无法滚动到。");
                Transform eighth = content.GetChild(7);
                Assert.IsTrue(eighth.GetComponent<UnityEngine.UI.Button>() != null, "第 8 条是可点击的报告按钮。");
            });
        }

        // ————— 围猎五#8(V0.9.16b):央行利率按钮在会议冷却期内置灰 —————

        [Test]
        public void CentralBankRateButtons_DisabledDuringMeetingCooldown()
        {
            state.progression.completedChapter = 37;   // 金融中心 30 章开页、央行 37 章解锁
            state.centralBank.unlocked = true;
            state.centralBank.lastPolicyDecisionDay = state.day - 7;   // until=0,无冷却(护栏前提)
            WithFreshUi(ui =>
            {
                ui.ShowFinanceCenter(3);   // tab 3 = 中央银行
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.Button cut = ButtonByText(ui, "降息 0.25"),
                    hold = ButtonByText(ui, "保持"), raise = ButtonByText(ui, "加息 0.25");
                Assert.IsNotNull(cut); Assert.IsNotNull(hold); Assert.IsNotNull(raise);
                Assert.IsTrue(cut.interactable && hold.interactable && raise.interactable,
                    "无冷却时应可点(护栏)。");
            });
            // 做出决定当天:7 天冷却内三按钮必须置灰(与 TrySetPolicyRate 守卫同口径)。
            state.centralBank.lastPolicyDecisionDay = state.day;
            WithFreshUi(ui =>
            {
                ui.ShowFinanceCenter(3);
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.Button cut = ButtonByText(ui, "降息 0.25"),
                    hold = ButtonByText(ui, "保持"), raise = ButtonByText(ui, "加息 0.25");
                Assert.IsNotNull(cut); Assert.IsNotNull(hold); Assert.IsNotNull(raise);
                Assert.IsFalse(cut.interactable || hold.interactable || raise.interactable,
                    "会议冷却期内三按钮必须置灰——旧代码可按但核心恒拒绝,「可按 vs 弹 Toast 拒绝」自相矛盾(bug搜#5 同族)。");
            });
        }

        static UnityEngine.UI.Button ButtonByText(UIManager ui, string label)
        {
            UnityEngine.UI.Button[] buttons = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                UnityEngine.UI.Text t = buttons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                if (t != null && t.text == label) return buttons[i];
            }
            return null;
        }

        static void WithFreshUi(System.Action<UIManager> body)
        {
            UIManager[] stale = Resources.FindObjectsOfTypeAll<UIManager>();
            for (int i = 0; i < stale.Length; i++)
                if (stale[i] != null && stale[i].gameObject.scene.IsValid())
                    Object.DestroyImmediate(stale[i].gameObject);
            UIManager ui = UIManager.Create();
            try { ui.InitializeForValidation(); body(ui); }
            finally { Object.DestroyImmediate(ui.gameObject); }
        }

        // ————— 围猎五#5:碰撞相交 helper 在真实重叠时必须报 true(断言不再空转) —————

        [Test]
        public void BoxesIntersect_DetectsRealOverlap()
        {
            MethodInfo boxesIntersect = typeof(EconTownTownLayoutTests).GetMethod("BoxesIntersect",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(boxesIntersect, "护栏:helper 存在。");
            GameObject a = new GameObject("V0916BoxA"), b = new GameObject("V0916BoxB");
            try
            {
                BoxCollider2D ca = a.AddComponent<BoxCollider2D>();
                BoxCollider2D cb = b.AddComponent<BoxCollider2D>();
                a.transform.position = Vector3.zero;
                b.transform.position = new Vector3(.3f, 0f, 0f);   // 同位重叠
                Assert.IsTrue((bool)boxesIntersect.Invoke(null, new object[] { ca, cb }),
                    "真实重叠必须报相交——修复前 bounds.Intersects 在 EditMode 退化零尺寸盒,恒 false(断言空转)。");
                b.transform.position = new Vector3(5f, 0f, 0f);    // 错开
                Assert.IsFalse((bool)boxesIntersect.Invoke(null, new object[] { ca, cb }), "分离时不得误报。");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
#endif
