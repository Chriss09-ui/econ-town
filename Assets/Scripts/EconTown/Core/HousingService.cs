using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>住房系统权威(V0.9.1《安家》,PRD §191.2/§193):状态块 Ensure、每期住房日结、DSR 读数。
    /// 纪律:零新随机流(严禁碰 random/tradeRandom/atmosphereRandom);只有玩家居民一户有个体住房状态,
    /// 其余住户全部入统计部门(townSectorDeposits)。</summary>
    public static class HousingService
    {
        public const float BaseHousePrice = 42000f;   // 房价指数基期(=100,R14)
        public const float DownPaymentRatio = 0.30f;  // 首付三成(LTV 上限 70%)

        // —— V0.9.2 房价/租金引擎系数(PRD §194.2-194.3;校准唯一入口=收口A 系数表)——
        // R21(as-built 校准实测):P=玩家口径收入在真实对局多钳下限(店员+微利玩家收入≈170/期,
        // P_raw≈0.28),镇级需求被单一玩家压死、房价每期打 −3% 钳位——钳位收紧到 [0.7,1.4]
        // (玩家=边际买家,单人波动对全镇冲击限 2 倍),κ 由 0.015 上调(R22 折旧复校后终值 0.0215)。
        public const float DemandScaleKappa = 0.0215f;
        public const float PowerClampMin = 0.7f;       // P 钳位(R21 修订,原 [0.3,2.5])
        public const float PowerClampMax = 1.4f;
        public const float PriceStepCoef = 0.06f;       // Δ房价系数
        public const float PriceStepClamp = 0.03f;      // 每期涨幅钳 ±3%
        // R22(as-built 长跑实测):开工固定 0.5/期而存量无限累积,150 期供给 120→195 把房价指数压到 36
        // (租金 140→55、CPI 住房分项钉下限)。存量折旧使长期平衡=starts/折旧≈125 套,泡沫期开工上浮
        // 时平衡点随之上移、破裂后回落——「供给响应缓慢」的正确实现。
        public const float StockDepreciation = 0.004f;
        public const float RentConvergence = 0.15f;     // 租金每期向隐含期租收敛 15%
        public const float RentWageElasticity = 0.5f;   // 实际工资增速弹性
        public const float RentStepClamp = 0.02f;       // 每期租金钳 ±2%
        public const float ImpliedRentDivisor = 6f * 52f; // 隐含期租=房价/(6×52)(R15:P/R=6 锁定)
        public const float BasePeriodRent = 140f;       // 租金指数基期(rentIndex=periodRent/140×100)

        // —— V0.9.3 杠杆周期系数(PRD §196;校准唯一入口=收口A 系数表)——
        // R23(校准落定,诊断实测):中性世界短窗年化噪声可到 68%/年(devZ 一度饱和→假 bp30),
        // 而真实强泡沫(低利率+高收入 104 期价 +265%)prZ 只到 0.29(租金收敛 15%/期使 P/R 偏移
        // 结构性 ≤~0.3)、mgdpZ 玩家口径恒≈0——原权重/基准下 bp 封顶 ~42,Inflating 不可达。
        // 落定:devZ 需 26 期完整窗口(短窗噪声不认定泡沫)+权重向动能倾斜 .25/.2/.55+
        // devZ 基准 20%/量程 20%(满格=年化 ≥40%,正常复苏 ~30%/年 不误报);阈 60/40 不动
        // ——devZ 满格 55 分,还需 P/R 拉伸才过 60:涨速与估值偏离双确认才是泡沫。
        public const float BpWeightPr = 0.25f;          // 泡沫三指标权重:房价年租比
        public const float BpWeightMgdp = 0.2f;         // 房贷/年化GDP
        public const float BpWeightDev = 0.55f;         // 房价动能(本镇泡沫主导项,R23)
        public const float PrBaseline = 6f;             // prZ 基准:房价/年租比=6(与 R15 锁定同源)
        public const float MgdpBaseline = 0.6f;         // mgdpZ 基准:房贷/(nominalGdp×52)
        public const float MgdpRange = 0.4f;
        public const float DevBaseline = 0.20f;         // devZ 基准:26 期年化涨幅 20%(R23,原 10%)
        public const float DevRange = 0.20f;            // devZ 量程:满格=年化 40%(R23,原 15%)
        public const float InflatingThreshold = 60f;    // bp≥60 进 Inflating(新开工上浮)
        public const float CleanupPressure = 40f;       // 回撤后压力回落至此以下方可 Cleanup
        public const float CrashDrawdown = 0.08f;       // 峰值回撤 ≥8% 判崩盘
        public const float SoftLandingPressure = 20f;   // 软着陆出口:泡沫自行消散(R23,防相位卡死)
        public const float DeflatingDemandPenalty = 0.05f; // Deflating 需求罚:bp/100×0.05/期
        public const float ReversalRateHike = 2f;       // 触发①政策利率较 26 期最低抬升 ≥2pp
        public const float ReversalJobHike = 2f;        // 触发②失业率较 26 期最低抬升 ≥2p.p.
        public const int ReversalLookback = 26;

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            if (state.housing == null) state.housing = new HousingState();
            HousingState h = state.housing;
            if (state.bank != null) state.bank.townSectorDeposits = Mathf.Max(0f, state.bank.townSectorDeposits);
            h.periodRent = Mathf.Max(0f, h.periodRent);
            h.townHousePrice = Mathf.Max(1000f, h.townHousePrice);
            h.fundCash = Mathf.Max(0f, h.fundCash);
            h.initialized = true;
        }

        /// <summary>住房日结:租金/欠租/搬离/在途房款到账。插位=合约结算后、居民收日前(R1),日报表能读到今日租金。</summary>
        public static void SettleDay(GameState state)
        {
            Ensure(state);
            if (state.resident == null || state.bank == null) return;
            HousingState h = state.housing;
            // 修复轮回D(同族):期账末条=今日 ⇒ 收日链已完整跑过本日(读档重放日)——租金/在途房款不再
            // 重复计提(与 AdvancePeriod 幂等闸同一标记;测试直调世界期账为空/重播种,不受影响)。
            if (h.history.Count > 0 && h.history[h.history.Count - 1].period == state.day) return;
            DayRecord rec = state.LastRecord;
            bool sameDay = rec != null && rec.day == state.day;

            if (h.renting && h.periodRent > 0f)
            {
                float rent = h.periodRent;
                // 修复轮回A:双口径同查(照 WithdrawResidentDeposit 惯例)——储蓄与银行入账并进才放行,
                // 防历史脱钩档把 householdDeposits 记负(负债端 Max(0,·) 钳位会截断、破 BalanceGap)。
                if (state.resident.savings + .01f < rent || state.bank.householdDeposits + .01f < rent)
                {
                    // 租金只走存款(R24 as-built,修订 PRD §193.2 原案「不足则 cash 补」):存款不足即欠租
                    h.rentMissed++;
                    if (h.rentMissed >= 3)
                    {
                        // 被迫搬离:幸福 −8、计数清零、重置租金档
                        h.rentMissed = 0;
                        state.resident.happiness = Mathf.Max(0f, state.resident.happiness - 8f);
                    }
                }
                else
                {
                    state.resident.savings -= rent;
                    // 房东同为本行储户:住户内部转移,负债端 household→townSector 平移,银行总量不变(平衡表零扰动)
                    state.bank.householdDeposits -= rent;
                    state.bank.townSectorDeposits += rent;
                    h.rentMissed = 0;
                    if (sameDay) rec.rentPaid += rent;
                    // V0.9.5 卡解锁:付过真金白银的租金,才配谈「租还是买」与租金收益率
                    state.UnlockCard("housing.rentVsBuy");
                    state.UnlockCard("housing.capRate");
                }
            }

            // 在途房款到账(V0.9.3 卖房约束消费;字段与到账路径 V0.9.1 先立)
            if (h.pendingSaleEta > 0f)
            {
                h.pendingSaleEta -= 1f;
                if (h.pendingSaleEta <= 0f) SettlePendingSaleProceeds(state);
            }
            state.Changed();
        }

        /// <summary>在途房款三腿到账(储蓄/存款负债+、镇住户池−)——SettleDay 到期路径与
        /// 修复轮回F(TrySellHouse 覆写前结旧)共用,单一到账口径。</summary>
        public static void SettlePendingSaleProceeds(GameState state)
        {
            HousingState h = state.housing;
            float proceeds = h.pendingSaleProceeds;
            // 围猎三(#2):镇住户池可能已被镇买方按揭月供抽低(在途款并非隔离资金),钳位扣减
            // 少扣的差额必须由权益腿吸收——否则负债净增无资产对应,BalanceGap 永久漂移
            // (对照 AssetAllocationService.TryRedeemFund 的 equity += available − payout 口径)。
            float shortfall = Mathf.Max(0f, proceeds - state.bank.townSectorDeposits);
            if (shortfall > 0f) state.bank.equity -= shortfall;
            state.resident.savings += proceeds;
            state.bank.householdDeposits += proceeds;
            state.bank.townSectorDeposits = Mathf.Max(0f, state.bank.townSectorDeposits - proceeds);
            h.pendingSaleProceeds = 0f;
            h.pendingSaleEta = 0f;
            state.Changed();
        }

        /// <summary>V0.9.5 卡解锁回填(找BUG围猎盲区2):解锁全挂「动作时点」,V0.9.1–0.9.4 期间已完成过
        /// 对应动作的旧档没有任何补解锁入口(再买房被「本版不做多套房」拒绝)。读档 ApplyToState 时按
        /// 既有状态幂等回填:玩家口径房贷(任意状态)→买房五卡;玩家房贷终态→摊销卡;真金白银付过租→
        /// rentVsBuy/capRate;期账相位史含 Inflating/Deflating→泡沫相位卡。房价分解卡不回填
        /// (读数动作无法从状态推断,接受缺口)。UnlockCard 幂等,重复读档零副作用。</summary>
        public static void BackfillHousingCards(GameState state)
        {
            if (state == null || state.housing == null || state.resident == null) return;
            bool hadMortgage = false, terminalMortgage = false, paidRent = false, inflating = false, deflating = false;
            for (int i = 0; state.bank != null && i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan == null || loan.purpose != LoanPurpose.Mortgage) continue;
                if (loan.borrowerId != state.resident.residentName) continue;
                hadMortgage = true;
                if (loan.status != LoanStatus.Current) terminalMortgage = true;
            }
            for (int i = 0; i < state.history.Count; i++)
                if (state.history[i] != null && state.history[i].rentPaid > 0f) { paidRent = true; break; }
            for (int i = 0; i < state.housing.history.Count; i++)
            {
                LeveragePhase phase = state.housing.history[i].phase;
                if (phase == LeveragePhase.Inflating) inflating = true;
                else if (phase == LeveragePhase.Deflating) deflating = true;
            }
            if (hadMortgage)
            {
                state.UnlockCard("housing.downpayment");
                state.UnlockCard("housing.installment");
                state.UnlockCard("housing.annuity");
                state.UnlockCard("housing.ltv");
                state.UnlockCard("housing.collateral");
                if (terminalMortgage) state.UnlockCard("housing.amortization");
            }
            if (paidRent)
            {
                state.UnlockCard("housing.rentVsBuy");
                state.UnlockCard("housing.capRate");
            }
            if (inflating)
            {
                state.UnlockCard("housing.bubbleThree");
                state.UnlockCard("housing.minsky");
            }
            if (deflating) state.UnlockCard("housing.deleveraging");
        }

        /// <summary>当前未结清房贷(无贷/已结清返回 null)。</summary>
        public static LoanRecord CurrentMortgage(GameState state)
        {
            Ensure(state);
            HousingState h = state.housing;
            if (string.IsNullOrEmpty(h.mortgageLoanId) || state.bank == null || state.bank.loans == null) return null;
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan != null && loan.id == h.mortgageLoanId && loan.status == LoanStatus.Current) return loan;
            }
            return null;
        }

        /// <summary>房贷总量(派生,不落档)。R6:按 purpose 过滤 bank.loans——
        /// BankState.HouseholdLoans 是计算属性且含消费贷,不能直接复用。</summary>
        public static float MortgageOutstanding(GameState state)
        {
            if (state == null || state.bank == null || state.bank.loans == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < state.bank.loans.Count; i++)
            {
                LoanRecord loan = state.bank.loans[i];
                if (loan != null && loan.purpose == LoanPurpose.Mortgage && loan.status == LoanStatus.Current)
                    sum += loan.remainingPrincipal;
            }
            return sum;
        }

        /// <summary>每期收入口径(R13):居民当期收入 + 企业当期净利润——玩家是企业所有者,两笔都是家庭现金流。
        /// 围猎三(#4):旧口径只认「今日已完成」的利润+todayIncome,而收日链末尾会清零 todayIncome、
        /// 推进 worldDay——清晨(玩家交互起点)两者皆空,DSR 显示随时点翻转、TryBuyHouse 按
        /// income≤0 误判 200% 拒贷。改最近 3 期已结算日的日均;两本 history 由同一条收日管线
        /// 成对写入,日序天然对齐,首日仍等价于当日口径。</summary>
        public static float HouseholdIncome(GameState state)
        {
            if (state == null || state.resident == null || state.history == null) return 0f;
            // 围猎三(#4):清晨(todayIncome 已被收日链清零、当日记录未写)旧口径恒读 0,DSR 显示
            // 随时点翻转、TryBuyHouse 按 income≤0 误判 200% 拒贷。口径改为:当日实时读数优先
            // (todayIncome>0 时连同当日已完成利润,与「DSR 实时读 todayIncome」的既有契约一致);
            // 当日为空(清晨/新档)时退回最近 3 期已结算日均。
            if (state.resident.todayIncome > 0f)
            {
                DayRecord rec = state.LastRecord;
                float profit = rec != null && rec.day == state.day ? rec.profit : 0f;
                return state.resident.todayIncome + Mathf.Max(0f, profit);
            }
            const int Window = 3;
            float profitSum = 0f; int profitDays = 0;
            for (int i = state.history.Count - 1; i >= 0 && profitDays < Window; i--, profitDays++)
                profitSum += Mathf.Max(0f, state.history[i].profit);
            float wageSum = 0f; int wageDays = 0;
            List<ResidentDayRecord> residentHistory = state.resident.history;
            if (residentHistory != null)
                for (int i = residentHistory.Count - 1; i >= 0 && wageDays < Window; i--, wageDays++)
                    wageSum += Mathf.Max(0f, residentHistory[i].income);
            return (profitDays > 0 ? profitSum / profitDays : 0f) +
                   (wageDays > 0 ? wageSum / wageDays : 0f);
        }

        /// <summary>DSR=每期供款/每期收入(无贷=0;有贷而收入≤0 按 200% 计)。</summary>
        public static float Dsr(GameState state)
        {
            LoanRecord loan = CurrentMortgage(state);
            if (loan == null) return 0f;
            float payment = MortgageService.AnnuityPayment(loan.principal, loan.annualInterestRate, loan.term);
            float income = HouseholdIncome(state);
            if (income <= 0f) return 2f;
            return payment / income;
        }

        /// <summary>DSR 档:0=轻松(&lt;25%) 1=有压力(25–40%,日终精力衰减 −8→−18) 2=高压(&gt;40%,解锁加班兼职)。</summary>
        public static int DsrBand(GameState state)
        {
            float dsr = Dsr(state);
            if (dsr < 0.25f) return 0;
            if (dsr <= 0.40f) return 1;
            return 2;
        }

        /// <summary>房价指数镜像(基期 42000=100)。</summary>
        public static float PriceIndexMirror(GameState state)
        {
            Ensure(state);
            return state.housing.townHousePrice / BaseHousePrice * 100f;
        }

        /// <summary>房产净值=估值−玩家在贷按揭余额(租房=0)。修复轮回C:改玩家口径(与 Dsr/ResidentDebt 的
        /// borrowerId 归属一致)——MortgageOutstanding 是镇级房贷簿(含镇买方按揭),卖房再买房后会把净值
        /// 错算成 0、财富效应信心贡献恒 0、与同屏 DSR 矛盾;镇级口径保留给 MortgageGdpZ 与宏观面板。</summary>
        public static float HomeEquity(GameState state)
        {
            Ensure(state);
            HousingState h = state.housing;
            if (h.renting) return 0f;
            LoanRecord loan = CurrentMortgage(state);
            return Mathf.Max(0f, h.homeAppraisal - (loan != null ? Mathf.Max(0f, loan.remainingPrincipal) : 0f));
        }

        // ———————————————— V0.9.5 房价分解(第 56 章信号,PRD §197.2) ————————————————

        /// <summary>房价分解读数:全部来自 history 末条引擎分量(§194.2),纯快照零计算。</summary>
        public struct PriceBreakdownInfo
        {
            public int period;
            public float demand;      // 需求(套)
            public float supply;      // 供给存量(套)
            public float priceDelta;  // 当期房价变动(¥,已含 ±3% 钳)
            public float periodRent;  // 当期租金(¥)
        }

        /// <summary>房价分解(纯读,不写计数):无历史返 null。采集器只读它判非空,
        /// 计数走 MarkPriceBreakdownRead——读数与计数分离,防「采集链自赠信号」(R26)。</summary>
        public static PriceBreakdownInfo? PriceBreakdown(GameState state)
        {
            Ensure(state);
            HousingState h = state.housing;
            if (h.history == null || h.history.Count == 0) return null;
            HousingPeriodRecord last = h.history[h.history.Count - 1];
            return new PriceBreakdownInfo
            {
                period = last.period,
                demand = last.demand,
                supply = last.supply,
                priceDelta = last.priceDelta,
                periodRent = last.periodRent
            };
        }

        /// <summary>玩家读一次房价分解(住房面板入口调用):计数＋解锁读数卡＋落 Changed。采集器严禁调用。</summary>
        public static void MarkPriceBreakdownRead(GameState state)
        {
            Ensure(state);
            state.housing.decomposeReads++;
            state.UnlockCard("housing.priceBreakdown");
            state.UnlockCard("housing.supplyElasticity");
            state.UnlockCard("housing.nominalReal");
            state.Changed();
        }

        // ———————————————— V0.9.2《房价是一头兽》引擎(PRD §194–195) ————————————————

        /// <summary>有效 LTV 上限(§196.1 公式提前落函数,V0.9.3 再接授信门槛):
        /// 金融压力越高,银行愿意接受的抵押成数越低。stress=0 → 0.7;stress≥75 → 0.35(钳下限)。</summary>
        public static float LtvEff(GameState state)
        {
            float stress = state != null && state.bank != null ? state.bank.financialStress : 0f;
            return 0.7f * Mathf.Clamp(1f - stress / 150f, 0.5f, 1f);
        }

        /// <summary>信贷可得性(§194.2):抵押物缩水→银行惜贷→购房需求去杠杆,基线=1。</summary>
        public static float CreditAvailability(GameState state)
        {
            return Mathf.Clamp(LtvEff(state) / 0.7f, 0.5f, 1.1f);
        }

        /// <summary>购买力 P=0.35×年化收入/单位房价年化供款(§194.2)。单位供款按当期房价×70%、
        /// 156 期、当期按揭利率计——利率↑→P↓→D↓,核心传导的引擎内实现。收入口径同 R13(玩家=边际买家),
        /// 钳位 [0.7,1.4](R21):镇房价不被单一玩家收入波动主导。</summary>
        public static float PurchasingPower(GameState state)
        {
            Ensure(state);
            float rate = (state.bank != null ? state.bank.householdLoanRate : 6f) + MortgageService.RateSpreadPp;
            float annualPayment = MortgageService.AnnuityPayment(
                state.housing.townHousePrice * (1f - DownPaymentRatio), rate, MortgageService.TermPeriods) * 52f;
            if (annualPayment <= 1f) return PowerClampMax;
            return Mathf.Clamp(0.35f * HouseholdIncome(state) * 52f / annualPayment, PowerClampMin, PowerClampMax);
        }

        /// <summary>购房需求 D=κ×人口×工资×P×信贷×信心因子(§194.2,R16)。读上一期宏观值——
        /// 本期房价由上期宏观状态定价,同日宏观结算再读新房价写 CPI。
        /// Deflating 相位(§196.4)额外按 bp/100×5% 折损——反转后买方观望。</summary>
        public static float Demand(GameState state)
        {
            Ensure(state);
            MacroEconomyState m = state.macro;
            int population = state.government != null ? Mathf.Max(1, state.government.population) : 320;
            float wage = m != null ? m.averageWage : 25f;
            float confidence = m != null ? m.consumerConfidence : 50f;
            float d = DemandScaleKappa * population * wage * PurchasingPower(state) *
                CreditAvailability(state) * (0.9f + confidence / 500f);
            if (state.housing.leveragePhase == LeveragePhase.Deflating)
                d *= 1f - state.housing.bubblePressure / 100f * DeflatingDemandPenalty;
            return d;
        }

        /// <summary>当期新开工(套):基线 0.5;泡沫压力≥60 时上浮 0.5×(1+0.5×bp/100),钳 1.0
        /// (§194.2:供给响应缓慢是泡沫的物理前提)。bp 的三指标计算属 V0.9.3(§196.2)。</summary>
        public static float StartsThisPeriod(GameState state)
        {
            Ensure(state);
            float bp = state.housing.bubblePressure;
            if (bp < 60f) return 0.5f;
            return Mathf.Min(0.5f * (1f + 0.5f * bp / 100f), 1f);
        }

        /// <summary>供给 S=住房存量+当期新开工(§194.2)。</summary>
        public static float Supply(GameState state)
        {
            Ensure(state);
            return state.housing.housingStock + StartsThisPeriod(state);
        }

        /// <summary>实际工资增速(近两期 averageWage;无宏观历史=0)。</summary>
        static float WageGrowth(GameState state)
        {
            MacroEconomyState m = state.macro;
            if (m == null || m.history == null || m.history.Count < 2) return 0f;
            MacroPeriodRecord last = m.history[m.history.Count - 1];
            MacroPeriodRecord prev = m.history[m.history.Count - 2];
            if (prev.averageWage <= 0f) return 0f;
            return (last.averageWage - prev.averageWage) / prev.averageWage;
        }

        /// <summary>房价年化涨幅(近 26 期涨幅按 52/26 年化;历史不足用最早记录按实际跨度年化;无历史=0)。</summary>
        public static float AnnualPriceGrowth(GameState state)
        {
            Ensure(state);
            System.Collections.Generic.List<HousingPeriodRecord> history = state.housing.history;
            if (history == null || history.Count < 2) return 0f;
            int lastIndex = history.Count - 1;
            int firstIndex = Mathf.Max(0, lastIndex - 26);
            HousingPeriodRecord first = history[firstIndex];
            HousingPeriodRecord last = history[lastIndex];
            if (first.priceIndex <= 0f) return 0f;
            int span = Mathf.Max(1, lastIndex - firstIndex);
            return (last.priceIndex / first.priceIndex - 1f) * 52f / span;
        }

        /// <summary>财富效应对信心的贡献(§195.1 单一闸门,p.p./期):
        /// 持房=8×房价年化涨幅×房产敞口占比(净值口径),钳 ±4;无房租户=−0.5×涨幅(上涨对无房者是负担)。</summary>
        public static float ConfidenceContribution(GameState state)
        {
            Ensure(state);
            float growth = AnnualPriceGrowth(state);
            if (state.housing.renting) return Mathf.Clamp(-0.5f * growth, -4f, 4f);
            if (state.resident == null) return 0f;
            float equity = HomeEquity(state);
            float netWorth = state.resident.savings + equity;
            if (netWorth <= 1f) return 0f;
            float exposure = Mathf.Clamp01(equity / netWorth);
            return Mathf.Clamp(8f * growth * exposure, -4f, 4f);
        }

        // ———————————————— V0.9.3《杠杆周期》(PRD §196) ————————————————

        /// <summary>房价/年租比偏离 prZ(§196.2):(房价/(期租×52)−6)/6,钳 [0,1]。与 R15 锁定同源:
        /// 租金向隐含期租收敛使 P/R 均值回归 6,只有房价持续快于租金(动能市)才累积。</summary>
        public static float PriceRentZ(GameState state)
        {
            Ensure(state);
            HousingState h = state.housing;
            if (h.periodRent <= 0f) return 0f;
            float ratio = h.townHousePrice / (h.periodRent * 52f);
            return Mathf.Clamp((ratio - PrBaseline) / PrBaseline, 0f, 1f);
        }

        /// <summary>房贷/年化 GDP 偏离 mgdpZ(§196.2):(房贷总量/(nominalGdp×52)−0.6)/0.4,钳 [0,1]。
        /// R23(口径落定):房贷总量=玩家口径 MortgageOutstanding(全镇唯一个体房贷,不造统计房贷簿)——
        /// 常态读数≈0 是诚实读数:本镇银行簿不以按揭为主,泡沫由价格动能驱动。</summary>
        public static float MortgageGdpZ(GameState state)
        {
            MacroEconomyState m = state.macro;
            if (m == null || m.nominalGdp <= 1f) return 0f;
            float ratio = MortgageOutstanding(state) / (m.nominalGdp * 52f);
            return Mathf.Clamp((ratio - MgdpBaseline) / MgdpRange, 0f, 1f);
        }

        /// <summary>房价动能偏离 devZ(§196.2):(26 期年化涨幅−20%)/20%,钳 [0,1]。
        /// R23:须有 26 期完整窗口才读数——短窗(2–5 期)的 52/span 年化放大是噪声放大器,
        /// 中性世界也曾读出 68%/年的假动能;泡沫认定宁缺毋滥。</summary>
        public static float DeviationZ(GameState state)
        {
            Ensure(state);
            System.Collections.Generic.List<HousingPeriodRecord> history = state.housing.history;
            if (history == null || history.Count < ReversalLookback + 1) return 0f;
            return Mathf.Clamp((AnnualPriceGrowth(state) - DevBaseline) / DevRange, 0f, 1f);
        }

        /// <summary>泡沫压力 bp(§196.2)=100×(权重·prZ+权重·mgdpZ+权重·devZ,R23 落定 .25/.2/.55),读上期状态。</summary>
        public static float BubblePressure(GameState state)
        {
            Ensure(state);
            return 100f * (BpWeightPr * PriceRentZ(state) + BpWeightMgdp * MortgageGdpZ(state) +
                BpWeightDev * DeviationZ(state));
        }

        /// <summary>杠杆相位机(§196.2/§196.4):bp≥60 进 Inflating;Deflating 只经反转进入,
        /// 出口=Cleanup(回撤≥8% 且 bp&lt;40,或软着陆 bp&lt;20——R23 防相位卡死);Cleanup 次期回 None 或再通胀。
        /// peakPriceIndex:None 期随当期重置(软锚),离 None 后只升不降(顶部冻结,回撤以此为基)。</summary>
        static void UpdatePhase(GameState state)
        {
            HousingState h = state.housing;
            float index = PriceIndexMirror(state);
            float bp = h.bubblePressure;
            if (h.leveragePhase == LeveragePhase.Deflating)
            {
                float drawdown = h.peakPriceIndex > 0f ? 1f - index / h.peakPriceIndex : 0f;
                if ((drawdown >= CrashDrawdown && bp < CleanupPressure) || bp < SoftLandingPressure)
                    h.leveragePhase = LeveragePhase.Cleanup;
            }
            else if (h.leveragePhase != LeveragePhase.Cleanup)
            {
                h.leveragePhase = bp >= InflatingThreshold ? LeveragePhase.Inflating : LeveragePhase.None;
            }
            else
            {
                h.leveragePhase = bp >= InflatingThreshold ? LeveragePhase.Inflating : LeveragePhase.None;
            }
            if (h.leveragePhase == LeveragePhase.None) h.peakPriceIndex = index;
            else h.peakPriceIndex = Mathf.Max(h.peakPriceIndex, index);

            // V0.9.5 卡解锁随相位(幂等):狂热与下行是事件,不是页面
            if (h.leveragePhase == LeveragePhase.Inflating)
            {
                state.UnlockCard("housing.bubbleThree");
                state.UnlockCard("housing.minsky");
            }
            else if (h.leveragePhase == LeveragePhase.Deflating)
            {
                state.UnlockCard("housing.deleveraging");
            }
        }

        /// <summary>近 26 期窗口内某宏观量的最低值(policyRate/unemploymentRate 通用);
        /// ≤0 的旧档记录按缺失跳过;窗口无有效记录返回 −1。</summary>
        static float LowestInWindow(MacroEconomyState m, bool policyRate)
        {
            if (m == null || m.history == null || m.history.Count == 0) return -1f;
            int from = Mathf.Max(0, m.history.Count - ReversalLookback);
            float lowest = -1f;
            for (int i = from; i < m.history.Count; i++)
            {
                MacroPeriodRecord r = m.history[i];
                if (r == null) continue;
                float v = policyRate ? r.policyRate : r.unemploymentRate;
                if (v <= 0f) continue;
                lowest = lowest < 0f ? v : Mathf.Min(lowest, v);
            }
            return lowest;
        }

        /// <summary>反转触发判定(§196.3,纯状态函数,零随机,bp 现场重算不依赖写入顺序)。任一命中即真:
        /// ①政策利率较 26 期最低抬升 ≥2pp ②失业率较 26 期最低抬升 ≥2p.p.
        /// ③既有外生冲击在队(泡沫期外生冲击落地即引爆,入队时覆写它)。
        /// R23:三触发统一加 bp≥60 闸——无泡沫不反转,也避免「无跌幅的 Deflating」把相位卡死;
        /// 相位已是 Deflating 或槽内已是 HousingReversal 时不重复入队。</summary>
        public static bool ShouldQueueReversal(GameState state)
        {
            Ensure(state);
            MacroEconomyState m = state.macro;
            if (m == null) return false;
            HousingState h = state.housing;
            if (BubblePressure(state) < InflatingThreshold) return false;
            if (h.leveragePhase == LeveragePhase.Deflating) return false;
            if (m.pendingShock == MacroShockType.HousingReversal) return false;
            if (m.pendingShock != MacroShockType.None) return true;
            float rateLow = LowestInWindow(m, true);
            if (rateLow > 0f && m.policyRate - rateLow >= ReversalRateHike) return true;
            float jobLow = LowestInWindow(m, false);
            if (jobLow > 0f && m.unemploymentRate - jobLow >= ReversalJobHike) return true;
            return false;
        }

        static void TryQueueReversal(GameState state)
        {
            if (ShouldQueueReversal(state)) MacroEconomySimulator.QueueShock(state, MacroShockType.HousingReversal);
        }

        /// <summary>住房市场期步(每期一步,确定性,零随机):泡沫压力步→相位机步→反转触发步→
        /// 房价步→租金步→CPI 住房分项写入→入档。插位=贸易结算后、政府周期与宏观期步前(R1):
        /// 当期 CPI 即读新值;入队的 HousingReversal 同日被宏观期步消费(管线顺序即因果)。</summary>
        public static void AdvancePeriod(GameState state)
        {
            Ensure(state);
            HousingState h = state.housing;

            // 修复轮回D:一日一期契约(照 MacroEconomySimulator 先例)——收日链中段存档(章节完成即落盘)
            // 后读档重玩当日,整条链会再跑一遍:同日裸 Add 产生重复期记录,房价/租金/CPI 住房分项/
            // 镇景指数同日双步,devZ 26 期窗口被重玩循环稀释。期账末条=今日 ⇒ 本日已步过,冻结一期。
            if (h.history.Count > 0 && h.history[h.history.Count - 1].period == state.day) return;

            // 泡沫压力步(§196.2):三指标按上期状态定价,先于相位机与房价步
            h.bubblePressure = BubblePressure(state);

            // 相位机步(§196.2/§196.4):Inflating/Cleanup/None 迁移(Deflating 只经反转进入)
            UpdatePhase(state);

            // 反转触发步(§196.3 纯状态函数):命中即入队单槽冲击,宏观期步同日消费
            TryQueueReversal(state);

            // 房价步:Δ=price×0.06×clamp((D−S)/S,−0.5,0.5),每期涨幅再钳 ±3%(Deflating 罚已并入 D)
            float price = h.townHousePrice;
            float demand = Demand(state);
            float supply = Supply(state);
            float imbalance = supply > 0f ? Mathf.Clamp((demand - supply) / supply, -0.5f, 0.5f) : 0.5f;
            float priceDelta = Mathf.Clamp(price * PriceStepCoef * imbalance, -price * PriceStepClamp, price * PriceStepClamp);
            h.townHousePrice = Mathf.Max(1000f, price + priceDelta);

            // 租金步:向隐含期租(新房价/312)收敛 15%,另加工资增速 0.5 弹性,每期钳 ±2%
            float implied = h.townHousePrice / ImpliedRentDivisor;
            float rentTarget = implied * (1f + RentWageElasticity * WageGrowth(state));
            float rent = h.periodRent;
            h.periodRent = Mathf.Clamp(rent + RentConvergence * (rentTarget - rent),
                rent * (1f - RentStepClamp), rent * (1f + RentStepClamp));
            h.rentIndex = h.periodRent / BasePeriodRent * 100f;

            // 新开工入存量,存量按期折旧(R22):长期平衡=starts/折旧≈125 套,供给洪流不无限累积
            float starts = StartsThisPeriod(state);
            h.housingStock = Mathf.Max(1f, h.housingStock * (1f - StockDepreciation) + starts);
            h.startsThisPeriod = starts;

            // 估值跟随(§196.4):自住房估值=当期镇房价——抵押物随行就市,净值/法拍回收随之下行链联动
            if (!h.renting)
            {
                h.homeAppraisal = h.townHousePrice;
                // V0.9.5 卡解锁(幂等):持有者才体会财富效应,持有期才看名义/实际双指数
                if (priceDelta > 0f) state.UnlockCard("housing.wealthEffect");
                state.UnlockCard("housing.nominalReal");
            }

            // 法拍铭牌余晖衰减(R23):地图铭牌「法拍」态只亮 26 期
            if (h.foreclosureFlash > 0) h.foreclosureFlash--;

            // CPI 住房分项接管(§194.4 单一权威):Lerp 向 0.7×租金指数+0.3×房价指数镜像,钳 [60,300]
            MacroEconomyState m = state.macro;
            if (m != null)
                m.housingPriceIndex = Mathf.Clamp(Mathf.Lerp(m.housingPriceIndex,
                    0.7f * h.rentIndex + 0.3f * PriceIndexMirror(state), 0.5f), 60f, 300f);

            // V0.9.5 镇景指数步(§197.5):确定性演化,零随机零银行写;读到的宏观值为上期(滞后一期定阶)
            AssetAllocationService.AdvanceIndex(state);

            h.history.Add(new HousingPeriodRecord
            {
                period = state.day,
                priceIndex = PriceIndexMirror(state),
                rentIndex = h.rentIndex,
                stock = h.housingStock,
                mortgageOutstanding = MortgageOutstanding(state),
                bubblePressure = h.bubblePressure,
                phase = h.leveragePhase,
                demand = demand,
                supply = supply,
                priceDelta = priceDelta,
                periodRent = h.periodRent
            });
            state.Changed();
        }
    }
}
