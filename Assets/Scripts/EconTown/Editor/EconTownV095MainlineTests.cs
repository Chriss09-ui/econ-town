#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.5《第八篇·资产与杠杆》(PRD §197.2-197.5/§197.7/§197.8):主线 54-58 章、
    /// 五个 housing.* 信号、储蓄三去向、21 张知识卡、53 位点清理。共 8 例,随实施步骤分批落地。</summary>
    public class EconTownV095MainlineTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;

        GameState NewWorld()
        {
            GameObject go = new GameObject("V095World" + worlds.Count);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260831);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "第八篇镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            state = NewWorld();
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        /// <summary>实验室标准买房世界(V094 BuyHouseForLab 直译):储蓄垫足 40000(差额镜像准备金)、
        /// dailyIncome 600/todayIncome 800 过授信 DSR。</summary>
        void BuyHouseForLab(GameState s)
        {
            s.resident.savings = 40000f;
            s.bank.reserves += 40000f - s.bank.householdDeposits;
            s.bank.householdDeposits = 40000f;
            s.resident.dailyIncome = 600f;
            s.resident.todayIncome = 800f;
            string msg;
            Assert.IsTrue(MortgageService.TryBuyHouse(s, out msg), msg);
        }

        // ———————————————— #1 篇结构与枚举 ————————————————

        [Test]
        public void ChapterCount_58_PartHousing_EnumTail9()
        {
            var chapters = ProgressionService.Chapters;
            Assert.AreEqual(58, chapters.Count, "主线应注册到第 58 章。");
            for (int i = 0; i < chapters.Count; i++)
                Assert.AreEqual(i + 1, chapters[i].number, "章号必须连续。");
            Assert.AreEqual(9, (int)LearningPart.Housing, "第八篇枚举尾值 9,不插队。");
            Assert.AreEqual(LearningPart.Trade, chapters[52].part, "第 53 章仍是第七篇(三元悖论章)。");
            Assert.AreEqual(LearningPart.Housing, chapters[53].part, "第 54 章起属第八篇。");
            Assert.AreEqual(LearningPart.Housing, chapters[57].part, "第 58 章收弧。");
            Assert.AreEqual("第八篇 · 资产与杠杆", ProgressionService.PartTitle(LearningPart.Housing));

            // 行为驱动承诺:第八篇每个章节信号都必须以 housing. 前缀落账。
            for (int i = 53; i < chapters.Count; i++)
                StringAssert.StartsWith("housing.", chapters[i].requiredSignal,
                    "第 " + chapters[i].number + " 章的信号必须属于第八篇命名空间。");
            Assert.AreEqual(ProgressSignals.HousingRentPaid, chapters[53].requiredSignal);
            Assert.AreEqual(ProgressSignals.HousingMortgageIssued, chapters[54].requiredSignal);
            Assert.AreEqual(ProgressSignals.HousingPriceBreakdown, chapters[55].requiredSignal);
            Assert.AreEqual(ProgressSignals.HousingDrawdownSurvived, chapters[56].requiredSignal);
            Assert.AreEqual(ProgressSignals.HousingAssetsDiversified, chapters[57].requiredSignal);

            // V0.9.5 过场六:楼市狂热头版(ShouldPlay 只认 Inflating 相位;八件套随 Count 循环被 V074 覆盖)。
            // V0.9.17 晋升线:Count 6→11(五段篇间晋升过场);八件套自测在 V0917,此处只钉尾值。
            Assert.AreEqual(11, CutsceneService.Count);
            Assert.AreEqual((int)CutsceneId.Homecoming, CutsceneService.Count, "枚举尾=Homecoming,1–6 旧值不动。");
            Assert.AreEqual("楼市狂热头版", CutsceneService.Title((int)CutsceneId.HousingMania));
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.HousingMania), "非狂热相位不播。");
            state.housing.leveragePhase = LeveragePhase.Inflating;
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.HousingMania), "狂热相位首播。");
            Assert.IsTrue(CutsceneService.Happened(state, (int)CutsceneId.HousingMania), "当前狂热相位=已发生。");
            CutsceneService.MarkViewed(state, (int)CutsceneId.HousingMania);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.HousingMania), "一次性(IsViewed 闸)。");

            // 旧档预填路径:相位已回落(None),但 history 里留有 Inflating 记录。
            state.housing.leveragePhase = LeveragePhase.None;
            Assert.IsFalse(CutsceneService.Happened(state, (int)CutsceneId.HousingMania), "无历史且非狂热=未发生。");
            state.housing.history.Add(new HousingPeriodRecord { period = 1, phase = LeveragePhase.Inflating });
            Assert.IsTrue(CutsceneService.Happened(state, (int)CutsceneId.HousingMania), "历史含狂热记录=已发生(预填)。");

            // Finale 随第八篇迁址(§197.7):53 章不再是主线终点,58 章才预填。
            state.progression.completedChapter = 57;
            Assert.IsFalse(CutsceneService.Happened(state, (int)CutsceneId.Finale), "cc=57 时 Finale 未发生。");
            state.progression.completedChapter = 58;
            Assert.IsTrue(CutsceneService.Happened(state, (int)CutsceneId.Finale), "cc=58 时 Finale 预填。");
        }

        // ———————————————— #2 篇界报告＋旧档兼容 ————————————————

        [Test]
        public void PartCompleted_At58_EighthStageReport_AndOldSaveReopensPart8()
        {
            // A:注入全部 58 个必答信号(等价于玩家真实完成所有行为),走 Evaluate 管线连过章。
            var chapters = ProgressionService.Chapters;
            for (int i = 0; i < chapters.Count; i++)
                ProgressionService.RecordSignal(state, chapters[i].requiredSignal);

            Assert.IsTrue(state.progression.mainlineComplete, "全信号注入后主线必须完成。");
            Assert.AreEqual(58, state.progression.completedChapter);
            Assert.IsTrue(state.progression.sandboxUnlocked);
            Assert.AreEqual(8, state.progression.stageReports.Count, "八个篇界各出一份阶段报告。");
            StageReportRecord housingReport = state.progression.stageReports.Find(
                r => r.part == LearningPart.Housing);
            Assert.IsNotNull(housingReport, "第八篇报告必须存在。");
            StringAssert.Contains("杠杆", housingReport.learnedSummary, "三摘要 Housing 分支已接入。");
            StringAssert.Contains("房价指数", housingReport.resultSummary, "结业读数引用真实住房状态。");
            while (ProgressionService.GetPendingStageReport(state) != null)
                ProgressionService.MarkPendingStageReportViewed(state);
            Assert.AreEqual(0, state.progression.pendingStageReports.Count, "弹窗队列可被完整消费。");

            // B:旧档兼容——V0.7 通关态存档(无 housing 块)读入后,第八篇自动开放且保留沙盒。
            state.progression.completedChapter = 53;
            state.progression.currentChapter = 54;
            state.progression.mainlineComplete = true;
            SaveSystem.SaveData data = SaveSystem.BuildSaveData(state);
            data.housing = null;   // 剥掉 housing 块,等价 V0.7 旧档键集
            string json = JsonUtility.ToJson(data, false);

            GameObject targetObject = new GameObject("V095OldSaveTarget");
            try
            {
                GameState target = targetObject.AddComponent<GameState>();
                SaveSystem.ApplyToState(target, JsonUtility.FromJson<SaveSystem.SaveData>(json));
                Assert.IsNotNull(target.housing, "缺失 housing 块时 ApplyToState 必须补默认 HousingState。");
                HousingService.Ensure(target);
                Assert.IsTrue(target.housing.renting, "旧档读入后处于租房态。");
                Assert.AreEqual("", target.housing.mortgageLoanId, "旧档无贷款。");
                Assert.AreEqual(100f, target.housing.indexLevel, 0.001f, "镇景指数基期 100。");
                Assert.IsFalse(target.progression.mainlineComplete, "主线延长后旧档通关态必须重开。");
                Assert.AreEqual(54, target.progression.currentChapter, "第八篇对旧档自动开放。");
                ChapterDefinition current = ProgressionService.Current(target);
                Assert.IsNotNull(current);
                Assert.AreEqual(LearningPart.Housing, current.part);
                Assert.IsTrue(target.progression.sandboxUnlocked, "旧档沙盒进度保留。");
            }
            finally { Object.DestroyImmediate(targetObject); }
        }

        // ———————————————— #3 信号·租金（第 54 章） ————————————————

        [Test]
        public void Signal_RentPaid_UnlocksCh54()
        {
            state.progression.completedChapter = 53;
            state.progression.currentChapter = 54;
            state.resident.savings = 1000f;   // 够付首期租金
            state.phase = GamePhase.Morning;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked), blocked);
            DayRecord last = state.LastRecord;
            Assert.IsNotNull(last);
            Assert.Greater(last.rentPaid, 0f, "首期租金已从存款划转房东。");
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.HousingRentPaid));
            Assert.AreEqual(54, state.progression.completedChapter, "第 54 章凭真实租金账本行解锁。");
            Assert.AreEqual(55, state.progression.currentChapter);

            // 反例①:存款不足 → 欠租,不采。
            GameState broke = NewWorld();
            broke.progression.completedChapter = 53;
            broke.progression.currentChapter = 54;
            broke.resident.savings = 0f;
            broke.phase = GamePhase.Morning;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(broke, out _, out blocked), blocked);
            Assert.IsFalse(ProgressionService.HasSignal(broke, ProgressSignals.HousingRentPaid), "欠租不采。");
            Assert.AreEqual(53, broke.progression.completedChapter);

            // 反例②:自有房(不在租),不采。
            GameState owner = NewWorld();
            owner.progression.completedChapter = 53;
            owner.progression.currentChapter = 54;
            owner.housing.renting = false;
            owner.resident.savings = 1000f;
            owner.phase = GamePhase.Morning;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(owner, out _, out blocked), blocked);
            Assert.IsFalse(ProgressionService.HasSignal(owner, ProgressSignals.HousingRentPaid), "非租房态不采。");
        }

        // ———————————————— #4 信号·房贷放款（第 55 章） ————————————————

        [Test]
        public void Signal_MortgageIssued_UnlocksCh55()
        {
            state.progression.completedChapter = 54;
            state.progression.currentChapter = 55;
            BuyHouseForLab(state);
            ProgressionService.OnDaySettled(state);
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.HousingMortgageIssued),
                "贷款簿存在居民按揭(放款入账)即采。");
            Assert.AreEqual(55, state.progression.completedChapter);
            Assert.AreEqual(56, state.progression.currentChapter);

            // 反例:未购房,采集器不得自赠。
            GameState renter = NewWorld();
            renter.progression.completedChapter = 54;
            renter.progression.currentChapter = 55;
            ProgressionService.OnDaySettled(renter);
            Assert.IsFalse(ProgressionService.HasSignal(renter, ProgressSignals.HousingMortgageIssued));
            Assert.AreEqual(54, renter.progression.completedChapter);
        }

        // ———————————————— #5 信号·房价分解（第 56 章） ————————————————

        [Test]
        public void Signal_PriceBreakdown_UnlocksCh56()
        {
            state.progression.completedChapter = 55;
            state.progression.currentChapter = 56;

            // 反例②:没有房价历史时,即便读过(计数>0)也不采——分解为 null。
            state.housing.decomposeReads = 1;
            ProgressionService.OnDaySettled(state);
            Assert.IsFalse(ProgressionService.HasSignal(state, ProgressSignals.HousingPriceBreakdown),
                "无历史=分解为 null,不采。");
            state.housing.decomposeReads = 0;

            // 真实跑一期,房价引擎写入 history 分量。
            state.phase = GamePhase.Morning;
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string blocked), blocked);
            HousingService.PriceBreakdownInfo? info = HousingService.PriceBreakdown(state);
            Assert.IsNotNull(info, "结算一期后分解非空。");
            HousingPeriodRecord rec = state.housing.history[state.housing.history.Count - 1];
            Assert.AreEqual(rec.demand, info.Value.demand, 1e-4f, "分解分量与引擎账本一致。");
            Assert.AreEqual(rec.supply, info.Value.supply, 1e-4f);
            Assert.AreEqual(rec.priceDelta, info.Value.priceDelta, 1e-4f);
            Assert.AreEqual(rec.periodRent, info.Value.periodRent, 1e-4f);

            // 反例①:未读分解(计数=0),采集器不得自赠。
            ProgressionService.OnDaySettled(state);
            Assert.IsFalse(ProgressionService.HasSignal(state, ProgressSignals.HousingPriceBreakdown),
                "纯读不计数——采集链严禁自赠信号。");

            // 正例:面板读数入口。
            HousingService.MarkPriceBreakdownRead(state);
            Assert.AreEqual(1, state.housing.decomposeReads);
            ProgressionService.OnDaySettled(state);
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.HousingPriceBreakdown));
            Assert.AreEqual(56, state.progression.completedChapter);
            Assert.AreEqual(57, state.progression.currentChapter);
        }

        // ———————————————— #6 信号·峰值回撤生还（第 57 章） ————————————————

        [Test]
        public void Signal_PeakDrawdownSurvived_UnlocksCh57()
        {
            state.progression.completedChapter = 56;
            state.progression.currentChapter = 57;
            BuyHouseForLab(state);
            state.housing.leveragePhase = LeveragePhase.Deflating;
            state.housing.peakPriceIndex = 120f;
            state.housing.townHousePrice = HousingService.BaseHousePrice;   // 镜像指数 100 → 回撤 16.7%
            ProgressionService.OnDaySettled(state);
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.HousingDrawdownSurvived),
                "在贷且回撤 ≥8% 即采。");
            Assert.AreEqual(57, state.progression.completedChapter);
            Assert.AreEqual(58, state.progression.currentChapter);

            // 反例①:回撤不足 8%(峰值 104 → 回撤 3.8%)。
            GameState shallow = NewWorld();
            shallow.progression.completedChapter = 56;
            shallow.progression.currentChapter = 57;
            BuyHouseForLab(shallow);
            shallow.housing.leveragePhase = LeveragePhase.Deflating;
            shallow.housing.peakPriceIndex = 104f;
            shallow.housing.townHousePrice = HousingService.BaseHousePrice;
            ProgressionService.OnDaySettled(shallow);
            Assert.IsFalse(ProgressionService.HasSignal(shallow, ProgressSignals.HousingDrawdownSurvived),
                "回撤不足阈值不采。");

            // 反例②:已违约(贷款 Defaulted)→ 不采。
            GameState defaulted = NewWorld();
            defaulted.progression.completedChapter = 56;
            defaulted.progression.currentChapter = 57;
            BuyHouseForLab(defaulted);
            LoanRecord loan = HousingService.CurrentMortgage(defaulted);
            Assert.IsNotNull(loan);
            loan.status = LoanStatus.Defaulted;
            defaulted.housing.leveragePhase = LeveragePhase.Deflating;
            defaulted.housing.peakPriceIndex = 120f;
            defaulted.housing.townHousePrice = HousingService.BaseHousePrice;
            ProgressionService.OnDaySettled(defaulted);
            Assert.IsFalse(ProgressionService.HasSignal(defaulted, ProgressSignals.HousingDrawdownSurvived),
                "违约贷款不算「扛过」。");

            // 反例③:无贷租客同处回撤 → 不采(章名《屋檐下的杠杆》,要求在贷)。
            GameState renter = NewWorld();
            renter.progression.completedChapter = 56;
            renter.progression.currentChapter = 57;
            renter.housing.leveragePhase = LeveragePhase.Deflating;
            renter.housing.peakPriceIndex = 120f;
            renter.housing.townHousePrice = HousingService.BaseHousePrice;
            ProgressionService.OnDaySettled(renter);
            Assert.IsFalse(ProgressionService.HasSignal(renter, ProgressSignals.HousingDrawdownSurvived),
                "无贷不采——教学点=扛着杠杆过回撤。");
        }

        // ———————————————— #8 卡库第八分类 ————————————————

        [Test]
        public void Cards_HousingBlock_21Registered_CategoryAndHints()
        {
            Assert.AreEqual(200, CardLibrary.All.Count, "卡库 179→200(V0.9.5 增 21 张)。");
            string[] ids =
            {
                "housing.downpayment", "housing.installment", "housing.annuity", "housing.ltv",
                "housing.collateral", "housing.amortization", "housing.rentVsBuy", "housing.priceBreakdown",
                "housing.supplyElasticity", "housing.capRate", "housing.wealthEffect", "housing.nominalReal",
                "housing.bubbleThree", "housing.minsky", "housing.deleveraging",
                "asset.allocation", "asset.riskReturn", "asset.liquidity", "asset.indexFund",
                "asset.transactionCost", "asset.treasury"
            };
            Assert.AreEqual(21, ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                CardDef def = CardLibrary.Get(ids[i]);
                Assert.IsNotNull(def, "卡片已注册:" + ids[i]);
                Assert.IsFalse(string.IsNullOrEmpty(def.title), ids[i] + " 有标题。");
                Assert.IsFalse(string.IsNullOrEmpty(def.body), ids[i] + " 有正文。");
                KnowledgeMetadata meta = KnowledgeSystem.GetMetadata(ids[i]);
                Assert.IsNotNull(meta, "元数据已建:" + ids[i]);
                Assert.AreEqual(KnowledgeCategory.Housing, meta.category, ids[i] + " 归第八分类。");
                Assert.IsNull(meta.experimentKey, "第八分类不挂实验台(无死链)。");
            }
            Assert.AreEqual("资产", KnowledgeSystem.CategoryName(KnowledgeCategory.Housing));
            Assert.AreEqual(21, KnowledgeSystem.Search(state, "", "资产").Count, "「资产」分类过滤命中 21 张。");
            StringAssert.Contains("资产配置", KnowledgeSystem.UnlockHint(state, "asset.indexFund"));
            StringAssert.Contains("泡沫仪表", KnowledgeSystem.UnlockHint(state, "housing.minsky"));
        }

        // ———————————————— #7 信号·三档资产与终态房贷（第 58 章）＋四腿守恒 ————————————————

        /// <summary>买入后立即提前还清(存款不足则镜像补足——householdDeposits 与 reserves 同加,BalanceGap 不动)。</summary>
        void BuyAndPayOff(GameState s)
        {
            BuyHouseForLab(s);
            LoanRecord loan = HousingService.CurrentMortgage(s);
            Assert.IsNotNull(loan);
            float topUp = loan.remainingPrincipal + 2000f - s.resident.savings;
            if (topUp > 0f)
            {
                s.resident.savings += topUp;
                s.bank.householdDeposits += topUp;
                s.bank.reserves += topUp;
            }
            string msg;
            Assert.IsTrue(MortgageService.TryEarlyPayoff(s, out msg), msg);
        }

        [Test]
        public void Signal_TerminalAndThreeAssets_UnlocksCh58()
        {
            // 反例:房贷终态＋存款动作,但国债/基金零动作 → 不采(三档必须各有一次)。
            GameState half = NewWorld();
            half.progression.completedChapter = 57;
            half.progression.currentChapter = 58;
            BuyAndPayOff(half);
            string msg;
            Assert.IsTrue(CreditService.DepositResidentCash(half, 100f, out msg), msg);
            ProgressionService.OnDaySettled(half);
            Assert.IsFalse(ProgressionService.HasSignal(half, ProgressSignals.HousingAssetsDiversified),
                "缺国债/基金动作,不采。");

            // 正例:终态房贷＋三档动作,含四腿守恒断言。
            state.progression.completedChapter = 57;
            state.progression.currentChapter = 58;
            BuyAndPayOff(state);
            // 还清后储蓄已薄:镜像补足三档动作本金(householdDeposits 与 reserves 同加,BalanceGap 不动)。
            float assetTopUp = 20000f - state.resident.savings;
            if (assetTopUp > 0f)
            {
                state.resident.savings += assetTopUp;
                state.bank.householdDeposits += assetTopUp;
                state.bank.reserves += assetTopUp;
            }
            LoanRecord paid = state.bank.loans.Find(l => l.purpose == LoanPurpose.Mortgage);
            Assert.IsNotNull(paid);
            Assert.AreEqual(LoanStatus.Paid, paid.status, "房贷走到 Paid 终态。");

            float gap0 = state.bank.BalanceGap;
            Assert.AreEqual(0f, gap0, 0.01f, "起点银行平衡。");

            // 国债认购:储蓄/负债/准备金三账户同减,政府 debt/cash 同增。
            float sav0 = state.resident.savings, dep0 = state.bank.householdDeposits, res0 = state.bank.reserves;
            float debt0 = state.government.debt, cash0 = state.government.cash;
            Assert.IsTrue(AssetAllocationService.TryBuyTreasury(state, 2000f, out msg), msg);
            Assert.AreEqual(2000f, state.housing.treasuryFace, 0.01f);
            Assert.AreEqual(sav0 - 2000f, state.resident.savings, 0.01f, "储蓄扣减。");
            Assert.AreEqual(dep0 - 2000f, state.bank.householdDeposits, 0.01f, "负债端同减。");
            Assert.AreEqual(res0 - 2000f, state.bank.reserves, 0.01f, "资产端同减。");
            Assert.AreEqual(debt0 + 2000f, state.government.debt, 0.01f, "政府债务入账。");
            Assert.AreEqual(cash0 + 2000f, state.government.cash, 0.01f, "政府现金入账。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, 0.01f, "国债认购银行平衡表零扰动。");
            Assert.AreEqual(1, state.housing.treasuryBuys);

            // 基金申购:托管现金+份额(指数基期 100 → 份额=金额)。
            sav0 = state.resident.savings; dep0 = state.bank.householdDeposits; res0 = state.bank.reserves;
            Assert.IsTrue(AssetAllocationService.TryBuyFund(state, 5000f, out msg), msg);
            Assert.AreEqual(5000f, state.housing.fundCash, 0.01f);
            Assert.AreEqual(5000f, state.housing.indexUnits, 0.01f, "基期 100 下份额=金额。");
            Assert.AreEqual(sav0 - 5000f, state.resident.savings, 0.01f);
            Assert.AreEqual(dep0 - 5000f, state.bank.householdDeposits, 0.01f);
            Assert.AreEqual(res0 - 5000f, state.bank.reserves, 0.01f);
            Assert.AreEqual(gap0, state.bank.BalanceGap, 0.01f, "申购银行平衡表零扰动。");

            // 指数演化:0.3×4% + 0.2×(88−68)/68 = 7.08% → 钳 +6% → 106。
            state.macro.gdpGrowth = 4f;
            state.government.enterpriseConfidence = 88f;
            state.macro.businessConfidence = 88f;
            AssetAllocationService.AdvanceIndex(state);
            Assert.AreEqual(106f, state.housing.indexLevel, 0.01f, "单期收益钳 ±6%。");

            // 赎回:gross=2500×1.06=2650,payout=2650×0.995=2636.75,价差经权益平账。
            float eq0 = state.bank.equity;
            Assert.IsTrue(AssetAllocationService.TryRedeemFund(state, 2500f, out msg), msg);
            Assert.AreEqual(2500f, state.housing.indexUnits, 0.01f);
            Assert.AreEqual(5000f - 2650f, state.housing.fundCash, 0.01f, "托管现金回表。");
            Assert.AreEqual(eq0 + 2650f - 2636.75f, state.bank.equity, 0.05f, "净损益经权益平账。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, 0.01f, "赎回银行平衡表零扰动。");
            Assert.AreEqual(2, state.housing.fundTrades, "申赎各计一次。");

            // 存款动作(既有机制)。
            Assert.IsTrue(CreditService.DepositResidentCash(state, 100f, out msg), msg);

            // 付息拆分(修复轮回E):份额分母=持券总额归一(bonds+treasuryFace),合并恒=1——
            // 旧 debt 分母在政府还本压低 debt 后会饱和把居民份额钳 0。
            float debt = state.government.debt;
            float bankBonds = state.bank.GovernmentBonds;
            float expectedResident = Mathf.Clamp01(2000f / (bankBonds + 2000f)) * 100f;
            sav0 = state.resident.savings; res0 = state.bank.reserves; dep0 = state.bank.householdDeposits;
            CreditService.ReceiveGovernmentBondInterest(state, 100f, debt);
            Assert.AreEqual(sav0 + expectedResident, state.resident.savings, 0.01f,
                "居民份额入储蓄(期望 ¥" + expectedResident.ToString("0.00") + ")。");
            Assert.AreEqual(dep0 + expectedResident, state.bank.householdDeposits, 0.01f, "负债端同加。");
            Assert.AreEqual(gap0, state.bank.BalanceGap, 0.01f, "付息拆分银行平衡表零扰动。");

            // 第 58 章解锁:主线完成、第八篇报告、沙盒即时传播。
            ProgressionService.OnDaySettled(state);
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.HousingAssetsDiversified));
            Assert.AreEqual(58, state.progression.completedChapter);
            Assert.IsTrue(state.progression.mainlineComplete, "第八篇收弧,主线完成。");
            // 本测试从 57 章直跳:只真实产出第八篇一份报告(全量 8 份由 V07Content FullMainlineRun 覆盖)。
            Assert.AreEqual(1, state.progression.stageReports.Count);
            Assert.AreEqual(LearningPart.Housing, state.progression.stageReports[0].part);
            Assert.IsTrue(state.progression.sandboxUnlocked);
            Assert.IsTrue(state.sandbox.unlocked, "沙盒解锁即时传播。");
        }
    }
}

#endif
