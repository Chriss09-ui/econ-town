using UnityEngine;

namespace EconTown
{
    /// <summary>WorldDay 金融结算编排：合约先结算，政府债务后入账，最后生成银行日账本。</summary>
    public static class FinancialSystemService
    {
        public const float BankOperatingCostPerPeriod = 12f;

        public static void Ensure(GameState state)
        {
            InterestRateService.Ensure(state);
        }

        public static void SettleContracts(GameState state)
        {
            Ensure(state);
            BankState bank = state.bank;
            if (bank.lastContractSettlementDay == state.day)
            {
                // 围猎五(#4)复审:已结算窗口读档重放——原时间线对本日记录的利息改写
                // (下方尾段)不随重放,重放日新日报 profit/interestExpense 恒缺这笔
                // (利润与现金勾稽差一笔利息)。按当日流量补写进重放记录,补后清零防重复。
                if (bank.todayLoanInterestPaidCompany > 0f)
                {
                    DayRecord replay = state.LastRecord;
                    if (replay != null && replay.day == state.day)
                    {
                        replay.interestExpense += bank.todayLoanInterestPaidCompany;
                        replay.totalCost += bank.todayLoanInterestPaidCompany;
                        replay.profit -= bank.todayLoanInterestPaidCompany;
                        replay.moneyEnd = state.money;
                        bank.todayLoanInterestPaidCompany = 0f;
                    }
                }
                return;
            }
            bank.lastContractSettlementDay = state.day;
            state.todayCompanyInterestExpense = 0f;
            // 围猎五(#4):当日供款现金垫付流量,只在真正结算时清零重计(同日闸早退不清)。
            bank.todayCompanyLoanPaymentFromCash = 0f;
            bank.todayResidentLoanPaymentFromCash = 0f;

            InterestRateService.AdvanceTransmission(state);
            CreditService.AccrueDepositInterest(state);
            CreditService.SettleLoanPayments(state);

            float cost = Mathf.Min(BankOperatingCostPerPeriod, Mathf.Max(0f, bank.reserves));
            bank.reserves -= cost;
            bank.operatingCost = cost;
            bank.equity -= cost;

            DayRecord enterprise = state.LastRecord;
            if (enterprise != null && enterprise.day == state.day && state.todayCompanyInterestExpense > 0f)
            {
                enterprise.interestExpense += state.todayCompanyInterestExpense;
                enterprise.totalCost += state.todayCompanyInterestExpense;
                enterprise.profit -= state.todayCompanyInterestExpense;
                enterprise.moneyEnd = state.money;
            }
            // 围猎五(#4)复审:真跑已把利息改写进当日记录,流量清零(否则同日再进早退分支会补写重复)。
            bank.todayLoanInterestPaidCompany = 0f;
        }

        public static void RegisterGovernmentDebtIssuance(GameState state, float oldDebt, float issuance)
        {
            Ensure(state);
            GovernmentState government = state.government;
            issuance = Mathf.Max(0f, issuance);
            government.newBondIssuance = issuance;
            if (issuance <= .001f) return;
            float newRate = InterestRateService.GovernmentNewDebtRate(state);
            float oldRate = government.averageDebtRate <= 0f ? 3f : government.averageDebtRate;
            government.averageDebtRate = (oldDebt * oldRate + issuance * newRate) / Mathf.Max(.001f, oldDebt + issuance);
            CreditService.PurchaseGovernmentBonds(state, issuance, newRate);
        }

        public static void ReceiveGovernmentInterest(GameState state, float interestPaid)
        {
            Ensure(state);
            CreditService.ReceiveGovernmentBondInterest(state, interestPaid, state.government.debt);
        }

        public static BankDayRecord CompleteSettlement(GameState state)
        {
            Ensure(state);
            BankState bank = state.bank;
            if (bank.lastCompletedSettlementDay == state.day && bank.history.Count > 0)
                return bank.history[bank.history.Count - 1];
            bank.lastCompletedSettlementDay = state.day;

            float loans = bank.PerformingLoans;
            bank.creditGrowth = bank.previousPerformingLoans <= .01f ? (loans > .01f ? 100f : 0f) :
                (loans - bank.previousPerformingLoans) / bank.previousPerformingLoans * 100f;
            bank.previousPerformingLoans = loans;
            // 围猎三(#7):日终按实际准备金重算——旧 OR 累加只能置位不能清除,自然回补(居民存款/
            // 现金还款/政府利息/法拍回收)后标志永真,流动性溢价与「流动性紧张」状态永久错报。
            // 复审补:保留 TriggerBankRun 置位口径的「挤兑未满足提款」腿——准备金当日恰好回补
            // 到法定线上时,未兑付的挤兑挂账(只有央行流动性工具能解决)不得被重算静默清除。
            bank.liquidityCrisis = bank.reserves + .01f < bank.RequiredReserves ||
                bank.bankRunUnmetWithdrawals > .01f;
            float liquidityGap = Mathf.Max(0f, bank.RequiredReserves - bank.reserves);
            float capitalRisk = Mathf.Max(0f, .08f - bank.CapitalAdequacy) * 450f;
            float badLoanRisk = bank.PerformingLoans <= .01f ? 0f : bank.loanLoss / bank.PerformingLoans * 100f;
            bank.financialStress = Mathf.Clamp(liquidityGap / Mathf.Max(1f, bank.RequiredReserves) * 55f +
                capitalRisk + badLoanRisk + (bank.bankRunActive ? 18f : 0f) + (bank.Insolvent ? 45f : 0f), 0f, 100f);
            state.centralBank.financialStress = bank.financialStress;

            float m0 = MoneySupplyM0(state);
            float m1 = m0 + bank.TotalDeposits;
            BankDayRecord record = new BankDayRecord
            {
                worldDay = state.day,
                reserves = bank.reserves,
                householdDeposits = bank.householdDeposits,
                companyDeposits = bank.companyDeposits,
                townSectorDeposits = bank.townSectorDeposits,
                householdLoans = bank.HouseholdLoans,
                companyLoans = bank.CompanyLoans,
                governmentBonds = bank.GovernmentBonds,
                centralBankBorrowing = bank.centralBankBorrowing,
                equity = bank.equity,
                totalAssets = bank.TotalAssets,
                totalLiabilities = bank.TotalLiabilities,
                balanceGap = bank.BalanceGap,
                depositRate = bank.depositRate,
                householdLoanRate = bank.householdLoanRate,
                companyLoanRate = bank.companyLoanRate,
                requiredReserveRatio = bank.requiredReserveRatio,
                actualReserveRatio = bank.ActualReserveRatio,
                interestIncome = bank.interestIncome,
                interestExpense = bank.interestExpense,
                operatingCost = bank.operatingCost,
                loanLoss = bank.loanLoss,
                newCredit = bank.todayNewCredit,
                companyInvestment = bank.todayCompanyInvestment,
                m0 = m0,
                m1 = m1,
                financialStress = bank.financialStress,
                liquidityCrisis = bank.liquidityCrisis,
                insolvent = bank.Insolvent
            };
            bank.history.Add(record);
            if (bank.history.Count > 730) bank.history.RemoveAt(0);
            if (Mathf.Abs(record.balanceGap) > .10f)
                Debug.LogError("银行资产负债表不平：Day " + state.day + " gap=" + record.balanceGap.ToString("0.00"));
            // V0.7 外储守恒对账(P0):干预消耗必须有账本行对应,禁止凭空增减。
            ExchangeRateService.AssertNoFreeMoney(state);
            // 账本落盘后才清空当日流量：玩家在收日前办理的贷款、投资与违约都应属于这一天。
            bank.interestIncome = 0f;
            bank.interestExpense = 0f;
            bank.operatingCost = 0f;
            bank.loanLoss = 0f;
            bank.todayNewCredit = 0f;
            bank.todayCompanyInvestment = 0f;
            state.Changed();
            return record;
        }

        public static float MoneySupplyM0(GameState state)
        {
            if (state == null) return 0f;
            return Mathf.Max(0f, state.money) + Mathf.Max(0f, state.resident != null ? state.resident.cash : 0f) +
                Mathf.Max(0f, state.government != null ? state.government.cash : 0f) + Mathf.Max(0f, state.bank != null ? state.bank.cash : 0f);
        }

        public static float MoneySupplyM1(GameState state)
        {
            Ensure(state);
            return MoneySupplyM0(state) + state.bank.TotalDeposits;
        }

        public static string StabilityLabel(GameState state)
        {
            Ensure(state);
            if (state.bank.Insolvent) return "资不抵债";
            if (state.bank.liquidityCrisis) return "流动性紧张";
            if (state.bank.financialStress >= 60f) return "高风险";
            if (state.bank.financialStress >= 30f) return "注意";
            return "正常";
        }
    }
}
