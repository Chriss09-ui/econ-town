#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>存档/读档与居民工资入账的回归测试(对应 2026-08 修复的三个数据 BUG)。</summary>
    public class EconTownSaveLoadTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("SaveLoadTestState");
            state = stateObject.AddComponent<GameState>();
            state.materials = 1000;
            state.planProduction = 120;
            state.employees = 3;
            state.competitor = new CompetitorState();
            DaySimulator.SetRandomSeed(20260821);
            // 注入临时存档目录:真实存档零接触,编辑器崩溃也不会丢玩家进度(P1-5)。
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownSaveLoadTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void NonFiniteValuesAnywhereInSaveAreRejected()
        {
            // P2-2 回归:非法值校验必须覆盖存档全部 float 字段,而不是只拦 money——
            // competitor.fixedCost 的 NaN 会顺着成本→利润→就业→GDP 污染整套指标。
            state.day = 5;
            state.competitor.fixedCost = 4321.5f;
            SaveSystem.Save(state);
            string json = File.ReadAllText(SaveSystem.SavePath);
            Assert.IsTrue(json.Contains("\"fixedCost\": 4321.5"), "前置:定位 competitor.fixedCost 字段。");
            File.WriteAllText(SaveSystem.SavePath, json.Replace("\"fixedCost\": 4321.5", "\"fixedCost\": NaN"));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "存档与备份均无法读取。");
            Assert.IsFalse(SaveSystem.Load(state), "任意位置的 NaN 都必须让主档被拒载并走备份回退链。");
        }

        [Test]
        public void PaydayMorningWageUsesFullMultiplierFormula()
        {
            // P2-6 回归:发工资日清晨提前入账必须与晚间口径一致(含技能/效率乘数)——
            // 此前清晨只乘政策乘数,技能 40 满级的居民发工资日实发反而比普通日少 40%。
            ResidentEconomy.CreateProfile(state, "小测", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            state.resident.skillLevel = 40f;   // 满技能:新旧口径差 40%,足以区分回归
            ResidentEconomy.EndDay(state);
            state.day++;
            ResidentEconomy.BeginNextDay(state);
            Assert.AreEqual("发工资", state.resident.todayEventName);
            Assert.IsTrue(state.resident.wageAdvanced, "前置:事件日清晨已提前入账。");

            float expected = ResidentEconomy.IncomeFor(state.resident.profession) *
                GovernmentSimulator.ResidentIncomeMultiplier(state) *
                ResidentEconomy.SkillIncomeMultiplier(state.resident) *
                ResidentEconomy.WorkEfficiency(state.resident);
            Assert.AreEqual(expected, state.resident.dailyIncome, 0.01f,
                "清晨提前入账必须用完整口径(含技能/效率乘数),而不是只乘政策乘数。");
        }

        [Test]
        public void LoadingAfterSettlementKeepsOneRecordPerDay()
        {
            state.day = 16;
            state.totalDays = 30;
            state.money = 8000f;
            state.price = 18f;
            state.playerQualityLevel = 2;
            state.playerBrand = 45f;
            CompetitorAI.PrepareDay(state);
            CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            Assert.AreEqual(1, state.history.Count, "前置:当天已结算并写入一条记录。");

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(GamePhase.Morning, state.phase);
            Assert.AreEqual(16, state.day);
            Assert.AreEqual(0, state.history.Count,
                "读档回到当天清晨时必须丢弃已结算的当天记录,否则重玩会产生同一天两条记录,环比归因全部失真。");
        }

        [Test]
        public void MidDaySaveReloadClearsPendingTaxToPreventDoubleCount()
        {
            // P1-2 回归:存档若写于当日结算之后(Report 阶段,如旧版事件处理回调的写档),
            // 读档会丢弃当日记录并重放当日结算——已挂账未进周期账本的税费/补贴若不清零,
            // 重放会再累加一遍,日末 AdvanceCycle 把 2× 的税/补贴记进政府账本。
            state.day = 5;
            state.history.Add(new DayRecord { day = 5 });
            state.government.pendingDrinkTaxRevenue = 42f;
            state.government.pendingPollutionTaxRevenue = 7f;
            state.government.pendingSubsidySpending = 11f;
            state.government.pendingCapitalSpending = 13f;

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(0f, state.government.pendingDrinkTaxRevenue, 0.001f,
                "丢弃当日记录时已挂账的饮品税必须清零,否则重放结算双计。");
            Assert.AreEqual(0f, state.government.pendingPollutionTaxRevenue, 0.001f,
                "污染税挂账同样必须清零。");
            Assert.AreEqual(0f, state.government.pendingSubsidySpending, 0.001f,
                "补贴挂账同样必须清零。");
            Assert.AreEqual(0f, state.government.pendingCapitalSpending, 0.001f,
                "基建挂账同样必须清零。");
            Assert.AreEqual(0, state.government.lastDrinkTaxRecordedDay,
                "同日防重标记复位(既有行为)必须保持。");
        }

        [Test]
        public void ResidentReloadAfterEndDayReplaysThatDayFromMorning()
        {
            ResidentEconomy.CreateProfile(state, "存档测试", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            state.resident.cash = 120f;
            ResidentEconomy.EndDay(state);
            Assert.IsTrue(state.resident.reportReady, "前置:当天已完成并出报表。");

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));

            ResidentState r = state.resident;
            Assert.IsFalse(r.reportReady, "读档后当天要可重玩,reportReady 短路会静默丢弃重玩日的选择。");
            Assert.AreEqual(0, r.history.Count, "居民侧同样不能出现同一天两条记录。");
            Assert.AreEqual(0, r.completedDays, "被丢弃的记录对应的完成天数要一起回退。");
            Assert.AreEqual(ResidentEconomy.DailyFreeHours, r.freeHours, 0.001f);
            Assert.AreEqual(r.cash, r.cashAtDayStart, 0.001f, "重玩日的期初现金必须等于读档时的现金。");
        }

        [Test]
        public void PaydayEventDoesNotDoubleCreditDailyWage()
        {
            // 事件序号 =(lifeDay*37 + 名字长度*11)%6;两字名在 lifeDay=2 命中「发工资」。
            ResidentEconomy.CreateProfile(state, "小测", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            ResidentEconomy.EndDay(state);
            state.day++;
            ResidentEconomy.BeginNextDay(state);
            Assert.AreEqual(2, state.resident.lifeDay);
            Assert.AreEqual("发工资", state.resident.todayEventName);
            Assert.IsTrue(state.resident.wageAdvanced, "事件日清晨应标记日薪已提前入账。");

            float cashAfterMorning = state.resident.cash;
            float incomeAfterMorning = state.resident.todayIncome;
            // 日薪不重发,但符合条件的自动稳定器福利(宏观产出缺口派生)仍会在日终入账。
            float welfare = GovernmentSimulator.WelfarePerEligibleResident(state);
            ResidentDayRecord rec = ResidentEconomy.EndDay(state);

            Assert.AreEqual(cashAfterMorning + welfare, rec.cashEnd, 0.01f,
                "发工资日清晨已提前入账,EndDay 不得再发一次日薪(自动福利除外)。");
            Assert.AreEqual(incomeAfterMorning + welfare, rec.income, 0.01f);
            Assert.IsFalse(state.resident.wageAdvanced, "标记必须随日终清除,不能影响后续天。");
        }

        [Test]
        public void NormalDayReportIncludesDailyWageInCashEnd()
        {
            // 两字名的 lifeDay=1 事件是「好友邀请聚餐」(不动现金),属普通日。
            ResidentEconomy.CreateProfile(state, "小测", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            state.resident.cash = 200f;
            state.resident.cashAtDayStart = 200f;
            float cashBeforeEnd = state.resident.cash;
            float wage = state.resident.dailyIncome;
            // 测试居民受雇但收入低于福利线,日终会拿到自动稳定器福利(宏观产出缺口派生,整元结算)。
            float welfare = GovernmentSimulator.WelfarePerEligibleResident(state);
            Assert.IsTrue(ResidentEconomy.IsWelfareEligible(state.resident), "前置:该居民应符合福利条件。");

            ResidentDayRecord rec = ResidentEconomy.EndDay(state);

            Assert.AreEqual(cashBeforeEnd + wage + welfare, rec.cashEnd, 0.01f,
                "日报的 cashEnd 必须含当天日薪与自动福利。");
            Assert.AreEqual(wage + welfare, rec.income, 0.01f, "普通日收入 = 一份日薪 + 自动福利。");
            Assert.AreEqual(rec.cashEnd, state.resident.cash, 0.01f, "先入账再写记录,现金与 cashEnd 一致。");

            state.day++;
            ResidentEconomy.BeginNextDay(state);
            Assert.AreEqual(rec.cashEnd, state.resident.cashAtDayStart, 0.01f, "次日 cashAtDayStart 应与今日 cashEnd 严格衔接。");
        }

        [Test]
        public void GovernmentPoliciesProjectsAndDebtSurviveSaveLoad()
        {
            GovernmentSimulator.Ensure(state);
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            GovernmentSimulator.BuildProject(state, PublicProjectType.StreetLights);
            state.government.debt = 880f;
            state.government.chapter = 4;
            SaveSystem.Save(state);

            state.government = new GovernmentState();
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(3f, GovernmentSimulator.PolicyValue(state, PolicyType.DrinkTax), .001f);
            Assert.AreEqual(880f, state.government.debt, .001f);
            Assert.AreEqual(4, state.government.chapter);
            Assert.IsTrue(state.government.projects.Exists(x => x.type == PublicProjectType.StreetLights && x.built));
        }

        [Test]
        public void ResidentProfileCreationInheritsTheSingleWorldDay()
        {
            state.day = 18;

            ResidentEconomy.CreateProfile(state, "时间居民", ResidentProfession.Freelancer,
                ResidentPreference.Entertainment);

            Assert.AreEqual(18, state.resident.lifeDay,
                "创建居民档案不得从 Day 1 另起一条时间轴。居民日必须派生自 WorldDay。");
            Assert.AreEqual(state.day, state.government.cycle);
            Assert.AreEqual(state.day, state.macro.period);
        }

        [Test]
        public void CompleteWorldStateRoundTripPreservesEveryAuthoritativeSubsystem()
        {
            state.day = 18;
            state.money = 4321f;
            state.price = 19f;
            state.planProduction = 137;
            state.employees = 4;
            state.materials = 456;
            state.inventory = 37;
            state.currentView = PlayerViewMode.Government;
            state.todayEventName = "校园放假";
            state.todayCompetitionEventName = "对手促销";
            state.playerBrand = 63f;
            state.playerQualityLevel = 2;
            state.marketingBudget = 300f;
            state.playerMarketingMomentum = 1.7f;
            state.todayStrategyChoice = PlayerCompetitionChoice.HoldPrice.ToString();
            state.marketExperimentMask = 5;
            state.unlockedCards.Add("elasticity");
            state.unlockedCards.Add("gdp");

            state.economy.random.Reset(778899);
            state.economy.random.Next01();
            state.economy.random.Next01();
            state.economy.SetTemporaryPlayerPrice(3f, 20, "存档测试");
            state.economy.financialDistressDays = 2;
            state.economy.financialHealthLastWorldDay = 17;
            state.economy.labor.employment = 207;
            state.economy.labor.unemployment = 13;
            state.economy.labor.unemploymentRate = 5.91f;

            state.competitor.companyName = "Round Trip Tea";
            state.competitor.price = 16f;
            state.competitor.brand = 71f;
            state.competitor.inventory = 42;
            state.competitor.strategy = CompetitorStrategy.Differentiation;

            ResidentEconomy.CreateProfile(state, "存档居民", ResidentProfession.Freelancer, ResidentPreference.Entertainment);
            Assert.AreEqual(state.day, state.resident.lifeDay, "Round Trip 前置状态必须只有一个 WorldDay。");
            state.resident.cash = 188f;
            state.resident.savings = 92f;
            state.resident.skillLevel = 6f;
            state.resident.history.Add(new ResidentDayRecord { day = 17, income = 88f, spending = 54f, cashEnd = 188f });

            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            state.government.debt = 987f;
            state.government.enterpriseConfidence = 61f;
            state.government.currentEvent = new GovernmentEventState
            {
                id = "round_trip_event", title = "存档事件", description = "待处理",
                optionA = "A", optionB = "B", optionC = "C", resolved = false
            };

            state.macro.pendingShock = MacroShockType.EnergyCrisis;
            state.macro.consumerConfidence = 47f;
            state.macro.businessConfidence = state.government.enterpriseConfidence;
            state.macro.energyCostIndex = 138f;
            state.macro.marginalPropensityToConsume = .65f;
            MacroEconomySimulator.Ensure(state);

            state.history.Add(new DayRecord
            {
                day = 17, desiredDemand = 61, actualSales = 55, sold = 55,
                demandEffects = new DemandEffectBreakdown
                {
                    baseDemand = 70, ownPriceEffect = -7, randomEffect = -2, desiredDemand = 61
                }
            });

            string economyBefore = JsonUtility.ToJson(state.economy);
            string competitorBefore = JsonUtility.ToJson(state.competitor);
            string residentBefore = JsonUtility.ToJson(state.resident);
            string governmentBefore = JsonUtility.ToJson(state.government);
            string macroBefore = JsonUtility.ToJson(state.macro);

            SaveSystem.Save(state);
            state.economy = new EconomyState();
            state.competitor = new CompetitorState();
            state.resident = new ResidentState();
            state.government = new GovernmentState();
            state.macro = new MacroEconomyState();
            state.currentView = PlayerViewMode.Enterprise;
            state.unlockedCards.Clear();

            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(18, state.day);
            Assert.AreEqual(4321f, state.money, .001f);
            Assert.AreEqual(19f, state.price, .001f);
            Assert.AreEqual(137, state.planProduction);
            Assert.AreEqual(4, state.employees);
            Assert.AreEqual(456, state.materials);
            Assert.AreEqual(37, state.inventory);
            Assert.AreEqual(PlayerViewMode.Enterprise, state.currentView,
                "身份跟当前篇章走。第 1 章读档时，把越级写成镇长的视角收回店主。");
            Assert.AreEqual("校园放假", state.todayEventName);
            Assert.AreEqual("对手促销", state.todayCompetitionEventName);
            Assert.IsTrue(state.unlockedCards.Contains("elasticity"));
            Assert.IsTrue(state.unlockedCards.Contains("gdp"));
            Assert.AreEqual(economyBefore, JsonUtility.ToJson(state.economy));
            Assert.AreEqual(competitorBefore, JsonUtility.ToJson(state.competitor));
            Assert.AreEqual(residentBefore, JsonUtility.ToJson(state.resident));
            Assert.AreEqual(governmentBefore, JsonUtility.ToJson(state.government));
            Assert.AreEqual(macroBefore, JsonUtility.ToJson(state.macro));
            Assert.AreEqual(17, state.LastRecord.day);
            Assert.AreEqual(61, state.LastRecord.demandEffects.ModeledTotal);
            Assert.AreEqual("round_trip_event", state.government.currentEvent.id);
            Assert.IsFalse(state.government.currentEvent.resolved);
        }
    }
}
#endif
