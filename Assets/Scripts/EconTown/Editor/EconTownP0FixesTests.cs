#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>暴露 protected BeginPanel 供 EditMode 测试直接调用。</summary>
    public class ExposedUIManager : UIManager
    {
        public GameObject OpenPanel(string title, float w, float h) { return BeginPanel(title, w, h); }
    }

    /// <summary>2026-08-24 深度审查 P0 修复的回归测试。</summary>
    public class EconTownP0FixesTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("P0FixTestState");
            state = stateObject.AddComponent<GameState>();
            DaySimulator.SetRandomSeed(20260824);
            // 注入临时存档目录:真实存档零接触,编辑器崩溃也不会丢玩家进度(P1-5)。
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownP0FixesTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            GameState.I = null; // EditMode 不走 Awake,单例由测试手动挂接,结束同样手动摘除。
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        /// <summary>把主档 JSON 中 money 字段替换为指定字面量(不依赖 JsonUtility 的浮点排版)。</summary>
        static void RewriteMoney(float from, string to)
        {
            string json = File.ReadAllText(SaveSystem.SavePath);
            Assert.IsTrue(json.Contains("\"money\": " + from.ToString("R")),
                "前置:存档 JSON 中应能定位 money 字段(实际内容异常)。");
            File.WriteAllText(SaveSystem.SavePath, json.Replace("\"money\": " + from.ToString("R"), "\"money\": " + to));
        }

        [Test]
        public void NewGameSeedIsTimeDerivedWhileExplicitSeedStaysReproducible()
        {
            int seed1 = DaySimulator.SeedNewGame(state);
            int seed2 = DaySimulator.SeedNewGame(state);
            Assert.AreNotEqual(seed1, seed2, "同刻连续两次新局播种由计数器区分,不得相同。");
            Assert.AreNotEqual(20260824, seed1, "新局种子不得停留在默认固定值,否则每局事件序列雷同可背板。");
            Assert.AreEqual(seed2, state.economy.random.seed);
            Assert.AreEqual(0L, state.economy.random.calls);

            GameState.I = state; // EditMode 不会自动调用 Awake,手动挂单例后 SetRandomSeed 才会落到本状态。
            DaySimulator.SetRandomSeed(42);
            float a1 = state.economy.random.Next01();
            float a2 = state.economy.random.Next01();
            DaySimulator.SetRandomSeed(42);
            Assert.AreEqual(a1, state.economy.random.Next01(), "显式播种(测试/复现用途)必须仍然可复现。");
            Assert.AreEqual(a2, state.economy.random.Next01());
        }

        [Test]
        public void MacroInitialDeclaredValuesMatchFirstFormulaPass()
        {
            state.government = new GovernmentState();
            state.resident = new ResidentState();
            state.macro = new MacroEconomyState();
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            WorldClock.SyncDerivedPeriods(state);
            MacroEconomyState m = state.macro;
            float c0 = m.consumption, i0 = m.investment, g0 = m.governmentSpending, x0 = m.exports,
                im0 = m.imports, nx0 = m.netExports, gdp0 = m.nominalGdp, real0 = m.realGdp,
                pot0 = m.potentialGdp, gap0 = m.outputGap, ad0 = m.aggregateDemand,
                as0 = m.aggregateSupply, pc0 = m.perCapitaGdp;
            // bug搜#1:就业六字段纳入同一对账——旧清单只钉 GDP 系 13 字段,就业声明值
            // (旧口径 outputGap≈-3)与首期公式(≈-17.57)差一倍漏网,开局失业率一日翻倍跳变。
            float lf0 = m.laborForce, emp0 = m.employment, unemp0 = m.unemployment, rate0 = m.unemploymentRate,
                fr0 = m.frictionalUnemployment, st0 = m.structuralUnemployment, cy0 = m.cyclicalUnemployment;
            MacroCyclePhase phase0 = m.cyclePhase;
            MacroEconomySimulator.AdvancePeriod(state);
            // 声明初值必须与首期公式输出一致:宏观面板第 1 天清晨展示声明值,当日结算后展示公式值,
            // 两者不一致就是一次无解释的读数跳变(MacroEconomyModels 注释承诺过对齐)。
            System.Text.StringBuilder drift = new System.Text.StringBuilder();
            Check(drift, "consumption", c0, m.consumption);
            Check(drift, "investment", i0, m.investment);
            Check(drift, "governmentSpending", g0, m.governmentSpending);
            Check(drift, "exports", x0, m.exports);
            Check(drift, "imports", im0, m.imports);
            Check(drift, "netExports", nx0, m.netExports);
            Check(drift, "nominalGdp", gdp0, m.nominalGdp);
            Check(drift, "realGdp", real0, m.realGdp);
            Check(drift, "potentialGdp", pot0, m.potentialGdp);
            Check(drift, "outputGap", gap0, m.outputGap);
            Check(drift, "aggregateDemand", ad0, m.aggregateDemand);
            Check(drift, "aggregateSupply", as0, m.aggregateSupply);
            Check(drift, "perCapitaGdp", pc0, m.perCapitaGdp);
            Check(drift, "laborForce", lf0, m.laborForce);
            Check(drift, "employment", emp0, m.employment);
            Check(drift, "unemployment", unemp0, m.unemployment);
            Check(drift, "unemploymentRate", rate0, m.unemploymentRate);
            Check(drift, "frictionalUnemployment", fr0, m.frictionalUnemployment);
            Check(drift, "structuralUnemployment", st0, m.structuralUnemployment);
            Check(drift, "cyclicalUnemployment", cy0, m.cyclicalUnemployment);
            // bug搜#1复审:周期相与政府镜像同受「首期读数不跳变」约束——首期分类器
            // previous==null→growth=0、gap<−5 → Trough;政府就业率声明=100−宏观首期失业率。
            Assert.AreEqual(phase0, m.cyclePhase,
                "周期相声明值必须是首期公式输出(首期 growth=0 口径下 gap<−5 → Trough),否则面板「复苏」首日跳「谷底」。");
            Check(drift, "governmentEmploymentRate", 100f - m.unemploymentRate,
                state.government.employmentRate);
            if (drift.Length > 0)
                Assert.Fail("宏观声明初值与首期公式输出不一致(改公式必须同步更新 MacroEconomyModels 初值):\n" + drift);
        }

        static void Check(System.Text.StringBuilder sb, string field, float declared, float actual)
        {
            float tol = Mathf.Max(0.5f, Mathf.Abs(declared) * 0.005f);
            if (Mathf.Abs(declared - actual) > tol)
                sb.Append(field).Append(": 声明 ").Append(declared).Append(" vs 公式 ").Append(actual).Append('\n');
        }

        [Test]
        public void BeginPanelDoesNotUnconditionallyQueueFontRebind()
        {
            GameObject go = new GameObject("UiManagerTest");
            ExposedUIManager ui = go.AddComponent<ExposedUIManager>();
            ui.InitializeForValidation();
            // 首次打开:新字形进入动态字体图集会真实触发 textureRebuilt,排队重绑是合法的。
            ui.OpenPanel("P0重绑护栏面板", 400f, 300f);
            // 模拟 LateUpdate 消费掉待处理标志(EditMode 不跑 LateUpdate)。
            typeof(UIManager).GetField("dynamicFontRefreshPending",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(ui, false);
            // 再次打开同文案面板:没有新字形,不应再无条件排队全树重绑+两次 ForceUpdateCanvases。
            ui.OpenPanel("P0重绑护栏面板", 400f, 300f);
            Assert.IsFalse(ui.DynamicFontRefreshPendingForTest,
                "无新字形的开面板不应触发全树字体重绑;真正的触发时机是字体图集重建回调。");
            // 回调路径必须仍然有效:动态中文字体扩图集后旧 Text 的 UV 失效,靠它帧末统一重绑。
            typeof(UIManager).GetMethod("OnFontTextureRebuilt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(ui, new object[] { null });
            Assert.IsTrue(ui.DynamicFontRefreshPendingForTest, "Font.textureRebuilt 回调路径必须仍然有效。");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SaveWritesVersionAndKeepsRollingBackup()
        {
            state.day = 3; state.money = 2500f;
            SaveSystem.Save(state);
            state.day = 4; state.money = 3000f;
            SaveSystem.Save(state);

            string json = File.ReadAllText(SaveSystem.SavePath);
            StringAssert.Contains("\"saveVersion\": \"" + GameState.Version + "\"", json,
                "存档必须携带版本号,未来格式变更才能按版本迁移而非静默丢字段。");
            StringAssert.Contains("\"day\": 4", json);
            Assert.IsTrue(File.Exists(SaveSystem.SavePath + ".bak"), "第二次存档后必须留下上一份备份。");
            StringAssert.Contains("\"day\": 3", File.ReadAllText(SaveSystem.SavePath + ".bak"),
                "备份内容应为上一次的存档。");
        }

        [Test]
        public void LegacySaveWithoutVersionStillLoads()
        {
            state.day = 5; state.money = 4000f;
            SaveSystem.Save(state);
            // 模拟 V0.5.1 旧档:整行剔除版本字段后仍应正常加载。
            string[] lines = File.ReadAllLines(SaveSystem.SavePath);
            using (StreamWriter sw = new StreamWriter(SaveSystem.SavePath))
                foreach (string line in lines)
                    if (!line.Contains("saveVersion")) sw.WriteLine(line);
            Assert.IsFalse(File.ReadAllText(SaveSystem.SavePath).Contains("saveVersion"), "前置:版本行已剔除。");
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(5, state.day);
        }

        [Test]
        public void CorruptMainSaveFallsBackToBackup()
        {
            state.day = 2; state.money = 2100f;
            SaveSystem.Save(state);
            state.day = 6; state.money = 5000f;
            SaveSystem.Save(state);
            File.WriteAllText(SaveSystem.SavePath, "{{{ 不是 JSON");

            // 复用同一状态即可:本工程已实证 6000.5 的 EditMode 下 AddComponent 不触发 Awake,
            // 不存在"二次 AddComponent 触发单例 Destroy"的问题(旧注释表述有误,P2-13 一并修正)。
            Assert.IsTrue(SaveSystem.Load(state), "主档损坏时应自动回退到 .bak 备份。");
            Assert.AreEqual(2, state.day, "恢复的应是备份时刻(day=2)的进度。");
        }

        [Test]
        public void NonFiniteMoneyIsRejectedWithoutPollutingState()
        {
            state.day = 7; state.money = 2600.5f;
            SaveSystem.Save(state);
            RewriteMoney(2600.5f, "NaN");

            float moneyBefore = state.money;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "存档与备份均无法读取。");
            Assert.IsFalse(SaveSystem.Load(state), "含 NaN 货币的存档必须整体拒载(无论解析层还是校验层拦下)。");
            Assert.AreEqual(moneyBefore, state.money, "拒载时不得部分污染目标状态。");
        }

        [Test]
        public void AbsurdButFiniteMoneyIsClampedNotRejected()
        {
            state.day = 8; state.money = 2600.5f;
            SaveSystem.Save(state);
            RewriteMoney(2600.5f, "-1000000000000"); // 无指数记法的有限负天文数。

            Assert.IsTrue(SaveSystem.Load(state), "有限值不应拒载——财务危机设计允许负货币。");
            Assert.AreEqual(-1000000f, state.money, "越界有限值应 clamp 到下界。");
        }
    }
}
#endif
