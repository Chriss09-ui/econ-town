using UnityEngine;

namespace EconTown
{
    /// <summary>V0.6 存款、信贷、偿还、违约与流动性操作的双边记账入口。</summary>
    public static class CreditService
    {
        public static void Ensure(GameState state) { InterestRateService.Ensure(state); }

        // V0.9.3:镇买方按揭同属 Resident 类但不是玩家的债——玩家口径按 borrowerId 归属过滤
        public static float ResidentDebt(GameState state)
        {
            return Debt(state, LoanBorrowerType.Resident,
                state.resident != null ? state.resident.residentName : null);
        }
        public static float CompanyDebt(GameState state) { return Debt(state, LoanBorrowerType.Company); }

        public static float AverageLoanRate(GameState state, LoanBorrowerType borrower, string borrowerId = null)
        {
            Ensure(state);
            float weightedRate = 0f;
            float principal = 0f;
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan == null || loan.status != LoanStatus.Current || loan.borrowerType != borrower ||
                    (borrowerId != null && loan.borrowerId != borrowerId &&
                     !string.IsNullOrEmpty(loan.borrowerId))) continue;
                float balance = Mathf.Max(0f, loan.remainingPrincipal);
                weightedRate += balance * loan.annualInterestRate;
                principal += balance;
            }
            return principal <= .001f ? 0f : weightedRate / principal;
        }

        public static float ResidentCreditScore(GameState state)
        {
            Ensure(state);
            ResidentState resident = state.resident;
            float assets = Mathf.Max(1f, resident.cash + resident.savings);
            float leverage = ResidentDebt(state) / assets;
            float employment = resident.employedToday ? 35f : Mathf.Max(0f, 20f - resident.unemployedDays * 3f);
            return Mathf.Clamp(620f + employment + resident.skillLevel * 18f - leverage * 150f -
                MissedPayments(state, LoanBorrowerType.Resident, resident.residentName) * 45f,
                300f, 850f);
        }

        public static float CompanyCreditScore(GameState state)
        {
            Ensure(state);
            float debt = CompanyDebt(state);
            float assets = Mathf.Max(1f, Mathf.Max(0f, state.money) + state.bank.companyDeposits + state.inventory * Mathf.Max(1f, state.price));
            float leverage = debt / assets;
            float profit = state.LastRecord != null ? Mathf.Clamp(state.LastRecord.profit / 20f, -80f, 80f) : 0f;
            return Mathf.Clamp(650f + profit + state.playerBrand * .35f - leverage * 180f -
                MissedPayments(state, LoanBorrowerType.Company) * 55f, 300f, 850f);
        }

        public static CreditQuote QuoteResidentLoan(GameState state, float amount, int term = 12)
        {
            Ensure(state);
            float score = ResidentCreditScore(state);
            float stableIncome = state.resident.employedToday ? Mathf.Max(20f, state.resident.dailyIncome) : 10f;
            float limit = Mathf.Max(0f, stableIncome * 12f + state.resident.savings * .35f - ResidentDebt(state) * .7f);
            return BuildQuote(state, LoanBorrowerType.Resident, LoanPurpose.Consumption, amount, term, score, limit);
        }

        public static CreditQuote QuoteCompanyLoan(GameState state, float amount, LoanPurpose purpose, int term = 24)
        {
            Ensure(state);
            float score = CompanyCreditScore(state);
            float profitCapacity = state.LastRecord != null ? Mathf.Max(0f, state.LastRecord.profit) * 12f : 1200f;
            float limit = Mathf.Max(0f, 1800f + profitCapacity + Mathf.Max(0f, state.money) * .45f - CompanyDebt(state) * .55f);
            if (purpose == LoanPurpose.EquipmentInvestment) limit += 3500f;
            return BuildQuote(state, LoanBorrowerType.Company, purpose, amount, term, score, limit);
        }

        public static bool DepositResidentCash(GameState state, float amount, out string message)
        {
            message = null;
            Ensure(state);
            amount = Mathf.Floor(Mathf.Max(0f, amount));
            if (amount <= 0f || state.resident.cash + .001f < amount)
            {
                message = "手头现金不足，无法存入该金额。";
                return false;
            }
            state.resident.cash -= amount;
            state.resident.savings += amount;
            state.resident.todaySaved += amount;
            state.resident.savingsActions++;
            state.bank.reserves += amount;
            state.bank.householdDeposits += amount;
            ProgressionService.RecordSignal(state, ProgressSignals.BankDepositMade,
                "居民存入银行", "居民现金 -¥" + amount.ToString("0") + "；银行准备金和存款负债同时 +¥" + amount.ToString("0"));
            state.UnlockCard("bankdeposit");
            state.Changed();
            message = "已存入 ¥" + amount.ToString("0") + "。这是你的资产，也是银行对你的负债。";
            return true;
        }

        public static bool WithdrawResidentDeposit(GameState state, float amount, out string message)
        {
            message = null;
            Ensure(state);
            amount = Mathf.Floor(Mathf.Max(0f, amount));
            if (amount <= 0f || state.resident.savings + .001f < amount || state.bank.householdDeposits + .001f < amount)
            {
                message = "存款余额不足。";
                return false;
            }
            if (state.bank.reserves + .001f < amount)
            {
                state.bank.liquidityCrisis = true;
                message = "银行总资产可能仍然充足，但当前准备金不足：这是流动性问题。";
                return false;
            }
            state.resident.savings -= amount;
            state.resident.cash += amount;
            state.resident.todayEmergencyWithdrawal += amount;
            state.bank.householdDeposits -= amount;
            state.bank.reserves -= amount;
            state.Changed();
            message = "已取出 ¥" + amount.ToString("0") + "，银行准备金与存款负债同时减少。";
            return true;
        }

        public static LoanRecord IssueResidentLoan(GameState state, float amount, int term, out string message)
        {
            CreditQuote quote = QuoteResidentLoan(state, amount, term);
            return IssueLoan(state, quote, LoanBorrowerType.Resident, LoanPurpose.Consumption, out message);
        }

        public static LoanRecord IssueCompanyLoan(GameState state, float amount, LoanPurpose purpose, int term, out string message)
        {
            CreditQuote quote = QuoteCompanyLoan(state, amount, purpose, term);
            return IssueLoan(state, quote, LoanBorrowerType.Company, purpose, out message);
        }

        public static bool TransferCompanyDepositToCash(GameState state, float amount, out string message)
        {
            message = null;
            Ensure(state);
            amount = Mathf.Floor(Mathf.Max(0f, amount));
            if (amount <= 0f || state.bank.companyDeposits + .001f < amount)
            {
                message = "企业银行账户余额不足。";
                return false;
            }
            if (state.bank.reserves + .001f < amount)
            {
                state.bank.liquidityCrisis = true;
                message = "银行准备金不足，暂时无法把这笔存款转成企业现金。";
                return false;
            }
            state.bank.companyDeposits -= amount;
            state.bank.reserves -= amount;
            state.money += amount;
            state.Changed();
            message = "企业账户转出 ¥" + amount.ToString("0") + " 用于经营周转。";
            return true;
        }

        public static bool FinanceEquipmentInvestment(GameState state, float cost, out string message)
        {
            message = null;
            Ensure(state);
            cost = Mathf.Floor(Mathf.Max(0f, cost));
            if (cost <= 0f || state.bank.companyDeposits + .001f < cost)
            {
                message = "企业账户余额不足，需先申请设备投资贷款或积累资金。";
                return false;
            }
            if (state.bank.reserves + .001f < cost)
            {
                state.bank.liquidityCrisis = true;
                message = "银行流动性不足，设备款暂时无法对外支付。";
                return false;
            }
            state.bank.companyDeposits -= cost;
            state.bank.reserves -= cost;
            state.companyCapitalInvestment += cost;
            state.pendingFinancedInvestment += cost;
            state.financedEquipmentCapacityBonus = Mathf.Min(300, state.financedEquipmentCapacityBonus + Mathf.RoundToInt(cost / 100f));
            state.bank.todayCompanyInvestment += cost;
            ProgressionService.RecordSignal(state, ProgressSignals.CompanyInvestmentFinanced,
                "借款购买设备", "投资 ¥" + cost.ToString("0") + "，贷款利率 " + state.bank.companyLoanRate.ToString("0.0") + "%");
            state.UnlockCard("interestinvestment");
            state.Changed();
            message = "设备投资已完成，产能上限提高；未偿贷款仍会在未来持续产生利息。";
            return true;
        }

        public static void AccrueDepositInterest(GameState state)
        {
            Ensure(state);
            BankState bank = state.bank;
            float householdInterest = bank.householdDeposits * InterestRateService.PeriodRate(bank.depositRate);
            float companyInterest = bank.companyDeposits * InterestRateService.PeriodRate(bank.depositRate);
            float total = householdInterest + companyInterest;
            if (total <= 0f) return;
            bank.householdDeposits += householdInterest;
            state.resident.savings += householdInterest;
            state.resident.interestIncome += householdInterest;
            state.resident.todayInterestIncome += householdInterest;
            bank.companyDeposits += companyInterest;
            bank.interestExpense += total;
            bank.totalInterestExpense += total;
            bank.equity -= total;
        }

        public static void SettleLoanPayments(GameState state)
        {
            Ensure(state);
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan == null || loan.status != LoanStatus.Current) continue;
                SettleOneLoan(state, loan);
            }
        }

        public static bool DefaultLoan(GameState state, LoanRecord loan, float recoveryRate = .30f)
        {
            Ensure(state);
            if (loan == null || loan.status != LoanStatus.Current) return false;
            float outstanding = Mathf.Max(0f, loan.remainingPrincipal);
            float recovery = outstanding * Mathf.Clamp01(recoveryRate);
            float loss = outstanding - recovery;
            state.bank.reserves += recovery;
            state.bank.loanLoss += loss;
            state.bank.totalLoanLoss += loss;
            state.bank.equity -= loss;
            loan.remainingPrincipal = 0f;
            loan.remainingTerm = 0;
            loan.status = LoanStatus.Defaulted;
            state.Changed();
            return true;
        }

        public static float TriggerBankRun(GameState state, float withdrawalRatio)
        {
            Ensure(state);
            BankState bank = state.bank;
            float requested = bank.householdDeposits * Mathf.Clamp01(withdrawalRatio);
            float paid = Mathf.Min(requested, Mathf.Min(bank.reserves, state.resident.savings));
            bank.householdDeposits -= paid;
            state.resident.savings -= paid;
            state.resident.cash += paid;
            bank.reserves -= paid;
            bank.bankRunUnmetWithdrawals = Mathf.Max(0f, requested - paid);
            bank.bankRunActive = true;
            bank.liquidityCrisis = bank.bankRunUnmetWithdrawals > .01f || bank.ActualReserveRatio < bank.requiredReserveRatio;
            ProgressionService.RecordSignal(state, ProgressSignals.BankRunTriggered,
                "银行挤兑实验", "申请提款 ¥" + requested.ToString("0") + "，未满足 ¥" + bank.bankRunUnmetWithdrawals.ToString("0"));
            state.UnlockCard("bankrun");
            state.Changed();
            return paid;
        }

        public static bool ProvideEmergencyLiquidity(GameState state, float amount, out string message)
        {
            message = null;
            Ensure(state);
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f) { message = "紧急流动性金额必须大于 0。"; return false; }
            state.bank.reserves += amount;
            state.bank.centralBankBorrowing += amount;
            state.centralBank.emergencyLending += amount;
            float unmet = Mathf.Min(state.bank.bankRunUnmetWithdrawals,
                Mathf.Min(state.bank.reserves, state.resident.savings));
            if (unmet > .001f)
            {
                state.bank.householdDeposits -= unmet;
                state.resident.savings -= unmet;
                state.resident.cash += unmet;
                state.bank.reserves -= unmet;
                state.bank.bankRunUnmetWithdrawals -= unmet;
            }
            state.bank.liquidityCrisis = state.bank.reserves + .01f < state.bank.RequiredReserves;
            bool resolved = state.bank.bankRunUnmetWithdrawals <= .01f && !state.bank.liquidityCrisis;
            state.bank.bankRunActive = !resolved;
            if (resolved)
                ProgressionService.RecordSignal(state, ProgressSignals.BankRunResolved,
                    "最后贷款人提供流动性", "央行向银行贷款 ¥" + amount.ToString("0"));
            state.UnlockCard("lenderoflastresort");
            state.Changed();
            message = "央行向银行提供 ¥" + amount.ToString("0") + " 准备金，并兑付未完成提款 ¥" + unmet.ToString("0") +
                "。" + (resolved ? "流动性危机已缓解。" : "银行仍存在流动性缺口。") +
                "这不会自动补回银行的资产损失。";
            return true;
        }

        public static float PurchaseGovernmentBonds(GameState state, float issuance, float annualRate)
        {
            Ensure(state);
            float purchase = Mathf.Min(Mathf.Max(0f, issuance), state.bank.ExcessReserves);
            if (purchase <= .001f) return 0f;
            state.bank.reserves -= purchase;
            state.bank.bonds.Add(new GovernmentBondLot
            {
                id = "bond." + state.day + "." + state.bank.nextBondSerial++,
                principal = purchase,
                annualInterestRate = annualRate,
                issuedWorldDay = state.day,
                remainingTerm = 260
            });
            return purchase;
        }

        public static void ReceiveGovernmentBondInterest(GameState state, float totalGovernmentInterest, float governmentDebt)
        {
            Ensure(state);
            if (totalGovernmentInterest <= 0f || governmentDebt <= .001f) return;
            float bonds = state.bank.GovernmentBonds;
            float treasuryFace = state.housing != null ? state.housing.treasuryFace : 0f;

            // V0.9.5 付息拆分(§197.5)+修复轮回E:居民持债份额付居民,银行份额照旧。
            // 分母=持券总额归一——政府盈余还本不注销持券(bonds/treasuryFace 只增不减),debt 可被压到
            // 持券总额之下;旧钳法 share=bonds/debt 饱和 1 会把居民份额钳成 0(利息全额错付银行,与面板
            // 「政府的每一期付息,都有你的一份」承诺相反)。两腿按持券占比分蛋糕,合并恒=1:既不溢出
            // 多付息,也没有「先到先得」挤占。付息人是政府:对方科目=准备金(资产/负债同加,BalanceGap
            // 不变),镜像 AccrueDepositInterest 双边。
            float denom = bonds + treasuryFace;
            if (denom <= .001f) return;
            // 围猎三(#3):认购不足的「无主债务」利息不出表——银行认购被钳在超额准备金,而
            // government.debt 按发债全额入账,旧拆分(share+residentShare≡1)把无主部分的利息
            // 也 100% 付给持券人,凭空进 bank.reserves/equity。coverage 把付息缩到持券人实际
            // 持有比例;debt ≤ 持券总额(盈余还本方向)时 coverage=1,修复轮回E 行为不变。
            float coverage = Mathf.Clamp01(denom / governmentDebt);
            float share = Mathf.Clamp01(bonds / denom);
            float residentShare = Mathf.Clamp01(treasuryFace / denom);
            if (residentShare > 0f)
            {
                float residentIncome = totalGovernmentInterest * residentShare * coverage;
                state.bank.reserves += residentIncome;
                state.bank.householdDeposits += residentIncome;
                state.resident.savings += residentIncome;
                state.resident.interestIncome += residentIncome;
                state.resident.todayInterestIncome += residentIncome;
            }

            float income = totalGovernmentInterest * share * coverage;
            state.bank.reserves += income;
            state.bank.interestIncome += income;
            state.bank.totalInterestIncome += income;
            state.bank.equity += income;
        }

        static LoanRecord IssueLoan(GameState state, CreditQuote quote, LoanBorrowerType borrower,
            LoanPurpose purpose, out string message)
        {
            message = quote.reason;
            if (!quote.approved || quote.approvedAmount <= 0f) return null;
            BankState bank = state.bank;
            float principal = quote.approvedAmount;
            LoanRecord loan = new LoanRecord
            {
                id = "loan." + state.day + "." + bank.nextLoanSerial++,
                borrowerType = borrower,
                borrowerId = borrower == LoanBorrowerType.Resident ? state.resident.residentName : "player.company",
                purpose = purpose,
                principal = principal,
                remainingPrincipal = principal,
                annualInterestRate = quote.finalRate,
                term = quote.term,
                remainingTerm = quote.term,
                paymentPerPeriod = principal / Mathf.Max(1, quote.term) + principal * InterestRateService.PeriodRate(quote.finalRate),
                status = LoanStatus.Current,
                defaultProbability = quote.defaultProbability,
                originatedWorldDay = state.day
            };
            bank.loans.Add(loan);
            if (borrower == LoanBorrowerType.Resident)
            {
                bank.householdDeposits += principal;
                state.resident.savings += principal;
                state.UnlockCard("loan");
            }
            else bank.companyDeposits += principal;
            bank.todayNewCredit += principal;
            state.UnlockCard("creditcreation");
            ProgressionService.RecordSignal(state, ProgressSignals.CreditCreated,
                "银行发放贷款", "贷款资产与借款人存款同时 +¥" + principal.ToString("0"));
            state.Changed();
            message = "批准 ¥" + principal.ToString("0") + "，年化利率 " + quote.finalRate.ToString("0.0") +
                "%。贷款增加当前购买力，也同时增加等额债务。";
            return loan;
        }

        static CreditQuote BuildQuote(GameState state, LoanBorrowerType borrower, LoanPurpose purpose,
            float amount, int term, float score, float borrowerLimit)
        {
            amount = Mathf.Floor(Mathf.Max(0f, amount));
            term = Mathf.Clamp(term, 2, 156);   // V0.9.1:放宽至 156 期承接房贷(PRD §193.4,登记)
            float reserveRoom = state.bank.requiredReserveRatio <= .001f ? amount :
                state.bank.ExcessReserves / state.bank.requiredReserveRatio;
            float approved = Mathf.Min(amount, Mathf.Min(borrowerLimit, reserveRoom));
            float risk = score >= 760f ? .5f : score >= 680f ? 1.5f : score >= 600f ? 3f : 6f;
            float termPremium = term <= 12 ? 1f : term <= 26 ? 1.5f : 2.5f;
            float liquidity = state.bank.ActualReserveRatio < state.bank.requiredReserveRatio * 1.25f ? 1.5f : 0f;
            float baseRate = borrower == LoanBorrowerType.Resident ? state.bank.householdLoanRate : state.bank.companyLoanRate;
            float final = Mathf.Clamp(baseRate + risk + termPremium + liquidity, 1f, 35f);
            bool approvedFlag = amount > 0f && approved >= Mathf.Min(amount, 100f) && score >= 430f && !state.bank.Insolvent;
            return new CreditQuote
            {
                approved = approvedFlag,
                requestedAmount = amount,
                approvedAmount = approvedFlag ? approved : 0f,
                baseRate = baseRate,
                riskPremium = risk,
                termPremium = termPremium,
                liquidityPremium = liquidity,
                finalRate = final,
                defaultProbability = Mathf.Clamp01((700f - score) / 600f),
                term = term,
                grade = score >= 760 ? "A" : score >= 680 ? "B+" : score >= 600 ? "B" : score >= 520 ? "C" : "D",
                reason = approvedFlag ? "银行根据现金流、负债率、信用评分和准备金确定报价。" :
                    "贷款未批准：信用、偿债能力或银行准备金不足。"
            };
        }

        static void SettleOneLoan(GameState state, LoanRecord loan)
        {
            // 修复轮回B:镇买方按揭(TrySellHouse 的信用创造对手方)不是玩家的债(文件头注释与 ResidentDebt
            // 的 borrowerId 归属口径)——月供走镇住户部门存款池,断供走镇级核销,玩家账户/住房零污染。
            if (loan.borrowerType == LoanBorrowerType.Resident && loan.borrowerId == MortgageService.TownBuyerId)
            {
                SettleTownBuyerLoan(state, loan);
                return;
            }
            float interestDue = loan.remainingPrincipal * InterestRateService.PeriodRate(loan.annualInterestRate);
            // V0.9.1:房贷走等额本息(固定供款,本金占比逐期升),其余贷款维持等额本金。
            // 单一结算入口不动——公式权威在 MortgageService(勘察实证:既有路径从不消费 paymentPerPeriod)。
            float principalDue = loan.purpose == LoanPurpose.Mortgage
                ? MortgageService.AnnuityPrincipalDue(loan, interestDue)
                : Mathf.Min(loan.remainingPrincipal, loan.principal / Mathf.Max(1, loan.term));
            float due = interestDue + principalDue;
            float availableDeposit = loan.borrowerType == LoanBorrowerType.Resident ?
                state.bank.householdDeposits : state.bank.companyDeposits;
            float availableCash = loan.borrowerType == LoanBorrowerType.Resident ?
                Mathf.Max(0f, state.resident.cash) : Mathf.Max(0f, state.money);
            float payment = Mathf.Min(due, availableDeposit + availableCash);

            float fromDeposit = Mathf.Min(payment, availableDeposit);
            if (loan.borrowerType == LoanBorrowerType.Resident)
            {
                state.bank.householdDeposits -= fromDeposit;
                state.resident.savings = Mathf.Max(0f, state.resident.savings - fromDeposit);
            }
            else state.bank.companyDeposits -= fromDeposit;

            float fromCash = payment - fromDeposit;
            if (loan.borrowerType == LoanBorrowerType.Resident)
            {
                state.resident.cash -= fromCash;
                state.bank.todayResidentLoanPaymentFromCash += fromCash;   // 围猎五(#4)
                state.resident.interestExpense += Mathf.Min(payment, interestDue);
                state.resident.todayInterestExpense += Mathf.Min(payment, interestDue);
            }
            else
            {
                state.money -= fromCash;
                state.bank.todayCompanyLoanPaymentFromCash += fromCash;    // 围猎五(#4)
                float companyInterest = Mathf.Min(payment, interestDue);
                state.companyInterestExpense += companyInterest;
                state.todayCompanyInterestExpense += companyInterest;
                state.bank.todayLoanInterestPaidCompany += companyInterest;   // 围猎五(#4)复审
            }
            state.bank.reserves += fromCash;

            float interestPaid = Mathf.Min(payment, interestDue);
            float principalPaid = Mathf.Min(principalDue, Mathf.Max(0f, payment - interestPaid));
            loan.remainingPrincipal = Mathf.Max(0f, loan.remainingPrincipal - principalPaid);
            loan.remainingTerm = Mathf.Max(0, loan.remainingTerm - 1);
            loan.totalPrincipalPaid += principalPaid;
            loan.totalInterestPaid += interestPaid;
            state.bank.interestIncome += interestPaid;
            state.bank.totalInterestIncome += interestPaid;
            state.bank.equity += interestPaid;
            if (loan.purpose == LoanPurpose.Mortgage)
            {
                // V0.9.1 住房账本行(R18):当日供款的本金/利息分解,供面板与守恒断言读取。
                DayRecord rec = state.LastRecord;
                if (rec != null && rec.day == state.day)
                {
                    rec.mortgagePrincipalPaid += principalPaid;
                    rec.mortgageInterestPaid += interestPaid;
                }
            }
            loan.missedPayments = payment + .01f < due ? loan.missedPayments + 1 : 0;
            if (loan.remainingPrincipal <= .01f)
            {
                loan.remainingPrincipal = 0f;
                loan.status = LoanStatus.Paid;
            }
            else if (loan.missedPayments >= 3)
            {
                // 修复轮回B:法拍只作用于玩家自己的在贷按揭(loan.id==housing.mortgageLoanId)——
                // 归属不明的 Mortgage 贷款走 DefaultLoan,绝不误清玩家住房状态/幸福/法拍铭牌。
                if (loan.purpose == LoanPurpose.Mortgage &&
                    state.housing != null && loan.id == state.housing.mortgageLoanId)
                    MortgageService.Foreclose(state, loan);   // 有抵押变体:回收=min(估值×85%,余额)
                else DefaultLoan(state, loan);
            }
        }

        /// <summary>镇买方按揭结算(修复轮回B):月供全额从镇住户部门存款池划扣(不足记断供),
        /// 利息进银行利息收入、本金核减贷款资产——ΔA−ΔL−ΔE≡0;
        /// 不写玩家储蓄/现金/利息费用/住房日报表(信用创造入镇级房贷簿,玩家口径零污染)。</summary>
        static void SettleTownBuyerLoan(GameState state, LoanRecord loan)
        {
            float interestDue = loan.remainingPrincipal * InterestRateService.PeriodRate(loan.annualInterestRate);
            float principalDue = MortgageService.AnnuityPrincipalDue(loan, interestDue);
            float due = interestDue + principalDue;
            float payment = Mathf.Min(due, Mathf.Max(0f, state.bank.townSectorDeposits));
            state.bank.townSectorDeposits -= payment;
            float interestPaid = Mathf.Min(payment, interestDue);
            float principalPaid = Mathf.Min(principalDue, Mathf.Max(0f, payment - interestPaid));
            loan.remainingPrincipal = Mathf.Max(0f, loan.remainingPrincipal - principalPaid);
            loan.remainingTerm = Mathf.Max(0, loan.remainingTerm - 1);
            loan.totalPrincipalPaid += principalPaid;
            loan.totalInterestPaid += interestPaid;
            state.bank.interestIncome += interestPaid;
            state.bank.totalInterestIncome += interestPaid;
            state.bank.equity += interestPaid;
            loan.missedPayments = payment + .01f < due ? loan.missedPayments + 1 : 0;
            if (loan.remainingPrincipal <= .01f)
            {
                loan.remainingPrincipal = 0f;
                loan.status = LoanStatus.Paid;
            }
            else if (loan.missedPayments >= 3) MortgageService.ForecloseTownBuyer(state, loan);
        }

        static float Debt(GameState state, LoanBorrowerType borrower, string borrowerId = null)
        {
            Ensure(state);
            float total = 0f;
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                // 围猎三复审:旧档(V0.9.3 前)玩家贷款无 borrowerId(null)——按玩家归属过滤时
                // 视为玩家持有,不再漏计(镇买方 id 恒非空,不会误并)。
                if (loan != null && loan.borrowerType == borrower && loan.status == LoanStatus.Current &&
                    (borrowerId == null || loan.borrowerId == borrowerId || string.IsNullOrEmpty(loan.borrowerId)))
                    total += Mathf.Max(0f, loan.remainingPrincipal);
            }
            return total;
        }

        // 围猎三(#6):补 borrowerId 过滤(照 Debt 口径)——镇买方按揭同属 Resident 类,
        // 其欠款/利率不得混进玩家信用分与平均利率展示。
        static int MissedPayments(GameState state, LoanBorrowerType borrower, string borrowerId = null)
        {
            int total = 0;
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan != null && loan.borrowerType == borrower && loan.status == LoanStatus.Current &&
                    (borrowerId == null || loan.borrowerId == borrowerId || string.IsNullOrEmpty(loan.borrowerId)))
                    total += loan.missedPayments;
            }
            return total;
        }
    }
}
