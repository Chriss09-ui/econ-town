#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.1《安家》(PRD §193.9):状态块默认值/租金守恒/买房复式记账/等额本息/授信/法拍/DSR 档。</summary>
    public class EconTownV091HousingTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V091State");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            FinancialSystemService.Ensure(state);
            HousingService.Ensure(state);
            state.history.Add(new DayRecord { day = state.day });
            state.resident.happiness = 60f;
            MirrorDeposits(state.resident.savings);   // 玩家存款在本行的镜像(与真实对局同构)
        }

        /// <summary>把玩家存款镜像到银行负债端,同时增等额准备金——保持资产=负债,不触发 Insolvent。</summary>
        void MirrorDeposits(float savings)
        {
            float delta = savings - state.bank.householdDeposits;
            state.bank.householdDeposits = savings;
            state.bank.reserves += Mathf.Max(0f, delta);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        DayRecord Rec() { return state.LastRecord; }

        LoanRecord Loan() { return HousingService.CurrentMortgage(state); }

        void Buy(float income)
        {
            state.resident.savings = 20000f;
            MirrorDeposits(20000f);
            state.resident.todayIncome = income;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(state, out msg), msg);
        }

        void AdvanceDay()
        {
            state.day += 1;
            state.history.Add(new DayRecord { day = state.day });
        }

        [Test]
        public void Ensure_OldArchive_HousingDefaults_RentingNoLoan()
        {
            state.housing = null;   // 模拟旧档(JsonUtility 对缺失字段留 null)
            HousingService.Ensure(state);
            Assert.IsNotNull(state.housing, "Ensure 必须复活状态块。");
            Assert.IsTrue(state.housing.renting, "旧档默认租房。");
            Assert.AreEqual("", state.housing.mortgageLoanId, "旧档无贷。");
            Assert.AreEqual(42000f, state.housing.townHousePrice, "R14 房价默认。");
            Assert.AreEqual(140f, state.housing.periodRent, "R12 租金默认。");
        }

        [Test]
        public void Rent_PaidFromSavings_TownSectorConserved()
        {
            float savingsBefore = state.resident.savings;
            float depositsBefore = state.bank.householdDeposits;
            float sectorBefore = state.bank.townSectorDeposits;
            HousingService.SettleDay(state);
            Assert.AreEqual(savingsBefore - 140f, state.resident.savings, .01f);
            Assert.AreEqual(depositsBefore - 140f, state.bank.householdDeposits, .01f);
            Assert.AreEqual(sectorBefore + 140f, state.bank.townSectorDeposits, .01f, "对手方=镇住户部门(银行负债端平移,平衡表零扰动)。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f, "租金不破坏银行平衡。");
            Assert.AreEqual(140f, Rec().rentPaid, .01f, "账本行 rentPaid。");
            Assert.AreEqual(0, state.housing.rentMissed);
        }

        [Test]
        public void Rent_MissedThreePeriods_Eviction()
        {
            state.resident.savings = 0f;
            HousingService.SettleDay(state);
            HousingService.SettleDay(state);
            Assert.AreEqual(2, state.housing.rentMissed);
            Assert.AreEqual(60f, state.resident.happiness, .01f, "欠租未满 3 期不惩罚幸福。");
            HousingService.SettleDay(state);
            Assert.AreEqual(0, state.housing.rentMissed, "搬离后计数清零。");
            Assert.AreEqual(52f, state.resident.happiness, .01f, "被迫搬离:幸福 −8。");
        }

        [Test]
        public void BuyHouse_DownPaymentPlusLoan_EqualsSellerReceipt()
        {
            float sectorBefore = state.bank.townSectorDeposits;
            Buy(800f);
            Assert.AreEqual(sectorBefore + 42000f * .30f + 42000f * .70f, state.bank.townSectorDeposits, .5f,
                "守恒:卖方入账=首付+放款。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .5f, "买房+放款后银行平衡表仍平(资产 loans+principal ↔ 负债 townSector+principal)。");
        }

        [Test]
        public void BuyHouse_BankDoubleEntry_AssetLiability()
        {
            Buy(800f);   // 镜像后负债端 household=20000
            LoanRecord loan = Loan();
            Assert.IsNotNull(loan, "放款后应能按 id 找到房贷。");
            Assert.AreEqual(42000f * .70f, loan.principal, .01f, "按揭=房价×70%(抵押通道)。");
            Assert.AreEqual(156, loan.term, "期限 156 期。");
            Assert.AreEqual(42000f, loan.collateralValue, .01f);
            Assert.AreEqual(.70f, loan.ltvAtOrigination, .001f);
            Assert.AreEqual(20000f - 42000f * .30f, state.bank.householdDeposits, .5f,
                "银行负债端只动首付——放款进卖方账户,不进居民存款。");
        }

        [Test]
        public void MortgageQuote_SpreadTerm156_AnnuityFormula()
        {
            float rate = state.bank.householdLoanRate + 1.50f;
            float payment = MortgageService.AnnuityPayment(42000f * .70f, rate, 156);
            float r = rate / 100f / 52f;
            float f = Mathf.Pow(1f + r, 156);
            Assert.AreEqual(42000f * .70f * r * f / (f - 1f), payment, .01f, "等额本息闭式公式。");
            Assert.Greater(payment, 42000f * .70f / 156f, "含息供款高于纯本金均摊。");
        }

        [Test]
        public void MortgageSettle_EqualInstallment_PrincipalInterestSplit()
        {
            Buy(800f);
            LoanRecord loan = Loan();
            float payment = loan.paymentPerPeriod;
            float savingsBefore = state.resident.savings;
            CreditService.SettleLoanPayments(state);
            Assert.AreEqual(payment, Rec().mortgagePrincipalPaid + Rec().mortgageInterestPaid, .5f,
                "守恒:供款=本金+利息。");
            Assert.AreEqual(savingsBefore - payment, state.resident.savings, .5f);
            float principal1 = Rec().mortgagePrincipalPaid;

            AdvanceDay();
            CreditService.SettleLoanPayments(state);
            Assert.AreEqual(payment, Rec().mortgagePrincipalPaid + Rec().mortgageInterestPaid, .5f, "每期总供款固定。");
            Assert.Greater(Rec().mortgagePrincipalPaid, principal1 + .01f, "本金占比逐期上升。");
            Assert.Greater(42000f * .70f, loan.remainingPrincipal);
        }

        [Test]
        public void MortgageSettle_SingleEntry_AnnuityNotStraightLine()
        {
            Buy(800f);
            LoanRecord loan = Loan();
            float p = loan.principal;
            CreditService.SettleLoanPayments(state);
            Assert.IsTrue(Mathf.Abs(Rec().mortgagePrincipalPaid - p / 156f) > .5f,
                "房贷必须走年金分支,不是等额本金 principal/term。");
            Assert.AreEqual(p * InterestRateService.PeriodRate(loan.annualInterestRate), Rec().mortgageInterestPaid, .01f,
                "利息按当期余额计。");
        }

        [Test]
        public void MortgageApproval_CollateralChannel_DsrWarning()
        {
            string msg;
            state.resident.savings = 500f;
            Assert.IsFalse(MortgageService.TryBuyHouse(state, out msg), "首付不足必须拒。");
            Assert.IsTrue(msg.Contains("首付"), msg);

            state.resident.savings = 20000f;
            // 修复轮回A:首付守卫改双口径同查——测试摆储蓄必须镜像银行入账(与真实对局同构)。
            state.bank.reserves += 20000f - state.bank.householdDeposits;
            state.bank.householdDeposits = 20000f;
            state.resident.todayIncome = 0f;
            Assert.IsFalse(MortgageService.TryBuyHouse(state, out msg), "零收入 DSR 无穷必须拒。");
            Assert.IsTrue(msg.Contains("DSR"), msg);

            state.resident.todayIncome = 800f;   // 供款≈210,DSR≈26%
            Assert.IsTrue(MortgageService.TryBuyHouse(state, out msg), msg);
            Assert.AreEqual(29400f, Loan().principal, .01f,
                "按揭额=房价×70%,不看 QuoteResidentLoan 收入限额(该限额量级必拒按揭)。");
        }

        [Test]
        public void Foreclose_RecoveryPlusLoss_EqualsBalance()
        {
            Buy(800f);
            LoanRecord loan = Loan();
            state.housing.homeAppraisal = 20000f;   // 压低抵押物:17000 < 余额 → 制造损失路径
            float outstandingBefore = loan.remainingPrincipal;
            float loanLossBefore = state.bank.loanLoss;
            state.resident.savings = 0f;
            state.resident.cash = 0f;
            state.bank.householdDeposits = 0f;

            CreditService.SettleLoanPayments(state);
            AdvanceDay();
            CreditService.SettleLoanPayments(state);
            AdvanceDay();
            CreditService.SettleLoanPayments(state);

            Assert.AreEqual(LoanStatus.Defaulted, loan.status, "连续 3 期欠供触发法拍。");
            Assert.AreEqual(0f, loan.remainingPrincipal);
            Assert.AreEqual(outstandingBefore - 20000f * .85f, state.bank.loanLoss - loanLossBefore, .5f,
                "坏账=余额−回收(回收=估值×85%)。");
            Assert.IsTrue(state.housing.renting, "法拍后回租房。");
            Assert.AreEqual(48f, state.resident.happiness, .01f, "幸福 −12。");
            Assert.AreEqual("", state.housing.mortgageLoanId);
        }

        [Test]
        public void EarlyPayoff_ClearsLoan_StatusPaid()
        {
            Buy(800f);
            CreditService.SettleLoanPayments(state);
            LoanRecord loan = Loan();
            state.resident.savings = loan.remainingPrincipal + 100f;
            MirrorDeposits(state.resident.savings);   // 存款调整同步镜像
            string msg;
            Assert.IsTrue(MortgageService.TryEarlyPayoff(state, out msg), msg);
            Assert.AreEqual(LoanStatus.Paid, loan.status);
            Assert.AreEqual("", state.housing.mortgageLoanId, "结清后清引用。");
            Assert.IsFalse(state.housing.renting, "还清后仍拥有住房。");
            Assert.AreEqual(100f, state.resident.savings, .01f);
            Assert.IsNull(HousingService.CurrentMortgage(state), "无未结清房贷。");
            Assert.AreEqual(0, HousingService.DsrBand(state), "结清后 DSR 归零。");
        }

        [Test]
        public void DsrBands_EnergyDecayExtra_SideJobGate()
        {
            state.resident.profileCreated = true;
            state.resident.energy = 100f;
            ResidentEconomy.EndDay(state);
            Assert.AreEqual(92f, state.resident.energy, .01f, "无贷基准:日终衰减 8。");

            // band1:DSR 25–40% → 衰减 18、兼职不解锁
            state.resident.reportReady = false;
            Buy(700f);   // 210/700=30%
            Assert.AreEqual(1, HousingService.DsrBand(state));
            state.resident.energy = 100f;
            ResidentEconomy.EndDay(state);
            Assert.AreEqual(82f, state.resident.energy, .01f, "band1:衰减 −8→−18。");
            state.resident.reportReady = false;
            state.resident.freeHours = 4f;
            ResidentActionResult fail = ResidentEconomy.SideJob(state);
            Assert.IsFalse(fail.success, "band1 不解锁加班兼职。");

            // band2:DSR>40% → 兼职解锁(收入口径实时读 todayIncome)
            state.resident.todayIncome = 400f;   // 210/400≈52.5%
            Assert.AreEqual(2, HousingService.DsrBand(state));
            state.resident.reportReady = false;
            state.resident.freeHours = 4f;
            state.resident.energy = 90f;
            ResidentActionResult ok = ResidentEconomy.SideJob(state);
            Assert.IsTrue(ok.success, ok.message);
            Assert.AreEqual(3f, state.resident.freeHours, .01f);
        }
    }
}

#endif
