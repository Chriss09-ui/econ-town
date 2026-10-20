using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.5 宏观经济中心：指标、实验、因果解释与财政决策。</summary>
    public partial class UIManager
    {
        static readonly Color MacroNavy = new Color32(0x20, 0x2d, 0x32, 255);
        static readonly Color MacroCard = new Color32(0x2b, 0x3b, 0x40, 255);
        static readonly Color MacroCardSoft = new Color32(0x34, 0x46, 0x4a, 255);
        static readonly Color MacroTeal = new Color32(0x5d, 0xc3, 0xb6, 255);
        static readonly Color MacroAmber = new Color32(0xf2, 0xa9, 0x3b, 255);
        static readonly Color MacroPaper = new Color32(0xe9, 0xd4, 0xa8, 255);
        static readonly Color MacroCoral = new Color32(0xdd, 0x6b, 0x5a, 255);

        public void ShowMacroDashboard(int selectedTab = 0)
        {
            GameState state = GameState.I;
            if (state == null) return;
            MacroEconomySimulator.Ensure(state);
            selectedTab = Mathf.Clamp(selectedTab, 0, 5);
            MacroEconomyState m = state.macro;
            CaptureMacroLearningAction(state, selectedTab);

            GameObject root = BeginPanel("国家经济运行中心", 1260f, 840f);
            root.name = "panel_macro_dashboard";
            Image panelImage = root.GetComponent<Image>();
            if (panelImage != null) panelImage.color = MacroNavy;
            RectTransform content = ContentOf(root);

            ChapterDefinition macroChapter = ProgressionService.Current(state);
            string macroHeadline = macroChapter != null && macroChapter.part == LearningPart.Macro
                ? "第 " + macroChapter.number + " 章 · " + macroChapter.title
                : state.progression.mainlineComplete || state.progression.completedChapter >= 30
                    ? "宏观经济 · 可以继续观察" : "宏观经济";
            MacroText(content, macroHeadline, 18, MacroTeal, 0f, 0f, 470f, 32f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(content, "第 " + m.period + " 期 · " + MacroEconomySimulator.CycleName(m.cyclePhase), 16,
                UiTheme.TextSub, 480f, 0f, 260f, 32f, TextAnchor.MiddleCenter);
            Button news = UiTheme.MakeButton(content, "经济新闻", ShowMacroNews, 17);
            MacroPlace(news.rectTransform(), 756f, 0f, 142f, 34f);
            UiTheme.SetButtonStyle(news, MacroCardSoft, UiTheme.TextMain);
            Button report = UiTheme.MakeButton(content, "国家报告", ShowNationalReport, 17);
            MacroPlace(report.rectTransform(), 908f, 0f, 142f, 34f);
            UiTheme.SetButtonStyle(report, MacroCardSoft, UiTheme.TextMain);
            int tabCapture = selectedTab;
            Button advance = UiTheme.MakeButton(content, "统一结算本日", () => AdvanceMacroPeriod(tabCapture), 18);
            MacroPlace(advance.rectTransform(), 1060f, 0f, 152f, 34f);
            UiTheme.SetButtonStyle(advance, MacroAmber, UiTheme.Ink);

            MacroMetric(content, "实际 GDP", "¥ " + m.realGdp.ToString("0"), 0f, m.gdpGrowth >= 0f ? MacroTeal : MacroCoral);
            MacroMetric(content, "实际增长", MacroSigned(m.gdpGrowth) + "%", 244f, m.gdpGrowth >= 0f ? MacroTeal : MacroCoral);
            MacroMetric(content, "通胀率", MacroSigned(m.inflation) + "%", 488f, Mathf.Abs(m.inflation) <= 3f ? MacroTeal : MacroCoral);
            MacroMetric(content, "失业率", m.unemploymentRate.ToString("0.0") + "%", 732f, m.unemploymentRate <= 7f ? MacroTeal : MacroCoral);
            MacroMetric(content, "产出缺口", MacroSigned(m.outputGap) + "%", 976f, Mathf.Abs(m.outputGap) <= 3f ? MacroTeal : MacroAmber);

            string[] tabs = { "国民经济", "物价与通胀", "劳动就业", "总需求 / 总供给", "财政政策", "住房市场" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button button = UiTheme.MakeButton(content, tabs[i], () => ReloadMacro(tab), 17);
                button.name = "macroTab_" + i;
                MacroPlace(button.rectTransform(), i * 202f, 119f, 192f, 42f);
                UiTheme.SetButtonStyle(button, i == selectedTab ? MacroTeal : MacroPaper,
                    i == selectedTab ? MacroNavy : UiTheme.Ink);
                button.interactable = i != selectedTab;
            }

            if (selectedTab == 0) BuildNationalEconomyTab(content, state);
            else if (selectedTab == 1) BuildPricesTab(content, state);
            else if (selectedTab == 2) BuildEmploymentTab(content, state);
            else if (selectedTab == 3) BuildAdAsTab(content, state);
            else if (selectedTab == 4) BuildMacroFiscalTab(content, state);
            else BuildHousingMarketTab(content, state);
        }

        void BuildNationalEconomyTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            RectTransform equation = MacroCardPanel(content, "GDP 支出构成", "四项来自企业、居民与政府的真实账本", 0f, 178f, 580f, 286f);
            string formula = "<b><color=#5DC3B6>C " + m.consumption.ToString("0") + "</color></b>  +  " +
                "<b><color=#F2A93B>I " + m.investment.ToString("0") + "</color></b>  +  " +
                "<b><color=#78A9E6>G " + m.governmentSpending.ToString("0") + "</color></b>  +  " +
                "<b><color=#E9D4A8>NX " + MacroSignedMoney(m.netExports) + "</color></b>";
            MacroText(equation, formula, 21, UiTheme.TextMain, 18f, 62f, 544f, 46f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroText(equation, "=  名义 GDP  ¥ " + m.nominalGdp.ToString("0"), 25, MacroAmber,
                18f, 112f, 544f, 42f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroText(equation, "基期价格折算后：实际 GDP ¥ " + m.realGdp.ToString("0") +
                "  ·  人均 ¥ " + m.perCapitaGdp.ToString("0.0"), 17, UiTheme.TextSub,
                18f, 165f, 544f, 36f, TextAnchor.MiddleCenter);
            Button valueAdded = UiTheme.MakeButton(equation, "为什么 2 + 5 + 10 不能算成 17？", () => ShowCardDetail("valueadded"), 17);
            MacroPlace(valueAdded.rectTransform(), 94f, 215f, 392f, 42f);
            UiTheme.SetButtonStyle(valueAdded, MacroCardSoft, UiTheme.TextMain);

            RectTransform graphCard = MacroCardPanel(content, "GDP 构成图", "柱高按本期最大构成项归一化", 596f, 178f, 616f, 286f);
            AddMacroGraph(graphCard, state, MacroGraphMode.GdpComponents, 16f, 55f, 584f, 190f);
            MacroText(graphCard, "C 消费       I 投资       G 政府购买       NX 净出口", 16, UiTheme.TextSub,
                36f, 237f, 544f, 28f, TextAnchor.MiddleCenter);

            RectTransform chain = MacroCardPanel(content, "这一期为什么会变化？", "先看因果链，再下钻到产生数据的主体", 0f, 480f, 1212f, 174f);
            MacroText(chain, m.latestCausalChain, 19, UiTheme.TextMain, 20f, 58f, 1172f, 50f, TextAnchor.UpperLeft);
            Button resident = UiTheme.MakeButton(chain, "查看居民消费", () => { CloseTop(); ShowResidentHome(); }, 17);
            Button enterprise = UiTheme.MakeButton(chain, "查看企业销售", () => { CloseTop(); ShowMarketDashboard(); }, 17);
            Button government = UiTheme.MakeButton(chain, "查看政府账本", () => { CloseTop(); ShowGovernmentDashboard(); }, 17);
            MacroPlace(resident.rectTransform(), 20f, 114f, 220f, 40f);
            MacroPlace(enterprise.rectTransform(), 250f, 114f, 220f, 40f);
            MacroPlace(government.rectTransform(), 480f, 114f, 220f, 40f);
            UiTheme.SetButtonStyle(resident, MacroTeal, MacroNavy);
            UiTheme.SetButtonStyle(enterprise, MacroAmber, UiTheme.Ink);
            UiTheme.SetButtonStyle(government, MacroPaper, UiTheme.Ink);
            MacroText(chain, "消费者信心 " + m.consumerConfidence.ToString("0") + "  ·  企业信心 " + m.businessConfidence.ToString("0") +
                "\n只计最终商品和服务；福利与债务利息不直接计入 G。", 16,
                UiTheme.TextSub, 726f, 105f, 466f, 54f, TextAnchor.MiddleRight);
        }

        void BuildPricesTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            RectTransform basket = MacroCardPanel(content, "CPI 固定消费篮子", "固定权重防止单项商品霸占总体物价", 0f, 178f, 580f, 304f);
            PriceRow(basket, "食品", 30, m.foodPriceIndex, 62f);
            PriceRow(basket, "住房", 30, m.housingPriceIndex, 99f);
            PriceRow(basket, "交通", 15, m.transportPriceIndex, 136f);
            PriceRow(basket, "饮品", 5, m.beveragePriceIndex, 173f);
            PriceRow(basket, "娱乐", 10, m.entertainmentPriceIndex, 210f);
            PriceRow(basket, "其他", 10, m.otherPriceIndex, 247f);

            RectTransform trend = MacroCardPanel(content, "价格趋势", "CPI " + m.cpi.ToString("0.0") + "  ·  本期通胀 " + MacroSigned(m.inflation) + "%", 596f, 178f, 616f, 304f);
            AddMacroGraph(trend, state, MacroGraphMode.PriceTrend, 16f, 57f, 584f, 205f);
            MacroText(trend, "青线：CPI  ·  横轴：最近 10 期", 16, UiTheme.TextSub, 40f, 259f, 530f, 28f, TextAnchor.MiddleCenter);

            RectTransform note = MacroCardPanel(content, "先判断是哪一种通胀", "原因不同，政策代价也不同", 0f, 498f, 1212f, 156f);
            string kind = m.energyCostIndex > 112f ? "成本推动：能源和运输成本正在把总供给向左推。" :
                m.outputGap > 2f ? "需求拉动：实际产出高于潜在水平，总需求偏热。" : "总体物价暂未显示明显的持续压力。";
            MacroText(note, kind, 20, m.energyCostIndex > 112f ? MacroCoral : MacroTeal, 20f, 58f, 770f, 40f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(note, "果汁价格指数 " + m.beveragePriceIndex.ToString("0") + " 只占 CPI 的 5%。单项上涨不等于所有生活成本都同幅上涨。",
                17, UiTheme.TextSub, 20f, 101f, 770f, 36f, TextAnchor.MiddleLeft);
            Button concept = UiTheme.MakeButton(note, "查看 CPI 知识卡", () => ShowCardDetail("cpi"), 17);
            MacroPlace(concept.rectTransform(), 920f, 80f, 252f, 44f);
            UiTheme.SetButtonStyle(concept, MacroPaper, UiTheme.Ink);
        }

        void BuildEmploymentTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            RectTransform accounting = MacroCardPanel(content, "劳动力人口怎么计算", "没有主动找工作的人不算失业", 0f, 178f, 580f, 304f);
            LaborRow(accounting, "总人口", state.government.population, 62f, UiTheme.TextMain);
            LaborRow(accounting, "工作年龄人口", m.workingAgePopulation, 103f, UiTheme.TextMain);
            LaborRow(accounting, "劳动力人口", m.laborForce, 144f, MacroPaper);
            LaborRow(accounting, "就业", m.employment, 185f, MacroTeal);
            LaborRow(accounting, "失业", m.unemployment, 226f, MacroCoral);
            MacroText(accounting, "失业率 = " + m.unemployment + " ÷ " + m.laborForce + " = " + m.unemploymentRate.ToString("0.0") + "%",
                18, MacroAmber, 20f, 264f, 540f, 30f, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform graph = MacroCardPanel(content, "劳动力结构", "就业 / 摩擦性 / 结构性 / 周期性失业", 596f, 178f, 616f, 304f);
            AddMacroGraph(graph, state, MacroGraphMode.Employment, 16f, 60f, 584f, 164f);
            MacroText(graph, "<color=#5DC3B6>就业 " + m.employment + "</color>   <color=#F2A93B>摩擦 " + m.frictionalUnemployment +
                "</color>   <color=#78A9E6>结构 " + m.structuralUnemployment + "</color>   <color=#DD6B5A>周期 " + m.cyclicalUnemployment + "</color>",
                16, UiTheme.TextSub, 24f, 237f, 568f, 42f, TextAnchor.MiddleCenter);

            RectTransform chain = MacroCardPanel(content, "微观行为怎样变成失业？", "这条链会反向影响下一期居民消费", 0f, 498f, 1212f, 156f);
            MacroText(chain, "消费减少 → 企业销售下降 → 减产与少招聘 → 周期性失业上升 → 居民收入下降 → 消费再次走弱",
                19, UiTheme.TextMain, 22f, 62f, 1168f, 42f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroText(chain, "当前就业反馈已经接入果汁需求：失业上升时，下一营业日需求会随收入和信心下降。",
                17, UiTheme.TextSub, 22f, 107f, 1168f, 30f, TextAnchor.MiddleCenter);
        }

        void BuildAdAsTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            AdAsGeometry geometry = MacroEconomyGraph.CalculateAdAsGeometry(m);
            RectTransform graph = MacroCardPanel(content, "AD–AS 总体经济图", "青线是总需求，黄线是总供给", 0f, 178f, 720f, 476f);
            AddMacroGraph(graph, state, MacroGraphMode.AggregateDemandSupply, 20f, 64f, 680f, 310f);
            MacroText(graph, "价格水平 CPI", 14, UiTheme.TextSub,
                20f, 58f, 128f, 24f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(graph, geometry.priceMax.ToString("0"), 13, UiTheme.TextSub,
                22f, 79f, 46f, 22f, TextAnchor.MiddleRight);
            MacroText(graph, geometry.equilibriumPrice.ToString("0"), 13, MacroPaper,
                22f, 210f, 46f, 22f, TextAnchor.MiddleRight, FontStyle.Bold);
            MacroText(graph, geometry.priceMin.ToString("0"), 13, UiTheme.TextSub,
                22f, 337f, 46f, 22f, TextAnchor.MiddleRight);
            MacroText(graph, geometry.outputMin.ToString("0"), 13, UiTheme.TextSub,
                54f, 350f, 76f, 22f, TextAnchor.MiddleLeft);
            MacroText(graph, ((geometry.outputMin + geometry.outputMax) * .5f).ToString("0"), 13, UiTheme.TextSub,
                326f, 350f, 78f, 22f, TextAnchor.MiddleCenter);
            MacroText(graph, geometry.outputMax.ToString("0"), 13, UiTheme.TextSub,
                620f, 350f, 66f, 22f, TextAnchor.MiddleRight);
            MacroText(graph, "实际产出 Q（¥）", 14, UiTheme.TextSub,
                520f, 327f, 164f, 24f, TextAnchor.MiddleRight, FontStyle.Bold);
            MacroText(graph, "<color=#5DC3B6>AD 总需求</color>   <color=#F2A93B>SRAS 短期总供给</color>   <color=#78A9E6>蓝线：潜在产出</color>   白点：当前均衡",
                16, UiTheme.TextSub, 42f, 382f, 636f, 32f, TextAnchor.MiddleCenter);
            MacroText(graph, "均衡产出 ¥" + geometry.equilibriumOutput.ToString("0") + "  ·  CPI " +
                geometry.equilibriumPrice.ToString("0.0") + "  ·  潜在产出 ¥" + geometry.potentialOutput.ToString("0") +
                "  ·  当前供给能力 ¥" + geometry.aggregateSupplyOutput.ToString("0"),
                16, UiTheme.TextMain, 24f, 420f, 672f, 38f, TextAnchor.MiddleCenter);

            RectTransform shocks = MacroCardPanel(content, "冲击实验", "选冲击，再运行下一期", 736f, 178f, 476f, 476f);
            ShockButton(shocks, state, MacroShockType.ConsumptionBoom, "消费热潮", 20f, 64f);
            ShockButton(shocks, state, MacroShockType.InvestmentBoom, "投资繁荣", 244f, 64f);
            ShockButton(shocks, state, MacroShockType.EnergyCrisis, "能源危机", 20f, 116f);
            ShockButton(shocks, state, MacroShockType.GlobalDemandFall, "海外需求下降", 244f, 116f);
            ShockButton(shocks, state, MacroShockType.AiRevolution, "AI 革命", 20f, 168f);
            ShockButton(shocks, state, MacroShockType.Disaster, "自然灾害", 244f, 168f);
            ShockButton(shocks, state, MacroShockType.PopulationGrowth, "人口增长", 20f, 220f);
            MacroText(shocks, "待发生：" + MacroShockName(m.pendingShock), 18,
                m.pendingShock == MacroShockType.None ? UiTheme.TextSub : MacroAmber,
                20f, 280f, 436f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroText(shocks, m.pendingShock == MacroShockType.EnergyCrisis
                ? "供给冲击会造成滞胀：产出下降、失业上升、通胀上升。"
                : "需求冲击先改变支出；供给冲击先改变成本与生产能力。",
                17, UiTheme.TextMain, 24f, 326f, 428f, 76f, TextAnchor.UpperLeft);
            Button why = UiTheme.MakeButton(shocks, "查看冲击概念", () => ShowCardDetail(m.pendingShock == MacroShockType.EnergyCrisis ? "supplyshock" : "demandshock"), 17);
            MacroPlace(why.rectTransform(), 112f, 414f, 252f, 40f);
            UiTheme.SetButtonStyle(why, MacroPaper, UiTheme.Ink);
        }

        void BuildMacroFiscalTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            RectTransform controls = MacroCardPanel(content, "财政立场", "先判断周期，再接受政策代价", 0f, 178f, 580f, 304f);
            Button expansion = UiTheme.MakeButton(controls, "扩张：增加购买", () => SetMacroFiscal(FiscalStance.Expansionary), 17);
            Button neutral = UiTheme.MakeButton(controls, "中性：自动稳定", () => SetMacroFiscal(FiscalStance.Neutral), 17);
            Button contraction = UiTheme.MakeButton(controls, "紧缩：需求降温", () => SetMacroFiscal(FiscalStance.Contractionary), 17);
            MacroPlace(expansion.rectTransform(), 20f, 64f, 170f, 46f);
            MacroPlace(neutral.rectTransform(), 202f, 64f, 170f, 46f);
            MacroPlace(contraction.rectTransform(), 384f, 64f, 170f, 46f);
            UiTheme.SetButtonStyle(expansion, m.fiscalStance == FiscalStance.Expansionary ? MacroTeal : MacroPaper, UiTheme.Ink);
            UiTheme.SetButtonStyle(neutral, m.fiscalStance == FiscalStance.Neutral ? MacroTeal : MacroPaper, UiTheme.Ink);
            UiTheme.SetButtonStyle(contraction, m.fiscalStance == FiscalStance.Contractionary ? MacroTeal : MacroPaper, UiTheme.Ink);
            MacroText(controls, "公共投资计划", 17, UiTheme.TextSub, 22f, 132f, 240f, 34f, TextAnchor.MiddleLeft);
            Button minus = UiTheme.MakeButton(controls, "−", () => ChangeMacroInvestment(-250f), 24);
            Button plus = UiTheme.MakeButton(controls, "+", () => ChangeMacroInvestment(250f), 24);
            MacroPlace(minus.rectTransform(), 278f, 129f, 48f, 40f);
            MacroText(controls, "¥ " + m.plannedPublicInvestment.ToString("0"), 21, MacroAmber, 330f, 129f, 142f, 40f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroPlace(plus.rectTransform(), 482f, 129f, 48f, 40f);
            UiTheme.SetButtonStyle(minus, MacroCardSoft, UiTheme.TextMain);
            UiTheme.SetButtonStyle(plus, MacroCardSoft, UiTheme.TextMain);
            MacroText(controls, "MPC " + m.marginalPropensityToConsume.ToString("0.00") + "  ·  简单乘数 " + m.fiscalMultiplier.ToString("0.00"), 17,
                UiTheme.TextMain, 22f, 190f, 532f, 34f, TextAnchor.MiddleCenter);
            MacroText(controls, "传导阶段：" + MacroFiscalLagName(m.fiscalLagStage), 17, UiTheme.TextSub,
                22f, 238f, 532f, 32f, TextAnchor.MiddleCenter);

            RectTransform preview = MacroCardPanel(content, "政策预览", "只说明方向与风险，不假装给出精确答案", 596f, 178f, 616f, 304f);
            MacroText(preview, m.policyPreview, 19, UiTheme.TextMain, 22f, 66f, 572f, 112f, TextAnchor.UpperLeft);
            MacroText(preview, "自动稳定器：产出缺口越负，税收自然下降、福利自然增加；不需要再次点击。",
                17, MacroTeal, 22f, 190f, 572f, 54f, TextAnchor.UpperLeft);
            MacroText(preview, "赤字 / GDP  " + m.deficitToGdp.ToString("0.0") + "%     债务 / GDP  " + m.debtToGdp.ToString("0.0") + "%",
                18, MacroAmber, 22f, 252f, 572f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform history = MacroCardPanel(content, "增长与债务轨迹", "青线：实际增长  ·  红线：债务 / GDP", 0f, 498f, 1212f, 156f);
            AddMacroGraph(history, state, MacroGraphMode.FiscalHistory, 24f, 52f, 1164f, 86f);
        }

        // —— V0.9.2《房价是一头兽》+V0.9.3《杠杆周期》:住房市场页签(PRD §194.7/§195.2/§196.6)。 ——
        // 2×2 布局(面板 content 无滚动,溢出会画出框外,总深锁定 654):上排房价/房贷,下排名义实际/泡沫仪表。
        void BuildHousingMarketTab(RectTransform content, GameState state)
        {
            MacroEconomyState m = state.macro;
            HousingService.Ensure(state);
            HousingState h = state.housing;

            RectTransform market = MacroCardPanel(content, "房价与租金", "青线：房价指数  ·  黄线：租金指数（基期 = 100）", 0f, 178f, 580f, 286f);
            int count = Mathf.Min(26, h.history.Count);
            if (count > 0)
            {
                GameObject graphObject = new GameObject("housingPriceGraph", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolylineGraph));
                graphObject.transform.SetParent(market, false);
                RectTransform graphRect = graphObject.GetComponent<RectTransform>();
                MacroPlace(graphRect, 16f, 57f, 548f, 186f);
                PolylineGraph graph = graphObject.GetComponent<PolylineGraph>();
                graph.raycastTarget = false;
                graph.lineColor = MacroTeal;
                graph.lineColorB = MacroAmber;
                System.Collections.Generic.List<Vector2> prices = new System.Collections.Generic.List<Vector2>();
                System.Collections.Generic.List<Vector2> rents = new System.Collections.Generic.List<Vector2>();
                float min = float.MaxValue, max = float.MinValue;
                for (int i = h.history.Count - count; i < h.history.Count; i++)
                {
                    min = Mathf.Min(min, h.history[i].priceIndex, h.history[i].rentIndex);
                    max = Mathf.Max(max, h.history[i].priceIndex, h.history[i].rentIndex);
                }
                min = Mathf.Max(0f, min - 4f);
                max += 4f;
                for (int j = 0; j < count; j++)
                {
                    HousingPeriodRecord record = h.history[h.history.Count - count + j];
                    float x = count <= 1 ? 0f : j / (float)(count - 1);
                    prices.Add(new Vector2(x, Mathf.InverseLerp(min, max, record.priceIndex)));
                    rents.Add(new Vector2(x, Mathf.InverseLerp(min, max, record.rentIndex)));
                }
                graph.SetData(prices, null, rents);
                MacroText(market, max.ToString("0"), 14, UiTheme.TextSub, 16f, 40f, 52f, 20f, TextAnchor.MiddleRight);
                MacroText(market, min.ToString("0"), 14, UiTheme.TextSub, 16f, 224f, 52f, 20f, TextAnchor.MiddleRight);
            }
            else
            {
                MacroText(market, "还没有住房市场记录。统一结算本日后，房价与租金会随镇上供需演化。",
                    17, UiTheme.TextSub, 20f, 122f, 540f, 90f, TextAnchor.MiddleCenter);
            }
            MacroText(market, "房价 ¥ " + h.townHousePrice.ToString("0") + " / 套   ·   每期租金 ¥ " + h.periodRent.ToString("0") +
                "   ·   存量 " + h.housingStock.ToString("0") + " 套 + 新开工 " + h.startsThisPeriod.ToString("0.0"),
                16, MacroPaper, 18f, 248f, 544f, 28f, TextAnchor.MiddleCenter);

            RectTransform mortgage = MacroCardPanel(content, "房贷总量与杠杆", "柱：房贷余额  ·  线：房贷 / 年化 GDP", 596f, 178f, 616f, 286f);
            AddMacroGraph(mortgage, state, MacroGraphMode.Housing, 16f, 57f, 584f, 186f);
            MacroText(mortgage, "房贷余额 ¥ " + HousingService.MortgageOutstanding(state).ToString("0") +
                "   ·   房价指数 " + HousingService.PriceIndexMirror(state).ToString("0.0") +
                "   ·   CPI 住房分项 " + m.housingPriceIndex.ToString("0.0"),
                16, MacroPaper, 18f, 248f, 580f, 28f, TextAnchor.MiddleCenter);

            RectTransform real = MacroCardPanel(content, "名义涨还是真涨？", "用 CPI 把价格变化与真实财富分开", 0f, 484f, 580f, 170f);
            float nominalIndex = HousingService.PriceIndexMirror(state);
            float realIndex = m.cpi > 1f ? nominalIndex / m.cpi * 100f : nominalIndex;
            string verdict = realIndex > 100.5f ? "实际房价在涨：涨幅跑赢了 CPI。" :
                realIndex < 99.5f ? "实际房价在跌：名义涨幅被通胀吃掉了。" : "实际房价持平：名义变化与 CPI 大致同步。";
            MacroText(real, "名义 " + nominalIndex.ToString("0.0") + "    实际 " + realIndex.ToString("0.0") +
                "（= 名义 ÷ CPI × 100）", 19, MacroAmber, 20f, 54f, 396f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(real, verdict + " 涨过 CPI 的才是真涨。", 18,
                realIndex >= 99.5f ? MacroTeal : MacroCoral, 20f, 88f, 400f, 32f, TextAnchor.MiddleLeft);
            MacroText(real, "房价上涨 ≠ 真实财富——变现需要接盘者；下跌期卖房要折价 8%、房款延迟 3 期。",
                15, UiTheme.TextSub, 20f, 126f, 400f, 34f, TextAnchor.MiddleLeft);
            Button home = UiTheme.MakeButton(real, "我的住房", () => { CloseTop(); ShowResidentHome(); }, 16);
            MacroPlace(home.rectTransform(), 440f, 62f, 118f, 40f);
            UiTheme.SetButtonStyle(home, MacroPaper, UiTheme.Ink);

            // —— V0.9.3 泡沫仪表(§196.6):三分读 + 总压条(60 阈值刻度) + 相位。红线句#4 进副题。 ——
            RectTransform gauge = MacroCardPanel(content, "泡沫仪表", "长期低利率＋高杠杆＋信贷扩张＝泡沫三征（阈 60 进 Inflating）", 596f, 484f, 616f, 170f);
            float bp = h.bubblePressure;
            float prZ = HousingService.PriceRentZ(state);
            float mgdpZ = HousingService.MortgageGdpZ(state);
            float devZ = HousingService.DeviationZ(state);
            Color phaseColor = h.leveragePhase == LeveragePhase.Inflating ? MacroAmber :
                h.leveragePhase == LeveragePhase.Deflating ? MacroCoral : MacroTeal;
            MacroText(gauge, "泡沫压力 " + bp.ToString("0") + " / 100   ·   相位：" + LeveragePhaseName(h.leveragePhase),
                19, phaseColor, 16f, 42f, 400f, 26f, TextAnchor.MiddleLeft, FontStyle.Bold);
            BubbleBar(gauge, "总压", bp / 100f, 0.6f, 70f, phaseColor);
            BubbleBar(gauge, "房价/年租比", prZ, -1f, 96f, MacroAmber);
            BubbleBar(gauge, "房贷/GDP", mgdpZ, -1f, 120f, MacroAmber);
            BubbleBar(gauge, "房价动能", devZ, -1f, 144f, MacroAmber);
        }

        /// <summary>泡沫仪表条:标签+轨道填充条(value01)+读数;threshold&gt;0 时画阈值刻度竖线。</summary>
        void BubbleBar(RectTransform card, string label, float value01, float threshold, float y, Color color)
        {
            MacroText(card, label, 15, UiTheme.TextMain, 16f, y, 132f, 20f, TextAnchor.MiddleLeft);
            Image track = UiTheme.MakeImage(card, MacroCardSoft, "bpTrack");
            MacroPlace(track.GetComponent<RectTransform>(), 152f, y + 2f, 336f, 12f);
            Image fill = UiTheme.MakeImage(track.transform, color, "bpFill");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.rectTransform.Stretch();
            fill.fillAmount = Mathf.Clamp01(value01);
            if (threshold > 0f)
            {
                Image tick = UiTheme.MakeImage(card, UiTheme.Ink, "bpTick");
                MacroPlace(tick.GetComponent<RectTransform>(), 152f + 336f * threshold - 1f, y - 1f, 2f, 18f);
            }
            MacroText(card, (value01 * 100f).ToString("0"), 15, UiTheme.TextSub, 496f, y, 40f, 20f, TextAnchor.MiddleRight);
        }

        static string LeveragePhaseName(LeveragePhase phase)
        {
            switch (phase)
            {
                case LeveragePhase.Inflating: return "泡沫累积（新开工上浮）";
                case LeveragePhase.Deflating: return "反转下行（卖房折价＋延迟）";
                case LeveragePhase.Cleanup: return "消化修复";
                default: return "平稳";
            }
        }

        public void ShowMacroNews()
        {
            GameState state = GameState.I;
            if (state == null) return;
            MacroEconomySimulator.Ensure(state);
            GameObject root = BeginPanel("经济新闻 · 因果而不是噪声", 880f, 650f);
            RectTransform content = ContentOf(root);
            if (state.macro.news.Count == 0)
            {
                MacroText(content, "还没有宏观周期记录。回到宏观中心运行下一期，新闻会解释指标为什么变化。",
                    20, UiTheme.TextSub, 40f, 80f, 752f, 80f, TextAnchor.MiddleCenter);
                return;
            }
            int count = Mathf.Min(7, state.macro.news.Count);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Button item = UiTheme.MakeButton(content, state.macro.news[i], () => ShowMacroRecord(index), 17);
                MacroPlace(item.rectTransform(), 20f, 12f + i * 70f, 792f, 58f);
                UiTheme.SetButtonStyle(item, i == 0 ? MacroCardSoft : MacroCard, UiTheme.TextMain);
            }
        }

        void ShowMacroRecord(int newestIndex)
        {
            MacroEconomyState m = GameState.I.macro;
            int index = m.history.Count - 1 - newestIndex;
            if (index < 0 || index >= m.history.Count) return;
            MacroPeriodRecord r = m.history[index];
            GameObject root = BeginPanel("第 " + r.period + " 期 · " + r.headline, 840f, 520f);
            RectTransform content = ContentOf(root);
            Text chain = MacroText(content, r.causalChain, 21, UiTheme.TextMain, 28f, 20f, 736f, 94f, TextAnchor.UpperLeft, FontStyle.Bold);
            float chainShift = ExpandTopText(chain);
            GrowPanel(root, chainShift);
            MacroText(content, "实际 GDP  ¥ " + r.realGdp.ToString("0") + "    增长 " + MacroSigned(r.gdpGrowth) + "%\n" +
                "CPI " + r.cpi.ToString("0.0") + "    通胀 " + MacroSigned(r.inflation) + "%    失业 " + r.unemploymentRate.ToString("0.0") + "%\n" +
                "消费 " + r.consumption.ToString("0") + "    投资 " + r.investment.ToString("0") + "    政府购买 " + r.governmentSpending.ToString("0") + "    净出口 " + MacroSignedMoney(r.netExports),
                19, UiTheme.TextSub, 28f, 132f + chainShift, 736f, 130f, TextAnchor.UpperLeft);
            MacroText(content, "周期位置：" + MacroEconomySimulator.CycleName(r.cyclePhase) + "    产出缺口 " + MacroSigned(r.outputGap) + "%\n" +
                "消费者信心 " + r.consumerConfidence.ToString("0") + "    企业信心 " + r.businessConfidence.ToString("0"),
                19, MacroTeal, 28f, 282f + chainShift, 736f, 80f, TextAnchor.UpperLeft);
            string conceptId = r.shock == MacroShockType.EnergyCrisis ? "stagflation" :
                r.shock == MacroShockType.AiRevolution ? "productivity" :
                r.gdpGrowth < 0f ? "businesscycle" : "gdp";
            Button concept = UiTheme.MakeButton(content, "打开相关概念卡", () => ShowCardDetail(conceptId), 18);
            MacroPlace(concept.rectTransform(), 276f, 386f + chainShift, 288f, 46f);
            UiTheme.SetButtonStyle(concept, MacroPaper, UiTheme.Ink);
        }

        public void ShowNationalReport()
        {
            GameState state = GameState.I;
            if (state == null) return;
            MacroEconomySimulator.Ensure(state);
            MacroEconomyState m = state.macro;
            GameObject root = BeginPanel("国家经济报告", 960f, 690f);
            RectTransform content = ContentOf(root);
            MacroText(content, MacroNationalStyle(m), 30, MacroAmber, 24f, 8f, 864f, 46f, TextAnchor.MiddleCenter, FontStyle.Bold);
            MacroText(content, "这不是分数，而是你在增长、稳定、财政与长期能力之间形成的治理风格。",
                17, UiTheme.TextSub, 24f, 55f, 864f, 34f, TextAnchor.MiddleCenter);
            ReportLine(content, "实际增长", MacroSigned(m.gdpGrowth) + "%", m.gdpGrowth >= 0f ? "产出正在扩张" : "需求和生产仍需修复", 96f);
            ReportLine(content, "物价稳定", "通胀 " + MacroSigned(m.inflation) + "%", Mathf.Abs(m.inflation) <= 3f ? "价格总体稳定" : "生活成本压力偏高", 154f);
            ReportLine(content, "充分就业", "失业 " + m.unemploymentRate.ToString("0.0") + "%", m.unemploymentRate <= 7f ? "就业较充分" : "仍有明显闲置劳动力", 212f);
            ReportLine(content, "财政空间", "债务 / GDP " + m.debtToGdp.ToString("0.0") + "%", m.debtToGdp <= 40f ? "仍有政策空间" : "利息与偿债会挤压未来选择", 270f);
            ReportLine(content, "居民收入", "中位收入 ¥ " + state.government.medianIncome.ToString("0"), "工资与就业共同决定家庭购买力", 328f);
            ReportLine(content, "分配差距", "不平等指数 " + m.incomeInequality.ToString("0.0"), "指数越高，收入与购买力差距越明显", 386f);
            ReportLine(content, "长期能力", "生产率 " + m.productivity.ToString("0.0"), "教育、技术和资本积累决定潜在 GDP", 444f);
            MacroText(content, "提醒：污染清理和灾后重建会增加支出与 GDP，但不代表居民一定比灾害发生前更幸福。",
                17, MacroPaper, 34f, 520f, 844f, 50f, TextAnchor.MiddleCenter);
        }

        void AdvanceMacroPeriod(int tab)
        {
            GameState state = GameState.I;
            // 先关自己、再结算(照抄 UiReports):TryNextDay 内部会弹出终局总评/竞争介绍/价格战等
            // 关键面板,结算后若再走 ReloadMacro 的 CloseTop 会把它们当场错杀(P1-1)。
            CloseTop();
            if (GameBootstrap.I.TryNextDay())
            {
                MacroPeriodRecord record = state.macro.history.Count > 0
                    ? state.macro.history[state.macro.history.Count - 1] : null;
                if (record != null) ShowToast(record.headline);
                if (state.phase != GamePhase.Ended) ShowMacroDashboard(tab);
            }
        }

        void ReloadMacro(int tab) { CloseTop(); ShowMacroDashboard(tab); }

        void SetMacroFiscal(FiscalStance stance)
        {
            MacroEconomyState m = GameState.I.macro;
            float investment = stance == FiscalStance.Neutral ? 0f : Mathf.Max(750f, m.plannedPublicInvestment);
            MacroEconomySimulator.SetFiscalPolicy(GameState.I, stance, investment,
                stance == FiscalStance.Expansionary ? -2f : stance == FiscalStance.Contractionary ? 2f : 0f);
            if (stance != FiscalStance.Neutral)
                ProgressionService.RecordSignal(GameState.I, ProgressSignals.FiscalResponse,
                    "选择财政立场", stance == FiscalStance.Expansionary ? "增加公共购买以支持需求" : "收紧需求以降低过热压力");
            ReloadMacro(4);
        }

        void ChangeMacroInvestment(float delta)
        {
            MacroEconomyState m = GameState.I.macro;
            float investment = Mathf.Clamp(m.plannedPublicInvestment + delta, 0f, 3200f);
            FiscalStance stance = m.fiscalStance == FiscalStance.Neutral && investment > 0f ? FiscalStance.Expansionary : m.fiscalStance;
            MacroEconomySimulator.SetFiscalPolicy(GameState.I, stance, investment, m.plannedTaxChange);
            ReloadMacro(4);
        }

        void ShockButton(RectTransform parent, GameState state, MacroShockType shock, string label, float x, float y)
        {
            Button button = UiTheme.MakeButton(parent, label, () =>
            {
                MacroEconomySimulator.QueueShock(state, shock);
                if (shock == MacroShockType.EnergyCrisis || shock == MacroShockType.GlobalDemandFall || shock == MacroShockType.Disaster)
                    ProgressionService.RecordSignal(state, ProgressSignals.RecessionObserved,
                        "运行衰退实验", "向当前世界加入「" + label + "」并观察产出与失业");
                if (shock == MacroShockType.AiRevolution)
                    ProgressionService.RecordSignal(state, ProgressSignals.ProductivityPolicy,
                        "运行生产率实验", "技术进步提高潜在产出，但也会改变就业结构");
                ReloadMacro(3);
            }, 17);
            MacroPlace(button.rectTransform(), x, y, 212f, 42f);
            UiTheme.SetButtonStyle(button, state.macro.pendingShock == shock ? MacroAmber : MacroPaper, UiTheme.Ink);
        }

        void CaptureMacroLearningAction(GameState state, int selectedTab)
        {
            ChapterDefinition chapter = ProgressionService.Current(state);
            if (chapter == null) return;
            if (chapter.number == 23)
                ProgressionService.RecordSignal(state, ProgressSignals.MacroOpened,
                    "打开宏观仪表盘", "开始从整个小镇的 GDP、物价与就业观察经济");
            else if (chapter.number == 24 && selectedTab == 0)
                ProgressionService.RecordSignal(state, ProgressSignals.RealNominalObserved,
                    "比较名义与实际 GDP", "使用 CPI 把价格变化与真实产出变化分开");
            else if (chapter.number == 25 && selectedTab == 1)
                ProgressionService.RecordSignal(state, ProgressSignals.InflationObserved,
                    "查看 CPI 分类", "从固定消费篮子识别总体物价变化");
            else if (chapter.number == 26 && selectedTab == 2)
                ProgressionService.RecordSignal(state, ProgressSignals.UnemploymentObserved,
                    "查看就业构成", "区分就业、失业与非劳动力人口");
            else if (chapter.number == 27 &&
                (state.macro.cyclePhase == MacroCyclePhase.Recession || state.macro.cyclePhase == MacroCyclePhase.Trough))
                ProgressionService.RecordSignal(state, ProgressSignals.RecessionObserved,
                    "观察经济衰退", "实际产出下降并伴随就业压力");
            else if (chapter.number == 29 && selectedTab == 3)
                ProgressionService.RecordSignal(state, ProgressSignals.OutputGapObserved,
                    "查看产出缺口", "比较实际 GDP 与潜在 GDP");
        }

        void PriceRow(RectTransform parent, string name, int weight, float index, float y)
        {
            MacroText(parent, name, 17, UiTheme.TextMain, 18f, y, 160f, 30f, TextAnchor.MiddleLeft);
            MacroText(parent, "权重 " + weight + "%", 16, UiTheme.TextSub, 184f, y, 150f, 30f, TextAnchor.MiddleCenter);
            MacroText(parent, "指数 " + index.ToString("0.0"), 18, index > 108f ? MacroCoral : MacroTeal,
                350f, y, 190f, 30f, TextAnchor.MiddleRight, FontStyle.Bold);
        }

        void LaborRow(RectTransform parent, string name, int value, float y, Color color)
        {
            MacroText(parent, name, 18, UiTheme.TextSub, 24f, y, 280f, 32f, TextAnchor.MiddleLeft);
            MacroText(parent, value + " 人", 21, color, 330f, y, 210f, 32f, TextAnchor.MiddleRight, FontStyle.Bold);
        }

        void ReportLine(RectTransform parent, string label, string value, string note, float y)
        {
            Image line = UiTheme.MakeImage(parent, MacroCard, "report_" + label);
            MacroPlace(line.rectTransform, 24f, y, 864f, 50f);
            MacroText(line.rectTransform, label, 17, UiTheme.TextSub, 18f, 3f, 190f, 44f, TextAnchor.MiddleLeft);
            MacroText(line.rectTransform, value, 19, MacroAmber, 212f, 3f, 210f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(line.rectTransform, note, 17, UiTheme.TextMain, 430f, 3f, 410f, 44f, TextAnchor.MiddleRight);
        }

        void MacroMetric(RectTransform parent, string label, string value, float x, Color valueColor)
        {
            Image card = UiTheme.MakeImage(parent, MacroCard, "macroMetric_" + label);
            MacroPlace(card.rectTransform, x, 42f, 232f, 66f);
            MacroText(card.rectTransform, label, 16, UiTheme.TextSub, 14f, 6f, 204f, 22f, TextAnchor.MiddleLeft);
            MacroText(card.rectTransform, value, 22, valueColor, 14f, 29f, 204f, 30f, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        RectTransform MacroCardPanel(RectTransform parent, string title, string subtitle, float x, float y, float width, float height)
        {
            Image card = UiTheme.MakeImage(parent, MacroCard, "macroCard_" + title);
            MacroPlace(card.rectTransform, x, y, width, height);
            Image rule = UiTheme.MakeImage(card.transform, MacroTeal, "sectionRule");
            MacroPlace(rule.rectTransform, 0f, 0f, 7f, 48f);
            MacroText(card.rectTransform, title, 21, UiTheme.TextMain, 18f, 5f, width - 36f, 29f, TextAnchor.MiddleLeft, FontStyle.Bold);
            MacroText(card.rectTransform, subtitle, 15, UiTheme.TextSub, 18f, 34f, width - 36f, 24f, TextAnchor.MiddleLeft);
            return card.rectTransform;
        }

        void AddMacroGraph(RectTransform parent, GameState state, MacroGraphMode mode, float x, float y, float width, float height)
        {
            GameObject go = new GameObject("macroGraph_" + mode, typeof(RectTransform), typeof(CanvasRenderer), typeof(MacroEconomyGraph));
            go.transform.SetParent(parent, false);
            MacroPlace(go.GetComponent<RectTransform>(), x, y, width, height);
            go.GetComponent<MacroEconomyGraph>().Configure(state, mode);
        }

        Text MacroText(Transform parent, string text, int size, Color color, float x, float y, float width, float height,
            TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text label = UiTheme.MakeText(parent, text, size, color, anchor, style);
            MacroPlace(label.rectTransform, x, y, width, height);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static void MacroPlace(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // 围猎三#3 复审:按显示精度取整后再判号——outputGap 复苏贴零是常态,旧 float 判号在 (-0.05,0) 显示「-0.0%」。
        // bug搜#21:显示值也必须用取整后的 shown——旧代码判号用 shown、拼串用原始 value,
        // value∈(-0.05,0) 时 shown 为负零(IEEE −0>=0 取「+」)而 ToString 保留负号,输出「+-0.0%」。
        static string MacroSigned(float value)
        {
            float shown = Mathf.Round(value * 10f) / 10f;
            if (shown == 0f) shown = 0f;   // 负零归正(-0f==0f 为真,再赋正零),防「+-0.0%」
            return (shown >= 0f ? "+" : "") + shown.ToString("0.0");
        }
        static string MacroSignedMoney(float value) { return (value >= 0f ? "+¥ " : "−¥ ") + Mathf.Abs(value).ToString("0"); }

        static string MacroChapterTitle(int chapter)
        {
            string[] titles = { "第 1 章 · 一座小镇的 GDP", "第 2 章 · 名义与实际增长", "第 3 章 · 物价与通胀",
                "第 4 章 · 就业与经济周期", "第 5 章 · 信心推动总需求", "第 6 章 · 能源危机与滞胀",
                "第 7 章 · 财政救援与乘数", "第 8 章 · 过热与产出缺口", "第 9 章 · 技术、教育与长期增长" };
            return titles[Mathf.Clamp(chapter, 1, titles.Length) - 1];
        }

        static string MacroShockName(MacroShockType shock)
        {
            switch (shock)
            {
                case MacroShockType.ConsumptionBoom: return "消费热潮";
                case MacroShockType.InvestmentBoom: return "投资繁荣";
                case MacroShockType.EnergyCrisis: return "能源危机";
                case MacroShockType.GlobalDemandFall: return "海外需求下降";
                case MacroShockType.AiRevolution: return "AI 革命";
                case MacroShockType.Disaster: return "自然灾害";
                case MacroShockType.PopulationGrowth: return "人口增长";
                case MacroShockType.HousingReversal: return "房贷市场反转";
                default: return "无";
            }
        }

        static string MacroFiscalLagName(int stage)
        {
            string[] stages = { "尚未启动", "审批", "施工准备", "工资与采购", "投入使用", "充分传导", "项目完成" };
            return stages[Mathf.Clamp(stage, 0, stages.Length - 1)];
        }

        static string MacroNationalStyle(MacroEconomyState m)
        {
            if (m.productivity > 108f && m.plannedPublicInvestment > 0f) return "长期主义型管理者";
            if (m.debtToGdp < 15f && m.fiscalStance != FiscalStance.Expansionary) return "财政稳健型管理者";
            if (m.fiscalStance == FiscalStance.Expansionary) return "短期刺激型管理者";
            if (m.gdpGrowth > 3f) return "增长优先型管理者";
            return "稳定平衡型管理者";
        }
    }
}
