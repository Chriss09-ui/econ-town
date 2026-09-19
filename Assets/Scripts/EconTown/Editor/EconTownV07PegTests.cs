#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.7 批次4:固定汇率守带干预、外储守恒、储备耗尽失守与再盯住门槛。</summary>
    public class EconTownV07PegTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07PegTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(88008800);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07PegTests_" + System.Guid.NewGuid().ToString("N"));
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

        void PushFlow(float exports, float imports)
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
        }

        [Test]
        public void FixedPeg_DefendsBand_ConsumingExactlyReportedIntervention()
        {
            // 可信度/外储达标 → 允许盯住;再显式改锚到 7 并把市价抬到带外,观察卖外储买本币。
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string on), on);
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            Assert.IsTrue(ExchangeRateService.TrySetPeg(state, 7f, out string pegMsg), pegMsg);
            fx.rate = 8f;
            fx.equilibriumRate = 8f;      // 与新锚 7 偏离 14% ⇒ 带外必须干预
            float consumedExpected = 0f;
            for (int i = 0; i < 6 && fx.regime == ExchangeRegime.Fixed; i++)
            {
                PushFlow(500f, 1600f);           // 持续逆差,贬值压力不减
                InternationalTradeService.CloseTradeDay(state);
                RateDayRecord last = fx.history[fx.history.Count - 1];
                if (Mathf.Abs(last.interventionEffect) > .001f)
                    consumedExpected += Mathf.Abs(state.centralBank.lastInterventionBCurrency);
                state.day++;
            }

            Assert.Greater(consumedExpected, 0f, "带外偏离应当产生干预消耗。");
            Assert.AreEqual(800f - consumedExpected, state.centralBank.forexReservesBCurrency, .10f,
                "外储消耗必须精确等于 Σ 报告的干预量(P0 对账)。");
            Assert.AreEqual(consumedExpected, state.centralBank.cumulativeInterventionBCurrency, .10f,
                "累计干预口径一致。");
        }

        [Test]
        public void ReserveLedger_NeverCreatesFreeMoney_AcrossFloatAndFixedMix()
        {
            // 浮动 5 天 + 固定 5 天混合制度跑批:对账恒等式在制度切换前后都必须成立。
            for (int i = 0; i < 5; i++) { PushFlow(i % 2 == 0 ? 400f : 1500f, 900f); CloseDay(); }
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string _), "达标后应可固定。");
            state.worldTrade.exchangeRate.equilibriumRate = 7.5f;
            for (int i = 0; i < 5; i++) { PushFlow(600f, 1300f); CloseDay(); }

            WorldTradeState trade = state.worldTrade;
            float ledgerSum = trade.trimmedReserveChangeBCurrency;   // 围猎三(#8):口径与生产对账一致
            foreach (TradeDayRecord row in trade.history) ledgerSum += row.reserveChangeBCurrency;
            Assert.AreEqual(800f + ledgerSum, state.centralBank.forexReservesBCurrency, .10f,
                "浮动/固定混合序列不得出现凭空增减的外储。");
        }

        [Test]
        public void ExhaustedReserves_ForciblyFloats_OneShotJumpToMarketPrice()
        {
            // 先在健康状态下进入固定制,再把外储抽到见底、缺口巨大——守汇不可持续的教学路径。
            CentralBankState central = state.centralBank;
            central.credibility = 80f;
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string on), on);
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            Assert.IsTrue(ExchangeRateService.TrySetPeg(state, 7f, out _), "锚定教学值。");
            central.forexReserveFloor = 300f;     // 中途的政策冲击:可动用余量只剩 ~20 B币
            central.forexReservesBCurrency = 320f;
            fx.rate = 8f;
            fx.equilibriumRate = 8f;

            int guard = 0;
            while (fx.regime == ExchangeRegime.Fixed && guard++ < 20)
            {
                PushFlow(200f, 2400f);
                InternationalTradeService.CloseTradeDay(state);
                state.day++;
            }
            Assert.AreEqual(ExchangeRegime.Floating, fx.regime, "弹药耗尽必须失守转浮动(P0)。");
            Assert.AreEqual(fx.equilibriumRate, fx.rate, .0001f, "失守=一步跳到市场出清价。");
            Assert.LessOrEqual(central.forexReservesBCurrency, central.forexReserveFloor + .01f);
            Assert.Less(central.credibility, 60f, "失守重创央行可信度(-25)。");
            Assert.AreEqual(MacroShockType.DevaluationSpike, state.macro.pendingShock,
                "失守要向宏观管线排队输入型通胀冲击。");
            Assert.AreNotEqual(-999, fx.lastAbandonPegWorldDay);
        }

        [Test]
        public void CapitalFlow_RespondsMonotonicallyToInterestDifferential()
        {
            WorldTradeState trade = state.worldTrade;
            CountryProfileState b = trade.countryB;
            b.policyRate = 4f;
            trade.capitalFlow.riskPremiumPercent = 1f;
            trade.capitalFlow.expectedDepreciationPercent = 0f;
            state.centralBank.credibility = 80f;

            float previousInflow = float.MinValue;
            int day = state.day;
            for (float policy = 2f; policy <= 8.01f; policy += 1f)
            {
                state.centralBank.policyRate = policy;
                // 直接走 posting:冻结当日行后比对。
                var method = typeof(InternationalTradeService);
                PushFlow(800f, 800f);
                ExchangeRateReflection.PostCapitalFlowForTest(state);
                float netInflow = trade.capitalFlow.inflowBCurrency - trade.capitalFlow.outflowBCurrency;
                Assert.GreaterOrEqual(netInflow, previousInflow - .0001f,
                    "利差上行应单调提升净流入(同风险溢价下)。");
                previousInflow = netInflow;
                day++;
            }
        }

        [Test]
        public void Repeg_BlockedByCredibilityCooldownAndReserves()
        {
            // 先制造一次失守(健康入场→抽干弹药→连续逆差)。
            state.centralBank.credibility = 80f;
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string enter), enter);
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            Assert.IsTrue(ExchangeRateService.TrySetPeg(state, 7f, out _));
            state.centralBank.forexReserveFloor = 300f;
            state.centralBank.forexReservesBCurrency = 310f;
            fx.rate = 8f; fx.equilibriumRate = 8f;
            int guard = 0;
            while (fx.regime == ExchangeRegime.Fixed && guard++ < 20) { PushFlow(100f, 2600f); CloseDay(); }
            Assert.AreEqual(ExchangeRegime.Floating, fx.regime);

            // 失守后:可信度不足 → 拒绝;可信度补足但外储/冷静期未过 → 拒绝;条件全齐 → 成功。
            bool lowCredibility = ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string m1);
            Assert.IsFalse(lowCredibility, "可信度 <60 时不得重新盯住。");
            StringAssert.Contains("可信度", m1);

            state.centralBank.credibility = 85f;
            bool lowReserves = ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string m2);
            Assert.IsFalse(lowReserves, "外储 <500 时不得重新盯住。");
            StringAssert.Contains("储备", m2);

            state.centralBank.forexReservesBCurrency = 800f;
            bool coolingDown = ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string m3);
            Assert.IsFalse(coolingDown, "失守后 7 天冷静期内不得重新盯住。");
            StringAssert.Contains("冷静期", m3);

            state.day += 8;
            bool repegged = ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out string m4);
            Assert.IsTrue(repegged, m4);
            Assert.AreEqual(ExchangeRegime.Fixed, state.worldTrade.exchangeRate.regime);
        }

        // —— 工具 ——

        void CloseDay()
        {
            InternationalTradeService.CloseTradeDay(state);
            state.day++;
        }
    }

    /// <summary>测试辅助:直接调用 posting(引擎内部为 private,用反射调用以保持生产 API 面干净)</summary>
    internal static class ExchangeRateReflection
    {
        public static void PostCapitalFlowForTest(GameState state)
        {
            var mi = typeof(ExchangeRateService).GetMethod("PostCapitalFlow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(mi, "ExchangeRateService.PostCapitalFlow 应存在。");
            mi.Invoke(null, new object[] { state, state.worldTrade });
        }
    }
}

#endif
