using System;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.4 市政府：政策设置、财政周期、外部性、公共品与城市结果。</summary>
    public partial class UIManager
    {
        static readonly Color GovCard = new Color32(0x42, 0x38, 0x2d, 255);
        static readonly Color GovCardSoft = new Color32(0x38, 0x31, 0x29, 255);
        static readonly Color GovTeal = new Color32(0x65, 0xb8, 0xb0, 255);
        static readonly Color GovPaper = new Color32(0xe9, 0xd4, 0xa8, 255);

        public void ShowGovernmentDashboard(int selectedTab = 0)
        {
            GameState state = GameState.I;
            if (state == null) return;
            SyncCurrentView(state, PlayerViewMode.Government);
            GovernmentSimulator.Ensure(state);
            selectedTab = Mathf.Clamp(selectedTab, 0, 6);

            GovernmentState government = state.government;
            GameObject root = BeginPanel("市政厅 · 城市政策作战桌", 1260f, 840f);
            root.name = "panel_government_dashboard";
            RectTransform content = ContentOf(root);

            string governmentMode = government.termReportViewed ?
                "主线政策课程已完成 · 自由治理" : GovernmentSimulator.ChapterTitle(government.chapter);
            GovText(content, governmentMode, 19, GovTeal,
                0f, 0f, 620f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(content, "政策周期 " + government.cycle + "  ·  任期 " + Mathf.Min(10, government.history.Count) + "/10",
                16, UiTheme.TextSub, 650f, 0f, 220f, 34f, TextAnchor.MiddleRight);
            Button macroCenter = UiTheme.MakeButton(content, "宏观中心", () => { CloseTop(); ShowMacroDashboard(); }, 17);
            PlaceTopLeft(macroCenter.rectTransform(), 882f, 0f, 150f, 36f);
            UiTheme.SetButtonStyle(macroCenter, GovTeal, UiTheme.Ink);
            bool reportPending = government.termReportReady && !government.termReportViewed;
            Button nextCycle = UiTheme.MakeButton(content, reportPending ? "查看任期报告" : "统一结算本日", () =>
            {
                if (reportPending) ShowMayorTermReport();
                else AdvanceGovernmentCycle(selectedTab);
            }, 18);
            PlaceTopLeft(nextCycle.rectTransform(), 1042f, 0f, 170f, 36f);
            UiTheme.SetButtonStyle(nextCycle, UiTheme.Accent, UiTheme.Ink);

            GovMetric(content, "政府资金", "¥ " + government.cash.ToString("0"), 0f,
                government.cash > 1000f ? UiTheme.Good : UiTheme.Bad);
            int balanceShown = Mathf.RoundToInt(government.budgetBalance);   // 围猎三#3 复审:先取整再判号,差额跨零不再显示「¥ -0」
            GovMetric(content, "本期收支", (balanceShown >= 0 ? "+" : "") + "¥ " + balanceShown, 244f,
                balanceShown >= 0 ? UiTheme.Good : UiTheme.Bad);
            GovMetric(content, "城市支持率", government.approvalRating.ToString("0") + "%", 488f, GovTeal);
            GovMetric(content, "就业率", government.employmentRate.ToString("0.0") + "%", 732f, UiTheme.Accent);
            GovMetric(content, "环境质量", government.environmentQuality.ToString("0"), 976f,
                government.environmentQuality >= 60f ? UiTheme.Good : UiTheme.Bad);

            string[] tabs = { "财政总览", "税收与限价", "补贴与福利", "劳动就业", "环境治理", "公共建设", "城市数据" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button button = UiTheme.MakeButton(content, tabs[i], () => ReloadGovernment(tab), 17);
                PlaceTopLeft(button.rectTransform(), i * 173f, 122f, 162f, 44f);
                UiTheme.SetButtonStyle(button, i == selectedTab ? UiTheme.Accent : GovPaper,
                    i == selectedTab ? UiTheme.Ink : new Color32(0x4c, 0x3d, 0x31, 255));
                button.interactable = i != selectedTab;
            }

            if (selectedTab == 0) BuildFiscalTab(content, state);
            else if (selectedTab == 1) BuildTaxTab(content, state);
            else if (selectedTab == 2) BuildSubsidyTab(content, state);
            else if (selectedTab == 3) BuildLabourTab(content, state);
            else if (selectedTab == 4) BuildEnvironmentTab(content, state);
            else if (selectedTab == 5) BuildPublicGoodsTab(content, state);
            else BuildCityDataTab(content, state);
        }

        void BuildFiscalTab(RectTransform content, GameState state)
        {
            GovernmentState g = state.government;
            GovernmentBudgetPreview budget = GovernmentSimulator.PreviewBudget(state);
            RectTransform ledger = GovCardPanel(content, "本周期财政账本", "钱从哪里来，又花到哪里去", 0f, 182f, 580f, 474f);
            GovLedgerRow(ledger, "规费与自动税收", budget.otherFeesRevenue + budget.automaticTaxChange, 64f, true);
            GovLedgerRow(ledger, "饮品税（今日待入账）", budget.drinkTaxRevenue, 94f, true);
            GovLedgerRow(ledger, "污染税（今日待入账）", budget.pollutionTaxRevenue, 124f, true);
            // bug搜#17:补关税一行——预计总收入(totalIncome)内部含 tariffRevenue,漏行则三行
            // 分项之和恒比总收入少关税额,「预计余额」无法从可见行对出,账本教学口径不闭合。
            GovLedgerRow(ledger, "进口关税（今日待入账）", budget.tariffRevenue, 154f, true);
            GovLedgerRule(ledger, 184f);
            GovLedgerRow(ledger, "预计总收入", budget.totalIncome, 190f, true, true);
            GovLedgerRow(ledger, "行政管理", budget.administrativeSpending, 230f, false);
            GovLedgerRow(ledger, "福利 · " + GovernmentSimulator.WelfareEnrolledResidents(state) + " 人(首批覆盖 25%)", budget.welfareSpending, 260f, false);
            GovLedgerRow(ledger, "商品/设备/培训补贴", budget.subsidySpending + budget.trainingSpending, 290f, false);
            GovLedgerRow(ledger, "公共服务与项目维护", budget.publicServiceSpending, 320f, false);
            GovLedgerRow(ledger, "公共项目与财政立场", budget.capitalSpending + budget.fiscalPurchase, 350f, false);
            GovLedgerRow(ledger, "债务利息", budget.interestSpending, 380f, false);
            GovLedgerRule(ledger, 416f);
            GovLedgerRow(ledger, "预计余额（完整口径）", budget.balance, 424f, budget.balance >= 0f, true);

            RectTransform chain = GovCardPanel(content, "政策后果时间轴", "同一项政策不会在一瞬间完成全部影响", 600f, 182f, 612f, 268f);
            PolicyImpactRecord latest = g.impacts != null && g.impacts.Count > 0 ? g.impacts[g.impacts.Count - 1] : null;
            BuildCausalTimeline(chain, latest);
            if (latest != null)
            {
                Button review = UiTheme.MakeButton(chain, "查看赢家、输家与完整因果链", () => ShowPolicyImpact(latest.type), 17);
                PlaceTopLeft(review.rectTransform(), 278f, 201f, 308f, 42f);
                UiTheme.SetButtonStyle(review, GovTeal, Color.white);
            }

            RectTransform brief = GovCardPanel(content, "市长简报", "决策前先看约束，不替你给出唯一答案", 600f, 466f, 612f, 190f);
            string summary = string.IsNullOrEmpty(g.lastPolicySummary) ? "尚未调整政策。先运行公共品实验，或从上方栏目选择一项政策。" : g.lastPolicySummary;
            GovText(brief, summary, 18, UiTheme.TextMain, 18f, 58f, 576f, 54f, TextAnchor.UpperLeft);
            string npc = string.IsNullOrEmpty(g.lastNpcFeedback) ? "居民代表：我们关心服务，也关心这笔钱由谁承担。" : g.lastNpcFeedback;
            GovText(brief, npc, 16, UiTheme.TextSub, 18f, 116f, 400f, 48f, TextAnchor.UpperLeft);
            if (g.currentEvent != null && !g.currentEvent.resolved)
            {
                Button eventButton = UiTheme.MakeButton(brief, "处理事件：" + g.currentEvent.title, ShowGovernmentEvent, 17);
                PlaceTopLeft(eventButton.rectTransform(), 424f, 116f, 170f, 48f);
                UiTheme.SetButtonStyle(eventButton, UiTheme.Bad, Color.white);
            }
        }

        void BuildTaxTab(RectTransform content, GameState state)
        {
            RectTransform controls = GovCardPanel(content, "饮品市场政策", "先调政策，再从右侧看均衡、税负和短缺", 0f, 182f, 580f, 474f);
            GovPolicyRow(controls, state, PolicyType.DrinkTax, "单位税", "税负由供需弹性共同分担", 62f, 1);
            GovPolicyRow(controls, state, PolicyType.PriceCeiling, "价格上限", "低于均衡价可能出现短缺", 151f, 1);
            GovPolicyRow(controls, state, PolicyType.PriceFloor, "价格下限", "高于均衡价可能出现过剩", 240f, 1);
            float share = GovernmentSimulator.ConsumerTaxShare(state);
            // U6 修复:货架价读 PlayerListedPrice(含玩家临时调价),不是基础价 state.price
            float listed = PriceService.PlayerListedPrice(state);
            float consumerPrice = GovernmentSimulator.DrinkConsumerPrice(state, listed);
            float producerPrice = GovernmentSimulator.DrinkProducerPrice(state, listed);
            DayRecord last = state.LastRecord;
            string rationing = last != null && last.policyShortage > 0 ? "  ·  短缺 " + last.policyShortage + " 杯" :
                last != null && last.policySurplus > 0 ? "  ·  过剩 " + last.policySurplus + " 杯" : "";
            float ceiling = GovernmentSimulator.PolicyValue(state, PolicyType.PriceCeiling);
            float floor = GovernmentSimulator.PolicyValue(state, PolicyType.PriceFloor);
            string conflict = ceiling > 0f && floor > 0f && ceiling < floor
                ? "\n政策冲突：价格上限低于价格下限，市场无法同时满足两项规则。"
                : "";
            GovText(controls, "当前传导", 16, GovTeal, 18f, 342f, 120f, 24f, TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(controls,
                "货架价 ¥" + listed.ToString("0") + "  →  消费者支付 ¥" + consumerPrice.ToString("0.0") +
                "  /  企业实收 ¥" + producerPrice.ToString("0.0") + rationing + "\n消费者承担 " +
                (share * 100f).ToString("0") + "% 的单位税；其余由企业承担。" + conflict,
                17, UiTheme.TextMain, 18f, 370f, 542f, 66f, TextAnchor.UpperLeft);

            RectTransform graph = GovCardPanel(content, "供需与政策楔子", "青色 D · 橙色 S · 红色 税后供给 · 蓝/紫 限价线", 600f, 182f, 612f, 474f);
            AddGovernmentGraph(graph, state, GovernmentGraphMode.TaxAndPrice, 18f, 64f, 576f, 335f);
            GovText(graph, "价格 P", 14, UiTheme.TextSub, 18f, 57f, 74f, 20f);
            GovText(graph, "数量 Q", 14, UiTheme.TextSub, 500f, 397f, 78f, 22f, TextAnchor.MiddleRight);
            float tax = GovernmentSimulator.PolicyValue(state, PolicyType.DrinkTax);
            GovText(graph, tax > 0f ? "红色三角表示因交易减少产生的无谓损失。" : "当前无单位税：供需在白点附近达到市场均衡。",
                15, UiTheme.TextSub, 18f, 425f, 560f, 30f, TextAnchor.MiddleCenter);
        }

        void BuildSubsidyTab(RectTransform content, GameState state)
        {
            RectTransform controls = GovCardPanel(content, "补贴组合", "补贴降低私人承担，但每个周期都会占用财政空间", 0f, 182f, 650f, 474f);
            GovPolicyRow(controls, state, PolicyType.DrinkSubsidy, "健康饮品补贴", "扩大消费与供给，支出随销量增加", 62f, 2);
            GovPolicyRow(controls, state, PolicyType.TrainingSubsidy, "职业培训补贴", "个人学习也会让企业与社会受益", 151f, 2);
            GovPolicyRow(controls, state, PolicyType.BasicWelfare, "基础生活补贴", "提高低收入购买力，注意长期可持续性", 240f, 2);
            GovPolicyRow(controls, state, PolicyType.PublicServiceBudget, "公共服务预算", "维护越充分，服务与吸引力越稳定", 329f, 2);

            RectTransform impact = GovCardPanel(content, "效率与公平观察", "同一项补贴会在群体之间产生不同结果", 670f, 182f, 542f, 474f);
            float welfare = GovernmentSimulator.PolicyValue(state, PolicyType.BasicWelfare);
            float training = GovernmentSimulator.PolicyValue(state, PolicyType.TrainingSubsidy);
            GovBigNumber(impact, "最低收入组购买力", state.government.lowestGroupPurchasingPower.ToString("0"), 18f, 64f, 238f, GovTeal);
            GovBigNumber(impact, "低收入居民支持", state.government.lowIncomeSupport.ToString("0") + "%", 278f, 64f, 238f, UiTheme.Accent);
            GovText(impact, "政策权衡", 18, UiTheme.TextMain, 18f, 172f, 180f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold);
            string observation = welfare <= 0f
                ? "尚未发放生活补贴。低收入组主要依靠工资与公共服务改善购买力。"
                : "生活补贴已提高低收入组的短期购买力；下一步要观察税收能否覆盖这项持续支出。";
            if (training > 0f) observation += " 培训补贴还会通过技能提升产生正外部性。";
            GovText(impact, observation, 17, UiTheme.TextSub, 18f, 208f, 506f, 78f, TextAnchor.UpperLeft);
            GovText(impact, "效率不是唯一目标，公平也不是免费获得。你需要决定愿意接受哪种组合。", 18,
                UiTheme.TextMain, 18f, 318f, 506f, 74f, TextAnchor.UpperLeft);
            Button card = UiTheme.MakeButton(impact, "打开知识卡：效率与公平", () => ShowCardDetail("equityefficiency"), 17);
            PlaceTopLeft(card.rectTransform(), 18f, 405f, 506f, 42f);
        }

        void BuildLabourTab(RectTransform content, GameState state)
        {
            RectTransform control = GovCardPanel(content, "最低工资实验", "观察收入提升与招聘压力为何可能同时出现", 0f, 182f, 580f, 474f);
            GovPolicyRow(control, state, PolicyType.MinimumWage, "最低时薪", "基准时薪 ¥25；提高后不会线性裁员", 62f, 3);
            float minWage = GovernmentSimulator.PolicyValue(state, PolicyType.MinimumWage);
            float cost = GovernmentSimulator.EffectiveDailyWage(state);
            GovBigNumber(control, "企业每名员工日工资", "¥ " + cost.ToString("0"), 18f, 174f, 258f, UiTheme.Accent);
            GovBigNumber(control, "城市就业率", state.government.employmentRate.ToString("0.0") + "%", 300f, 174f, 258f, GovTeal);
            string threshold = minWage <= 25f
                ? "当前标准未高于市场基准，企业招聘行为基本不受约束。"
                : minWage <= 30f
                    ? "温和提高工资：仍在岗员工收入改善，需求和消费可能缓冲一部分成本。"
                    : "工资明显高于基准：利润薄弱企业会缩减招聘，影响取决于需求与利润空间。";
            GovText(control, threshold, 18, UiTheme.TextMain, 18f, 298f, 542f, 82f, TextAnchor.UpperLeft);
            GovText(control, "提示：把最低工资单独设得很高，与高税或限价叠加时，企业承压会更明显。", 16,
                UiTheme.TextSub, 18f, 397f, 542f, 48f, TextAnchor.UpperLeft);

            RectTransform outcome = GovCardPanel(content, "谁得到、谁承担", "最低工资不是一个只有单向结果的按钮", 600f, 182f, 612f, 474f);
            GovOutcomeBlock(outcome, "直接受益", "仍然就业的低工资员工：工资与购买力提高。", 18f, 66f, UiTheme.Good);
            GovOutcomeBlock(outcome, "可能承压", "利润空间较小的企业、新求职者，以及承担涨价的消费者。", 18f, 158f, UiTheme.Bad);
            GovOutcomeBlock(outcome, "关键条件", "商品需求、企业利润与其他政策决定企业是吸收成本、涨价，还是减少招聘。", 18f, 250f, GovTeal);
            Button impact = UiTheme.MakeButton(outcome, "查看三阶段政策影响", () => ShowPolicyImpact(PolicyType.MinimumWage), 18);
            PlaceTopLeft(impact.rectTransform(), 18f, 390f, 576f, 48f);
            UiTheme.SetButtonStyle(impact, GovTeal, Color.white);
        }

        void BuildEnvironmentTab(RectTransform content, GameState state)
        {
            GovernmentState g = state.government;
            RectTransform controls = GovCardPanel(content, "工厂治理", "污染成本没有完全出现在企业账本里", 0f, 182f, 580f, 474f);
            GovPolicyRow(controls, state, PolicyType.PollutionTax, "污染税", "让企业为排放造成的社会成本付费", 62f, 4);
            GovPolicyRow(controls, state, PolicyType.GreenEquipmentSubsidy, "环保设备补贴", "降低改造门槛，政府承担部分费用", 151f, 4);
            GovBigNumber(controls, "工厂产量", g.factory.production.ToString("0"), 18f, 270f, 166f, UiTheme.Accent);
            GovBigNumber(controls, "工厂就业", g.factory.jobs.ToString("0") + " 人", 204f, 270f, 166f, GovTeal);
            GovBigNumber(controls, "污染排放", g.factory.pollution.ToString("0"), 390f, 270f, 166f,
                g.factory.pollution <= 40f ? UiTheme.Good : UiTheme.Bad);
            string equipment = g.factory.greenEquipmentInstalled ? "环保设备已安装：单位产量污染显著下降。" :
                "环保改造进度 " + g.factory.greenUpgradeProgress.ToString("0") + "%：补贴越高，升级越快。";
            GovText(controls, equipment, 17, UiTheme.TextMain, 18f, 382f, 542f, 52f, TextAnchor.UpperLeft);

            RectTransform graph = GovCardPanel(content, "社会成本与市场产量", "橙色 MPC · 绿色 MSC · 青色需求/边际收益", 600f, 182f, 612f, 474f);
            AddGovernmentGraph(graph, state, GovernmentGraphMode.Externality, 18f, 64f, 576f, 330f);
            GovText(graph, "社会最优产量", 14, UiTheme.Good, 170f, 390f, 132f, 24f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovText(graph, "市场产量", 14, UiTheme.Accent, 322f, 390f, 110f, 24f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovText(graph, "两条成本曲线之间的红色区域，是生产者账外却由社会承担的污染损失。", 15,
                UiTheme.TextSub, 18f, 423f, 576f, 30f, TextAnchor.MiddleCenter);
        }

        void BuildPublicGoodsTab(RectTransform content, GameState state)
        {
            GovernmentState g = state.government;
            RectTransform experiment = GovCardPanel(content, "免费搭便车实验", "20 位居民都想要路灯，但个人可以选择不出钱", 0f, 182f, 440f, 474f);
            string result = g.freeRiderExperimentDone
                ? g.freeRiderContributors + " / 20 人出资\n共募集 ¥" + g.freeRiderFundsRaised.ToString("0") + " / 目标 ¥1000"
                : "尚未运行\n目标：自愿募集 ¥1000";
            GovText(experiment, result, 25, g.freeRiderExperimentDone ? UiTheme.Bad : GovTeal,
                18f, 70f, 404f, 90f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovText(experiment,
                g.freeRiderExperimentDone ? "路灯对所有人都有好处，即使没出钱也无法把他排除。自愿筹资因此失败。" :
                "实验不会扣除政府资金。它用来观察公共品为什么经常无法靠自愿交易充分提供。",
                17, UiTheme.TextMain, 18f, 184f, 404f, 92f, TextAnchor.UpperLeft);
            Button run = UiTheme.MakeButton(experiment, g.freeRiderExperimentDone ? "实验已完成" : "运行自愿筹资实验", () =>
            {
                if (GovernmentSimulator.RunFreeRiderExperiment(state)) ReloadGovernment(5);
            }, 18);
            PlaceTopLeft(run.rectTransform(), 18f, 306f, 404f, 50f);
            run.interactable = !g.freeRiderExperimentDone;
            GovText(experiment, "结论不是“居民自私”，而是个人有动力等待别人付费。政府提供公共品正是在改变这组激励。",
                15, UiTheme.TextSub, 18f, 377f, 404f, 66f, TextAnchor.UpperLeft);

            RectTransform projects = GovCardPanel(content, "公共建设清单", "一次建设 + 每周期维护；建成后全体居民共享", 460f, 182f, 752f, 474f);
            for (int i = 0; i < g.projects.Count; i++)
                GovProjectRow(projects, state, g.projects[i], 62f + i * 76f);
        }

        void BuildCityDataTab(RectTransform content, GameState state)
        {
            GovernmentState g = state.government;
            RectTransform city = GovCardPanel(content, "城市脉搏", "同一份交易数据，从企业、居民与政府三个视角汇总", 0f, 182f, 580f, 474f);
            GovDataRow(city, "居民幸福", g.residentHappiness, "%", 64f, GovTeal);
            GovDataRow(city, "企业信心", g.enterpriseConfidence, "%", 106f, UiTheme.Accent);
            GovDataRow(city, "公共服务", g.publicServiceLevel, "", 148f, UiTheme.Good);
            GovDataRow(city, "环境质量", g.environmentQuality, "", 190f, new Color32(0x75, 0xbd, 0x78, 255));
            GovDataRow(city, "低收入支持", g.lowIncomeSupport, "%", 252f, new Color32(0xd6, 0x9a, 0xcf, 255));
            GovDataRow(city, "企业支持", g.businessSupport, "%", 294f, new Color32(0x79, 0xa9, 0xd8, 255));
            GovDataRow(city, "学生支持", g.studentSupport, "%", 336f, new Color32(0xe1, 0xc1, 0x63, 255));
            GovText(city, "收入结构  低 " + g.lowIncomeShare.ToString("0") + "%  ·  中 " + g.middleIncomeShare.ToString("0") +
                "%  ·  高 " + g.highIncomeShare.ToString("0") + "%", 17, UiTheme.TextMain, 18f, 402f, 544f, 32f, TextAnchor.MiddleCenter);

            RectTransform history = GovCardPanel(content, "政策结果走势", "红色：财政余额 · 青色：城市支持率（最近 10 个周期）", 600f, 182f, 612f, 474f);
            AddGovernmentGraph(history, state, GovernmentGraphMode.CityHistory, 18f, 64f, 576f, 300f);
            string empty = g.history.Count == 0 ? "还没有周期数据。调整政策后点击“结束本周期”生成第一组观察。" :
                "已记录 " + g.history.Count + " 个周期。政策滞后意味着至少要比较多个点，而不是只看当天。";
            GovText(history, empty, 16, UiTheme.TextSub, 18f, 374f, 576f, 48f, TextAnchor.MiddleCenter);
            Button report = UiTheme.MakeButton(history, g.termReportReady ? "查看市长任期报告" : "任期报告将在第 7 章后开放", ShowMayorTermReport, 17);
            PlaceTopLeft(report.rectTransform(), 122f, 424f, 368f, 38f);
            report.interactable = g.termReportReady;
        }

        void GovMetric(RectTransform parent, string label, string value, float x, Color valueColor)
        {
            Image card = UiTheme.MakeImage(parent, GovCardSoft, "metric_" + label);
            PlaceTopLeft(card.rectTransform, x, 44f, 232f, 66f);
            Image marker = UiTheme.MakeImage(card.transform, valueColor, "metricMarker");
            PlaceTopLeft(marker.rectTransform, 0f, 0f, 5f, 66f);
            GovText(card.rectTransform, label, 14, UiTheme.TextSub, 16f, 6f, 116f, 20f);
            GovText(card.rectTransform, value, 24, valueColor, 16f, 25f, 200f, 36f, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        RectTransform GovCardPanel(RectTransform parent, string title, string caption, float x, float y, float width, float height)
        {
            Image card = UiTheme.MakeImage(parent, GovCard, "govCard_" + title);
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            Image rule = UiTheme.MakeImage(card.transform, UiTheme.Accent, "sectionRule");
            PlaceTopLeft(rule.rectTransform, 18f, 13f, 5f, 35f);
            GovText(card.rectTransform, title, 21, UiTheme.TextMain, 36f, 5f, width - 54f, 28f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(card.rectTransform, caption, 14, UiTheme.TextSub, 36f, 31f, width - 54f, 20f);
            return card.rectTransform;
        }

        Text GovText(RectTransform parent, string value, int size, Color color, float x, float y, float width, float height,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            return ShopText(parent, value, size, color, x, y, width, height, alignment, style);
        }

        void GovBigNumber(RectTransform parent, string label, string value, float x, float y, float width, Color color)
        {
            Image box = UiTheme.MakeImage(parent, GovCardSoft, "big_" + label);
            PlaceTopLeft(box.rectTransform, x, y, width, 100f);
            GovText(box.rectTransform, label, 14, UiTheme.TextSub, 14f, 8f, width - 28f, 22f);
            GovText(box.rectTransform, value, 28, color, 14f, 34f, width - 28f, 50f, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        void GovPolicyRow(RectTransform parent, GameState state, PolicyType type, string label, string note, float y, int tab)
        {
            PolicyState policy = GovernmentSimulator.FindPolicy(state, type);
            Image row = UiTheme.MakeImage(parent, GovCardSoft, "policy_" + type);
            PlaceTopLeft(row.rectTransform, 16f, y, parent.sizeDelta.x - 32f, 78f);
            float width = row.rectTransform.sizeDelta.x;
            GovText(row.rectTransform, label, 18, UiTheme.TextMain, 14f, 4f, width - 276f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(row.rectTransform, note, 13, UiTheme.TextSub, 14f, 32f, width - 276f, 35f, TextAnchor.UpperLeft);
            Button impact = UiTheme.MakeButton(row.transform, "影响", () => ShowPolicyImpact(type), 15);
            PlaceTopLeft(impact.rectTransform(), width - 258f, 17f, 64f, 44f);
            UiTheme.SetButtonStyle(impact, GovTeal, Color.white);
            Button minus = UiTheme.MakeButton(row.transform, "−", () => AdjustGovernmentPolicy(type, -policy.step, tab), 22);
            PlaceTopLeft(minus.rectTransform(), width - 186f, 17f, 40f, 44f);
            GovText(row.rectTransform, GovernmentSimulator.FormatPolicyValue(policy), 17, UiTheme.Accent,
                width - 142f, 17f, 92f, 44f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Button plus = UiTheme.MakeButton(row.transform, "+", () => AdjustGovernmentPolicy(type, policy.step, tab), 22);
            PlaceTopLeft(plus.rectTransform(), width - 46f, 17f, 40f, 44f);
            minus.interactable = policy.value > policy.minimum;
            plus.interactable = policy.value < policy.maximum;
        }

        void GovProjectRow(RectTransform parent, GameState state, PublicProjectState project, float y)
        {
            Image row = UiTheme.MakeImage(parent, GovCardSoft, "project_" + project.type);
            PlaceTopLeft(row.rectTransform, 16f, y, parent.sizeDelta.x - 32f, 64f);
            float width = row.rectTransform.sizeDelta.x;
            GovText(row.rectTransform, project.name, 18, UiTheme.TextMain, 14f, 3f, 210f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(row.rectTransform, "建设 ¥" + project.buildCost.ToString("0") + " · 维护 ¥" + project.maintenanceCost.ToString("0") + "/周期",
                13, UiTheme.TextSub, 14f, 31f, 310f, 22f);
            GovText(row.rectTransform, "服务 +" + project.serviceGain.ToString("0") + "  幸福 +" + project.happinessGain.ToString("0"),
                15, GovTeal, 330f, 11f, 190f, 36f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Button build = UiTheme.MakeButton(row.transform, project.built ? "已建成" : "拨款建设", () =>
            {
                if (GovernmentSimulator.BuildProject(state, project.type))
                {
                    MapBuilder.RefreshGovernmentProjects(state);
                    ReloadGovernment(5);
                }
                else ShowToast("政府资金不足，暂时无法建设「" + project.name + "」");
            }, 16);
            PlaceTopLeft(build.rectTransform(), width - 142f, 10f, 128f, 44f);
            // bug搜#5复审:与 BuildProject 守卫同口径——当日已挂账的资本支出(拨款只挂账,收日才扣
            // 现金)同样占用资金,只看 cash 会出现「按钮可按、点下去却弹资金不足」的自相矛盾。
            build.interactable = !project.built &&
                state.government.cash >= project.buildCost + state.government.pendingCapitalSpending;
            if (project.built) UiTheme.SetButtonStyle(build, UiTheme.Good, UiTheme.Ink);
        }

        void GovLedgerRow(RectTransform parent, string label, float amount, float y, bool income, bool total = false)
        {
            GovText(parent, label, total ? 18 : 16, total ? UiTheme.TextMain : UiTheme.TextSub,
                18f, y, parent.sizeDelta.x - 180f, 32f, TextAnchor.MiddleLeft, total ? FontStyle.Bold : FontStyle.Normal);
            string sign = income ? "+" : "−";
            if (amount < 0f) sign = "−";
            GovText(parent, sign + " ¥ " + Mathf.Abs(amount).ToString("0"), total ? 20 : 17,
                income ? UiTheme.Good : UiTheme.Bad, parent.sizeDelta.x - 160f, y, 142f, 32f,
                TextAnchor.MiddleRight, FontStyle.Bold);
        }

        void GovLedgerRule(RectTransform parent, float y)
        {
            Image rule = UiTheme.MakeImage(parent, new Color32(0x9c, 0x87, 0x6b, 100), "ledgerRule");
            PlaceTopLeft(rule.rectTransform, 18f, y, parent.sizeDelta.x - 36f, 1f);
        }

        void BuildCausalTimeline(RectTransform parent, PolicyImpactRecord latest)
        {
            string[] titles = { "立即生效", "主体调整", "长期结果" };
            string[] empty = { "选择政策", "观察价格/产量", "比较城市数据" };
            for (int i = 0; i < 3; i++)
            {
                float x = 18f + i * 194f;
                Color color = latest != null && i < latest.currentStage ? UiTheme.Accent : new Color32(0x76, 0x68, 0x59, 255);
                Image dot = UiTheme.MakeImage(parent, color, "stageDot");
                PlaceTopLeft(dot.rectTransform, x, 75f, 18f, 18f);
                if (i < 2)
                {
                    Image line = UiTheme.MakeImage(parent, new Color32(0xa5, 0x8d, 0x70, 150), "stageLine");
                    PlaceTopLeft(line.rectTransform, x + 18f, 83f, 176f, 2f);
                }
                GovText(parent, titles[i], 16, color, x, 100f, 176f, 26f, TextAnchor.MiddleLeft, FontStyle.Bold);
                string detail = latest == null ? empty[i] : i == 0 ? latest.immediateEffect : i == 1 ? "价格、消费、产量与招聘开始响应。" : latest.delayedEffect;
                GovText(parent, detail, 13, UiTheme.TextSub, x, 130f, 176f, 58f, TextAnchor.UpperLeft);
            }
        }

        void GovOutcomeBlock(RectTransform parent, string title, string body, float x, float y, Color color)
        {
            Image marker = UiTheme.MakeImage(parent, color, "outcomeMarker");
            PlaceTopLeft(marker.rectTransform, x, y, 6f, 70f);
            GovText(parent, title, 17, color, x + 18f, y - 2f, 160f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold);
            GovText(parent, body, 16, UiTheme.TextSub, x + 18f, y + 28f, parent.sizeDelta.x - x - 54f, 45f, TextAnchor.UpperLeft);
        }

        void GovDataRow(RectTransform parent, string label, float value, string suffix, float y, Color color)
        {
            GovText(parent, label, 16, UiTheme.TextMain, 18f, y, 122f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            Image track = UiTheme.MakeImage(parent, new Color32(0x28, 0x25, 0x21, 255), "dataTrack");
            PlaceTopLeft(track.rectTransform, 146f, y + 8f, 334f, 14f);
            Image fill = UiTheme.MakeImage(track.transform, color, "dataFill");
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / 100f), 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            GovText(parent, value.ToString("0") + suffix, 16, color, 492f, y, 68f, 30f, TextAnchor.MiddleRight, FontStyle.Bold);
        }

        void AddGovernmentGraph(RectTransform parent, GameState state, GovernmentGraphMode mode, float x, float y, float width, float height)
        {
            GameObject go = new GameObject("policyGraph_" + mode, typeof(RectTransform), typeof(CanvasRenderer), typeof(GovernmentPolicyGraph));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            PlaceTopLeft(rt, x, y, width, height);
            GovernmentPolicyGraph graph = go.GetComponent<GovernmentPolicyGraph>();
            graph.raycastTarget = false;
            graph.Configure(state, mode);
        }

        void AdjustGovernmentPolicy(PolicyType type, float delta, int tab)
        {
            GameState state = GameState.I;
            PolicyState policy = GovernmentSimulator.FindPolicy(state, type);
            if (policy == null) return;
            string conflict = GovernmentSimulator.PolicyConflictReason(state, type, policy.value + delta);
            if (!string.IsNullOrEmpty(conflict))
            {
                ShowToast(conflict);
                return;
            }
            if (GovernmentSimulator.SetPolicy(state, type, policy.value + delta))
            {
                ShowToast(policy.name + "已调整为 " + GovernmentSimulator.FormatPolicyValue(policy));
                ReloadGovernment(tab);
            }
        }

        void ReloadGovernment(int tab)
        {
            CloseTop();
            ShowGovernmentDashboard(tab);
        }

        void AdvanceGovernmentCycle(int tab)
        {
            GameState state = GameState.I;
            GovernmentEventState current = state.government.currentEvent;
            if (current != null && !current.resolved)
            {
                ShowToast("先处理当前城市事件，再结束政策周期");
                ShowGovernmentEvent();
                return;
            }
            // 先关自己、再结算(照抄 UiReports):TryNextDay 内部会弹出终局总评/竞争介绍/价格战等
            // 关键面板,若结算后再 CloseTop 会把它们当场错杀——终局画面永久消失、旧面板残栈还会
            // 让"昨天的结算按钮"复活造成二次跳日(P1-1)。
            CloseTop();
            if (GameBootstrap.I.TryNextDay())
            {
                GovernmentCycleRecord record = state.government.history.Count > 0
                    ? state.government.history[state.government.history.Count - 1] : null;
                if (state.phase != GamePhase.Ended) ShowGovernmentDashboard(tab);
                if (record != null) ShowToast(record.summary);
            }
        }

        public void ShowPolicyImpact(PolicyType type)
        {
            GameState state = GameState.I;
            GovernmentSimulator.Ensure(state);
            PolicyImpactRecord impact = null;
            for (int i = state.government.impacts.Count - 1; i >= 0; i--)
                if (state.government.impacts[i].type == type) { impact = state.government.impacts[i]; break; }
            PolicyState policy = GovernmentSimulator.FindPolicy(state, type);
            GameObject root = BeginPanel((policy == null ? "政策" : policy.name) + " · 影响观察", 900f, 680f);
            RectTransform content = ContentOf(root);
            if (impact == null)
            {
                GovText(content, "这项政策尚未被调整。先在市政府面板改变一次数值，系统才会建立政策前后的观察记录。",
                    21, UiTheme.TextMain, 24f, 42f, 804f, 100f, TextAnchor.MiddleCenter);
                Button back = UiTheme.MakeButton(content, "回到政策设置", CloseTop, 19);
                PlaceTopLeft(back.rectTransform(), 294f, 190f, 264f, 52f);
                return;
            }

            GovText(content, "第 " + impact.cycleEnacted + " 周期调整  ·  当前处于第 " + impact.currentStage + " 阶段",
                17, GovTeal, 0f, 0f, 852f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform timeline = GovCardPanel(content, "政策滞后", impact.observedSummary, 0f, 42f, 852f, 210f);
            BuildCausalTimeline(timeline, impact);
            RectTransform groups = GovCardPanel(content, "分配结果", "总量改善不代表每个群体都受益", 0f, 270f, 412f, 280f);
            GovOutcomeBlock(groups, "主要受益者", impact.winners, 18f, 66f, UiTheme.Good);
            GovOutcomeBlock(groups, "主要承担者", impact.losers, 18f, 158f, UiTheme.Bad);
            RectTransform causes = GovCardPanel(content, "完整因果链", "政府作用也必须计入成本", 432f, 270f, 420f, 280f);
            GovText(causes, impact.causalChain, 16, UiTheme.TextMain, 18f, 62f, 384f, 108f, TextAnchor.UpperLeft);
            GovText(causes, "政府侧：" + impact.governmentEffect, 16, UiTheme.TextSub, 18f, 184f, 384f, 58f, TextAnchor.UpperLeft);
            Button close = UiTheme.MakeButton(content, "继续观察城市数据", CloseTop, 19);
            PlaceTopLeft(close.rectTransform(), 290f, 566f, 272f, 48f);
            UiTheme.SetButtonStyle(close, GovTeal, Color.white);
        }

        public void ShowGovernmentEvent()
        {
            GameState state = GameState.I;
            GovernmentSimulator.Ensure(state);
            GovernmentEventState cityEvent = state.government.currentEvent;
            if (cityEvent == null || cityEvent.resolved)
            {
                ShowToast("当前没有待处理的城市事件");
                return;
            }
            SaveSystem.SaveKeyMoment(state, "关键选择：城市事件 " + cityEvent.title);
            GameObject root = BeginPanel("城市事件 · " + cityEvent.title, 760f, 560f);
            root.name = "panel_government_event";
            RectTransform content = ContentOf(root);
            GovText(content, cityEvent.description, 21, UiTheme.TextMain, 24f, 22f, 664f, 86f, TextAnchor.MiddleCenter);
            GovText(content, "没有单一正确答案。选择你愿意承担的代价。", 16, GovTeal, 24f, 112f, 664f, 30f, TextAnchor.MiddleCenter, FontStyle.Bold);
            string[] options = { cityEvent.optionA, cityEvent.optionB, cityEvent.optionC };
            for (int i = 0; i < options.Length; i++)
            {
                int choice = i;
                Button option = UiTheme.MakeButton(content, options[i], () => ResolveGovernmentEvent(choice), 19);
                PlaceTopLeft(option.rectTransform(), 74f, 166f + i * 86f, 564f, 64f);
                if (i == 1) UiTheme.SetButtonStyle(option, GovTeal, Color.white);
            }
        }

        void ResolveGovernmentEvent(int option)
        {
            GameState state = GameState.I;
            string title = state.government.currentEvent == null ? "城市事件" : state.government.currentEvent.title;
            if (!GovernmentSimulator.ResolveEvent(state, option)) return;
            // 选项打开前已经保存关键节点；选择结果随下一次 WorldDayStart 自动存档。
            CloseTop();
            ShowToast("已处理「" + title + "」；结果会继续传导到后续周期");
            if (InputBlocked)
            {
                CloseTop();
                ShowGovernmentDashboard(0);
            }
        }

        public void ShowMayorTermReport()
        {
            GameState state = GameState.I;
            GovernmentSimulator.Ensure(state);
            GovernmentState g = state.government;
            if (!g.termReportReady)
            {
                ShowToast("完成第 7 章并积累至少 7 个政策周期后生成任期报告");
                return;
            }
            GameObject root = BeginPanel("市长任期报告 · " + GovernmentSimulator.TermStyle(g), 920f, 700f);
            RectTransform content = ContentOf(root);
            GovText(content, "这不是好坏判决，而是你反复选择后形成的治理风格。", 18, GovTeal,
                0f, 0f, 872f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovReportGrade(content, "经济活力", GovernmentSimulator.Grade(g.enterpriseConfidence), g.enterpriseConfidence, 0f, UiTheme.Accent);
            GovReportGrade(content, "居民福祉", GovernmentSimulator.Grade(g.residentHappiness), g.residentHappiness, 218f, GovTeal);
            GovReportGrade(content, "环境质量", GovernmentSimulator.Grade(g.environmentQuality), g.environmentQuality, 436f, UiTheme.Good);
            float fiscal = GovernmentSimulator.FiscalHealth(g);
            GovReportGrade(content, "财政健康", GovernmentSimulator.Grade(fiscal), fiscal, 654f, new Color32(0xd7, 0x9a, 0xc9, 255));
            RectTransform story = GovCardPanel(content, "你的政策画像", "成绩来自取舍，没有一种风格能同时最大化所有目标", 0f, 190f, 872f, 312f);
            GovText(story, "任期内记录 " + g.history.Count + " 个政策周期，当前债务 ¥" + g.debt.ToString("0") +
                "，支持率 " + g.approvalRating.ToString("0") + "% 。", 20, UiTheme.TextMain,
                24f, 70f, 824f, 42f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovOutcomeBlock(story, "你守住的目标", BestGovernmentOutcome(g), 24f, 132f, UiTheme.Good);
            GovOutcomeBlock(story, "仍需面对的代价", WeakestGovernmentOutcome(g), 24f, 218f, UiTheme.Bad);
            Button back = UiTheme.MakeButton(content, "进入自由治理", () =>
            {
                g.termReportViewed = true;
                state.Changed();
                CloseTop();
            }, 19);
            PlaceTopLeft(back.rectTransform(), 304f, 526f, 264f, 50f);
        }

        void GovReportGrade(RectTransform parent, string label, string grade, float score, float x, Color color)
        {
            Image card = UiTheme.MakeImage(parent, GovCard, "grade_" + label);
            PlaceTopLeft(card.rectTransform, x, 54f, 200f, 116f);
            GovText(card.rectTransform, label, 15, UiTheme.TextSub, 12f, 8f, 176f, 24f, TextAnchor.MiddleCenter);
            GovText(card.rectTransform, grade, 34, color, 12f, 31f, 176f, 48f, TextAnchor.MiddleCenter, FontStyle.Bold);
            GovText(card.rectTransform, score.ToString("0") + " / 100", 14, UiTheme.TextMain, 12f, 83f, 176f, 22f, TextAnchor.MiddleCenter);
        }

        static string BestGovernmentOutcome(GovernmentState g)
        {
            float best = g.enterpriseConfidence;
            string result = "经济活力与企业信心";
            if (g.residentHappiness > best) { best = g.residentHappiness; result = "居民福祉与生活满意"; }
            if (g.environmentQuality > best) { best = g.environmentQuality; result = "环境治理与长期质量"; }
            if (g.publicServiceLevel > best) result = "公共服务与城市可达性";
            return result + "成为本届政府最突出的成果。";
        }

        static string WeakestGovernmentOutcome(GovernmentState g)
        {
            float fiscal = GovernmentSimulator.FiscalHealth(g);
            float weakest = g.enterpriseConfidence;
            string result = "企业仍在适应政策成本与市场变化。";
            if (g.residentHappiness < weakest) { weakest = g.residentHappiness; result = "部分居民仍未充分分享到城市发展。"; }
            if (g.environmentQuality < weakest) { weakest = g.environmentQuality; result = "污染外部性仍未被完全内部化。"; }
            if (fiscal < weakest) result = "持续支出与债务利息压缩了未来财政空间。";
            return result;
        }
    }
}
