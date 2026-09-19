#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9 开工前置的四件教学正确性修复的回归测试(母版 §186-2)。</summary>
    public class EconTownV09PrepFixesTests
    {
        GameObject stateObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("PrepFixState");
            GameState.I = stateObject.AddComponent<GameState>();
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        /// <summary>C4:averageWage 声明初值与首期公式 EffectiveDailyWage/8 一致,首期前读数不跳变。</summary>
        [Test]
        public void AverageWage_DeclaredDefault_MatchesFirstPeriodFormula()
        {
            GovernmentSimulator.Ensure(GameState.I);
            float firstPeriodWage = GovernmentSimulator.EffectiveDailyWage(GameState.I) / 8f;
            Assert.AreEqual(firstPeriodWage, new MacroEconomyState().averageWage, .001f,
                "声明初值应为 " + firstPeriodWage + "(默认工资 200/8),不是 28。");
        }

        /// <summary>①税楔图真实化:无税时 D∩S 均衡恰为当前营业点——均衡不再恒在画布中心。</summary>
        [Test]
        public void TaxGraphModel_NoTax_EquilibriumAtOperatingPoint()
        {
            PriceQuote quote = Quote(12f, 12f, 1.22f, 1.8f);
            TaxGraphModel model = new TaxGraphModel(quote, 40f);
            Assert.AreEqual(0f, model.wedgePerUnit, 1e-4f, "无税无楔。");
            Assert.AreEqual(quote.consumerPrice, model.noTaxPrice, .01f, "无税均衡价=当前价。");
            Assert.AreEqual(40f, model.noTaxQuantity, .01f, "无税均衡量=当前量。");
        }

        /// <summary>①税楔图真实化:有税时楔=单位税,红色税后供给恰过当前营业点,无税均衡在两价之间且量更高。</summary>
        [Test]
        public void TaxGraphModel_WithTax_WedgeAndShiftedSupplyConsistent()
        {
            PriceQuote quote = Quote(13.06f, 11.56f, 1.22f, 1.8f);
            TaxGraphModel model = new TaxGraphModel(quote, 40f);
            Assert.AreEqual(1.5f, model.wedgePerUnit, .01f, "楔=买方价−卖方价=单位税。");
            Assert.AreEqual(40f, model.ShiftedSupplyAt(quote.consumerPrice), .1f,
                "税后供给应过当前营业点(自洽)。");
            Assert.That(model.noTaxPrice, Is.InRange(quote.producerNetPrice, quote.consumerPrice),
                "无税均衡价应落在卖方价与买方价之间。");
            Assert.Greater(model.noTaxQuantity, 40f, "撤税应扩大交易量。");
        }

        /// <summary>①外部性图:污染税使社会最优点数量低于市场量、价格高于卖方净价。</summary>
        [Test]
        public void ExternalityModel_PollutionTax_SocialOptimumLeftOfMarket()
        {
            PriceQuote quote = Quote(12f, 12f, 1.22f, 1.8f);
            ExternalityGraphModel ext = new ExternalityGraphModel(quote, 40f, 1.5f);
            Assert.Less(ext.socialQuantity, ext.marketQuantity, "社会最优量应低于市场量。");
            Assert.Greater(ext.socialPrice, quote.producerNetPrice, "社会最优价应高于卖方净价。");
        }

        static PriceQuote Quote(float consumerPrice, float producerNetPrice, float ed, float es)
        {
            return new PriceQuote
            {
                consumerPrice = consumerPrice,
                producerNetPrice = producerNetPrice,
                uncontrolledConsumerPrice = consumerPrice,
                demandElasticity = ed,
                supplyElasticity = es,
                consumerTaxShare = es / (es + ed)
            };
        }
    }
}

#endif
