#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.9.15 第一批(全镇逻辑 bug 搜寻·高危四条)回归:公共项目建设成本双扣/
    /// 读档撤销当日玩家按揭打错记账腿/居民重放漏回滚技能可白拿/银行资产负债表漏列镇部门存款
    /// ——逐条钉死修复后的口径,防止回归。</summary>
    public class EconTownV0915BugfixTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;
        string tempDir;

        GameState NewWorld(string name)
        {
            GameObject go = new GameObject(name);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            DaySimulator.SetRandomSeed(20260903);
            s.experience.settings.difficulty = DifficultyMode.Simulation;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "围猎镇民", ResidentProfession.StoreClerk,
                ResidentPreference.Saver);
            GovernmentSimulator.Ensure(s);
            MacroEconomySimulator.Ensure(s);
            FinancialSystemService.Ensure(s);
            HousingService.Ensure(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            state = NewWorld("V0915World");
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV0915Tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideSaveDirectory(null);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch (System.Exception) { }
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        // ————— bug搜#5:BuildProject 建设成本只准经收日结算扣一次 —————

        [Test]
        public void BuildProject_CashChargedExactlyOnce_AcrossSettlement()
        {
            // 现金口径自洽:BuildProject 不动现金,当期现金变化全部来自结算余额——
            // cash = 建设前 cash + budgetBalance(造价恰含一次)。双扣版本是
            // cash = 建设前 cash − buildCost + budgetBalance(差恰为造价,必红)。
            // 不用 A/B 现金差对照:路灯的 serviceGain 会经 UpdateCityMetrics 传导到
            // 当期福利等收支(政策第二效应),现金差 ≠ 恰好造价,那不是双扣问题。
            GameState with = NewWorld("V0915With"), without = NewWorld("V0915Without");
            PublicProjectState lights = with.government.projects.Find(x => x.type == PublicProjectType.StreetLights);
            Assert.IsNotNull(lights);
            Assert.Greater(lights.buildCost, 0f);

            float cashAtBuild = with.government.cash;
            Assert.IsTrue(GovernmentSimulator.BuildProject(with, PublicProjectType.StreetLights));
            Assert.AreEqual(cashAtBuild, with.government.cash, .01f,
                "拨款只挂 pendingCapitalSpending,不得即时扣现金(对照 ResolveEvent 的事件治理支出惯例)。");

            GovernmentCycleRecord withRecord = GovernmentSimulator.AdvanceCycle(with);
            float cash0 = without.government.cash;   // 结算前快照(与建设世界的 cashAtBuild 同口径)
            GovernmentCycleRecord withoutRecord = GovernmentSimulator.AdvanceCycle(without);
            // 围猎五#10:改 A/B 差分精确锁定「恰一次」——GreaterOrEqual 单边下界拦不住
            // 「结算科目内双计(2×造价)」形态(现金两断言在该形态下同样自洽);差分把
            // 两世界共同的 directFiscalPurchase 等背景支出消掉,差额必须恰等于造价。
            Assert.AreEqual(lights.buildCost, withRecord.capitalSpending - withoutRecord.capitalSpending, .01f,
                "造价必须出现在周期账本的资本支出科目里(恰一次)——建设世界比对照世界的资本支出恰多一个造价。");
            Assert.AreEqual(cashAtBuild + withRecord.budgetBalance, with.government.cash, .01f,
                "建设世界:现金只经结算变动一次(旧代码:即时扣+结算再扣=2×造价)。");
            Assert.AreEqual(cash0 + withoutRecord.budgetBalance, without.government.cash, .01f,
                "不建设世界同样自洽——两世界的现金都只来自结算余额,无结算外扣款。");
        }

        [Test]
        public void BuildProject_GuardCountsPendingCapitalAgainstCash()
        {
            // 守卫必须把当日已挂账资本支出计入占用——删掉即时扣款后,这是防同日连建
            // 超发现金的唯一闸门(原即时扣款的隐性职责,随双扣修复一并转正)。
            // cash=1900 特意取「够公园造价 1800、但不够 1800+路灯挂账 1000」:旧口径
            // (只看 cash)此场景会放行——本断言红即守卫退回旧口径。
            GameState s = NewWorld("V0915Guard");
            PublicProjectState lights = s.government.projects.Find(x => x.type == PublicProjectType.StreetLights);
            PublicProjectState park = s.government.projects.Find(x => x.type == PublicProjectType.Park);
            Assert.IsNotNull(park, "护栏:默认项目表含公园(造价 1800)。");
            Assert.Less(park.buildCost, lights.buildCost * 2f, "护栏:公园贵于路灯,场景成立。");
            s.government.cash = park.buildCost + 100f;
            Assert.IsTrue(GovernmentSimulator.BuildProject(s, PublicProjectType.StreetLights));
            Assert.IsFalse(GovernmentSimulator.BuildProject(s, PublicProjectType.StreetLights),
                "同项目不可重复建设(built 守卫)。");
            Assert.IsFalse(GovernmentSimulator.BuildProject(s, PublicProjectType.Park),
                "现金 1900 够公园造价,但当日已挂账 1000——必须拦(旧口径只看 cash 会放行,结算后凭空发债)。");
        }

        [Test]
        public void ReplayDay_BuiltProjectCapitalPending_RehungOnLoad()
        {
            // bug搜#5复审:Report 阶段落的 checkpoint(挂账已持久化、结算未跑),恢复后资本挂账
            // 必须按「本周期建成项目」回挂——旧逻辑无条件清零,路灯造价凭空消失(0×扣款)。
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            PublicProjectState lights = state.government.projects.Find(x => x.type == PublicProjectType.StreetLights);
            float cashBefore = state.government.cash;
            Assert.IsTrue(GovernmentSimulator.BuildProject(state, PublicProjectType.StreetLights));
            Assert.AreEqual(lights.buildCost, state.government.pendingCapitalSpending, .01f);

            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(lights.buildCost, state.government.pendingCapitalSpending, .01f,
                "恢复后按本周期建成项目回挂造价(裸挂账/事件类不回挂——重放会重新挂,回挂会双计)。");
            float cashAtReplay = state.government.cash;
            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            Assert.GreaterOrEqual(record.capitalSpending, lights.buildCost,
                "重放日结算的资本支出必须含这笔造价。");
            Assert.AreEqual(cashAtReplay + record.budgetBalance, state.government.cash, .01f,
                "结算恰扣一次(0×=白拿,2×=双扣,都不允许)。");
            Assert.AreNotEqual(cashBefore, state.government.cash, "护栏:结算确实动了现金。");
        }

        // ————— bug搜#2:读档撤销当日新发贷款,玩家按揭豁免、消费贷照撤 —————

        [Test]
        public void ReplayDay_PlayerMortgage_Exempt_ConsumptionLoanStillUndone()
        {
            // 当日营业已结算(droppedTodayRecord)后注入两笔当日贷款:玩家按揭(放款腿记镇住户池)
            // 与居民消费贷(放款腿记居民存款/储蓄)。恢复后按揭豁免、消费贷撤销——两种口径都不得串。
            state.planProduction = 100;
            DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));

            const float mortgagePrincipal = 29400f, consumptionPrincipal = 500f;
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                purpose = LoanPurpose.Mortgage,
                status = LoanStatus.Current,
                principal = mortgagePrincipal,
                remainingPrincipal = mortgagePrincipal,
                originatedWorldDay = state.day
            });
            state.bank.townSectorDeposits += mortgagePrincipal;   // 按揭放款腿(卖方收款,不进居民储蓄)
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                purpose = LoanPurpose.Consumption,
                status = LoanStatus.Current,
                principal = consumptionPrincipal,
                remainingPrincipal = consumptionPrincipal,
                originatedWorldDay = state.day
            });
            state.resident.savings += consumptionPrincipal;       // 消费贷放款腿(居民侧)
            state.bank.householdDeposits += consumptionPrincipal;

            float savings0 = state.resident.savings;
            float town0 = state.bank.townSectorDeposits;
            float household0 = state.bank.householdDeposits;
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));

            Assert.AreEqual(1, state.bank.loans.Count,
                "玩家按揭与购房合同同口径豁免(合同不回滚则贷款不撤);消费贷照常撤销。");
            Assert.AreEqual(LoanPurpose.Mortgage, state.bank.loans[0].purpose);
            Assert.AreEqual(savings0 - consumptionPrincipal, state.resident.savings, .01f,
                "消费贷本金退回;按揭本金从未进居民储蓄,不得再扣一次(旧代码两笔各扣一次,储蓄被清零)。");
            Assert.AreEqual(town0, state.bank.townSectorDeposits, .01f,
                "按揭放款腿(镇住户池)不动。");
            Assert.AreEqual(household0 - consumptionPrincipal, state.bank.householdDeposits, .01f,
                "消费贷的存款镜像撤销,按揭的镜像(在镇住户池)不受消费贷口径污染。");
        }

        // ————— bug搜#8:居民重放回滚技能到清晨快照,封死「学习→存档→恢复」白拿循环 —————

        [Test]
        public void ReplayDay_SkillLevel_RolledBackToMorningSnapshot()
        {
            state.resident.lifeDay = 4;
            state.day = 4;
            state.resident.skillLevel = 6f;        // 昨日累计技能(清晨快照基线)
            state.resident.reportReady = true;     // 上一日已完成——BeginNextDay 的推进闸
            ResidentEconomy.BeginNextDay(state);   // 清晨:快照 skillLevelAtDayStart=6
            Assert.AreEqual(6f, state.resident.skillLevelAtDayStart, .01f);

            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);   // 学习 → skillLevel=8
            Assert.AreEqual(8f, state.resident.skillLevel, .01f);
            ResidentEconomy.EndDay(state);        // 生成当日居民记录(residentReplay 判据)
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(6f, state.resident.skillLevel, .01f,
                "重放回滚技能到清晨快照——白拿循环封死(旧代码:每轮白赚 2 点,封顶 40 即日薪乘数虚高 40%)。");

            // 恢复后再学:从清晨基线 6 重新到 8,而非 10——多轮存读不叠加。
            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);
            Assert.AreEqual(8f, state.resident.skillLevel, .01f);
        }

        [Test]
        public void ReplayDay_FirstDaySkillZero_AlsoRolledBack()
        {
            // bug搜#8复审:首日从未跑过 BeginNextDay,清晨快照只能来自 CreateProfile(=0);
            // 旧版 >0f 守卫把合法快照 0 误判为旧档跳过——白拿循环在首日仍然开放,本用例钉死。
            state.resident.lifeDay = state.day;   // CreateProfile 已置 skillLevelAtDayStart=0
            Assert.AreEqual(0f, state.resident.skillLevelAtDayStart, .01f,
                "护栏:CreateProfile 初始化首日清晨快照为 0(非哨兵 -1)。");
            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);
            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);
            Assert.AreEqual(4f, state.resident.skillLevel, .01f);
            ResidentEconomy.EndDay(state);
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(0f, state.resident.skillLevel, .01f,
                "首日清晨技能 0 是合法快照,同样要回滚(白拿 4 点不得残留)。");
        }

        // ————— bug搜#16:资产负债表负债侧必须列「镇部门存款」,分项加总=负债合计 —————

        [Test]
        public void BankBalanceSheet_LiabilitiesRowCoversTownSectorDeposits()
        {
            state.progression.completedChapter = 32;   // 越过金融中心门禁与资产负债 unlocked 线
            state.bank.townSectorDeposits = 1234.56f;  // 居民付租/还按揭即有余额的科目(特异金额防合计行代满足)
            UIManager[] stale = Resources.FindObjectsOfTypeAll<UIManager>();
            for (int i = 0; i < stale.Length; i++)
                if (stale[i] != null && stale[i].gameObject.scene.IsValid())
                    Object.DestroyImmediate(stale[i].gameObject);
            UIManager ui = UIManager.Create();
            try
            {
                ui.InitializeForValidation();
                ui.ShowFinanceCenter(1);   // tab 1 = 资产负债
                Canvas.ForceUpdateCanvases();

                Text[] texts = ui.GetComponentsInChildren<Text>(true);
                bool hasTownRow = false, hasTownAmount = false, hasTotal = false;
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].text == null) continue;
                    if (texts[i].text.Contains("镇部门存款")) hasTownRow = true;
                    // 金额精确匹配整行(FinanceLedgerRow 的金额格式)——Contains("1234.56") 会被
                    // 负债合计行代满足,行金额错绑时测试不红。
                    if (texts[i].text == "¥ 1234.56") hasTownAmount = true;
                    if (texts[i].text.Contains("负债合计")) hasTotal = true;
                }
                Assert.IsTrue(hasTownRow,
                    "负债侧必须列出「镇部门存款」——合计(TotalLiabilities)内部含它,漏行则分项加总恒比合计少这截,账本教学口径不闭合。");
                Assert.IsTrue(hasTownAmount, "镇部门存款行的金额必须以独立行可见(¥ 1234.56)。");
                Assert.IsTrue(hasTotal, "负债合计行仍在。");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }
    }
}
#endif
