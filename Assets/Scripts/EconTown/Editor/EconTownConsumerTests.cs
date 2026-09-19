#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownConsumerTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("ConsumerTestState");
            state = stateObject.AddComponent<GameState>();
            state.materials = 1000;
            state.planProduction = 140;
            state.employees = 3;
            state.competitor = new CompetitorState();
            ResidentEconomy.CreateProfile(state, "测试居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);
            DaySimulator.SetRandomSeed(20260820);
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void ResidentStartsWithAcceptanceBudgetAndFreeTime()
        {
            Assert.AreEqual(100f, state.resident.cash, .001f);
            Assert.AreEqual(4f, state.resident.freeHours, .001f);
            Assert.AreEqual(1000f, state.resident.savings, .001f);
            Assert.IsTrue(state.resident.profileCreated);
        }

        [Test]
        public void RepeatedConsumptionHasDiminishingMarginalUtility()
        {
            state.resident.cash = 200f;
            ResidentActionResult first = ResidentEconomy.Consume(state, ResidentItemKind.BasicMeal);
            ResidentActionResult second = ResidentEconomy.Consume(state, ResidentItemKind.BasicMeal);

            Assert.IsTrue(first.success);
            Assert.IsTrue(second.success);
            Assert.Less(second.choice.marginalUtility, first.choice.marginalUtility);
            Assert.Greater(second.choice.utility, first.choice.utility);
        }

        [Test]
        public void BudgetAndTimeConstraintsRejectImpossibleChoices()
        {
            state.resident.cash = 1f;
            ResidentActionResult tooExpensive = ResidentEconomy.Consume(state, ResidentItemKind.Movie);
            state.resident.cash = 100f;
            state.resident.freeHours = .25f;
            ResidentActionResult tooLong = ResidentEconomy.Consume(state, ResidentItemKind.PremiumMeal);

            Assert.IsFalse(tooExpensive.success);
            Assert.IsFalse(tooLong.success);
            Assert.AreEqual(0, state.resident.todayChoices.Count);
        }

        [Test]
        public void MovieRaisesPopcornUtilityAsAComplement()
        {
            state.resident.cash = 200f;
            ResidentActionResult plainPopcorn = ResidentEconomy.Consume(state, ResidentItemKind.Popcorn);

            ResidentEconomy.CreateProfile(state, "测试居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);
            state.resident.cash = 200f;
            Assert.IsTrue(ResidentEconomy.Consume(state, ResidentItemKind.Movie).success);
            ResidentActionResult moviePopcorn = ResidentEconomy.Consume(state, ResidentItemKind.Popcorn);

            Assert.Greater(moviePopcorn.choice.marginalUtility, plainPopcorn.choice.marginalUtility);
        }

        [Test]
        public void PriceShockMovesResidentAndEnterpriseDemandTogether()
        {
            state.day = 6;
            state.money = 8000f;
            state.price = 15f;
            state.playerQualityLevel = 2;
            state.playerBrand = 45f;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            CompetitorAI.PrepareDay(state);
            CompetitionSimulator.ProfitEstimate before = CompetitionSimulator.Estimate(state, state.price, state.competitor.price);

            ResidentEconomy.RunExperiment(state, 1);
            CompetitionSimulator.ProfitEstimate after = CompetitionSimulator.Estimate(state, state.price, state.competitor.price);

            Assert.AreEqual(15f, state.price, .001f, "居民实验不得永久改写玩家基础货架价");
            Assert.Greater(PriceService.PlayerListedPrice(state), state.price);
            Assert.AreEqual(1f, state.residentPlayerDemandMul, .001f, "价格效应只应由统一需求模型计算一次");
            Assert.Less(after.playerSold, before.playerSold);
            Assert.Greater(after.competitorSold, before.competitorSold);

            state.day = 17;
            Assert.AreEqual(state.price, PriceService.PlayerListedPrice(state), .001f);
        }

        [Test]
        public void WorkLeisureSavingAndDailyReportUpdateResources()
        {
            Assert.IsTrue(ResidentEconomy.Work(state, false).success);
            Assert.IsTrue(ResidentEconomy.Leisure(state, false).success);
            Assert.IsTrue(ResidentEconomy.Save(state, 20f).success);
            ResidentDayRecord report = ResidentEconomy.EndDay(state);

            Assert.IsTrue(state.resident.reportReady);
            Assert.AreEqual(1, state.resident.history.Count);
            Assert.AreEqual(20f, report.saved, .001f);
            Assert.Greater(report.freeHoursUsed, 0f);
            Assert.Greater(state.resident.savings, 1000f);

            state.day++;
            ResidentEconomy.BeginNextDay(state);
            Assert.AreEqual(2, state.resident.lifeDay);
            Assert.AreEqual(4f, state.resident.freeHours, .001f);
            Assert.IsFalse(state.resident.reportReady);
        }

        [Test]
        public void FourExperimentsAreRecordedAndUnlockRelevantConcepts()
        {
            for (int i = 0; i < 4; i++) Assert.IsNotEmpty(ResidentEconomy.RunExperiment(state, i));

            Assert.AreEqual(15, state.resident.experimentMask);
            Assert.IsTrue(state.unlockedCards.Contains("incomeeffect"));
            Assert.IsTrue(state.unlockedCards.Contains("substitutioneffect"));
            Assert.IsTrue(state.unlockedCards.Contains("complement"));
            Assert.IsTrue(state.unlockedCards.Contains("laborleisure"));
        }

        [Test]
        public void ResidentLifeSupportsThirtyDaysAndProducesProfile()
        {
            state.resident.cash = 250f;
            Assert.IsTrue(ResidentEconomy.Consume(state, ResidentItemKind.BasicMeal).success);
            Assert.IsTrue(ResidentEconomy.Consume(state, ResidentItemKind.BasicMeal).success);
            Assert.IsTrue(ResidentEconomy.Consume(state, ResidentItemKind.BasicMeal).success);
            for (int day = 1; day <= ResidentEconomy.TotalLifeDays; day++)
            {
                ResidentEconomy.EndDay(state);
                if (day < ResidentEconomy.TotalLifeDays)
                {
                    state.day++;
                    ResidentEconomy.BeginNextDay(state);
                    if (day == 1) ResidentEconomy.RunExperiment(state, 1);
                    if (day == 2) ResidentEconomy.RunExperiment(state, 0);
                    if (day == 3) ResidentEconomy.RunExperiment(state, 3);
                }
            }

            Assert.AreEqual(30, state.resident.history.Count);
            Assert.AreEqual(30, state.resident.lifeDay);
            Assert.IsNotEmpty(ResidentEconomy.LifeProfile(state.resident));
            Assert.AreEqual(7, ResidentEconomy.CurrentChapter(state.resident));
        }

        [Test]
        public void ThirtyDaysCoverAllSpecifiedResidentRandomEvents()
        {
            System.Collections.Generic.HashSet<string> events = new System.Collections.Generic.HashSet<string>();
            for (int day = 1; day <= ResidentEconomy.TotalLifeDays; day++)
            {
                events.Add(state.resident.todayEventName);
                ResidentEconomy.EndDay(state);
                if (day < ResidentEconomy.TotalLifeDays)
                {
                    state.day++;
                    ResidentEconomy.BeginNextDay(state);
                }
            }

            string[] expected = { "发工资", "意外奖金", "失去兼职", "商家促销", "商品涨价", "好友邀请聚餐" };
            for (int i = 0; i < expected.Length; i++) Assert.IsTrue(events.Contains(expected[i]), "缺少随机事件: " + expected[i]);
        }

        [Test]
        public void LifeProfileSupportsFiveNamedSettlementStyles()
        {
            ResidentState profile = new ResidentState { completedDays = 10 };
            profile.history.Add(new ResidentDayRecord { spending = 30f, totalUtility = 25f, freeHoursUsed = 2f });

            profile.workActions = 6;
            StringAssert.StartsWith("工作狂", ResidentEconomy.LifeProfile(profile));

            profile.workActions = 0; profile.savingsActions = 5; profile.savings = 1300f;
            StringAssert.StartsWith("储蓄达人", ResidentEconomy.LifeProfile(profile));

            profile.savingsActions = 0; profile.savings = 1000f; profile.premiumPurchases = 5; profile.basicPurchases = 1;
            profile.history[0].totalUtility = 45f;
            StringAssert.StartsWith("快乐主义者", ResidentEconomy.LifeProfile(profile));

            profile.premiumPurchases = 1; profile.basicPurchases = 7; profile.history[0].spending = 20f; profile.history[0].totalUtility = 25f;
            StringAssert.StartsWith("精打细算者", ResidentEconomy.LifeProfile(profile));

            profile.premiumPurchases = 2; profile.basicPurchases = 2; profile.history[0].spending = 45f;
            StringAssert.StartsWith("均衡生活者", ResidentEconomy.LifeProfile(profile));
        }
    }
}
#endif
