using System.Text;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// “为什么”解释系统(见 PRD §20/§32):
    /// 先描述玩家自己的数据发生了什么,再点出经济学概念的名称。
    /// </summary>
    public static class Explainer
    {
        public class Explanation
        {
            public string title;
            public string[] paragraphs;
            public string concept;     // 关联知识卡 id(可空)
        }

        /// <summary>解释今日销量变化。</summary>
        public static Explanation ExplainSales(GameState s, DayRecord r)
        {
            DayRecord y = s.Yesterday;
            var list = new System.Collections.Generic.List<string>();
            DemandEffectBreakdown effects = r.demandEffects;
            if (effects != null)
            {
                list.Add("本次结算的可加总需求账本：" + CompactDemandEffects(effects, 12) + "。");
                string drivers = MainDemandDrivers(effects, 4);
                if (!string.IsNullOrEmpty(drivers))
                    list.Add("模型估计的主要影响：" + drivers +
                        "。这些数量来自当日同一次消费者选择计算，不是用前后两天的销量事后猜测。");
                if (!Mathf.Approximately(effects.eventMultiplier, 1f))
                    list.Add("事件乘数为 " + effects.eventMultiplier.ToString("0.00") +
                        "，说明今天发生的是需求曲线移动，不把它误归因给价格。");
                if (!Mathf.Approximately(effects.macroMultiplier, 1f))
                    list.Add("宏观乘数为 " + effects.macroMultiplier.ToString("0.00") +
                        "：就业与消费者信心仍在竞争期继续影响两家店的市场规模。");
                if (r.competitionActive)
                    list.Add("进入市场的人中，本店模型份额约 " + (effects.competitionShare * 100f).ToString("0") +
                        "%，不购买份额约 " + (effects.noPurchaseShare * 100f).ToString("0") +
                        "%。价格、品质、品牌与距离共同决定选择，但价格敏感度只来自同一套需求弹性。");
            }
            else
            {
                list.Add("这是旧版本存档生成的日报，没有保存完整需求归因；只能确认当日想买 " +
                    r.demandTotal + " 杯、实际卖出 " + r.sold + " 杯。");
            }

            list.Add("结算口径：想买 " + r.desiredDemand + " 杯；可售 " + r.availableSupply +
                " 杯；实际卖出 " + r.actualSales + " 杯。三者不会再混成同一个“销量”。");
            if (r.operationalStockout > 0)
                list.Add("其中 " + r.operationalStockout + " 杯属于本店备货不足造成的经营性缺货。");
            if (r.priceCeilingBinding && r.policyShortage > 0)
                list.Add("另有 " + r.policyShortage + " 杯属于真正有约束力的价格上限所形成的政策短缺。");

            // 弹性解读
            bool comparableDays = y != null && string.IsNullOrEmpty(r.eventName) && string.IsNullOrEmpty(y.eventName) &&
                string.IsNullOrEmpty(r.competitionEventName) && string.IsNullOrEmpty(y.competitionEventName) &&
                (!r.competitionActive || !y.competitionActive || Mathf.Approximately(r.competitorPrice, y.competitorPrice));
            float ed = comparableDays ? DaySimulator.Elasticity(y, r) : float.NaN;
            if (!float.IsNaN(ed))
            {
                float a = Mathf.Abs(ed);
                string feel = a >= 1.5f ? "非常敏感(富有弹性)" : a >= 0.8f ? "比较敏感" : "不太敏感(缺乏弹性)";
                list.Add("这两天算出的需求价格弹性约为 " + ed.ToString("0.00") + "(绝对值 " + a.ToString("0.00") + ")——消费者对你的涨价" + feel + "。");
            }
            else if (!comparableDays)
            {
                list.Add("这两天还有事件或对手调价,需求曲线发生了移动,因此不把销量变化硬算成你的价格弹性。");
            }

            return new Explanation
            {
                title = "需求与销量的真实归因",
                paragraphs = list.ToArray(),
                concept = "demand"
            };
        }

        public static string CompactDemandEffects(DemandEffectBreakdown effects, int maximumEffects = 6)
        {
            if (effects == null) return "无归因数据";
            var entries = DemandEntries(effects);
            entries.Sort((a, b) => Mathf.Abs(b.Value).CompareTo(Mathf.Abs(a.Value)));
            StringBuilder builder = new StringBuilder();
            builder.Append("基础 ").Append(Mathf.RoundToInt(effects.baseDemand));
            int shown = 0;
            for (int i = 0; i < entries.Count && shown < maximumEffects; i++)
            {
                if (entries[i].Value == 0) continue;
                builder.Append("  ").Append(entries[i].Value > 0 ? "+ " : "- ")
                    .Append(entries[i].Key).Append(" ").Append(Mathf.Abs(entries[i].Value));
                shown++;
            }
            builder.Append("  =  ").Append(effects.desiredDemand).Append(" 杯");
            return builder.ToString();
        }

        public static string MainDemandDrivers(DemandEffectBreakdown effects, int maximumEffects = 3)
        {
            if (effects == null) return null;
            var entries = DemandEntries(effects);
            entries.Sort((a, b) => Mathf.Abs(b.Value).CompareTo(Mathf.Abs(a.Value)));
            StringBuilder builder = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < entries.Count && shown < maximumEffects; i++)
            {
                if (entries[i].Value == 0) continue;
                if (shown > 0) builder.Append("、");
                builder.Append(entries[i].Key).Append(entries[i].Value > 0 ? " +" : " ")
                    .Append(entries[i].Value);
                shown++;
            }
            return builder.ToString();
        }

        static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>
            DemandEntries(DemandEffectBreakdown effects)
        {
            return new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>
            {
                Entry("人群客流", effects.segmentEffect),
                Entry("小镇事件", effects.eventEffect),
                Entry("信心与就业", effects.macroEffect),
                Entry("自身价格", effects.ownPriceEffect),
                Entry("对手价格", effects.competitorPriceEffect),
                Entry("税收/价格管制", effects.policyPriceEffect),
                Entry("本店品质", effects.qualityEffect),
                Entry("本店品牌", effects.brandEffect),
                Entry("对手品牌与品质", effects.competitorAppealEffect),
                Entry("广告", effects.marketingEffect),
                Entry("居民偏好", effects.residentPreferenceEffect),
                Entry("竞争事件", effects.competitionEventEffect),
                Entry("随机波动", effects.randomEffect),
                Entry("对手缺货转入", effects.stockoutSwitchEffect)
            };
        }

        static System.Collections.Generic.KeyValuePair<string, int> Entry(string label, int value)
        {
            return new System.Collections.Generic.KeyValuePair<string, int>(label, value);
        }

        /// <summary>解释今日利润变化。</summary>
        public static Explanation ExplainProfit(GameState s, DayRecord r)
        {
            DayRecord y = s.Yesterday;
            var list = new System.Collections.Generic.List<string>();
            list.Add("利润 = 总收入 - 总成本 = " + r.revenue.ToString("0") + " - " + r.totalCost.ToString("0") + " = " + r.profit.ToString("0") + " 元。");

            if (y != null)
            {
                float d = r.profit - y.profit;
                if (d > 1f) list.Add("比昨天多赚 " + d.ToString("0") + " 元。" + (r.revenue > y.revenue ? "主要原因是收入增加。" : "主要原因是成本下降。"));
                else if (d < -1f) list.Add("比昨天少赚 " + Mathf.Abs(d).ToString("0") + " 元。回头看看:是收入变少,还是成本变高?");
                else list.Add("和昨天基本持平。");
            }

            // 五项与结算口径 totalCost 一一对应,玩家自己加总才能对上展示的利润。
            string costLine = "成本构成:原料 " + r.materialCost.ToString("0") + "(可变) + 工资 " + r.wageCost.ToString("0") + "(可变) + 租金折旧 " + r.rentCost.ToString("0") + "(固定)";
            if (r.playerMarketing > 0f || r.strategicSpend > 0f)
            {
                costLine += " + 广告投放 " + r.playerMarketing.ToString("0");
                if (r.strategicSpend > 0f) costLine += " + 策略性支出 " + r.strategicSpend.ToString("0");
                costLine += "(当日决策)";
            }
            list.Add(costLine + "。");
            if (r.profit < 0) list.Add("今天亏钱了。收入没能覆盖成本——要么提高售价/销量,要么压缩产量减少可变成本。固定成本无论开不开门都要付。");
            return new Explanation { title = "利润是怎么算出来的?", paragraphs = list.ToArray(), concept = "profit" };
        }

        /// <summary>解释库存与损耗。</summary>
        public static Explanation ExplainInventory(GameState s, DayRecord r)
        {
            var list = new System.Collections.Generic.List<string>();
            int openingInventory = Mathf.Max(0, r.availableSupply - r.produced);
            list.Add("今日可售供给 = 期初库存 " + openingInventory + " + 今日生产 " + r.produced +
                " = " + r.availableSupply + " 杯；卖出 " + r.actualSales + " 杯后，留下 " + r.inventoryEnd + " 杯。");
            list.Add("生产量 ≠ 销售量。库存是两者的缓冲垫,但果汁放久了会坏:每天有 20% 损耗。");
            if (r.spoiled > 0) list.Add("今天清晨已损耗 " + r.spoiled + " 杯；这笔损耗记在今天，没有回写昨天的日报。");
            if (r.inventoryEnd > 40) list.Add("库存偏多——明早会损耗 " + Mathf.FloorToInt(r.inventoryEnd * Eco.SpoilageRate) + " 杯。少生产一点也许更划算?");
            if (r.stockoutLost > 0) list.Add("今天断货损失了 " + r.stockoutLost + " 位顾客——库存太少同样亏。找到平衡点,就是「均衡」的思想。");
            return new Explanation { title = "库存告诉我们什么?", paragraphs = list.ToArray(), concept = "supply" };
        }

        /// <summary>解释边际产量(员工面板用)。</summary>
        public static string ExplainEmployees(int employees)
        {
            var sb = new StringBuilder();
            int laborPotential = Eco.Capacity(employees);
            int actual = Mathf.Min(laborPotential, Eco.EquipmentCap);
            sb.Append("当前 ").Append(employees).Append(" 名员工：人力潜在产能 ").Append(laborPotential)
                .Append(" 杯，设备上限 ").Append(Eco.EquipmentCap).Append(" 杯，实际产能 ").Append(actual).Append(" 杯。");
            if (employees >= 2)
            {
                sb.Append("\n每多雇 1 人的增量:");
                for (int n = 2; n <= employees; n++)
                {
                    int before = Mathf.Min(Eco.Capacity(n - 1), Eco.EquipmentCap);
                    int after = Mathf.Min(Eco.Capacity(n), Eco.EquipmentCap);
                    sb.Append("\n第 ").Append(n).Append(" 人：人力 +").Append(Eco.Capacity(n) - Eco.Capacity(n - 1))
                        .Append("，实际 +").Append(after - before).Append(" 杯");
                }
                if (employees >= 3) sb.Append("\n看到了吗?增量在缩小——这就是「边际产量递减」。");
            }
            else sb.Append("\n雇第 2 名员工可以 +65 杯/日,要不要试试?");
            if (laborPotential > Eco.EquipmentCap) sb.Append("\n设备已经成为瓶颈；继续加人不会等比例增加实际产出。");
            return sb.ToString();
        }

        /// <summary>日报顶部的环比摘要。</summary>
        public static string DayOverDay(GameState s, DayRecord r)
        {
            DayRecord y = s.Yesterday;
            if (y == null) return "开业首日,暂无对比数据。";
            var sb = new StringBuilder();
            sb.Append("相比昨日:");
            float currentListed = r.listedPrice > 0f ? r.listedPrice : r.price;
            float yesterdayListed = y.listedPrice > 0f ? y.listedPrice : y.price;
            if (!Mathf.Approximately(currentListed, yesterdayListed)) sb.Append("\n标价 ").Append(Pct(currentListed, yesterdayListed));
            sb.Append("\n销量 ").Append(Pct(r.sold, y.sold));
            sb.Append("\n利润 ").Append(Pct(r.profit, y.profit, true));
            return sb.ToString();
        }

        static string Pct(float now, float before, bool allowNegativeZero = false)
        {
            if (Mathf.Approximately(before, 0f)) return "—";
            float p = (now - before) / Mathf.Abs(before) * 100f;
            if (p >= 0.05f) return "↑ " + p.ToString("0") + "%";
            if (p <= -0.05f) return "↓ " + Mathf.Abs(p).ToString("0") + "%";
            return "持平";
        }
    }
}
