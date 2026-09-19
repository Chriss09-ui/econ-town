using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// V0.7《世界经济 · 国际贸易中心》面板(PRD §61/§63/§7)。
    /// 四个标签页:概览(含贸易战抉择) / 贸易与企业(出海开关+配额) /
    /// 外汇市场(报价+五项分解+制度切换) / 海关与实验室。数据全部读真实账本行;
    /// 结构化新闻归因、章节信号采集与手册"我的案例"共用同一组数字。
    /// </summary>
    public partial class UIManager
    {
        static readonly Color WorldTeal = new Color32(0x35, 0x7a, 0x74, 255);
        static readonly Color WorldGold = new Color32(0xe8, 0xa8, 0x4c, 255);
        static readonly Color WorldBg = new Color32(0x33, 0x3d, 0x47, 255);
        static readonly Color WorldPaperRow = new Color32(0x3e, 0x49, 0x55, 255);

        // —— 比较优势实验室参数(会话内有效;重开恢复 PRD §19 默认表) ——
        float cmpAdvHoursADrink = 1f, cmpAdvHoursAMachine = 4f;
        float cmpAdvHoursBDrink = 3f, cmpAdvHoursBMachine = 6f;
        int cmpAdvCursorA = 0, cmpAdvCursorB = 2;

        // —— 三元悖论三开关 ——
        bool triFixedRate = true, triFreeCapital = true, triIndependentMoney = true;

        public void ShowWorldEconomy(int selectedTab)
        {
            GameState s = GameState.I;
            if (s == null) return;
            InternationalTradeService.Ensure(s);
            ProgressionService.Ensure(s);

            GameObject root = BeginPanel("世界经济 · 国际贸易中心", 1260f, 840f);
            RectTransform c = ContentOf(root);
            int tab = Mathf.Clamp(selectedTab, 0, 3);

            string[] tabs = { "概览", "贸易与企业", "外汇市场", "海关与实验室" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                Button b = UiTheme.MakeButton(c, tabs[i], () => { ClosePanel(root); ShowWorldEconomy(idx); }, 18);
                b.name = "worldTab_" + i;
                PlaceTopLeft(b.rectTransform(), 12f + i * 218f, 0f, 206f, 46f);
                UiTheme.SetButtonStyle(b, i == tab ? WorldTeal : new Color32(0xe6, 0xcf, 0xa6, 255),
                    i == tab ? Color.white : UiTheme.Ink);
                b.interactable = i != tab;
            }

            if (!state_can_export(s))
            {
                ShopText(c, "国际港口尚未对小镇开放。\n\n完成第六篇「钱、银行与央行」之后，\n国际贸易港的集装箱吊机才会开始运转。",
                    22, UiTheme.TextMain, 120f, 260f, 1020f, 240f, TextAnchor.MiddleCenter);
                return;
            }
            // bug搜#19:学习信号只在解锁门禁之后记录——旧代码在函数开头(门禁之前)就
            // RecordSignal,第 30-41 章点到外汇页签时信号先落账,推到第 47 章「查看外汇
            // 报价」时瞬间自动结课,章节目标从未真正完成(信号永久留存,不看记录时机)。
            if (tab == 2)
                ProgressionService.RecordSignal(s, ProgressSignals.ExchangeRateObserved,
                    "查看外汇市场", "阅读了「1 B币 = X A币」的实时报价与五项分解");

            if (tab == 0) BuildWorldOverview(c, s, root);
            else if (tab == 1) BuildWorldTradeTab(c, s, root);
            else if (tab == 2) BuildWorldFxTab(c, s, root);
            else BuildWorldPolicyTab(c, s, root);
        }

        static bool state_can_export(GameState state)
        {
            return state.progression != null &&
                   (state.progression.mainlineComplete ||
                    state.progression.completedChapter >= InternationalTradeService.ExportUnlockChapter);
        }

        static string MoveText(float pp)
        {
            return pp >= 0 ? "+" + pp.ToString("0.00") : pp.ToString("0.00");
        }

        static string AnchorText(float baseRate)
        {
            return baseRate.ToString("0.00");
        }

        // ——————————————————— Tab 0 概览 ———————————————————
        void BuildWorldOverview(RectTransform c, GameState s, GameObject root)
        {
            WorldTradeState trade = s.worldTrade;
            TradeDayRecord row = InternationalTradeService.LastRecord(s);
            ExchangeRateState fx = trade.exchangeRate;

            RectTransform quote = WorldCard(c, "今日汇率", 12f, 62f, 400f, 210f);
            ShopText(quote, "1 B币 = " + fx.rate.ToString("0.00") + " A币", 38, WorldGold,
                20f, 48f, 366f, 62f, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (fx.history.Count >= 2)
            {
                RateDayRecord lastRate = fx.history[fx.history.Count - 1];
                RateDayRecord prevRate = fx.history[fx.history.Count - 2];
                float movePct = prevRate.rate > 0f ? (lastRate.rate - prevRate.rate) / prevRate.rate * 100f : 0f;
                string direction = movePct > .01f ? "A币贬值" : movePct < -.01f ? "A币升值" : "基本持稳";
                ShopText(quote, direction + " " + Mathf.Abs(movePct).ToString("0.00") + "%　·　" +
                    ExchangeRateService.RegimeName(s), 18,
                    movePct > .01f ? UiTheme.Bad : movePct < -.01f ? UiTheme.Good : UiTheme.TextSub,
                    20f, 118f, 366f, 30f);
            }
            else
                ShopText(quote, "尚未形成汇率史：结算一天后显示变动方向。", 16, UiTheme.TextSub,
                    20f, 118f, 360f, 30f);
            ShopText(quote, "名义锚 1 B币 = " + AnchorText(fx.baseRate) +
                " A币　·　外汇储备 " + s.centralBank.forexReservesBCurrency.ToString("0") + " B币",
                15, UiTheme.TextSub, 20f, 156f, 366f, 44f, TextAnchor.UpperLeft);

            RectTransform bProfile = WorldCard(c, trade.countryB.name, 424f, 62f, 380f, 210f, WorldGold);
            ShopText(bProfile,
                "增长趋势 " + trade.countryB.gdpGrowthPeriod.ToString("+0.00;-0.00") + "%/日，动量 " +
                trade.countryB.growthMomentum.ToString("+0.00;-0.00") +
                "\n收入指数 " + trade.countryB.incomeLevel.ToString("0") +
                "，政策利率 " + trade.countryB.policyRate.ToString("0.00") + "%" +
                "\n对 A 产品关税 " + (trade.countryB.tariffOnAExports * 100f).ToString("0") + "%" +
                "\n状态：" + (trade.countryB.recessionActive ? "衰退中（外需收缩）" : "正常运行"),
                17, UiTheme.TextMain, 20f, 52f, 344f, 150f, TextAnchor.UpperLeft);

            RectTransform balance = WorldCard(c, "本日国际收支（真实账本行）", 816f, 62f, 420f, 210f);
            if (row != null)
            {
                ShopText(balance,
                    "出口 X ¥" + row.exportsTotal.ToString("0") +
                    "\n进口 M ¥" + row.importsTotal.ToString("0") +
                    "\n净出口 NX ¥" + row.netExports.ToString("0") +
                    "\n经常账户 CA " + row.currentAccountBCurrency.ToString("+0.0;-0.0") +
                    " B币，资本账户 KA " +
                    (row.capitalInflowBCurrency - row.capitalOutflowBCurrency).ToString("+0.0;-0.0") + " B币",
                    18, UiTheme.TextMain, 20f, 52f, 388f, 150f, TextAnchor.UpperLeft);
            }
            else
                ShopText(balance, "还没有贸易账本记录：完成一天结算后这里会出现真实数据。",
                    17, UiTheme.TextSub, 20f, 60f, 388f, 120f, TextAnchor.UpperLeft);

            // 结构化归因卡(PRD §114):同源分解喂给新闻、"我的案例"与这里。
            RectTransform why = WorldCard(c, "汇率为什么这样动？（本日五项分解，单位百分点）",
                12f, 286f, 1224f, 180f, WorldGold);
            if (fx.history.Count > 0)
            {
                RateDayRecord rateLast = fx.history[fx.history.Count - 1];
                float sum = rateLast.tradeFlowEffect + rateLast.capitalFlowEffect +
                    rateLast.expectationEffect + rateLast.interventionEffect + rateLast.anchorPullEffect;
                ShopText(why,
                    "贸易流 " + MoveText(rateLast.tradeFlowEffect) +
                    "　资本流 " + MoveText(rateLast.capitalFlowEffect) +
                    "　预期 " + MoveText(rateLast.expectationEffect) +
                    "　央行干预 " + MoveText(rateLast.interventionEffect) +
                    "　锚回归 " + MoveText(rateLast.anchorPullEffect) +
                    "\n合计 ≈ " + MoveText(sum) + "%　→　顺差推动本币升值（X↓），逆差与风险外流推动贬值（X↑）。" +
                    "\n（负值=升值压力；正值=贬值压力；与经济新闻、手册案例同源）",
                    19, UiTheme.TextMain, 22f, 50f, 1180f, 122f, TextAnchor.UpperLeft);
            }
            else
                ShopText(why, "至少完成两天结算后，此处展示五项分解。", 17, UiTheme.TextSub,
                    22f, 66f, 900f, 50f, TextAnchor.UpperLeft);

            if (trade.pendingTradeWarChoice)
                BuildWarBanner(c, s, root);

            Button openLab = UiTheme.MakeButton(c, "进入比较优势 / 三元悖论实验室 → 第 3 页",
                () => { ClosePanel(root); ShowWorldEconomy(3); }, 17);
            PlaceTopLeft(openLab.rectTransform(), 12f, 760f, 420f, 46f);
        }

        void BuildWarBanner(RectTransform c, GameState s, GameObject root)
        {
            CountryProfileState b = s.worldTrade.countryB;
            RectTransform war = WorldCard(c, "贸易战抉择：B 国把对 A 产品关税上调到 " +
                (b.tariffOnAExports * 100f).ToString("0") + "%", 12f, 480f, 1224f, 236f,
                new Color32(0xa8, 0x4c, 0x3c, 255));
            AddWarOption(war, s, root, "不回应", "忍受更高外部关税；保持市场开放与低价进口品。",
                new Rect(24f, 92f, 372f, 120f), () => ResolveTradeWarChoice(s, root, 0));
            AddWarOption(war, s, root, "对等关税 +5pp", "保护国内同类企业；进口到岸价上涨、供应链承压，或再遭报复。",
                new Rect(426f, 92f, 372f, 120f), () => ResolveTradeWarChoice(s, root, 1));
            AddWarOption(war, s, root, "财政补贴出口 ¥150", "稳住海外订单、品牌 +2；赤字扩大由全体纳税人承担。",
                new Rect(828f, 92f, 372f, 120f), () => ResolveTradeWarChoice(s, root, 2));
        }

        void AddWarOption(RectTransform parent, GameState s, GameObject root, string title, string caption,
            Rect rect, System.Action pick)
        {
            Image box = UiTheme.MakeImage(parent, WorldPaperRow, "warOpt_" + title);
            PlaceTopLeft(box.rectTransform, rect.x, rect.y, rect.width, rect.height);
            Button b = box.gameObject.AddComponent<Button>();
            b.targetGraphic = box;
            b.onClick.AddListener(() => pick());
            ShopText(box.rectTransform, title, 19, Color.white, 14f, 10f, rect.width - 28f, 30f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            ShopText(box.rectTransform, caption, 14, UiTheme.TextSub, 14f, 44f, rect.width - 28f,
                rect.height - 52f, TextAnchor.UpperLeft);
        }

        void ResolveTradeWarChoice(GameState state, GameObject root, int option)
        {
            CountryProfileState b = state.worldTrade.countryB;
            string summary;
            if (option == 0)
            {
                state.government.enterpriseConfidence =
                    Mathf.Clamp(state.government.enterpriseConfidence - 3f, 15f, 100f);
                summary = "选择不回应：维持开放。企业信心 -3；B 国关税维持在 " +
                    (b.tariffOnAExports * 100f).ToString("0") + "%。";
            }
            else if (option == 1)
            {
                GovernmentSimulator.SetPolicy(state, PolicyType.ImportTariff,
                    Mathf.Min(25f, GovernmentSimulator.PolicyValue(state, PolicyType.ImportTariff) + 5f));
                b.tariffOnAExports = Mathf.Clamp(b.tariffOnAExports - .04f, 0f, .6f);
                summary = "对等关税落地：本国进口关税升至 " +
                    GovernmentSimulator.PolicyValue(state, PolicyType.ImportTariff).ToString("0") +
                    "%，B 国回调 4pp；观察双方贸易量变化。";
            }
            else
            {
                state.government.pendingSubsidySpending += 150f;
                state.playerBrand = Mathf.Clamp(state.playerBrand + 2f, 0f, 100f);
                summary = "出口补贴 ¥150 已挂入下期支出，品牌 +2 提升海外竞争力；代价是财政空间缩小。";
            }
            state.worldTrade.pendingTradeWarChoice = false;
            state.UnlockCard("intl.tradewar");
            state.UnlockCard("intl.protectionism");
            ProgressionService.RecordSignal(state, ProgressSignals.TradeWarResponseChosen, "贸易战回应", summary);
            SaveSystem.SaveKeyMoment(state, "贸易战回应");
            CloseAll();
            ShowToast(summary);
            ShowWorldEconomy(0);
        }

        // ——————————————————— Tab 1 贸易与企业 ———————————————————
        void BuildWorldTradeTab(RectTransform c, GameState s, GameObject root)
        {
            WorldTradeState trade = s.worldTrade;
            TradeDayRecord row = InternationalTradeService.LastRecord(s);

            RectTransform export = WorldCard(c, "我的出海渠道", 12f, 62f, 600f, 330f);
            ShopText(export, "当前状态：" + (s.exportChannelEnabled ? "营业中（产能配额池）" : "未开通"),
                20, s.exportChannelEnabled ? UiTheme.Good : UiTheme.TextSub,
                22f, 46f, 400f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            Button toggle = UiTheme.MakeButton(export, s.exportChannelEnabled ? "暂停出海" : "开通出海通道", () =>
            {
                s.exportChannelEnabled = !s.exportChannelEnabled;
                s.Changed();
                ClosePanel(root);
                ShowWorldEconomy(1);
            }, 19);
            PlaceTopLeft(toggle.rectTransform(), 24f, 94f, 250f, 52f);
            UiTheme.SetButtonStyle(toggle, s.exportChannelEnabled ? UiTheme.Bad : WorldTeal, Color.white);

            ShopText(export, "每日期望上限", 17, UiTheme.TextSub, 300f, 106f, 140f, 30f);
            Button minus = UiTheme.MakeButton(export, "-", () =>
            {
                s.exportTargetQuotaCups = Mathf.Max(0, s.exportTargetQuotaCups - 10);
                s.Changed();
                ClosePanel(root); ShowWorldEconomy(1);
            }, 20);
            PlaceTopLeft(minus.rectTransform(), 440f, 100f, 44f, 40f);
            Button plus = UiTheme.MakeButton(export, "+", () =>
            {
                s.exportTargetQuotaCups =
                    Mathf.Min((int)s.worldTrade.dailyPortCapacityCups, s.exportTargetQuotaCups + 10);
                s.Changed();
                ClosePanel(root); ShowWorldEconomy(1);
            }, 20);
            PlaceTopLeft(plus.rectTransform(), 490f, 100f, 44f, 40f);
            ShopText(export, s.exportTargetQuotaCups + " 杯\n（港口日吞吐上限 " +
                trade.dailyPortCapacityCups.ToString("0") + "）", 18, WorldGold,
                296f, 142f, 200f, 56f, TextAnchor.UpperLeft);

            ShopText(export,
                "机制：出口不占内销库存，只用「计划产量之外的人力产能余量」；\n" +
                "实收出厂价 Fob = B 国基准价 × 汇率，运费 ¥" +
                InternationalPriceService.ShippingWedgePerUnit(s).ToString("0.0") + "/杯我方承担；\n" +
                "B 国关税计入消费者到手价，不过玩家的手。",
                16, UiTheme.TextSub, 22f, 214f, 560f, 108f, TextAnchor.UpperLeft);

            RectTransform ledger = WorldCard(c, "本日贸易账本（三路真实流量）", 624f, 62f, 624f, 330f, WorldGold);
            if (row == null)
            {
                ShopText(ledger, "暂无数据：先完成一天结算。", 18, UiTheme.TextSub, 24f, 90f, 560f, 60f);
                return;
            }
            float taxPercent = GovernmentSimulator.PolicyValue(s, PolicyType.ImportTariff);
            ShopText(ledger,
                "出口｜玩家出货 " + row.playerExportUnits.ToString("0") + " 杯 × ¥" +
                InternationalPriceService.ProducerFobPerUnit(s).ToString("0.00") + " ≈ ¥" +
                row.playerExportRevenue.ToString("0") + "（运费 −¥" + row.playerShippingCost.ToString("0") + "）\n" +
                "　　　镇集体商行基线 ¥" + row.townExportRevenue.ToString("0") + "\n" +
                "进口｜居民消费品 ¥" + row.consumerImportSpend.ToString("0") +
                " · 企业中间投入 ¥" + row.intermediateImportSpend.ToString("0") +
                " · 商行与其他 ¥" + row.townImportSpend.ToString("0") + "\n" +
                "关税｜" + (row.tariffRateApplied * 100f).ToString("0") + "% × 完税基数 ¥" +
                row.tariffBase.ToString("0") + " = 关税收 ¥" + row.tariffRevenue.ToString("0") +
                "\n参考｜进口巧克力礼盒现价 ≈ ¥" +
                InternationalPriceService.ImportedSkuConsumerPrice(s, .85f).ToString("0.00") +
                " = 0.85 B币 × 汇率 × (1+" + taxPercent.ToString("0") + "%）",
                18, UiTheme.TextMain, 22f, 48f, 586f, 274f, TextAnchor.UpperLeft);
        }

        // ——————————————————— Tab 2 外汇市场 ———————————————————
        void BuildWorldFxTab(RectTransform c, GameState s, GameObject root)
        {
            WorldTradeState trade = s.worldTrade;
            ExchangeRateState fx = trade.exchangeRate;

            RectTransform market = WorldCard(c, "外汇市场 ·「1 B币 = X A币」（X 变大 = A币贬值）",
                12f, 62f, 610f, 700f);
            Button switchBtn = UiTheme.MakeButton(market,
                fx.regime == ExchangeRegime.Fixed ? "转入浮动汇率" : "宣布固定汇率（可信度≥60 且储备≥500）", () =>
                {
                    ExchangeRegime target =
                        fx.regime == ExchangeRegime.Fixed ? ExchangeRegime.Floating : ExchangeRegime.Fixed;
                    ExchangeRateService.TrySetRegime(s, target, out string message);
                    ShowToast(message);
                    ClosePanel(root); ShowWorldEconomy(2);
                }, 16);
            PlaceTopLeft(switchBtn.rectTransform(), 22f, 48f, 566f, 50f);
            UiTheme.SetButtonStyle(switchBtn, WorldGold, UiTheme.Ink);

            if (fx.regime == ExchangeRegime.Fixed)
            {
                ShopText(market, "盯住目标价（±0.25 调整）：当前 " + fx.pegTarget.ToString("0.00"),
                    17, UiTheme.TextSub, 22f, 112f, 320f, 30f);
                Button pegMinus = UiTheme.MakeButton(market, "-", () =>
                {
                    ExchangeRateService.TrySetPeg(s, fx.pegTarget - .25f, out string m);
                    ShowToast(m);
                    ClosePanel(root); ShowWorldEconomy(2);
                }, 19);
                PlaceTopLeft(pegMinus.rectTransform(), 356f, 108f, 44f, 38f);
                Button pegPlus = UiTheme.MakeButton(market, "+", () =>
                {
                    ExchangeRateService.TrySetPeg(s, fx.pegTarget + .25f, out string m);
                    ShowToast(m);
                    ClosePanel(root); ShowWorldEconomy(2);
                }, 19);
                PlaceTopLeft(pegPlus.rectTransform(), 408f, 108f, 44f, 38f);

                bool ammoLow = s.centralBank.forexReservesBCurrency <
                               s.centralBank.forexReserveFloor + 160f;
                ShopText(market,
                    "守汇弹药：外储 " + s.centralBank.forexReservesBCurrency.ToString("0") +
                    " B币（底线 " + s.centralBank.forexReserveFloor.ToString("0") +
                    "，单日上限 " + s.centralBank.maxDailyInterventionBCurrency.ToString("0") + "）\n" +
                    "累计干预 " + s.centralBank.cumulativeInterventionBCurrency.ToString("0") + " B币 / " +
                    s.centralBank.interventionDays + " 天。弹药见底或缺口>15% 将被迫失守。",
                    16, ammoLow ? UiTheme.Bad : UiTheme.TextSub, 22f, 152f, 570f, 70f, TextAnchor.UpperLeft);

                Image meterBg = UiTheme.MakeImage(market, new Color32(0x14, 0x1a, 0x20, 255), "reserveMeterBg");
                PlaceTopLeft(meterBg.rectTransform, 22f, 232f, 566f, 22f);
                float ratio = Mathf.Clamp01(s.centralBank.forexReservesBCurrency / 800f);
                Image meterFill = UiTheme.MakeImage(meterBg.transform,
                    ratio < .25f ? UiTheme.Bad : ratio < .5f ? WorldGold : UiTheme.Good, "reserveMeterFill");
                PlaceTopLeft(meterFill.rectTransform, 1f, 1f, 564f * Mathf.Max(.004f, ratio), 20f);

                ShopText(market,
                    "固定汇率不是写死 exchangeRate=7，而是每天用真金白银把市价拉回带内；\n" +
                    "偏离 ±3% 带外即触发干预，每一点消耗都记进国际收支账本。",
                    15, UiTheme.TextSub, 22f, 264f, 566f, 64f, TextAnchor.UpperLeft);
            }
            else
            {
                float carry = s.centralBank.policyRate - trade.countryB.policyRate -
                              trade.capitalFlow.riskPremiumPercent;
                ShopText(market,
                    "浮动制下央行不承诺点位：汇率由贸易流与资本流每日重新定价。\n" +
                    "利差渠道：本国 " + s.centralBank.policyRate.ToString("0.00") + "% − B国 " +
                    trade.countryB.policyRate.ToString("0.00") + "% − 风险溢价 " +
                    trade.capitalFlow.riskPremiumPercent.ToString("0.0") + "% = 净推力 " +
                    carry.ToString("+0.00;-0.00") + "pp（正=吸引流入升值；负=推动流出贬值）\n" +
                    "可信度 " + s.centralBank.credibility.ToString("0") +
                    "/100 会放大或折扣资本反应，也会稳定贬值预期。",
                    17, UiTheme.TextSub, 22f, 116f, 572f, 168f, TextAnchor.UpperLeft);
            }

            Button handbook = UiTheme.MakeButton(market, "知识手册 · 国际分类", () =>
            {
                handbookCategory = "国际";
                handbookPage = 0;
                CloseAll();
                ShowHandbook();
            }, 16);
            PlaceTopLeft(handbook.rectTransform(), 22f, 636f, 300f, 42f);

            RectTransform historyCard = WorldCard(c, "最近五日五项分解（百分点）", 634f, 62f, 614f, 306f, WorldGold);
            List<RateDayRecord> hist = fx.history;
            if (hist.Count == 0)
            {
                ShopText(historyCard, "尚无数据：至少结算一天。", 17, UiTheme.TextSub, 24f, 90f, 500f, 60f);
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int startRow = Mathf.Max(0, hist.Count - 5);
                for (int i = startRow; i < hist.Count; i++)
                {
                    RateDayRecord r = hist[i];
                    sb.Append("D").Append(r.worldDay).Append(" 价 ").Append(r.rate.ToString("0.00"))
                      .Append("  贸 ").Append(MoveText(r.tradeFlowEffect))
                      .Append(" 资 ").Append(MoveText(r.capitalFlowEffect))
                      .Append(" 预 ").Append(MoveText(r.expectationEffect))
                      .Append(" 干 ").Append(MoveText(r.interventionEffect))
                      .Append(" 锚 ").Append(MoveText(r.anchorPullEffect)).Append('\n');
                }
                sb.Append("\n口径：负=升值压力，正=贬值压力；Σ≈当日实际变动%。");
                ShopText(historyCard, sb.ToString(), 16, UiTheme.TextMain, 20f, 46f, 584f, 252f,
                    TextAnchor.UpperLeft);
            }

            RectTransform graphCard = WorldCard(c, "汇率走势（近 14 日）", 634f, 380f, 614f, 340f);
            BuildFxHistoryGraph(graphCard, fx);
        }

        void BuildFxHistoryGraph(RectTransform parent, ExchangeRateState fx)
        {
            int count = Mathf.Min(14, fx.history.Count);
            if (count < 2)
            {
                ShopText(parent, "至少完成两天结算后绘制。", 16, UiTheme.TextSub, 24f, 130f, 420f, 40f);
                return;
            }
            GameObject go = new GameObject("fxHistoryGraph", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PolylineGraph));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            PlaceTopLeft(rt, 78f, 54f, 520f, 240f);
            PolylineGraph graph = go.GetComponent<PolylineGraph>();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = fx.history.Count - count; i < fx.history.Count; i++)
            {
                min = Mathf.Min(min, fx.history[i].rate);
                max = Mathf.Max(max, fx.history[i].rate);
            }
            min = Mathf.Max(ExchangeRateService.MinRate - .1f, min - .05f);
            max = Mathf.Min(ExchangeRateService.MaxRate + .1f, max + .05f);
            if (max - min < .02f) max = min + .02f;
            List<Vector2> points = new List<Vector2>(count);
            for (int j = 0; j < count; j++)
            {
                float value = fx.history[fx.history.Count - count + j].rate;
                points.Add(new Vector2(count <= 1 ? 0f : j / (float)(count - 1),
                    Mathf.InverseLerp(min, max, value)));
            }
            graph.lineColor = WorldGold;
            graph.SetData(points, null, null);
            ShopText(parent, max.ToString("0.00"), 13, UiTheme.TextSub, 6f, 52f, 68f, 20f, TextAnchor.MiddleRight);
            ShopText(parent, min.ToString("0.00"), 13, UiTheme.TextSub, 6f, 274f, 68f, 20f, TextAnchor.MiddleRight);
            ShopText(parent, "← 早", 13, UiTheme.TextSub, 84f, 296f, 90f, 20f);
            ShopText(parent, "今 →", 13, UiTheme.TextSub, 500f, 296f, 96f, 20f, TextAnchor.MiddleRight);
        }

        // ——————————————————— Tab 3 海关与实验室 ———————————————————
        void BuildWorldPolicyTab(RectTransform c, GameState s, GameObject root)
        {
            GovernmentSimulator.Ensure(s);
            RectTransform tariff = WorldCard(c, "海关 · 进口关税率（从价，作用于到岸价税基）",
                12f, 62f, 610f, 330f);
            float currentRate = GovernmentSimulator.PolicyValue(s, PolicyType.ImportTariff);
            ShopText(tariff, "当前税率 " + currentRate.ToString("0") + "%", 30, WorldGold,
                24f, 48f, 300f, 50f, TextAnchor.MiddleLeft, FontStyle.Bold);
            Button tMinus = UiTheme.MakeButton(tariff, "-", () =>
            {
                GovernmentSimulator.SetPolicy(s, PolicyType.ImportTariff,
                    GovernmentSimulator.PolicyValue(s, PolicyType.ImportTariff) - 1f);
                ClosePanel(root); ShowWorldEconomy(3);
            }, 21);
            PlaceTopLeft(tMinus.rectTransform(), 344f, 50f, 52f, 46f);
            Button tPlus = UiTheme.MakeButton(tariff, "+", () =>
            {
                GovernmentSimulator.SetPolicy(s, PolicyType.ImportTariff,
                    GovernmentSimulator.PolicyValue(s, PolicyType.ImportTariff) + 1f);
                ClosePanel(root); ShowWorldEconomy(3);
            }, 21);
            PlaceTopLeft(tPlus.rectTransform(), 404f, 50f, 52f, 46f);
            ShopText(tariff,
                "范围 0–25%，步长 1pp。加税后的即时效应预告：\n" +
                "· 进口巧克力礼盒到岸价 → ¥" +
                InternationalPriceService.ImportedSkuConsumerPrice(s, .85f).ToString("0.00") +
                "\n· 国内同类企业销量 ↑；消费者福利 ↓，出现无谓损失\n" +
                "· 关税收 = 税率 × 实际完税进口额——税基会随进口收缩而缩小\n" +
                "· 若外产依赖度高，输入型通胀沿 CPI 食物/交通分项上行（宏观页可见贡献值）",
                17, UiTheme.TextMain, 22f, 116f, 566f, 200f, TextAnchor.UpperLeft);

            RectTransform labs = WorldCard(c, "经济实验室 · 国际单元", 634f, 62f, 614f, 330f, WorldGold);
            Button cmp = UiTheme.MakeButton(labs, "比较优势实验（第 44 章）", () =>
            {
                CloseAll();
                ShowComparativeAdvantageLab();
            }, 19);
            PlaceTopLeft(cmp.rectTransform(), 26f, 58f, 556f, 62f);
            UiTheme.SetButtonStyle(cmp, WorldTeal, Color.white);
            Button tri = UiTheme.MakeButton(labs, "三元悖论演示（第 53 章）", () =>
            {
                CloseAll();
                ShowTrilemmaLab();
            }, 19);
            PlaceTopLeft(tri.rectTransform(), 26f, 138f, 556f, 62f);
            UiTheme.SetButtonStyle(tri, WorldGold, UiTheme.Ink);
            ShopText(labs,
                "比较优势实验：改两组生产小时数，自动给出机会成本与专业化前后消费组合。\n" +
                "三元悖论演示：三个开关演绎为什么固定汇率/资本自由/货币独立难全。\n" +
                "两个实验都会把完成信号写入主线进度与世界状态（第 44/53 章以此结课）。",
                16, UiTheme.TextSub, 26f, 216f, 556f, 104f, TextAnchor.UpperLeft);
        }

        // ——————————————————— 比较优势实验室 ———————————————————
        public void ShowComparativeAdvantageLab()
        {
            GameState s = GameState.I;
            if (s == null) return;
            InternationalTradeService.Ensure(s);
            // bug搜#20:重建面板必须复位行游标——UIManager 是跨面板存续的单例,_hourCursor
            // 只增不减,点一次档位(回调整面板重绘)后四行 y 整体下漂 208px,三次后控件
            // 移出 800 高的面板,实验室报废不可点。
            _hourCursor = 0;
            GameObject root = BeginPanel("比较优势实验室 · 绝对优势 ≠ 比较优势", 1120f, 800f);
            RectTransform c = ContentOf(root);

            ShopText(c, "改小时数试试：哪怕 A 国样样更快，只要机会成本不同，专业化与贸易仍然双赢。",
                19, UiTheme.TextMain, 12f, 6f, 1096f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);

            RectTransform table = WorldCard(c, "生产 1 单位所需小时数（点击数值循环切换）",
                12f, 48f, 720f, 250f);
            cmpAdvCursorA = AddHourStepper(table, 24f, "A国 · 一杯饮品", cmpAdvHoursADrink,
                new[] { 1f, 2f, 3f });
            cmpAdvHoursADrink = new[] { 1f, 2f, 3f }[cmpAdvCursorA];
            cmpAdvCursorB = AddHourStepper(table, 24f, "B国 · 一杯饮品", cmpAdvHoursBDrink,
                new[] { 3f, 2f, 4f, 6f });
            cmpAdvHoursBDrink = new[] { 3f, 2f, 4f, 6f }[cmpAdvCursorB];
            int aMachineCursor = AddHourStepper(table, 380f, "A国 · 一台机器", cmpAdvHoursAMachine,
                new[] { 4f, 5f, 6f, 8f });
            cmpAdvHoursAMachine = new[] { 4f, 5f, 6f, 8f }[aMachineCursor];
            int bMachineCursor = AddHourStepper(table, 380f, "B国 · 一台机器", cmpAdvHoursBMachine,
                new[] { 6f, 4f, 8f, 12f });
            cmpAdvHoursBMachine = new[] { 6f, 4f, 8f, 12f }[bMachineCursor];

            RectTransform analysis = WorldCard(c, "机会成本（做 1 台机器放弃几杯饮品？）",
                744f, 48f, 364f, 250f, WorldGold);
            float aMachineCost = cmpAdvHoursAMachine / Mathf.Max(.5f, cmpAdvHoursADrink);
            float bMachineCost = cmpAdvHoursBMachine / Mathf.Max(.5f, cmpAdvHoursBDrink);
            ShopText(analysis,
                "A国：" + aMachineCost.ToString("0.##") + " 杯\nB国：" + bMachineCost.ToString("0.##") + " 杯\n\n" +
                (aMachineCost < bMachineCost
                    ? "⇒ 机器上 A 国比较优势更大，饮品则轮到 B 国。\n哪怕 B 国两样都更慢！"
                    : "⇒ 机器上 B 国比较优势更大。\n绝对优势在前，比较优势说了算。"),
                17, UiTheme.TextMain, 18f, 46f, 330f, 196f, TextAnchor.UpperLeft);

            RectTransform outcome = WorldCard(c, "消费组合对比（各投入 12 小时劳动力）",
                12f, 310f, 1096f, 300f);
            const float hoursPool = 12f;
            float aDrinksAutarky = hoursPool / 2f / Mathf.Max(.5f, cmpAdvHoursADrink);
            float aMachinesAutarky = hoursPool / 2f / Mathf.Max(.5f, cmpAdvHoursAMachine);
            float bDrinksAutarky = hoursPool / 2f / Mathf.Max(.5f, cmpAdvHoursBDrink);
            float bMachinesAutarky = hoursPool / 2f / Mathf.Max(.5f, cmpAdvHoursBMachine);
            bool aMakeDrinks = aMachineCost >= bMachineCost;
            float aSpecDrinks = aMakeDrinks ? hoursPool / Mathf.Max(.5f, cmpAdvHoursADrink) : 0f;
            float aSpecMachines = aMakeDrinks ? 0f : hoursPool / Mathf.Max(.5f, cmpAdvHoursAMachine);
            float bSpecDrinks = aMakeDrinks ? 0f : hoursPool / Mathf.Max(.5f, cmpAdvHoursBDrink);
            float bSpecMachines = aMakeDrinks ? hoursPool / Mathf.Max(.5f, cmpAdvHoursBMachine) : 0f;
            ShopText(outcome,
                "情景一 自给自足（每人一半时间干两件事）\n" +
                "　A国：饮品 " + aDrinksAutarky.ToString("0.#") + " ＋ 机器 " + aMachinesAutarky.ToString("0.#") +
                "　|　B国：饮品 " + bDrinksAutarky.ToString("0.#") + " ＋ 机器 " + bMachinesAutarky.ToString("0.#") +
                "\n　世界总量：饮品 " + (aDrinksAutarky + bDrinksAutarky).ToString("0.#") +
                "，机器 " + (aMachinesAutarky + bMachinesAutarky).ToString("0.#") +
                "\n\n情景二 按比较优势专业化并交换\n" +
                (aMakeDrinks
                    ? "　A国全做饮品 " + aSpecDrinks.ToString("0.#") + "；B国全做机器 " + bSpecMachines.ToString("0.#")
                    : "　A国全做机器 " + aSpecMachines.ToString("0.#") + "；B国全做饮品 " + bSpecDrinks.ToString("0.#")) +
                "\n　世界总量：饮品 " + (aSpecDrinks + bSpecDrinks).ToString("0.#") +
                "，机器 " + (aSpecMachines + bSpecMachines).ToString("0.#") +
                "\n　→ 只要交换价落在两国机会成本之间，两国可消费组合同时优于情景一（贸易利益）。",
                18, UiTheme.TextMain, 22f, 50f, 1052f, 244f, TextAnchor.UpperLeft);

            Button run = UiTheme.MakeButton(c, "我理解了：记录为已完成实验", () =>
            {
                s.worldTrade.comparativeAdvantageExperimentDone = true;
                KnowledgeSystem.MarkExperimented(s, "intl.comparativeadvantage", "intl.absoluteadvantage",
                    "intl.specialization", "intl.gainsfromtrade");
                s.UnlockCard("intl.comparativeadvantage");
                s.UnlockCard("intl.gainsfromtrade");
                ProgressionService.RecordSignal(s, ProgressSignals.ComparativeAdvantageExperimentDone,
                    "运行比较优势实验", "机器机会成本：A国 " + aMachineCost.ToString("0.##") +
                    " 杯 vs B国 " + bMachineCost.ToString("0.##"));
                SaveSystem.SaveKeyMoment(s, "比较优势实验完成");
                ShowToast("已记录为已完成实验，主线进度已更新");
                CloseAll();
                ShowWorldEconomy(3);
            }, 20);
            PlaceTopLeft(run.rectTransform(), 370f, 630f, 400f, 56f);
            UiTheme.SetButtonStyle(run, WorldTeal, Color.white);
            Button closeL = UiTheme.MakeButton(c, "关闭实验室", () => ClosePanel(root), 19);
            PlaceTopLeft(closeL.rectTransform(), 880f, 740f, 220f, 48f);
        }

        /// <summary>循环切换档位:返回新下标并立即重绘面板。</summary>
        int AddHourStepper(RectTransform parent, float columnX, string label, float current, float[] options)
        {
            int index = 0;
            for (int i = 0; i < options.Length; i++)
                if (Mathf.Approximately(options[i], current)) index = i;
            int captured = index;
            ShopText(parent, label, 17, UiTheme.TextSub, columnX + 4f, _hourCursor * 52f + 62f, 210f, 30f);
            Button b = UiTheme.MakeButton(parent, options[index].ToString("0.#") + " 小时", () =>
            {
                captured = (captured + 1) % options.Length;
                ApplyHourSelection(label, options[captured]);
                CloseAll();
                ShowComparativeAdvantageLab();
            }, 18);
            PlaceTopLeft(b.rectTransform(), columnX + 230f, _hourCursor * 52f + 56f, 170f, 42f);
            UiTheme.SetButtonStyle(b, UiTheme.Paper, UiTheme.Ink);
            _hourCursor++;
            return index;
        }

        void ApplyHourSelection(string label, float value)
        {
            if (label.Contains("A国") && label.Contains("饮品")) cmpAdvHoursADrink = value;
            else if (label.Contains("B国") && label.Contains("饮品")) cmpAdvHoursBDrink = value;
            else if (label.Contains("A国")) cmpAdvHoursAMachine = value;
            else cmpAdvHoursBMachine = value;
        }

        float _hourCursor;

        // ——————————————————— 三元悖论演示 ———————————————————
        public void ShowTrilemmaLab()
        {
            GameState s = GameState.I;
            if (s == null) return;
            InternationalTradeService.Ensure(s);
            GameObject root = BeginPanel("三元悖论演示 · 三个目标难全（PRD §53）", 1120f, 760f);
            RectTransform c = ContentOf(root);

            ShopText(c, "任意勾选两个目标都很自然；三个全开时，下面的冲突就会真实发生。",
                19, UiTheme.TextMain, 12f, 6f, 1096f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);

            RectTransform toggles = WorldCard(c, "政策开关（点击切换）", 12f, 48f, 1096f, 170f);
            AddTrilemmaToggle(toggles, 24f, "固定汇率", ref triFixedRate);
            AddTrilemmaToggle(toggles, 380f, "资本自由流动", ref triFreeCapital);
            AddTrilemmaToggle(toggles, 736f, "独立货币政策", ref triIndependentMoney);

            int activeCount = (triFixedRate ? 1 : 0) + (triFreeCapital ? 1 : 0) + (triIndependentMoney ? 1 : 0);
            RectTransform drama = WorldCard(c, "冲突推演", 12f, 230f, 1096f, 340f, WorldGold);
            string story;
            if (activeCount <= 2)
            {
                story = "当前组合是可维持的（三选二）。\n" +
                    (triFixedRate && triFreeCapital && !triIndependentMoney
                        ? "· 放弃了独立货币政策：国内衰退时也不能自由降息，否则利差反转引发资本外逃。\n"
                        : "") +
                    (triFixedRate && triIndependentMoney && !triFreeCapital
                        ? "· 必须管制资本流动来守住汇率：资本自由被牺牲。\n"
                        : "") +
                    (triFreeCapital && triIndependentMoney && !triFixedRate
                        ? "· 汇率完全交给市场浮动，无法承诺点位。\n"
                        : "") +
                    "\n试着同时打开第三个开关，看看会发生什么。";
            }
            else
            {
                story =
                    "三项全开——历史上绝大多数经济体都在这里碰壁。推演链：\n" +
                    "1. 本国衰退，央行为保就业降息（『独立货币政策』在说话）。\n" +
                    "2. 利差立刻转负：资本说走就走（『资本自由流动』放它走）。\n" +
                    "3. 外汇市场上 A币遭抛售，市价冲出偏离带；想守住承诺只能卖外储买回本币（『固定汇率』）。\n" +
                    "4. 外储是有限的——游戏里有精确账本；弹药耗尽那天，汇价一步跳贬，被迫三选二。\n" +
                    "\n现实注脚：1997 年亚洲金融危机中的联系汇率保卫战。";
            }
            ShopText(drama, story, 18, UiTheme.TextMain, 22f, 50f, 1052f, 280f, TextAnchor.UpperLeft);

            // V0.9.5 语义修正:该按钮记录的是三元悖论演示(第 53 章信号)——第 53 章不再是主线终点,
            // 措辞改为第七篇收官;恒真三元顺手移除。
            Button demo = UiTheme.MakeButton(c, "记录第七篇收官（第 53 章）", () =>
            {
                s.worldTrade.trilemmaDemoDone = true;
                KnowledgeSystem.MarkExperimented(s, "intl.trilemma", "intl.fixedrate",
                    "intl.floatingrate", "intl.capitaloutflow");
                s.UnlockCard("intl.trilemma");
                ProgressionService.RecordSignal(s, ProgressSignals.TrilemmaDemoDone,
                    "三元悖论演示", "体验固定汇率/资本自由/货币独立的『三选二』困境");
                SaveSystem.SaveKeyMoment(s, "三元悖论演示完成");
                ShowToast("已记录，第七篇迎来收官");
                CloseAll();
                ShowWorldEconomy(3);
            }, 20);
            PlaceTopLeft(demo.rectTransform(), 350f, 600f, 420f, 56f);
            UiTheme.SetButtonStyle(demo, WorldGold, UiTheme.Ink);
            Button closeT = UiTheme.MakeButton(c, "关闭演示", () => ClosePanel(root), 19);
            PlaceTopLeft(closeT.rectTransform(), 880f, 700f, 220f, 48f);
        }

        void AddTrilemmaToggle(RectTransform parent, float x, string label, ref bool value)
        {
            Button b = UiTheme.MakeButton(parent, (value ? "✅ " : "⬜ ") + label, () =>
            {
                if (label.Contains("固定")) triFixedRate = !triFixedRate;
                else if (label.Contains("自由")) triFreeCapital = !triFreeCapital;
                else triIndependentMoney = !triIndependentMoney;
                CloseAll();
                ShowTrilemmaLab();
            }, 17);
            PlaceTopLeft(b.rectTransform(), x, 52f, 330f, 92f);
            UiTheme.SetButtonStyle(b, value ? WorldTeal : UiTheme.PanelLight, value ? Color.white : UiTheme.Ink);
        }

        RectTransform WorldCard(RectTransform parent, string title, float x, float y, float w, float h,
            Color? accent = null)
        {
            Color accentColor = accent ?? WorldTeal;
            Image card = UiTheme.MakeImage(parent, WorldBg, "worldCard_" + title);
            PlaceTopLeft(card.rectTransform, x, y, w, h);
            Image stripe = UiTheme.MakeImage(card.transform, accentColor, "stripe");
            PlaceTopLeft(stripe.rectTransform, 0f, 0f, 6f, h);
            ExperienceText(card.rectTransform, title, 16, 16f, 5f, w - 28f, 26f, accentColor,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            return card.rectTransform;
        }
    }
}
