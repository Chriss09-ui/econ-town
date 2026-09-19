using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>随机城市事件、章节推进与政策知识卡解锁。（partial 拆分自 GovernmentSimulator.cs,逻辑未变。）</summary>
    public static partial class GovernmentSimulator
    {
        static void RollEvent(GovernmentState government)
        {
            if (government.currentEvent != null && !government.currentEvent.resolved) return;
            int cycle = government.cycle;
            if (cycle == 2)
                government.currentEvent = Event("pollution_accident", "河流污染事故",
                    "工厂排放事故使环境质量突然下降，需要决定如何处理。",
                    "政府出资治理", "提高污染税", "暂不干预");
            else if (cycle == 4)
                government.currentEvent = Event("shortage", "饮品原料短缺",
                    "水果供应减少，饮品价格面临上涨压力。",
                    "补贴供给", "设置价格上限", "让市场调整");
            else if (cycle == 6)
                government.currentEvent = Event("layoff", "企业裁员",
                    "部分企业利润下降，宣布缩减招聘。",
                    "临时就业补贴", "降低企业负担", "不直接干预");
            else if (cycle == 8)
                government.currentEvent = Event("price_protest", "居民抗议物价",
                    "居民要求降低基本生活成本。",
                    "发放生活补贴", "实施限价", "解释并观察");
        }

        static GovernmentEventState Event(string id, string title, string description,
            string optionA, string optionB, string optionC)
        {
            return new GovernmentEventState
            {
                id = id,
                title = title,
                description = description,
                optionA = optionA,
                optionB = optionB,
                optionC = optionC
            };
        }

        public static bool ResolveEvent(GameState state, int option)
        {
            Ensure(state);
            GovernmentEventState current = state.government.currentEvent;
            if (current == null || current.resolved || option < 0 || option > 2) return false;
            current.resolved = true;
            current.chosenOption = option == 0 ? current.optionA : option == 1 ? current.optionB : current.optionC;
            GovernmentState government = state.government;
            bool policyUnchanged = false;
            if (current.id == "pollution_accident")
            {
                government.environmentQuality = Mathf.Clamp(government.environmentQuality - 20f, 0f, 100f);
                if (option == 0)
                {
                    // 记入资本支出科目,现金由收日 AdvanceCycle 统一结算:此前直接 Max(0, cash-1000) 截断,
                    // 现金不足时治理效果全额生效但支出缩水(白嫖),且这笔钱不进任何账本科目(P2-4)。
                    government.pendingCapitalSpending += 1000f;
                    government.environmentQuality += 16f;
                }
                else if (option == 1) policyUnchanged = !SetPolicy(state, PolicyType.PollutionTax, Mathf.Max(3f, PolicyValue(state, PolicyType.PollutionTax)));
            }
            else if (current.id == "shortage")
            {
                if (option == 0) policyUnchanged = !SetPolicy(state, PolicyType.DrinkSubsidy, Mathf.Max(2f, PolicyValue(state, PolicyType.DrinkSubsidy)));
                else if (option == 1) policyUnchanged = !SetPolicy(state, PolicyType.PriceCeiling, 15f);
                else
                {
                    government.eventAveragePriceOffset += 2f;
                    government.averagePrice += 2f;
                }
            }
            else if (current.id == "layoff")
            {
                government.eventEmploymentRateOffset -= 4f;
                government.employmentRate = Mathf.Clamp(government.employmentRate - 4f, 0f, 100f);
                if (option == 0)
                {
                    // 同污染事件:就业补贴记入资本支出,现金不足自然形成赤字(P2-4)。
                    government.pendingCapitalSpending += 800f;
                    government.eventEmploymentRateOffset += 3f;
                    government.employmentRate = Mathf.Clamp(government.employmentRate + 3f, 0f, 100f);
                }
                else if (option == 1)
                {
                    government.eventEnterpriseConfidenceOffset += 5f;
                    government.enterpriseConfidence = Mathf.Clamp(government.enterpriseConfidence + 5f, 15f, 100f);
                }
            }
            else if (current.id == "price_protest")
            {
                if (option == 0) policyUnchanged = !SetPolicy(state, PolicyType.BasicWelfare, Mathf.Max(50f, PolicyValue(state, PolicyType.BasicWelfare)));
                else if (option == 1) policyUnchanged = !SetPolicy(state, PolicyType.PriceCeiling, 15f);
                else
                {
                    government.eventResidentSupportOffset -= 5f;
                    government.residentSupport = Mathf.Clamp(government.residentSupport - 5f, 0f, 100f);
                }
            }
            // SetPolicy 在目标与现状冲突(如价格下限高于上限)或数值相同时静默失败:
            // 事件仍算已处理,但要把"政策实际未变"写进摘要,否则玩家以为政策生效了(P2-5)。
            government.lastPolicySummary = "事件「" + current.title + "」：选择了“" + current.chosenOption + "”。" +
                (policyUnchanged ? "（政策未调整：目标与现状冲突或相同，无实际变化）" : "");
            state.Changed();
            return true;
        }

        static void ProgressChapterForPolicy(GameState state, PolicyType type, float value)
        {
            GovernmentState government = state.government;
            if (government.chapter >= 2 && (type == PolicyType.PollutionTax || type == PolicyType.GreenEquipmentSubsidy) && value > 0f)
                UnlockChapter(state, 3);
            if (government.chapter >= 3 && type == PolicyType.DrinkTax && value > 0f)
                UnlockChapter(state, 4);
            if (government.chapter >= 4 && type == PolicyType.MinimumWage && value > 20f)
                UnlockChapter(state, 5);
            if (government.chapter >= 5 && type == PolicyType.PriceCeiling && value > 0f)
                UnlockChapter(state, 6);
        }

        static void ProgressFiscalChapter(GameState state)
        {
            GovernmentState government = state.government;
            if (government.chapter >= 6 && government.budgetBalance < 0f) UnlockChapter(state, 7);
            if (government.chapter >= 7 && government.debt > 0f)
            {
                state.UnlockCard("governmentdebt");
                state.UnlockCard("fiscaldeficit");
            }
        }

        static void UnlockChapter(GameState state, int chapter)
        {
            GovernmentState government = state.government;
            government.chapter = Mathf.Max(government.chapter, chapter);
            for (int i = 1; i <= government.chapter; i++) government.chapterMask |= 1 << (i - 1);
        }

        static void UnlockPolicyCards(GameState state, PolicyType type, float value)
        {
            if (value <= 0f && type != PolicyType.MinimumWage) return;
            if (type == PolicyType.DrinkTax)
            {
                state.UnlockCard("commoditytax"); state.UnlockCard("taxincidence");
                state.UnlockCard("taxelasticity"); state.UnlockCard("deadweightloss");
            }
            else if (type == PolicyType.DrinkSubsidy || type == PolicyType.TrainingSubsidy)
            {
                state.UnlockCard("subsidy");
                if (type == PolicyType.TrainingSubsidy)
                {
                    state.UnlockCard("externality");
                    state.UnlockCard("positiveexternality");
                }
            }
            else if (type == PolicyType.PollutionTax || type == PolicyType.GreenEquipmentSubsidy)
            {
                state.UnlockCard("externality"); state.UnlockCard("negativeexternality");
            }
            else if (type == PolicyType.MinimumWage) state.UnlockCard("minimumwage");
            else if (type == PolicyType.PriceCeiling)
            {
                state.UnlockCard("priceceiling"); state.UnlockCard("shortage");
            }
            else if (type == PolicyType.PriceFloor)
            {
                state.UnlockCard("pricefloor"); state.UnlockCard("surplus");
            }
            else if (type == PolicyType.BasicWelfare) state.UnlockCard("equityefficiency");
        }

    }
}
