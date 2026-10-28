using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>经营日报 + “为什么”解释 + 终局评价(见 PRD §19/§20/§28)。</summary>
    public partial class UIManager
    {
        public void ShowReport(DayRecord r)
        {
            if (r == null) return;
            GameState s = GameState.I;
            GameObject root = BeginPanel("第 " + r.day + " 日经营报告" +
                (string.IsNullOrEmpty(r.eventName) ? "" : " · " + r.eventName), 1060, 760);
            RectTransform c = ContentOf(root);
            int desired = r.desiredDemand > 0 ? r.desiredDemand : r.demandTotal;
            int available = r.availableSupply > 0 ? r.availableSupply : r.produced + r.inventoryEnd;
            int actual = r.actualSales > 0 ? r.actualSales : r.sold;
            float listed = r.listedPrice > 0f ? r.listedPrice : r.price;
            float consumer = r.consumerPrice > 0f ? r.consumerPrice : listed;
            float producer = r.producerNetPrice > 0f ? r.producerNetPrice : listed;

            RectTransform demandCard = ReportCard(c, "01 · 市场流量", 0f, 0f, 322f, 126f, UiTheme.Market);
            ReportText(demandCard, "想买", desired + " 杯", 16f, 43f, 86f, 54f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            ReportText(demandCard, "可售", available + " 杯", 112f, 43f, 86f, 54f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            ReportText(demandCard, "卖出", actual + " 杯", 208f, 43f, 96f, 54f,
                actual < desired ? UiTheme.Bad : UiTheme.Market, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
            ReportText(demandCard, r.shopOpen ? "已营业" : "今日歇业", r.operationalStockout > 0 ?
                "经营性缺货 " + r.operationalStockout : "库存结余 " + r.operationalSurplus,
                16f, 91f, 286f, 24f, UiTheme.Ink, 15, TextAnchor.MiddleLeft);

            RectTransform priceCard = ReportCard(c, "02 · 三种价格", 338f, 0f, 322f, 126f, UiTheme.Accent);
            ReportText(priceCard, "标价", "¥" + listed.ToString("0.0"), 16f, 43f, 88f, 48f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            ReportText(priceCard, "顾客实付", "¥" + consumer.ToString("0.0"), 108f, 43f, 98f, 48f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            ReportText(priceCard, "企业实收", "¥" + producer.ToString("0.0"), 210f, 43f, 98f, 48f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            string policy = r.priceCeilingBinding ? "价格上限正在约束 · 政策短缺 " + r.policyShortage :
                r.priceFloorBinding ? "价格下限正在约束 · 政策过剩 " + r.policySurplus : "当前没有约束性价格管制";
            ReportText(priceCard, policy, null, 16f, 91f, 290f, 24f, UiTheme.Ink, 15, TextAnchor.MiddleLeft);

            RectTransform resultCard = ReportCard(c, "03 · 经营结果", 676f, 0f, 322f, 126f,
                r.profit >= 0f ? UiTheme.Good : UiTheme.Bad);
            ReportText(resultCard, "收入", "¥" + r.revenue.ToString("0"), 16f, 43f, 90f, 42f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            ReportText(resultCard, "总成本", "¥" + r.totalCost.ToString("0"), 112f, 43f, 96f, 42f, UiTheme.Ink, 17, TextAnchor.MiddleLeft);
            int profitShown = Mathf.RoundToInt(r.profit);   // 围猎三#3 复审:先取整再判号,收支恰抵不再显示「¥-0」
            ReportText(resultCard, "利润", (profitShown >= 0 ? "+" : "") + "¥" + profitShown,
                210f, 39f, 100f, 50f, profitShown >= 0 ? UiTheme.Good : UiTheme.Bad, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            ReportText(resultCard, "满意度 " + r.satisfaction.ToString("0") + "  ·  资金余额 ¥" + r.moneyEnd.ToString("0"), null,
                16f, 91f, 290f, 24f, UiTheme.Ink, 15, TextAnchor.MiddleLeft);

            RectTransform effectsCard = ReportCard(c, "需求因果链 · 本日真实归因", 0f, 142f, 998f, 102f, UiTheme.Market);
            DemandEffectBreakdown effects = r.demandEffects;
            string effectLine = effects == null ? "旧存档没有保存完整归因；从下一次营业开始记录。" :
                Explainer.CompactDemandEffects(effects, 5);
            ReportText(effectsCard, effectLine, null, 18f, 42f, 962f, 48f, UiTheme.Ink, 18, TextAnchor.MiddleLeft, FontStyle.Bold);

            RectTransform costCard = ReportCard(c, "成本账本", 0f, 260f, 630f, 126f, UiTheme.Accent);
            string costLine = "原料 ¥" + r.materialCost.ToString("0") + "  +  工资 ¥" + r.wageCost.ToString("0") +
                "  +  固定成本 ¥" + r.rentCost.ToString("0");
            if (r.playerMarketing > 0f) costLine += "  +  广告 ¥" + r.playerMarketing.ToString("0");
            if (r.strategicSpend > 0f) costLine += "  +  投资 ¥" + r.strategicSpend.ToString("0");
            costLine += "  =  ¥" + r.totalCost.ToString("0");
            ReportText(costCard, costLine, null, 18f, 42f, 594f, 34f, UiTheme.Ink, 17, TextAnchor.MiddleLeft, FontStyle.Bold);
            string competitionLine = r.competitionActive ? "份额 你 " + (r.playerMarketShare * 100f).ToString("0.0") +
                "% / Sunny Tea " + (r.competitorMarketShare * 100f).ToString("0.0") + "%  ·  未购买 " + r.outsideDemand + " 人" :
                "学生 " + r.demandStudents + " / 白领 " + r.demandWhitecollars + " / 游客 " + r.demandTourists;
            ReportText(costCard, competitionLine, null, 18f, 80f, 594f, 28f, UiTheme.Ink, 15, TextAnchor.MiddleLeft);

            RectTransform compareCard = ReportCard(c, "与昨日相比", 646f, 260f, 352f, 126f, UiTheme.Market);
            ReportText(compareCard, Explainer.DayOverDay(s, r), null, 18f, 40f, 316f, 72f,
                UiTheme.Ink, 16, TextAnchor.UpperLeft, FontStyle.Bold);

            float actionY = 402f;
            ReportAction(c, "需求与销量为什么这样？", () => ShowExplain(Explainer.ExplainSales(s, r)), 0f, actionY);
            ReportAction(c, "利润怎么算？", () => ShowExplain(Explainer.ExplainProfit(s, r)), 338f, actionY);
            ReportAction(c, "库存说明什么？", () => ShowExplain(Explainer.ExplainInventory(s, r)), 676f, actionY);
            ReportAction(c, r.competitionActive ? "查看竞争曲线" : "查看需求曲线",
                () => { CloseTop(); ShowCurves(r.competitionActive ? 3 : 0); }, 0f, actionY + 56f);
            if (r.competitionActive)
            {
                ReportAction(c, "顾客去了哪里？", () => ShowCompetitionWhy(r), 338f, actionY + 56f);
                ReportAction(c, "按同一模型重算反事实", () => ShowCounterfactual(r), 676f, actionY + 56f);
            }

            LongTermExperienceService.Ensure(s);
            bool financialCrisis = s.economy != null && s.economy.financialCrisisActive &&
                !s.experience.enterpriseRecovery.active;
            bool recovering = s.experience.enterpriseRecovery.active;
            Button next = UiTheme.MakeButton(c, financialCrisis ? "处理财务危机" :
                recovering ? "继续恢复 · 进入下一天 →" : "进入下一天 →", () =>
            {
                CloseTop();
                if (financialCrisis)
                    // 围猎三(#13):危机面板走宿主包装——镜像 GameBootstrap 收日阻塞路径的
                    // CutsceneDirector.TryPlay(CrisisClouds, ...)。日报主按钮直调会旁路首播,
                    // 玩家任选恢复计划后 recovery.active=true,「乌云压城」本档永远不播。
                    CutsceneDirector.TryPlay(s, (int)CutsceneId.CrisisClouds, ShowFinancialCrisis);
                else GameBootstrap.I.NextDay();
            }, 26);
            next.rectTransform().anchorMin = next.rectTransform().anchorMax = new Vector2(0.5f, 0);
            next.rectTransform().pivot = new Vector2(0.5f, 0f);
            next.rectTransform().anchoredPosition = new Vector2(0, 8);
            next.rectTransform().sizeDelta = new Vector2(340, 60);
            UiTheme.SetButtonStyle(next,
                financialCrisis ? UiTheme.Bad : recovering ? UiTheme.Accent : UiTheme.Market,
                Color.white);
        }

        public void ShowFinancialCrisis()
        {
            GameState s = GameState.I;
            if (s == null || s.economy == null) return;
            LongTermExperienceService.Ensure(s);
            bool mandatory = s.economy.financialCrisisActive && !s.experience.enterpriseRecovery.active;
            if (!mandatory && !s.experience.enterpriseRecovery.active) return;
            GameObject root = BeginPanel(mandatory ? "财务危机 · 选择恢复计划" : "企业恢复计划 · 进行中", 860f, 640f);
            if (mandatory)
            {
                MarkMandatoryDecision(root);
                HideFalseClose(root);
            }
            RectTransform c = ContentOf(root);
            const float width = 812f;
            string status = "现金 ¥" + s.money.ToString("0") + "    库存 " + s.inventory + " 杯    员工 " + s.employees + " 人\n" +
                (s.experience.enterpriseRecovery.active
                    ? s.experience.enterpriseRecovery.latestStatus
                    : "选一个动作之后还要继续经营。连续 2 天赚到钱，并且资金回到 ¥-1000 以上，才算缓过来。");
            float y = EventBrief(c, mandatory ? "连续三天，店里的钱低于 ¥-1000" : "恢复还没完成",
                status, 0f, 0f, width, mandatory ? UiTheme.Bad : UiTheme.Accent) + 16f;

            if (mandatory)
            {
                float col = (width - 16f) * .5f;
                EventChoice(c, "清库存并重新定价", "折价回笼资金，售价靠近成本", 0f, y, col,
                    () => ApplyEnterpriseRecovery(root, "liquidate", mandatory));
                EventChoice(c, "紧缩经营", "减少 1 名员工，停下广告", col + 16f, y, col,
                    () => ApplyEnterpriseRecovery(root, "tighten", mandatory));
                EventChoice(c, "出售一档设备", "换一笔周转金，品质可能下降", 0f, y + 100f, col,
                    () => ApplyEnterpriseRecovery(root, "asset", mandatory));
                EventChoice(c, "暂停并重整", "店从基础规模重开，其他进度保留", col + 16f, y + 100f, col, () =>
                {
                    LongTermExperienceService.PauseAndRestartEnterprise(s);
                    ResolveMandatoryDecision(root);
                    if (s.experience.pendingCrisisReview != null) ShowCrisisReview(s.experience.pendingCrisisReview);
                });
                y += 216f;
            }

            Text note = UiTheme.MakeText(c, "缓过来之后，这里会留下一张复盘：当时为什么亏、你做了什么、结果如何。",
                16, UiTheme.TextSub, TextAnchor.MiddleLeft);
            PlaceTopLeft(note.rectTransform, 0f, y, width, 48f);
            y += 56f;
            if (!mandatory)
            {
                Button close = UiTheme.MakeButton(c, "返回经营", () => ClosePanel(root), 18);
                PlaceTopLeft(close.rectTransform(), (width - 220f) * .5f, y, 220f, 48f);
                y += 48f;
            }
            FitEventPanel(root, y);
        }

        void ApplyEnterpriseRecovery(GameObject root, string plan, bool mandatory)
        {
            GameState state = GameState.I;
            LongTermExperienceService.BeginEnterpriseRecovery(state, plan);
            if (mandatory) ResolveMandatoryDecision(root); else ClosePanel(root);
            ShowToast("恢复动作已执行；接下来需要用真实经营结果证明现金流已经修复");
        }

        RectTransform ReportCard(RectTransform parent, string title, float x, float y, float width, float height, Color accent)
        {
            Image card = UiTheme.MakeImage(parent, UiTheme.Paper, "reportCard_" + title);
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            Image stripe = UiTheme.MakeImage(card.transform, accent, "stripe");
            PlaceTopLeft(stripe.rectTransform, 0f, 0f, 6f, height);
            ReportText(card.rectTransform, title, null, 16f, 8f, width - 28f, 28f,
                UiTheme.Ink, 17, TextAnchor.MiddleLeft, FontStyle.Bold);
            return card.rectTransform;
        }

        Text ReportText(RectTransform parent, string label, string value, float x, float y, float width, float height,
            Color color, int size, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            string text = value == null ? label : "<size=80%>" + label + "</size>\n" + value;
            Text result = UiTheme.MakeText(parent, text, size, color, anchor, style);
            PlaceTopLeft(result.rectTransform, x, y, width, height);
            result.verticalOverflow = VerticalWrapMode.Truncate;
            return result;
        }

        void ReportAction(RectTransform parent, string label, System.Action action, float x, float y)
        {
            Button button = UiTheme.MakeButton(parent, label, action, 18);
            PlaceTopLeft(button.rectTransform(), x, y, 322f, 48f);
        }

        public void ShowExplain(Explainer.Explanation e)
        {
            GameObject root = BeginPanel(e.title, 760, 620);
            RectTransform c = ContentOf(root);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < e.paragraphs.Length; i++)
            {
                if (i > 0) sb.Append("\n\n");
                sb.Append(e.paragraphs[i]);
            }
            Image scrollArea = UiTheme.MakeImage(c, new Color32(0x3b, 0x2e, 0x27, 255), "explainScroll");
            PlaceTopLeft(scrollArea.rectTransform, 0f, 0f, 712f, 440f);
            scrollArea.raycastTarget = true;
            scrollArea.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new GameObject("scrollContent", typeof(RectTransform));
            contentObject.transform.SetParent(scrollArea.transform, false);
            RectTransform scrollContent = contentObject.GetComponent<RectTransform>();
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;

            Text t = UiTheme.MakeText(scrollContent, sb.ToString(), 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            t.rectTransform.sizeDelta = new Vector2(-32f, 100f);
            float bodyHeight = Mathf.Max(408f, MeasuredTextHeight(t));
            t.rectTransform.sizeDelta = new Vector2(-32f, bodyHeight);
            scrollContent.sizeDelta = new Vector2(0f, bodyHeight + 32f);

            ScrollRect scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollArea.rectTransform;
            scroll.content = scrollContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.verticalNormalizedPosition = 1f;

            Text scrollHint = UiTheme.MakeText(c, "内容较长时可滚轮或拖动查看", 14,
                UiTheme.TextSub, TextAnchor.MiddleCenter);
            scrollHint.rectTransform.anchorMin = scrollHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            scrollHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            scrollHint.rectTransform.anchoredPosition = new Vector2(0f, 66f);
            scrollHint.rectTransform.sizeDelta = new Vector2(420f, 24f);

            CardDef card = e.concept != null ? CardLibrary.Get(e.concept) : null;
            if (card != null && GameState.I != null && GameState.I.unlockedCards.Contains(card.id))
            {
                Button toCard = UiTheme.MakeButton(c, "查看知识卡《" + card.title + "》", () => { CloseTop(); ShowCardDetail(card.id); }, 20);
                toCard.rectTransform().anchorMin = toCard.rectTransform().anchorMax = new Vector2(0.5f, 0f);
                toCard.rectTransform().pivot = new Vector2(0.5f, 0f);
                toCard.rectTransform().anchoredPosition = new Vector2(-160f, 8f);
                toCard.rectTransform().sizeDelta = new Vector2(300f, 48f);
            }

            Button ok = UiTheme.MakeButton(c, "关闭", CloseTop, 21);
            ok.rectTransform().anchorMin = ok.rectTransform().anchorMax = new Vector2(0.5f, 0);
            ok.rectTransform().pivot = new Vector2(0.5f, 0f);
            ok.rectTransform().anchoredPosition = new Vector2(card != null && GameState.I != null &&
                GameState.I.unlockedCards.Contains(card.id) ? 190f : 0f, 8f);
            ok.rectTransform().sizeDelta = new Vector2(180, 48);
        }
    }
}
