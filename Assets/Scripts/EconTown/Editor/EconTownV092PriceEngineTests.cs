#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.2《房价是一头兽》(PRD §194–195):房价/租金引擎、CPI 住房分项接管(单一写点)、
    /// 财富效应单闸门、钉利率传导链五环、中性世界校准(R15–R17 落地形态)。
    /// bp 校准断言已在 V0.9.3 落定(R23):锚点由 [10,40] 修订为「全程 &lt;40 不进 Inflating」——
    /// 三指标基准以下读 0,mgdpZ 玩家口径常态≈0,见 §196.2 as-built。</summary>
    public class EconTownV092PriceEngineTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V092State");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260829);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            state.government.cash = 100000f;
            state.money = 100000f;
            state.materials = 4000;
            ResidentEconomy.CreateProfile(state, "房价镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            FinancialSystemService.Ensure(state);
            HousingService.Ensure(state);
            LongTermExperienceService.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        /// <summary>跑期样板(照 EconTownV07LongRunTests.SettleOneDay:营业演出在编辑器内等价
        /// 开门→跳过,再统一收日)。链测试全程用它保证两世界除钉利率外完全同构。</summary>
        bool SettleOneDay(GameState target, List<string> failures)
        {
            if (target.government.currentEvent != null && !target.government.currentEvent.resolved)
                GovernmentSimulator.ResolveEvent(target, 1);
            int settlingDay = target.day;
            target.price = 10f + (settlingDay % 7);
            if (target.CanMaterials && target.materials < 400 && target.money > 3000f)
            {
                float unit = DaySimulator.MaterialUnitPrice(target);
                target.money -= 200 * unit;
                target.materials += 200;
            }
            var demand = DaySimulator.RollDemand(target);
            int produced = DaySimulator.Production(target);
            DaySimulator.Settle(target, demand, produced);
            target.phase = GamePhase.Morning;
            if (!WorldSettlementService.TrySettleAndAdvance(target, out int _, out string blocked))
            {
                failures.Add("Day " + settlingDay + " blocked: " + blocked);
                return false;
            }
            return true;
        }

        void RunPeriods(GameState target, int periods)
        {
            var failures = new List<string>();
            for (int i = 0; i < periods; i++)
                Assert.IsTrue(SettleOneDay(target, failures), "第 " + i + " 期收日失败: " +
                    (failures.Count > 0 ? failures[failures.Count - 1] : "未知"));
        }

        void SeedPriceHistory(float fromIndex, float toIndex, int count)
        {
            state.housing.history.Clear();
            for (int i = 0; i < count; i++)
                state.housing.history.Add(new HousingPeriodRecord
                {
                    period = i,
                    priceIndex = Mathf.Lerp(fromIndex, toIndex, count == 1 ? 0f : i / (float)(count - 1))
                });
        }

        // ———————————————— 传导链:钉利率双世界(§194.5) ————————————————

        struct ChainSnapshot
        {
            public float loanRate, power, priceIndex, cpiHousing, confidence;
            public float standardPower, wealthContribution;
        }

        /// <summary>构建钉政策利率世界并跑 52 期:玩家先买房(财富效应经持房闸门,§195.1),
        /// 储蓄垫足 40000 消除 52 期断供噪声(焦点=利率传导,非供款压力)。顺序单世界,避免单例互扰。</summary>
        ChainSnapshot RunPinnedBoughtWorld(float policyRate)
        {
            GameObject go = new GameObject("V092Chain" + policyRate.ToString("0"));
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260829);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "房价镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            string msg;
            Assert.IsTrue(InterestRateService.TrySetPolicyRate(s, policyRate, out msg, true), msg);
            s.resident.savings = 40000f;
            // 存款镜像准备金按差额计(初始银行已有存款与等额资产;全额加会凭空多垫,Day1 即 gap 断言)
            s.bank.reserves += 40000f - s.bank.householdDeposits;
            s.bank.householdDeposits = 40000f;
            // 玩家=高薪边际买家(dailyIncome 每期管线内持续生效):P 落在活跃区 [0.7,1.4],
            // 利率→供款→P 的传导不被钳位遮蔽;todayIncome 只为买房授信那一刻的 DSR 检查。
            s.resident.dailyIncome = 600f;
            s.resident.todayIncome = 800f;
            Assert.IsTrue(MortgageService.TryBuyHouse(s, out msg), msg);

            RunPeriods(s, 52);
            ChainSnapshot snapshot = new ChainSnapshot
            {
                loanRate = s.bank.householdLoanRate,
                power = HousingService.PurchasingPower(s),
                priceIndex = s.housing.townHousePrice / HousingService.BaseHousePrice * 100f,
                cpiHousing = s.macro.housingPriceIndex,
                confidence = s.macro.consumerConfidence,
                standardPower = 0.35f * 600f * 52f / (MortgageService.AnnuityPayment(
                    HousingService.BaseHousePrice * (1f - HousingService.DownPaymentRatio),
                    s.bank.householdLoanRate + MortgageService.RateSpreadPp,
                    MortgageService.TermPeriods) * 52f),
                wealthContribution = HousingService.ConfidenceContribution(s)
            };
            Object.DestroyImmediate(go);
            GameState.I = state;
            return snapshot;
        }

        [Test]
        public void Calibration_NeutralWorld_FlatPrice_LowBubble()
        {
            RunPeriods(state, 52);
            // 52 期=1 整年:涨幅按整段首末指数算(AnnualPriceGrowth 是财富效应的 26 期窗口年化外推,
            // 只覆盖下半年、含 ×2 外推,不能当全年涨幅用)。
            float firstIndex = state.housing.history[0].priceIndex;
            float lastIndex = state.housing.history[state.housing.history.Count - 1].priceIndex;
            float growth = lastIndex / firstIndex - 1f;
            Assert.Greater(growth, -0.04f, "中性世界年化房价涨幅下限 −4%(实测 " + growth.ToString("P1") + ")");
            Assert.Less(growth, 0.08f, "中性世界年化房价涨幅上限 +8%(实测 " + growth.ToString("P1") + ")");
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < state.housing.history.Count; i++)
            {
                HousingPeriodRecord record = state.housing.history[i];
                if (record.supply <= 0f) continue;
                sum += (record.demand - record.supply) / record.supply;
                count++;
            }
            float mean = count > 0 ? sum / count : 1f;
            Assert.Less(Mathf.Abs(mean), 0.05f, "供需失衡均值须居中(实测 " + mean.ToString("P2") + ")");

            // 泡沫压力全程 <40(V0.9.3 落定,R23 锚点修订:原 [10,40]——三指标在基准以下读 0,
            // 中性世界 bp≈0 是诚实读数,约束的实质是不进 Inflating 阈 60 且留裕量)
            float maxBp = 0f;
            for (int i = 0; i < state.housing.history.Count; i++)
                maxBp = Mathf.Max(maxBp, state.housing.history[i].bubblePressure);
            Assert.Less(maxBp, 40f, "中性世界泡沫压力全程 <40(实测峰值 " + maxBp.ToString("0") + ")");
        }

        [Test]
        public void Demand_ScaledByKappa_BaselineCentered()
        {
            state.macro.consumerConfidence = 50f;
            state.resident.todayIncome = 600f;
            float power = HousingService.PurchasingPower(state);
            float demand = HousingService.Demand(state);
            float expected = HousingService.DemandScaleKappa * 320 * 25f * power *
                HousingService.CreditAvailability(state) * (0.9f + 50f / 500f);
            Assert.AreEqual(expected, demand, .01f, "D 与公式逐项一致(κ·人口·工资·P·信贷·信心)。");

            // P 钳位(R21):低收入玩家是边际买家的下限,高收入是上限——单人波动不主导全镇需求
            state.resident.todayIncome = 0f;
            Assert.AreEqual(HousingService.PowerClampMin, HousingService.PurchasingPower(state), .01f, "收入过低:P 钳下限。");
            state.resident.todayIncome = 5000f;
            Assert.AreEqual(HousingService.PowerClampMax, HousingService.PurchasingPower(state), .01f, "收入过高:P 钳上限。");
            // 基线居中(权威口径)由 Calibration 52 期实测断言,不在此处用伪造收入重复。
        }

        [Test]
        public void Chain_PinnedRate_MortgageRateSameDirection()
        {
            ChainSnapshot low = RunPinnedBoughtWorld(1f);
            ChainSnapshot high = RunPinnedBoughtWorld(4f);
            Assert.Greater(high.loanRate, low.loanRate, "政策利率↑→居民贷款利率(按揭基准)同向上行。");
        }

        [Test]
        public void Chain_PinnedRate_PurchasingPowerInverse()
        {
            ChainSnapshot low = RunPinnedBoughtWorld(1f);
            ChainSnapshot high = RunPinnedBoughtWorld(4f);
            // P 的利率环按「固定收入 600/期+固定基期房价」标准化(standardPower):只让按揭利率变,
            // 隔离出 利率↑→供款↑→P↓ 的纯传导。玩家真实收入(~150/期)使 P 常态钳在 R21 下限,
            // 且当期房价本身随利率反馈(低利率→房价涨→本金大→供款也涨),直接断当期供款会混入价格反馈。
            Assert.Less(high.standardPower, low.standardPower,
                "政策利率↑→按揭利率↑→标准化购买力 P 反向走低(供款更重)。");
            Assert.AreEqual(HousingService.PowerClampMin, low.power, .01f);
            Assert.AreEqual(HousingService.PowerClampMin, high.power, .01f);
        }

        [Test]
        public void Chain_PinnedRate_HousePriceDirectional()
        {
            ChainSnapshot low = RunPinnedBoughtWorld(1f);
            ChainSnapshot high = RunPinnedBoughtWorld(4f);
            Assert.Less(high.priceIndex, low.priceIndex, "政策利率↑→需求去杠杆→房价涨幅反向收窄。");
        }

        [Test]
        public void Chain_PinnedRate_CpiHousingContribution()
        {
            ChainSnapshot low = RunPinnedBoughtWorld(1f);
            ChainSnapshot high = RunPinnedBoughtWorld(4f);
            Assert.Less(high.cpiHousing, low.cpiHousing, "CPI 住房分项随房价同向(单一权威接管后)。");
        }

        [Test]
        public void Chain_PinnedRate_ConfidenceSameDirection()
        {
            ChainSnapshot low = RunPinnedBoughtWorld(1f);
            ChainSnapshot high = RunPinnedBoughtWorld(4f);
            // 断在闸门输出(ConfidenceContribution)而非信心总量:总量还叠加油价/汇率/通胀等其它通道
            // ——低利率世界可因贬值通胀把总量信心压低,这不否定财富效应环本身(教学点:多通道并存)。
            Assert.Greater(low.wealthContribution, high.wealthContribution,
                "持房世界经 §195 单一闸门:低利率→房价更高→财富效应贡献更大(与房价同向)。");
        }

        [Test]
        public void SingleWriter_CpiHousing_OnlyFromHousingService()
        {
            state.macro.housingPriceIndex = 150f;
            MacroEconomySimulator.AdvancePeriod(state);   // 只跑宏观期步(不含住房步)
            Assert.AreEqual(150f, state.macro.housingPriceIndex, .01f,
                "宏观演化不再是住房分项写点(旧 :209 行已删)。");

            HousingService.AdvancePeriod(state);          // 住房步=唯一写点
            HousingState h = state.housing;
            float mirror = h.townHousePrice / HousingService.BaseHousePrice * 100f;
            float expected = Mathf.Clamp(Mathf.Lerp(150f, 0.7f * h.rentIndex + 0.3f * mirror, 0.5f), 60f, 300f);
            Assert.AreEqual(expected, state.macro.housingPriceIndex, .01f,
                "住房步按 Lerp(旧,0.7×租金指数+0.3×房价镜像,0.5) 写入,钳 [60,300]。");
        }

        [Test]
        public void RentEngine_ConvergesToImplied_ClampPerPeriod()
        {
            float first = state.housing.periodRent;
            HousingService.AdvancePeriod(state);
            float implied = state.housing.townHousePrice / HousingService.ImpliedRentDivisor;
            Assert.Less(state.housing.periodRent, first, "基线租金 140 在隐含期租之上,首期向下收敛。");
            Assert.Greater(state.housing.periodRent, implied, "单步只走 15%,不一步到位。");

            float previous = state.housing.periodRent;
            for (int i = 0; i < 8; i++)
            {
                state.day++;                               // 修复轮回D:一日一期契约,直调需手动推进
                HousingService.AdvancePeriod(state);
                float step = state.housing.periodRent / previous - 1f;
                Assert.LessOrEqual(Mathf.Abs(step), HousingService.RentStepClamp + .001f,
                    "每期租金步长钳 ±2%(实测 " + step.ToString("P2") + ")");
                previous = state.housing.periodRent;
            }

            state.housing.townHousePrice = 84000f;   // 房价翻倍:隐含期租跳升远超单步上限
            state.day++;                               // 修复轮回D:一日一期契约
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(previous * 1.02f, state.housing.periodRent, .01f, "跳升目标下单步只涨 2%(钳位)。");
        }

        [Test]
        public void NewStarts_UpliftOnlyInflating_Capped()
        {
            Assert.AreEqual(0.5f, HousingService.StartsThisPeriod(state), .001f, "基线 0.5 套/期。");
            state.housing.bubblePressure = 30f;
            Assert.AreEqual(0.5f, HousingService.StartsThisPeriod(state), .001f, "泡沫压力不足 60 不上浮。");
            state.housing.bubblePressure = 80f;
            Assert.AreEqual(0.5f * 1.4f, HousingService.StartsThisPeriod(state), .001f,
                "bp≥60:0.5×(1+0.5×80/100)=0.7。");
            state.housing.bubblePressure = 200f;
            Assert.AreEqual(1f, HousingService.StartsThisPeriod(state), .001f, "开工上浮钳 1.0 套/期。");
        }

        [Test]
        public void WealthEffect_Homeowner_ContributionClamped()
        {
            state.housing.renting = false;
            state.housing.homeAppraisal = 42000f;   // 无贷:净值=估值
            state.resident.savings = 0f;            // 敞口占比=1
            SeedPriceHistory(100f, 110f, 27);       // 26 期 +10% → 年化 +20%
            float contribution = HousingService.ConfidenceContribution(state);
            Assert.Greater(contribution, 0f, "持房+房价上涨:财富效应为正。");
            Assert.AreEqual(8f * 0.2f * 1f, contribution, .01f, "公式=8×年化涨幅×敞口占比。");

            SeedPriceHistory(100f, 300f, 27);       // 26 期 +200% → 年化 +400%
            Assert.AreEqual(4f, HousingService.ConfidenceContribution(state), .01f, "贡献钳 +4 p.p.。");
        }

        [Test]
        public void WealthEffect_Renter_NegativeTerm_ConsumptionViaExistingLerp()
        {
            SeedPriceHistory(100f, 110f, 27);       // 年化 +20%,玩家默认租房
            Assert.AreEqual(-0.5f * 0.2f, HousingService.ConfidenceContribution(state), .01f,
                "无房租户:房价上涨是负担,反向小项 −0.5×涨幅。");

            state.macro.consumerConfidence = 80f;
            float high = MacroEconomySimulator.DemandFeedback(state);
            state.macro.consumerConfidence = 40f;
            float low = MacroEconomySimulator.DemandFeedback(state);
            Assert.Greater(high, low, "消费通道=既有信心 Lerp(.82,1.10),财富效应不开旁门。");
        }
    }
}

#endif
