using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.6 金融中心：银行账本、信贷、央行与金融稳定共享同一组真实状态。</summary>
    public partial class UIManager
    {
        static readonly Color FinanceNavy = new Color32(0x1f, 0x2f, 0x34, 255);
        static readonly Color FinanceCard = new Color32(0x2b, 0x3d, 0x40, 255);
        static readonly Color FinanceSoft = new Color32(0x36, 0x4b, 0x4e, 255);
        static readonly Color FinanceTeal = new Color32(0x69, 0xc4, 0xb7, 255);
        static readonly Color FinanceGold = new Color32(0xf0, 0xb4, 0x55, 255);
        static readonly Color FinancePaper = new Color32(0xe8, 0xd8, 0xb6, 255);

        public void ShowFinanceCenter(int selectedTab = 0)
        {
            GameState state = GameState.I;
            if (state == null) return;
            FinancialSystemService.Ensure(state);
            ProgressionService.Ensure(state);
            selectedTab = Mathf.Clamp(selectedTab, 0, 4);

            GameObject root = BeginPanel("金融中心 · 银行与央行", 1260f, 840f);
            root.name = "panel_finance_center";
            Image panel = root.GetComponent<Image>();
            if (panel != null) panel.color = FinanceNavy;
            RectTransform content = ContentOf(root);

            ChapterDefinition chapter = ProgressionService.Current(state);
            string route = chapter != null && chapter.part == LearningPart.Finance
                ? "Chapter " + chapter.number + " · " + chapter.title
                : state.progression.completedChapter >= 42 ? "金融系统已完成 · 自由运行" : "完成宏观篇后开放第六篇";
            FinanceText(content, route, 18, FinanceTeal, 0f, 0f, 500f, 34f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(content, "统一 WorldDay · 所有交易双边入账", 16, UiTheme.TextSub,
                500f, 0f, 420f, 34f, TextAnchor.MiddleCenter);
            Button macro = UiTheme.MakeButton(content, "宏观中心", () => { ClosePanel(root); ShowMacroDashboard(); }, 17);
            PlaceTopLeft(macro.rectTransform(), 800f, 0f, 132f, 34f);
            Button world = UiTheme.MakeButton(content, "世界经济", () => { ClosePanel(root); ShowWorldEconomy(0); }, 17);
            PlaceTopLeft(world.rectTransform(), 940f, 0f, 128f, 34f);
            UiTheme.SetButtonStyle(world, FinanceGold, UiTheme.Ink);
            Button settle = UiTheme.MakeButton(content, "统一结算本日", () =>
            {
                ClosePanel(root);
                if (GameBootstrap.I != null) GameBootstrap.I.TryNextDay();
            }, 17);
            PlaceTopLeft(settle.rectTransform(), 1076f, 0f, 136f, 34f);
            UiTheme.SetButtonStyle(settle, FinanceGold, UiTheme.Ink);

            FinanceMetric(content, "政策利率", state.centralBank.policyRate.ToString("0.00") + "%", 0f, FinanceGold);
            FinanceMetric(content, "企业贷款利率", state.bank.companyLoanRate.ToString("0.00") + "%", 244f, FinanceTeal);
            FinanceMetric(content, "存款总额", "¥ " + state.bank.TotalDeposits.ToString("0"), 488f, UiTheme.TextMain);
            FinanceMetric(content, "贷款余额", "¥ " + state.bank.PerformingLoans.ToString("0"), 732f, UiTheme.TextMain);
            FinanceMetric(content, "稳定状态", FinancialSystemService.StabilityLabel(state), 976f,
                state.bank.liquidityCrisis || state.bank.Insolvent ? UiTheme.Bad : UiTheme.Good);

            string[] tabs = { "银行大厅", "资产负债", "企业融资", "央行政策", "金融压力" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button button = UiTheme.MakeButton(content, tabs[i], () => ReloadFinance(root, tab), 17);
                PlaceTopLeft(button.rectTransform(), i * 244f, 120f, 232f, 42f);
                UiTheme.SetButtonStyle(button, i == selectedTab ? FinanceTeal : FinancePaper,
                    i == selectedTab ? FinanceNavy : UiTheme.Ink);
                button.interactable = i != selectedTab;
            }

            if (state.progression.completedChapter < 30)
            {
                BuildFinanceLocked(content);
                return;
            }
            if (selectedTab == 0) BuildBankLobby(content, state, root);
            else if (selectedTab == 1) BuildBankBalanceSheet(content, state, root);
            else if (selectedTab == 2) BuildCompanyFinance(content, state, root);
            else if (selectedTab == 3) BuildCentralBank(content, state, root);
            else BuildFinancialStress(content, state, root);
        }

        void BuildFinanceLocked(RectTransform content)
        {
            RectTransform card = FinanceCardPanel(content, "第六篇尚未开放", "前五篇的企业、市场、居民、政府与宏观会在这里连成一个金融系统",
                0f, 184f, 1212f, 470f);
            FinanceText(card, "完成 Chapter 30「怎样真正变富」后，小镇银行与新的货币章节将自动开放。",
                23, UiTheme.TextMain, 70f, 128f, 1072f, 80f, TextAnchor.MiddleCenter, FontStyle.Bold);
            FinanceText(card, "在此之前，你的旧存档、自由模式和所有历史账本都会保留。",
                18, UiTheme.TextSub, 70f, 226f, 1072f, 50f, TextAnchor.MiddleCenter);
        }

        void BuildBankLobby(RectTransform content, GameState state, GameObject root)
        {
            if (!ProgressionService.HasSignal(state, ProgressSignals.MoneyFunctionsExperienced))
            {
                RectTransform lesson = FinanceCardPanel(content, "以物易物的麻烦", "Chapter 31 · 钱到底是什么", 0f, 184f, 1212f, 470f);
                FinanceText(lesson, "你有果汁，想换一双鞋；鞋匠却只想要米。\n没有共同接受的东西，交易必须同时凑齐「我要你的、你也要我的」。",
                    21, UiTheme.TextMain, 50f, 66f, 1112f, 100f, TextAnchor.MiddleCenter);
                FinanceText(lesson, "<b>货币的三项职能</b>\n交换媒介：先卖果汁，再买鞋\n计价单位：所有商品都有可比价格\n价值储藏：把今天的购买力带到未来",
                    19, FinancePaper, 140f, 184f, 932f, 134f, TextAnchor.MiddleLeft);
                Button complete = UiTheme.MakeButton(lesson, "用货币完成交换", () =>
                {
                    state.UnlockCard("money"); state.UnlockCard("mediumofexchange");
                    state.UnlockCard("unitofaccount"); state.UnlockCard("storeofvalue");
                    ProgressionService.RecordSignal(state, ProgressSignals.MoneyFunctionsExperienced,
                        "从以物易物到货币", "用共同交换媒介解决需求双重巧合。");
                    ReloadFinance(root, 0);
                }, 20);
                PlaceTopLeft(complete.rectTransform(), 390f, 350f, 432f, 56f);
                UiTheme.SetButtonStyle(complete, FinanceGold, UiTheme.Ink);
                return;
            }

            RectTransform account = FinanceCardPanel(content, "居民账户", "存款对你是资产，对银行是负债", 0f, 184f, 580f, 470f);
            FinanceText(account, "手持现金", 17, UiTheme.TextSub, 20f, 62f, 250f, 28f);
            FinanceText(account, "¥ " + state.resident.cash.ToString("0"), 30, FinanceGold, 20f, 88f, 250f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(account, "银行存款", 17, UiTheme.TextSub, 300f, 62f, 240f, 28f);
            FinanceText(account, "¥ " + state.resident.savings.ToString("0"), 30, FinanceTeal, 300f, 88f, 240f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(account, "存款年化 " + state.bank.depositRate.ToString("0.00") + "%  ·  实际存款利率 " +
                InterestRateService.RealDepositRate(state).ToString("0.00") + "%\n本日存款利息 +¥ " + state.resident.todayInterestIncome.ToString("0.00"),
                16, UiTheme.TextMain, 20f, 140f, 520f, 54f, TextAnchor.UpperLeft);
            float residentDebt = CreditService.ResidentDebt(state);
            float residentNetWorth = state.resident.cash + state.resident.savings - residentDebt;
            CreditQuote residentQuote = CreditService.QuoteResidentLoan(state, 500f, 12);
            // 围猎三(#6):平均利率按玩家归属过滤——镇买方按揭利率不得混进居民账户卡片展示。
            float playerAvgRate = CreditService.AverageLoanRate(state, LoanBorrowerType.Resident,
                state.resident.residentName);
            string residentRate = playerAvgRate > .001f
                ? playerAvgRate.ToString("0.00") + "%"
                : "无未偿贷款";
            FinanceText(account, "消费贷 ¥" + residentDebt.ToString("0") + " · " + residentRate + "　信用分 " +
                CreditService.ResidentCreditScore(state).ToString("0") + "\n流动净资产 ¥" + residentNetWorth.ToString("0") +
                "　今日贷款利息 -¥" + state.resident.todayInterestExpense.ToString("0.00"),
                15, UiTheme.TextSub, 20f, 194f, 520f, 50f, TextAnchor.UpperLeft);
            Button deposit = UiTheme.MakeButton(account, "存入 ¥200", () =>
            {
                bool ok = CreditService.DepositResidentCash(state, 200f, out string message);
                ShowToast(message); if (ok) ReloadFinance(root, 0);
            }, 18);
            Button withdraw = UiTheme.MakeButton(account, "取出 ¥200", () =>
            {
                bool ok = CreditService.WithdrawResidentDeposit(state, 200f, out string message);
                ShowToast(message); if (ok) ReloadFinance(root, 0);
            }, 18);
            PlaceTopLeft(deposit.rectTransform(), 20f, 252f, 250f, 46f);
            PlaceTopLeft(withdraw.rectTransform(), 290f, 252f, 250f, 46f);
            UiTheme.SetButtonStyle(deposit, FinanceGold, UiTheme.Ink);
            Button residentLoan = UiTheme.MakeButton(account, "申请 ¥500 消费贷 · " + residentQuote.finalRate.ToString("0.0") + "%", () =>
            {
                LoanRecord loan = CreditService.IssueResidentLoan(state, 500f, 12, out string message);
                ShowToast(message); if (loan != null) ReloadFinance(root, 0);
            }, 16);
            PlaceTopLeft(residentLoan.rectTransform(), 20f, 312f, 250f, 46f);
            UiTheme.SetButtonStyle(residentLoan, FinanceTeal, FinanceNavy);
            residentLoan.interactable = residentQuote.approved && state.progression.completedChapter >= 32;
            Button compare = UiTheme.MakeButton(account, "比较「现在花」与「存下来」", () =>
            {
                state.UnlockCard("interestrate"); state.UnlockCard("realinterest");
                state.UnlockCard("interestsaving"); state.UnlockCard("interestconsumption");
                ProgressionService.RecordSignal(state, ProgressSignals.InterestChoiceObserved,
                    "储蓄与消费选择", "存款实际利率 " + InterestRateService.RealDepositRate(state).ToString("0.00") + "%");
                ShowToast("已记录这次跨期选择：利率是现在与未来之间的价格。");
                ReloadFinance(root, 0);
            }, 17);
            PlaceTopLeft(compare.rectTransform(), 290f, 312f, 250f, 46f);
            FinanceText(account, "贷款到账同时增加存款资产与等额债务，不会直接增加净资产。",
                14, UiTheme.TextSub, 20f, 374f, 520f, 42f, TextAnchor.UpperLeft);

            RectTransform bank = FinanceCardPanel(content, "银行今日状态", "准备金解决提款，资本吸收损失", 600f, 184f, 612f, 470f);
            FinanceText(bank, "准备金\n<b>¥ " + state.bank.reserves.ToString("0") + "</b>", 20, FinanceTeal,
                20f, 64f, 260f, 76f, TextAnchor.MiddleCenter);
            FinanceText(bank, "应留准备金\n<b>¥ " + state.bank.RequiredReserves.ToString("0") + "</b>", 20, FinanceGold,
                320f, 64f, 260f, 76f, TextAnchor.MiddleCenter);
            float ratio = state.bank.ActualReserveRatio * 100f;
            FinanceText(bank, "实际准备金率 " + ratio.ToString("0.0") + "%  /  要求 " +
                (state.bank.requiredReserveRatio * 100f).ToString("0.0") + "%", 18,
                ratio + .01f >= state.bank.requiredReserveRatio * 100f ? UiTheme.Good : UiTheme.Bad,
                20f, 164f, 572f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);
            FinanceText(bank, "资本充足率 " + (state.bank.CapitalAdequacy * 100f).ToString("0.0") + "%\n信贷增长 " +
                state.bank.creditGrowth.ToString("+0.0;-0.0;0.0") + "%\n金融压力 " + state.bank.financialStress.ToString("0") + " / 100",
                18, UiTheme.TextMain, 42f, 218f, 528f, 92f, TextAnchor.UpperLeft);
            Button ledger = UiTheme.MakeButton(bank, "打开完整资产负债表", () => ReloadFinance(root, 1), 18);
            PlaceTopLeft(ledger.rectTransform(), 60f, 326f, 492f, 52f);
            UiTheme.SetButtonStyle(ledger, FinanceTeal, FinanceNavy);
        }

        void BuildBankBalanceSheet(RectTransform content, GameState state, GameObject root)
        {
            bool unlocked = state.progression.completedChapter >= 32;
            RectTransform assets = FinanceCardPanel(content, "资产", "银行拥有、可以带来未来现金流的项目", 0f, 184f, 580f, 392f);
            FinanceLedgerRow(assets, "现金", state.bank.cash, 68f);
            FinanceLedgerRow(assets, "准备金", state.bank.reserves, 108f);
            FinanceLedgerRow(assets, "居民贷款", state.bank.HouseholdLoans, 148f);
            FinanceLedgerRow(assets, "企业贷款", state.bank.CompanyLoans, 188f);
            FinanceLedgerRow(assets, "政府债券", state.bank.GovernmentBonds, 228f);
            FinanceLedgerRow(assets, "资产合计", state.bank.TotalAssets, 288f, true);

            RectTransform funding = FinanceCardPanel(content, "负债与资本", "银行欠给谁，以及股东承担的损益", 600f, 184f, 612f, 392f);
            FinanceLedgerRow(funding, "居民存款", state.bank.householdDeposits, 68f);
            FinanceLedgerRow(funding, "企业存款", state.bank.companyDeposits, 108f);
            // bug搜#16:补「镇部门存款」一行——负债合计(TotalLiabilities)内部含它,漏行则分项
            // 加总恒比合计少这截(居民付租/还按揭即有余额),「平衡差额」无法从可见行对出,
            // 「一笔交易，两边记账」的账本教学口径不闭合。
            FinanceLedgerRow(funding, "镇部门存款", state.bank.townSectorDeposits, 148f);
            FinanceLedgerRow(funding, "央行借款", state.bank.centralBankBorrowing, 188f);
            FinanceLedgerRow(funding, "负债合计", state.bank.TotalLiabilities, 238f, true);
            FinanceLedgerRow(funding, "银行资本", state.bank.equity, 278f, true);
            FinanceText(funding, "平衡差额  " + state.bank.BalanceGap.ToString("+0.00;-0.00;0.00"), 18,
                Mathf.Abs(state.bank.BalanceGap) <= .1f ? UiTheme.Good : UiTheme.Bad,
                20f, 332f, 572f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform explanation = FinanceCardPanel(content, "一笔交易，两边记账", "信贷创造不会凭空增加银行净资产", 0f, 574f, 1212f, 80f);
            FinanceText(explanation, "发放 ¥1,000 贷款：贷款资产 +¥1,000  ↔  借款人存款负债 +¥1,000",
                18, FinancePaper, 20f, 50f, 1172f, 24f, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (unlocked)
            {
                state.UnlockCard("bankbalancesheet"); state.UnlockCard("reserves"); state.UnlockCard("reserveratio");
                ProgressionService.RecordSignal(state, ProgressSignals.BankBalanceViewed,
                    "查看银行资产负债表", "平衡差额 " + state.bank.BalanceGap.ToString("0.00"));
            }
            else FinanceText(content, "先在银行大厅完成一次存款，再查看账本如何双边变化。", 18,
                FinanceGold, 170f, 606f, 872f, 36f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void BuildCompanyFinance(RectTransform content, GameState state, GameObject root)
        {
            CreditQuote quote = CreditService.QuoteCompanyLoan(state, 1000f, LoanPurpose.EquipmentInvestment, 24);
            CreditQuote workingQuote = CreditService.QuoteCompanyLoan(state, 600f, LoanPurpose.WorkingCapital, 12);
            RectTransform quoteCard = FinanceCardPanel(content, "企业贷款报价", "贷款不是固定一个利率，它由基准与风险共同定价", 0f, 184f, 580f, 470f);
            FinanceText(quoteCard, quote.grade + " 级", 32, quote.approved ? UiTheme.Good : UiTheme.Bad,
                20f, 66f, 150f, 54f, TextAnchor.MiddleCenter, FontStyle.Bold);
            FinanceText(quoteCard, "申请 ¥1,000\n可批 ¥" + quote.approvedAmount.ToString("0"), 19, UiTheme.TextMain,
                188f, 60f, 170f, 70f, TextAnchor.MiddleCenter);
            FinanceText(quoteCard, "年化利率\n<b>" + quote.finalRate.ToString("0.00") + "%</b>", 19, FinanceGold,
                374f, 60f, 166f, 70f, TextAnchor.MiddleCenter);
            // bug搜#18:finalRate=Clamp(基础+风险+期限+流动性)——流动性溢价(准备金率低于
            // 要求×1.25 时 +1.5)不在分解里会让三项加总≠显示年化,定价教学口径不闭合。
            string liquidityLine = quote.liquidityPremium > 0f
                ? "\n流动性溢价  +" + quote.liquidityPremium.ToString("0.00") + "%"
                : "";
            FinanceText(quoteCard, "银行基础利率  " + quote.baseRate.ToString("0.00") + "%\n信用风险溢价  +" + quote.riskPremium.ToString("0.00") +
                "%\n设备期限溢价  +" + quote.termPremium.ToString("0.00") + "%" + liquidityLine +
                "\n经营贷报价  " + workingQuote.finalRate.ToString("0.00") + "%",
                18, UiTheme.TextMain, 36f, 156f, 508f, 116f, TextAnchor.UpperLeft);
            Button working = UiTheme.MakeButton(quoteCard, "¥600 经营贷", () =>
            {
                LoanRecord loan = CreditService.IssueCompanyLoan(state, 600f, LoanPurpose.WorkingCapital, 12, out string message);
                ShowToast(message); if (loan != null) ReloadFinance(root, 2);
            }, 17);
            Button borrow = UiTheme.MakeButton(quoteCard, "¥1,000 设备贷", () =>
            {
                LoanRecord loan = CreditService.IssueCompanyLoan(state, 1000f, LoanPurpose.EquipmentInvestment, 24, out string message);
                ShowToast(message); if (loan != null) ReloadFinance(root, 2);
            }, 18);
            PlaceTopLeft(working.rectTransform(), 36f, 296f, 246f, 52f);
            PlaceTopLeft(borrow.rectTransform(), 298f, 296f, 246f, 52f);
            UiTheme.SetButtonStyle(working, FinanceTeal, FinanceNavy);
            UiTheme.SetButtonStyle(borrow, FinanceGold, UiTheme.Ink);
            working.interactable = workingQuote.approved && state.progression.completedChapter >= 33;
            borrow.interactable = quote.approved && state.progression.completedChapter >= 33;
            FinanceText(quoteCard, quote.reason, 15, UiTheme.TextSub, 36f, 362f, 508f, 54f, TextAnchor.UpperLeft);

            RectTransform invest = FinanceCardPanel(content, "借款之后怎么用", "借到钱不等于已经变得更富", 600f, 184f, 612f, 470f);
            FinanceText(invest, "企业存款", 17, UiTheme.TextSub, 20f, 62f, 260f, 28f);
            FinanceText(invest, "¥ " + state.bank.companyDeposits.ToString("0"), 30, FinanceTeal, 20f, 88f, 260f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(invest, "未偿企业贷款", 17, UiTheme.TextSub, 320f, 62f, 260f, 28f);
            FinanceText(invest, "¥ " + CreditService.CompanyDebt(state).ToString("0"), 30, FinanceGold,
                320f, 88f, 260f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinancialEffectBreakdown effect = InterestRateService.CompanyInvestmentBreakdown(state, 1000f);
            FinanceText(invest, "同样一个 ¥1,000 投资意向：\n利率与信心调整后有效投资 ¥" + effect.finalValue.ToString("0") +
                "\n贷款利率影响 " + effect.loanRateEffect.ToString("+0;-0;0") + "  ·  金融压力 " +
                effect.financialStressEffect.ToString("+0;-0;0") + "\n信贷可得性 " + effect.creditAvailabilityEffect.ToString("+0;-0;0"),
                17, UiTheme.TextMain, 32f, 160f, 548f, 112f, TextAnchor.UpperLeft);
            Button transfer = UiTheme.MakeButton(invest, "转 ¥500 经营现金", () =>
            {
                bool ok = CreditService.TransferCompanyDepositToCash(state, 500f, out string message);
                ShowToast(message); if (ok) ReloadFinance(root, 2);
            }, 16);
            Button equipment = UiTheme.MakeButton(invest, "购买 ¥800 设备", () =>
            {
                bool ok = CreditService.FinanceEquipmentInvestment(state, 800f, out string message);
                ShowToast(message); if (ok) ReloadFinance(root, 2);
            }, 18);
            PlaceTopLeft(transfer.rectTransform(), 32f, 286f, 260f, 52f);
            PlaceTopLeft(equipment.rectTransform(), 310f, 286f, 270f, 52f);
            UiTheme.SetButtonStyle(transfer, FinancePaper, UiTheme.Ink);
            UiTheme.SetButtonStyle(equipment, FinanceTeal, FinanceNavy);
            transfer.interactable = state.bank.companyDeposits >= 500f;
            equipment.interactable = state.bank.companyDeposits >= 800f;
            FinanceText(invest, "设备会进入企业投资 I，并增加产能；贷款本金和利息仍要按期偿还。",
                16, UiTheme.TextSub, 40f, 354f, 532f, 58f, TextAnchor.UpperLeft);
        }

        void BuildCentralBank(RectTransform content, GameState state, GameObject root)
        {
            if (!state.centralBank.unlocked)
            {
                RectTransform locked = FinanceCardPanel(content, "中央银行尚未开放", "央行不等于商业银行", 0f, 184f, 1212f, 470f);
                FinanceText(locked, "先完成存款、银行账本、信用创造、企业融资与利率选择。\nChapter 37 开始后，你将参加货币政策会议。",
                    22, UiTheme.TextMain, 100f, 135f, 1012f, 100f, TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }
            ProgressionService.RecordSignal(state, ProgressSignals.CentralBankOpened,
                "进入中央银行", "开始查看通胀、就业、产出缺口与金融稳定。");
            state.UnlockCard("centralbank"); state.UnlockCard("policyrate"); state.UnlockCard("inflationtarget");

            RectTransform decision = FinanceCardPanel(content, "货币政策会议", "政策利率不会直接改写 GDP 或通胀", 0f, 184f, 580f, 470f);
            FinanceText(decision, "当前政策利率", 18, UiTheme.TextSub, 20f, 62f, 540f, 28f, TextAnchor.MiddleCenter);
            FinanceText(decision, state.centralBank.policyRate.ToString("0.00") + "%", 42, FinanceGold,
                20f, 88f, 540f, 62f, TextAnchor.MiddleCenter, FontStyle.Bold);
            FinanceText(decision, "通胀 " + state.macro.inflation.ToString("0.0") + "%  /  目标 " + state.centralBank.inflationTarget.ToString("0.0") +
                "%\n失业 " + state.macro.unemploymentRate.ToString("0.0") + "%  ·  产出缺口 " + state.macro.outputGap.ToString("+0.0;-0.0;0.0") +
                "%\n金融压力 " + state.bank.financialStress.ToString("0") + " / 100",
                18, UiTheme.TextMain, 46f, 166f, 488f, 96f, TextAnchor.UpperLeft);
            int until = Mathf.Max(0, state.centralBank.meetingIntervalDays - (state.day - state.centralBank.lastPolicyDecisionDay));
            Button cut = UiTheme.MakeButton(decision, "降息 0.25", () => ChangePolicyRate(state, root, -.25f), 18);
            Button hold = UiTheme.MakeButton(decision, "保持", () => ChangePolicyRate(state, root, 0f), 18);
            Button raise = UiTheme.MakeButton(decision, "加息 0.25", () => ChangePolicyRate(state, root, .25f), 18);
            // 围猎五#8:冷却期内三按钮置灰,与 TrySetPolicyRate 的会议间隔守卫同口径
            // (bug搜#5 建设按钮先例)——否则按钮可按而核心恒拒绝,「可按 vs 弹 Toast 拒绝」自相矛盾。
            cut.interactable = hold.interactable = raise.interactable = until <= 0;
            PlaceTopLeft(cut.rectTransform(), 20f, 286f, 166f, 50f);
            PlaceTopLeft(hold.rectTransform(), 197f, 286f, 166f, 50f);
            PlaceTopLeft(raise.rectTransform(), 374f, 286f, 166f, 50f);
            UiTheme.SetButtonStyle(cut, FinanceTeal, FinanceNavy);
            UiTheme.SetButtonStyle(raise, FinanceGold, UiTheme.Ink);
            FinanceText(decision, until > 0 ? "下次正式会议还有 " + until + " 天" : "今日可做出新的政策决定",
                16, until > 0 ? UiTheme.TextSub : UiTheme.Good, 20f, 352f, 540f, 34f, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform transmission = FinanceCardPanel(content, "政策传导", "金色：政策利率 · 绿色：企业贷款利率", 600f, 184f, 612f, 470f);
            BuildTransmissionSteps(transmission, state);
            FinanceText(transmission, InterestRateService.TransmissionStageText(state), 17, UiTheme.TextMain,
                22f, 174f, 568f, 52f, TextAnchor.UpperLeft, FontStyle.Bold);
            BuildRateHistoryGraph(transmission, state, 22f, 258f, 568f, 170f);
        }

        void BuildFinancialStress(RectTransform content, GameState state, GameObject root)
        {
            RectTransform status = FinanceCardPanel(content, "银行压力测试", "资产充足不等于手上随时有足够准备金", 0f, 184f, 580f, 470f);
            FinanceText(status, FinancialSystemService.StabilityLabel(state), 34,
                state.bank.liquidityCrisis || state.bank.Insolvent ? UiTheme.Bad : UiTheme.Good,
                20f, 58f, 540f, 54f, TextAnchor.MiddleCenter, FontStyle.Bold);
            FinanceText(status, "准备金 ¥" + state.bank.reserves.ToString("0") + "\n未满足提款 ¥" + state.bank.bankRunUnmetWithdrawals.ToString("0") +
                "\n央行紧急贷款 ¥" + state.bank.centralBankBorrowing.ToString("0") + "\n银行资本 ¥" + state.bank.equity.ToString("0"),
                19, UiTheme.TextMain, 62f, 134f, 456f, 126f, TextAnchor.UpperLeft);
            Button run = UiTheme.MakeButton(status, "触发：50% 存款人同时提款", () =>
            {
                float paid = CreditService.TriggerBankRun(state, .5f);
                ShowToast("实际兑付 ¥" + paid.ToString("0") + "，未满足 ¥" + state.bank.bankRunUnmetWithdrawals.ToString("0"));
                ReloadFinance(root, 4);
            }, 18);
            PlaceTopLeft(run.rectTransform(), 42f, 284f, 496f, 52f);
            UiTheme.SetButtonStyle(run, UiTheme.Bad, Color.white);
            run.interactable = state.progression.completedChapter >= 40 && !state.bank.bankRunActive;
            FinanceText(status, "实验只改变真实账本：兑付会同时减少存款负债与准备金资产。",
                16, UiTheme.TextSub, 42f, 350f, 496f, 60f, TextAnchor.UpperLeft);

            RectTransform response = FinanceCardPanel(content, "最后贷款人", "紧急流动性缓解兑付压力，但不会消除坏账", 600f, 184f, 612f, 470f);
            FinanceText(response, "需要准备金", 17, UiTheme.TextSub, 20f, 62f, 250f, 28f);
            float requiredLiquidity = Mathf.Max(500f, state.bank.RequiredReserves - state.bank.reserves + state.bank.bankRunUnmetWithdrawals);
            FinanceText(response, "¥ " + requiredLiquidity.ToString("0"), 30, FinanceGold,
                20f, 88f, 250f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(response, "资产是否资不抵债", 17, UiTheme.TextSub, 310f, 62f, 260f, 28f);
            FinanceText(response, state.bank.Insolvent ? "是" : "否", 30, state.bank.Insolvent ? UiTheme.Bad : UiTheme.Good,
                310f, 88f, 260f, 44f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(response, "<b>流动性危机</b>：有资产，但暂时付不出。\n<b>偿付能力危机</b>：资产已小于负债，补准备金也不会修复资本。",
                18, UiTheme.TextMain, 32f, 166f, 548f, 92f, TextAnchor.UpperLeft);
            Button lend = UiTheme.MakeButton(response, "央行提供紧急流动性 ¥" + requiredLiquidity.ToString("0"), () =>
            {
                bool ok = CreditService.ProvideEmergencyLiquidity(state, requiredLiquidity, out string message);
                ShowToast(message); if (ok) ReloadFinance(root, 4);
            }, 18);
            PlaceTopLeft(lend.rectTransform(), 44f, 286f, 524f, 52f);
            UiTheme.SetButtonStyle(lend, FinanceTeal, FinanceNavy);
            lend.interactable = state.bank.bankRunActive && state.centralBank.unlocked;
            Button mix = UiTheme.MakeButton(response, "前往财政政策·完成政策组合", () =>
            {
                ClosePanel(root); ShowMacroDashboard(4);
            }, 17);
            PlaceTopLeft(mix.rectTransform(), 44f, 354f, 524f, 46f);
        }

        void ChangePolicyRate(GameState state, GameObject root, float delta)
        {
            bool ok = InterestRateService.TrySetPolicyRate(state, state.centralBank.policyRate + delta, out string message);
            if (ok)
            {
                state.UnlockCard("monetarytransmission"); state.UnlockCard("policylag");
                state.UnlockCard(delta < 0f ? "expansionarymonetary" : delta > 0f ? "contractionarymonetary" : "policyrate");
            }
            ShowToast(message);
            if (ok) ReloadFinance(root, 3);
        }

        void ReloadFinance(GameObject root, int tab)
        {
            ClosePanel(root);
            ShowFinanceCenter(tab);
        }

        void BuildTransmissionSteps(RectTransform parent, GameState state)
        {
            PolicyTransmissionState t = state.centralBank.transmission;
            string[] labels = { "政策利率", "银行报价", "消费 / 投资", "GDP / 通胀" };
            for (int i = 0; i < labels.Length; i++)
            {
                bool reached = i == 0 || i == 1 && t.bankPassThrough > .01f || i == 2 && t.realEconomyPassThrough > .01f || i == 3 && t.stage >= 3;
                Image box = UiTheme.MakeImage(parent, reached ? FinanceTeal : FinanceSoft, "transmission_" + i);
                PlaceTopLeft(box.rectTransform, 18f + i * 145f, 64f, 126f, 58f);
                FinanceText(box.rectTransform, labels[i], 16, reached ? FinanceNavy : UiTheme.TextSub,
                    4f, 8f, 118f, 42f, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (i < labels.Length - 1)
                    FinanceText(parent, "→", 24, FinanceGold, 142f + i * 145f, 72f, 22f, 38f, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            FinanceText(parent, "银行传导 " + (t.bankPassThrough * 100f).ToString("0") + "%  ·  实体传导 " +
                (t.realEconomyPassThrough * 100f).ToString("0") + "%", 16, UiTheme.TextSub,
                18f, 136f, 574f, 28f, TextAnchor.MiddleCenter);
        }

        void BuildRateHistoryGraph(RectTransform parent, GameState state, float x, float y, float width, float height)
        {
            int count = state.macro.history != null ? Mathf.Min(12, state.macro.history.Count) : 0;
            if (count < 2)
            {
                Image empty = UiTheme.MakeImage(parent, FinanceSoft, "rateHistoryEmpty");
                PlaceTopLeft(empty.rectTransform, x, y, width, height);
                FinanceText(empty.rectTransform, "至少完成 2 天统一结算后，\n这里才会绘制「政策利率 → 银行贷款利率」的真实走势。",
                    17, UiTheme.TextSub, 32f, 54f, width - 64f, 82f, TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }
            float graphX = x + 58f;
            float graphY = y + 18f;
            float graphWidth = width - 70f;
            float graphHeight = height - 48f;
            GameObject graphObject = new GameObject("rateHistoryGraph", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolylineGraph));
            graphObject.transform.SetParent(parent, false);
            RectTransform rt = graphObject.GetComponent<RectTransform>();
            PlaceTopLeft(rt, graphX, graphY, graphWidth, graphHeight);
            PolylineGraph graph = graphObject.GetComponent<PolylineGraph>();
            List<Vector2> policy = new List<Vector2>();
            List<Vector2> loans = new List<Vector2>();
            float min = Mathf.Min(state.centralBank.policyRate, state.bank.companyLoanRate);
            float max = Mathf.Max(state.centralBank.policyRate, state.bank.companyLoanRate);
            for (int i = state.macro.history.Count - count; i < state.macro.history.Count; i++)
            {
                min = Mathf.Min(min, state.macro.history[i].policyRate, state.macro.history[i].companyLoanRate);
                max = Mathf.Max(max, state.macro.history[i].policyRate, state.macro.history[i].companyLoanRate);
            }
            min = Mathf.Max(0f, min - 1f); max += 1f;
            for (int j = 0; j < count; j++)
            {
                MacroPeriodRecord record = state.macro.history[state.macro.history.Count - count + j];
                float px = count <= 1 ? 0f : j / (float)(count - 1);
                policy.Add(new Vector2(px, Mathf.InverseLerp(min, max, record.policyRate)));
                loans.Add(new Vector2(px, Mathf.InverseLerp(min, max, record.companyLoanRate)));
            }
            graph.lineColor = FinanceGold;
            graph.lineColorB = FinanceTeal;
            graph.SetData(policy, null, loans);
            MacroPeriodRecord first = state.macro.history[state.macro.history.Count - count];
            MacroPeriodRecord last = state.macro.history[state.macro.history.Count - 1];
            // 半屏金融卡使用紧凑坐标：通用图表的 5 组轴标签会在缩放后挤到说明和按钮。
            // 保留真实上下界与首末日，曲线本身仍由同一 PolylineGraph 绘制。
            FinanceText(parent, max.ToString("0.00") + "%", 14, UiTheme.TextSub,
                x, graphY - 2f, 74f, 22f, TextAnchor.MiddleRight);
            FinanceText(parent, min.ToString("0.00") + "%", 14, UiTheme.TextSub,
                x, graphY + graphHeight - 20f, 74f, 22f, TextAnchor.MiddleRight);
            FinanceText(parent, "Day " + first.period, 14, UiTheme.TextSub,
                graphX, graphY + graphHeight + 2f, 100f, 22f, TextAnchor.MiddleLeft);
            FinanceText(parent, "Day " + last.period, 14, UiTheme.TextSub,
                graphX + graphWidth - 100f, graphY + graphHeight + 2f, 100f, 22f, TextAnchor.MiddleRight);
        }

        RectTransform FinanceCardPanel(RectTransform parent, string title, string caption,
            float x, float y, float width, float height)
        {
            Image card = UiTheme.MakeImage(parent, FinanceCard, "finance_" + title);
            PlaceTopLeft(card.rectTransform, x, y, width, height);
            Image marker = UiTheme.MakeImage(card.transform, FinanceTeal, "marker");
            PlaceTopLeft(marker.rectTransform, 16f, 12f, 5f, 34f);
            FinanceText(card.rectTransform, title, 21, UiTheme.TextMain, 34f, 6f, width - 50f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold);
            FinanceText(card.rectTransform, caption, 14, UiTheme.TextSub, 34f, 31f, width - 50f, 24f, TextAnchor.MiddleLeft);
            return card.rectTransform;
        }

        void FinanceMetric(RectTransform parent, string label, string value, float x, Color valueColor)
        {
            Image card = UiTheme.MakeImage(parent, FinanceCard, "financeMetric_" + label);
            PlaceTopLeft(card.rectTransform, x, 48f, 232f, 60f);
            FinanceText(card.rectTransform, label, 14, UiTheme.TextSub, 12f, 4f, 208f, 22f, TextAnchor.MiddleCenter);
            FinanceText(card.rectTransform, value, 21, valueColor, 12f, 25f, 208f, 30f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void FinanceLedgerRow(RectTransform parent, string label, float value, float y, bool total = false)
        {
            FinanceText(parent, label, total ? 19 : 17, total ? FinanceGold : UiTheme.TextMain,
                24f, y, 310f, 32f, TextAnchor.MiddleLeft, total ? FontStyle.Bold : FontStyle.Normal);
            FinanceText(parent, "¥ " + value.ToString("0.00"), total ? 20 : 17,
                total ? FinanceGold : UiTheme.TextMain, 344f, y, parent.rect.width - 368f, 32f,
                TextAnchor.MiddleRight, total ? FontStyle.Bold : FontStyle.Normal);
            if (total)
            {
                Image line = UiTheme.MakeImage(parent, FinanceSoft, "ledgerRule");
                PlaceTopLeft(line.rectTransform, 24f, y - 6f, parent.rect.width - 48f, 2f);
            }
        }

        Text FinanceText(RectTransform parent, string value, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            Text text = UiTheme.MakeText(parent, value, size, color, anchor, style);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            PlaceTopLeft(text.rectTransform, x, y, width, height);
            return text;
        }
    }
}
