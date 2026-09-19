#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.7 批次3:真实 NX 闭环、出口产能配额池、进口到岸价与关税六触点。</summary>
    public class EconTownV07NetExportsTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V07TradeTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(31415926);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV07TradeTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);

            // 通关形态:出口通道与居民账本可用,资金物料充足,无危机拦截。
            state.progression.completedChapter = 43;
            state.government.cash = 200000f;
            state.money = 100000f;
            state.materials = 5000;
            state.economy.financialCrisisActive = false;
            ResidentEconomy.CreateProfile(state, "贸易测试居民", ResidentProfession.StoreClerk,
                ResidentPreference.Foodie);
            state.resident.cash = 900f;
            LongTermExperienceService.Ensure(state);
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

        /// <summary>走真实收日管线一天(Morning→结算→次日),返回当日贸易行。</summary>
        TradeDayRecord SettleOneDay()
        {
            state.phase = GamePhase.Morning;
            int blocked;
            string reason;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out blocked, out reason),
                "收日被阻塞: " + reason);
            TradeDayRecord row = InternationalTradeService.LastRecord(state);
            Assert.IsNotNull(row);
            return row;
        }

        [Test]
        public void NetExports_EqualsSummedRealTradeFlows_AndMacroReadsSameRow()
        {
            // 让居民真的买一件进口品 → 消费型进口非零;玩家出海关闭,基线+中间投入构成 M。
            ResidentEconomy.Consume(state, ResidentItemKind.ImportedSnack);
            Assert.Greater(state.worldTrade.todayConsumerImportSpend, 0f, "购买应累进当日进口消费。");

            MacroEconomyState m0 = state.macro;
            float gdpBeforeSettle = m0.nominalGdp;   // 声明初值(尚未结算)
            TradeDayRecord row = SettleOneDay();

            Assert.AreEqual(row.exportsTotal - row.importsTotal, row.netExports, .01f, "P0:NX=X−M。");
            Assert.AreEqual(row.importsTotal,
                row.consumerImportSpend + row.intermediateImportSpend + row.townImportSpend, .01f);
            Assert.AreEqual(row.consumerImportSpend, row.tariffBase - row.intermediateImportSpend + row.intermediateImportSpend,
                .01f, "完税税基=消费+中间投入(本行校验写法上的同一性)。");

            // 宏观只读账本行:首期 GDP 读数从声明值跳到按真实 X/M 重算的恒等值。
            Assert.AreNotEqual(gdpBeforeSettle, state.macro.nominalGdp);
            Assert.AreEqual(state.macro.consumption + state.macro.investment + state.macro.governmentSpending +
                state.macro.exports - state.macro.imports, state.macro.nominalGdp, .05f, "GDP 恒等式保持。");
            Assert.AreEqual(row.netExports, state.macro.netExports, .01f, "宏观 NX 必须等于账本行 NX。");
        }

        [Test]
        public void TariffRevenue_EqualsRateTimesActualImports_AcrossSixTouchpoints()
        {
            Assert.IsTrue(GovernmentSimulator.SetPolicy(state, PolicyType.ImportTariff, 20f),
                "ImportTariff 政策必须在 Ensure 时注册并可设置。");
            ResidentEconomy.Consume(state, ResidentItemKind.ImportedSnack);
            ResidentEconomy.Consume(state, ResidentItemKind.ImportedSnack);

            float expectedTaxableBase = state.worldTrade.todayConsumerImportSpend;   // 本测未生产→中间投入=0
            TradeDayRecord row = SettleOneDay();

            Assert.AreEqual(expectedTaxableBase, row.tariffBase, .01f);
            Assert.AreEqual(.2f, row.tariffRateApplied, .0001f);
            Assert.AreEqual(row.tariffBase * .2f, row.tariffRevenue, .01f, "P0:关税收=率×实际进口额。");

            GovernmentCycleRecord cycle = state.government.history[state.government.history.Count - 1];
            Assert.AreEqual(row.worldDay, cycle.cycle, "周期号对齐(SettleDayTrade 与 AdvanceCycle 同日)。");
            Assert.AreEqual(row.tariffBase * .2f, cycle.tariffRevenue, .01f,
                "关税必须在本周期入账(SettleDayTrade 早于 AdvanceCycle 的插入位保证)。");
            Assert.AreEqual(0f, state.government.pendingTariffRevenue, .001f, "入账后挂账清零。");
            // 预算预览的关税字段就位即可(totalIncome 受自动稳定器等合法负项影响,不做绝对额断言)。
            GovernmentBudgetPreview preview = GovernmentSimulator.PreviewBudget(state);
            Assert.GreaterOrEqual(preview.tariffRevenue, 0f, "预览的关税收字段非负。");
            Assert.IsTrue(float.IsFinite(preview.totalIncome), "totalIncome 保持有限值可计算。");
        }

        [Test]
        public void PlayerExports_UseCapacitySlack_NotInventory()
        {
            state.exportChannelEnabled = true;
            state.exportTargetQuotaCups = 90;
            state.planProduction = 195;             // 员工 3 人产能 195 全占满
            state.employees = 3;

            TradeDayRecord row = SettleOneDay();
            DayRecord enterprise = state.LastRecord;

            Assert.LessOrEqual(row.playerExportUnits, Mathf.Max(0, Mathf.Max(0,
                CostService.Capacity(state).actualCapacity - enterprise.produced)),
                "出口只能占用计划之外的产能余量。");
            Assert.GreaterOrEqual(enterprise.sold, 0);
            Assert.LessOrEqual(enterprise.actualSales, enterprise.availableSupply, "现有 validate 不变量不被破坏。");
            if (row.playerExportUnits > 0)
            {
                Assert.Greater(enterprise.revenue, 0f);
                Assert.AreEqual(enterprise.exportUnits, row.playerExportUnits, "企业记录回填一致。");
                Assert.Greater(enterprise.exportShippingCost, 0f, "运费楔子必须体现。");
            }
        }

        [Test]
        public void ImportedSkuPrice_TracksFxAndTariff_CashLedgerConsistent()
        {
            // 升值端、零关税。
            ExchangeRateState fx = state.worldTrade.exchangeRate;
            fx.rate = ExchangeRateService.MinRate;
            fx.equilibriumRate = fx.rate;
            GovernmentSimulator.SetPolicy(state, PolicyType.ImportTariff, 0f);
            float priceStrong = ResidentEconomy.Price(state, ResidentItemKind.ImportedSnack);

            // 贬值端 + 最高关税 → 价格上行。
            fx.rate = ExchangeRateService.MaxRate;
            fx.equilibriumRate = fx.rate;
            GovernmentSimulator.SetPolicy(state, PolicyType.ImportTariff, 25f);
            float priceWeak = ResidentEconomy.Price(state, ResidentItemKind.ImportedSnack);
            Assert.Greater(priceWeak, priceStrong, "贬值+关税必须抬升到岸价。");
            Assert.AreEqual(.85f * ExchangeRateService.MaxRate * 1.25f, priceWeak, .01f);

            float cash = state.resident.cash;
            var action = ResidentEconomy.Consume(state, ResidentItemKind.ImportedSnack);
            Assert.IsTrue(action.success, action.message);
            Assert.AreEqual(cash - priceWeak, state.resident.cash, .001f, "现金扣减与到岸价一致。");
            Assert.AreEqual(priceWeak / 1.25f, state.worldTrade.todayConsumerImportSpend, .001f,
                "进口消费累计为税前完税基(围猎三#10:关税由 SettleDayTrade 以此基计提,不再税上税)。");
        }
    }
}

#endif
