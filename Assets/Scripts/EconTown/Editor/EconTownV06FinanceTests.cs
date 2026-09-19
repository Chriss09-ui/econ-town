#if UNITY_EDITOR

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    public class EconTownV06FinanceTests
    {
        GameObject stateObject;
        GameObject uiObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V06FinanceTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.resident.profileCreated = true;
            state.resident.cash = 3000f;
            state.resident.cashAtDayStart = state.resident.cash;
            state.money = 10000f;
            FinancialSystemService.Ensure(state);
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV06FinanceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (Exception) { }
            if (uiObject != null) UnityEngine.Object.DestroyImmediate(uiObject);
            GameState.I = null;
            if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void FreshState_DoesNotInventGovernmentDebtOrBankBonds()
        {
            Assert.AreEqual(0f, state.government.debt, .001f);
            Assert.AreEqual(0, state.bank.bonds.Count,
                "政府没有初始债务时，银行资产端不得凭空出现国债。" );
            Assert.AreEqual(0f, state.bank.GovernmentBonds, .001f);
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void Deposit_IncreasesBankReserves_AndDeposits()
        {
            float cash = state.resident.cash;
            float deposit = state.resident.savings;
            float bankDeposit = state.bank.householdDeposits;
            float reserves = state.bank.reserves;

            Assert.IsTrue(CreditService.DepositResidentCash(state, 500f, out string message), message);

            Assert.AreEqual(cash - 500f, state.resident.cash, .001f);
            Assert.AreEqual(deposit + 500f, state.resident.savings, .001f);
            Assert.AreEqual(bankDeposit + 500f, state.bank.householdDeposits, .001f);
            Assert.AreEqual(reserves + 500f, state.bank.reserves, .001f);
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void Loan_IncreasesBorrowerDeposit_AndBankLoanAsset()
        {
            float assetsBefore = state.money + state.bank.companyDeposits;
            LoanRecord loan = CreditService.IssueCompanyLoan(state, 1000f, LoanPurpose.WorkingCapital, 12, out string message);

            Assert.IsNotNull(loan, message);
            Assert.AreEqual(1000f, state.bank.companyDeposits, .001f);
            Assert.AreEqual(1000f, state.bank.CompanyLoans, .001f);
            Assert.AreEqual(assetsBefore, state.money + state.bank.companyDeposits - CreditService.CompanyDebt(state), .001f,
                "贷款同时增加存款资产和负债，不应凭空提高净资产。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void ResidentLoan_IncreasesAssetsAndDebtWithoutIncreasingNetWorth()
        {
            state.resident.employedToday = true;
            state.resident.dailyIncome = 100f;
            float netWorthBefore = state.resident.cash + state.resident.savings - CreditService.ResidentDebt(state);

            LoanRecord loan = CreditService.IssueResidentLoan(state, 500f, 12, out string message);

            Assert.IsNotNull(loan, message);
            float netWorthAfter = state.resident.cash + state.resident.savings - CreditService.ResidentDebt(state);
            Assert.AreEqual(netWorthBefore, netWorthAfter, .01f);
            Assert.AreEqual(500f, state.bank.HouseholdLoans, .001f);
            Assert.AreEqual(loan.annualInterestRate,
                CreditService.AverageLoanRate(state, LoanBorrowerType.Resident), .001f);
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void LoanRepayment_ReducesLoanAsset_AndBorrowerCash()
        {
            LoanRecord loan = CreditService.IssueCompanyLoan(state, 1000f, LoanPurpose.WorkingCapital, 4, out string message);
            Assert.IsNotNull(loan, message);
            Assert.IsTrue(CreditService.TransferCompanyDepositToCash(state, 1000f, out message), message);
            float cashBefore = state.money;
            float debtBefore = loan.remainingPrincipal;

            FinancialSystemService.SettleContracts(state);

            Assert.Less(state.money, cashBefore);
            Assert.Less(loan.remainingPrincipal, debtBefore);
            Assert.Greater(state.bank.interestIncome, 0f);
            Assert.AreEqual(0f, state.bank.BalanceGap, .02f);
        }

        [Test]
        public void BankBalanceSheet_AlwaysBalances()
        {
            Assert.IsTrue(CreditService.DepositResidentCash(state, 300f, out string message), message);
            LoanRecord loan = CreditService.IssueCompanyLoan(state, 1200f, LoanPurpose.EquipmentInvestment, 24, out message);
            Assert.IsNotNull(loan, message);
            Assert.IsTrue(CreditService.FinanceEquipmentInvestment(state, 800f, out message), message);
            FinancialSystemService.SettleContracts(state);
            GovernmentSimulator.AdvanceCycle(state);
            FinancialSystemService.CompleteSettlement(state);

            Assert.AreEqual(0f, state.bank.BalanceGap, .05f);
            Assert.AreEqual(state.bank.TotalAssets, state.bank.TotalLiabilities + state.bank.equity, .05f);
        }

        [Test]
        public void InterestIncome_IsNotCreatedWithoutCounterpartyExpense()
        {
            float residentBefore = state.resident.savings;
            float equityBefore = state.bank.equity;

            CreditService.AccrueDepositInterest(state);

            float interest = state.resident.savings - residentBefore;
            Assert.Greater(interest, 0f);
            Assert.AreEqual(interest, state.bank.interestExpense, .001f);
            Assert.AreEqual(interest, equityBefore - state.bank.equity, .001f);
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void PolicyRate_ChangesBankRates_WithLag()
        {
            state.centralBank.unlocked = true;
            float loanRateBefore = state.bank.companyLoanRate;
            float gdpBefore = state.macro.realGdp;
            float inflationBefore = state.macro.inflation;

            Assert.IsTrue(InterestRateService.TrySetPolicyRate(state, 5f, out string message), message);
            Assert.AreEqual(loanRateBefore, state.bank.companyLoanRate, .001f, "政策公布当下不应瞬间改完银行报价。");
            Assert.AreEqual(gdpBefore, state.macro.realGdp, .001f, "央行不得直接修改 GDP。");
            Assert.AreEqual(inflationBefore, state.macro.inflation, .001f, "央行不得直接修改通胀。");

            state.day++;
            InterestRateService.AdvanceTransmission(state);
            Assert.Greater(state.bank.companyLoanRate, loanRateBefore);
            Assert.Less(state.centralBank.transmission.realEconomyPassThrough, .01f,
                "实体经济传导应晚于银行利率。");
        }

        [Test]
        public void HigherLoanRate_ReducesInvestmentCeterisParibus()
        {
            state.centralBank.transmission.decisionWorldDay = -999;
            state.bank.companyLoanRate = 5f;
            float lowRateInvestment = InterestRateService.CompanyInvestmentBreakdown(state, 1000f).finalValue;
            state.bank.companyLoanRate = 12f;
            float highRateInvestment = InterestRateService.CompanyInvestmentBreakdown(state, 1000f).finalValue;

            Assert.Less(highRateInvestment, lowRateInvestment);
        }

        [Test]
        public void FinancialCrisis_ReducesConsumptionAndInvestment()
        {
            state.centralBank.transmission.decisionWorldDay = -999;
            state.bank.financialStress = 0f;
            state.bank.liquidityCrisis = false;
            float normalConsumption = InterestRateService.ResidentConsumptionBreakdown(state, 1000f).finalValue;
            float normalInvestment = InterestRateService.CompanyInvestmentBreakdown(state, 1000f).finalValue;

            state.bank.financialStress = 80f;
            state.bank.liquidityCrisis = true;
            FinancialEffectBreakdown stressedConsumption = InterestRateService.ResidentConsumptionBreakdown(state, 1000f);
            FinancialEffectBreakdown stressedInvestment = InterestRateService.CompanyInvestmentBreakdown(state, 1000f);

            Assert.Less(stressedConsumption.finalValue, normalConsumption);
            Assert.Less(stressedInvestment.finalValue, normalInvestment);
            Assert.Less(stressedConsumption.financialStressEffect, 0f);
            Assert.Less(stressedInvestment.creditAvailabilityEffect, 0f);
        }

        [Test]
        public void RequiredReserveRatio_IsRespected()
        {
            state.bank.reserves = state.bank.RequiredReserves;
            CreditQuote quote = CreditService.QuoteCompanyLoan(state, 1000f, LoanPurpose.WorkingCapital, 12);

            Assert.IsFalse(quote.approved);
            Assert.AreEqual(0f, quote.approvedAmount, .001f);
        }

        [Test]
        public void BankRun_CanCreateLiquidityCrisisWithoutInsolvency()
        {
            state.bank.loans.Clear();
            state.bank.bonds.Clear();
            state.bank.bonds.Add(new GovernmentBondLot { id = "illiquid", principal = 10000f, annualInterestRate = 3f });
            state.bank.householdDeposits = 10000f;
            state.resident.savings = 10000f;
            state.bank.companyDeposits = 0f;
            state.bank.reserves = 1000f;
            state.bank.centralBankBorrowing = 0f;
            state.bank.equity = state.bank.TotalAssets - state.bank.TotalLiabilities;

            CreditService.TriggerBankRun(state, .5f);

            Assert.IsTrue(state.bank.liquidityCrisis);
            Assert.Greater(state.bank.bankRunUnmetWithdrawals, 0f);
            Assert.IsFalse(state.bank.Insolvent, "银行可以资产大于负债，却因准备金不足而无法立刻兑付。");
            Assert.AreEqual(0f, state.bank.BalanceGap, .01f);
        }

        [Test]
        public void LoanDefault_ReducesBankEquity()
        {
            LoanRecord loan = CreditService.IssueCompanyLoan(state, 1000f, LoanPurpose.WorkingCapital, 12, out string message);
            Assert.IsNotNull(loan, message);
            float equityBefore = state.bank.equity;

            Assert.IsTrue(CreditService.DefaultLoan(state, loan, .30f));

            Assert.AreEqual(700f, equityBefore - state.bank.equity, .01f);
            Assert.AreEqual(LoanStatus.Defaulted, loan.status);
            Assert.AreEqual(0f, state.bank.BalanceGap, .02f);
        }

        [Test]
        public void GovernmentInterestExpense_UsesActualDebtRate()
        {
            state.government.debt = 5200f;
            state.government.averageDebtRate = 7f;
            float expected = 5200f * InterestRateService.PeriodRate(7f);

            GovernmentBudgetPreview preview = GovernmentSimulator.PreviewBudget(state);

            Assert.AreEqual(expected, preview.interestSpending, .001f);
            state.centralBank.unlocked = true;
            Assert.IsTrue(InterestRateService.TrySetPolicyRate(state, 9f, out string message), message);
            Assert.AreEqual(expected, GovernmentSimulator.PreviewBudget(state).interestSpending, .001f,
                "既有固定利率债务不得因央行加息立即全部重定价。");
        }

        [Test]
        public void V052CompletedSave_ReopensAtFinanceChapter_AndKeepsSandbox()
        {
            state.progression = new ProgressionState
            {
                initialized = true,
                completedChapter = 30,
                currentChapter = 31,
                mainlineComplete = true,
                sandboxUnlocked = true
            };

            ProgressionService.Ensure(state);

            Assert.IsFalse(state.progression.mainlineComplete);
            Assert.AreEqual(31, state.progression.currentChapter);
            Assert.AreEqual(30, state.progression.completedChapter);
            Assert.IsTrue(state.progression.sandboxUnlocked);
        }

        [Test]
        public void FinanceMainline_HasTwelveOrderedChapters_AndFortyCards()
        {
            // V0.7 主线延至 53 章后,第六篇仍占 31-42 章(断言只锚定本篇,不锁总数)。
            Assert.GreaterOrEqual(ProgressionService.Chapters.Count, 42);
            for (int i = 30; i < 42; i++)
            {
                Assert.AreEqual(i + 1, ProgressionService.Chapters[i].number);
                Assert.AreEqual(LearningPart.Finance, ProgressionService.Chapters[i].part);
            }

            string[] ids = {
                "money", "mediumofexchange", "unitofaccount", "storeofvalue", "commercialbank",
                "bankbalancesheet", "bankdeposit", "loan", "reserves", "reserveratio", "creditcreation",
                "depositmoney", "moneysupply", "moneymultiplier", "credit", "interestrate", "nominalinterest",
                "realinterest", "riskpremium", "interestsaving", "interestconsumption", "interestinvestment",
                "centralbank", "policyrate", "expansionarymonetary", "contractionarymonetary",
                "monetarytransmission", "policylag", "inflationtarget", "inflationexpectations", "liquidity",
                "bankrun", "baddebt", "bankcapital", "lenderoflastresort", "financialstability", "creditcycle",
                "financialaccelerator", "governmentbond", "debtinterest"
            };
            Assert.AreEqual(40, ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.IsNotNull(CardLibrary.Get(ids[i]), "缺少 V0.6 金融知识卡：" + ids[i]);
                Assert.AreEqual(KnowledgeCategory.Finance, KnowledgeSystem.GetMetadata(ids[i]).category);
            }
        }

        [Test]
        public void FinanceState_SurvivesSaveRoundTrip()
        {
            state.progression.completedChapter = 37;
            state.progression.currentChapter = 38;
            state.centralBank.unlocked = true;
            LoanRecord loan = CreditService.IssueCompanyLoan(state, 1400f, LoanPurpose.EquipmentInvestment, 24, out string message);
            Assert.IsNotNull(loan, message);
            Assert.IsTrue(InterestRateService.TrySetPolicyRate(state, 2.5f, out message), message);
            string loanId = loan.id;
            float deposits = state.bank.companyDeposits;

            SaveSystem.Save(state);
            state.bank = new BankState();
            state.centralBank = new CentralBankState();
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(deposits, state.bank.companyDeposits, .001f);
            Assert.AreEqual(loanId, state.bank.loans[0].id);
            Assert.AreEqual(1400f, state.bank.loans[0].remainingPrincipal, .001f);
            Assert.AreEqual(2.5f, state.centralBank.policyRate, .001f);
            Assert.AreEqual(state.day, state.centralBank.transmission.decisionWorldDay);
            Assert.AreEqual(0f, state.bank.BalanceGap, .02f);
        }

        [Test]
        public void BankBalanceSheet_RemainsBalanced_For365Settlements()
        {
            for (int i = 0; i < 365; i++)
            {
                FinancialSystemService.SettleContracts(state);
                BankDayRecord record = FinancialSystemService.CompleteSettlement(state);
                Assert.AreEqual(0f, record.balanceGap, .05f, "WorldDay " + state.day + " 银行账本不平");
                state.day++;
            }
            Assert.AreEqual(365, state.bank.history.Count);
        }

        [Test]
        public void EmergencyLiquidity_PaysUnmetWithdrawals_AndClosesRun()
        {
            state.bank.loans.Clear();
            state.bank.bonds.Clear();
            state.bank.bonds.Add(new GovernmentBondLot { id = "illiquid", principal = 10000f, annualInterestRate = 3f });
            state.bank.householdDeposits = 10000f;
            state.resident.savings = 10000f;
            state.bank.reserves = 1000f;
            state.bank.equity = state.bank.TotalAssets - state.bank.TotalLiabilities;
            state.centralBank.unlocked = true;
            CreditService.TriggerBankRun(state, .5f);
            float amount = state.bank.bankRunUnmetWithdrawals + state.bank.RequiredReserves + 100f;

            Assert.IsTrue(CreditService.ProvideEmergencyLiquidity(state, amount, out string message), message);

            Assert.AreEqual(0f, state.bank.bankRunUnmetWithdrawals, .01f);
            Assert.IsFalse(state.bank.bankRunActive);
            Assert.IsFalse(state.bank.liquidityCrisis);
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.BankRunResolved));
            Assert.AreEqual(0f, state.bank.BalanceGap, .02f);
        }

        [Test]
        public void FinanceKnowledgeCases_UseCurrentSharedLedger()
        {
            state.bank.financialStress = 25f;
            KnowledgeSystem.SyncPlayerCases(state);

            KnowledgeCaseRecord balance = KnowledgeSystem.GetCase(state, "bankbalancesheet");
            KnowledgeCaseRecord policy = KnowledgeSystem.GetCase(state, "policyrate");
            KnowledgeCaseRecord transmission = KnowledgeSystem.GetCase(state, "monetarytransmission");

            Assert.IsNotNull(balance);
            StringAssert.Contains(state.bank.TotalAssets.ToString("0"), balance.summary);
            StringAssert.Contains(state.bank.BalanceGap.ToString("0.00"), balance.summary);
            Assert.IsNotNull(policy);
            StringAssert.Contains(state.centralBank.policyRate.ToString("0.00"), policy.summary);
            Assert.IsNotNull(transmission);
            StringAssert.Contains("政策", transmission.summary);
        }

        [Test]
        public void CentralBankRateHistoryGraph_HasDataAndStaysInsideCard()
        {
            state.progression.initialized = true;
            state.progression.completedChapter = ProgressionService.Chapters.Count;
            state.progression.currentChapter = 42;
            state.progression.mainlineComplete = true;
            state.centralBank.unlocked = true;
            state.macro.history.Clear();
            state.macro.history.Add(new MacroPeriodRecord { period = 1, policyRate = 3f, companyLoanRate = 5.5f });
            state.macro.history.Add(new MacroPeriodRecord { period = 2, policyRate = 2.75f, companyLoanRate = 5.4f });
            state.macro.history.Add(new MacroPeriodRecord { period = 3, policyRate = 2.5f, companyLoanRate = 5.1f });

            uiObject = new GameObject("V06FinanceTestUi");
            UIManager ui = uiObject.AddComponent<UIManager>();
            ui.InitializeForValidation();
            ui.ShowFinanceCenter(3);
            Canvas.ForceUpdateCanvases();

            Transform graphTransform = FindDescendant(ui.transform, "rateHistoryGraph");
            Assert.IsNotNull(graphTransform, "有至少两期历史时，央行政策页必须生成利率曲线。" );
            PolylineGraph graph = graphTransform.GetComponent<PolylineGraph>();
            Assert.AreEqual(3, graph.PointCount);
            Assert.AreEqual(3, graph.SeriesBPointCount);
            RectTransform graphRect = graph.rectTransform;
            RectTransform cardRect = graphRect.parent as RectTransform;
            Assert.IsNotNull(cardRect);
            float left = graphRect.anchoredPosition.x;
            float top = -graphRect.anchoredPosition.y;
            Assert.GreaterOrEqual(left, 0f);
            Assert.GreaterOrEqual(top, 0f);
            Assert.LessOrEqual(left + graphRect.rect.width, cardRect.rect.width + .1f,
                "曲线不应越过政策传导卡片右边界。" );
            Assert.LessOrEqual(top + graphRect.rect.height, cardRect.rect.height + .1f,
                "曲线不应越过政策传导卡片下边界。" );
        }

        static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

#endif
