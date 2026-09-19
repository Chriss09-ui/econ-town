#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>暴露 UIManager 给 EditMode 冒烟(V093/V094 harness 同款手法,另起名避重)。</summary>
    public class V09FixUiHarness : UIManager { }

    /// <summary>找 BUG 修复轮回(V0.9 弧六维度围猎 11 实锤缺陷):A 脱钩根治/B 镇买方归属/C HomeEquity 口径/
    /// D 一日一期契约/E 付息分母归一/F 在途房款先结旧/G Finale 读档路由。逐批次随修复落地。</summary>
    public class EconTownV09BugfixTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;

        GameState NewWorld()
        {
            GameObject go = new GameObject("V09FixWorld" + worlds.Count);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260830);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "修复镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            state = NewWorld();
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        /// <summary>照 V095 BuyHouseForLab:储蓄垫足(差额镜像准备金,银行表零扰动)过首付与 DSR。</summary>
        void BuyStandardHouse(GameState s, float savings = 40000f)
        {
            s.resident.savings = savings;
            s.bank.reserves += savings - s.bank.householdDeposits;
            s.bank.householdDeposits = savings;
            s.resident.dailyIncome = 600f;
            s.resident.todayIncome = 800f;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(s, out msg), msg);
        }

        // ———————————————— A:savings/householdDeposits 脱钩根治 ————————————————

        [Test]
        public void ResidentSave_NowDoubleEntry_BankMirrorAndBalanceHold()
        {
            state.resident.cash = 6000f;
            float dep0 = state.bank.householdDeposits, res0 = state.bank.reserves;
            float gap0 = state.bank.BalanceGap;
            Assert.IsTrue(ResidentEconomy.Save(state, 2000f).success, "存钱动作成功。");
            Assert.AreEqual(state.resident.savings, state.bank.householdDeposits, .01f,
                "修复A:银行负债端同加——savings 是银行对居民的负债镜像,两账恒等。");
            Assert.AreEqual(res0 + 2000f, state.bank.reserves, .01f, "资产端(准备金)同加。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f, "双边入账银行平衡表零扰动。");

            // 反复存钱不再制造脱钩(旧单边写会让 savings 无限高于 householdDeposits)
            Assert.IsTrue(ResidentEconomy.Save(state, 1000f).success);
            Assert.AreEqual(state.resident.savings, state.bank.householdDeposits, .01f, "两连存后两账仍恒等。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void DivergentLegacySavings_AllDebitGuardsBlock_NeverNegativeDeposits()
        {
            // 模拟历史坏档:V0.5.1 单边 Save 的遗产——savings 涨到 20000,银行只入账 1000
            state.resident.savings = 20000f;
            state.bank.householdDeposits = 1000f;
            string msg;
            Assert.IsFalse(MortgageService.TryBuyHouse(state, out msg), "银行入账 1000 < 首付 12600:拒贷。");
            StringAssert.Contains("银行入账", msg, "拒贷文案讲清双口径。");
            Assert.AreEqual(1000f, state.bank.householdDeposits, .01f, "负债端原样,绝不记负。");

            // 租金:银行入账不足同样按欠租处理,不再双端扣穿
            state.bank.householdDeposits = 50f;
            state.housing.periodRent = 140f;
            HousingService.SettleDay(state);
            Assert.AreEqual(1, state.housing.rentMissed, "银行入账 50 < 租金 140:按欠租处理(修复A 守卫)。");
            Assert.AreEqual(50f, state.bank.householdDeposits, .01f, "负债端不再被扣穿。");
            Assert.AreEqual(20000f, state.resident.savings, .01f, "储蓄原样(欠租不扣)。");

            // 提前还清:健康买入后单边抬高储蓄/打薄入账,结清被拒
            BuyStandardHouse(state);
            state.resident.savings = 40000f;                // 模拟单边涨(银行入账仍薄)
            state.bank.householdDeposits = 100f;
            Assert.IsFalse(MortgageService.TryEarlyPayoff(state, out msg), "银行入账 100 < 余额:拒结清。");
            StringAssert.Contains("银行入账", msg);
            Assert.AreEqual(100f, state.bank.householdDeposits, .01f);
        }

        [Test]
        public void NegativeDepositLegacySave_HealedByEnsure_EquityRecomputed()
        {
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f, "起点平衡。");
            state.bank.householdDeposits = -500f;           // 历史坏档:负债端已被扣穿
            Assert.AreNotEqual(0f, state.bank.BalanceGap, "钳位截断负债端,gap 漂移(缺陷现场)。");
            InterestRateService.Ensure(state);
            Assert.AreEqual(0f, state.bank.householdDeposits, .01f, "自愈:负负债归零。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f, "按当前表重算权益后 BalanceGap 归零。");
            Assert.AreEqual(state.bank.TotalAssets - state.bank.TotalLiabilities, state.bank.equity, .01f,
                "权益=资产−负债(与首初始化同口径)。");
        }

        // ———————————————— B:镇买方按揭归属分流 ————————————————

        [Test]
        public void TownBuyerMortgage_SettlesFromTownPool_PlayerLedgerUntouched()
        {
            BuyStandardHouse(state);
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);   // None 相位:全额即时
            LoanRecord buyerLoan = state.bank.loans.Find(
                l => l != null && l.borrowerId == MortgageService.TownBuyerId);
            Assert.IsNotNull(buyerLoan, "卖房产生镇买方按揭。");
            Assert.AreEqual(LoanStatus.Current, buyerLoan.status);

            float sav0 = state.resident.savings, cash0 = state.resident.cash;
            float intExp0 = state.resident.interestExpense, town0 = state.bank.townSectorDeposits;
            float principal0 = buyerLoan.remainingPrincipal;
            float gap0 = state.bank.BalanceGap;
            CreditService.SettleLoanPayments(state);
            float interestDue = principal0 * InterestRateService.PeriodRate(buyerLoan.annualInterestRate);
            float expectedPayment = interestDue + MortgageService.AnnuityPrincipalDue(buyerLoan, interestDue);
            Assert.AreEqual(town0 - expectedPayment, state.bank.townSectorDeposits, .01f,
                "月供从镇住户部门存款池划扣。");
            Assert.AreEqual(sav0, state.resident.savings, .01f, "修复B:玩家储蓄零污染。");
            Assert.AreEqual(cash0, state.resident.cash, .01f, "现金零污染。");
            Assert.AreEqual(intExp0, state.resident.interestExpense, .01f, "利息不再进玩家费用口径。");
            Assert.Less(buyerLoan.remainingPrincipal, principal0, "镇级房贷簿正常摊还。");
            Assert.AreEqual(0, buyerLoan.missedPayments);
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f, "镇级结算四腿守恒。");
        }

        [Test]
        public void TownBuyerDefault_TownForeclosure_PlayerHousingUntouched()
        {
            BuyStandardHouse(state);
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);
            LoanRecord buyerLoan = state.bank.loans.Find(
                l => l != null && l.borrowerId == MortgageService.TownBuyerId);
            buyerLoan.collateralValue = 20000f;               // 压低抵押:制造真实坏账腿
            state.bank.townSectorDeposits = 0f;               // 镇池掏空:买方连续断供 3 期

            float equity0 = state.bank.equity, reserves0 = state.bank.reserves;
            float lossBefore = state.bank.totalLoanLoss;
            float gap0 = state.bank.BalanceGap;
            float happy0 = state.resident.happiness;
            float balance = buyerLoan.remainingPrincipal;
            for (int i = 0; i < 3; i++) CreditService.SettleLoanPayments(state);

            Assert.AreEqual(LoanStatus.Defaulted, buyerLoan.status, "镇级核销。");
            Assert.AreEqual(0f, state.housing.foreclosureFlash, "修复B:玩家不吃法拍铭牌。");
            Assert.AreEqual(happy0, state.resident.happiness, .01f, "玩家幸福零惩罚。");
            Assert.IsTrue(state.housing.renting, "玩家仍是租房态(卖房后),未被误清。");
            Assert.AreEqual("", state.housing.mortgageLoanId);
            float recovery = Mathf.Min(20000f * MortgageService.ForecloseRecoveryRate, balance);
            Assert.AreEqual(reserves0 + recovery, state.bank.reserves, .01f,
                "回收=min(放款抵押估值×85%,余额)。");
            Assert.AreEqual(lossBefore + (balance - recovery), state.bank.totalLoanLoss, .01f, "差额冲坏账。");
            Assert.AreEqual(equity0 - (balance - recovery), state.bank.equity, .01f);
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f, "镇级法拍四腿守恒。");
        }

        // ———————————————— C:HomeEquity 玩家口径 ————————————————

        [Test]
        public void HomeEquity_PlayerScope_SellThenRebuy_NotZeroedByTownLoan()
        {
            BuyStandardHouse(state);
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);   // 镇买方按揭入镇级房贷簿
            BuyStandardHouse(state, 40000f);                                    // 再买:贷款簿现有两笔按揭
            LoanRecord own = HousingService.CurrentMortgage(state);
            Assert.IsNotNull(own);
            Assert.AreEqual(state.housing.homeAppraisal - own.remainingPrincipal,
                HousingService.HomeEquity(state), .01f,
                "修复C:净值=估值−玩家在贷余额,不被镇买方按揭压成 0。");
            Assert.Greater(HousingService.HomeEquity(state), 0f, "付过首付的玩家净值必为正。");
            // 镇级口径不受影响(泡沫三指标/宏观面板仍读全镇房贷簿)
            Assert.Greater(HousingService.MortgageOutstanding(state), own.remainingPrincipal,
                "镇级房贷簿仍含镇买方按揭。");
        }

        // ———————————————— D:一日一期契约(读档重放日不双步) ————————————————

        [Test]
        public void OnePeriodPerDayContract_ReplayDayDoesNotDoubleStepOrDoubleCharge()
        {
            float price0 = state.housing.townHousePrice;
            HousingService.AdvancePeriod(state);
            int count1 = state.housing.history.Count;
            Assert.AreEqual(state.day, state.housing.history[count1 - 1].period, "期账记本日。");

            // 读档重放日(收日链中段存档→读档→重玩同一天):房价重置回档点后链再跑——
            // 期步与日结(租金)双双冻结,不双步、不双扣
            state.housing.townHousePrice = price0;
            float sav0 = state.resident.savings;
            HousingService.AdvancePeriod(state);
            HousingService.SettleDay(state);
            Assert.AreEqual(count1, state.housing.history.Count, "修复D:同日期账幂等,不产生重复记录。");
            Assert.AreEqual(price0, state.housing.townHousePrice, .01f, "重放日房价不双步。");
            Assert.AreEqual(sav0, state.resident.savings, .01f, "重放日租金不双扣。");

            state.day++;                                   // 真实管线由 AdvanceDay 推进
            HousingService.AdvancePeriod(state);
            Assert.AreEqual(count1 + 1, state.housing.history.Count, "次日正常入档。");
        }

        // ———————————————— E:国债付息分母归一 ————————————————

        [Test]
        public void GovernmentBondInterest_SaturatedDebt_ResidentShareProportional()
        {
            // 银行持券 4000(资产互换:准备金→国债,平衡表不动)+ 居民认购 3000(四腿)
            Assert.AreEqual(4000f, CreditService.PurchaseGovernmentBonds(state, 4000f, 4f), .01f);
            state.resident.savings = 20000f;
            state.bank.reserves += 20000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 20000f;
            string msg;
            Assert.IsTrue(AssetAllocationService.TryBuyTreasury(state, 3000f, out msg), msg);

            // 政府盈余把 debt 压到 3800 < 持券总额 7000:旧钳法 share=4000/3800→饱和 1,居民份额被钳 0
            float sav0 = state.resident.savings, dep0 = state.bank.householdDeposits;
            float eq0 = state.bank.equity, res0 = state.bank.reserves;
            float gap0 = state.bank.BalanceGap;
            CreditService.ReceiveGovernmentBondInterest(state, 100f, 3800f);
            Assert.AreEqual(sav0 + 100f * 3000f / 7000f, state.resident.savings, .01f,
                "修复E:居民按持券占比 3/7 分息,不再被饱和钳 0。");
            Assert.AreEqual(dep0 + 100f * 3000f / 7000f, state.bank.householdDeposits, .01f, "负债端同加。");
            Assert.AreEqual(eq0 + 100f * 4000f / 7000f, state.bank.equity, .01f, "银行份额 4/7 照旧。");
            Assert.AreEqual(res0 + 100f, state.bank.reserves, .01f, "付息人是政府,对方科目=准备金。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f, "付息拆分银行平衡表零扰动。");
        }

        // ———————————————— F:在途房款先结旧再写新 ————————————————

        [Test]
        public void SellTwiceInDeflating_FirstProceedsSettled_NotVanished()
        {
            BuyStandardHouse(state);
            state.housing.leveragePhase = LeveragePhase.Deflating;
            string msg;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);   // 第一笔在途(净款 9240)
            float net1 = state.housing.pendingSaleProceeds;
            Assert.Greater(state.housing.pendingSaleEta, 0f);

            BuyStandardHouse(state, 40000f);                                    // 再买(卖后回租房,可再买)
            state.housing.leveragePhase = LeveragePhase.Deflating;
            float savBeforeSell2 = state.resident.savings;
            float gap0 = state.bank.BalanceGap;
            Assert.IsTrue(MortgageService.TrySellHouse(state, out msg), msg);   // 第二笔覆写前先结旧
            Assert.AreEqual(savBeforeSell2 + net1, state.resident.savings, .01f,
                "修复F:第一笔在途款即时到账,不再被单槽覆写静默蒸发。");
            Assert.AreEqual(MortgageService.SaleDelayPeriods, state.housing.pendingSaleEta, .01f,
                "第二笔重新起 3 期到账。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, .01f, "结旧入账银行平衡表零扰动。");

            // 第二笔照常 3 期到账(本测试世界期账为空,SettleDay 幂等闸不触发)
            for (int i = 0; i < 3; i++) HousingService.SettleDay(state);
            Assert.AreEqual(0f, state.housing.pendingSaleProceeds, .01f, "第二笔净款到账。");
            Assert.AreEqual(0f, state.housing.pendingSaleEta, .01f);
        }

        // ———————————————— G:Finale 读档路由(第四处宿主) ————————————————

        [Test]
        public void DeliverPendingReport_HousingPart_RoutesThroughFinaleGate()
        {
            // 读档补发现场:待看第八篇报告 + 尚未看过收官过场
            state.progression.stageReports.Add(new StageReportRecord
            { part = LearningPart.Housing, completedWorldDay = state.day });
            state.progression.pendingStageReports.Add((int)LearningPart.Housing);
            Assert.IsFalse(CutsceneService.IsViewed(state, (int)CutsceneId.Finale), "未看过收官过场。");

            GameObject uiGo = new GameObject("V09FixUi");
            V09FixUiHarness ui = uiGo.AddComponent<V09FixUiHarness>();
            ui.InitializeForValidation();
            ui.DeliverPendingStageReport(state.progression.stageReports[0]);
            Assert.IsTrue(CutsceneService.IsViewed(state, (int)CutsceneId.Finale),
                "修复G:读档补发先走 Finale 闸(EditMode 标记+放行,真机播完过场再弹报告)。");
            Assert.AreEqual(0, state.progression.pendingStageReports.Count, "报告已展示消费 pending。");

            // 反例:非第八篇报告直接弹出,不触发收官过场标记
            GameState other = NewWorld();
            other.progression.stageReports.Add(new StageReportRecord
            { part = LearningPart.Trade, completedWorldDay = other.day });
            other.progression.pendingStageReports.Add((int)LearningPart.Trade);
            GameState.I = other;
            ui.DeliverPendingStageReport(other.progression.stageReports[0]);
            Assert.IsFalse(CutsceneService.IsViewed(other, (int)CutsceneId.Finale), "非第八篇不触发 Finale。");
            Assert.AreEqual(0, other.progression.pendingStageReports.Count, "照常消费 pending。");
            GameState.I = state;
            Object.DestroyImmediate(uiGo);
        }

        // ———————————————— H:住房实验室纵向收回内容区 ————————————————

        [Test]
        public void HousingLabPanel_BubbleCardFitsContentArea()
        {
            GameObject uiGo = new GameObject("V09FixUiLab");
            V09FixUiHarness ui = uiGo.AddComponent<V09FixUiHarness>();
            ui.InitializeForValidation();
            try
            {
                ui.ShowCurves(6);
                Transform card = FindIn(ui.transform, "labBubbleCard");
                Assert.IsNotNull(card, "泡沫卡已建。");
                RectTransform rt = (RectTransform)card;
                Assert.AreEqual(212f, rt.sizeDelta.y, .01f, "修复H:泡沫卡高 236→212。");
                Assert.LessOrEqual(416f + rt.sizeDelta.y, 632f, "总深 628 收回内容区(632)内,不再穿出面板底。");
            }
            finally { Object.DestroyImmediate(uiGo); }
        }

        // ———————————————— 盲区2:旧档 V0.9.5 卡回填 ————————————————

        [Test]
        public void OldSave_BackfillsV095Cards_FromExistingState()
        {
            // 造一个「V0.9.4 时代」的档:已买房并提前还清(Paid 终态)、付过租、期账见过 Deflating
            BuyStandardHouse(state);
            state.resident.savings = 40000f;                 // 垫足余额(首付后 27400<29400 还不清)
            state.bank.reserves += 40000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 40000f;
            string msg;
            Assert.IsTrue(MortgageService.TryEarlyPayoff(state, out msg), msg);
            state.history.Add(new DayRecord { day = state.day - 1, rentPaid = 140f });   // 前一日:当日记录会被 ApplyToState 丢弃
            state.housing.history.Add(new HousingPeriodRecord { period = 1, phase = LeveragePhase.Deflating });

            SaveSystem.SaveData data = SaveSystem.BuildSaveData(state);
            string json = JsonUtility.ToJson(data, false);
            GameObject targetObject = new GameObject("V09FixOldSave");
            try
            {
                GameState target = targetObject.AddComponent<GameState>();
                SaveSystem.ApplyToState(target, JsonUtility.FromJson<SaveSystem.SaveData>(json));
                foreach (string id in new[]
                {
                    "housing.downpayment", "housing.installment", "housing.annuity", "housing.ltv",
                    "housing.collateral", "housing.amortization", "housing.rentVsBuy", "housing.capRate",
                    "housing.deleveraging"
                })
                    Assert.IsTrue(target.unlockedCards.Contains(id), "旧档按既有状态回填:" + id);
                Assert.IsFalse(target.unlockedCards.Contains("housing.bubbleThree"),
                    "期账没见过 Inflating,不回填狂热卡。");
                Assert.IsFalse(target.unlockedCards.Contains("asset.treasury"),
                    "V0.9.5 新动作(认购国债)无法从旧状态推断,不回填。");
            }
            finally { Object.DestroyImmediate(targetObject); }
        }

        // ———————————————— 未验证#2:ShouldPlay(Finale) 纯读窥视 ————————————————

        [Test]
        public void ShouldPlayFinale_PurePeek_DanglingEntryNotConsumed()
        {
            // 悬挂队首(Trade 无报告)+有效第二位(Housing 有报告)
            state.progression.pendingStageReports.Add((int)LearningPart.Trade);
            state.progression.pendingStageReports.Add((int)LearningPart.Housing);
            state.progression.stageReports.Add(new StageReportRecord { part = LearningPart.Housing });
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.Finale), "有效待看报告成立。");
            Assert.AreEqual(2, state.progression.pendingStageReports.Count,
                "修复(未验证#2):过场判定零写,悬挂条目原样保留。");
            StageReportRecord viaGet1 = ProgressionService.GetPendingStageReport(state);
            Assert.IsNull(viaGet1, "Get 的既有语义:清掉悬挂队首后本趟返 null(一次清一条)。");
            Assert.AreEqual(1, state.progression.pendingStageReports.Count, "消费路径照常清悬挂。");
            StageReportRecord viaGet2 = ProgressionService.GetPendingStageReport(state);
            Assert.AreEqual(LearningPart.Housing, viaGet2.part, "第二趟取到有效 Housing 报告。");
            Assert.AreEqual(1, state.progression.pendingStageReports.Count,
                "取报告不消费下标(消费走 MarkPendingStageReportViewed)。");
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
    }
}

#endif
