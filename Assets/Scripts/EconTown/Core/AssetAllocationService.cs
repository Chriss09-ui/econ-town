using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.5《第八篇·资产与杠杆》储蓄三去向(PRD §197.5):存款(既有 resident.savings)之外,
    /// 国债(政府借贷的最小居民入口)与镇景指数基金(分散化的最小形态)。
    /// 记账铁律:银行 BalanceGap==0 是全库硬不变量——凡动银行必四腿齐平:
    /// 申购=负债 householdDeposits 与资产 reserves 同减(货币出表);赎回=托管现金回表,
    /// 净损益经 bank.equity 平账,ΔA−ΔL−ΔE≡0 无条件成立(R26)。
    /// 零随机、零新存档键(资产字段 V0.9.1 已预埋于 HousingState)。</summary>
    public static class AssetAllocationService
    {
        public const float TreasuryMinFace = 100f;   // 国债最小认购面额
        public const float FundMinBuy = 1000f;       // 基金起购
        public const float FundSpread = 0.005f;      // 申赎价差 0.5%(交易成本教学)
        public const float IndexReturnClamp = 0.06f; // 指数单期收益钳 ±6%

        public static void Ensure(GameState state)
        {
            if (state == null) return;
            HousingService.Ensure(state);
            HousingState h = state.housing;
            h.treasuryFace = Mathf.Max(0f, h.treasuryFace);
            h.indexUnits = Mathf.Max(0f, h.indexUnits);
            h.indexLevel = Mathf.Max(1f, h.indexLevel);
            h.fundCash = Mathf.Max(0f, h.fundCash);
        }

        /// <summary>居民认购国债(面额制):储蓄→国债。政府 debt/cash 同增(政府得融资),
        /// 银行负债与准备金同减(存款货币销毁,BalanceGap 不变);averageDebtRate 混价——
        /// 政府下一期按混价利率对全部债务计息,居民持债的利息经付息拆分回流(R26)。</summary>
        public static bool TryBuyTreasury(GameState state, float face, out string message)
        {
            Ensure(state);
            message = "";
            if (face < TreasuryMinFace)
            {
                message = "最小认购面额 ¥" + TreasuryMinFace.ToString("0") + "。";
                return false;
            }
            if (state.resident.savings + .01f < face) { message = "存款不足。"; return false; }
            if (state.bank.householdDeposits + .01f < face) { message = "存款账户余额异常。"; return false; }
            if (state.bank.reserves + .01f < face)
            {
                message = "银行准备金不足——存款搬家也会收紧银行流动性,这正是它教给你的一课。";
                return false;
            }

            state.resident.savings -= face;
            state.bank.householdDeposits -= face;
            state.bank.reserves -= face;
            state.housing.treasuryFace += face;

            GovernmentState government = state.government;
            float newRate = InterestRateService.GovernmentNewDebtRate(state);
            float oldDebt = government.debt;
            float oldRate = government.averageDebtRate <= 0f ? 3f : government.averageDebtRate;
            government.averageDebtRate = (oldDebt * oldRate + face * newRate) / Mathf.Max(.001f, oldDebt + face);
            government.debt += face;
            government.cash += face;

            state.housing.treasuryBuys++;
            state.UnlockCard("asset.treasury");
            state.UnlockCard("asset.allocation");
            state.Changed();
            message = "认购国债 ¥" + face.ToString("0") + "(年化 " + newRate.ToString("0.0") + "%,政府按期付息)。";
            return true;
        }

        /// <summary>申购镇景指数基金(起购 ¥1000):储蓄→托管现金(银行表外)。
        /// 份额 = 金额 ÷ (指数/100);赎回价值随指数浮动——基金是证券,如房子一样按市价计值。</summary>
        public static bool TryBuyFund(GameState state, float amount, out string message)
        {
            Ensure(state);
            message = "";
            if (amount < FundMinBuy)
            {
                message = "基金起购 ¥" + FundMinBuy.ToString("0") + "。";
                return false;
            }
            if (state.resident.savings + .01f < amount) { message = "存款不足。"; return false; }
            if (state.bank.householdDeposits + .01f < amount) { message = "存款账户余额异常。"; return false; }
            if (state.bank.reserves + .01f < amount)
            {
                message = "银行准备金不足——申购款出表同样占用银行流动性。";
                return false;
            }

            HousingState h = state.housing;
            state.resident.savings -= amount;
            state.bank.householdDeposits -= amount;
            state.bank.reserves -= amount;
            h.fundCash += amount;
            h.indexUnits += amount / (h.indexLevel / 100f);

            h.fundTrades++;
            state.UnlockCard("asset.indexFund");
            state.UnlockCard("asset.riskReturn");
            state.Changed();
            message = "申购镇景指数基金 ¥" + amount.ToString("0") + ",得 " +
                (amount / (h.indexLevel / 100f)).ToString("0.0") + " 份(指数 " + h.indexLevel.ToString("0.0") + ")。";
            return true;
        }

        /// <summary>赎回基金:赎回款 = 份额 × 指数/100 × (1−0.5% 价差)。托管现金回表优先支付,
        /// 买卖差额经 bank.equity 平账——指数涨,已实现净收益由银行权益吸收;指数跌,居民实现亏损归银行。
        /// 封闭单银行经济里银行是基金的唯一对手方(教学最小版,面板有注明)。</summary>
        public static bool TryRedeemFund(GameState state, float units, out string message)
        {
            Ensure(state);
            message = "";
            HousingState h = state.housing;
            if (units <= 0f) { message = "请输入要赎回的份额。"; return false; }
            if (units > h.indexUnits + .01f) { message = "持有份额不足。"; return false; }

            float gross = units * h.indexLevel / 100f;
            if (gross < 1f) { message = "赎回金额过小。"; return false; }
            float available = Mathf.Min(gross, h.fundCash);
            float payout = gross * (1f - FundSpread);

            h.indexUnits -= units;
            h.fundCash -= available;
            state.resident.savings += payout;
            state.bank.householdDeposits += payout;
            state.bank.reserves += available;
            state.bank.equity += available - payout;

            h.fundTrades++;
            state.UnlockCard("asset.liquidity");
            state.UnlockCard("asset.transactionCost");
            state.Changed();
            message = "赎回 " + units.ToString("0.0") + " 份,到手 ¥" + payout.ToString("0") +
                "(价差 ¥" + (gross * FundSpread).ToString("0") + " 已扣)。";
            return true;
        }

        /// <summary>镇景指数每期演化(§197.5):0.3×实际增速 + 0.2×(信心−68)/68,钳 ±6%/期,零随机。
        /// 调用点=HousingService.AdvancePeriod 尾部——宏观期步在结算链更晚,这里读到的是上期值(滞后一期定阶)。</summary>
        public static void AdvanceIndex(GameState state)
        {
            Ensure(state);
            MacroEconomyState m = state.macro;
            float growth = m != null ? m.gdpGrowth / 100f : 0f;          // gdpGrowth 以百分数存
            float confidence = m != null ? m.businessConfidence : 72f;
            float r = Mathf.Clamp(0.3f * growth + 0.2f * (confidence - 68f) / 68f,
                -IndexReturnClamp, IndexReturnClamp);
            state.housing.indexLevel = Mathf.Max(1f, state.housing.indexLevel * (1f + r));
        }
    }
}
