using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>章节/政策/结局等全部展示文案与格式化。（partial 拆分自 GovernmentSimulator.cs,逻辑未变。）</summary>
    public static partial class GovernmentSimulator
    {
        public static string ChapterTitle(int chapter)
        {
            string[] titles = { "", "第一章 · 为什么需要政府", "第二章 · 工厂污染了河流",
                "第三章 · 一杯饮品的税", "第四章 · 工资太低了", "第五章 · 东西太贵了",
                "第六章 · 政府的钱不够了", "第七章 · 借来的钱以后要还" };
            return titles[Mathf.Clamp(chapter, 1, 7)];
        }

        public static string FormatPolicyValue(PolicyState policy)
        {
            if (policy == null) return "—";
            // bug搜#7:关税是从价百分率(0~25%,结算按 值/100×进口基),按金额格式化成
            // 「¥25/单位」会让玩家把 25% 误读成每单位 ¥25 的从量税,量级差一个数量级以上。
            if (policy.type == PolicyType.ImportTariff) return policy.value.ToString("0") + "%";
            if (policy.type == PolicyType.GreenEquipmentSubsidy) return policy.value.ToString("0") + "%";
            if (policy.type == PolicyType.MinimumWage) return "¥" + policy.value.ToString("0") + "/小时";
            if (policy.type == PolicyType.PublicServiceBudget || policy.type == PolicyType.BasicWelfare)
                return "¥" + policy.value.ToString("0") + "/周期";
            if (policy.type == PolicyType.PriceCeiling || policy.type == PolicyType.PriceFloor)
                return policy.value <= 0f ? "未启用" : "¥" + policy.value.ToString("0");
            return "¥" + policy.value.ToString("0") + "/单位";
        }

        public static string TermStyle(GovernmentState government)
        {
            if (government == null) return "尚未形成任期风格";
            float pollution = Value(government, PolicyType.PollutionTax) + Value(government, PolicyType.GreenEquipmentSubsidy) * 0.05f;
            float welfare = Value(government, PolicyType.BasicWelfare) + Value(government, PolicyType.PublicServiceBudget) * 0.2f;
            int active = 0;
            for (int i = 0; i < government.policies.Count; i++) if (government.policies[i].active) active++;
            if (pollution >= 6f && government.environmentQuality >= 70f) return "绿色市长";
            if (welfare >= 150f) return "福利市长";
            if (government.debt <= 100f && AverageBalance(government) >= 0f) return "财政鹰派";
            if (active >= 6) return "干预大师";
            return "自由市场派";
        }

        public static string Grade(float value)
        {
            if (value >= 88f) return "A";
            if (value >= 80f) return "A-";
            if (value >= 72f) return "B+";
            if (value >= 64f) return "B";
            if (value >= 56f) return "C+";
            if (value >= 48f) return "C";
            return "D";
        }

        public static float FiscalHealth(GovernmentState government)
        {
            if (government == null) return 0f;
            float debtPenalty = Mathf.Clamp(government.debt / 200f, 0f, 50f);
            float balanceScore = Mathf.Clamp(50f + AverageBalance(government) / 20f, 0f, 100f);
            return Mathf.Clamp(balanceScore - debtPenalty + 20f, 0f, 100f);
        }

        static float AverageBalance(GovernmentState government)
        {
            if (government == null || government.history == null || government.history.Count == 0)
                return government == null ? 0f : government.budgetBalance;
            float sum = 0f;
            for (int i = 0; i < government.history.Count; i++) sum += government.history[i].budgetBalance;
            return sum / government.history.Count;
        }

        static float Value(GovernmentState government, PolicyType type)
        {
            PolicyState policy = FindPolicy(government, type);
            return policy == null ? 0f : policy.value;
        }

        static string BuildCycleSummary(GovernmentState government, float balance)
        {
            return "第 " + government.cycle + " 个政策周期：财政" +
                (balance >= 0f ? "盈余 ¥" : "赤字 ¥") + Mathf.Abs(balance).ToString("0") +
                "，就业率 " + government.employmentRate.ToString("0.0") + "% ，环境 " +
                government.environmentQuality.ToString("0") + "，支持率 " +
                government.approvalRating.ToString("0") + "% 。";
        }

        static string FeedbackForPolicy(PolicyType type, float value)
        {
            switch (type)
            {
                case PolicyType.DrinkTax: return value > 0f ? "居民：饮品贵了一些；店主：这笔税不可能全由一方承担。" : "店主：取消税收后，价格与销量会重新调整。";
                case PolicyType.DrinkSubsidy: return "居民：补贴让健康饮品便宜了，但这笔钱来自政府预算。";
                case PolicyType.PollutionTax: return "工厂老板：污染型生产更贵了，我们得考虑减产或升级设备。";
                case PolicyType.GreenEquipmentSubsidy: return "工厂老板：环保设备仍不便宜，不过补贴确实降低了门槛。";
                case PolicyType.TrainingSubsidy: return "学生：培训便宜以后，我更愿意参加，企业也会受益。";
                case PolicyType.MinimumWage: return "员工：工资涨了；店主：是否缩减招聘还要看利润和需求。";
                case PolicyType.PriceCeiling: return "居民：标价低了，但能不能买到还要看供给。";
                case PolicyType.PriceFloor: return "商家：最低价提高了收入空间，但可能卖不完。";
                case PolicyType.BasicWelfare: return "低收入居民：补贴改善了生活，但财政能否持续很重要。";
                case PolicyType.ImportTariff: return "店主：进口货贵了，我的销量好了一点；顾客：选择变少、价格变高。";
                default: return "居民：公共服务改善需要持续维护，而不是只付一次建设费。";
            }
        }

        static string WinnersFor(PolicyType type)
        {
            switch (type)
            {
                case PolicyType.DrinkTax: return "政府财政；健康替代品提供者";
                case PolicyType.DrinkSubsidy: return "饮品消费者；饮品企业";
                case PolicyType.PollutionTax: return "受污染居民；环境";
                case PolicyType.GreenEquipmentSubsidy: return "改造企业；受污染居民";
                case PolicyType.TrainingSubsidy: return "参加培训的居民；未来用工企业";
                case PolicyType.MinimumWage: return "仍在就业的低工资员工";
                case PolicyType.PriceCeiling: return "成功买到商品的消费者";
                case PolicyType.PriceFloor: return "仍能售出的生产者";
                case PolicyType.BasicWelfare: return "低收入居民；基础消费行业";
                case PolicyType.ImportTariff: return "受保护的国内同类企业；政府关税收入；上游供应商";
                default: return "公共服务使用者；周边商户";
            }
        }

        static string LosersFor(PolicyType type)
        {
            switch (type)
            {
                case PolicyType.DrinkTax: return "部分消费者；饮品企业；退出市场的交易者";
                case PolicyType.DrinkSubsidy: return "政府财政；未被补贴的替代行业";
                case PolicyType.PollutionTax: return "高污染企业；部分依赖该行业的就业者";
                case PolicyType.GreenEquipmentSubsidy: return "政府财政；未获得补贴的企业";
                case PolicyType.TrainingSubsidy: return "政府财政；未参加培训者";
                case PolicyType.MinimumWage: return "利润薄弱企业；可能失去岗位的居民；部分消费者";
                case PolicyType.PriceCeiling: return "无法买到商品的消费者；供应者";
                case PolicyType.PriceFloor: return "消费者；卖不掉产品的生产者";
                case PolicyType.BasicWelfare: return "政府财政；未来承担债务成本的居民";
                case PolicyType.ImportTariff: return "购买进口品的消费者；下游用进口原料的企业；出口受阻的贸易伙伴行业";
                default: return "政府财政；不使用该服务但共同纳税者";
            }
        }

        static string GovernmentEffectFor(PolicyType type)
        {
            if (type == PolicyType.DrinkTax || type == PolicyType.PollutionTax ||
                type == PolicyType.ImportTariff) return "税收收入增加，但税基可能缩小。";
            if (type == PolicyType.PriceCeiling || type == PolicyType.PriceFloor || type == PolicyType.MinimumWage)
                return "直接支出不一定增加，但税收、就业与福利支出会间接变化。";
            return "政府支出增加，财政空间缩小。";
        }

        static string CausalChainFor(PolicyType type)
        {
            switch (type)
            {
                case PolicyType.DrinkTax: return "饮品税上升 → 消费者支付价上升 / 企业实收下降 → 需求与供给调整 → 交易量下降 → 政府获得税收并出现无谓损失";
                case PolicyType.DrinkSubsidy: return "补贴上升 → 消费者价格下降 / 企业实收上升 → 供需扩大 → 交易增加 → 政府支出上升";
                case PolicyType.PollutionTax: return "污染税上升 → 污染型生产成本上升 → 企业减产或技术升级 → 污染下降，同时利润与就业承压";
                case PolicyType.GreenEquipmentSubsidy: return "环保补贴上升 → 改造自付成本下降 → 设备升级 → 单位污染下降 → 政府支出上升";
                case PolicyType.TrainingSubsidy: return "培训补贴上升 → 参加成本下降 → 培训人数增加 → 居民技能与企业生产率提高";
                case PolicyType.MinimumWage: return "最低工资上升 → 低工资员工收入增加 → 企业成本上升 → 消费、价格与招聘依利润空间共同调整";
                case PolicyType.PriceCeiling: return "价格上限低于均衡价 → 消费价格下降 → 需求增加、供给减少 → 短缺与排队";
                case PolicyType.PriceFloor: return "价格下限高于均衡价 → 消费价格上升 → 需求减少、供给增加 → 过剩";
                case PolicyType.BasicWelfare: return "生活补贴增加 → 低收入购买力提高 → 消费与幸福上升 → 政府支出与债务压力增加";
                case PolicyType.ImportTariff: return "关税率上升 → 进口到岸价上升 → 进口量下降、国产替代上升 → 消费者剩余下降并出现无谓损失 → 政府获得关税×实际进口量";
                default: return "公共投入增加 → 服务覆盖改善 → 居民幸福与商业吸引力提高 → 建设和维护支出增加";
            }
        }
    }
}
