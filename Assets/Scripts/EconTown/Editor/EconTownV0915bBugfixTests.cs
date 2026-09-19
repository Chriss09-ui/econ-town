#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.15b 第二批(全镇逻辑 bug 搜寻·中危六条)回归:居民单侧重放撤居民贷/
    /// 出口配额 0 读档洗值/区段门控交互点回滚对偶注销/央行可信度回升路径/就业六字段声明值/
    /// 居民重放精神状态回滚——逐条钉死修复后的口径。</summary>
    public class EconTownV0915bBugfixTests
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
            state = NewWorld("V0915bWorld");
            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV0915bTests_" + System.Guid.NewGuid().ToString("N"));
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
            Interactables.All.Clear();
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        // ————— bug搜#9:居民单侧重放(不营业)同样要撤当日居民贷款;企业贷绑定企业侧重放 —————

        [Test]
        public void ReplayDay_ResidentOnly_UndoesResidentLoan_KeepsCompanyLoan()
        {
            // 清晨先过居民侧:贷 ¥500(消费贷放款腿进储蓄/居民存款)并完成居民日;
            // 企业当日无结算记录 → residentReplay=true、droppedTodayRecord=false。
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Resident,
                borrowerId = state.resident.residentName,
                purpose = LoanPurpose.Consumption,
                status = LoanStatus.Current,
                principal = 500f,
                remainingPrincipal = 500f,
                originatedWorldDay = state.day
            });
            state.resident.savings += 500f;
            state.bank.householdDeposits += 500f;
            state.bank.loans.Add(new LoanRecord
            {
                borrowerType = LoanBorrowerType.Company,
                borrowerId = "player.company",
                purpose = LoanPurpose.WorkingCapital,
                status = LoanStatus.Current,
                principal = 400f,
                remainingPrincipal = 400f,
                originatedWorldDay = state.day
            });
            state.bank.companyDeposits += 400f;
            float savings0 = state.resident.savings, hh0 = state.bank.householdDeposits, cd0 = state.bank.companyDeposits;
            ResidentEconomy.EndDay(state);   // 居民当日记录(不营业,企业 history 末条仍是昨日)
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(1, state.bank.loans.Count,
                "居民单侧重放:当日居民消费贷照撤(否则重放日再贷=双重负债),企业贷不动(其钱端只在企业侧重放时回基线)。");
            Assert.AreEqual(LoanBorrowerType.Company, state.bank.loans[0].borrowerType);
            Assert.AreEqual(savings0 - 500f, state.resident.savings, .01f, "消费贷放款腿随撤销退回。");
            Assert.AreEqual(hh0 - 500f, state.bank.householdDeposits, .01f);
            Assert.AreEqual(cd0, state.bank.companyDeposits, .01f, "企业存款镜像不动。");
        }

        // ————— bug搜#10:合法的出口配额 0 不得读档洗回 40 —————

        [Test]
        public void Load_PreservesZeroExportQuota()
        {
            state.exportChannelEnabled = true;
            state.exportTargetQuotaCups = 0;   // 玩家显式「暂停出口」(UI 可减到 0)
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(0, state.exportTargetQuotaCups,
                "0 是合法配额(按 0 杯出口),读档洗回 40 违背玩家意图(旧代码 `<=0?40`)。");
        }

        // ————— bug搜#13:区段门控交互点读档回滚,三条同生命周期条目对偶注销 —————

        [Test]
        public void DistrictInteractables_AllGatedEntriesRemovedOnRollback()
        {
            GameObject mapObject = new GameObject("V0915bMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                GameState.I.progression.completedChapter = 53;   // 全落成进度:三个区段全部动工
                GameBootstrap.RegisterDistrictInteractables();
                Assert.IsNotNull(FindByLabel("阅读本周概念"));
                Assert.IsNotNull(FindByLabel("查看公告碑"));
                Assert.IsNotNull(FindByLabel("查看政策利率牌"));

                GameState.I.progression.completedChapter = 3;    // 读档回滚到全区未动工
                GameBootstrap.RegisterDistrictInteractables();
                Assert.IsNull(FindByLabel("进入东街书店"));
                Assert.IsNull(FindByLabel("阅读本周概念"),
                    "概念黑板随东商业新区同生命周期——回滚残留会让空草地可按 E。");
                Assert.IsNull(FindByLabel("查看公告碑"), "公告碑同生命周期,对偶注销。");
                Assert.IsNull(FindByLabel("查看政策利率牌"), "利率牌同生命周期,对偶注销。");
            }
            finally
            {
                Interactables.All.Clear();
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— bug搜#6:央行可信度有回升路径,固定汇率不再一次性永久锁死 —————

        [Test]
        public void CentralBankCredibility_RecoversInFloat_RebuildsInFixed()
        {
            // 失守后残局:可信度 55、已切浮动。浮动期每日 +1(封顶 60),5 天后过再固定门槛。
            state.centralBank.credibility = 55f;
            state.worldTrade.exchangeRate.regime = ExchangeRegime.Floating;
            state.worldTrade.exchangeRate.lastAbandonPegWorldDay = -999;   // 冷静期已过
            state.centralBank.forexReservesBCurrency = 800f;
            for (int i = 0; i < 5; i++)
            {
                state.day++;
                ExchangeRateService.ApplyDailyForm(state);
            }
            Assert.GreaterOrEqual(state.centralBank.credibility, 60f,
                "浮动期缓释修复:55+5×1=60,达到再固定门槛(旧代码只降不升,永久锁死)。");
            string message;
            Assert.IsTrue(ExchangeRateService.TrySetRegime(state, ExchangeRegime.Fixed, out message),
                "重建信誉后可再固定:" + message);
            // 固定制守住:每日 +2,封顶 80(失守前的最高档)。
            float afterPeg = state.centralBank.credibility;
            state.day++;
            ExchangeRateService.ApplyDailyForm(state);
            Assert.Greater(state.centralBank.credibility, afterPeg, "守住承诺积累信誉(+2/日)。");
            state.centralBank.credibility = 80f;
            state.day++;
            ExchangeRateService.ApplyDailyForm(state);
            Assert.AreEqual(80f, state.centralBank.credibility, .01f, "封顶 80,不再上探。");
        }

        [Test]
        public void CentralBankCredibility_FloatDoesNotErodeHighCredibility()
        {
            // 已在 60 以上(如从未失守的 80):浮动期只维持不降——修复不能反向压低信誉。
            state.centralBank.credibility = 80f;
            state.worldTrade.exchangeRate.regime = ExchangeRegime.Floating;
            state.day++;
            ExchangeRateService.ApplyDailyForm(state);
            Assert.AreEqual(80f, state.centralBank.credibility, .01f,
                "浮动修复上限 60 的语义是「60 以下缓释回 60」,不是「80 压到 60」。");
        }

        // ————— bug搜#11:居民重放把精神状态回滚到清晨快照 —————

        [Test]
        public void ReplayDay_VitalsRolledBackToMorningSnapshot()
        {
            state.resident.lifeDay = 4;
            state.day = 4;
            state.resident.reportReady = true;
            ResidentEconomy.BeginNextDay(state);   // 清晨快照
            float energy0 = state.resident.energy, satiety0 = state.resident.satiety,
                happiness0 = state.resident.happiness, entertainment0 = state.resident.entertainment;
            Assert.Greater(state.resident.satiety, 20f, "护栏:基线饱食足够衰减后仍有区分度。");
            ResidentEconomy.EndDay(state);         // 日终衰减:energy-8~18/satiety-18/entertainment-8
            Assert.Less(state.resident.satiety, satiety0, "护栏:EndDay 衰减确实发生了。");
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(energy0, state.resident.energy, .01f,
                "重放日从清晨精力开局(旧代码从昨日日末开局,效率系统性偏低)。");
            Assert.AreEqual(satiety0, state.resident.satiety, .01f,
                "重放日从清晨饱食开局。");
            // 复审(tests):四字段全部要钉住——只断言 energy/satiety 时 happiness/entertainment
            // 的回滚行被无声删掉测试照绿。
            Assert.AreEqual(happiness0, state.resident.happiness, .01f, "重放日从清晨幸福开局。");
            Assert.AreEqual(entertainment0, state.resident.entertainment, .01f,
                "重放日从清晨娱乐开局(lifeDay=4/4字名=eventIndex 0,无聚餐事件腿,快照即终值)。");
        }

        static Interactable FindByLabel(string label)
        {
            for (int i = 0; i < Interactables.All.Count; i++)
                if (Interactables.All[i].label == label) return Interactables.All[i];
            return null;
        }
    }
}
#endif
