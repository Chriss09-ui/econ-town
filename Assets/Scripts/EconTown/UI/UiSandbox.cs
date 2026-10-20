using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        float scenarioBSpendingChange = 20f;
        int scenarioRunDays = 5;

        public void ShowExperimentConsole()
        {
            GameState s = GameState.I;
            if (s == null) return;
            SandboxService.Ensure(s);
            GameObject root = BeginPanel("经济实验控制台 · A/B 情景比较", 1120f, 780f);
            RectTransform c = ContentOf(root);

            Image warning = UiTheme.MakeImage(c, new Color32(0x75, 0x47, 0x28, 255), "experimentModeBanner");
            PlaceTopLeft(warning.rectTransform, 8f, 0f, 1056f, 48f);
            ShopText(warning.rectTransform, "实验模式 · 基于 Day " + s.day + " 的世界副本运行，结果不会修改主世界",
                18, Color.white, 16f, 0f, 1024f, 48f, TextAnchor.MiddleLeft, FontStyle.Bold);

            BuildScenarioCard(c, "Scenario A · 不干预", 8f, 66f, 510f, 178f,
                "政府支出变化　0%\n玩家售价变化　0%\n使用当前税率、工资与供给能力");
            BuildScenarioCard(c, "Scenario B · 财政实验", 554f, 66f, 510f, 178f,
                "政府支出变化　" + SignedPercent(scenarioBSpendingChange) +
                "\n玩家售价变化　0%\n其余条件与 Scenario A 完全相同");

            Button minus = UiTheme.MakeButton(c, "−10%", () =>
            {
                scenarioBSpendingChange = Mathf.Max(-100f, scenarioBSpendingChange - 10f);
                ClosePanel(root); ShowExperimentConsole();
            }, 18);
            PlaceTopLeft(minus.rectTransform(), 616f, 204f, 120f, 40f);
            Button plus = UiTheme.MakeButton(c, "+10%", () =>
            {
                scenarioBSpendingChange = Mathf.Min(300f, scenarioBSpendingChange + 10f);
                ClosePanel(root); ShowExperimentConsole();
            }, 18);
            PlaceTopLeft(plus.rectTransform(), 882f, 204f, 120f, 40f);

            Text days = ShopText(c, "运行天数　" + scenarioRunDays + " 日", 18, UiTheme.TextMain,
                8f, 260f, 260f, 42f, TextAnchor.MiddleLeft, FontStyle.Bold);
            days.name = "scenarioDays";
            Button daysMinus = UiTheme.MakeButton(c, "−", () =>
            {
                scenarioRunDays = Mathf.Max(1, scenarioRunDays - 1);
                ClosePanel(root); ShowExperimentConsole();
            }, 20);
            PlaceTopLeft(daysMinus.rectTransform(), 270f, 260f, 48f, 40f);
            Button daysPlus = UiTheme.MakeButton(c, "+", () =>
            {
                scenarioRunDays = Mathf.Min(30, scenarioRunDays + 1);
                ClosePanel(root); ShowExperimentConsole();
            }, 20);
            PlaceTopLeft(daysPlus.rectTransform(), 326f, 260f, 48f, 40f);
            Button run = UiTheme.MakeButton(c, "运行相同随机种子的 A/B 实验", () =>
            {
                SandboxService.RunComparison(s,
                    new ScenarioSettings { id = "A", label = "不干预" },
                    new ScenarioSettings { id = "B", label = "政府支出 " + SignedPercent(scenarioBSpendingChange),
                        governmentSpendingChangePercent = scenarioBSpendingChange }, scenarioRunDays);
                SaveSystem.SaveKeyMoment(s, "实验档案已更新");
                ClosePanel(root); ShowExperimentConsole();
            }, 19);
            PlaceTopLeft(run.rectTransform(), 700f, 258f, 364f, 44f);

            BuildScenarioResults(c, SandboxService.LastComparison(s), 8f, 320f, 1056f);

            Button back = UiTheme.MakeButton(c, "返回学习路线", () => { ClosePanel(root); ShowProgression(); }, 18);
            PlaceTopLeft(back.rectTransform(), 408f, 588f, 240f, 48f);
        }

        void BuildScenarioCard(RectTransform parent, string title, float x, float y, float width, float height, string body)
        {
            Image card = UiTheme.MakeImage(parent, new Color32(0x33, 0x2d, 0x29, 255), "scenarioCard");
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            ShopText(card.rectTransform, title, 21, UiTheme.Accent, 18f, 10f, width - 36f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform, body, 17, UiTheme.TextMain, 18f, 50f, width - 36f, 104f, TextAnchor.UpperLeft);
        }

        void BuildScenarioResults(RectTransform parent, ScenarioComparisonRecord record, float x, float y, float width)
        {
            Image table = UiTheme.MakeImage(parent, new Color32(0x25, 0x22, 0x20, 255), "scenarioResults");
            PlaceTopLeft(table.rectTransform, x, y, width, 250f);
            if (record == null)
            {
                ShopText(table.rectTransform, "运行一次实验后，这里会比较 GDP、失业、通胀、债务与企业利润。",
                    19, UiTheme.TextSub, 40f, 74f, width - 80f, 90f, TextAnchor.MiddleCenter);
                return;
            }
            ShopText(table.rectTransform, "核心指标", 18, UiTheme.TextSub, 20f, 10f, 240f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(table.rectTransform, "A · " + record.scenarioA.label, 18, UiTheme.Accent, 470f, 10f, 250f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            ShopText(table.rectTransform, "B · " + record.scenarioB.label, 18, new Color32(0x78, 0xd0, 0xc2, 255), 760f, 10f, 270f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            string[] labels = { "实际 GDP", "失业率", "通胀率", "政府债务", "企业利润" };
            string[] a = { record.outcomeA.realGdp.ToString("0"), record.outcomeA.unemploymentRate.ToString("0.0") + "%",
                record.outcomeA.inflation.ToString("0.0") + "%", record.outcomeA.governmentDebt.ToString("0"), record.outcomeA.businessProfit.ToString("0") };
            string[] b = { record.outcomeB.realGdp.ToString("0"), record.outcomeB.unemploymentRate.ToString("0.0") + "%",
                record.outcomeB.inflation.ToString("0.0") + "%", record.outcomeB.governmentDebt.ToString("0"), record.outcomeB.businessProfit.ToString("0") };
            for (int i = 0; i < labels.Length; i++)
            {
                float rowY = 50f + i * 36f;
                ShopText(table.rectTransform, labels[i], 17, UiTheme.TextMain, 20f, rowY, 320f, 32f, TextAnchor.MiddleLeft);
                ShopText(table.rectTransform, a[i], 17, UiTheme.TextMain, 470f, rowY, 250f, 32f, TextAnchor.MiddleCenter);
                ShopText(table.rectTransform, b[i], 17, UiTheme.TextMain, 760f, rowY, 270f, 32f, TextAnchor.MiddleCenter);
            }
        }

        public void ShowChallenges()
        {
            GameState s = GameState.I;
            if (s == null) return;
            SandboxService.Ensure(s);
            if (!s.sandbox.unlocked)
            {
                ShowToast("完成五篇主线后开放经济挑战");
                return;
            }
            if (s.sandbox.activeChallenge != null && s.sandbox.activeChallenge.active)
            {
                ShowChallengeRun();
                return;
            }
            GameObject root = BeginPanel("自由经济时代 · 经济挑战", 1000f, 760f);
            RectTransform c = ContentOf(root);
            ShopText(c, "挑战使用独立情景，不修改你的主世界。没有唯一正确答案，先活下来，再复盘。",
                18, UiTheme.TextSub, 8f, 0f, 936f, 42f, TextAnchor.MiddleCenter);
            for (int i = 0; i < ChallengeService.All.Count; i++)
                BuildChallengeCard(c, s, ChallengeService.All[i], i, root);
            Button back = UiTheme.MakeButton(c, "返回学习路线", () => { ClosePanel(root); ShowProgression(); }, 18);
            PlaceTopLeft(back.rectTransform(), 356f, 548f, 240f, 48f);
        }

        void BuildChallengeCard(RectTransform parent, GameState state, ChallengeDefinition def, int index, GameObject root)
        {
            Image card = UiTheme.MakeImage(parent, new Color32(0x3b, 0x31, 0x29, 255), "challenge_" + def.id);
            PlaceTopLeft(card.rectTransform, 8f, 58f + index * 156f, 936f, 138f);
            bool completed = state.sandbox.completedChallengeIds.Contains(def.id);
            ShopText(card.rectTransform, def.title + " · " + def.role + (completed ? "　✓ 已完成" : ""),
                21, completed ? UiTheme.Good : UiTheme.Accent, 18f, 10f, 650f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform, def.premise + "\n目标：" + def.goal, 16, UiTheme.TextMain,
                18f, 48f, 680f, 76f, TextAnchor.UpperLeft);
            Button start = UiTheme.MakeButton(card.transform, completed ? "再次挑战" : "开始挑战", () =>
            {
                ChallengeService.Start(state, def.id);
                SaveSystem.SaveKeyMoment(state, "挑战开始：" + def.title);
                ClosePanel(root);
                ShowChallengeRun();
            }, 17);
            PlaceTopLeft(start.rectTransform(), 744f, 44f, 164f, 52f);
        }

        public void ShowChallengeRun()
        {
            GameState s = GameState.I;
            if (s == null) return;
            SandboxService.Ensure(s);
            ChallengeRunState run = s.sandbox.activeChallenge;
            ChallengeDefinition def = run != null ? ChallengeService.Get(run.challengeId) : null;
            if (run == null || def == null) { ShowChallenges(); return; }
            GameObject root = BeginPanel("经济挑战 · " + def.title, 920f, 700f);
            RectTransform c = ContentOf(root);
            ShopText(c, "实验情景 · 第 " + run.elapsedDays + " / " + def.days + " 轮　剩余 " + run.remainingDays + " 轮",
                18, UiTheme.Accent, 8f, 0f, 856f, 38f, TextAnchor.MiddleCenter, FontStyle.Bold);
            string metrics = ChallengeMetrics(run);
            Image status = UiTheme.MakeImage(c, new Color32(0x32, 0x2c, 0x28, 255), "challengeStatus");
            PlaceTopLeft(status.rectTransform, 8f, 52f, 856f, 226f);
            Text statusText = ShopText(status.rectTransform, def.premise + "\n\n" + metrics + "\n\n" +
                (string.IsNullOrEmpty(run.lastSummary) ? "选择一种经济行动，运行下一轮。" : run.lastSummary),
                18, UiTheme.TextMain, 20f, 14f, 816f, 196f, TextAnchor.UpperLeft);
            float statusShift = ExpandTopText(statusText);
            if (statusShift > 1f)
            {
                status.rectTransform.sizeDelta = new Vector2(856f, 226f + statusShift);
                GrowPanel(root, statusShift);
            }

            if (run.active)
            {
                for (int i = 0; i < def.actionIds.Length; i++)
                {
                    string actionId = def.actionIds[i];
                    Button action = UiTheme.MakeButton(c, def.actionLabels[i], () =>
                    {
                        ChallengeService.ApplyAction(s, actionId);
                        SaveSystem.SaveKeyMoment(s, "挑战行动已执行");
                        ClosePanel(root);
                        ShowChallengeRun();
                    }, 18);
                    PlaceTopLeft(action.rectTransform(), 38f + i * 280f, 310f + statusShift, 256f, 58f);
                }
            }
            else
            {
                ShopText(c, run.completed ? "挑战完成 · 你找到了可持续的恢复路径" : "挑战结束 · 失败不会损坏主存档，可以立即重试",
                    21, run.completed ? UiTheme.Good : UiTheme.Bad, 80f, 320f + statusShift, 704f, 54f, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            Button retry = UiTheme.MakeButton(c, "重新开始", () =>
            {
                ChallengeService.Start(s, def.id);
                SaveSystem.SaveKeyMoment(s, "挑战重新开始");
                ClosePanel(root); ShowChallengeRun();
            }, 18);
            PlaceTopLeft(retry.rectTransform(), 206f, 440f + statusShift, 200f, 50f);
            Button back = UiTheme.MakeButton(c, "返回挑战列表", () =>
            {
                if (s.sandbox.activeChallenge != null) s.sandbox.activeChallenge.active = false;
                SaveSystem.SaveKeyMoment(s, "退出挑战");
                ClosePanel(root); ShowChallenges();
            }, 18);
            PlaceTopLeft(back.rectTransform(), 450f, 440f + statusShift, 200f, 50f);
        }

        static string ChallengeMetrics(ChallengeRunState run)
        {
            if (run.challengeId == "rescue_shop")
                return "现金 ¥" + run.cash.ToString("0") + "　库存 " + run.inventory + "　员工 " + run.employees +
                    "　售价 ¥" + run.price.ToString("0") + "　成本指数 " + run.costIndex.ToString("0.00");
            if (run.challengeId == "price_war")
                return "企业现金 ¥" + run.cash.ToString("0") + "　对手现金 ¥" + run.rivalCash.ToString("0") +
                    "　你的售价 ¥" + run.price.ToString("0") + "　品质 " + run.quality.ToString("0");
            return "通胀 " + run.inflation.ToString("0.0") + "%　失业 " + run.unemployment.ToString("0.0") +
                "%　增长 " + run.gdpGrowth.ToString("0.0") + "%　债务率 " + run.debtRatio.ToString("0.0") + "%";
        }

        static string SignedPercent(float value) { return (value >= 0f ? "+" : "") + value.ToString("0") + "%"; }
    }
}
