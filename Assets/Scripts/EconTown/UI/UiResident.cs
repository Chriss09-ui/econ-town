using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.3 居民生活、消费实验与生活报告 UI。</summary>
    public partial class UIManager
    {
        static readonly Color ResidentTeal = new Color32(0x4f, 0x91, 0x8b, 255);
        static readonly Color ResidentInk = new Color32(0x27, 0x24, 0x21, 255);
        static readonly Color ResidentPaper = new Color32(0xf0, 0xe1, 0xc3, 255);
        int consumerLabTab;
        // V0.9.5 资产配置面板的操作值(不落档,重开回默认——与实验室结果同口径)。
        float assetTreasuryFace = 2000f;
        float assetFundAmount = 2000f;
        float assetFundUnits = 100f;

        public void ShowResidentHome()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            if (!s.resident.profileCreated) { ShowResidentProfile(); return; }
            if (s.resident.reportReady) { ShowResidentReport(); return; }

            ResidentState r = s.resident;
            GameObject root = BeginPanel("居民生活 · 今日账本", 1180, 900);
            RectTransform c = ContentOf(root);

            ResidentText(c, r.residentName + "  ·  " + ResidentEconomy.ProfessionName(r.profession), 28,
                UiTheme.TextMain, 8, 0, 680, 38, FontStyle.Bold);
            ChapterDefinition residentChapter = ProgressionService.Current(s);
            string residentHeadline = residentChapter != null &&
                (residentChapter.part == LearningPart.Resident || residentChapter.part == LearningPart.Housing)
                ? "第 " + residentChapter.number + " 章 · " + residentChapter.title
                : "镇民生活";
            ResidentText(c, residentHeadline, 16, ResidentTeal,
                8, 38, 700, 24, FontStyle.Bold);
            ResidentText(c, "偏好：" + ResidentEconomy.PreferenceName(r.preference), 16,
                UiTheme.TextSub, 820, 8, 280, 28, FontStyle.Bold, TextAnchor.MiddleRight);

            DrawTimeLedger(c, r, 8, 72, 1100, 82);
            DrawResourceChips(c, r, 8, 168);

            RectTransform left = ResidentCard(c, "choices", 8, 242, 712, 504, new Color32(0x42, 0x35, 0x2c, 255));
            SectionHeader(left, "今天怎样生活？", "没有唯一正确答案；注意现金、时间与状态的交换。", 18, 12, 660);

            ResidentNavButton(left, "去商业街", "食物 / 饮料 / 娱乐 / 日用品", 18, 78, 324, 70,
                () => { CloseTop(); ShowResidentCommerce("食物"); });
            ResidentNavButton(left, "安排工作与闲暇", "兼职、加班、休息或学习", 366, 78, 324, 70,
                () => { CloseTop(); ShowTimeChoices(); });
            ResidentNavButton(left, "存一笔钱", "把今天的购买力留给未来", 18, 154, 324, 70,
                () => { CloseTop(); ShowSavingChoices(); });
            ResidentNavButton(left, "消费者实验", "改变收入、价格与时间看看", 366, 154, 324, 70,
                () => { CloseTop(); ShowResidentExperiments(); });
            ResidentNavButton(left, "查看需求与效用曲线", "曲线使用你自己的消费记录", 18, 230, 324, 70,
                () => { CloseTop(); ShowConsumerLab(0); });
            ResidentNavButton(left, "我的住房",
                MainlineGuide.CanEnterHousing(s) ? "租房 · 房贷 · 月供压力" : "第 54 章再安家", 366, 230, 324, 70,
                () =>
                {
                    if (!MainlineGuide.CanEnterHousing(s)) { ShowToast("第 54 章再安家"); return; }
                    CloseTop();
                    ShowHousing();
                });
            ResidentNavButton(left, "资产配置",
                MainlineGuide.CanEnterAssets(s) ? "存款 · 国债 · 指数基金" : "第 58 章再安排储蓄去向", 18, 308, 672, 54,
                () =>
                {
                    if (!MainlineGuide.CanEnterAssets(s)) { ShowToast("第 58 章再安排储蓄的去向"); return; }
                    CloseTop();
                    ShowAssetAllocation();
                });

            string last = string.IsNullOrEmpty(r.lastChoiceReason)
                ? "今天还没有选择。保留现金与时间，也是一种选择。"
                : r.lastChoiceReason;
            RectTransform note = ResidentCard(left, "lastChoice", 18, 372, 672, 62, new Color32(0x2e, 0x2a, 0x25, 255));
            ResidentText(note, "WHY · 最近一次选择", 13, ResidentTeal, 14, 6, 250, 16, FontStyle.Bold);
            ResidentText(note, last, 14, UiTheme.TextSub, 14, 24, 642, 34, FontStyle.Normal, TextAnchor.UpperLeft);

            Button endDay = UiTheme.MakeButton(left, "完成今日生活并生成日报", () =>
            {
                ResidentEconomy.EndDay(s);
                CloseTop();
                ShowResidentReport();
            }, 20);
            ResidentPlace(endDay.rectTransform(), 18, 442, 672, 48);
            endDay.image.color = ResidentTeal;
            Text endLabel = endDay.GetComponentInChildren<Text>(); if (endLabel != null) endLabel.color = Color.white;

            RectTransform right = ResidentCard(c, "dailyEvent", 738, 242, 370, 504, new Color32(0x39, 0x30, 0x29, 255));
            SectionHeader(right, "生活便笺", "事件与预算会改变选择空间。", 18, 12, 322);
            ResidentText(right, r.todayEventName, 23, UiTheme.Accent, 18, 76, 330, 32, FontStyle.Bold);
            ResidentText(right, r.todayEventDetail, 16, UiTheme.TextSub, 18, 112, 330, 55, FontStyle.Normal, TextAnchor.UpperLeft);
            LedgerLine(right, "今日消费", "¥" + r.todaySpending.ToString("0"), 186);
            LedgerLine(right, "额外收入", "+¥" + r.todayIncome.ToString("0"), 226);
            LedgerLine(right, "今日储蓄", "¥" + r.todaySaved.ToString("0"), 266);
            LedgerLine(right, "累计总效用", r.dailyUtility.ToString("0.0"), 306);
            LedgerLine(right, "消费笔数", r.todayChoices.Count + " 笔", 346);
            ResidentText(right, "企业端需求信号", 13, UiTheme.TextSub, 18, 402, 150, 22, FontStyle.Bold);
            int demandPct = Mathf.RoundToInt(s.ResidentDemandChangePercent);   // 围猎三#3:先取整再判号,-0.x% 不再显示「-0%」
            string demand = (demandPct >= 0 ? "+" : "") + demandPct + "%";
            ResidentText(right, demand, 28, demandPct >= 0 ? UiTheme.Good : UiTheme.Bad,
                185, 392, 160, 42, FontStyle.Bold, TextAnchor.MiddleRight);
            ResidentText(right, "你对果汁店的选择会进入企业销量模型。", 14, UiTheme.TextSub,
                18, 440, 330, 40, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        /// <summary>我的住房(V0.9.1《安家》,PRD §193.8):租/买状态、月供构成、DSR 仪表、净资产表。
        /// as-built:以第六张导航卡进独立面板,而非给家面板加装页签——与既有导航卡结构一致。</summary>
        public void ShowHousing()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            HousingState h = s.housing;
            GameObject root = BeginPanel("居民生活 · 我的住房", 1180, 900);
            RectTransform c = ContentOf(root);

            ResidentText(c, "一个屋檐下的经济学", 28, UiTheme.TextMain, 8, 0, 680, 38, FontStyle.Bold);
            ResidentText(c, "租房是灵活性，买房是三十年的账。", 16, ResidentTeal, 8, 38, 700, 24, FontStyle.Bold);

            RectTransform left = ResidentCard(c, "status", 8, 80, 712, 560, new Color32(0x42, 0x35, 0x2c, 255));
            if (h.renting)
            {
                SectionHeader(left, "租房中", "每期租金自动从存款划转房东", 18, 12, 660);
                LedgerLine(left, "每期租金", "¥" + h.periodRent.ToString("0"), 70);
                LedgerLine(left, "连续欠租", h.rentMissed + " 期（连续 3 期被迫搬离）", 110);
                LedgerLine(left, "存款余额", "¥" + s.resident.savings.ToString("0"), 150);
                LedgerLine(left, "镇房价", "¥" + h.townHousePrice.ToString("0") + " / 套", 190);
                if (h.pendingSaleEta > 0f)
                    LedgerLine(left, "在途房款", "¥" + h.pendingSaleProceeds.ToString("0") + "（" +
                        h.pendingSaleEta.ToString("0") + " 期后到账）", 230);
                else
                    ResidentText(left, "搬家自由是租房的期权：上涨的房价与你无关，下跌的也一样。", 15, UiTheme.TextSub,
                        18, 232, 660, 30, FontStyle.Normal, TextAnchor.UpperLeft);

                RectTransform buyCard = ResidentCard(left, "buy", 18, 276, 672, 106, new Color32(0x2e, 0x2a, 0x25, 255));
                // 修复(找BUG未验证#1):预告改用 LtvEff 实时成数(金融压力↑→银行接受成数↓→首付门槛↑),
                // 与 TryBuyHouse 放款同口径——固定 30% 在压力期与真实成交数字不符。
                float ltv = HousingService.LtvEff(s);
                float down = h.townHousePrice * (1f - ltv);
                float principal = h.townHousePrice * ltv;
                float rate = s.bank.householdLoanRate + MortgageService.RateSpreadPp;
                float pay = MortgageService.AnnuityPayment(principal, rate, MortgageService.TermPeriods);
                ResidentText(buyCard, "买下它：首付 ¥" + down.ToString("0") + "（" + ((1f - ltv) * 100f).ToString("0") +
                    "%）＋ 按揭 ¥" + principal.ToString("0") +
                    "（" + MortgageService.TermPeriods + " 期 · 年化 " + rate.ToString("0.0") + "% · 每期供款约 ¥" + pay.ToString("0") + "）",
                    15, UiTheme.TextMain, 14, 8, 644, 30, FontStyle.Normal, TextAnchor.UpperLeft);
                ResidentText(buyCard, "首付越厚，杠杆越低：房价涨是放大器，跌也是。", 13, UiTheme.TextSub,
                    14, 38, 644, 22, FontStyle.Normal, TextAnchor.UpperLeft);
                Button buy = UiTheme.MakeButton(buyCard, "申请房贷购房", () =>
                {
                    string msg;
                    bool ok = MortgageService.TryBuyHouse(s, out msg);
                    if (UIManager.I != null) UIManager.I.ShowToast(msg);
                    CloseTop();
                    if (ok) ShowResidentHome(); else ShowHousing();
                }, 16);
                ResidentPlace(buy.rectTransform(), 452, 62, 200, 36);
                buy.image.color = ResidentTeal;
                Text buyLabel = buy.GetComponentInChildren<Text>(); if (buyLabel != null) buyLabel.color = Color.white;
            }
            else
            {
                SectionHeader(left, "自有住房", "月供构成与还款进度", 18, 12, 660);
                LedgerLine(left, "估值 / 购入价", "¥" + h.homeAppraisal.ToString("0") + " / ¥" + h.purchasePrice.ToString("0"), 70);
                LedgerLine(left, "已付首付", "¥" + h.downPaymentPaid.ToString("0"), 110);
                LoanRecord loan = HousingService.CurrentMortgage(s);
                if (loan != null)
                {
                    float pay = MortgageService.AnnuityPayment(loan.principal, loan.annualInterestRate, loan.term);
                    LedgerLine(left, "剩余本金 / 剩余期数", "¥" + loan.remainingPrincipal.ToString("0") + " / " + loan.remainingTerm + " 期", 150);
                    LedgerLine(left, "每期供款（固定）", "¥" + pay.ToString("0"), 190);
                    LedgerLine(left, "累计已还 本金/利息", "¥" + loan.totalPrincipalPaid.ToString("0") + " / ¥" + loan.totalInterestPaid.ToString("0"), 230);
                }
                else
                {
                    LedgerLine(left, "房贷", "已结清——房子完全属于你", 150);
                }
                DayRecord last = s.LastRecord;
                float mp = last != null ? last.mortgagePrincipalPaid : 0f;
                float mi = last != null ? last.mortgageInterestPaid : 0f;
                ResidentText(left, "最近一期供款：本金 ¥" + mp.ToString("0") + " ＋ 利息 ¥" + mi.ToString("0") +
                    "——每期总供款不变，本金占比逐期上升（等额本息）。", 14, UiTheme.TextSub,
                    18, 272, 660, 34, FontStyle.Normal, TextAnchor.UpperLeft);
                if (loan != null)
                {
                    Button payoff = UiTheme.MakeButton(left, "提前还清（余额 ¥" + loan.remainingPrincipal.ToString("0") + "）", () =>
                    {
                        string msg;
                        MortgageService.TryEarlyPayoff(s, out msg);
                        if (UIManager.I != null) UIManager.I.ShowToast(msg);
                        CloseTop();
                        ShowHousing();
                    }, 15);
                    ResidentPlace(payoff.rectTransform(), 18, 326, 400, 38);
                }
                // V0.9.3 卖房(§196.5):下跌期折价 8%＋延迟 3 期,按钮文案预告约束
                bool deflating = h.leveragePhase == LeveragePhase.Deflating;
                Button sell = UiTheme.MakeButton(left, deflating ? "卖房（折价 8%＋延迟 3 期）" : "卖房（全额变现）", () =>
                {
                    string msg;
                    MortgageService.TrySellHouse(s, out msg);
                    if (UIManager.I != null) UIManager.I.ShowToast(msg);
                    CloseTop();
                    ShowHousing();
                }, 14);
                ResidentPlace(sell.rectTransform(), loan != null ? 430 : 18, 326, loan != null ? 264 : 676, 38);
                sell.image.color = deflating ? UiTheme.Bad : ResidentTeal;
                Text sellLabel = sell.GetComponentInChildren<Text>();
                if (sellLabel != null) sellLabel.color = Color.white;
                if (deflating)
                    ResidentText(left, "反转下行期：买家观望、变现要折价、房款还要在路上等——流动性风险在规则里，不在文案里。",
                        13, UiTheme.Bad, 18, 368, 660, 26, FontStyle.Normal, TextAnchor.UpperLeft);
            }

            // V0.9.5 房价分解(第 56 章信号,PRD §197.2):面板读到非空分解即计数——
            // 读数与计数分离,采集器只读不赠(R26)。
            RectTransform breakdownCard = ResidentCard(left, "breakdown", 18, 400, 672, 144,
                new Color32(0x2e, 0x2a, 0x25, 255));
            HousingService.PriceBreakdownInfo? breakdown = HousingService.PriceBreakdown(s);
            if (breakdown != null)
            {
                HousingService.MarkPriceBreakdownRead(s);
                HousingService.PriceBreakdownInfo b = breakdown.Value;
                ResidentText(breakdownCard, "房价分解 · 第 " + b.period + " 期", 13, ResidentTeal,
                    14, 8, 644, 20, FontStyle.Bold);
                ResidentText(breakdownCard,
                    "需求 " + b.demand.ToString("0.0") + " 套 − 供给 " + b.supply.ToString("0.0") + " 套 ＝ 失衡推价 Δ¥" +
                    b.priceDelta.ToString("0") + "（每期钳 ±3%）· 当期租金 ¥" + b.periodRent.ToString("0"),
                    14, UiTheme.TextMain, 14, 30, 644, 26, FontStyle.Normal, TextAnchor.UpperLeft);
                ResidentText(breakdownCard,
                    "谁在推着房价走：人口与收入进需求，开工进供给，信贷把两边都放大；涨过 CPI 的才是真涨。",
                    13, UiTheme.TextSub, 14, 58, 644, 40, FontStyle.Normal, TextAnchor.UpperLeft);
            }
            else
            {
                ResidentText(breakdownCard, "房价分解", 13, ResidentTeal, 14, 8, 644, 20, FontStyle.Bold);
                ResidentText(breakdownCard,
                    "结算一期后，这里能看到需求/供给/Δ价/租金的真实分解——每一项都来自房价引擎账本。",
                    14, UiTheme.TextSub, 14, 30, 644, 40, FontStyle.Normal, TextAnchor.UpperLeft);
            }

            RectTransform right = ResidentCard(c, "dsr", 738, 80, 370, 246, new Color32(0x39, 0x30, 0x29, 255));
            SectionHeader(right, "月供压力 DSR", "每期供款 ÷ 每期收入（居民＋企业）", 18, 12, 322);
            float dsr = HousingService.Dsr(s);
            int band = HousingService.DsrBand(s);
            ResidentText(right, (dsr * 100f).ToString("0") + "%", 42,
                band == 2 ? UiTheme.Bad : band == 1 ? UiTheme.Accent : UiTheme.Good,
                18, 64, 180, 54, FontStyle.Bold, TextAnchor.MiddleLeft);
            ResidentText(right,
                band == 0 ? "＜25% 轻松" : band == 1 ? "25–40% 有压力：日终精力恢复变慢" : "＞40% 高压：解锁「加班兼职」",
                14, UiTheme.TextSub, 18, 124, 330, 40, FontStyle.Normal, TextAnchor.UpperLeft);

            RectTransform wealth = ResidentCard(c, "wealth", 738, 342, 370, 246, new Color32(0x39, 0x30, 0x29, 255));
            SectionHeader(wealth, "净资产", "存款 ＋ 房产净值", 18, 12, 322);
            LedgerLine(wealth, "存款", "¥" + s.resident.savings.ToString("0"), 64);
            LedgerLine(wealth, "房产净值", "¥" + HousingService.HomeEquity(s).ToString("0"), 104);
            float total = s.resident.savings + HousingService.HomeEquity(s);
            LedgerLine(wealth, "合计", "¥" + total.ToString("0"), 144);
            ResidentText(wealth, "房价上涨≠真实财富——变现需要接盘者。", 13, UiTheme.TextSub,
                18, 190, 330, 34, FontStyle.Normal, TextAnchor.UpperLeft);

            RectTransform note = ResidentCard(c, "note", 738, 604, 370, 148, new Color32(0x2e, 0x2a, 0x25, 255));
            ResidentText(note, "WHY · 租还是买", 13, ResidentTeal, 14, 8, 250, 20, FontStyle.Bold);
            ResidentText(note,
                "租房的钱是消费，买房的首付与月供本金是资产转移、利息是成本。决定的不是「哪个划算」，而是你愿意把多少人生锁进一个屋檐。",
                14, UiTheme.TextSub, 14, 30, 342, 96, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        /// <summary>资产配置(V0.9.5《第八篇》,PRD §197.5):储蓄三去向简表——存款/国债/镇景指数基金。
        /// 简表起步(饼图与累计收益线留后续);每笔操作走 AssetAllocationService 四腿守恒记账。</summary>
        public void ShowAssetAllocation()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            GovernmentSimulator.Ensure(s);
            AssetAllocationService.Ensure(s);
            HousingState h = s.housing;
            GameObject root = BeginPanel("居民生活 · 资产配置", 1180, 900);
            RectTransform c = ContentOf(root);

            ResidentText(c, "钱放在哪里", 28, UiTheme.TextMain, 8, 0, 680, 38, FontStyle.Bold);
            ResidentText(c, "收益、风险与流动性的三角——存款随取、国债吃息、基金求增长。", 16, ResidentTeal,
                8, 38, 760, 24, FontStyle.Bold);

            // 左:三档持仓简表(全部来自真实账本)
            RectTransform table = ResidentCard(c, "table", 8, 80, 712, 420, new Color32(0x42, 0x35, 0x2c, 255));
            SectionHeader(table, "三档持仓", "每笔操作都双边入账,平衡表零扰动", 18, 12, 660);
            float fundValue = h.indexUnits * h.indexLevel / 100f;
            float financial = s.resident.savings + h.treasuryFace + fundValue;
            LedgerLine(table, "① 银行存款", "¥" + s.resident.savings.ToString("0") +
                "（随取随用 · 年化 " + s.bank.depositRate.ToString("0.00") + "%）", 70);
            LedgerLine(table, "② 国债（面值）", "¥" + h.treasuryFace.ToString("0") +
                "（政府按期付息 · 年化 " + s.government.averageDebtRate.ToString("0.0") + "%）", 110);
            LedgerLine(table, "③ 镇景指数基金", "¥" + fundValue.ToString("0") +
                "（" + h.indexUnits.ToString("0.0") + " 份 × 指数 " + h.indexLevel.ToString("0.0") + "）", 150);
            LedgerLine(table, "金融资产合计", "¥" + financial.ToString("0") +
                "（另有房产净值 ¥" + HousingService.HomeEquity(s).ToString("0") + "）", 190);
            ResidentText(table,
                "镇景指数本期读数:0.3 × 实际增速(" + s.macro.gdpGrowth.ToString("0.0") + "%) + 0.2 × (信心−68)/68(" +
                s.government.enterpriseConfidence.ToString("0") + "),每期钳 ±6%——分散不掉的是整个镇子的景气。",
                13, UiTheme.TextSub, 18, 236, 660, 44, FontStyle.Normal, TextAnchor.UpperLeft);
            ResidentText(table,
                "指数基金按市价计值:赎回款 = 份额 × 指数/100 × (1−0.5% 价差)。封闭单银行经济里,银行是基金的唯一对手方。",
                13, UiTheme.TextSub, 18, 288, 660, 44, FontStyle.Normal, TextAnchor.UpperLeft);
            ResidentText(table,
                "国债利息随政府周期到账:你持有的份额按 treasuryFace ÷ 政府债务分摊——政府的每一期付息,都有你的一份。",
                13, UiTheme.TextSub, 18, 340, 660, 44, FontStyle.Normal, TextAnchor.UpperLeft);

            // 右上:国债操作卡
            RectTransform treasuryCard = ResidentCard(c, "treasury", 738, 80, 370, 260, new Color32(0x39, 0x30, 0x29, 255));
            SectionHeader(treasuryCard, "认购国债", "面额制 · 政府融资的最小入口", 18, 12, 322);
            MakeAssetStepper(treasuryCard, AssetField.TreasuryFace, 500f, 100f, 20000f, 62);
            Button buyTreasury = UiTheme.MakeButton(treasuryCard, "认购 ¥" + assetTreasuryFace.ToString("0"), () =>
            {
                string msg;
                AssetAllocationService.TryBuyTreasury(s, assetTreasuryFace, out msg);
                if (UIManager.I != null) UIManager.I.ShowToast(msg);
                CloseTop();
                ShowAssetAllocation();
            }, 16);
            ResidentPlace(buyTreasury.rectTransform(), 18, 150, 334, 42);
            buyTreasury.image.color = ResidentTeal;
            Text btLabel = buyTreasury.GetComponentInChildren<Text>(); if (btLabel != null) btLabel.color = Color.white;
            ResidentText(treasuryCard, "国债 = 政府以税收权背书的借款,是全镇利率的地基。",
                13, UiTheme.TextSub, 18, 204, 330, 44, FontStyle.Normal, TextAnchor.UpperLeft);

            // 右中:基金操作卡
            RectTransform fundCard = ResidentCard(c, "fund", 738, 356, 370, 320, new Color32(0x39, 0x30, 0x29, 255));
            SectionHeader(fundCard, "镇景指数基金", "起购 ¥1000 · 申赎价差 0.5%", 18, 12, 322);
            MakeAssetStepper(fundCard, AssetField.FundAmount, 500f, 1000f, 50000f, 62);
            Button buyFund = UiTheme.MakeButton(fundCard, "申购 ¥" + assetFundAmount.ToString("0"), () =>
            {
                string msg;
                AssetAllocationService.TryBuyFund(s, assetFundAmount, out msg);
                if (UIManager.I != null) UIManager.I.ShowToast(msg);
                CloseTop();
                ShowAssetAllocation();
            }, 16);
            ResidentPlace(buyFund.rectTransform(), 18, 150, 334, 42);
            buyFund.image.color = ResidentTeal;
            Text bfLabel = buyFund.GetComponentInChildren<Text>(); if (bfLabel != null) bfLabel.color = Color.white;

            MakeAssetStepper(fundCard, AssetField.FundUnits, 50f, 50f, 100000f, 208);
            Button redeem = UiTheme.MakeButton(fundCard, "赎回 " + assetFundUnits.ToString("0") + " 份", () =>
            {
                string msg;
                AssetAllocationService.TryRedeemFund(s, assetFundUnits, out msg);
                if (UIManager.I != null) UIManager.I.ShowToast(msg);
                CloseTop();
                ShowAssetAllocation();
            }, 16);
            ResidentPlace(redeem.rectTransform(), 18, 262, 334, 42);

            // 右下:WHY 便签
            RectTransform why = ResidentCard(c, "why", 738, 692, 370, 88, new Color32(0x2e, 0x2a, 0x25, 255));
            ResidentText(why, "WHY · 三档的分工", 13, ResidentTeal, 14, 8, 250, 18, FontStyle.Bold);
            ResidentText(why, "没有「最好的一档」:应急的钱要流动性,保本的钱要确定性,长钱才配得上波动。份额现值随指数浮动,赎回才把浮动变成现金。",
                13, UiTheme.TextSub, 14, 28, 342, 52, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        enum AssetField { TreasuryFace, FundAmount, FundUnits }

        float AssetFieldValue(AssetField field)
        {
            return field == AssetField.TreasuryFace ? assetTreasuryFace :
                field == AssetField.FundAmount ? assetFundAmount : assetFundUnits;
        }

        void BumpAssetField(AssetField field, float delta, float min, float max)
        {
            if (field == AssetField.TreasuryFace)
                assetTreasuryFace = Mathf.Clamp(assetTreasuryFace + delta, min, max);
            else if (field == AssetField.FundAmount)
                assetFundAmount = Mathf.Clamp(assetFundAmount + delta, min, max);
            else
                assetFundUnits = Mathf.Clamp(assetFundUnits + delta, min, max);
        }

        /// <summary>金额/份额步进器(±step,钳 [min,max];点击后重建面板刷新读数)。</summary>
        void MakeAssetStepper(RectTransform parent, AssetField field, float step, float min, float max, float y)
        {
            Button minus = UiTheme.MakeButton(parent, "−", () =>
            {
                BumpAssetField(field, -step, min, max);
                CloseTop();
                ShowAssetAllocation();
            }, 18);
            ResidentPlace(minus.rectTransform(), 18, y, 52, 36);
            ResidentText(parent, AssetFieldValue(field).ToString("0") + (step >= 500f ? " 元" : " 份"),
                17, UiTheme.TextMain, 74, y + 5, 120, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Button plus = UiTheme.MakeButton(parent, "＋", () =>
            {
                BumpAssetField(field, step, min, max);
                CloseTop();
                ShowAssetAllocation();
            }, 18);
            ResidentPlace(plus.rectTransform(), 200, y, 52, 36);
        }

        void ShowResidentProfile()
        {
            GameState s = GameState.I;
            ResidentProfession profession = ResidentProfession.StoreClerk;
            ResidentPreference preference = ResidentPreference.Foodie;
            GameObject root = BeginPanel("创建居民档案", 820, 690);
            RectTransform c = ContentOf(root);
            ResidentText(c, "给镇上的自己起个名字", 28, UiTheme.TextMain,
                20, 4, 730, 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            ResidentText(c, "这是口袋里的另一本账，不会动果汁店的资金。", 16, UiTheme.TextSub,
                20, 45, 730, 30, FontStyle.Normal, TextAnchor.MiddleCenter);

            ResidentText(c, "居民名字", 16, ResidentTeal, 40, 92, 180, 24, FontStyle.Bold);
            InputField nameInput = MakeResidentInput(c, "小经", 40, 122, 700, 52);

            ResidentText(c, "职业 · 决定每日基础收入", 16, ResidentTeal, 40, 200, 350, 24, FontStyle.Bold);
            string[] professionLabels = { "市场帮工 · ¥60", "小镇配送员 · ¥70", "自由创作者 · ¥82" };
            Button[] professionButtons = new Button[3];
            for (int i = 0; i < professionButtons.Length; i++)
            {
                int idx = i;
                professionButtons[i] = UiTheme.MakeButton(c, professionLabels[i], () =>
                {
                    profession = (ResidentProfession)idx;
                    for (int j = 0; j < professionButtons.Length; j++) professionButtons[j].image.color = j == idx ? ResidentTeal : ResidentPaper;
                }, 17);
                ResidentPlace(professionButtons[i].rectTransform(), 40 + i * 235, 232, 220, 52);
            }
            professionButtons[0].image.color = ResidentTeal;

            ResidentText(c, "生活偏好 · 影响同样消费带来的效用", 16, ResidentTeal, 40, 316, 410, 24, FontStyle.Bold);
            string[] preferenceLabels = { "美食爱好者", "娱乐优先", "稳健储蓄" };
            Button[] preferenceButtons = new Button[3];
            for (int i = 0; i < preferenceButtons.Length; i++)
            {
                int idx = i;
                preferenceButtons[i] = UiTheme.MakeButton(c, preferenceLabels[i], () =>
                {
                    preference = (ResidentPreference)idx;
                    for (int j = 0; j < preferenceButtons.Length; j++) preferenceButtons[j].image.color = j == idx ? ResidentTeal : ResidentPaper;
                }, 18);
                ResidentPlace(preferenceButtons[i].rectTransform(), 40 + i * 235, 348, 220, 52);
            }
            preferenceButtons[0].image.color = ResidentTeal;

            RectTransform starting = ResidentCard(c, "start", 40, 430, 700, 84, new Color32(0x38, 0x31, 0x2a, 255));
            ResidentText(starting, "开局账本", 14, ResidentTeal, 16, 8, 130, 20, FontStyle.Bold);
            ResidentText(starting, "现金 ¥100   ·   储蓄 ¥1000   ·   每日自由时间 4 小时", 20,
                UiTheme.TextMain, 16, 31, 668, 38, FontStyle.Bold, TextAnchor.MiddleCenter);

            Button create = UiTheme.MakeButton(c, "开始居民生活", () =>
            {
                ResidentEconomy.CreateProfile(s, nameInput.text, profession, preference);
                CloseTop();
                ShowResidentHome();
            }, 23);
            ResidentPlace(create.rectTransform(), 180, 548, 440, 58);
            create.image.color = UiTheme.Accent;
        }

        public void ShowResidentCommerce(string category)
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            if (!s.resident.profileCreated) { ShowResidentProfile(); return; }

            string[] categories = { "食物", "饮料", "娱乐", "日用品" };
            List<ResidentItemDef> shown = new List<ResidentItemDef>();
            IList<ResidentItemDef> defs = ResidentEconomy.Items;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].category == category) shown.Add(defs[i]);

            float panelHeight = shown.Count > 2 ? 880f : 620f;
            GameObject root = BeginPanel("商业街 · 消费选择", 1180, panelHeight);
            RectTransform c = ContentOf(root);
            for (int i = 0; i < categories.Length; i++)
            {
                string tab = categories[i];
                Button b = UiTheme.MakeButton(c, tab, () => { CloseTop(); ShowResidentCommerce(tab); }, 19);
                ResidentPlace(b.rectTransform(), 14 + i * 174, 4, 160, 48);
                if (tab == category)
                {
                    b.image.color = ResidentTeal;
                    Text bt = b.GetComponentInChildren<Text>(); if (bt != null) bt.color = Color.white;
                }
            }
            ResidentText(c, "现金 ¥" + s.resident.cash.ToString("0") + "  ·  剩余 " + s.resident.freeHours.ToString("0.##") + " 小时",
                19, UiTheme.TextMain, 750, 4, 350, 48, FontStyle.Bold, TextAnchor.MiddleRight);

            for (int i = 0; i < shown.Count; i++)
            {
                ResidentItemDef def = shown[i];
                bool centeredLastCard = shown.Count % 2 == 1 && i == shown.Count - 1;
                float x = centeredLastCard ? 298f : 14 + (i % 2) * 555;
                float y = 76 + (i / 2) * 238;
                DrawCommerceChoice(c, s, def, x, y, 535, 216, category);
            }

            float noteY = shown.Count > 2 ? 570 : 332;
            RectTransform note = ResidentCard(c, "commerceNote", 14, noteY, 1090, 116, new Color32(0x32, 0x2d, 0x28, 255));
            ResidentText(note, "选择提示", 14, ResidentTeal, 16, 8, 130, 22, FontStyle.Bold);
            string message = category == "饮料"
                ? "果汁店与 Sunny Tea 是替代品：相对价格变化会改变你去哪家，也会改变企业端销量。"
                : category == "娱乐" ? "电影与爆米花是互补品；一起消费时，爆米花的边际效用会提高。"
                : "基础款和升级款都能满足需要；你的预算、偏好和剩余时间共同决定选择。";
            ResidentText(note, message, 17, UiTheme.TextSub, 16, 34, 1058, 56, FontStyle.Normal, TextAnchor.UpperLeft);
            ResidentText(note, "已选 " + s.resident.todayChoices.Count + " 笔 · 总效用 " + s.resident.dailyUtility.ToString("0.0"),
                16, UiTheme.Accent, 680, 82, 390, 24, FontStyle.Bold, TextAnchor.MiddleRight);

            Button home = UiTheme.MakeButton(c, "返回生活账本", () => { CloseTop(); ShowResidentHome(); }, 20);
            ResidentPlace(home.rectTransform(), 430, noteY + 136f, 300, 48);
        }

        void DrawCommerceChoice(RectTransform parent, GameState s, ResidentItemDef def,
            float x, float y, float w, float h, string category)
        {
            RectTransform card = ResidentCard(parent, def.kind.ToString(), x, y, w, h,
                def.premium ? new Color32(0x55, 0x3e, 0x2e, 255) : new Color32(0x42, 0x38, 0x31, 255));
            ResidentText(card, def.name, 23, UiTheme.TextMain, 18, 12, 330, 34, FontStyle.Bold);
            ResidentText(card, def.premium ? "升级选择" : "基础选择", 13, def.premium ? UiTheme.Accent : ResidentTeal,
                w - 130, 14, 110, 25, FontStyle.Bold, TextAnchor.MiddleRight);
            float price = ResidentEconomy.Price(s, def.kind);
            ResidentText(card, "¥" + price.ToString("0") + "   ·   " + def.hours.ToString("0.##") + " 小时",
                20, UiTheme.Accent, 18, 54, 300, 30, FontStyle.Bold);
            int previous = CategoryCountForUi(s.resident, def.category);
            float estimate = def.baseUtility / (1f + previous * .65f);
            ResidentText(card, "预计新增效用约 " + estimate.ToString("0.0") + (previous > 0 ? "（同类消费递减中）" : ""),
                15, UiTheme.TextSub, 18, 88, w - 36, 28, FontStyle.Normal);
            ResidentText(card, ResourceEffect(def), 14, UiTheme.TextSub, 18, 118, w - 190, 48, FontStyle.Normal, TextAnchor.UpperLeft);
            Button choose = UiTheme.MakeButton(card, "选择", () =>
            {
                ResidentActionResult result = ResidentEconomy.Consume(s, def.kind);
                ShowToast(result.message);
                if (result.success) { CloseTop(); ShowResidentCommerce(category); }
            }, 19);
            ResidentPlace(choose.rectTransform(), w - 152, 132, 132, 54);
            if (s.resident.cash < price || s.resident.freeHours < def.hours) choose.interactable = false;
        }

        void ShowTimeChoices()
        {
            GameState s = GameState.I;
            ResidentState r = s.resident;
            GameObject root = BeginPanel("工作与闲暇 · 24 小时怎么分", 960, 720);
            RectTransform c = ContentOf(root);
            DrawTimeLedger(c, r, 20, 12, 870, 82);
            ResidentText(c, "每个选择都在收入、精力和幸福感之间交换。", 17, UiTheme.TextSub,
                20, 108, 870, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
            TimeChoice(c, "接一小时零工", "+¥15  ·  精力 -8  ·  幸福 -3", 20, 162, () => ResidentEconomy.Work(s, false));
            TimeChoice(c, "加班两小时", "+¥34  ·  精力 -18  ·  幸福 -8", 470, 162, () => ResidentEconomy.Work(s, true));
            TimeChoice(c, "休息一小时", "精力 +14  ·  幸福 +10", 20, 284, () => ResidentEconomy.Leisure(s, false));
            TimeChoice(c, "学习两小时", "效用 +17  ·  长期能力感", 470, 284, () => ResidentEconomy.Leisure(s, true));
            RectTransform why = ResidentCard(c, "whyTime", 20, 420, 870, 90, new Color32(0x32, 0x2d, 0x28, 255));
            ResidentText(why, "机会成本", 14, ResidentTeal, 16, 8, 150, 22, FontStyle.Bold);
            ResidentText(why, "多工作获得的收入看得见，被放弃的休息同样有价值。适合你的答案取决于偏好与当日状态。",
                16, UiTheme.TextSub, 16, 32, 838, 46, FontStyle.Normal, TextAnchor.UpperLeft);
            Button back = UiTheme.MakeButton(c, "返回生活账本", () => { CloseTop(); ShowResidentHome(); }, 20);
            ResidentPlace(back.rectTransform(), 330, 548, 300, 48);
        }

        void TimeChoice(RectTransform parent, string title, string detail, float x, float y,
            System.Func<ResidentActionResult> action)
        {
            RectTransform card = ResidentCard(parent, title, x, y, 420, 102, new Color32(0x45, 0x38, 0x2f, 255));
            ResidentText(card, title, 21, UiTheme.TextMain, 16, 8, 245, 32, FontStyle.Bold);
            ResidentText(card, detail, 14, UiTheme.TextSub, 16, 43, 270, 36, FontStyle.Normal, TextAnchor.UpperLeft);
            Button b = UiTheme.MakeButton(card, "选择", () =>
            {
                ResidentActionResult result = action();
                ShowToast(result.message);
                if (result.success) { CloseTop(); ShowTimeChoices(); }
            }, 18);
            ResidentPlace(b.rectTransform(), 302, 22, 100, 56);
        }

        void ShowSavingChoices()
        {
            GameState s = GameState.I;
            ResidentState r = s.resident;
            GameObject root = BeginPanel("储蓄 · 把购买力留给未来", 760, 580);
            RectTransform c = ContentOf(root);
            ResidentText(c, "现金", 15, UiTheme.TextSub, 36, 20, 150, 22, FontStyle.Bold);
            ResidentText(c, "¥" + r.cash.ToString("0"), 34, UiTheme.Accent, 36, 46, 250, 48, FontStyle.Bold);
            ResidentText(c, "已有储蓄", 15, UiTheme.TextSub, 410, 20, 180, 22, FontStyle.Bold);
            ResidentText(c, "¥" + r.savings.ToString("0"), 34, ResidentTeal, 410, 46, 250, 48, FontStyle.Bold);
            ResidentText(c, "存钱会收紧今天的预算线，却扩大未来的选择空间。", 18, UiTheme.TextMain,
                36, 122, 650, 44, FontStyle.Normal, TextAnchor.MiddleCenter);
            SavingButton(c, s, 20f, 36, 198);
            SavingButton(c, s, 50f, 370, 198);
            SavingButton(c, s, Mathf.Floor(r.cash * .5f), 36, 290, "存入一半现金");
            Button back = UiTheme.MakeButton(c, "暂时不存 · 返回", () => { CloseTop(); ShowResidentHome(); }, 20);
            ResidentPlace(back.rectTransform(), 370, 290, 300, 58);
            ResidentText(c, "储蓄不是分数奖励。留得太多可能牺牲当下需要，花得太多则会降低未来安全感。",
                15, UiTheme.TextSub, 36, 382, 650, 56, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        void SavingButton(RectTransform parent, GameState s, float amount, float x, float y, string custom = null)
        {
            Button b = UiTheme.MakeButton(parent, custom ?? "存入 ¥" + amount.ToString("0"), () =>
            {
                ResidentActionResult result = ResidentEconomy.Save(s, amount);
                ShowToast(result.message);
                if (result.success) { CloseTop(); ShowResidentHome(); }
            }, 20);
            ResidentPlace(b.rectTransform(), x, y, 300, 58);
            b.interactable = amount > 0f && s.resident.cash >= amount;
        }

        void ShowResidentReport()
        {
            GameState s = GameState.I;
            ResidentEconomy.Ensure(s);
            ResidentState r = s.resident;
            if (!r.reportReady || r.history.Count == 0) { ShowResidentHome(); return; }
            ResidentDayRecord d = r.history[r.history.Count - 1];
            GameObject root = BeginPanel("第 " + d.day + " 日生活报告", 1120, 850);
            RectTransform c = ContentOf(root);

            RectTransform summary = ResidentCard(c, "summary", 10, 4, 1048, 104, new Color32(0x43, 0x38, 0x2f, 255));
            ResidentText(summary, d.summary, 19, UiTheme.TextMain, 18, 12, 1012, 48, FontStyle.Bold, TextAnchor.UpperLeft);
            ResidentText(summary, string.IsNullOrEmpty(d.eventName) ? "普通工作日" : d.eventName, 14,
                ResidentTeal, 18, 70, 1012, 22, FontStyle.Bold);

            RectTransform ledger = ResidentCard(c, "ledger", 10, 126, 500, 390, new Color32(0x38, 0x30, 0x29, 255));
            SectionHeader(ledger, "收支与资源", "今天的约束留下了哪些结果？", 16, 10, 452);
            LedgerLine(ledger, "现金", "¥" + d.cashStart.ToString("0") + " → ¥" + d.cashEnd.ToString("0"), 74);
            LedgerLine(ledger, "消费", "-¥" + d.spending.ToString("0"), 116);
            LedgerLine(ledger, "额外收入", "+¥" + d.income.ToString("0"), 158);
            string savingsFlow = d.emergencyWithdrawal > 0f ?
                "应急取出 ¥" + d.emergencyWithdrawal.ToString("0") : "转入 ¥" + d.saved.ToString("0");
            LedgerLine(ledger, "储蓄变动", savingsFlow, 200);
            LedgerLine(ledger, "使用自由时间", d.freeHoursUsed.ToString("0.##") + " / 4 小时", 242);
            LedgerLine(ledger, "总效用", d.totalUtility.ToString("0.0"), 284);
            LedgerLine(ledger, "幸福 / 精力", d.happinessEnd.ToString("0") + " / " + d.energyEnd.ToString("0"), 326);

            RectTransform choices = ResidentCard(c, "choices", 530, 126, 528, 390, new Color32(0x38, 0x30, 0x29, 255));
            SectionHeader(choices, "消费明细", "总效用与边际效用来自真实选择。", 16, 10, 480);
            if (d.choices.Count == 0)
            {
                ResidentText(choices, "今天没有消费。你保留了现金与时间。", 18, UiTheme.TextSub,
                    20, 100, 488, 60, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            else
            {
                int visible = Mathf.Min(6, d.choices.Count);
                for (int i = 0; i < visible; i++)
                {
                    ResidentChoiceRecord choice = d.choices[i];
                    ResidentText(choices, (i + 1) + ". " + choice.itemName, 16, UiTheme.TextMain,
                        18, 70 + i * 48, 250, 26, FontStyle.Bold);
                    ResidentText(choices, "¥" + choice.price.ToString("0") + "  ·  MU " + choice.marginalUtility.ToString("0.0") + "  ·  剩余 " + choice.consumerSurplus.ToString("0.0"),
                        14, UiTheme.TextSub, 260, 70 + i * 48, 245, 26, FontStyle.Normal, TextAnchor.MiddleRight);
                }
            }

            Button why = UiTheme.MakeButton(c, "为什么会得到这个结果？", () => ShowResidentWhy(d), 19);
            ResidentPlace(why.rectTransform(), 10, 540, 330, 52);
            Button curves = UiTheme.MakeButton(c, "查看我的需求与效用曲线", () => { CloseTop(); ShowConsumerLab(1); }, 19);
            ResidentPlace(curves.rectTransform(), 360, 540, 350, 52);
            Button profile = UiTheme.MakeButton(c, "生活档案", () => ShowLifeProfile(), 19);
            ResidentPlace(profile.rectTransform(), 730, 540, 328, 52);

            Button next = UiTheme.MakeButton(c, "统一结算并进入第 " + (s.day + 1) + " 天", AdvanceResidentCycle, 22);
            ResidentPlace(next.rectTransform(), 250, 630, 570, 60);
            next.image.color = ResidentTeal;
            Text nt = next.GetComponentInChildren<Text>(); if (nt != null) nt.color = Color.white;
        }

        /// <summary>居民视角收日:先关自己、再结算(照抄 UiReports)。</summary>
        void AdvanceResidentCycle()
        {
            GameState s = GameState.I;
            // TryNextDay 内部会弹出终局总评/竞争介绍/价格战等关键面板,若结算后再 CloseTop
            // 会把它们当场错杀,旧面板残栈还会让"昨天的结算按钮"复活造成二次跳日(P1-1)。
            CloseTop();
            if (GameBootstrap.I.TryNextDay() && s.phase != GamePhase.Ended) ShowResidentHome();
        }

        void ShowResidentWhy(ResidentDayRecord day)
        {
            string body = "今天的结果由三层原因共同形成：\n\n" +
                "1. 约束：你只有 ¥" + day.cashStart.ToString("0") + " 现金和 4 小时自由时间。\n" +
                "2. 偏好：同一商品对不同居民产生的效用不同。\n" +
                "3. 边际变化：同类消费越多，下一份新增满足通常越少。\n\n" +
                "你花了 ¥" + day.spending.ToString("0") + "，使用 " + day.freeHoursUsed.ToString("0.##") + " 小时，获得 " + day.totalUtility.ToString("0.0") + " 总效用。";
            ShowInfoPopup("WHY · 生活结果拆解", body);
        }

        void ShowLifeProfile()
        {
            GameState s = GameState.I;
            ResidentState r = s.resident;
            GameObject root = BeginPanel("居民生活档案 · 持续更新", 820, 590);
            RectTransform c = ContentOf(root);
            ResidentText(c, r.residentName, 32, UiTheme.TextMain, 55, 16, 650, 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            ResidentText(c, ResidentEconomy.ProfessionName(r.profession) + "  ·  " + ResidentEconomy.PreferenceName(r.preference),
                17, ResidentTeal, 55, 62, 650, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform card = ResidentCard(c, "profile", 55, 120, 650, 190, new Color32(0x3b, 0x32, 0x2b, 255));
            ResidentText(card, ResidentEconomy.LifeProfile(r), 20, UiTheme.TextMain, 22, 22, 606, 86, FontStyle.Bold, TextAnchor.UpperLeft);
            ResidentText(card, "生活画像不是评分，也不会给出“正确人生”。它只是把你的真实取舍整理成可以回看的模式。",
                16, UiTheme.TextSub, 22, 122, 606, 52, FontStyle.Normal, TextAnchor.UpperLeft);
            LedgerLine(c, "完成居民日", r.completedDays + " 天", 330);
            LedgerLine(c, "当前储蓄", "¥" + r.savings.ToString("0"), 370);
            LedgerLine(c, "技能 / 失业日", r.skillLevel.ToString("0") + " / " + r.unemployedDays, 410);
            Button close = UiTheme.MakeButton(c, "返回", CloseTop, 20);
            ResidentPlace(close.rectTransform(), 280, 476, 200, 48);
        }

        public void ShowResidentExperiments()
        {
            GameState s = GameState.I;
            ResidentEconomy.Ensure(s);
            GameObject root = BeginPanel("消费者实验 · 改变一个条件", 960, 720);
            RectTransform c = ContentOf(root);
            ResidentText(c, "实验会真实改变居民预算或市场价格；结果会同步到企业视角。", 17,
                UiTheme.TextSub, 20, 8, 870, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
            ExperimentChoice(c, s, 0, "突然多出 ¥100", "观察收入增加后预算线与消费组合如何变化", 20, 64);
            ExperimentChoice(c, s, 1, "玩家果汁突然涨价", "观察替代效应，并把需求变化送到企业销量", 470, 64);
            ExperimentChoice(c, s, 2, "电影院半价", "观察电影与爆米花这组互补品", 20, 210);
            ExperimentChoice(c, s, 3, "重排 24 小时", "比较工作收入与闲暇效用，不设标准答案", 470, 210);
            RectTransform result = ResidentCard(c, "experimentResult", 20, 380, 870, 122, new Color32(0x30, 0x2c, 0x27, 255));
            ResidentText(result, "实验观察", 14, ResidentTeal, 16, 8, 150, 22, FontStyle.Bold);
            ResidentText(result, string.IsNullOrEmpty(s.resident.lastExperimentSummary) ? "选择一个实验后，这里会解释预算线、需求或效用发生了什么。" : s.resident.lastExperimentSummary,
                16, UiTheme.TextSub, 16, 34, 838, 72, FontStyle.Normal, TextAnchor.UpperLeft);
            Button lab = UiTheme.MakeButton(c, "查看消费者曲线", () => { CloseTop(); ShowConsumerLab(2); }, 20);
            ResidentPlace(lab.rectTransform(), 180, 536, 270, 48);
            Button home = UiTheme.MakeButton(c, "返回生活账本", () => { CloseTop(); ShowResidentHome(); }, 20);
            ResidentPlace(home.rectTransform(), 470, 536, 270, 48);
        }

        void ExperimentChoice(RectTransform parent, GameState s, int id, string title, string detail, float x, float y)
        {
            bool done = (s.resident.experimentMask & (1 << id)) != 0;
            RectTransform card = ResidentCard(parent, "exp" + id, x, y, 420, 124,
                done ? new Color32(0x35, 0x4a, 0x43, 255) : new Color32(0x46, 0x38, 0x2f, 255));
            ResidentText(card, title + (done ? "  ·  已完成" : ""), 20, UiTheme.TextMain, 16, 10, 385, 30, FontStyle.Bold);
            ResidentText(card, detail, 14, UiTheme.TextSub, 16, 42, 270, 54, FontStyle.Normal, TextAnchor.UpperLeft);
            Button b = UiTheme.MakeButton(card, done ? "回看" : "实验", () =>
            {
                string message = ResidentEconomy.RunExperiment(s, id);
                if (id == 0) KnowledgeSystem.MarkExperimented(s, "budgetconstraint", "budgetline", "incomeeffect");
                else if (id == 1) KnowledgeSystem.MarkExperimented(s, "substitute", "substitutioneffect", "realpurchasingpower");
                else if (id == 2) KnowledgeSystem.MarkExperimented(s, "complement", "utility");
                else KnowledgeSystem.MarkExperimented(s, "laborleisure", "oppcost");
                ShowToast(message);
                CloseTop();
                ShowResidentExperiments();
            }, 17);
            ResidentPlace(b.rectTransform(), 304, 54, 98, 48);
        }

        public void ShowConsumerLab(int tab)
        {
            GameState s = GameState.I;
            if (s == null) return;
            ResidentEconomy.Ensure(s);
            consumerLabTab = Mathf.Clamp(tab, 0, 3);
            GameObject root = BeginPanel("消费者实验室 · 我的选择曲线", 1180, 880);
            RectTransform c = ContentOf(root);
            string[] tabs = { "消费结构", "总效用 / 边际效用", "预算线 / 无差异", "收入 / 价格变化" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                Button b = UiTheme.MakeButton(c, tabs[i], () => { CloseTop(); ShowConsumerLab(idx); }, 17);
                ResidentPlace(b.rectTransform(), 12 + i * 274, 4, 260, 48);
                if (i == consumerLabTab)
                {
                    b.image.color = ResidentTeal;
                    Text bt = b.GetComponentInChildren<Text>(); if (bt != null) bt.color = Color.white;
                }
            }

            PolylineGraph graph = CreateResidentPlot(c, 12, 78, 730, 520);
            RectTransform explain = ResidentCard(c, "explain", 762, 78, 344, 520, new Color32(0x38, 0x30, 0x29, 255));
            Text title = ResidentText(explain, "", 22, UiTheme.TextMain, 18, 18, 308, 64, FontStyle.Bold, TextAnchor.UpperLeft);
            Text body = ResidentText(explain, "", 16, UiTheme.TextSub, 18, 92, 308, 360, FontStyle.Normal, TextAnchor.UpperLeft);
            Text legend = ResidentText(c, "", 15, UiTheme.TextSub, 22, 610, 1080, 80, FontStyle.Normal, TextAnchor.UpperLeft);

            if (consumerLabTab == 0) BuildConsumptionStructure(s.resident, graph, title, body, legend);
            else if (consumerLabTab == 1) BuildUtilityCurve(s.resident, graph, title, body, legend);
            else if (consumerLabTab == 2) BuildBudgetCurve(s, graph, title, body, legend);
            else BuildIncomePriceCurve(s, graph, title, body, legend);

            Button experiments = UiTheme.MakeButton(c, "改变条件做实验", () => { CloseTop(); ShowResidentExperiments(); }, 19);
            ResidentPlace(experiments.rectTransform(), 290, 708, 260, 48);
            Button home = UiTheme.MakeButton(c, "返回生活账本", () => { CloseTop(); ShowResidentHome(); }, 19);
            ResidentPlace(home.rectTransform(), 570, 708, 260, 48);
        }

        void BuildConsumptionStructure(ResidentState r, PolylineGraph graph, Text title, Text body, Text legend)
        {
            List<ResidentChoiceRecord> source = r.todayChoices;
            if (source.Count == 0 && r.history.Count > 0) source = r.history[r.history.Count - 1].choices;
            float[] totals = new float[4];
            string[] cats = { "食物", "饮料", "娱乐", "日用品" };
            for (int i = 0; i < source.Count; i++)
                for (int j = 0; j < cats.Length; j++) if (source[i].category == cats[j]) totals[j] += source[i].price;
            float max = Mathf.Max(1f, Mathf.Max(Mathf.Max(totals[0], totals[1]), Mathf.Max(totals[2], totals[3])));
            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i < 4; i++) points.Add(new Vector2(i / 3f, totals[i] / max));
            graph.SetData(points);
            title.text = "钱花去了哪里？";
            body.text = "结构比单个数字更能说明偏好。收入不变时，提高一类支出往往意味着减少另一类支出。\n\n" +
                "食物 ¥" + totals[0].ToString("0") + "\n饮料 ¥" + totals[1].ToString("0") + "\n娱乐 ¥" + totals[2].ToString("0") + "\n日用品 ¥" + totals[3].ToString("0");
            legend.text = "横轴：食物 → 饮料 → 娱乐 → 日用品   ·   纵轴：该类别支出（按最高类别归一化）";
        }

        void BuildUtilityCurve(ResidentState r, PolylineGraph graph, Text title, Text body, Text legend)
        {
            List<ResidentChoiceRecord> source = r.todayChoices;
            if (source.Count == 0 && r.history.Count > 0) source = r.history[r.history.Count - 1].choices;
            List<Vector2> total = new List<Vector2>();
            List<Vector2> marginal = new List<Vector2>();
            if (source.Count == 0)
            {
                float running = 0f;
                for (int i = 0; i < 6; i++) { float mu = 30f / (1f + i * .65f); running += mu; total.Add(new Vector2(i / 5f, running)); marginal.Add(new Vector2(i / 5f, mu)); }
            }
            else
            {
                float maxX = Mathf.Max(1, source.Count - 1);
                for (int i = 0; i < source.Count; i++) { total.Add(new Vector2(i / maxX, source[i].utility)); marginal.Add(new Vector2(i / maxX, source[i].marginalUtility)); }
            }
            NormalizeY(total, marginal);
            graph.lineColor = UiTheme.Accent;
            graph.lineColorB = ResidentTeal;
            graph.SetData(total, null, marginal);
            title.text = "为什么下一份快乐变少？";
            body.text = "橙线是累计总效用，通常继续上升；绿线是每次新增的边际效用。同类消费重复后，绿线往往下降。\n\n这不表示“不能再买”，只表示下一份带来的新增满足变少。";
            legend.text = "橙线：总效用 TU   ·   绿线：边际效用 MU   ·   横轴：消费次序";
        }

        void BuildBudgetCurve(GameState s, PolylineGraph graph, Text title, Text body, Text legend)
        {
            float budget = Mathf.Max(50f, s.resident.cash + s.resident.todaySpending);
            float pJuice = ResidentEconomy.Price(s, ResidentItemKind.PlayerJuice);
            float pMovie = ResidentEconomy.Price(s, ResidentItemKind.Movie);
            float maxJuice = budget / pJuice;
            float maxMovie = budget / pMovie;
            List<Vector2> budgetLine = new List<Vector2>();
            List<Vector2> indifference = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float x = i / 20f;
                budgetLine.Add(new Vector2(x, 1f - x));
                float unitsX = Mathf.Max(.15f, maxJuice * x);
                float target = Mathf.Sqrt(Mathf.Max(.2f, maxJuice * maxMovie)) * .34f;
                float unitsY = target * target / unitsX;
                indifference.Add(new Vector2(x, Mathf.Clamp01(unitsY / Mathf.Max(.1f, maxMovie))));
            }
            graph.lineColor = UiTheme.Accent;
            graph.fitColor = new Color32(0x67, 0xb5, 0xae, 220);
            graph.SetData(budgetLine, indifference);
            title.text = "买得起什么组合？";
            body.text = "预算 ¥" + budget.ToString("0") + "。\n\n若全买果汁约 " + maxJuice.ToString("0.0") + " 杯；若全看电影约 " + maxMovie.ToString("0.0") + " 次。\n\n果汁涨价时，横轴截距会向内缩，预算线发生转动。";
            legend.text = "橙线：预算线（果汁数量 / 电影次数）   ·   蓝线：一条示意无差异曲线   ·   相切附近代表可负担的高效用组合";
        }

        void BuildIncomePriceCurve(GameState s, PolylineGraph graph, Text title, Text body, Text legend)
        {
            List<Vector2> normal = new List<Vector2>();
            List<Vector2> inferior = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float income = 50f + i * 25f;
                normal.Add(new Vector2(i / 6f, Mathf.Clamp01((income * .35f / 28f) / 3f)));
                inferior.Add(new Vector2(i / 6f, Mathf.Clamp01((2.8f - income / 100f) / 2.4f)));
            }
            graph.lineColor = UiTheme.Accent;
            graph.lineColorB = ResidentTeal;
            graph.SetData(normal, null, inferior);
            title.text = "收入增加后会买什么？";
            int demandPct = Mathf.RoundToInt(s.ResidentDemandChangePercent);   // 围猎三#3 复审:同数据源第四处,-0.x% 不再显示「-0%」
            body.text = "橙线示意收入上升后，升级餐饮这类正常品消费增加；绿线示意基础应急品可能减少。\n\n当前果汁价 ¥" + s.price.ToString("0") + "，居民需求信号 " + (demandPct >= 0 ? "+" : "") + demandPct + "%：价格实验会同步改变企业销量。";
            legend.text = "橙线：正常品消费   ·   绿线：低档品消费示意   ·   横轴：收入从 ¥50 增至 ¥200";
        }

        static void NormalizeY(List<Vector2> a, List<Vector2> b)
        {
            float max = 1f;
            for (int i = 0; i < a.Count; i++) max = Mathf.Max(max, a[i].y);
            for (int i = 0; i < b.Count; i++) max = Mathf.Max(max, b[i].y);
            for (int i = 0; i < a.Count; i++) a[i] = new Vector2(a[i].x, a[i].y / max);
            for (int i = 0; i < b.Count; i++) b[i] = new Vector2(b[i].x, b[i].y / max);
        }

        PolylineGraph CreateResidentPlot(RectTransform parent, float x, float y, float w, float h)
        {
            Image bg = UiTheme.MakeImage(parent, new Color32(0x24, 0x21, 0x1d, 255), "consumerPlot");
            ResidentPlace(bg.rectTransform, x, y, w, h);
            GameObject graphObject = new GameObject("consumerGraph", typeof(RectTransform), typeof(CanvasRenderer));
            graphObject.transform.SetParent(bg.transform, false);
            PolylineGraph graph = graphObject.AddComponent<PolylineGraph>();
            graph.raycastTarget = false;
            graph.rectTransform.Stretch();
            graph.rectTransform.offsetMin = new Vector2(10, 10);
            graph.rectTransform.offsetMax = new Vector2(-10, -10);
            return graph;
        }

        void DrawTimeLedger(RectTransform parent, ResidentState r, float x, float y, float w, float h)
        {
            RectTransform card = ResidentCard(parent, "timeLedger", x, y, w, h, new Color32(0x36, 0x30, 0x2a, 255));
            ResidentText(card, "24H", 15, UiTheme.Accent, 14, 8, 52, 20, FontStyle.Bold);
            float startX = 76f, available = w - 92f, barY = 13f, barH = 30f;
            float[] hours = { 8f, 8f, 4f, r.freeHoursAtDayStart - r.freeHours, r.freeHours };
            string[] labels = { "睡眠 8h", "工作 8h", "必要 4h", "已用 " + hours[3].ToString("0.#") + "h", "可用 " + r.freeHours.ToString("0.#") + "h" };
            Color[] colors = { new Color32(0x62, 0x64, 0x78, 255), new Color32(0x88, 0x63, 0x45, 255), new Color32(0x75, 0x70, 0x61, 255), UiTheme.Accent, ResidentTeal };
            float cursor = startX;
            for (int i = 0; i < hours.Length; i++)
            {
                float width = available * Mathf.Max(0f, hours[i]) / 24f;
                if (width < .5f) continue;
                Image segment = UiTheme.MakeImage(card, colors[i], "time_" + i);
                ResidentPlace(segment.rectTransform, cursor, barY, width, barH);
                if (width > 74f) ResidentText(segment.rectTransform, labels[i], 12, Color.white, 3, 0, width - 6, barH, FontStyle.Bold, TextAnchor.MiddleCenter);
                cursor += width;
            }
            ResidentText(card, "睡眠 + 固定工作 + 必要活动 + 自由选择 = 24 小时", 13, UiTheme.TextSub,
                76, 48, available, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawResourceChips(RectTransform parent, ResidentState r, float x, float y)
        {
            string[] labels = { "现金  ¥" + r.cash.ToString("0"), "储蓄  ¥" + r.savings.ToString("0"), "精力  " + r.energy.ToString("0"),
                "幸福  " + r.happiness.ToString("0"), "饱腹  " + r.satiety.ToString("0"), "娱乐  " + r.entertainment.ToString("0") };
            for (int i = 0; i < labels.Length; i++)
            {
                Image chip = UiTheme.MakeImage(parent, i < 2 ? new Color32(0x56, 0x42, 0x30, 255) : new Color32(0x35, 0x50, 0x4d, 255), "resourceChip");
                ResidentPlace(chip.rectTransform, x + i * 184f, y, 172, 50);
                ResidentText(chip.rectTransform, labels[i], 16, UiTheme.TextMain, 8, 0, 156, 50, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        RectTransform ResidentCard(RectTransform parent, string name, float x, float y, float w, float h, Color color)
        {
            Image image = UiTheme.MakeImage(parent, color, name);
            image.sprite = null;
            image.type = Image.Type.Simple;
            ResidentPlace(image.rectTransform, x, y, w, h);
            return image.rectTransform;
        }

        Text ResidentText(RectTransform parent, string value, int size, Color color,
            float x, float y, float w, float h, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text text = UiTheme.MakeText(parent, value, size, color, anchor, style);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            ResidentPlace(text.rectTransform, x, y, w, h);
            return text;
        }

        void SectionHeader(RectTransform parent, string title, string caption, float x, float y, float w)
        {
            Image marker = UiTheme.MakeImage(parent, ResidentTeal, "marker");
            ResidentPlace(marker.rectTransform, x, y + 4, 5, 42);
            ResidentText(parent, title, 22, UiTheme.TextMain, x + 16, y, w - 16, 30, FontStyle.Bold);
            ResidentText(parent, caption, 13, UiTheme.TextSub, x + 16, y + 30, w - 16, 22);
        }

        void ResidentNavButton(RectTransform parent, string title, string caption, float x, float y, float w, float h, System.Action onClick)
        {
            Button b = UiTheme.MakeButton(parent, "", onClick, 18);
            ResidentPlace(b.rectTransform(), x, y, w, h);
            b.image.color = ResidentPaper;
            ResidentText(b.rectTransform(), title, 19, ResidentInk, 14, 8, w - 28, 28, FontStyle.Bold);
            ResidentText(b.rectTransform(), caption, 13, new Color32(0x70, 0x5b, 0x47, 255), 14, 38, w - 28, 24);
        }

        void LedgerLine(RectTransform parent, string label, string value, float y)
        {
            ResidentText(parent, label, 15, UiTheme.TextSub, 18, y, parent.sizeDelta.x * .46f, 28, FontStyle.Bold);
            ResidentText(parent, value, 18, UiTheme.TextMain, parent.sizeDelta.x * .46f, y, parent.sizeDelta.x * .48f - 16f, 28, FontStyle.Bold, TextAnchor.MiddleRight);
            Image line = UiTheme.MakeImage(parent, new Color32(0x66, 0x59, 0x4b, 140), "ledgerLine");
            ResidentPlace(line.rectTransform, 18, y + 31, parent.sizeDelta.x - 36f, 1);
        }

        InputField MakeResidentInput(RectTransform parent, string placeholder, float x, float y, float w, float h)
        {
            Image bg = UiTheme.MakeImage(parent, ResidentPaper, "nameInput");
            ResidentPlace(bg.rectTransform, x, y, w, h);
            InputField input = bg.gameObject.AddComponent<InputField>();
            Text value = ResidentText(bg.rectTransform, "", 20, ResidentInk, 14, 0, w - 28, h, FontStyle.Bold);
            Text hint = ResidentText(bg.rectTransform, placeholder, 18, new Color32(0x8b, 0x78, 0x66, 255), 14, 0, w - 28, h);
            input.textComponent = value;
            input.placeholder = hint;
            input.targetGraphic = bg;
            input.characterLimit = 12;
            return input;
        }

        static void ResidentPlace(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static int CategoryCountForUi(ResidentState r, string category)
        {
            if (category == "食物") return r.foodCount;
            if (category == "饮料") return r.drinkCount;
            if (category == "娱乐") return r.entertainmentCount;
            return r.householdCount;
        }

        static string ResourceEffect(ResidentItemDef def)
        {
            List<string> effects = new List<string>();
            if (def.satiety != 0f) effects.Add("饱腹 " + Signed(def.satiety));
            if (def.happiness != 0f) effects.Add("幸福 " + Signed(def.happiness));
            if (def.energy != 0f) effects.Add("精力 " + Signed(def.energy));
            if (def.entertainment != 0f) effects.Add("娱乐 " + Signed(def.entertainment));
            if (def.household != 0f) effects.Add("居家 " + Signed(def.household));
            return string.Join("  ·  ", effects);
        }

        static string Signed(float value) { return (value >= 0f ? "+" : "") + value.ToString("0"); }
    }
}
