using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>经济实验室(曲线中心) + 经济学手册 + 设置(见 PRD §21/§22)。</summary>
    public partial class UIManager
    {
        int curveTab;
        int curveCutoff = int.MaxValue;

        // V0.9.4 住房实验室(第 7 页签,PRD §197.6/R9):结果暂存实例字段——不落档,重开面板即确定性重跑
        MortgageStressResult housingStressResult;
        BubbleExperimentResult housingBubbleResult;
        float housingLabHikePp = 2f;

        string settingsHint = "";
        Text settingsHintLine;

        bool ChartAutoScale
        {
            get
            {
                GameState state = GameState.I;
                return state == null || state.experience == null || state.experience.settings == null ||
                    state.experience.settings.chartAutoScale;
            }
        }

        public void ShowCurves(int tab)
        {
            GameState s = GameState.I;
            if (s == null) return;
            curveTab = tab;

            GameObject root = BeginPanel("经济实验室 · 用你自己的数据画曲线", 1120, 720);
            RectTransform c = ContentOf(root);

            // 数据工作台：标签与时间窗口收进同一条工具栏，避免控件在宽屏上被拆散。
            Image toolbar = UiTheme.MakeImage(c, new Color32(0x3a, 0x30, 0x29, 250), "curveToolbar");
            PlaceTopLeft(toolbar.rectTransform, 8f, 0f, 1056f, 104f);

            // V0.9.4 起第 7 页签「住房实验室」;按钮宽 162→140、步进 172→149(7 个不溢出 1056 工具栏)
            string[] tabs = { "需求", "成本", "收入利润", "市场份额", "价格走势", "竞争利润", "住房实验室" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                Button b = UiTheme.MakeButton(toolbar.transform, tabs[i], () => { CloseTop(); ShowCurves(idx); }, 18);
                b.name = "curveTab_" + i;
                PlaceTopLeft(b.rectTransform(), 10f + i * 149f, 8f, 140f, 44f);
                Text tabText = b.GetComponentInChildren<Text>();
                if (i == curveTab)
                {
                    b.image.color = new Color32(0x4f, 0x91, 0x89, 255);
                    if (tabText != null) tabText.color = UiTheme.TextMain;
                    Image activeLine = UiTheme.MakeImage(b.transform, UiTheme.Accent, "activeLine");
                    PlaceTopLeft(activeLine.rectTransform, 0f, 40f, 140f, 4f);
                }
                else
                {
                    b.image.color = new Color32(0xe7, 0xc9, 0x98, 255);
                    if (tabText != null) tabText.color = new Color32(0x4a, 0x38, 0x2c, 255);
                }
            }

            if (curveTab != 6)
            {
                int latest = s.history.Count > 0 ? s.history[s.history.Count - 1].day : s.day;
                if (curveCutoff > latest) curveCutoff = latest;
                if (curveCutoff < 1) curveCutoff = 1;
                ShopText(toolbar.rectTransform, "按经营日回看曲线变化", 16, UiTheme.TextSub,
                    12f, 60f, 360f, 34f, TextAnchor.MiddleLeft);

                Image cutoffStepper = UiTheme.MakeImage(toolbar.transform,
                    new Color32(0x50, 0x3b, 0x2d, 255), "curveCutoffStepper");
                PlaceTopLeft(cutoffStepper.rectTransform, 716f, 57f, 328f, 39f);
                ShopText(cutoffStepper.rectTransform, "数据截至", 16, UiTheme.TextSub,
                    12f, 0f, 90f, 39f, TextAnchor.MiddleLeft, FontStyle.Bold);

                Button cutMinus = UiTheme.MakeButton(cutoffStepper.transform, "-", () =>
                {
                    curveCutoff = Mathf.Max(1, curveCutoff - 1);
                    CloseTop();
                    ShowCurves(curveTab);
                }, 22);
                cutMinus.name = "curveCutoffMinus";
                PlaceTopLeft(cutMinus.rectTransform(), 112f, 1f, 40f, 37f);
                cutMinus.interactable = curveCutoff > 1;

                Text cutValue = ShopText(cutoffStepper.rectTransform, "第 " + curveCutoff + " 天", 18,
                    UiTheme.Accent, 158f, 0f, 94f, 39f, TextAnchor.MiddleCenter, FontStyle.Bold);
                cutValue.name = "curveCutoffValue";

                Button cutPlus = UiTheme.MakeButton(cutoffStepper.transform, "+", () =>
                {
                    curveCutoff = Mathf.Min(latest, curveCutoff + 1);
                    CloseTop();
                    ShowCurves(curveTab);
                }, 22);
                cutPlus.name = "curveCutoffPlus";
                PlaceTopLeft(cutPlus.rectTransform(), 258f, 1f, 40f, 37f);
                cutPlus.interactable = curveCutoff < latest;

                // 绘图区
                Image plotBg = UiTheme.MakeImage(c, new Color32(0x24, 0x1e, 0x18, 255), "plot");
                plotBg.rectTransform.anchorMin = plotBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                plotBg.rectTransform.anchoredPosition = new Vector2(-20, -38);
                plotBg.rectTransform.sizeDelta = new Vector2(1000, 360);

                GameObject graphGo = new GameObject("graph", typeof(RectTransform), typeof(CanvasRenderer));
                graphGo.transform.SetParent(plotBg.transform, false);
                PolylineGraph graph = graphGo.AddComponent<PolylineGraph>();
                graph.raycastTarget = true;
                graph.rectTransform.Stretch();
                graph.rectTransform.offsetMin = new Vector2(6, 6);
                graph.rectTransform.offsetMax = new Vector2(-6, -6);

                // 图例 + 摘要
                Text legend = UiTheme.MakeText(c, "", 20, UiTheme.TextSub, TextAnchor.UpperLeft);
                legend.rectTransform.anchorMin = legend.rectTransform.anchorMax = new Vector2(0, 0);
                legend.rectTransform.pivot = new Vector2(0, 0);
                legend.rectTransform.anchoredPosition = new Vector2(10, 10);
                legend.rectTransform.sizeDelta = new Vector2(1000, 108);

                List<DayRecord> data = s.history.FindAll(x => x.day <= curveCutoff);
                if (curveTab == 0) BuildDemandGraph(graph, legend, data);
                else if (curveTab == 1) BuildCostGraph(graph, legend, data);
                else if (curveTab == 2) BuildRevenueGraph(graph, legend, data);
                else BuildCompetitionGraph(graph, legend, data, curveTab);
            }
            else BuildHousingLab(c, s);

            Button close = UiTheme.MakeButton(c, "关闭实验室", CloseTop, 21);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(1, 0);
            close.rectTransform().pivot = new Vector2(1f, 0f);
            close.rectTransform().anchoredPosition = new Vector2(-120, 8);
            close.rectTransform().sizeDelta = new Vector2(220, 48);
        }

        // ———————————————— V0.9.4 住房实验室(第 7 页签,PRD §197.6/R9) ————————————————

        /// <summary>住房实验室页签:两个分叉实验卡片(点击同步跑,主世界零扰动)。
        /// 修复轮回H:布局总深 654 超出 1120×720 面板内容区(632)——泡沫卡穿出底边框、
        /// 「未跑满」提示与副题/关闭按钮重叠;压到 ≤628(横幅 112–152 / 压测卡 160–410 / 泡沫卡 416–628)。</summary>
        void BuildHousingLab(RectTransform c, GameState s)
        {
            Image banner = UiTheme.MakeImage(c, new Color32(0x3a, 0x30, 0x29, 250), "labBanner");
            PlaceTopLeft(banner.rectTransform, 8f, 112f, 1056f, 40f);
            ShopText(banner.rectTransform,
                "实验模式 · 基于第 " + s.day + " 天的世界副本同步重放 · 结果不修改主世界",
                17, UiTheme.Accent, 12f, 0f, 760f, 40f, TextAnchor.MiddleLeft, FontStyle.Bold);

            BuildStressCard(c, s);
            BuildBubbleCard(c, s);
        }

        void BuildStressCard(RectTransform c, GameState s)
        {
            Image card = UiTheme.MakeImage(c, new Color32(0x2c, 0x25, 0x1e, 255), "labStressCard");
            PlaceTopLeft(card.rectTransform, 8f, 160f, 1056f, 250f);
            ShopText(card.rectTransform, "房贷压力测试 · 利率路径 A 维持 vs B 加息", 20, UiTheme.TextMain,
                14f, 10f, 520f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform,
                "同一世界克隆两份各重放 52 期;存量房贷利率放款日锁定——加息不经你的月供,经镇房价与存款利息",
                15, UiTheme.TextSub, 14f, 42f, 940f, 26f, TextAnchor.MiddleLeft);

            Button minus = UiTheme.MakeButton(card.transform, "-", () =>
            { housingLabHikePp = Mathf.Max(0.5f, housingLabHikePp - 0.5f); CloseTop(); ShowCurves(6); }, 20);
            PlaceTopLeft(minus.rectTransform(), 794f, 12f, 40f, 34f);
            Text hike = ShopText(card.rectTransform, "+" + housingLabHikePp.ToString("0.0") + "pp", 17,
                UiTheme.Accent, 840f, 12f, 92f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            hike.name = "labHikeValue";
            Button plus = UiTheme.MakeButton(card.transform, "+", () =>
            { housingLabHikePp = Mathf.Min(4f, housingLabHikePp + 0.5f); CloseTop(); ShowCurves(6); }, 20);
            PlaceTopLeft(plus.rectTransform(), 938f, 12f, 40f, 34f);

            Button run = UiTheme.MakeButton(card.transform, "运行 52 期", () =>
            {
                housingStressResult = HousingLabService.RunMortgageStress(s, housingLabHikePp, 52);
                if (!string.IsNullOrEmpty(housingStressResult.refusal)) ShowToast(housingStressResult.refusal);
                CloseTop();
                ShowCurves(6);
            }, 18);
            PlaceTopLeft(run.rectTransform(), 14f, 76f, 180f, 38f);

            MortgageStressResult r = housingStressResult;
            if (r == null || r.outcomeA == null)
            {
                ShopText(card.rectTransform, "尚未运行——点击「运行 52 期」生成对照", 16, UiTheme.TextSub,
                    210f, 82f, 700f, 30f, TextAnchor.MiddleLeft);
                return;
            }
            if (!r.runA.completed || !r.runB.completed)
                ShopText(card.rectTransform, "未跑满:" +
                    (r.runA.completed ? r.runB.blockedReason : r.runA.blockedReason), 14,
                    new Color32(0xd9, 0x6a, 0x4a, 255), 210f, 70f, 700f, 20f, TextAnchor.MiddleLeft);

            ShopText(card.rectTransform, "读数", 15, UiTheme.TextSub, 22f, 118f, 190f, 24f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform, "A 维持", 15, UiTheme.TextSub, 230f, 118f, 200f, 24f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform, "B 加息", 15, UiTheme.TextSub, 450f, 118f, 200f, 24f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform, "Δ(B−A)", 15, UiTheme.TextSub, 670f, 118f, 220f, 24f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            LabRow(card.rectTransform, 144, "DSR(供款/收入)",
                r.outcomeA.dsr.ToString("0.00"), r.outcomeB.dsr.ToString("0.00"),
                LabDeltaF(r.outcomeB.dsr, r.outcomeA.dsr, "0.00"));
            LabRow(card.rectTransform, 170, "净资产(存款+房产净值)",
                LabMoney(r.outcomeA.netWorth), LabMoney(r.outcomeB.netWorth),
                LabDelta(r.outcomeB.netWorth, r.outcomeA.netWorth));
            LabRow(card.rectTransform, 196, "累计坏账(银行)",
                LabMoney(r.outcomeA.totalLoanLoss), LabMoney(r.outcomeB.totalLoanLoss),
                LabDelta(r.outcomeB.totalLoanLoss, r.outcomeA.totalLoanLoss));
            LabRow(card.rectTransform, 222, "房价指数(基期100)",
                r.outcomeA.priceIndex.ToString("0.0"), r.outcomeB.priceIndex.ToString("0.0"),
                LabDeltaF(r.outcomeB.priceIndex, r.outcomeA.priceIndex, "0.0"));
        }

        void BuildBubbleCard(RectTransform c, GameState s)
        {
            Image card = UiTheme.MakeImage(c, new Color32(0x2c, 0x25, 0x1e, 255), "labBubbleCard");
            PlaceTopLeft(card.rectTransform, 8f, 416f, 1056f, 212f);
            ShopText(card.rectTransform, "泡沫形成实验 · 钉住政策利率 1.00% 重放 26 期", 20, UiTheme.TextMain,
                14f, 8f, 620f, 26f, TextAnchor.MiddleLeft, FontStyle.Bold);
            ShopText(card.rectTransform,
                "低利率不退场+价格动能+租金滞后——泡沫压力三指标的确定性重放",
                15, UiTheme.TextSub, 14f, 36f, 940f, 22f, TextAnchor.MiddleLeft);

            Button run = UiTheme.MakeButton(card.transform, "运行 26 期", () =>
            {
                housingBubbleResult = HousingLabService.RunBubbleExperiment(s, 1f, 26);
                if (!string.IsNullOrEmpty(housingBubbleResult.refusal)) ShowToast(housingBubbleResult.refusal);
                CloseTop();
                ShowCurves(6);
            }, 18);
            PlaceTopLeft(run.rectTransform(), 14f, 64f, 180f, 32f);

            BubbleExperimentResult r = housingBubbleResult;
            if (r == null || r.track.Count == 0)
            {
                ShopText(card.rectTransform, "尚未运行——点击「运行 26 期」重放", 16, UiTheme.TextSub,
                    210f, 70f, 700f, 24f, TextAnchor.MiddleLeft);
                return;
            }
            int last = r.track.Count - 1;
            ShopText(card.rectTransform, "泡沫压力 " + r.track[0].bubblePressure.ToString("0.0") + " → " +
                r.track[last].bubblePressure.ToString("0.0") + "(阈值 60 进 Inflating;期末相位 " +
                LeveragePhaseName(r.end.leveragePhase) + ")", 16, UiTheme.TextMain,
                210f, 100f, 830f, 22f, TextAnchor.MiddleLeft);
            ShopText(card.rectTransform, "房价指数 " + r.track[0].priceIndex.ToString("0.0") + " → " +
                r.track[last].priceIndex.ToString("0.0") + " · 房价/年租比 " +
                r.track[0].priceRentRatio.ToString("0.0") + " → " + r.track[last].priceRentRatio.ToString("0.0"),
                15, UiTheme.TextSub, 210f, 126f, 830f, 22f, TextAnchor.MiddleLeft);

            System.Text.StringBuilder sb = new System.Text.StringBuilder("bp 轨迹");
            int step = Mathf.Max(1, r.track.Count / 4);
            for (int i = 0; i < r.track.Count; i += step)
                sb.Append("  第").Append(r.track[i].period + 1).Append("期 ")
                    .Append(r.track[i].bubblePressure.ToString("0.0"));
            ShopText(card.rectTransform, sb.ToString(), 15, UiTheme.Accent,
                210f, 152f, 830f, 22f, TextAnchor.MiddleLeft);
            if (!r.run.completed)
                ShopText(card.rectTransform, "未跑满:" + r.run.blockedReason, 14,
                    new Color32(0xd9, 0x6a, 0x4a, 255), 210f, 178f, 500f, 20f, TextAnchor.MiddleLeft);
        }

        void LabRow(RectTransform card, float y, string label, string va, string vb, string delta)
        {
            ShopText(card, label, 15, UiTheme.TextMain, 22f, y, 200f, 24f, TextAnchor.MiddleLeft);
            ShopText(card, va, 15, UiTheme.TextMain, 230f, y, 200f, 24f, TextAnchor.MiddleLeft);
            ShopText(card, vb, 15, UiTheme.TextMain, 450f, y, 200f, 24f, TextAnchor.MiddleLeft);
            ShopText(card, delta, 15, UiTheme.Accent, 670f, y, 240f, 24f, TextAnchor.MiddleLeft);
        }

        static string LabMoney(float v) { return "¥" + Mathf.RoundToInt(v).ToString("N0"); }

        static string LabDelta(float b, float a)
        {
            float d = b - a;
            return (d >= 0f ? "+" : "") + Mathf.RoundToInt(d).ToString("N0");
        }

        static string LabDeltaF(float b, float a, string format)
        {
            float d = b - a;
            return (d >= 0f ? "+" : "") + d.ToString(format);
        }

        static void Normalize(List<Vector2> pts, Vector2 min, Vector2 max, List<Vector2> output)
        {
            output.Clear();
            Vector2 span = new Vector2(Mathf.Max(1e-4f, max.x - min.x), Mathf.Max(1e-4f, max.y - min.y));
            for (int i = 0; i < pts.Count; i++)
                output.Add(new Vector2((pts[i].x - min.x) / span.x, (pts[i].y - min.y) / span.y));
        }

        void BuildDemandGraph(PolylineGraph graph, Text legend, List<DayRecord> data)
        {
            GameState state = GameState.I;
            List<Vector2> pts = new List<Vector2>();
            for (int i = 0; i < data.Count; i++)
            {
                float price = data[i].consumerPrice > 0f ? data[i].consumerPrice : data[i].price;
                int demand = data[i].desiredDemand > 0 ? data[i].desiredDemand : data[i].demandTotal;
                pts.Add(new Vector2(demand, price));
            }
            pts.Sort((a, b) => a.x.CompareTo(b.x));

            // 随机事件与竞争对手会移动需求曲线，拟合时优先使用 V0.1 的平常日，避免把曲线移动误当成价格弹性。
            List<DayRecord> fitData = data.FindAll(x => !x.competitionActive && string.IsNullOrEmpty(x.eventName));

            // 幂函数拟合 Q = a·P^-b(对数-对数最小二乘)
            List<Vector2> fit = new List<Vector2>();
            string fitText;
            bool hasPlayerFit = false;
            if (fitData.Count >= 2)
            {
                float distinct = 0f; var prices = new HashSet<float>();
                for (int i = 0; i < fitData.Count; i++)
                    prices.Add(fitData[i].consumerPrice > 0f ? fitData[i].consumerPrice : fitData[i].price);
                distinct = prices.Count;
                if (distinct >= 2)
                {
                    float sx = 0, sy = 0, sxx = 0, sxy = 0; int n = fitData.Count;
                    for (int i = 0; i < n; i++)
                    {
                        float observedPrice = fitData[i].consumerPrice > 0f ? fitData[i].consumerPrice : fitData[i].price;
                        int observedDemand = fitData[i].desiredDemand > 0 ? fitData[i].desiredDemand : fitData[i].demandTotal;
                        float lx = Mathf.Log(Mathf.Max(1f, observedPrice));
                        float ly = Mathf.Log(Mathf.Max(1f, observedDemand));
                        sx += lx; sy += ly; sxx += lx * lx; sxy += lx * ly;
                    }
                    float denom = n * sxx - sx * sx;
                    float b = Mathf.Abs(denom) < 1e-5f ? 1f : (n * sxy - sx * sy) / denom;   // 斜率(负)→弹性
                    // lnQ = lnA + b·lnP(Q = A·P^b,b 为负),截距 lnA = (sy - b·sx)/n
                    float a = Mathf.Exp((sy - b * sx) / n);
                    for (float p = 5f; p <= 30f; p += 0.5f)
                    {
                        float q = a * Mathf.Pow(p, b);
                        fit.Add(new Vector2(q, p));
                    }
                    hasPlayerFit = true;
                    float ed = -b;
                    fitText = "拟合需求曲线 Q = " + a.ToString("0") + " × P^-" + ed.ToString("0.00") +
                        "\n需求价格弹性 |Ed| ≈ " + ed.ToString("0.00") + (ed >= 1.5f ? "(富有弹性:涨价会明显赶走顾客)" : ed >= 0.8f ? "(接近单位弹性)" : "(缺乏弹性:顾客对价格不敏感)") +
                        "\n彩色圆点 = 每日真实数据；拟合只使用无事件、无竞争的平常日";
                }
                else fitText = "";
            }
            else fitText = "";

            if (!hasPlayerFit)
            {
                float anchorPrice = data.Count > 0
                    ? (data[data.Count - 1].consumerPrice > 0f ? data[data.Count - 1].consumerPrice : data[data.Count - 1].price)
                    : state.price;
                int anchorDemand = data.Count > 0
                    ? (data[data.Count - 1].desiredDemand > 0 ? data[data.Count - 1].desiredDemand : data[data.Count - 1].demandTotal)
                    : Mathf.RoundToInt(UnifiedDemandAtPrice(state, anchorPrice));
                float anchorModel = UnifiedDemandAtPrice(state, anchorPrice);
                float calibration = anchorModel > 0.001f ? anchorDemand / anchorModel : 1f;
                for (float p = 5f; p <= 30f; p += 0.5f)
                    fit.Add(new Vector2(UnifiedDemandAtPrice(state, p) * calibration, p));
                fitText = data.Count == 0 ? "还没有营业数据；蓝线先显示统一消费者模型的参考需求曲线。" :
                    "目前只有一个有效价格样本；蓝线先显示按当天数据校准的参考需求曲线。" +
                    "\n再用不同售价经营一天后，这里会替换成你的真实数据拟合曲线。";
            }

            float xMax = 220f;
            // 自动缩放：纳入当前数据窗口（含 cutoff 内散点）的极值——漏掉散点会让事件日
            // 高需求点超出刻度、被画到绘图区右缘之外。固定刻度：扫全 history 保持跨日可比。
            if (ChartAutoScale)
                for (int i = 0; i < data.Count; i++)
                    xMax = Mathf.Max(xMax, data[i].desiredDemand > 0 ? data[i].desiredDemand : data[i].demandTotal);
            else
                for (int i = 0; i < state.history.Count; i++)
                    xMax = Mathf.Max(xMax, state.history[i].desiredDemand > 0 ? state.history[i].desiredDemand : state.history[i].demandTotal);
            for (int i = 0; i < fit.Count; i++) xMax = Mathf.Max(xMax, fit[i].x);
            xMax = NiceCeil(xMax, 50f);
            List<Vector2> ptsN = new List<Vector2>(), fitN = new List<Vector2>();
            Vector2 min = new Vector2(0f, 4f), max = new Vector2(xMax, 32f);
            Normalize(pts, min, max, ptsN);
            Normalize(fit, min, max, fitN);
            graph.SetPrimaryConnections(false);
            graph.SetData(ptsN, fitN, null);
            graph.ConfigureAxes(min.x, max.x, min.y, max.y, "需求量 Q", "顾客实付价 P", " 杯", " 元");
            if (data.Count > 0)
            {
                DayRecord current = data[data.Count - 1];
                float currentPrice = current.consumerPrice > 0f ? current.consumerPrice : current.price;
                int currentDemand = current.desiredDemand > 0 ? current.desiredDemand : current.demandTotal;
                graph.SetCurrentPoint(NormalizePoint(new Vector2(currentDemand, currentPrice), min, max));
            }

            legend.text = "● 每日数据  ·  橙色大点 = 当前日  ·  蓝线 = 需求曲线  ·  移动或点击数据点查看数值\n" + fitText;
        }

        static float UnifiedDemandAtPrice(GameState state, float price)
        {
            bool competitorOpen = state.CompetitionActive;
            float rivalPrice = competitorOpen ? state.competitor.price : Eco.BasePrice;
            ConsumerChoiceResult student = ConsumerDemandService.Evaluate(state, ConsumerType.Student,
                price, rivalPrice, competitorOpen, 1f, 1f, 1f, false);
            ConsumerChoiceResult white = ConsumerDemandService.Evaluate(state, ConsumerType.WhiteCollar,
                price, rivalPrice, competitorOpen, 1f, 1f, 1f, false);
            ConsumerChoiceResult tourist = ConsumerDemandService.Evaluate(state, ConsumerType.Tourist,
                price, rivalPrice, competitorOpen, 1f, 1f, 1f, false);
            return student.player + white.player + tourist.player;
        }

        void BuildCostGraph(PolylineGraph graph, Text legend, List<DayRecord> data)
        {
            List<Vector2> atc = new List<Vector2>(), avc = new List<Vector2>(), mc = new List<Vector2>();
            List<DayRecord> observations = data.FindAll(x => x.produced > 0);
            DayRecord current = observations.Count > 0 ? observations[observations.Count - 1] : null;
            observations.Sort((a, b) => a.produced.CompareTo(b.produced));
            float maxOutput = Mathf.Max(50f, Eco.EquipmentCap), yMax = 30f;
            for (int i = 0; i < observations.Count; i++)
            {
                DayRecord r = observations[i];
                float averageTotal = r.totalCost / r.produced;
                float averageVariable = (r.materialCost + r.wageCost + r.playerMarketing + r.strategicSpend) / r.produced;
                atc.Add(new Vector2(r.produced, averageTotal));
                avc.Add(new Vector2(r.produced, averageVariable));
                mc.Add(new Vector2(r.produced, r.marginalCost));
                yMax = Mathf.Max(yMax, averageTotal, averageVariable, r.marginalCost);
            }
            if (!ChartAutoScale)
                for (int i = 0; i < GameState.I.history.Count; i++)
                {
                    DayRecord r = GameState.I.history[i];
                    if (r.produced > 0) yMax = Mathf.Max(yMax, r.totalCost / r.produced, r.marginalCost);
                }
            yMax = NiceCeil(yMax, 10f);

            List<Vector2> atcN = new List<Vector2>(), avcN = new List<Vector2>(), mcN = new List<Vector2>();
            Vector2 min = new Vector2(0f, 0f), max = new Vector2(maxOutput, yMax);
            Normalize(atc, min, max, atcN);
            Normalize(avc, min, max, avcN);
            Normalize(mc, min, max, mcN);
            graph.SetData(atcN, avcN, mcN);
            graph.ConfigureAxes(min.x, max.x, min.y, max.y, "产量 Q", "单位成本", " 杯", " 元/杯");
            if (current != null)
                graph.SetCurrentPoint(NormalizePoint(new Vector2(current.produced, current.totalCost / current.produced), min, max));

            legend.text = atc.Count == 0 ? "暂无有效成本点：完成一次有生产的营业后显示。坐标尺度固定，不会随筛选跳动。" :
                "橙线 = 平均总成本 ATC  ·  蓝线 = 平均可变成本 AVC  ·  绿线 = 边际成本 MC  ·  橙色大点 = 当前日\n产量增加会摊薄固定成本；品质、工资和原料价格会移动成本曲线。";
        }

        void BuildRevenueGraph(PolylineGraph graph, Text legend, List<DayRecord> data)
        {
            List<Vector2> tr = new List<Vector2>(), profit = new List<Vector2>();
            int latestDay = data.Count > 0 ? data[data.Count - 1].day : Mathf.Max(1, GameState.I.day);
            float maxDay = ChartAutoScale
                ? Mathf.Max(2f, latestDay)
                : Mathf.Max(2f, GameState.I.totalDays, GameState.I.day);
            float vMin = 0f, vMax = 500f;
            for (int i = 0; i < data.Count; i++)
            {
                DayRecord r = data[i];
                tr.Add(new Vector2(r.day, r.revenue));
                profit.Add(new Vector2(r.day, r.profit));
                vMax = Mathf.Max(vMax, r.revenue, r.profit);
                vMin = Mathf.Min(vMin, r.profit);
            }
            if (!ChartAutoScale)
                for (int i = 0; i < GameState.I.history.Count; i++)
                {
                    DayRecord r = GameState.I.history[i];
                    vMax = Mathf.Max(vMax, r.revenue, r.profit);
                    vMin = Mathf.Min(vMin, r.profit);
                }
            vMax = NiceCeil(vMax, 500f);
            vMin = vMin < 0f ? -NiceCeil(Mathf.Abs(vMin), 500f) : 0f;

            List<Vector2> trN = new List<Vector2>(), prN = new List<Vector2>();
            Vector2 min = new Vector2(1f, vMin), max = new Vector2(maxDay, vMax);
            Normalize(tr, min, max, trN);
            Normalize(profit, min, max, prN);
            graph.SetData(prN, null, trN);
            graph.ConfigureAxes(min.x, max.x, min.y, max.y, "世界日", "金额", " 日", " 元");
            if (profit.Count > 0) graph.SetCurrentPoint(NormalizePoint(profit[profit.Count - 1], min, max));

            legend.text = profit.Count == 0 ? "暂无收入利润数据：营业或歇业结算后显示。" :
                "橙线 = 每日利润  ·  绿线 = 每日总收入  ·  橙色大点 = 当前日\n对照竞争者实际开业前后，观察市场进入和价格战如何改变收入与利润。";
        }

        void BuildCompetitionGraph(PolylineGraph graph, Text legend, List<DayRecord> data, int mode)
        {
            List<DayRecord> competition = data.FindAll(x => x.competitionActive);
            float minX = competition.Count > 0 ? competition[0].day : 1f;
            float observedLastDay = competition.Count > 0 ? competition[competition.Count - 1].day : Mathf.Max(1, GameState.I.day);
            float maxX = ChartAutoScale
                ? Mathf.Max(minX + 1f, observedLastDay)
                : Mathf.Max(minX + 1f, GameState.I.totalDays, GameState.I.day);
            if (competition.Count == 0)
            {
                graph.SetData(null, null, null);
                if (mode == 3) graph.ConfigureAxes(minX, maxX, 0f, 100f, "世界日", "市场份额", " 日", "%");
                else if (mode == 4) graph.ConfigureAxes(minX, maxX, 4f, 32f, "世界日", "价格", " 日", " 元");
                else graph.ConfigureAxes(minX, maxX, -2500f, 3000f, "世界日", "估算利润", " 日", " 元");
                legend.text = "完成企业篇、Sunny Tea 开业后，这里会出现竞争曲线。";
                return;
            }

            List<Vector2> player = new List<Vector2>(), rival = new List<Vector2>(), average = new List<Vector2>();
            float minY = mode == 4 ? 4f : 0f, maxY = mode == 3 ? 100f : mode == 4 ? 32f : 500f;
            for (int i = 0; i < competition.Count; i++)
            {
                DayRecord r = competition[i];
                float py, ry;
                if (mode == 3) { py = r.playerMarketShare * 100f; ry = r.competitorMarketShare * 100f; }
                else if (mode == 4)
                {
                    py = r.price; ry = r.competitorPrice;
                    float marketAverage = (r.price * r.sold + r.competitorPrice * r.competitorSold) / Mathf.Max(1, r.sold + r.competitorSold);
                    average.Add(new Vector2(r.day, marketAverage));
                    minY = Mathf.Min(minY, marketAverage); maxY = Mathf.Max(maxY, marketAverage);
                }
                else { py = r.profit; ry = r.competitorProfit; }
                player.Add(new Vector2(r.day, py)); rival.Add(new Vector2(r.day, ry));
                minY = Mathf.Min(minY, py, ry); maxY = Mathf.Max(maxY, py, ry);
            }
            if (mode == 5)
            {
                if (!ChartAutoScale)
                    for (int i = 0; i < GameState.I.history.Count; i++)
                    {
                        DayRecord r = GameState.I.history[i];
                        if (!r.competitionActive) continue;
                        minY = Mathf.Min(minY, r.profit, r.competitorProfit);
                        maxY = Mathf.Max(maxY, r.profit, r.competitorProfit);
                    }
                minY = minY < 0f ? -NiceCeil(Mathf.Abs(minY), 500f) : 0f;
                maxY = NiceCeil(maxY, 500f);
            }
            if (Mathf.Approximately(minY, maxY)) maxY = minY + 1f;
            List<Vector2> pn = new List<Vector2>(), rn = new List<Vector2>(), an = new List<Vector2>();
            Vector2 min = new Vector2(minX, minY), max = new Vector2(maxX, maxY);
            Normalize(player, min, max, pn);
            Normalize(rival, min, max, rn);
            if (average.Count > 0) Normalize(average, min, max, an);
            graph.SetData(pn, an.Count > 0 ? an : null, rn);
            string yTitle = mode == 3 ? "市场份额" : mode == 4 ? "价格" : "估算利润";
            string ySuffix = mode == 3 ? "%" : " 元";
            graph.ConfigureAxes(min.x, max.x, min.y, max.y, "世界日", yTitle, " 日", ySuffix);
            graph.SetCurrentPoint(NormalizePoint(player[player.Count - 1], min, max));

            if (mode == 3)
                legend.text = "橙线 = 你  ·  绿线 = Sunny Tea  ·  橙色大点 = 当前日\n份额上涨说明抢到了顾客，但请和利润曲线一起看。";
            else if (mode == 4)
                legend.text = "橙线 = 你  ·  绿线 = Sunny Tea  ·  蓝线 = 市场平均价  ·  橙色大点 = 当前日\n价格线连续向下，通常意味着价格战正在形成。";
            else
                legend.text = "橙线 = 你的真实利润  ·  绿线 = 对手估算趋势  ·  橙色大点 = 当前日\n对手的精确成本与利润仍然不可见，只展示根据公开行为估算的趋势。";
        }

        static Vector2 NormalizePoint(Vector2 point, Vector2 min, Vector2 max)
        {
            Vector2 span = new Vector2(Mathf.Max(.0001f, max.x - min.x), Mathf.Max(.0001f, max.y - min.y));
            return new Vector2((point.x - min.x) / span.x, (point.y - min.y) / span.y);
        }

        static float NiceCeil(float value, float step)
        {
            return Mathf.Max(step, Mathf.Ceil(Mathf.Max(0f, value) / step) * step);
        }

        // ————— 手册 —————

        int handbookPage;
        string handbookQuery = "";
        string handbookCategory = "全部";

        public void ShowHandbook()
        {
            GameState s = GameState.I;
            if (s == null) return;
            KnowledgeSystem.Ensure(s);
            KnowledgeSystem.SyncPlayerCases(s);
            GameObject root = BeginPanel("经济学手册 · 知识地图", 1120, 780);
            RectTransform c = ContentOf(root);

            int understood = 0, experimented = 0;
            for (int i = 0; i < CardLibrary.All.Count; i++)
            {
                KnowledgeStatus status = KnowledgeSystem.Status(s, CardLibrary.All[i].id);
                if (status >= KnowledgeStatus.Understood) understood++;
                if (status >= KnowledgeStatus.Experimented) experimented++;
            }
            ShopText(c, "掌握进度　已发现 " + s.unlockedCards.Count + " / " + CardLibrary.All.Count +
                "　已理解 " + understood + "　已实验 " + experimented, 18, UiTheme.TextSub,
                8f, 0f, 700f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);

            InputField search = MakeKnowledgeSearch(c, handbookQuery, 708f, 0f, 250f, 38f);
            Button searchButton = UiTheme.MakeButton(c, "搜索", () =>
            {
                handbookQuery = search.text;
                handbookPage = 0;
                ClosePanel(root);
                ShowHandbook();
            }, 17);
            PlaceTopLeft(searchButton.rectTransform(), 966f, 0f, 100f, 38f);

            // V0.7 新增"国际"分类:9 个按钮把 1056px 内容宽占满,间距/宽度从 132/122 压到 116/110。
            // V0.9.5 新增"资产"分类:10 个按钮再压到 104/98(8+9×104+98=1042≤1056)。
            string[] categories = { "全部", "企业", "消费者", "市场", "政府", "宏观", "金融", "国际", "资产", "实验" };
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                Button categoryButton = UiTheme.MakeButton(c, category, () =>
                {
                    handbookCategory = category;
                    handbookPage = 0;
                    ClosePanel(root);
                    ShowHandbook();
                }, 17);
                PlaceTopLeft(categoryButton.rectTransform(), 8f + i * 104f, 48f, 98f, 40f);
                UiTheme.SetButtonStyle(categoryButton,
                    handbookCategory == category ? UiTheme.Accent : new Color32(0xe6, 0xcf, 0xa6, 255), UiTheme.Ink);
            }

            List<CardDef> matches = KnowledgeSystem.Search(s, handbookQuery, handbookCategory);
            int perPage = 8;
            int pages = Mathf.Max(1, Mathf.CeilToInt(matches.Count / (float)perPage));
            if (handbookPage >= pages) handbookPage = pages - 1;
            if (handbookPage < 0) handbookPage = 0;

            for (int i = 0; i < perPage; i++)
            {
                int idx = handbookPage * perPage + i;
                if (idx >= matches.Count) break;
                CardDef def = matches[idx];
                KnowledgeMetadata meta = KnowledgeSystem.GetMetadata(def.id);
                KnowledgeStatus status = KnowledgeSystem.Status(s, def.id);
                string label = "<b>" + def.title + "</b>\n<size=14>" + KnowledgeSystem.CategoryName(meta.category) +
                    " · " + KnowledgeSystem.LevelName(meta.level) + " · " + KnowledgeSystem.StatusName(status) + "</size>";

                Button b = UiTheme.MakeButton(c, label, () =>
                {
                    ShowCardDetail(def.id);
                }, 19);
                float x = i % 2 == 0 ? 8f : 543f;
                float y = 104f + (i / 2) * 102f;
                PlaceTopLeft(b.rectTransform(), x, y, 523f, 88f);
                UiTheme.SetButtonStyle(b,
                    status == KnowledgeStatus.Experimented ? new Color32(0x74, 0xaa, 0x91, 255) :
                    status == KnowledgeStatus.Understood ? new Color32(0xc8, 0xaa, 0x72, 255) :
                    status == KnowledgeStatus.Discovered ? UiTheme.PanelLight : new Color32(0x3a, 0x32, 0x2b, 255),
                    status == KnowledgeStatus.Undiscovered ? UiTheme.TextSub : UiTheme.Ink);
            }

            if (matches.Count == 0)
                ShopText(c, "没有找到“" + handbookQuery + "”相关知识。可以换一个词，或选择“全部”。",
                    20, UiTheme.TextSub, 210f, 280f, 650f, 80f, TextAnchor.MiddleCenter);

            Button prev = UiTheme.MakeButton(c, "← 上一页", () => { handbookPage = Mathf.Max(0, handbookPage - 1); ClosePanel(root); ShowHandbook(); }, 18);
            PlaceTopLeft(prev.rectTransform(), 260f, 530f, 170f, 46f);
            prev.interactable = handbookPage > 0;

            Button next = UiTheme.MakeButton(c, "下一页 →", () => { handbookPage = Mathf.Min(pages - 1, handbookPage + 1); ClosePanel(root); ShowHandbook(); }, 18);
            PlaceTopLeft(next.rectTransform(), 636f, 530f, 170f, 46f);
            next.interactable = handbookPage < pages - 1;

            ShopText(c, (handbookPage + 1) + " / " + pages + "　共 " + matches.Count + " 项", 18,
                UiTheme.TextSub, 443f, 530f, 180f, 46f, TextAnchor.MiddleCenter);

            Button route = UiTheme.MakeButton(c, "知识路线", () => ShowKnowledgeRoute(), 18);
            PlaceTopLeft(route.rectTransform(), 8f, 530f, 170f, 46f);
            Button logs = UiTheme.MakeButton(c, "知识日志", () => ShowKnowledgeLog(), 18);
            PlaceTopLeft(logs.rectTransform(), 896f, 530f, 170f, 46f);
        }

        public void ShowCardDetail(string id)
        {
            CardDef def = CardLibrary.Get(id);
            GameState s = GameState.I;
            if (def == null || s == null) return;
            KnowledgeSystem.Ensure(s);
            KnowledgeSystem.SyncPlayerCases(s);
            KnowledgeMetadata meta = KnowledgeSystem.GetMetadata(id);
            KnowledgeStatus status = KnowledgeSystem.Status(s, id);
            bool discovered = status != KnowledgeStatus.Undiscovered;
            GameObject root = BeginPanel("知识卡 · " + def.title, 820, 620);
            RectTransform c = ContentOf(root);
            KnowledgeCaseRecord ownCase = KnowledgeSystem.GetCase(s, id);
            string body = "<color=#e8a84c>" + KnowledgeSystem.CategoryName(meta.category) + " · " +
                KnowledgeSystem.LevelName(meta.level) + " · " + KnowledgeSystem.StatusName(status) + "</color>\n\n" +
                (discovered ? def.body + (def.formula != null ? "\n\n<b>公式</b>　" + def.formula : "") :
                    "<b>你为什么还没解锁</b>\n" + KnowledgeSystem.UnlockHint(s, id)) +
                (ownCase != null ? "\n\n<b>我的案例 · Day " + ownCase.worldDay + "</b>\n" + ownCase.summary :
                    discovered ? "\n\n<b>我的案例</b>\n继续做一次相关决策后，这里会显示你自己的数据。" : "");
            Text t = UiTheme.MakeText(c, body, 20, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(8f, 88f);
            t.rectTransform.offsetMax = new Vector2(-8f, -8f);
            FitTextAboveFooter(t);

            Button understand = UiTheme.MakeButton(c,
                status >= KnowledgeStatus.Understood ? "已理解" : "我理解了", () =>
                {
                    KnowledgeSystem.MarkUnderstood(s, id);
                    ClosePanel(root);
                    ShowCardDetail(id);
                }, 18);
            understand.rectTransform().anchorMin = understand.rectTransform().anchorMax = new Vector2(.5f, 0f);
            understand.rectTransform().pivot = new Vector2(.5f, 0f);
            understand.rectTransform().anchoredPosition = new Vector2(-230f, 8f);
            understand.rectTransform().sizeDelta = new Vector2(180f, 50f);
            understand.interactable = discovered && status < KnowledgeStatus.Understood;

            Button experiment = UiTheme.MakeButton(c, "再试一次", () => RunKnowledgeExperiment(meta), 18);
            experiment.rectTransform().anchorMin = experiment.rectTransform().anchorMax = new Vector2(.5f, 0f);
            experiment.rectTransform().pivot = new Vector2(.5f, 0f);
            experiment.rectTransform().anchoredPosition = new Vector2(0f, 8f);
            experiment.rectTransform().sizeDelta = new Vector2(180f, 50f);
            experiment.interactable = discovered && !string.IsNullOrEmpty(meta.experimentKey);

            Button ok = UiTheme.MakeButton(c, "关闭", () => ClosePanel(root), 18);
            ok.rectTransform().anchorMin = ok.rectTransform().anchorMax = new Vector2(.5f, 0f);
            ok.rectTransform().pivot = new Vector2(.5f, 0f);
            ok.rectTransform().anchoredPosition = new Vector2(230f, 8f);
            ok.rectTransform().sizeDelta = new Vector2(180f, 50f);
        }

        InputField MakeKnowledgeSearch(RectTransform parent, string value, float x, float y, float w, float h)
        {
            Image bg = UiTheme.MakeImage(parent, new Color32(0xee, 0xdd, 0xb9, 255), "handbookSearch");
            PlaceTopLeft(bg.rectTransform, x, y, w, h);
            InputField input = bg.gameObject.AddComponent<InputField>();
            Text text = UiTheme.MakeText(bg.transform, value ?? "", 17, UiTheme.Ink, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch(); text.rectTransform.offsetMin = new Vector2(12f, 2f); text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            Text hint = UiTheme.MakeText(bg.transform, "搜索：弹性、通胀……", 16, new Color32(0x78, 0x66, 0x55, 255), TextAnchor.MiddleLeft);
            hint.rectTransform.Stretch(); hint.rectTransform.offsetMin = new Vector2(12f, 2f); hint.rectTransform.offsetMax = new Vector2(-8f, -2f);
            input.textComponent = text;
            input.placeholder = hint;
            input.targetGraphic = bg;
            input.text = value ?? "";
            input.characterLimit = 24;
            return input;
        }

        void RunKnowledgeExperiment(KnowledgeMetadata meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.experimentKey)) return;
            CloseAll();
            switch (meta.experimentKey)
            {
                case "demand": ShowCurves(0); break;
                case "cost": ShowCurves(1); break;
                case "resident":
                    if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Resident))
                        ShowToast(MainlineGuide.LockReason(PlayerViewMode.Resident));
                    else if (GameState.I.resident.profileCreated) ShowResidentExperiments();
                    else ShowResidentHome();
                    break;
                case "market":
                    if (GameState.I.CompetitionActive) ShowMarketExperiments();
                    else ShowToast("完成企业篇、进入市场竞争后开放这个实验");
                    break;
                case "publicgood":
                    if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Government))
                        ShowToast(MainlineGuide.LockReason(PlayerViewMode.Government));
                    else ShowGovernmentDashboard(5);
                    break;
                case "government":
                    if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Government))
                        ShowToast(MainlineGuide.LockReason(PlayerViewMode.Government));
                    else ShowGovernmentDashboard(1);
                    break;
                case "macro":
                    if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Macro))
                        ShowToast(MainlineGuide.LockReason(PlayerViewMode.Macro));
                    else ShowMacroDashboard(3);
                    break;
                case "finance":
                    if (!MainlineGuide.CanEnter(GameState.I, PlayerViewMode.Bank))
                        ShowToast(MainlineGuide.LockReason(PlayerViewMode.Bank));
                    else ShowFinanceCenter(0);
                    break;
                case "world":
                    if (!MainlineGuide.CanEnterTrade(GameState.I))
                        ShowToast(MainlineGuide.TradeLockReason());
                    else ShowWorldEconomy(0);
                    break;
            }
        }

        void ShowKnowledgeRoute()
        {
            GameObject root = BeginPanel("知识路线 · 概念怎样连起来", 760, 560);
            RectTransform c = ContentOf(root);
            Text text = UiTheme.MakeText(c,
                "<b>供给与需求</b>\n　↓\n<b>弹性</b>\n　↓\n<b>企业定价与成本</b>\n　↓\n<b>市场竞争与博弈</b>\n　↓\n<b>税收、外部性与财政</b>\n　↓\n<b>总需求、总供给与长期增长</b>\n\n每一步都可以从卡片的“再试一次”回到相关实验。",
                22, UiTheme.TextMain, TextAnchor.MiddleCenter);
            text.rectTransform.Stretch(); text.rectTransform.offsetMin = new Vector2(20f, 76f); text.rectTransform.offsetMax = new Vector2(-20f, -10f);
            FitTextAboveFooter(text);
            Button close = UiTheme.MakeButton(c, "关闭", () => ClosePanel(root), 19);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(.5f, 0f);
            close.rectTransform().pivot = new Vector2(.5f, 0f); close.rectTransform().anchoredPosition = new Vector2(0f, 8f); close.rectTransform().sizeDelta = new Vector2(200f, 48f);
        }

        void ShowKnowledgeLog()
        {
            GameState s = GameState.I;
            KnowledgeSystem.Ensure(s);
            GameObject root = BeginPanel("知识日志 · 已出现的解释", 820, 620);
            RectTransform c = ContentOf(root);
            System.Text.StringBuilder body = new System.Text.StringBuilder();
            for (int i = 0; i < s.knowledge.explanationLogIds.Count; i++)
            {
                CardDef card = CardLibrary.Get(s.knowledge.explanationLogIds[i]);
                if (card != null) body.Append("<b>").Append(card.title).Append("</b>　")
                    .Append(KnowledgeSystem.StatusName(KnowledgeSystem.Status(s, card.id))).Append('\n')
                    .Append(card.body).Append("\n\n");
            }
            if (body.Length == 0) body.Append("打开一张已发现卡片并标记“我理解了”后，解释会进入这里。教学提示关闭后也能随时回看。");
            Text text = UiTheme.MakeText(c, body.ToString(), 18, UiTheme.TextMain, TextAnchor.UpperLeft);
            text.rectTransform.Stretch(); text.rectTransform.offsetMin = new Vector2(12f, 76f); text.rectTransform.offsetMax = new Vector2(-12f, -8f);
            FitTextAboveFooter(text);
            Button close = UiTheme.MakeButton(c, "关闭", () => ClosePanel(root), 18);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(.5f, 0f);
            close.rectTransform().pivot = new Vector2(.5f, 0f); close.rectTransform().anchoredPosition = new Vector2(0f, 8f); close.rectTransform().sizeDelta = new Vector2(200f, 48f);
        }

        // ————— 设置 —————

        public void ShowSettings()
        {
            GameState s = GameState.I;
            LongTermExperienceService.Ensure(s);
            bool chapterPlay = ChapterPlayService.IsChapterPlay;
            WorldExperienceSettings settings = s.experience.settings;
            GameObject root = BeginPanel("设置 · " + GameState.Version, 920, 760);
            RectTransform c = ContentOf(root);

            RectTransform learning = ExperienceCard(c, "游玩与教学", 8f, 0f, 420f, 280f, UiTheme.Accent);
            SettingRow(learning, "难度模式", LongTermExperienceService.DifficultyName(settings.difficulty), 42f,
                "教学少噪声，模拟冲击更勤，规律不变", () =>
            {
                settings.difficulty = (DifficultyMode)(((int)settings.difficulty + 1) % 3);
                ReloadSettings(root);
            });
            SettingRow(learning, "解释详细度", LongTermExperienceService.GuidanceName(settings.guidance), 102f,
                "报告和提示写到这一层", () =>
            {
                settings.guidance = (GuidanceLevel)(((int)settings.guidance + 1) % 3);
                ReloadSettings(root);
            });
            SettingRow(learning, "事件频率", settings.eventFrequency.ToString("0.00") + "×", 162f,
                "镇上随机事件的疏密", () =>
            {
                settings.eventFrequency = settings.eventFrequency >= 1.5f ? .5f : settings.eventFrequency + .5f;
                ReloadSettings(root);
            });
            SettingRow(learning, "竞争强度", settings.competitionIntensity.ToString("0.0") + "×", 222f,
                "对手压价、抢客的力度", () =>
            {
                settings.competitionIntensity = settings.competitionIntensity >= 1.5f ? .5f : settings.competitionIntensity + .5f;
                ReloadSettings(root);
            });

            RectTransform display = ExperienceCard(c, "显示、声音与存档", 448f, 0f, 420f, 450f, UiTheme.Market);
            SettingRow(display, "图表自动缩放", settings.chartAutoScale ? "开启" : "关闭", 42f,
                "折线图纵轴跟着数据收放", () =>
            {
                settings.chartAutoScale = !settings.chartAutoScale;
                ReloadSettings(root);
            });
            // 围猎三#4/#20:该设置唯一消费点是折线图轴刻度(PolylineGraph.FormatAxis),
            // HUD 与各面板始终显示完整数字——文案按实际作用面改述,不扩大实现面。
            SettingRow(display, "图表数值单位", settings.numericUnit, 92f,
                "只缩折线图的轴，金额仍写全", () =>
            {
                settings.numericUnit = settings.numericUnit == "自动" ? "完整数值" : settings.numericUnit == "完整数值" ? "万/亿" : "自动";
                ReloadSettings(root);
            });
            int volume = Mathf.RoundToInt(AudioListener.volume * 100f);
            SettingRow(display, "音量", volume + "%", 142f, "整个游戏的音量", () =>
            {
                AudioListener.volume = AudioListener.volume >= .99f ? 0f : Mathf.Clamp01(AudioListener.volume + .25f);
                PlayerPrefs.SetFloat("EconTown.Volume", AudioListener.volume);
                ReloadSettings(root);
            });
            SettingRow(display, "自动存档", settings.autoSave ? "开启" : "关闭", 192f,
                "清晨自动记下进度", () =>
            {
                settings.autoSave = !settings.autoSave;
                ReloadSettings(root);
            });
            SettingRow(display, "氛围效果", settings.atmosphereEffects ? "开启" : "关闭", 242f,
                "时段、季节和过场；关闭则跳过过场", () =>
            {
                settings.atmosphereEffects = !settings.atmosphereEffects;
                ReloadSettings(root);
            });
            SettingRow(display, "经济状态可视化", settings.ecoVisibility ? "开启" : "关闭", 292f,
                "街上的人、橱窗和价目牌跟着经营变", () =>
            {
                settings.ecoVisibility = !settings.ecoVisibility;
                ReloadSettings(root);
            });
            SettingRow(display, "港口演出", settings.fullPortAnimations ? "完整" : "精简", 342f,
                "精简时码头直接停在靠泊", () =>
            {
                settings.fullPortAnimations = !settings.fullPortAnimations;
                ReloadSettings(root);
            });
            SettingRow(display, "显示缩放", "随窗口自适应", 392f,
                "按 1280×720 适配窗口", () => { });
            settingsHintLine = ExperienceText(c, settingsHint, 16, 8f, 462f, 860f, 32f, UiTheme.TextSub, TextAnchor.MiddleLeft);

            Button save = UiTheme.MakeButton(c, "手动保存", () =>
            {
                if (s.phase != GamePhase.Morning) { ShowToast("只有清晨阶段可以手动存档"); return; }
                SaveSystem.Save(s);
                ShowToast("进度已保存");
            }, 19);
            PlaceTopLeft(save.rectTransform(), 8f, 502f, 200f, 52f);
            Button load = UiTheme.MakeButton(c, "读取存档", () =>
            {
                if (s.phase != GamePhase.Morning) { ShowToast("只有清晨阶段可以读档"); return; }
                if (!SaveSystem.HasSave()) { ShowToast("没有找到存档"); return; }
                CloseAll();
                if (SaveSystem.Load(s))
                {
                    ChapterPlayService.ApplySharedSettings(s);
                    // 与启动 Continue 路径对齐(P1-4)：读入的档可能比当前地图状态旧，
                    // 不刷新会把已拆/未建的公共设施留在场上，直到场景重载。
                    // 围猎三(#14/29):季节/氛围/新区/pending 报告一并收尾(PostLoadRefresh 三路径共用)。
                    PostLoadRefresh(s);
                    RefreshHud();
                    ShowToast("读档成功：Day " + s.day);
                }
                else ShowToast("存档损坏，已保留当前状态");
            }, 19);
            PlaceTopLeft(load.rectTransform(), 228f, 502f, 200f, 52f);
            Button restart = UiTheme.MakeButton(c, chapterPlay ? "重玩本篇" : "重新开始", () =>
            {
                ChapterPlayPartDefinition currentPart = ChapterPlayService.CurrentPart(s);
                string warning = chapterPlay
                    ? "将用本篇标准开局替换当前篇章游玩档；正式剧情不受影响。"
                    : "将删除当前正式剧情进度并回到 Day 1；篇章游玩档不受影响。";
                ShowConfirmation(chapterPlay ? "确认重玩本篇" : "确认重新开始", warning, () =>
                {
                    if (chapterPlay && currentPart != null)
                        ChapterPlayService.RequestStartPart(currentPart.number);
                    else ChapterPlayService.RequestNewStory();
                    GameBootstrap.I.RestartGame();
                }, chapterPlay ? "重玩本篇" : "开始新剧情");
            }, 19);
            PlaceTopLeft(restart.rectTransform(), 448f, 502f, 200f, 52f);
            UiTheme.SetButtonStyle(restart, UiTheme.Bad, Color.white);
            Button close = UiTheme.MakeButton(c, "关闭", () =>
            {
                ChapterPlayService.SaveSharedSettings(s);
                if (s.phase == GamePhase.Morning) SaveSystem.SaveKeyMoment(s, "设置已更新");
                ClosePanel(root);
            }, 19);
            PlaceTopLeft(close.rectTransform(), 668f, 502f, 200f, 52f);

            // checkpoint 此前只写不读——关键节点备份没有任何游戏内恢复入口（V0.5.3 补齐）。
            Button restore = UiTheme.MakeButton(c, "从关键节点恢复", () =>
            {
                if (s.phase != GamePhase.Morning) { ShowToast("只有清晨阶段可以恢复"); return; }
                if (!SaveSystem.HasCheckpoint()) { ShowToast("还没有关键节点存档"); return; }
                ShowConfirmation("从关键节点恢复",
                    "将放弃当前进度，回到最近一次关键节点（" + s.experience.lastCheckpointReason + "）。",
                    () =>
                    {
                        CloseAll();
                        if (SaveSystem.LoadCheckpoint(s))
                        {
                            ChapterPlayService.ApplySharedSettings(s);
                            // 围猎三(#14/29):恢复点可能跨季/跨章节,收尾与读档/继续经营同口径。
                            PostLoadRefresh(s);
                            RefreshHud();
                            ShowToast("已恢复：" + s.experience.lastCheckpointReason);
                        }
                        else ShowToast("关键节点存档损坏，已保留当前状态");
                    });
            }, 19);
            PlaceTopLeft(restore.rectTransform(), 8f, 572f, 200f, 52f);

            Button choosePart = UiTheme.MakeButton(c, chapterPlay ? "选择其他篇" : "选篇开始", () =>
            {
                if (s.phase == GamePhase.Morning && chapterPlay) SaveSystem.Save(s);
                ClosePanel(root);
                ShowChapterSelection();
            }, 18);
            PlaceTopLeft(choosePart.rectTransform(), 228f, 572f, 200f, 52f);

            Button returnStory = UiTheme.MakeButton(c,
                chapterPlay ? "回到正式剧情" : "当前为正式剧情", () =>
                {
                    ShowConfirmation("回到正式剧情",
                        SaveSystem.HasSave(SaveSlot.Story)
                            ? "将保存当前篇章游玩，然后读取正式剧情档。"
                            : "将保存当前篇章游玩，然后从新剧情开始。",
                        ReturnToStoryFromChapterPlay, "保存并返回");
                }, 17);
            returnStory.interactable = chapterPlay;
            PlaceTopLeft(returnStory.rectTransform(), 448f, 572f, 200f, 52f);

            ExperienceText(c, (chapterPlay ? "篇章游玩 · " : "正式剧情 · ") +
                "经济小镇 " + GameState.Version + " · 四个视角共享同一个 WorldDay",
                15, 8f, 636f, 860f, 28f, UiTheme.TextSub, TextAnchor.MiddleCenter);
        }

        void SettingRow(RectTransform parent, string label, string value, float y, string hint, System.Action action)
        {
            ExperienceText(parent, label, 17, 18f, y, 180f, 42f, UiTheme.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            Button button = UiTheme.MakeButton(parent, value, () =>
            {
                settingsHint = hint;
                if (settingsHintLine != null) settingsHintLine.text = hint;
                if (action != null) action();
            }, 17);
            PlaceTopLeft(button.rectTransform(), 214f, y, 184f, 42f);
            UiTheme.SetButtonStyle(button, UiTheme.Paper, UiTheme.Ink);

            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                if (settingsHintLine != null) settingsHintLine.text = hint;
            });
            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                if (settingsHintLine != null) settingsHintLine.text = settingsHint;
            });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        void ReloadSettings(GameObject root)
        {
            GameState.I.Changed();
            ChapterPlayService.SaveSharedSettings(GameState.I);
            ClosePanel(root);
            ShowSettings();
        }
    }
}
