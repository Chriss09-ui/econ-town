#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.7 批次1:世界贸易数据模型、存档往返与旧档复活。</summary>
    public class EconTownV07ModelTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07ModelTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            // 编辑器模式 AddComponent 不触发 Awake:必须显式走 Ensure 链(V0.6 测试同惯例),
            // 否则汇率首初始化不会发生、first-init 又会在读档时把合法值洗回锚。
            InternationalTradeService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07ModelTests_" + System.Guid.NewGuid().ToString("N"));
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

        [Test]
        public void FreshState_TradeDefaults_RateIsSeven_Floating_ReservesAre800()
        {
            Assert.AreEqual("V0.7.2", GameState.Version);

            Assert.IsTrue(state.worldTrade.exchangeRate.initialized);
            Assert.AreEqual(7f, state.worldTrade.exchangeRate.rate, .001f);
            Assert.AreEqual(ExchangeRegime.Floating, state.worldTrade.exchangeRate.regime);
            Assert.AreEqual(7f, ExchangeRateService.Rate(state), .001f);

            // 换算互逆是 P0 不变量:全项目只有一个汇率定义「1 B币 = 7 A币」。
            float yuan = ExchangeRateService.ConvertAToB(state, 140f);
            Assert.AreEqual(20f, yuan, .001f, "140 A币应按 7 折合为 20 B币。");
            Assert.AreEqual(140f, ExchangeRateService.ConvertBToA(state, yuan), .001f);

            Assert.AreEqual(800f, state.centralBank.forexReservesBCurrency, .001f);
            Assert.AreEqual(120f, state.centralBank.forexReserveFloor, .001f);
            Assert.AreEqual(40, state.exportTargetQuotaCups);
            Assert.IsFalse(state.exportChannelEnabled);
            Assert.AreEqual(LearningPart.Trade, (LearningPart)8, "枚举值不得插队:Trade 必须是 8。");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesCountryBProfileAndHistory()
        {
            state.worldTrade.countryB.tariffOnAExports = .14f;
            state.worldTrade.countryB.policyRate = 3.25f;
            state.worldTrade.capitalFlow.riskPremiumPercent = 2.5f;
            state.worldTrade.exchangeRate.rate = 7.4f;
            state.worldTrade.history.Add(new TradeDayRecord { worldDay = 9, playerExportUnits = 30, netExports = -120f });
            state.exportChannelEnabled = true;
            state.exportTargetQuotaCups = 66;

            SaveSystem.Save(state);
            state.worldTrade = new WorldTradeState();
            state.exportChannelEnabled = false;
            state.exportTargetQuotaCups = 1;

            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(.14f, state.worldTrade.countryB.tariffOnAExports, .001f);
            Assert.AreEqual(3.25f, state.worldTrade.countryB.policyRate, .001f);
            Assert.AreEqual(2.5f, state.worldTrade.capitalFlow.riskPremiumPercent, .001f);
            Assert.AreEqual(7.4f, state.worldTrade.exchangeRate.rate, .001f,
                "汇率是交易日快照,存档往返后不得被锚洗掉。");
            Assert.IsTrue(state.exportChannelEnabled);
            Assert.AreEqual(66, state.exportTargetQuotaCups);
            TradeDayRecord restored = state.worldTrade.history.Find(
                x => x.worldDay == 9 && Mathf.Abs(x.netExports + 120f) < .01f);
            Assert.IsNotNull(restored, "贸易日账本必须随主档整体往返。");
        }

        [Test]
        public void LegacyV06SaveWithoutWorldTrade_RevivesWithoutException()
        {
            // 模拟 V0.6 存档:JSON 里根本没有 worldTrade 键,还塞了一个越界汇率验证坏档钳制。
            File.WriteAllText(SaveSystem.SavePath,
                "{\"saveVersion\":\"V0.6\",\"day\":23,\"money\":5200,\"price\":15," +
                "\"worldTrade\":{\"initialized\":true,\"exchangeRate\":{\"rate\":99.5}}}");

            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(23, state.day);
            Assert.AreEqual(5200f, state.money, .001f);
            Assert.IsNotNull(state.worldTrade);
            Assert.IsNotNull(state.worldTrade.countryB);
            float rate = state.worldTrade.exchangeRate.rate;
            Assert.GreaterOrEqual(rate, ExchangeRateService.MinRate, "注入的越界汇率必须回到交易区间。");
            Assert.LessOrEqual(rate, ExchangeRateService.MaxRate, "注入的越界汇率必须回到交易区间。");
        }

        [Test]
        public void DroppedTodayRecord_ClearsPendingTariffAndCounter()
        {
            // 构造"存档写于当日结算之后"形态:企业历史末条就是今天——读档要丢弃重玩。
            state.history.Add(new DayRecord { day = state.day, shopOpen = true, sold = 10 });
            state.government.pendingTariffRevenue = 123f;
            state.government.lastTariffRecordedDay = state.day;

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.IsNull(state.LastRecord, "当日已结算记录应被丢弃以便重玩该日。");
            Assert.AreEqual(0f, state.government.pendingTariffRevenue, .001f,
                "挂账关税不清零会让重玩日按 2× 记进政府账本。");
            Assert.AreEqual(0, state.government.lastTariffRecordedDay, "同日防重标记一并复位。");
        }

        [Test]
        public void TradeRandomStream_ReproducibleUnderSameSeed()
        {
            state.EnsureEconomyState();
            DaySimulator.SetRandomSeed(777);
            float t1 = state.economy.tradeRandom.Next01();
            float t2 = state.economy.tradeRandom.Next01();

            DaySimulator.SetRandomSeed(777);
            Assert.AreEqual(t1, state.economy.tradeRandom.Next01(), .0001f,
                "同种子贸易流必须可复现(A/B 实验与 SimDriver 都依赖这一点)。");
            Assert.AreEqual(t2, state.economy.tradeRandom.Next01(), .0001f);

            DaySimulator.SetRandomSeed(778);
            float other = state.economy.tradeRandom.Next01();
            Assert.That(Mathf.Abs(t1 - other), Is.GreaterThan(.0001f),
                "不同种子应产生不同序列(异或偏移生效)。");

            // 独立流确保不污染共享流状态(calls 计数只属于各自流)。
            long sharedBefore = state.economy.random.calls;
            state.economy.tradeRandom.Next01();
            Assert.AreEqual(sharedBefore, state.economy.random.calls,
                "贸易流取数不得消耗共享流。");
        }
    }
}

#endif
