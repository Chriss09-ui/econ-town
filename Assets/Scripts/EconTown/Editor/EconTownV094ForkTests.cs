#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>暴露 UIManager 给 EditMode 冒烟(V093UiHarness 同款手法,另起名避重)。</summary>
    public class V094UiHarness : UIManager { }

    /// <summary>V0.9.4《分叉的实验室》(PRD §197):最小世界分叉引擎——快照 JSON 往返克隆、
    /// 单例时序、四组静态量快照恢复、写抑制四入口、blocked 起点拒绝、两个住房实验室。
    /// 本文件第①步先落引擎骨架六例(#1/2/3/8/9/10);#4/5 随写抑制步、#6/7 随实验室步补齐。</summary>
    public class EconTownV094ForkTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V094State");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260830);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            state.government.cash = 100000f;
            state.money = 100000f;
            state.materials = 4000;
            ResidentEconomy.CreateProfile(state, "分叉镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            FinancialSystemService.Ensure(state);
            HousingService.Ensure(state);
            LongTermExperienceService.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
            // 防御:任何断言在 fork 闭包内抛出时 finally 已恢复,这里再兜底一次目录覆写。
            SaveSystem.OverrideSaveDirectory(null);
        }

        // ———————————————— #1 主世界零扰动 ————————————————

        [Test]
        public void Capture_MainWorldJson_ByteIdentical_AfterForkRun()
        {
            string before = WorldForkService.CaptureJson(state);
            var result = WorldForkService.RunClonedWorld(before, 6, null, null, null);
            Assert.IsTrue(result.completed, "fork 应跑满 6 期: " + result.blockedReason);
            Assert.AreEqual(before, WorldForkService.CaptureJson(state),
                "fork 跑 6 期后主世界快照必须逐字节不变(引用共享/静态污染都会在这里现形)。");
        }

        // ———————————————— #2 单例时序 ————————————————

        [Test]
        public void Fork_AwakeOrder_SingletonRestored_AfterDestroy()
        {
            string json = WorldForkService.CaptureJson(state);
            GameState seenInside = null;
            var result = WorldForkService.RunClonedWorld(json, 3, null, null, fork =>
            {
                seenInside = fork;
                Assert.IsTrue(ReferenceEquals(GameState.I, fork),
                    "fork 运行期 I 必须指向 fork(Awake 置位,主世界暂存)。");
            });
            Assert.IsTrue(result.completed, result.blockedReason);
            Assert.IsNotNull(seenInside, "onFinal 应收到 fork 实例。");
            Assert.IsTrue(ReferenceEquals(GameState.I, state),
                "fork 销毁后 I 必须恢复主世界(暂存→finally 显式回写)。");
        }

        // ———————————————— #3 静态量快照/恢复(R10 四组) ————————————————

        [Test]
        public void Fork_StaticSnapshot_LastEvent_Streams_SeedCalls_Directory()
        {
            // 先让 LastEvent 落一个真实事件引用(跑一天主世界),快照/恢复才有的比。
            state.phase = GamePhase.Morning;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out int _, out string blocked),
                blocked);

            DaySimulator.ForkStatics before = DaySimulator.BeginForkRun(); // 同一原语=读当前静态量
            string pathBefore = SaveSystem.SavePath;
            string json = WorldForkService.CaptureJson(state);

            var result = WorldForkService.RunClonedWorld(json, 4, null, null, fork =>
            {
                // fork 途中污染四组静态量(模拟分叉内副作用):EndForkRun 必须全部复原。
                DaySimulator.LastEvent = null;
                DaySimulator.SeedNewGame(fork); // newGameSeedCalls++
                SaveSystem.OverrideSaveDirectory("/tmp/v094_corrupt_dir_should_not_survive");
            });
            Assert.IsTrue(result.completed, result.blockedReason);

            DaySimulator.ForkStatics after = DaySimulator.BeginForkRun();
            Assert.IsTrue(ReferenceEquals(before.lastEvent, after.lastEvent),
                "LastEvent 应恢复为 fork 前引用(当前=" + (after.lastEvent == null ? "null" : after.lastEvent.name) + ")。");
            Assert.AreEqual(before.newGameSeedCalls, after.newGameSeedCalls,
                "newGameSeedCalls 应恢复(fork 内 SeedNewGame 的自增不得外溢)。");
            Assert.AreEqual(before.fallbackSeed, after.fallbackSeed, "fallbackRandom.seed 不外溢。");
            Assert.AreEqual(before.fallbackState, after.fallbackState, "fallbackRandom.state 不外溢。");
            Assert.AreEqual(before.fallbackCalls, after.fallbackCalls, "fallbackRandom.calls 不外溢。");
            Assert.AreEqual(pathBefore, SaveSystem.SavePath,
                "directoryOverride 应恢复为 fork 前取值。");
        }

        // ———————————————— #8 引用切断 ————————————————

        [Test]
        public void Fork_HistoryList_NotShared_AfterJsonClone()
        {
            int historyBefore = state.history.Count;
            string json = WorldForkService.CaptureJson(state);
            var result = WorldForkService.RunClonedWorld(json, 4, null, null, fork =>
            {
                Assert.IsFalse(ReferenceEquals(fork.history, state.history),
                    "JSON 往返克隆必须切断 history List 引用共享。");
                Assert.IsFalse(ReferenceEquals(fork.unlockedCards, state.unlockedCards),
                    "unlockedCards(HashSet 重建)同样不得共享。");
                fork.history.Add(new DayRecord { day = 9999 }); // 深度验证:改 fork 不动主世界
            });
            Assert.IsTrue(result.completed, result.blockedReason);
            Assert.AreEqual(historyBefore, state.history.Count,
                "fork 内对 history 的追加不得外溢到主世界。");
        }

        // ———————————————— #9 确定性 ————————————————

        class ReplayReadings
        {
            public int worldDay, historyCount;
            public float money, savings, price, housePrice, bubblePressure, nominalGdp, totalLoanLoss;
        }

        static ReplayReadings TakeReadings(GameState f)
        {
            return new ReplayReadings
            {
                worldDay = f.economy.worldDay,
                historyCount = f.history.Count,
                money = f.money,
                savings = f.resident.savings,
                price = f.price,
                housePrice = f.housing.townHousePrice,
                bubblePressure = f.housing.bubblePressure,
                nominalGdp = f.macro.nominalGdp,
                totalLoanLoss = f.bank.totalLoanLoss
            };
        }

        [Test]
        public void Fork_Determinism_SameSnapshotTwice_IdenticalOutcome()
        {
            string json = WorldForkService.CaptureJson(state);
            ReplayReadings a = null, b = null;
            var runA = WorldForkService.RunClonedWorld(json, 12, null, null, f => a = TakeReadings(f));
            Assert.IsTrue(runA.completed, runA.blockedReason);
            var runB = WorldForkService.RunClonedWorld(json, 12, null, null, f => b = TakeReadings(f));
            Assert.IsTrue(runB.completed, runB.blockedReason);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(a.worldDay, b.worldDay, "worldDay 逐字段一致");
            Assert.AreEqual(a.historyCount, b.historyCount, "history 数一致");
            Assert.AreEqual(a.money, b.money, "money 一致");
            Assert.AreEqual(a.savings, b.savings, "savings 一致");
            Assert.AreEqual(a.price, b.price, "price 一致");
            Assert.AreEqual(a.housePrice, b.housePrice, "房价一致");
            Assert.AreEqual(a.bubblePressure, b.bubblePressure, "泡沫压力一致");
            Assert.AreEqual(a.nominalGdp, b.nominalGdp, "名义 GDP 一致");
            Assert.AreEqual(a.totalLoanLoss, b.totalLoanLoss, "累计坏账一致");
        }

        // ———————————————— #4 写抑制四入口 ————————————————

        [Test]
        public void Fork_WriteSuppression_FourEntries_NoDiskWrite()
        {
            string tempDir = "/tmp/v094_fork_write_suppression";
            if (System.IO.Directory.Exists(tempDir))
                foreach (string f in System.IO.Directory.GetFiles(tempDir)) System.IO.File.Delete(f);
            else System.IO.Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);

            string mainBefore = WorldForkService.CaptureJson(state);
            string json = WorldForkService.CaptureJson(state);

            var result = WorldForkService.RunClonedWorld(json, 4, null, null, fork =>
            {
                // 抑制期内四写入口直调:全部必须早退落空(R4)
                SaveSystem.Save(fork);
                SaveSystem.SaveKeyMoment(fork, "fork 内关键节点(必须被抑制)");
                SaveSystem.SaveCheckpoint(fork, "fork 内 checkpoint(必须被抑制)");
                SaveSystem.Delete();
            });
            Assert.IsTrue(result.completed, result.blockedReason);
            Assert.AreEqual(0, System.IO.Directory.GetFiles(tempDir).Length,
                "抑制期四写入口零落盘。");
            Assert.AreEqual(mainBefore, WorldForkService.CaptureJson(state),
                "fork 期主世界快照仍逐字节不变。");

            // 解闸后写能力恢复(闸没有把 SaveSystem 弄坏)
            SaveSystem.Save(state);
            Assert.AreEqual(1, System.IO.Directory.GetFiles(tempDir).Length,
                "解闸后 Save 恢复写能力。");
            SaveSystem.OverrideSaveDirectory(null);
        }

        // ———————————————— #5 blocked 起点拒绝 ————————————————

        [Test]
        public void Fork_BlockedStart_Refused_AllGuards()
        {
            string json = WorldForkService.CaptureJson(state);

            // 1. null 源
            string reason;
            Assert.IsFalse(WorldForkService.CanFork(null, out reason), "null 源拒绝。");
            Assert.IsNotEmpty(reason);

            // 2. 终局
            state.phase = GamePhase.Ended;
            AssertRefused("本局已结束", json);
            state.phase = GamePhase.Morning;

            // 3. 营业中
            state.phase = GamePhase.Business;
            AssertRefused("营业进行中", json);
            state.phase = GamePhase.Morning;

            // 4. 政府事件未决
            state.government.currentEvent = new GovernmentEventState();
            AssertRefused("先处理当前城市事件", json);
            state.government.currentEvent = null;

            // 5. 价格战抉择未做(第 9 章+竞争已开+无策略选择+无已解决信号)
            state.progression.currentChapter = 9;
            state.economy.market.sunnyTea.isOpen = true;
            state.todayStrategyChoice = null;
            AssertRefused("价格战选择", json);
            state.progression.currentChapter = 1;
            state.economy.market.sunnyTea.isOpen = false;

            // 6. 财务危机未处置
            state.economy.financialCrisisActive = true;
            AssertRefused("财务危机", json);
            state.economy.financialCrisisActive = false;
        }

        void AssertRefused(string reasonFragment, string json)
        {
            string reason;
            Assert.IsFalse(WorldForkService.CanFork(state, out reason), "CanFork 应拒绝(" + reasonFragment + ")。");
            StringAssert.Contains(reasonFragment, reason);
            ForkRunResult r = WorldForkService.RunClonedWorld(state, 3, null, null, null);
            Assert.IsFalse(r.completed, "拒绝态 RunClonedWorld 不得跑。");
            Assert.AreEqual(reason, r.blockedReason, "同因返回。");
            Assert.AreEqual(0, r.periodsRun);
            Assert.IsNull(GameObject.Find("WorldFork"), "拒绝时不得创建任何 GameObject。");
            Assert.IsTrue(ReferenceEquals(GameState.I, state), "拒绝时 I 不动。");
        }

        // ———————————————— #6/#7 两个实验室 ————————————————

        /// <summary>实验室标准买房世界(V092 RunPinnedBoughtWorld 直译,默认利率):
        /// 储蓄垫足 40000(差额镜像准备金)、dailyIncome 600/todayIncome 800 过授信 DSR。</summary>
        void BuyHouseForLab()
        {
            state.resident.savings = 40000f;
            state.bank.reserves += 40000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 40000f;
            state.resident.dailyIncome = 600f;
            state.resident.todayIncome = 800f;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(state, out msg), msg);
        }

        void SeedPriceHistory(float fromIndex, float toIndex, int count)
        {
            state.housing.history.Clear();
            for (int i = 0; i < count; i++)
                state.housing.history.Add(new HousingPeriodRecord
                {
                    period = i,
                    priceIndex = Mathf.Lerp(fromIndex, toIndex, count == 1 ? 0f : i / (float)(count - 1))
                });
        }

        [Test]
        public void Fork_MortgageStress_A_vs_B_52Periods()
        {
            BuyHouseForLab();
            var r = HousingLabService.RunMortgageStress(state, 2f, 52);
            Assert.IsNull(r.refusal, r.refusal);
            Assert.IsTrue(r.runA.completed, "A 应跑满: " + r.runA.blockedReason);
            Assert.IsTrue(r.runB.completed, "B 应跑满: " + r.runB.blockedReason);
            Assert.AreEqual(52, r.runA.periodsRun);
            Assert.AreEqual(52, r.runB.periodsRun);
            Assert.IsNotNull(r.start);
            Assert.IsNotNull(r.outcomeA);
            Assert.IsNotNull(r.outcomeB);
            Assert.Greater(r.outcomeA.dsr, 0.05f, "A 期末房贷存续(防断供归零陷阱)。dsrA=" + r.outcomeA.dsr);
            Assert.Greater(r.outcomeB.dsr, 0.05f, "B 期末房贷存续。dsrB=" + r.outcomeB.dsr);
            // R25 as-built:利率→房价方向在世界间不稳定——玩家收入口径 <400/日时 P 钳在 0.7 下限
            // (R21 反「单一玩家收入主导镇价」的既定设计),教科书通道关闭,A/B 房价差由二阶环决定
            // (本世界政府净储蓄,加息经政府利息收入反而刺激需求→B 房价更高)。实验室诚实并排读数,
            // 只断言结构性稳定的通道:加息→存款利率↑→储蓄 B≥A(非严格,余额为正期间利息单调多赚)。
            Assert.GreaterOrEqual(r.outcomeB.savings, r.outcomeA.savings - 1e-4f,
                "存款利息通道:储蓄 B≥A。saveA=" + r.outcomeA.savings + " saveB=" + r.outcomeB.savings);
            Assert.IsFalse(Mathf.Abs(r.outcomeA.priceIndex - r.outcomeB.priceIndex) < 0.01f,
                "分叉有效性:两世界房价路径确实走向不同。idxA=" + r.outcomeA.priceIndex +
                " idxB=" + r.outcomeB.priceIndex);

            // —— UI 冒烟(步④):「住房实验室」页签可见,既有六页签不破
            GameObject uiGo = new GameObject("V094Ui");
            V094UiHarness ui = uiGo.AddComponent<V094UiHarness>();
            ui.InitializeForValidation();
            try
            {
                ui.ShowCurves(6);
                Transform labPanel = FindIn(ui.transform, "panel_经济实验室 · 用你自己的数据画曲线");
                Assert.IsNotNull(labPanel, "实验室面板已打开。");
                for (int i = 0; i <= 6; i++)
                    Assert.IsNotNull(FindIn(labPanel, "curveTab_" + i), "curveTab_" + i + " 存在。");
                Assert.IsTrue(AnyTextContains(labPanel, "住房实验室"), "第 7 页签文案。");
                Assert.IsTrue(AnyTextContains(labPanel, "房贷压力测试"), "压力测试卡片渲染。");
                Assert.IsTrue(AnyTextContains(labPanel, "泡沫形成实验"), "泡沫实验卡片渲染。");
                Assert.IsTrue(AnyTextContains(labPanel, "不修改主世界"), "实验模式横幅渲染。");
            }
            finally { Object.DestroyImmediate(uiGo); }
        }

        static Transform FindIn(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindIn(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        static bool AnyTextContains(Transform root, string needle)
        {
            UnityEngine.UI.Text t = root.GetComponent<UnityEngine.UI.Text>();
            if (t != null && t.text != null && t.text.Contains(needle)) return true;
            for (int i = 0; i < root.childCount; i++)
                if (AnyTextContains(root.GetChild(i), needle)) return true;
            return false;
        }

        [Test]
        public void Fork_BubbleExperiment_LowRate26Periods_BubbleRises()
        {
            // 世界:先以基准价买房,再把镇价推到 130%(动能在史:100→130×27),租金 120 显著滞后
            // (泡沫期常态:租金跟不上房价),人口 600 拉满需求失衡(价格步 +3%/期钳满格)。
            BuyHouseForLab();
            state.housing.townHousePrice = HousingService.BaseHousePrice * 1.3f;
            state.housing.periodRent = 120f;
            SeedPriceHistory(100f, 130f, 27);
            state.government.population = 600;

            var r = HousingLabService.RunBubbleExperiment(state, 1f, 26);
            Assert.IsNull(r.refusal, r.refusal);
            Assert.IsTrue(r.run.completed, r.run.blockedReason);
            Assert.AreEqual(26, r.track.Count);

            float maxBp = 0f;
            bool sawInflating = false;
            for (int i = 0; i < r.track.Count; i++)
            {
                maxBp = Mathf.Max(maxBp, r.track[i].bubblePressure);
                if (r.track[i].phase == LeveragePhase.Inflating) sawInflating = true;
            }
            Assert.Greater(r.track[25].bubblePressure, r.track[0].bubblePressure,
                "钉低利率下 bp 上升。bp0=" + r.track[0].bubblePressure + " bp25=" + r.track[25].bubblePressure);
            Assert.GreaterOrEqual(maxBp, 60f, "触及 Inflating 阈(60)。maxBp=" + maxBp);
            Assert.IsTrue(sawInflating, "轨迹中出现过 Inflating 相位。");
            Assert.Greater(r.track[25].priceIndex, r.track[0].priceIndex, "房价指数上行。");

            // 确定性重放:同一起点再跑一遍,轨迹逐点一致
            var r2 = HousingLabService.RunBubbleExperiment(state, 1f, 26);
            Assert.IsTrue(r2.run.completed);
            Assert.AreEqual(r.track.Count, r2.track.Count);
            for (int i = 0; i < r.track.Count; i++)
            {
                Assert.AreEqual(r.track[i].bubblePressure, r2.track[i].bubblePressure, 1e-4f,
                    "第 " + i + " 期 bp 复跑分叉。");
                Assert.AreEqual(r.track[i].priceIndex, r2.track[i].priceIndex, 1e-4f,
                    "第 " + i + " 期价指复跑分叉。");
                Assert.AreEqual(r.track[i].priceRentRatio, r2.track[i].priceRentRatio, 1e-4f,
                    "第 " + i + " 期价租比复跑分叉。");
            }
        }

        // ———————————————— #10 销毁清理 ————————————————

        [Test]
        public void Fork_Destroy_NoOrphanState_CleansUp()
        {
            string json = WorldForkService.CaptureJson(state);
            var result = WorldForkService.RunClonedWorld(json, 3, null, null, null);
            Assert.IsTrue(result.completed, result.blockedReason);
            var survivors = UnityEngine.Object.FindObjectsByType<GameState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, survivors.Length, "fork 销毁后场景只剩主世界 GameState。");
            Assert.IsTrue(ReferenceEquals(state, survivors[0]));
            Assert.IsNull(GameObject.Find("WorldFork"), "WorldFork 对象应已销毁。");
            Assert.IsTrue(ReferenceEquals(GameState.I, state), "I 恢复主世界。");
        }
    }
}

#endif
