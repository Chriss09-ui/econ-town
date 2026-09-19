#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>暴露 UIManager 给 EditMode 冒烟(P0 ExposedUIManager 同款手法,另起名避重)。</summary>
    public class V093UiHarness : UIManager { }

    /// <summary>V0.9.3《杠杆周期》(PRD §196):信贷反馈环接授信、泡沫三指标与相位机、
    /// 确定性反转三触发、下行链、卖房约束、法拍下行。R23 as-built:三触发统一加 bp≥60 闸、
    /// Deflating 软着陆出口(bp&lt;20)、mgdpZ 玩家口径、估值每期跟价、法拍铭牌余晖。</summary>
    public class EconTownV093LeverageTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V093State");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260829);
            state.experience.settings.difficulty = DifficultyMode.Simulation;
            state.government.cash = 100000f;
            state.money = 100000f;
            state.materials = 4000;
            ResidentEconomy.CreateProfile(state, "杠杆镇民", ResidentProfession.StoreClerk,
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

        /// <summary>伪造宏观历史 26 期(policyRate/unemploymentRate 常值),供触发器的 26 期最低点比对。</summary>
        void SeedMacroHistory(float policyRate, float unemploymentRate)
        {
            state.macro.history.Clear();
            for (int i = 0; i < 26; i++)
                state.macro.history.Add(new MacroPeriodRecord { period = i, policyRate = policyRate, unemploymentRate = unemploymentRate });
        }

        /// <summary>把三指标顶到 bp≥60(高房价/低租金→prZ 高,陡峭 26 期历史→devZ 满)。</summary>
        void ForceBubbleIndicators()
        {
            state.housing.townHousePrice = HousingService.BaseHousePrice * 2f;  // 84000
            state.housing.periodRent = HousingService.BasePeriodRent;           // 140 原地→P/R=11.5
            SeedPriceHistory(100f, 130f, 27);                                   // 年化 +60%→devZ 钳 1
        }

        /// <summary>标准买房(存款 40000 差额镜像+收入 600 过 DSR):principal=42000×0.7=29400。</summary>
        void BuyStandardHouse(GameState s)
        {
            s.resident.savings = 40000f;
            s.bank.reserves += 40000f - s.bank.householdDeposits;
            s.bank.householdDeposits = 40000f;
            s.resident.todayIncome = 600f;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(s, out msg), msg);
        }

        // ———————————————— §196.1 信贷反馈环 ————————————————

        [Test]
        public void LtvEff_StressTightens_CollateralLoop()
        {
            state.bank.financialStress = 0f;
            Assert.AreEqual(0.7f, HousingService.LtvEff(state), .001f, "无压力:LTVeff=0.7(与首付三成口径一致)。");
            state.bank.financialStress = 75f;
            Assert.AreEqual(0.35f, HousingService.LtvEff(state), .001f, "压力 75:成数压到 0.35。");
            state.bank.financialStress = 300f;
            Assert.AreEqual(0.35f, HousingService.LtvEff(state), .001f, "压力远超:钳下限 0.35。");

            // 信贷可得性→需求去杠杆(§194.2 因子环内闭环)
            state.macro.consumerConfidence = 50f;
            state.resident.todayIncome = 600f;
            state.bank.financialStress = 0f;
            float demand0 = HousingService.Demand(state);
            state.bank.financialStress = 75f;
            float demand75 = HousingService.Demand(state);
            Assert.AreEqual(demand0 * 0.5f, demand75, .05f, "压力 75→credit=0.5→D 减半(惜贷去杠杆)。");
            Assert.Greater(demand0, demand75);

            // 授信门槛:高压力世界可贷额=房价×LtvEff(§196.1)
            state.bank.financialStress = 75f;
            BuyStandardHouse(state);
            LoanRecord loan = HousingService.CurrentMortgage(state);
            Assert.IsNotNull(loan);
            Assert.AreEqual(42000f * 0.35f, loan.principal, .01f, "可贷额=房价×LtvEff(0.35)。");
            Assert.AreEqual(0.35f, loan.ltvAtOrigination, .001f);
            Assert.AreEqual(42000f * 0.65f, state.housing.downPaymentPaid, .01f, "首付门槛相应抬到 65%。");
        }

        // ———————————————— §196.2 泡沫三指标与相位机 ————————————————

        [Test]
        public void BubblePressure_ThreeIndicators_Clamped()
        {
            // 基线世界:三指标全部居基准之下→bp=0
            Assert.AreEqual(0f, HousingService.BubblePressure(state), .01f, "中性世界 bp=0(远低于 Inflating 阈 60)。");

            // prZ:房价翻倍+租金原地→P/R=11.5→z=0.923;再夸张→钳 1;低于基准→钳 0
            ForceBubbleIndicators();
            Assert.AreEqual((84000f / (140f * 52f) - 6f) / 6f, HousingService.PriceRentZ(state), .001f);
            state.housing.townHousePrice = 200000f;
            state.housing.periodRent = 100f;
            Assert.AreEqual(1f, HousingService.PriceRentZ(state), .001f, "prZ 上钳 1。");
            state.housing.townHousePrice = 42000f;
            state.housing.periodRent = 140f;
            Assert.AreEqual(0f, HousingService.PriceRentZ(state), .001f, "P/R=5.77<6→prZ=0。");

            // mgdpZ:买入(余额 29400)后按公式精确驱动,基准 0.6、量程 0.4
            BuyStandardHouse(state);
            float outstanding = HousingService.MortgageOutstanding(state);
            Assert.AreEqual(29400f, outstanding, .01f);
            state.macro.nominalGdp = outstanding / (0.6f * 52f);   // 比值恰为基准→z=0
            Assert.AreEqual(0f, HousingService.MortgageGdpZ(state), .001f);
            state.macro.nominalGdp = outstanding / (0.9f * 52f);   // 比值 0.9→z=0.75
            Assert.AreEqual(0.75f, HousingService.MortgageGdpZ(state), .001f);
            state.macro.nominalGdp = outstanding / (1.5f * 52f);   // 远超→钳 1
            Assert.AreEqual(1f, HousingService.MortgageGdpZ(state), .001f);

            // devZ:26 期 +30%→年化 60%→(0.60−0.10)/0.15 钳 1;+10%(恰基准)→0
            SeedPriceHistory(100f, 130f, 27);
            Assert.AreEqual(1f, HousingService.DeviationZ(state), .001f);
            SeedPriceHistory(100f, 105f, 27);   // 26 期 +5%→年化 10%=基准
            Assert.AreEqual(0f, HousingService.DeviationZ(state), .02f);

            // 组合与写入:bp=100×(权重·prZ+权重·mgdpZ+权重·devZ),AdvancePeriod 把期前计算值写进状态
            ForceBubbleIndicators();
            float expected = 100f * (HousingService.BpWeightPr * HousingService.PriceRentZ(state) +
                HousingService.BpWeightMgdp * HousingService.MortgageGdpZ(state) +
                HousingService.BpWeightDev * HousingService.DeviationZ(state));
            Assert.AreEqual(expected, HousingService.BubblePressure(state), .01f);
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(expected, state.housing.bubblePressure, .01f,
                "AdvancePeriod 写入的 bp=期前状态的三指标计算值(记录与 StartsThisPeriod 消费同一值)。");
        }

        [Test]
        public void Phase_Inflating_At60_NewStartsUplift()
        {
            // 中性→None
            Assert.AreEqual(LeveragePhase.None, state.housing.leveragePhase);
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(LeveragePhase.None, state.housing.leveragePhase, "bp<60 保持 None。");

            // 顶到 bp≥60→Inflating,新开工上浮,峰值开始跟踪
            ForceBubbleIndicators();
            float bp = HousingService.BubblePressure(state);
            Assert.GreaterOrEqual(bp, 60f, "指标合力须把 bp 顶过 60(实测 " + bp.ToString("0") + ")");
            state.day++;                                   // 修复轮回D:一日一期契约,直调需手动推进
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(LeveragePhase.Inflating, state.housing.leveragePhase, "bp≥60 进 Inflating。");
            Assert.AreEqual(0.5f * (1f + 0.5f * state.housing.bubblePressure / 100f),
                state.housing.startsThisPeriod, .01f, "Inflating 新开工上浮。");
            float peak = state.housing.peakPriceIndex;
            Assert.Greater(peak, 100f, "峰值随价格上行跟踪。");

            // 回落→None,峰值软锚重置(相位机读的是房价步之前的指数:恰好=基期 100)
            state.housing.townHousePrice = HousingService.BaseHousePrice;
            state.housing.periodRent = HousingService.BasePeriodRent;
            SeedPriceHistory(100f, 100f, 27);
            state.day++;                                   // 修复轮回D:一日一期契约
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(LeveragePhase.None, state.housing.leveragePhase, "bp 回落退出 Inflating。");
            Assert.AreEqual(100f, state.housing.peakPriceIndex, .01f,
                "None 期峰值重置为期前指数(软锚)。");
        }

        // ———————————————— §196.3 确定性反转 ————————————————

        [Test]
        public void Reversal_Trigger_RateHike2pp_From26Low()
        {
            SeedMacroHistory(1f, 7f);
            state.macro.policyRate = 3.1f;                 // 较 26 期最低(1.0)抬升 2.1pp
            ForceBubbleIndicators();
            Assert.GreaterOrEqual(HousingService.BubblePressure(state), 60f);
            Assert.IsTrue(HousingService.ShouldQueueReversal(state), "加息 2.1pp+泡沫在场→触发。");

            // 无泡沫不反转(R23 统一闸)
            state.housing.townHousePrice = HousingService.BaseHousePrice;
            SeedPriceHistory(100f, 100f, 27);
            state.housing.bubblePressure = 0f;
            Assert.IsFalse(HousingService.ShouldQueueReversal(state), "bp<60:加息也不反转(无泡沫)。");

            // 全链:住房步入队→宏观步同日消费→Deflating+带数字新闻
            ForceBubbleIndicators();
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(MacroShockType.HousingReversal, state.macro.pendingShock, "住房步入队单槽冲击。");
            MacroEconomySimulator.AdvancePeriod(state);
            Assert.AreEqual(LeveragePhase.Deflating, state.housing.leveragePhase, "宏观步消费冲击钉入 Deflating。");
            StringAssert.Contains("回撤", state.macro.latestHeadline, "新闻行带当前回撤数字。");
        }

        [Test]
        public void Reversal_Trigger_Unemployment2pp()
        {
            SeedMacroHistory(3f, 5f);
            state.macro.policyRate = 3f;                   // 利率无抬升
            state.macro.unemploymentRate = 7.5f;           // 较 26 期最低(5.0)抬升 2.5p.p.
            ForceBubbleIndicators();
            Assert.IsTrue(HousingService.ShouldQueueReversal(state), "失业率抬升 2.5p.p.+泡沫在场→触发。");
            state.macro.unemploymentRate = 6.5f;
            Assert.IsFalse(HousingService.ShouldQueueReversal(state), "抬升不足 2p.p. 不触发。");
        }

        [Test]
        public void Reversal_Trigger_ExogenousShockWithBubble()
        {
            SeedMacroHistory(3f, 7f);
            state.macro.policyRate = 3f;
            state.macro.unemploymentRate = 7f;
            ForceBubbleIndicators();
            MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);   // 外生冲击已占槽
            Assert.IsTrue(HousingService.ShouldQueueReversal(state), "外生在队+bp≥60:落地即引爆。");
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(MacroShockType.HousingReversal, state.macro.pendingShock,
                "入队覆写外生冲击(单槽先例)——泡沫期任何冲击都变成引爆。");
        }

        [Test]
        public void Reversal_SingleSlot_OnlyOneQueued()
        {
            // 三条件同发:仍只入一次,且已是 Deflating 后不重复入队
            SeedMacroHistory(1f, 5f);
            state.macro.policyRate = 3.5f;
            state.macro.unemploymentRate = 8f;
            ForceBubbleIndicators();
            MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(MacroShockType.HousingReversal, state.macro.pendingShock, "多条件同发→单槽一枚。");
            MacroEconomySimulator.AdvancePeriod(state);
            Assert.AreEqual(LeveragePhase.Deflating, state.housing.leveragePhase);
            Assert.AreEqual(MacroShockType.None, state.macro.pendingShock, "消费后清槽。");

            state.day++;                                   // 修复轮回D:一日一期契约
            HousingService.AdvancePeriod(state);           // Deflating 期间条件仍在
            Assert.AreEqual(MacroShockType.None, state.macro.pendingShock, "已在 Deflating:不重复入队。");
        }

        // ———————————————— §196.4 下行链 ————————————————

        [Test]
        public void Deflating_DemandPenalty_PriceFalls()
        {
            state.macro.consumerConfidence = 50f;
            state.resident.todayIncome = 600f;
            state.housing.leveragePhase = LeveragePhase.None;
            float baseDemand = HousingService.Demand(state);
            state.housing.leveragePhase = LeveragePhase.Deflating;
            state.housing.bubblePressure = 80f;
            Assert.AreEqual(baseDemand * (1f - 0.8f * 0.05f), HousingService.Demand(state), .05f,
                "Deflating 需求罚=bp/100×5%(bp80→×0.96)。");

            // 行为:同指标泡沫世界,Deflating 期的房价步低于非 Deflating 期
            // (相位机每期重评——指标顶住 bp≥60,手动钉入的 Deflating 才待得住)
            ForceBubbleIndicators();
            state.housing.leveragePhase = LeveragePhase.None;
            HousingService.AdvancePeriod(state);           // 期前 None→机器判 Inflating,无罚
            float deltaNoPenalty = state.housing.history[state.housing.history.Count - 1].priceDelta;
            state.housing.leveragePhase = LeveragePhase.Deflating;
            state.day++;                                   // 修复轮回D:一日一期契约
            HousingService.AdvancePeriod(state);           // bp 仍高:Deflating 稳住,罚则生效
            Assert.AreEqual(LeveragePhase.Deflating, state.housing.leveragePhase,
                "bp≥60 时 Deflating 不触发软着陆出口。");
            float deltaPenalty = state.housing.history[state.housing.history.Count - 1].priceDelta;
            Assert.Less(deltaPenalty, deltaNoPenalty, "下行链:罚则使当期房价步更低。");
        }

        [Test]
        public void Deflating_WealthEffectAutoNegative()
        {
            SeedPriceHistory(110f, 100f, 27);              // 26 期 −9%→年化 ≈−18%
            float growth = HousingService.AnnualPriceGrowth(state);
            Assert.Less(growth, 0f);

            state.housing.renting = false;
            state.housing.homeAppraisal = 42000f;
            state.resident.savings = 0f;                   // 敞口占比=1
            float ownerContribution = HousingService.ConfidenceContribution(state);
            Assert.Less(ownerContribution, 0f, "持房+房价下跌:财富效应公式自动反号(负贡献)。");
            Assert.AreEqual(Mathf.Clamp(8f * growth, -4f, 4f), ownerContribution, .01f);

            state.housing.renting = true;                  // 无房租户:下跌是减负
            Assert.Greater(HousingService.ConfidenceContribution(state), 0f, "无房+下跌:−0.5×负涨幅=正。");
        }

        // ———————————————— §196.5 卖房约束 ————————————————

        [Test]
        public void SellHouse_Deflating_8PctDiscount_3PeriodInTransit_Conserved()
        {
            BuyStandardHouse(state);
            float savingsAtSell = state.resident.savings;   // 40000−12600=27400
            float townAtSell = state.bank.townSectorDeposits;   // 0+12600+29400=42000
            float balance = HousingService.CurrentMortgage(state).remainingPrincipal;
            Assert.AreEqual(29400f, balance, .01f);
            float gapBefore = state.bank.BalanceGap;

            state.housing.leveragePhase = LeveragePhase.Deflating;
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);
            float gross = 42000f * (1f - MortgageService.SellDiscount);
            float buyerMortgage = gross * HousingService.LtvEff(state);   // R23:买方按揭由银行创造
            Assert.AreEqual(gross, 38640f, .01f, "下跌期强制折价 8%。");
            Assert.AreEqual(Mathf.Max(0f, gross - balance), state.housing.pendingSaleProceeds, .01f,
                "在途净款=折价毛额−贷款余额(9240)。");
            Assert.AreEqual(MortgageService.SaleDelayPeriods, state.housing.pendingSaleEta, .01f, "延迟 3 期到账。");
            Assert.IsNull(HousingService.CurrentMortgage(state), "玩家贷款由买方款项即时销账。");
            Assert.IsTrue(state.housing.renting, "卖房后回租房。");
            Assert.AreEqual(savingsAtSell, state.resident.savings, .01f, "当期存款不动——钱在路上。");
            Assert.AreEqual(townAtSell + buyerMortgage - balance, state.bank.townSectorDeposits, .01f,
                "池子=原额+买方按揭(信用创造)−替玩家还贷的 29400。");
            Assert.AreEqual(gapBefore, state.bank.BalanceGap, .01f, "卖房当刻银行平衡表恒等。");

            // 3 期后到账:租金照付(140×3),净款入账,守恒闭环
            for (int i = 0; i < 3; i++) HousingService.SettleDay(state);
            Assert.AreEqual(0f, state.housing.pendingSaleProceeds, .01f);
            Assert.AreEqual(0f, state.housing.pendingSaleEta, .01f);
            float expectedSavings = savingsAtSell - 3f * state.housing.periodRent + (gross - balance);
            Assert.AreEqual(expectedSavings, state.resident.savings, .01f, "净款 9240 到账(扣 3 期租金)。");
            float expectedTown = townAtSell + buyerMortgage - balance + 3f * state.housing.periodRent - (gross - balance);
            Assert.AreEqual(expectedTown, state.bank.townSectorDeposits, .01f);
            Assert.AreEqual(gapBefore, state.bank.BalanceGap, .01f, "到账后银行平衡表恒等(守恒,不做假钱)。");
        }

        [Test]
        public void SellHouse_Inflating_FullImmediate()
        {
            BuyStandardHouse(state);
            float savingsAtSell = state.resident.savings;
            float householdAtSell = state.bank.householdDeposits;
            float balance = HousingService.CurrentMortgage(state).remainingPrincipal;

            state.housing.leveragePhase = LeveragePhase.Inflating;
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);
            Assert.AreEqual(0f, state.housing.pendingSaleEta, .01f, "全额即时:无在途。");
            Assert.AreEqual(savingsAtSell + (42000f - balance), state.resident.savings, .01f,
                "净款=估值−余额即时入账。");
            Assert.AreEqual(householdAtSell + (42000f - balance), state.bank.householdDeposits, .01f);
            Assert.IsNull(HousingService.CurrentMortgage(state));

            // R23:买方按揭入镇级房贷簿(信用创造);玩家口径负债不含它(borrowerId 归属)
            LoanRecord buyerLoan = null;
            for (int i = 0; i < state.bank.loans.Count; i++)
                if (state.bank.loans[i] != null && state.bank.loans[i].borrowerId == "镇买方")
                    buyerLoan = state.bank.loans[i];
            Assert.IsNotNull(buyerLoan, "镇买方按揭入账。");
            Assert.AreEqual(42000f * HousingService.LtvEff(state), buyerLoan.principal, .01f);
            Assert.AreEqual(buyerLoan.remainingPrincipal, HousingService.MortgageOutstanding(state), .01f,
                "镇级房贷簿=买方按揭(玩家贷已结清)。");
            Assert.AreEqual(0f, CreditService.ResidentDebt(state), .01f,
                "玩家口径负债不含镇买方按揭(borrowerId 归属过滤)。");
            StringAssert.Contains("即时还贷", msg, "文案讲清款项去向。");

            // 空池拒卖:变现需要接盘者(§196.5)——池子凑不出买方首付,下跌期卖不出
            BuyStandardHouse(state);                        // 再买一套(上一套已卖回租房)
            state.bank.townSectorDeposits = 0f;             // 池子被掏空(极端)
            state.housing.leveragePhase = LeveragePhase.Deflating;
            Assert.IsFalse(MortgageService.TrySellHouse(state, out string blocked), "买方池没钱→卖不出。");
            StringAssert.Contains("接盘", blocked);
        }

        // ———————————————— §196.4/§193.6 法拍下行 ————————————————

        [Test]
        public void ForeclosureLoss_HitsEquity_CapitalAdequacyUntouched()
        {
            BuyStandardHouse(state);
            // 估值跟随:房价跌→homeAppraisal 跟跌(§196.4 抵押物缩水)
            state.housing.townHousePrice = 30000f;
            HousingService.AdvancePeriod(state);
            Assert.AreNotEqual(42000f, state.housing.homeAppraisal, "估值不再钉在购入价。");
            Assert.AreEqual(state.housing.townHousePrice, state.housing.homeAppraisal, .01f,
                "自住房估值=当期镇房价(每期跟随)。");

            LoanRecord loan = HousingService.CurrentMortgage(state);
            float balance = loan.remainingPrincipal;
            float equityBefore = state.bank.equity;
            float loanLossBefore = state.bank.totalLoanLoss;
            float reservesBefore = state.bank.reserves;
            float appraisalAtForeclose = state.housing.homeAppraisal;   // Foreclose 内会清零估值

            MortgageService.Foreclose(state, loan);
            float recovery = Mathf.Min(appraisalAtForeclose * MortgageService.ForecloseRecoveryRate, balance);
            float loss = balance - recovery;
            Assert.Less(recovery, Mathf.Min(42000f * MortgageService.ForecloseRecoveryRate, balance),
                "跟跌后回收低于按购入价口径(抵押物缩水做进法拍)。");
            Assert.Greater(loss, 0f, "下跌期法拍产生真实坏账。");
            Assert.AreEqual(equityBefore - loss, state.bank.equity, .01f, "坏账冲资本。");
            Assert.AreEqual(loanLossBefore + loss, state.bank.totalLoanLoss, .01f);
            Assert.AreEqual(reservesBefore + recovery, state.bank.reserves, .01f);
            Assert.AreEqual(26, state.housing.foreclosureFlash, "法拍铭牌余晖亮 26 期(R23)。");

            // CapitalAdequacy 既有公式逐项复算一致(§196.4:公式不动)
            float assets = Mathf.Max(0f, state.bank.cash) + state.bank.reserves +
                state.bank.PerformingLoans + state.bank.GovernmentBonds;
            Assert.AreEqual(state.bank.equity / assets, state.bank.CapitalAdequacy, .0001f,
                "资本充足率=权益/总资产,既有公式未被本版触碰。");
        }

        // ———————————————— §196.6 地图要素护栏 ————————————————

        /// <summary>两处住房地图要素:①门侧三态铭牌挂家 root、贴墙面、无碰撞、离开门柱带
        /// (R8 禁例,照 V082CompoundTests:107 守卫形态);②新开工地块预建且默认隐藏,
        /// 带小碰撞(自动入 V081 LayoutGuard 全对净距护栏)。</summary>
        [Test]
        public void HousingMarkers_PlacementGuard()
        {
            ProgressionService.Ensure(state);
            GameObject mapObject = new GameObject("V093Map");
            MapBuilder.Build(mapObject.transform);
            try
            {
                Transform plaque = FindIn(MapBuilder.TownRoot, "plaque_rent");
                Assert.IsNotNull(plaque, "门侧铭牌应已建(三态子物体挂家 root)。");
                Assert.IsNull(plaque.GetComponent<Collider2D>(), "铭牌无碰撞(R8:不入 LayoutGuard 碰撞对)。");
                Transform home = plaque.parent;
                Vector3 door = home.position + BuildingSpecs.Get("home").doorOffset;
                Assert.Greater(Mathf.Abs(plaque.position.x - door.x), 0.7f, "铭牌离开门柱带(R8:不横越门位)。");
                Assert.Less(Mathf.Abs(plaque.position.y - door.y), 1.2f, "铭牌与门同带贴墙面。");
                Assert.IsNotNull(FindIn(MapBuilder.TownRoot, "plaque_own"));
                Assert.IsNotNull(FindIn(MapBuilder.TownRoot, "plaque_foreclose"), "法拍态铭牌在位。");

                Transform site = FindIn(MapBuilder.TownRoot, "housing_starts_site");
                Assert.IsNotNull(site, "新开工地块应已预建。");
                Assert.IsFalse(site.gameObject.activeSelf, "默认相位 None:地块隐藏。");
                Assert.IsNotNull(site.GetComponentInChildren<BoxCollider2D>(), "地块有小碰撞(入 LayoutGuard 护栏)。");

                MapBuilder.RefreshHousingMarkers(state);   // 默认 housing:租住 → 租住铭牌亮
                Assert.IsTrue(FindIn(MapBuilder.TownRoot, "plaque_rent").gameObject.activeSelf);
                Assert.IsFalse(FindIn(MapBuilder.TownRoot, "plaque_own").gameObject.activeSelf);
                Assert.IsFalse(FindIn(MapBuilder.TownRoot, "plaque_foreclose").gameObject.activeSelf);
                Assert.IsFalse(FindIn(MapBuilder.TownRoot, "housing_starts_site").gameObject.activeSelf,
                    "相位 None:新开工地块保持隐藏。");

                // Inflating 档全对护栏:地块点亮后其碰撞体须与全镇碰撞两两净距 ≥0.4 且不遮门
                state.housing.leveragePhase = LeveragePhase.Inflating;
                MapBuilder.RefreshHousingMarkers(state);
                Assert.IsTrue(FindIn(MapBuilder.TownRoot, "housing_starts_site").gameObject.activeSelf,
                    "相位 Inflating:新开工地块点亮。");
                EconTownV081LandmarkTests.AssertLayoutGuard("Inflating 档");
            }
            finally { Object.DestroyImmediate(mapObject); }
        }

        static Transform FindIn(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindIn(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ———————————————— 端到端补测(真实收日管线+真面板冒烟) ————————————————

        /// <summary>真实玩家旅程:买房→持房 26 期真实供款(SettleOneLoan 年金分支)→平静期卖房→
        /// 再买→断供 3 期法拍(missedPayments 真实路径),全程走 WorldSettlementService 收日管线,
        /// 每一腿断银行平衡表 gap 恒等;末尾真 UIManager 实例化家面板与宏观住房页签冒烟。</summary>
        [Test]
        public void EndToEnd_PlayerJourney_And_PanelSmoke()
        {
            // —— 买房(储蓄视作已攒——焦点在管线,不在攒钱过程)——
            state.resident.savings = 20000f;
            state.bank.reserves += 20000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 20000f;
            state.resident.todayIncome = 600f;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(state, out msg), msg);
            float gapAfterBuy = state.bank.BalanceGap;

            // —— 持房 26 期:供款/利息/房价引擎/宏观全走世界管线 ——
            RunPeriods(state, 26);
            LoanRecord loan = HousingService.CurrentMortgage(state);
            Assert.IsNotNull(loan, "持房中。");
            Assert.Greater(loan.totalPrincipalPaid, 0f, "等额本息摊还在真实发生。");
            Assert.AreEqual(gapAfterBuy, state.bank.BalanceGap, 1f, "持房期银行平衡表恒等。");

            // —— 平静期卖房(全额即时)——
            float savingsBefore = state.resident.savings;
            float balance = loan.remainingPrincipal;
            float appraisal = state.housing.homeAppraisal;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);
            Assert.AreEqual(savingsBefore + appraisal - balance, state.resident.savings, .01f, "全额即时净入。");
            Assert.AreEqual(gapAfterBuy, state.bank.BalanceGap, 1f, "卖房后平衡表恒等。");

            // —— 再买→断供 3 期→法拍(照 V091 Foreclose 样板:直接驱动供款结算;
            //    完整商店日会把收入垫回供款线,故本腿只跑收日不跑商店日)——
            state.resident.savings = 20000f;
            state.bank.reserves += 20000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 20000f;
            state.resident.todayIncome = 600f;
            Assert.IsTrue(MortgageService.TryBuyHouse(state, out msg), msg);
            float spend = state.resident.savings;   // 清空钱包(资产/负债同减,平衡保持)
            state.resident.savings = 0f;
            state.resident.cash = 0f;
            state.bank.householdDeposits -= spend;
            state.bank.reserves -= spend;
            var journeyFailures = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                CreditService.SettleLoanPayments(state);
                state.phase = GamePhase.Morning;
                Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out int _, out string blocked),
                    blocked);
            }
            Assert.IsTrue(state.housing.renting, "连续欠供法拍后回租房。");
            Assert.IsNull(HousingService.CurrentMortgage(state), "贷款已核销。");
            Assert.GreaterOrEqual(state.housing.foreclosureFlash, 20, "法拍铭牌余晖在亮。");
            Assert.AreEqual(gapAfterBuy, state.bank.BalanceGap, 1f, "法拍坏账冲权益后平衡表恒等。");

            // —— 真 UIManager 冒烟:家面板(法拍余晖态)+宏观住房页签(泡沫仪表卡)——
            GameObject uiGo = new GameObject("V093Ui");
            V093UiHarness ui = uiGo.AddComponent<V093UiHarness>();
            ui.InitializeForValidation();
            ui.ShowHousing();
            Transform housingPanel = FindIn(ui.transform, "panel_居民生活 · 我的住房");
            Assert.IsNotNull(housingPanel, "家面板构建成功。");
            Assert.IsTrue(AnyTextContains(housingPanel, "申请房贷购房"), "租房态含买房入口。");
            ui.ShowMacroDashboard(5);
            Transform macroPanel = FindIn(ui.transform, "panel_macro_dashboard");
            Assert.IsNotNull(macroPanel, "宏观面板构建成功。");
            Assert.IsTrue(AnyTextContains(macroPanel, "泡沫仪表"), "泡沫仪表卡在住房页签内。");
            Assert.IsNotNull(FindIn(macroPanel, "bpTrack"), "仪表轨道已建。");
            Assert.IsNotNull(FindIn(macroPanel, "bpFill"), "填充条已建。");
            Assert.IsTrue(AnyTextContains(macroPanel, "房价上涨 ≠ 真实财富"), "变现附注仍在页签内。");
            Object.DestroyImmediate(uiGo);
        }

        /// <summary>反转的完整收日管线落地:指标+加息种子就位后跑**一个真实游戏日**
        /// (WorldSettlementService:住房步入队→政府周期→宏观步同日消费),当期即 Deflating。</summary>
        [Test]
        public void EndToEnd_ReversalViaFullSettlementPipeline()
        {
            SeedMacroHistory(1f, 7f);
            state.macro.policyRate = 3.5f;                 // 较 26 期最低 +2.5pp
            ForceBubbleIndicators();
            var failures = new List<string>();
            Assert.IsTrue(SettleOneDay(state, failures), failures.Count > 0 ? failures[0] : "收日失败");
            Assert.AreEqual(LeveragePhase.Deflating, state.housing.leveragePhase,
                "完整收日管线当期反转落地(非仅引擎单步)。");
            Assert.AreEqual(LeveragePhase.Inflating,
                state.housing.history[state.housing.history.Count - 1].phase,
                "管线顺序即因果:入档相位=期前机器判定(Inflating),宏观步消费冲击后现值才钉入 Deflating。");
        }

        static bool AnyTextContains(Transform root, string needle)
        {
            UnityEngine.UI.Text t = root.GetComponent<UnityEngine.UI.Text>();
            if (t != null && t.text != null && t.text.Contains(needle)) return true;
            for (int i = 0; i < root.childCount; i++)
                if (AnyTextContains(root.GetChild(i), needle)) return true;
            return false;
        }

        // ———————————————— §196.1 确定性回放 ————————————————

        [Test]
        public void FeedbackLoop_104PeriodReplay_DeterministicNoRandom()
        {
            // 顺序单世界(照 V092 RunPinnedBoughtWorld 先例,避免单例互扰):A 跑完摘下历史再跑 B
            GameObject goA = new GameObject("replayA");
            GameState a = goA.AddComponent<GameState>();
            ReplayWorld(a);
            var historyA = new List<HousingPeriodRecord>(a.housing.history);
            Object.DestroyImmediate(goA);

            GameObject goB = new GameObject("replayB");
            GameState b = goB.AddComponent<GameState>();
            ReplayWorld(b);
            var historyB = b.housing.history;
            Object.DestroyImmediate(goB);
            GameState.I = state;

            Assert.AreEqual(104, historyA.Count);
            Assert.AreEqual(historyA.Count, historyB.Count);
            for (int i = 0; i < historyA.Count; i++)
            {
                Assert.AreEqual(historyA[i].priceIndex, historyB[i].priceIndex, .001f, "第 " + i + " 期房价指数分叉");
                Assert.AreEqual(historyA[i].rentIndex, historyB[i].rentIndex, .001f, "第 " + i + " 期租金指数分叉");
                Assert.AreEqual(historyA[i].bubblePressure, historyB[i].bubblePressure, .001f, "第 " + i + " 期泡沫压力分叉");
                Assert.AreEqual(historyA[i].phase, historyB[i].phase, "第 " + i + " 期杠杆相位分叉");
                Assert.AreEqual(historyA[i].stock, historyB[i].stock, .001f);
            }
        }

        void ReplayWorld(GameState s)
        {
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260829);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "杠杆镇民", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            RunPeriods(s, 104);
        }
    }
}

#endif
