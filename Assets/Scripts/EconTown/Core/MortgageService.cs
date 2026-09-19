using UnityEngine;

namespace EconTown
{
    /// <summary>房贷权威(V0.9.1《安家》,PRD §193.4):等额本息公式、购房放款、提前还清、法拍。
    /// 每期结算不另设入口——由 CreditService.SettleOneLoan 的房贷分支按本文件公式执行(单一结算入口);
    /// 既有路径是等额本金且从不消费 paymentPerPeriod,不能「复用」只能分支(勘察实证)。
    /// 授信走抵押通道:可贷额=房价×70%,不走 QuoteResidentLoan 收入限额(该限额量级必拒按揭)。</summary>
    public static class MortgageService
    {
        public const float RateSpreadPp = 1.50f;   // 长贷溢价:householdLoanRate+1.50pp,随既有 35% 收敛同步移动
        public const int TermPeriods = 156;        // 156 期=3 个游戏年(52 期/年),「三十年房贷」的游戏压缩(R11)
        public const float DsrWarningLine = 0.50f; // 拒贷警示线
        public const float ForecloseRecoveryRate = 0.85f; // 法拍回收率
        public const float SellDiscount = 0.08f;   // Deflating 卖房强制折价(§196.5)
        public const int SaleDelayPeriods = 3;     // Deflating 卖房房款延迟到账期数(估值×0.85)
        public const string TownBuyerId = "镇买方"; // 卖房接盘者的镇级房贷簿归属(V0.9.3 R23 信用创造)

        /// <summary>等额本息每期供款:P·r·(1+r)^n/((1+r)^n−1),r=PeriodRate(年化百分比)。</summary>
        public static float AnnuityPayment(float principal, float annualRatePercent, int term)
        {
            if (principal <= 0f) return 0f;
            if (term <= 0) return principal;
            float r = InterestRateService.PeriodRate(annualRatePercent);
            if (r <= 0f) return principal / term;
            float f = Mathf.Pow(1f + r, term);
            return principal * r * f / (f - 1f);
        }

        /// <summary>当期应收本金=固定供款−当期利息(期末钳到余额)——供款不变,本金占比逐期上升。</summary>
        public static float AnnuityPrincipalDue(LoanRecord loan, float interestDue)
        {
            float payment = AnnuityPayment(loan.principal, loan.annualInterestRate, loan.term);
            return Mathf.Min(loan.remainingPrincipal, Mathf.Max(0f, payment - interestDue));
        }

        /// <summary>购房:首付三成走存款→卖方(镇住户部门),余额走房贷(抵押通道)。
        /// 复式记账照 IssueLoan 信用创造路径:资产端 loans+principal,负债端卖方存款+principal。</summary>
        public static bool TryBuyHouse(GameState state, out string message)
        {
            HousingService.Ensure(state);
            FinancialSystemService.Ensure(state);
            ResidentEconomy.Ensure(state);
            HousingState h = state.housing;
            message = "";
            if (!h.renting) { message = "已有自住房——本版不做多套房(PRD §198 非目标)。"; return false; }

            float price = h.townHousePrice;
            // V0.9.3 信贷反馈环(§196.1):可贷额=房价×LTVeff(金融压力↑→银行接受的成数↓→首付门槛↑)
            float ltv = HousingService.LtvEff(state);
            float down = price * (1f - ltv);
            float principal = price * ltv;
            // 修复轮回A:双口径同查——储蓄与银行入账并进才放行,防历史脱钩档把负债端记负(照 Withdraw 惯例)。
            if (state.resident.savings < down || state.bank.householdDeposits + .01f < down)
            {
                message = "首付不足:需要 ¥" + down.ToString("0") + "(银行当前接受 " + (ltv * 100f).ToString("0") +
                          "% 成数),当前存款 ¥" + state.resident.savings.ToString("0") +
                          "/银行入账 ¥" + state.bank.householdDeposits.ToString("0") + "。";
                return false;
            }
            if (state.bank.Insolvent) { message = "银行资不抵债,暂不放贷。"; return false; }

            float rate = state.bank.householdLoanRate + RateSpreadPp;
            float payment = AnnuityPayment(principal, rate, TermPeriods);
            float income = HousingService.HouseholdIncome(state);
            float dsr = income > 0f ? payment / income : 2f;
            if (dsr > DsrWarningLine)
            {
                message = "拒贷(警示线):每期供款 ¥" + payment.ToString("0") + ",DSR " + (dsr * 100f).ToString("0") +
                          "% > 50%——先提高收入再买房。";
                return false;
            }

            BankState bank = state.bank;
            // 首付:居民存款 → 卖方(镇住户部门,同本行储户——负债端平移)
            state.resident.savings -= down;
            bank.householdDeposits -= down;
            bank.townSectorDeposits += down;
            // 放款:资产端贷款+principal,负债端卖方存款+principal(不进居民存款——资金有出处有去向)
            LoanRecord loan = new LoanRecord
            {
                id = "loan." + state.day + "." + bank.nextLoanSerial++,
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                purpose = LoanPurpose.Mortgage,
                principal = principal,
                remainingPrincipal = principal,
                annualInterestRate = rate,
                term = TermPeriods,
                remainingTerm = TermPeriods,
                paymentPerPeriod = payment,
                status = LoanStatus.Current,
                collateralValue = price,
                ltvAtOrigination = ltv,
                originatedWorldDay = state.day
            };
            bank.loans.Add(loan);
            bank.townSectorDeposits += principal;
            bank.todayNewCredit += principal;

            h.renting = false;
            h.homeAppraisal = price;
            h.purchasePrice = price;
            h.downPaymentPaid = down;
            h.mortgageLoanId = loan.id;
            h.rentMissed = 0;
            state.UnlockCard("loan");
            state.UnlockCard("creditcreation");
            // V0.9.5 第八篇:放款入账即解锁杠杆五卡
            state.UnlockCard("housing.downpayment");
            state.UnlockCard("housing.installment");
            state.UnlockCard("housing.annuity");
            state.UnlockCard("housing.ltv");
            state.UnlockCard("housing.collateral");
            ProgressionService.RecordSignal(state, ProgressSignals.CreditCreated,
                "房贷放款", "按揭 ¥" + principal.ToString("0") + ",抵押物估值 ¥" + price.ToString("0") +
                ",首付 ¥" + down.ToString("0") + ",每期供款 ¥" + payment.ToString("0"));
            state.Changed();
            message = "成交:首付 ¥" + down.ToString("0") + " + 按揭 ¥" + principal.ToString("0") +
                      "(年化 " + rate.ToString("0.0") + "%," + TermPeriods + " 期,每期供款 ¥" + payment.ToString("0") + ")。";
            return true;
        }

        /// <summary>提前还清:余额一次结清(存款足额才可;利息按期结清,不收违约金——教学最小版)。</summary>
        public static bool TryEarlyPayoff(GameState state, out string message)
        {
            HousingService.Ensure(state);
            message = "";
            LoanRecord loan = HousingService.CurrentMortgage(state);
            if (loan == null) { message = "当前没有未结清的房贷。"; return false; }
            float balance = loan.remainingPrincipal;
            if (balance <= .01f) { message = "贷款已结清。"; return false; }
            // 修复轮回A:双口径同查(照首付守卫)——银行入账不足同样拒绝,绝不把负债端记负。
            if (state.resident.savings < balance || state.bank.householdDeposits + .01f < balance)
            {
                message = "存款不足以一次结清(需 ¥" + balance.ToString("0") + ",银行入账 ¥" +
                          state.bank.householdDeposits.ToString("0") + ")。";
                return false;
            }
            state.resident.savings -= balance;
            state.bank.householdDeposits -= balance;
            loan.remainingPrincipal = 0f;
            loan.remainingTerm = 0;
            loan.totalPrincipalPaid += balance;
            loan.status = LoanStatus.Paid;
            state.housing.mortgageLoanId = "";
            // V0.9.5:提前还清省的是「后面那些期的利息」——摊销卡的解锁时机
            state.UnlockCard("housing.amortization");
            state.Changed();
            message = "提前还清 ¥" + balance.ToString("0") + ",房子完全属于你了。";
            return true;
        }

        /// <summary>卖房(§196.5 变现需要接盘者):None/Inflating 全额即时;Deflating 强制折价 8%、
        /// 房款延迟 3 期到账(走 pendingSale 在途管线)。买方=镇住户的杠杆买家:按揭部分按 LTVeff
        /// 由银行创造(贷款→存款,信用创造,入镇级房贷簿),首付部分动用镇住户存款池——池子凑不出
        /// 首付就卖不掉(变现需要接盘者);玩家贷款余额由买方款项即时销账(照 TryEarlyPayoff
        /// 资产/负债同减口径),折价后不足以覆盖余额的差额照 §193.6 冲银行资本(倒挂卖出)。</summary>
        public static bool TrySellHouse(GameState state, out string message)
        {
            HousingService.Ensure(state);
            message = "";
            HousingState h = state.housing;
            if (h.renting) { message = "当前没有自住房可卖。"; return false; }

            bool deflating = h.leveragePhase == LeveragePhase.Deflating;
            float appraisal = h.homeAppraisal;
            float gross = deflating ? appraisal * (1f - SellDiscount) : appraisal;
            LoanRecord loan = HousingService.CurrentMortgage(state);
            float balance = loan != null ? Mathf.Max(0f, loan.remainingPrincipal) : 0f;

            BankState bank = state.bank;
            // R23(端到端实测修正):买方也是杠杆买家——按揭部分由银行创造(贷款→存款,信用创造),
            // 买方池只出首付。原案全额从池扣,房价涨过池子总额时把池扣穿(gap −5146)——
            // 「全镇的钱买不起这套房」正是流动性约束,解法是让接盘者也加杠杆(本版主题)。
            float buyerLtv = HousingService.LtvEff(state);
            float buyerMortgage = gross * buyerLtv;
            float buyerDown = gross - buyerMortgage;
            if (bank.townSectorDeposits < buyerDown)
            {
                message = "镇住户部门凑不出首付 ¥" + buyerDown.ToString("0") + " 接盘——变现需要接盘者,等买得起的人攒够首付。";
                return false;
            }
            bank.townSectorDeposits += buyerMortgage;   // 信用创造:买方按揭先落成存款
            bank.todayNewCredit += buyerMortgage;
            float rate = bank.householdLoanRate + RateSpreadPp;
            bank.loans.Add(new LoanRecord
            {
                id = "loan." + state.day + "." + bank.nextLoanSerial++,
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = TownBuyerId,
                purpose = LoanPurpose.Mortgage,
                principal = buyerMortgage,
                remainingPrincipal = buyerMortgage,
                annualInterestRate = rate,
                term = TermPeriods,
                remainingTerm = TermPeriods,
                paymentPerPeriod = AnnuityPayment(buyerMortgage, rate, TermPeriods),
                status = LoanStatus.Current,
                collateralValue = appraisal,
                ltvAtOrigination = buyerLtv,
                originatedWorldDay = state.day
            });
            state.UnlockCard("creditcreation");

            float immediate = Mathf.Min(balance, gross);   // 成交时由买方款项替玩家还贷的部分
            if (loan != null)
            {
                bank.townSectorDeposits -= immediate;
                if (gross < balance)
                {
                    float shortfall = balance - gross;   // 倒挂卖出:差额坏账冲银行资本(§193.6 变体)
                    bank.loanLoss += shortfall;
                    bank.totalLoanLoss += shortfall;
                    bank.equity -= shortfall;
                }
                loan.remainingPrincipal = 0f;
                loan.remainingTerm = 0;
                loan.totalPrincipalPaid += immediate;
                loan.status = LoanStatus.Paid;
                h.mortgageLoanId = "";
            }

            float net = Mathf.Max(0f, gross - balance);
            if (deflating)
            {
                // 修复轮回F:单槽覆写前先结清旧在途款——否则第一笔净款被静默蒸发(沉淀镇住户池无主,
                // 面板/日志零提示)。Deflating 期内「卖→买→卖」是下行期割肉抄底再割肉的真实操作。
                if (h.pendingSaleEta > 0f) HousingService.SettlePendingSaleProceeds(state);
                h.pendingSaleProceeds = net;
                h.pendingSaleEta = SaleDelayPeriods;
            }
            else if (net > 0f)
            {
                state.resident.savings += net;
                bank.householdDeposits += net;
                bank.townSectorDeposits -= net;
            }

            // 回租房(照 Foreclose 字段形态,自愿卖出无幸福罚;租金档沿用当前市场)
            h.renting = true;
            h.rentMissed = 0;
            h.homeAppraisal = 0f;
            h.purchasePrice = 0f;
            h.downPaymentPaid = 0f;
            state.Changed();
            message = deflating
                ? "成交(下跌期):折价 8% 卖出 ¥" + gross.ToString("0") + ",净款 ¥" + net.ToString("0") + " " +
                          SaleDelayPeriods + " 期后到账——变现需要接盘者。"
                : "成交:卖出 ¥" + gross.ToString("0") + "(买方按揭 ¥" + buyerMortgage.ToString("0") + " 由银行创造,首付 ¥" +
                          buyerDown.ToString("0") + " 来自镇住户存款)" +
                          (balance > 0f ? ",其中 ¥" + balance.ToString("0") + " 即时还贷" : "") +
                          ",净入 ¥" + net.ToString("0") + "。";
            return true;
        }

        /// <summary>法拍(由 SettleOneLoan 的连续 3 期欠款路径调用):回收=min(估值×85%,余额),
        /// 差额坏账冲银行资本(照 DefaultLoan 30/70 框架的有抵押变体);居民回租房,幸福 −12。</summary>
        public static void Foreclose(GameState state, LoanRecord loan)
        {
            HousingService.Ensure(state);
            HousingState h = state.housing;
            float outstanding = Mathf.Max(0f, loan.remainingPrincipal);
            float recovery = Mathf.Min(h.homeAppraisal * ForecloseRecoveryRate, outstanding);
            float loss = outstanding - recovery;
            state.bank.reserves += recovery;
            state.bank.loanLoss += loss;
            state.bank.totalLoanLoss += loss;
            state.bank.equity -= loss;
            loan.remainingPrincipal = 0f;
            loan.remainingTerm = 0;
            loan.status = LoanStatus.Defaulted;
            h.renting = true;
            h.rentMissed = 0;
            h.mortgageLoanId = "";
            h.homeAppraisal = 0f;
            h.purchasePrice = 0f;
            h.downPaymentPaid = 0f;
            h.foreclosureFlash = 26;   // 铭牌「法拍」态余晖(R23):亮半年后散
            if (state.resident != null)
                state.resident.happiness = Mathf.Max(0f, state.resident.happiness - 12f);
            state.Changed();
        }

        /// <summary>镇级法拍(修复轮回B:镇买方断供 3 期):回收=min(放款时抵押估值×85%,余额),差额冲银行资本;
        /// 只核销镇级房贷簿——不触碰玩家住房状态/幸福/法拍铭牌(与玩家版 Foreclose 的本质差别)。</summary>
        public static void ForecloseTownBuyer(GameState state, LoanRecord loan)
        {
            float outstanding = Mathf.Max(0f, loan.remainingPrincipal);
            float recovery = Mathf.Min(Mathf.Max(0f, loan.collateralValue) * ForecloseRecoveryRate, outstanding);
            float loss = outstanding - recovery;
            state.bank.reserves += recovery;
            state.bank.loanLoss += loss;
            state.bank.totalLoanLoss += loss;
            state.bank.equity -= loss;
            loan.remainingPrincipal = 0f;
            loan.remainingTerm = 0;
            loan.status = LoanStatus.Defaulted;
            state.Changed();
        }
    }
}
