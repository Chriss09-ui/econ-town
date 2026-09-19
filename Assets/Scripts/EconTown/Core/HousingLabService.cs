using System.Collections.Generic;

namespace EconTown
{
    /// <summary>期末读数口径(V0.9.4):DSR=HousingService.Dsr;净资产=resident.savings+HomeEquity(R24);
    /// 坏账=bank.totalLoanLoss 累计读数(bank.loanLoss 是当日流量,每结算日在 FinancialSystemService
    /// 清零,期末不可用;实验期增量=期末−期初)。期初读数时点=ApplyToState 后的起点日清晨。</summary>
    public class ForkReadings
    {
        public int day;
        public float dsr, savings, homeEquity, netWorth, totalLoanLoss, priceIndex, periodRent, bubblePressure;
        public LeveragePhase leveragePhase;

        public static ForkReadings Take(GameState s)
        {
            HousingState h = s.housing;
            return new ForkReadings
            {
                day = s.day,
                dsr = HousingService.Dsr(s),
                savings = s.resident.savings,
                homeEquity = HousingService.HomeEquity(s),
                netWorth = s.resident.savings + HousingService.HomeEquity(s),
                totalLoanLoss = s.bank.totalLoanLoss,
                priceIndex = h.townHousePrice / HousingService.BaseHousePrice * 100f,
                periodRent = h.periodRent,
                bubblePressure = h.bubblePressure,
                leveragePhase = h.leveragePhase
            };
        }
    }

    /// <summary>实验室①结果:同一起点 A(利率维持)/B(加息 hikePp) 两份克隆各跑 periods 期。</summary>
    public class MortgageStressResult
    {
        public ForkReadings start, outcomeA, outcomeB;
        public ForkRunResult runA, runB;
        public float hikePp;
        public int periods;
        public string refusal;   // CanFork 拒绝原因(非空=未运行)
    }

    /// <summary>泡沫实验逐期轨迹点:priceRentRatio=镇房价/(期租×52) 年租口径(prZ 的原始比值)。</summary>
    public class BubbleTrackPoint
    {
        public int period;
        public float bubblePressure, priceIndex, priceRentRatio;
        public LeveragePhase phase;
    }

    /// <summary>实验室②结果:钉低利率跑 periods 期的确定性重放轨迹。</summary>
    public class BubbleExperimentResult
    {
        public ForkReadings start, end;
        public List<BubbleTrackPoint> track = new List<BubbleTrackPoint>();
        public ForkRunResult run;
        public float pinnedRate;
        public string refusal;
    }

    /// <summary>住房实验室(V0.9.4 PRD §197.6):两个编排入口,只经 WorldForkService 跑同一结算管线
    /// (快照重放,母版 §187-8 铁律)。零新随机、不写档、主世界零扰动;EditMode 同步执行。</summary>
    public static class HousingLabService
    {
        /// <summary>实验室①房贷压力测试:同一快照克隆两份,A=利率维持,B=加息 hikePp(experiment 通道),
        /// 各跑 periods 期,对比 DSR/净资产/累计坏账三读数。注意:存量房贷利率放款日锁定
        /// (CreditService SettleOneLoan 逐期读 loan.annualInterestRate),加息不重定价存量贷——
        /// 传导走银行报价→新购 P→镇需求→房价→净值,与宏观工资/信心→商店利润→DSR 分母。</summary>
        public static MortgageStressResult RunMortgageStress(GameState source, float hikePp, int periods)
        {
            MortgageStressResult r = new MortgageStressResult { hikePp = hikePp, periods = periods };
            if (!WorldForkService.CanFork(source, out r.refusal)) return r;
            string json = WorldForkService.CaptureJson(source);
            ForkReadings startHolder = null;
            r.runA = WorldForkService.RunClonedWorld(json, periods,
                f => { if (startHolder == null) startHolder = ForkReadings.Take(f); },
                null,
                f => r.outcomeA = ForkReadings.Take(f));
            r.runB = WorldForkService.RunClonedWorld(json, periods,
                f =>
                {
                    string msg;
                    InterestRateService.TrySetPolicyRate(f, f.centralBank.policyRate + hikePp, out msg, true);
                },
                null,
                f => r.outcomeB = ForkReadings.Take(f));
            r.start = startHolder;
            return r;
        }

        /// <summary>实验室②泡沫形成:钉住政策利率 pinnedRate 跑 periods 期,逐期记录 bubblePressure
        /// 与房价/年租比演化(确定性重放——同一起点两次运行轨迹逐点一致)。</summary>
        public static BubbleExperimentResult RunBubbleExperiment(GameState source, float pinnedRate, int periods)
        {
            BubbleExperimentResult r = new BubbleExperimentResult { pinnedRate = pinnedRate };
            if (!WorldForkService.CanFork(source, out r.refusal)) return r;
            string json = WorldForkService.CaptureJson(source);
            r.run = WorldForkService.RunClonedWorld(json, periods,
                f =>
                {
                    r.start = ForkReadings.Take(f);
                    string msg;
                    InterestRateService.TrySetPolicyRate(f, pinnedRate, out msg, true);
                },
                (f, i) => r.track.Add(new BubbleTrackPoint
                {
                    period = i,
                    bubblePressure = f.housing.bubblePressure,
                    priceIndex = f.housing.townHousePrice / HousingService.BaseHousePrice * 100f,
                    priceRentRatio = f.housing.townHousePrice / (f.housing.periodRent * 52f),
                    phase = f.housing.leveragePhase
                }),
                f => r.end = ForkReadings.Take(f));
            return r;
        }
    }
}
