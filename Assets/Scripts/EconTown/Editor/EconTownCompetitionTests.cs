#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownCompetitionTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("TestGameState");
            state = stateObject.AddComponent<GameState>();
            state.materials = 1000;
            state.planProduction = 120;
            state.employees = 3;
            DaySimulator.SetRandomSeed(20260819);
        }

        [TearDown]
        public void TearDown()
        {
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void PurchasedMaterialsAreNotChargedTwiceFromCash()
        {
            state.day = 3;
            state.money = 1700f; // 已经用 300 元采购过 50 份原料
            state.materials = 50;
            state.planProduction = 50;
            state.employees = 1;
            DaySimulator.DemandRoll noDemand = new DaySimulator.DemandRoll();

            DaySimulator.Settle(state, noDemand, 50);

            Assert.AreEqual(1400f, state.money, 0.01f,
                "结算现金只应再扣工资与租金，不能把采购时已支付的原料重复扣除。");
        }

        [Test]
        public void CompetitionMarketAllocatesConsumersAcrossBothStores()
        {
            PrepareCompetitionDay();

            CompetitionSimulator.MarketRoll roll = CompetitionSimulator.RollMarket(state);

            Assert.Greater(roll.Total, 100);
            Assert.Greater(roll.player.Total, 0);
            Assert.Greater(roll.competitor.Total, 0);
            Assert.GreaterOrEqual(roll.outside, 0);
            Assert.AreEqual(roll.Total, roll.player.Total + roll.competitor.Total + roll.outside);
        }

        [Test]
        public void StrongBrandProtectsDemandAtTheSameHighPrice()
        {
            PrepareCompetitionDay();
            state.price = 22f;
            state.playerBrand = 10f;
            CompetitionSimulator.ProfitEstimate weak = CompetitionSimulator.Estimate(state, state.price, state.competitor.price);

            state.playerBrand = 90f;
            CompetitionSimulator.ProfitEstimate strong = CompetitionSimulator.Estimate(state, state.price, state.competitor.price);

            Assert.Greater(strong.playerSold, weak.playerSold,
                "高品牌应降低顾客对高价的敏感度，形成市场势力。 ");
        }

        [Test]
        public void MutualPriceCutsReduceCombinedProfit()
        {
            PrepareCompetitionDay();
            state.day = 22;
            state.price = 18f;
            state.competitor.price = 18f;
            state.marketingBudget = 0f;
            state.competitor.marketing = 0f;

            CompetitionSimulator.EstimatePayoffMatrix(state, out CompetitionSimulator.ProfitEstimate bothHold,
                out CompetitionSimulator.ProfitEstimate playerCuts, out CompetitionSimulator.ProfitEstimate competitorCuts,
                out CompetitionSimulator.ProfitEstimate bothCut);

            Assert.Less(bothCut.playerProfit + bothCut.competitorProfit,
                bothHold.playerProfit + bothHold.competitorProfit,
                "双方一起降价时，行业合计利润应该低于双方保持价格，才能形成囚徒困境。 ");
        }

        [Test]
        public void CompetitionSettlementProducesValidMarketShares()
        {
            PrepareCompetitionDay();
            CompetitionSimulator.MarketRoll roll = CompetitionSimulator.RollMarket(state);
            int produced = DaySimulator.Production(state);

            DayRecord record = CompetitionSimulator.Settle(state, roll, produced);

            Assert.IsTrue(record.competitionActive);
            Assert.AreEqual(1f, record.playerMarketShare + record.competitorMarketShare, 0.001f);
            Assert.AreEqual(1, state.history.Count);
            Assert.IsFalse(float.IsNaN(record.profit));
            Assert.IsFalse(float.IsNaN(record.competitorProfit));
        }

        [Test]
        public void TwoDayPromotionDoesNotCompoundTheDiscount()
        {
            PrepareCompetitionDay();
            state.day = 22;
            state.competitor.price = 16f;
            state.competitor.promoBasePrice = 20f;
            state.competitor.promoDays = 1;

            CompetitorAI.PrepareDay(state);
            Assert.AreEqual(16f, state.competitor.price, 0.01f);
            Assert.AreEqual(0, state.competitor.promoDays);

            CompetitorAI.PrepareDay(state);
            Assert.AreEqual(20f, state.competitor.price, 0.01f,
                "促销结束应回到原价，不能在 16 元上再打八折。");
        }

        [Test]
        public void HoldingPriceEndsThePriceWarInsteadOfFlaggingEveryLaterDay()
        {
            PrepareCompetitionDay();
            state.day = 22;
            state.priceWarActive = true;
            state.priceWarRound = 1;
            state.history.Add(new DayRecord
            {
                day = 21,
                competitionActive = true,
                price = 18f,
                competitorPrice = 14f
            });

            CompetitorAI.PrepareDay(state);

            Assert.IsFalse(state.priceWarActive);
            Assert.AreEqual(CompetitorStrategy.Differentiation, state.competitor.strategy);
        }

        [Test]
        public void ExplicitNoneCannotBypassTheMandatoryChapterNineChoice()
        {
            PrepareCompetitionDay();
            state.progression.currentChapter = 9;
            state.progression.completedChapter = 8;
            CompetitorAI.PrepareDay(state);
            int currentDay = state.day;
            Assert.AreEqual(PlayerCompetitionChoice.None.ToString(), state.todayStrategyChoice,
                "前置：清晨尚未做出价格战选择。");

            bool advanced = WorldSettlementService.TrySettleAndAdvance(state, out int spoiled,
                out string blockedReason);

            Assert.IsFalse(advanced);
            Assert.AreEqual(0, spoiled);
            Assert.AreEqual(currentDay, state.day, "未做章节关键选择时不得推进世界日。");
            StringAssert.Contains("价格战选择", blockedReason);
            Assert.IsFalse(CompetitionEco.HasPlayerChoice(state.todayStrategyChoice));
            Assert.IsTrue(CompetitionEco.HasPlayerChoice(PlayerCompetitionChoice.FollowPrice.ToString()));
        }

        [Test]
        public void CounterfactualUsesPreSettlementCapacityAndInventory()
        {
            PrepareCompetitionDay();
            state.inventory = 30;
            state.competitor.inventory = 20;
            state.history.Add(new DayRecord { day = 15, competitionActive = true, price = 20f, competitorPrice = 18f });
            CompetitionSimulator.MarketRoll roll = CompetitionSimulator.RollMarket(state);
            CompetitionSimulator.ProfitEstimate expected = CompetitionSimulator.Estimate(state, 20f, state.competitor.price, roll.jitters);

            DayRecord record = CompetitionSimulator.Settle(state, roll, DaySimulator.Production(state));

            Assert.AreEqual(expected.playerSold, record.counterfactualSold);
            Assert.AreEqual(expected.playerProfit, record.counterfactualProfit, 0.01f);
        }

        [Test]
        public void AdvertisingIsADailyDecisionWithDecayingAwareness()
        {
            PrepareCompetitionDay();
            state.day = 20;
            state.marketingBudget = 500f;
            state.playerMarketingMomentum = 0f;

            CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            float campaignMomentum = state.playerMarketingMomentum;

            Assert.Greater(campaignMomentum, 0f, "投放广告后应留下后续可衰减的品牌余温。");
            DaySimulator.AdvanceDay(state);
            Assert.AreEqual(0f, state.marketingBudget, 0.001f, "广告预算是每日决策，次日不能自动续费。");
            Assert.Greater(state.playerMarketingMomentum, 0f, "次日仍应保留一部分广告余温。");
            Assert.Less(state.playerMarketingMomentum, campaignMomentum, "广告余温必须逐日衰减。");

            state.playerMarketingMomentum = 4f;
            CompetitionSimulator.ProfitEstimate withAwareness = CompetitionSimulator.Estimate(
                state, state.price, state.competitor.price);
            state.playerMarketingMomentum = 0f;
            CompetitionSimulator.ProfitEstimate withoutAwareness = CompetitionSimulator.Estimate(
                state, state.price, state.competitor.price);
            Assert.Greater(withAwareness.playerSold, withoutAwareness.playerSold,
                "残留的广告认知应继续带来一部分顾客，而不是成为无效存档字段。");
        }

        [Test]
        public void CrossPriceElasticityRequiresStableCompetitorPrice()
        {
            PrepareCompetitionDay();
            state.price = 20f;
            state.history.Add(new DayRecord { day = 15, competitionActive = true, price = 16f,
                competitorPrice = state.competitor.price, competitorSold = 40 });
            DayRecord stable = CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            Assert.IsTrue(stable.hasCrossPriceElasticity, "对手价格未动时,涨价日的交叉弹性应该可计算。");

            PrepareCompetitionDay();
            state.price = 20f;
            state.history.Add(new DayRecord { day = 15, competitionActive = true, price = 16f,
                competitorPrice = state.competitor.price - 3f, competitorSold = 40 });
            DayRecord moved = CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), DaySimulator.Production(state));
            Assert.IsFalse(moved.hasCrossPriceElasticity,
                "对手自己也调过价时,它的销量变化无法归因于我的价格,交叉弹性必须置为不可算。");
        }

        void PrepareCompetitionDay()
        {
            state.day = 6;
            state.totalDays = 30;
            state.money = 8000f;
            state.price = 18f;
            state.playerQualityLevel = 2;
            state.playerBrand = 45f;
            state.competitor = new CompetitorState();
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            CompetitorAI.PrepareDay(state);
        }
    }
}
#endif
