using System;
using System.Collections.Generic;

namespace EconTown
{
    // V0.9.1《安家》:住房状态块。存档结构文件不混装服务,一类一文件(照 GovernmentModels 先例)。
    // 口径:期=游戏日,52 期/年(R11);数值与默认值见执行 PRD §193.1(含 R12/R14 修订)。

    /// <summary>杠杆周期相位(V0.9.3 消费;V0.9.1 先立枚举随档)。</summary>
    public enum LeveragePhase { None, Inflating, Peak, Deflating, Cleanup }

    [Serializable]
    public class HousingState
    {
        public bool initialized;

        // —— 玩家住房(V0.9.1)——
        public bool renting = true;             // 租房中(默认)
        public float periodRent = 140f;         // 每期租金(¥;与隐含期租 135 同量级,R12)
        public int rentMissed;                  // 连续欠租期数(3 期被迫搬离)
        public float homeAppraisal;             // 自住房估值(无房=0)
        public float purchasePrice;             // 购入价
        public float downPaymentPaid;           // 已付首付
        public string mortgageLoanId = "";      // 关联 LoanRecord.id,空=无贷

        // —— 镇级统计(V0.9.2 引擎演化;V0.9.1 先立读数与唯一权威值)——
        public float townHousePrice = 42000f;   // 镇房价 ¥/套,房价唯一权威(R14:DSR≈35% 校准)
        public float rentIndex = 100f;          // 租金指数(基期=100)
        public float housingStock = 120f;       // 住房存量(套)
        public float startsThisPeriod = 0.5f;   // 当期新开工(套;基线 26 套/年)
        // 注:镇住户部门存款(租金/首付/卖房款对手方)是银行真实负债科目,落 BankState.townSectorDeposits
        // 入平衡表——放 housing 块会让银行平衡断言 gap(实测 Day1 gap=140 暴露,as-built 修订 R20)。

        // —— 泡沫与卖房约束(V0.9.3;字段先立随档)——
        public float peakPriceIndex = 100f;
        public float bubblePressure;
        public LeveragePhase leveragePhase = LeveragePhase.None;
        public float pendingSaleProceeds;       // 在途房款
        public float pendingSaleEta;            // 剩余到账期数
        public int foreclosureFlash;            // 法拍铭牌余晖期数(R23:法拍时=26,每期−1,0=散)

        // —— 储蓄三去向(V0.9.4/9.5;字段先立随档)——
        public int decomposeReads, treasuryBuys, fundTrades;
        public float treasuryFace;              // 国债面值
        public float indexUnits;                // 镇景指数基金份额
        public float indexLevel = 100f;         // 镇景指数
        public float fundCash;                  // 基金托管存款(R19:申赎有出处有去向)

        public List<HousingPeriodRecord> history = new List<HousingPeriodRecord>();
    }

    [Serializable]
    public class HousingPeriodRecord
    {
        public int period;
        public float priceIndex;            // 房价指数镜像(townHousePrice/42000×100)
        public float rentIndex;
        public float stock;                 // 住房存量
        public float mortgageOutstanding;   // 房贷总量(派生量的入档快照)
        public float bubblePressure;
        public LeveragePhase phase;

        // —— V0.9.2 引擎分量(PRD §194.2:供 PriceBreakdown 与第 56 章信号)——
        public float demand;                // D(套)
        public float supply;                // S(套)
        public float priceDelta;            // Δ房价(¥,含 ±3% 钳前结果)
        public float periodRent;            // 当期租金(¥)
    }
}
