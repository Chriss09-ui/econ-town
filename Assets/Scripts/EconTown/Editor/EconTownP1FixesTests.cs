#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>2026-08-24 深度审查 P1(核心模拟层)修复的回归测试。</summary>
    public class EconTownP1FixesTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("P1FixTestState");
            state = stateObject.AddComponent<GameState>();
            DaySimulator.SetRandomSeed(20260824);
            DaySimulator.LastEvent = null;
            // 注入临时存档目录:真实存档零接触,编辑器崩溃也不会丢玩家进度(P1-5)。
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownP1FixesTests_" + System.Guid.NewGuid().ToString("N"));
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
            DaySimulator.LastEvent = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— C8:同日双结算不得双计税基 —————

        [Test]
        public void DoubleSettlementDoesNotDoubleCountPendingDrinkTax()
        {
            GameState.I = state;
            state.day = 16;
            state.totalDays = 30;
            state.money = 8000f;
            state.price = 18f;
            CompetitorAI.PrepareDay(state);
            CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            float pendingAfterFirst = state.government.pendingDrinkTaxRevenue;
            float subsidyAfterFirst = state.government.pendingSubsidySpending;
            Assert.GreaterOrEqual(pendingAfterFirst, 0f);

            // 异常路径:同一天第二次完整结算。记录会被去重防御合并,税/补贴基也绝不能记两次。
            CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            Assert.AreEqual(pendingAfterFirst, state.government.pendingDrinkTaxRevenue, 0.01f,
                "同日双结算不得把待入账饮品税记两次。");
            Assert.AreEqual(subsidyAfterFirst, state.government.pendingSubsidySpending, 0.01f,
                "同日双结算不得把待入账补贴记两次。");
            Assert.AreEqual(1, state.history.Count, "前置:同日记录仍只有一条。");
        }

        // ————— C10:终局之后不得再推进世界 —————

        [Test]
        public void SettlementIsRejectedAfterGameEnded()
        {
            GameState.I = state;
            state.totalDays = 3;
            state.phase = GamePhase.Report;
            // 推进到最后一日并正常收日:应产生第 3 期政府/宏观记录,世界日到 4。
            state.day = 3;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out _));
            Assert.AreEqual(4, state.day);
            int govCycles = state.government.history.Count;
            int macroPeriods = state.macro.history.Count;
            state.phase = GamePhase.Ended;

            bool again = WorldSettlementService.TrySettleAndAdvance(state, out _, out string reason);
            Assert.IsFalse(again, "终局后不得再执行任何世界结算。");
            StringAssert.Contains("结束", reason);
            Assert.AreEqual(4, state.day, "终局后世界日不得再推进。");
            Assert.AreEqual(govCycles, state.government.history.Count, "终局后不得写入额外政府周期。");
            Assert.AreEqual(macroPeriods, state.macro.history.Count, "终局后不得写入额外宏观期数。");
        }

        // ————— C7:学校生产率加成只授予一次 —————

        [Test]
        public void SchoolProductivityBonusAppliesOnlyOnce()
        {
            GovernmentSimulator.Ensure(state);
            GovernmentSimulator.BuildProject(state, PublicProjectType.School);
            float productivityBefore = state.macro.productivity;
            MacroPeriodRecord m1 = MacroEconomySimulator.AdvancePeriod(state);
            state.day++;
            MacroPeriodRecord m2 = MacroEconomySimulator.AdvancePeriod(state);
            state.day++;
            MacroPeriodRecord m3 = MacroEconomySimulator.AdvancePeriod(state);

            float firstGrowth = m1.productivity - productivityBefore;
            float laterGrowth = m3.productivity - m2.productivity;
            Assert.Greater(firstGrowth, 0.3f, "学校建成后的第一期应一次性授予 +0.32 生产率。");
            // 自然增长只有投资项(约 0.17),阈值取 0.25:足以拦下重复的 +0.32,又不误伤自然增长。
            Assert.Less(m2.productivity - m1.productivity, 0.25f, "第二期不得重复累加学校加成。");
            Assert.Less(laterGrowth, 0.25f, "后续各期不得每期重复累加学校加成(原缺陷:30 期白赚 +9.6)。");
        }

        // ————— C3:今日事件按稳定 id 重建,改名免疫 —————

        [Test]
        public void LoadRestoresLastEventByIdSurvivingEventRename()
        {
            state.day = 5;
            state.todayEventName = TownEvents.HotWeather.name;
            state.todayEventId = TownEvents.HotWeather.id;
            SaveSystem.Save(state);
            // 模拟未来事件改名:存档里的中文名与代码不再一致,但 id 不变。
            string json = File.ReadAllText(SaveSystem.SavePath);
            Assert.IsTrue(json.Contains("\"todayEventId\": \"hot\""), "前置:存档应携带稳定事件 id。");
            File.WriteAllText(SaveSystem.SavePath, json.Replace(TownEvents.HotWeather.name, "已改名事件"));

            GameState fresh = LoadIntoFreshState();
            GameState.I = fresh;
            Assert.IsTrue(SaveSystem.Load(fresh));
            Assert.AreSame(TownEvents.HotWeather, DaySimulator.LastEvent, "应按 id 而非中文文案重建今日事件。");
            DestroyFreshState(fresh);
        }

        [Test]
        public void LegacySaveWithoutEventIdStillRestoresByName()
        {
            state.day = 5;
            state.todayEventName = TownEvents.Festival.name;
            SaveSystem.Save(state);
            // 模拟 V0.5.1 旧档:没有 todayEventId 字段,只能按名字回查。
            string json = File.ReadAllText(SaveSystem.SavePath);
            File.WriteAllText(SaveSystem.SavePath, json.Replace("    \"todayEventId\": \"festival\",\n", ""));

            GameState fresh = LoadIntoFreshState();
            GameState.I = fresh;
            Assert.IsTrue(SaveSystem.Load(fresh));
            Assert.AreSame(TownEvents.Festival, DaySimulator.LastEvent, "旧档无 id 时应回退按名字重建。");
            DestroyFreshState(fresh);
        }

        /// <summary>
        /// 读档目标用独立 GameObject:此前在同一 stateObject 上二次 AddComponent,若未来
        /// EditMode 行为变化触发了 GameState.Awake 的单例防御(Destroy(gameObject)),
        /// 会连坐销毁整个共享 stateObject(P2-13)。
        /// </summary>
        static GameState LoadIntoFreshState()
        {
            GameObject freshObject = new GameObject("P1FixFreshState");
            return freshObject.AddComponent<GameState>();
        }

        static void DestroyFreshState(GameState fresh)
        {
            if (fresh != null) UnityEngine.Object.DestroyImmediate(fresh.gameObject);
        }

        // ————— C4:福利支出按首批覆盖人数计量,与固定收入同量级 —————

        [Test]
        public void WelfareSpendingScalesWithEnrolledCoverage()
        {
            GovernmentSimulator.Ensure(state);
            GovernmentSimulator.SetPolicy(state, PolicyType.BasicWelfare, 25f);
            int enrolled = GovernmentSimulator.WelfareEnrolledResidents(state);
            Assert.AreEqual(32, enrolled, "首批覆盖 = 128 名符合者的 25%。");
            float expected = enrolled * GovernmentSimulator.WelfarePerEligibleResident(state);
            Assert.AreEqual(expected, GovernmentSimulator.ProjectedWelfareSpending(state), 0.01f);
            Assert.Less(GovernmentSimulator.ProjectedWelfareSpending(state), 1200f,
                "最低档(¥25)福利的周期支出必须与政府固定收入(¥600)同量级,中间档位才有可玩空间。");
        }

        // ————— C5:居民买自家果汁要在选择说明里讲清独立账本口径 —————

        [Test]
        public void PlayerJuiceChoiceExplainsSeparateResidentLedger()
        {
            ResidentEconomy.CreateProfile(state, "小测", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            state.resident.cash = 100f;
            ResidentActionResult result = ResidentEconomy.Consume(state, ResidentItemKind.PlayerJuice);
            Assert.IsTrue(result.success, "前置:消费应成功。");
            StringAssert.Contains("居民账本", state.resident.lastChoiceReason,
                "买自家果汁必须向玩家说明:这笔钱记在居民账本,不直接进门店收入——否则玩家会以为店里卖出了钱没进账。");
        }
    }
}
#endif
