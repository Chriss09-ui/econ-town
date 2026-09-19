using UnityEngine;

namespace EconTown
{
    public static class ChallengeService
    {
        static readonly ChallengeDefinition[] all =
        {
            new ChallengeDefinition
            {
                id = "rescue_shop", title = "濒临破产的饮品店", role = "企业主",
                premise = "现金 ¥300、库存积压、4 名员工与高固定成本。",
                goal = "10 日内恢复正现金流并保持现金大于 0。", days = 10,
                actionIds = new[] { "clear", "cut", "reprice" },
                actionLabels = new[] { "降价清库存", "紧缩经营", "重新定价" }
            },
            new ChallengeDefinition
            {
                id = "price_war", title = "价格战求生", role = "企业主",
                premise = "双方品牌与份额接近，Sunny Tea 主动降价。",
                goal = "5 轮后企业仍然存续，不要求击败对手。", days = 5,
                actionIds = new[] { "follow", "differentiate", "hold" },
                actionLabels = new[] { "跟随降价", "提高差异化", "保持价格" }
            },
            new ChallengeDefinition
            {
                id = "high_inflation", title = "高通胀治理", role = "市长",
                premise = "通胀 8%、失业 4%、增长较快，只能使用财政与供给侧工具。",
                goal = "6 期内把通胀压到 4% 以下，同时失业不超过 10%。", days = 6,
                actionIds = new[] { "tighten", "tax", "supply" },
                actionLabels = new[] { "削减政府支出", "临时增税降温", "投资供给能力" }
            }
        };

        public static System.Collections.Generic.IList<ChallengeDefinition> All { get { return all; } }

        public static ChallengeDefinition Get(string id)
        {
            for (int i = 0; i < all.Length; i++) if (all[i].id == id) return all[i];
            return null;
        }

        public static ChallengeRunState Start(GameState state, string challengeId)
        {
            ChallengeDefinition def = Get(challengeId);
            if (state == null || def == null) return null;
            SandboxService.Ensure(state);
            ChallengeRunState run = new ChallengeRunState
            {
                challengeId = def.id,
                active = true,
                remainingDays = def.days,
                random = new DeterministicRandomState()
            };
            int seed = state.economy != null && state.economy.random != null ? state.economy.random.seed : 20260825;
            run.random.Reset(seed ^ StableChallengeSalt(def.id));
            if (def.id == "rescue_shop")
            {
                run.cash = 300f; run.inventory = 240; run.employees = 4; run.price = 15f; run.costIndex = 1.45f;
            }
            else if (def.id == "price_war")
            {
                run.cash = 2000f; run.rivalCash = 2000f; run.price = 18f; run.competitorPrice = 14f; run.quality = 50f;
            }
            else
            {
                run.inflation = 8f; run.unemployment = 4f; run.gdpGrowth = 5f; run.debtRatio = 35f;
            }
            state.sandbox.activeChallenge = run;
            state.Changed();
            return run;
        }

        public static ChallengeRunState ApplyAction(GameState state, string actionId)
        {
            if (state == null) return null;
            SandboxService.Ensure(state);
            ChallengeRunState run = state.sandbox.activeChallenge;
            ChallengeDefinition def = run != null ? Get(run.challengeId) : null;
            if (run == null || def == null || !run.active) return run;
            if (!Contains(def.actionIds, actionId)) return run;

            if (run.challengeId == "rescue_shop") ApplyRescueAction(run, actionId);
            else if (run.challengeId == "price_war") ApplyPriceWarAction(run, actionId);
            else ApplyInflationAction(run, actionId);

            run.elapsedDays++;
            run.remainingDays = Mathf.Max(0, def.days - run.elapsedDays);
            Evaluate(run, def);
            if (run.completed)
            {
                run.active = false;
                if (!state.sandbox.completedChallengeIds.Contains(run.challengeId))
                    state.sandbox.completedChallengeIds.Add(run.challengeId);
            }
            else if (run.failed) run.active = false;
            state.Changed();
            return run;
        }

        static void ApplyRescueAction(ChallengeRunState run, string action)
        {
            if (action == "clear") run.price = Mathf.Max(8f, run.price - 2f);
            else if (action == "cut")
            {
                run.employees = Mathf.Max(1, run.employees - 1);
                run.costIndex = Mathf.Max(.72f, run.costIndex * .86f);
            }
            else run.price = Mathf.Min(24f, run.price + 1f);
            int demand = Mathf.Clamp(Mathf.RoundToInt(104f - (run.price - 12f) * 6f +
                (run.random.Next01() - .5f) * 12f), 10, 130);
            int sold = Mathf.Min(run.inventory, demand);
            float costs = sold * 5f * run.costIndex + run.employees * 42f + 120f * run.costIndex;
            run.lastCashFlow = run.price * sold - costs;
            run.cash += run.lastCashFlow;
            run.inventory = Mathf.Max(0, run.inventory - sold);
            run.lastAction = action;
            run.lastSummary = "售出 " + sold + " 杯，现金流 " + SignedMoney(run.lastCashFlow) + "，现金 ¥" + run.cash.ToString("0") + "。";
        }

        static void ApplyPriceWarAction(ChallengeRunState run, string action)
        {
            if (action == "follow") run.price = Mathf.Max(9f, run.competitorPrice);
            else if (action == "differentiate") { run.quality = Mathf.Min(100f, run.quality + 12f); run.cash -= 220f; }
            float playerAppeal = (22f - run.price) + run.quality * .10f;
            float rivalAppeal = (22f - run.competitorPrice) + 5f;
            float share = Mathf.Clamp(playerAppeal / Mathf.Max(1f, playerAppeal + rivalAppeal), .1f, .9f);
            int market = 180 + Mathf.RoundToInt((run.random.Next01() - .5f) * 20f);
            int sold = Mathf.RoundToInt(market * share);
            int rivalSold = market - sold;
            run.lastCashFlow = sold * (run.price - 8f) - 180f;
            float rivalFlow = rivalSold * (run.competitorPrice - 8f) - 180f;
            run.cash += run.lastCashFlow;
            run.rivalCash += rivalFlow;
            if (action == "follow") run.competitorPrice = Mathf.Max(9f, run.price - 1f);
            else if (action == "differentiate") run.competitorPrice = Mathf.Min(18f, run.competitorPrice + 1f);
            run.lastAction = action;
            run.lastSummary = "你的份额 " + (share * 100f).ToString("0") + "% ，现金流 " + SignedMoney(run.lastCashFlow) +
                "，企业现金 ¥" + run.cash.ToString("0") + "。";
        }

        static void ApplyInflationAction(ChallengeRunState run, string action)
        {
            float noise = (run.random.Next01() - .5f) * .35f;
            if (action == "tighten")
            {
                run.inflation -= 1.25f; run.gdpGrowth -= 1.2f; run.unemployment += .8f; run.debtRatio -= .4f;
            }
            else if (action == "tax")
            {
                run.inflation -= .9f; run.gdpGrowth -= .7f; run.unemployment += .45f; run.debtRatio -= .8f;
            }
            else
            {
                run.inflation -= .65f; run.gdpGrowth += .2f; run.unemployment -= .25f; run.debtRatio += 1.2f;
            }
            run.inflation = Mathf.Clamp(run.inflation + noise, -5f, 30f);
            run.unemployment = Mathf.Clamp(run.unemployment - run.gdpGrowth * .04f, 0f, 30f);
            run.lastAction = action;
            run.lastSummary = "通胀 " + run.inflation.ToString("0.0") + "% ，失业 " + run.unemployment.ToString("0.0") +
                "% ，增长 " + run.gdpGrowth.ToString("0.0") + "% ，债务率 " + run.debtRatio.ToString("0.0") + "% 。";
        }

        static void Evaluate(ChallengeRunState run, ChallengeDefinition def)
        {
            if (run.challengeId == "rescue_shop")
            {
                run.completed = run.lastCashFlow > 0f && run.cash > 0f && run.elapsedDays >= 2;
                run.failed = run.cash <= -1200f || (run.remainingDays == 0 && !run.completed);
            }
            else if (run.challengeId == "price_war")
            {
                run.failed = run.cash <= 0f;
                run.completed = run.remainingDays == 0 && !run.failed;
            }
            else
            {
                run.completed = run.inflation < 4f && run.unemployment <= 10f;
                run.failed = run.remainingDays == 0 && !run.completed;
            }
            if (run.completed) run.lastSummary += " 挑战目标已达成。";
            else if (run.failed) run.lastSummary += " 挑战结束，可复盘后重试。";
        }

        static bool Contains(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++) if (values[i] == value) return true;
            return false;
        }

        static int StableChallengeSalt(string id)
        {
            int hash = 17;
            for (int i = 0; i < id.Length; i++) hash = unchecked(hash * 31 + id[i]);
            return hash;
        }

        static string SignedMoney(float value) { return (value >= 0f ? "+¥" : "-¥") + Mathf.Abs(value).ToString("0"); }
    }
}
