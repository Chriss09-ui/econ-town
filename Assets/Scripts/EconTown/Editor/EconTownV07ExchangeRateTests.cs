#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.7 批次2:汇率形成、分解恒等、外储零消耗、进口成本与 CPI 输入型通胀。</summary>
    public class EconTownV07ExchangeRateTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07FxTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260827);   // 噪声可复现(独立流)

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07FxTests_" + System.Guid.NewGuid().ToString("N"));
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
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        /// <summary>推入当日真实贸易流(测试用:绕过批次3的撮合层,直接造账本行)。</summary>
        TradeDayRecord PushFlow(float exports, float imports)
        {
            WorldTradeState trade = state.worldTrade;
            TradeDayRecord record = new TradeDayRecord
            {
                worldDay = state.day,
                exportsTotal = exports,
                importsTotal = imports,
                exchangeRateUsed = ExchangeRateService.Rate(state)
            };
            record.currentAccountBCurrency =
                (record.exportsTotal - record.importsTotal) / Mathf.Max(.01f, record.exchangeRateUsed);
            trade.history.Add(record);
            if (trade.history.Count > 730) trade.history.RemoveAt(0);
            return record;
        }

        void NextDay()
        {
            ExchangeRateService.ApplyDailyForm(state);
            state.day++;
        }

        [Test]
        public void RateDecomposition_SumMatchesActualMove_AtInteriorPrices()
        {
            // 三天不同方向的流量(逆差→贬值 / 顺差→升值 / 中性),全部保持在区间内点。
            PushFlow(620f, 1180f); NextDay();      // CA<0 → 贬值压力
            float before1 = state.worldTrade.exchangeRate.rate;
            PushFlow(1420f, 780f); NextDay();      // CA>0 → 升值压力
            RateDayRecord last = state.worldTrade.exchangeRate.history[state.worldTrade.exchangeRate.history.Count - 1];
            float actualMove = (last.rate - before1) / before1 * 100f;
            float sum = last.tradeFlowEffect + last.capitalFlowEffect +
                last.expectationEffect + last.interventionEffect + last.anchorPullEffect;
            Assert.AreEqual(actualMove, sum, .03f,
                "五项分解之和必须还原当日实际变动(P0 归因恒等)。");
            Assert.Less(last.tradeFlowEffect, 0f, "顺差日(CA>0)贸易分量必须是升值方向(负值,压低 X)。");

            PushFlow(900f, 900f);
            float before2 = state.worldTrade.exchangeRate.rate;
            NextDay();
            RateDayRecord mid = state.worldTrade.exchangeRate.history[state.worldTrade.exchangeRate.history.Count - 1];
            float move2 = (mid.rate - before2) / before2 * 100f;
            float sum2 = mid.tradeFlowEffect + mid.capitalFlowEffect +
                mid.expectationEffect + mid.interventionEffect + mid.anchorPullEffect;
            Assert.AreEqual(move2, sum2, .03f, "中性流下分解仍须守恒。");
            Assert.Less(System.Math.Abs(move2), .30f, "无冲击日汇率变动应保持温和。");
        }

        [Test]
        public void RateNeverExitsBounds_UnderExtremeFlowSequences()
        {
            for (int i = 0; i < 90; i++)
            {
                bool surplusPhase = i < 45;
                PushFlow(surplusPhase ? 99999999f : 0f, surplusPhase ? 0f : 99999999f);
                NextDay();
                float rate = state.worldTrade.exchangeRate.rate;
                Assert.IsTrue(float.IsFinite(rate), "第 " + i + " 天汇率出现 NaN/Inf。");
                Assert.GreaterOrEqual(rate, ExchangeRateService.MinRate - .0001f);
                Assert.LessOrEqual(rate, ExchangeRateService.MaxRate + .0001f);
            }
            Assert.GreaterOrEqual(state.centralBank.forexReservesBCurrency, 800f - .0001f,
                "浮动制下外储不得被汇率形成消耗。");
        }

        [Test]
        public void AnchorPull_DriftsBackTowardBase_WhenFlowsBalanced()
        {
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            fx.rate = 8f;
            fx.equilibriumRate = 8f;
            for (int i = 0; i < 40; i++)
            {
                PushFlow(880f, 880f);   // CA=0,只剩锚回归+利差+噪声
                NextDay();
            }
            Assert.Less(fx.equilibriumRate, 7.97f, "持续偏离锚时必须被温和拉回。");
            Assert.Greater(fx.equilibriumRate, ExchangeRateService.MinRate + .01f, "回归不得击穿区间。");
        }

        [Test]
        public void FloatingRegime_InterventionComponentStaysZero()
        {
            for (int i = 0; i < 10; i++)
            {
                PushFlow(i % 2 == 0 ? 500f : 1500f, i % 2 == 0 ? 1500f : 500f);
                NextDay();
                var entries = state.worldTrade.exchangeRate.history;
                Assert.AreEqual(0f, entries[entries.Count - 1].interventionEffect, .0001f,
                    "浮动制不得产生官方干预分量。");
            }
            Assert.AreEqual(0f, state.centralBank.cumulativeInterventionBCurrency, .0001f);
            Assert.AreEqual(0, state.centralBank.interventionDays);
        }

        [Test]
        public void Depreciation_RaisesIntermediateUnitCost_WithinClampedFactor()
        {
            // 贬值端:rate 8.4 → 因子≈1.107 → 单位成本 > 锚定态。
            state.worldTrade.exchangeRate.rate = ExchangeRateService.MaxRate;
            state.worldTrade.exchangeRate.equilibriumRate = ExchangeRateService.MaxRate;
            float costDepreciated = CostService.EffectiveInputUnitCost(state);
            float factorHigh = InternationalPriceService.IntermediateImportFactor(state);

            // 升值端:rate 5.6 → 因子钳到 0.9 下限。
            state.worldTrade.exchangeRate.rate = ExchangeRateService.MinRate;
            state.worldTrade.exchangeRate.equilibriumRate = ExchangeRateService.MinRate;
            float costAppreciated = CostService.EffectiveInputUnitCost(state);
            float factorLow = InternationalPriceService.IntermediateImportFactor(state);

            Assert.That(factorHigh, Is.InRange(.9f, 1.3f));
            Assert.That(factorLow, Is.InRange(.9f, 1.3f));
            Assert.Greater(costDepreciated, costAppreciated + .05f, "本币贬值必须抬升含进口份额的单位成本。");
            Assert.AreEqual(Eco.MaterialUnitCost * factorHigh, costDepreciated, .01f,
                "成本穿透只经由 IntermediateImportFactor 一个因子。");
            Assert.Greater(ExchangeRateService.ImportPressureIndex(state), 79.9f,
                "升值端进口价格指数应低于锚。");
        }

        [Test]
        public void AdvancePeriod_RecordsFiniteSignedFxInflationContribution()
        {
            // 复制金值测试的最小可行环境后把汇率推到贬值端再结算一期。
            state.government = new GovernmentState();
            state.resident = new ResidentState();
            state.macro = new MacroEconomyState();
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            InternationalTradeService.Ensure(state);
            WorldClock.SyncDerivedPeriods(state);
            state.worldTrade.exchangeRate.rate = ExchangeRateService.MaxRate;
            state.worldTrade.exchangeRate.equilibriumRate = ExchangeRateService.MaxRate;

            MacroEconomySimulator.AdvancePeriod(state);
            MacroEconomyState m = state.macro;
            Assert.IsTrue(float.IsFinite(m.exchangeRateInflationContribution), "贡献必须是有限值。");
            Assert.Greater(m.exchangeRateInflationContribution, .0001f, "贬值必须给出正向输入型通胀贡献。");
            Assert.LessOrEqual(m.exchangeRateInflationContribution, 1.5f, "贡献不得超出教学钳制带。");
            Assert.AreEqual(ExchangeRateService.MaxRate, m.exchangeRateSnapshot, .0001f,
                "宏观快照必须取自唯一汇率权威。");
            MacroPeriodRecord record = m.history[m.history.Count - 1];
            Assert.AreEqual(m.exchangeRateInflationContribution, record.exchangeRateContributionPp, .0001f,
                "周期记录必须快照通胀贡献(新闻/面板的数据源)。");
        }
    }
}

#endif
