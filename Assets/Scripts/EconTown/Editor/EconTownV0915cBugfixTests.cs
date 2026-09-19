#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.9.15c 第三批(全镇逻辑 bug 搜寻·低中危十一条)回归:培训补贴死写入/
    /// 政府付息缺席日报/关税百分比格式化/住房铭牌初始投影/东区雪帽/财政账本关税行/
    /// 企业贷流动性溢价行/外汇信号门禁/比较优势游标复位/MacroSigned 双符号——逐条钉死。
    /// (过场雨向为私有 UI 动画数学,由代码修复+注释覆盖,不做单测。)</summary>
    public class EconTownV0915cBugfixTests
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
            state = NewWorld("V0915cWorld");
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV0915cTests_" + System.Guid.NewGuid().ToString("N"));
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

        // ————— bug搜#3:培训补贴走效用通道,dailyIncome 不被死写入 —————

        [Test]
        public void TrainingSubsidy_StudyKeepsDailyIncome_UtilityBonusEnters()
        {
            GovernmentSimulator.SetPolicy(state, PolicyType.TrainingSubsidy, 20f);
            float income0 = state.resident.dailyIncome, utility0 = state.resident.dailyUtility;
            Assert.IsTrue(ResidentEconomy.Leisure(state, true).success);
            Assert.AreEqual(income0, state.resident.dailyIncome, .01f,
                "学习不动日薪——旧代码 += 补贴×0.08 是死写入(EndDay 公式重算覆写),与「眼前收入没增加」口径相反。");
            Assert.Greater(state.resident.dailyUtility, utility0 + 20f,
                "补贴的效用加成(studyBonus=20×0.25=5)必须进 dailyUtility——门槛须高于无补贴学习基础效用 17,否则 studyBonus 被删(增量 17)时测试照绿(围猎五#9)。");
        }

        // ————— bug搜#4:政府付息(国债份额)进入当日居民日报 —————

        [Test]
        public void SettlePipeline_GovBondInterestEntersDayReport()
        {
            GameState withBond = NewWorld("V0915cBond"), withoutBond = NewWorld("V0915cPlain");
            // debt=持券额 → coverage=clamp(denom/debt)=1,且 bank.GovernmentBonds=0 → 居民份额
            // 100%:日报多出的利息恰等于政府本期利息全额,断言不依赖内部分配公式。
            withBond.government.debt = 2000f;
            withBond.government.averageDebtRate = 4f;
            withBond.housing.treasuryFace = 2000f;   // 居民持国债份额
            withoutBond.government.debt = 2000f;     // 同债务无持券对照
            withoutBond.government.averageDebtRate = 4f;
            float withInterest = SettleOneDay(withBond, out ResidentDayRecord withRecord);
            float withoutInterest = SettleOneDay(withoutBond, out _);
            Assert.Greater(withInterest, withoutInterest + .01f,
                "持国债居民的当日日报必须多出政府利息份额(旧代码付息在封账后、清零前,这笔利息从任何日报缺席)。");
            // 复审(tests):金额必须恰为政府利息全额(重复计存款利息/翻倍/漏 savingsEnd 腿都不许溜过)。
            Assert.AreEqual(withBond.government.interestPaid, withInterest - withoutInterest, .01f,
                "持券居民日报多出的利息恰等于政府本期利息(bank 无持券,居民份额 100%)。");
            Assert.AreEqual(withBond.resident.savings, withRecord.savingsEnd, .01f,
                "日报终态储蓄必须等于管线结束时刻的居民储蓄(付息腿同时回填 savingsEnd)。");
        }

        static float SettleOneDay(GameState s, out ResidentDayRecord record)
        {
            GameState.I = s;   // 管线内 Ensure/UI 钩子读单例
            s.planProduction = 100;
            DaySimulator.Settle(s, DaySimulator.RollDemand(s), DaySimulator.Production(s));
            Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(s, out _, out string blocked),
                "护栏:结算管线不被事件/竞争拦截。blocked=" + blocked);
            record = s.resident.history[s.resident.history.Count - 1];   // 收日管线内 EndDay 写的当日记录
            return record.interestIncome;
        }

        // ————— bug搜#7:关税按百分比格式化 —————

        [Test]
        public void FormatPolicyValue_ImportTariffIsPercent()
        {
            PolicyState tariff = new PolicyState { type = PolicyType.ImportTariff, value = 25f };
            Assert.AreEqual("25%", GovernmentSimulator.FormatPolicyValue(tariff),
                "关税是从价百分率(结算按 值/100×进口基),旧默认分支显示「¥25/单位」量级误读一个数量级以上。");
        }

        // ————— bug搜#12:住房三态铭牌建后立即投影 —————

        [Test]
        public void HousingPlaque_InitialProjectionAfterBuild()
        {
            GameObject mapObject = new GameObject("V0915cMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Transform rent = Plaque("homePlaqueRent"), own = Plaque("homePlaqueOwn"),
                    foreclose = Plaque("homePlaqueForeclose");
                Assert.IsNotNull(rent);
                Assert.IsTrue(rent.gameObject.activeSelf, "默认租房中:首日只亮「租住」。");
                Assert.IsFalse(own.gameObject.activeSelf, "「自有」不得亮。");
                Assert.IsFalse(foreclose.gameObject.activeSelf, "「法拍」不得亮。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        static Transform Plaque(string fieldName)
        {
            FieldInfo field = typeof(MapBuilder).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            return field != null ? field.GetValue(null) as Transform : null;
        }

        // ————— bug搜#15:雪帽覆盖东商业新区(间接子级) —————

        [Test]
        public void SnowOverlays_CoverEastDistrictBuildings()
        {
            GameObject mapObject = new GameObject("V0915cSnowMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                GameState.I.progression.completedChapter = 10;   // 东商业新区落成
                MapBuilder.RefreshGrowthDistricts(GameState.I);
                GameState.I.day = 40;                            // 冬季
                MapBuilder.ApplySeason(GameState.I);
                Transform bookstore = FindDeep(MapBuilder.TownRoot, "B_东街书店");
                Assert.IsNotNull(bookstore, "护栏:东区书店建筑存在(第 10 章落成)。");
                Assert.IsNotNull(bookstore.Find("snow_overlay"),
                    "东区建筑挂 growth_east/completed 之下,雪帽必须递归覆盖(旧代码只扫直接子级,一排不积雪)。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(name)) return child;
                Transform deeper = FindDeep(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // ————— bug搜#17:财政账本收入侧列关税行,账本闭合 —————

        [Test]
        public void FiscalTab_TariffRowPresent()
        {
            state.government.pendingTariffRevenue = 500f;
            WithFreshUi(ui =>
            {
                ui.ShowGovernmentDashboard(0);   // tab 0 = 财政总览
                Canvas.ForceUpdateCanvases();
                Text[] texts = ui.GetComponentsInChildren<Text>(true);
                bool hasRow = false, hasAmount = false;
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].text == null) continue;
                    if (texts[i].text.Contains("进口关税")) hasRow = true;
                    if (texts[i].text == "+ ¥ 500") hasAmount = true;
                }
                Assert.IsTrue(hasRow, "财政账本收入侧必须列「进口关税」——预计总收入内部含它,漏行则账本不闭合。");
                Assert.IsTrue(hasAmount, "关税金额行可见。");
                // 复审(tests):补布局腿——只钉文本存在时,y 误写叠印/整段顺延漏掉都照绿
                // (照 V09BugfixTests 卡高/越界断言先例):关税行不叠污染税行,余额行不穿出卡底。
                RectTransform tariffRow = RowByText(texts, "进口关税"), pollutionRow = RowByText(texts, "污染税"),
                    balanceRow = RowByText(texts, "预计余额（完整口径）");
                Assert.IsNotNull(tariffRow); Assert.IsNotNull(pollutionRow); Assert.IsNotNull(balanceRow);
                Assert.GreaterOrEqual(-tariffRow.anchoredPosition.y - (-pollutionRow.anchoredPosition.y), 30f,
                    "关税行必须在污染税行下方至少一个行距(30),否则同位叠印。");
                Assert.LessOrEqual(-balanceRow.anchoredPosition.y + 32f, 474f,
                    "顺延后余额行(偏移+行高32)必须仍在 474 高的账本卡内。");
            });
        }

        // ————— bug搜#18:企业贷报价分解含流动性溢价行 —————

        [Test]
        public void CompanyLoanQuote_LiquidityLineShownWhenTight()
        {
            state.progression.completedChapter = 32;
            state.bank.reserves = state.bank.RequiredReserves;   // 压到恰达标:ratio<required×1.25 触发流动性溢价
            CreditQuote probe = CreditService.QuoteCompanyLoan(state, 1000f, LoanPurpose.EquipmentInvestment, 24);
            Assert.Greater(probe.liquidityPremium, 0f,
                "护栏:注入的低准备金构造确实触发流动性溢价(否则本用例空转)。");
            WithFreshUi(ui =>
            {
                ui.ShowFinanceCenter(2);   // tab 2 = 企业融资
                Canvas.ForceUpdateCanvases();
                Text[] texts = ui.GetComponentsInChildren<Text>(true);
                bool hasLine = false;
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i].text != null && texts[i].text.Contains("流动性溢价")) { hasLine = true; break; }
                Assert.IsTrue(hasLine,
                    "finalRate=基础+风险+期限+流动性,分解漏流动性溢价行则三项加总≠显示年化,定价教学口径不闭合。");
            });
        }

        // ————— bug搜#19:外汇学习信号只在解锁门禁之后记录 —————

        [Test]
        public void WorldEconomy_FxSignalNotRecordedWhenLocked()
        {
            WithFreshUi(ui =>
            {
                ui.ShowWorldEconomy(2);   // cc=0:锁定态点外汇页签
                Assert.IsFalse(HasFxSignal(state),
                    "锁定态不得记录学习信号——旧代码在门禁之前 RecordSignal,第 47 章会被白过(信号先落账,到达即结课)。");
            });
            state.progression.completedChapter = InternationalTradeService.ExportUnlockChapter;
            WithFreshUi(ui =>
            {
                ui.ShowWorldEconomy(2);
                Assert.IsTrue(HasFxSignal(state), "解锁后查看外汇页照常记录信号。");
            });
        }

        static bool HasFxSignal(GameState s)
        {
            for (int i = 0; i < s.progression.signals.Count; i++)
                if (s.progression.signals[i] == ProgressSignals.ExchangeRateObserved) return true;
            return false;
        }

        // ————— bug搜#20:比较优势实验室行游标随重建复位 —————

        [Test]
        public void ComparativeAdvantageLab_HourCursorResetsOnRebuild()
        {
            WithFreshUi(ui =>
            {
                FieldInfo cursor = typeof(UIManager).GetField("_hourCursor",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(cursor);
                cursor.SetValue(ui, 99);   // 模拟上一局残留(旧代码只增不减)
                ui.ShowComparativeAdvantageLab();
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(4, (int)(float)cursor.GetValue(ui),
                    "重建后游标从 0 数四行(残留 99 被复位);旧代码会是 103,控件整体漂出面板。");
            });
        }

        // ————— bug搜#21:MacroSigned 贴零负数显示「+0.0%」而非「+-0.0%」 —————

        [Test]
        public void MacroSigned_PlusZeroForTinyNegative()
        {
            MethodInfo signed = typeof(UIManager).GetMethod("MacroSigned",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(signed);
            Assert.AreEqual("+0.0", signed.Invoke(null, new object[] { -0.03f }),
                "判号与显示必须同用取整值——旧代码 shown 判号「+」、原始值显示「-0.0」拼出「+-0.0」。");
            Assert.AreEqual("-1.2", signed.Invoke(null, new object[] { -1.2f }), "常规负数不受影响。");
            Assert.AreEqual("+3.4", signed.Invoke(null, new object[] { 3.44f }), "常规正数不受影响。");
        }

        static RectTransform RowByText(Text[] texts, string fragment)
        {
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].text != null && texts[i].text.Contains(fragment))
                    return texts[i].rectTransform;
            return null;
        }

        static void WithFreshUi(System.Action<UIManager> body)
        {
            UIManager[] stale = Resources.FindObjectsOfTypeAll<UIManager>();
            for (int i = 0; i < stale.Length; i++)
                if (stale[i] != null && stale[i].gameObject.scene.IsValid())
                    Object.DestroyImmediate(stale[i].gameObject);
            UIManager ui = UIManager.Create();
            try { ui.InitializeForValidation(); body(ui); }
            finally { Object.DestroyImmediate(ui.gameObject); }
        }
    }
}
#endif
