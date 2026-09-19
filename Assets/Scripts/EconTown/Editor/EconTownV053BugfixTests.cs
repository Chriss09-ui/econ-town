#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.5.3 深度审查批次 1 回归测试（P1×3 + 快赢小修）：
    /// 1) 恢复计划不得重复执行（无限刷钱防护）；
    /// 2) 主线完成的 sandbox 解锁即时传播（当次会话挑战入口可用）；
    /// 3) 居民福利转移走政府账本（pendingSubsidySpending），不再直扣现金破坏
    ///    "现金变化==结余+债务融资"恒等式；
    /// 4) "最低消费方案"恢复值必须越过危机判定线（satiety < 25）；
    /// 5) 品牌<=0 读档兜底只在竞争未引入时生效（洗掉 day<16 残留）。
    /// 章节 Toast 幂等(notifiedUnlockChapter)在 GameBootstrap 实例路径上，EditMode 不便覆盖，留 Play 验证。
    /// </summary>
    public class EconTownV053BugfixTests
    {
        GameObject stateObject;
        GameState state;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V053BugfixTestState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            state.economy.random.Reset(20260826);
            LongTermExperienceService.Ensure(state);

            tempDir = Path.Combine(Path.GetTempPath(), "EconTownV053BugfixTests_" + System.Guid.NewGuid().ToString("N"));
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
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void RecoveryPlanCannotBeExecutedTwice()
        {
            state.money = -1200f;
            state.playerQualityLevel = 2;
            LongTermExperienceService.BeginEnterpriseRecovery(state, "asset");
            float moneyAfterFirst = state.money;
            int actionsAfterFirst = state.experience.enterpriseRecovery.actions.Count;
            Assert.IsTrue(state.experience.enterpriseRecovery.active, "首次调用应进入恢复期");
            Assert.AreEqual(moneyAfterFirst - (-1200f), 900f, .001f, "首次出售设备应得 ¥900（品质 2→1）");

            // 恢复期再次触发（对应从今日简报重开面板误点）：不得重复执行救援效果。
            LongTermExperienceService.BeginEnterpriseRecovery(state, "asset");
            LongTermExperienceService.BeginEnterpriseRecovery(state, "liquidate");
            Assert.AreEqual(moneyAfterFirst, state.money, .001f, "恢复期内重复调用不得再次加钱");
            Assert.AreEqual(actionsAfterFirst, state.experience.enterpriseRecovery.actions.Count,
                "恢复期内重复调用不得追加动作记录");
        }

        [Test]
        public void MainlineCompletionPropagatesSandboxUnlockImmediately()
        {
            Assert.IsFalse(state.sandbox.unlocked, "新档挑战入口应默认锁定");
            state.progression.completedChapter = 30;
            state.progression.sandboxUnlocked = true;
            ProgressionService.Ensure(state); // Ensure 每次都会跑 ApplyUnlocks（与 RefreshHud 同路径）
            Assert.IsTrue(state.sandbox.unlocked,
                "主线完成的当次会话（未经读档/SandboxService.Ensure）挑战入口应即时解锁");
        }

        [Test]
        public void WelfareTransferGoesThroughGovernmentLedger()
        {
            GovernmentState government = state.government;
            government.cash = 5000f;
            government.debt = 0f;
            float cashBefore = government.cash;
            float pendingBefore = government.pendingSubsidySpending;
            float residentCashBefore = state.resident.cash;

            LongTermExperienceService.ApplyResidentRecovery(state, "welfare");

            Assert.AreEqual(residentCashBefore + 80f, state.resident.cash, .001f, "居民应立即 +¥80");
            Assert.AreEqual(cashBefore, government.cash, .001f,
                "福利转移不得直扣政府现金——那会绕过账本破坏现金演化恒等式");
            Assert.AreEqual(pendingBefore + 80f, government.pendingSubsidySpending, .001f,
                "政府侧应记入补贴支出 pending，随周期结算入账");

            // 周期结算后：现金变化应严格等于预算结余（无债务融资时），80 已含在支出里。
            GovernmentCycleRecord record = GovernmentSimulator.AdvanceCycle(state);
            Assert.GreaterOrEqual(record.subsidySpending, 80f, "结算行的补贴支出应包含这笔福利转移");
            Assert.AreEqual(0f, government.pendingSubsidySpending, .001f, "结算后 pending 应清零");
            Assert.AreEqual(record.budgetBalance, government.cash - cashBefore, .001f,
                "政府现金变化必须等于预算结余（账本恒等式）");
        }

        [Test]
        public void MinimalSpendingRecoveryClearsCrisisLine()
        {
            ResidentState resident = state.resident;
            resident.profileCreated = true;
            resident.cash = 0f;
            resident.satiety = 10f;
            resident.energy = 40f;
            resident.employedToday = false;
            resident.unemployedDays = 2;
            LongTermExperienceService.OnDaySettled(state); // 走危机判定路径
            Assert.IsTrue(state.experience.residentCrisisActive, "构造的状态应处于生活危机");

            LongTermExperienceService.ApplyResidentRecovery(state, "cut");

            Assert.GreaterOrEqual(resident.satiety, 25f,
                "『最低消费』的饱腹恢复值必须越过危机判定线 satiety<25，否则该选项永远无效");
            Assert.IsFalse(state.experience.residentCrisisActive, "恢复后危机应解除");
        }

        [Test]
        public void BrandPenaltySurvivesLoadWhenCompetitionIntroduced()
        {
            state.competitionIntroduced = true;
            state.economy.market.sunnyTea.isOpen = true;
            state.playerBrand = 0f; // 竞争事件『食品质量事故』-30 后的合法惩罚结果
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(0f, state.playerBrand, .001f,
                "竞争已引入时品牌 0 是真实惩罚，读档不得洗回 30");
        }

        [Test]
        public void BrandResetsWhenCompetitionNeverIntroduced()
        {
            state.competitionIntroduced = false;
            state.economy.market.sunnyTea.isOpen = false;
            state.playerBrand = 0f; // 竞争未引入时只可能是坏数据
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(30f, state.playerBrand, .001f,
                "竞争未引入时品牌<=0 应兜底回初始值 30");
        }

        [Test]
        public void NullPendingCrisisReviewSurvivesSaveRoundTrip()
        {
            // JsonUtility 会把 null 的 class 字段写成 {}，读档后变成全默认实例、判 null 恒真，
            // 导致收日/读档补发弹出空白"危机复盘"面板。Ensure 必须按 id 清洗。
            state.experience.pendingCrisisReview = null;
            SaveSystem.Save(state);
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.IsNull(state.experience.pendingCrisisReview,
                "读档后 null 的 pendingCrisisReview 不得以全默认实例复活");
        }

        [Test]
        public void SaveCheckpointDoesNotTouchMainSave()
        {
            state.money = 1000f;
            state.day = 3;
            SaveSystem.Save(state); // 主档 v1：money=1000

            state.money = 4242f;
            SaveSystem.SaveCheckpoint(state, "测试关键节点");
            Assert.IsTrue(SaveSystem.HasCheckpoint(), "checkpoint 文件应存在");

            // checkpoint 只是可回滚副本：主档不得被覆盖（旧实现先 Save 覆盖主档再 Copy）。
            state.money = 1f;
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(1000f, state.money, .001f, "主档应保持 checkpoint 写入前的状态");
        }

        [Test]
        public void SaveKeyMomentRespectsAutoSaveSetting()
        {
            state.experience.settings.autoSave = false;
            state.money = 1000f;
            SaveSystem.Save(state); // 主档 v1：money=1000

            state.money = 2000f;
            SaveSystem.SaveKeyMoment(state, "章节完成");
            state.money = 1f;
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(1000f, state.money, .001f,
                "自动存档关闭时，关键节点只写 checkpoint，不得静默覆盖玩家主档");

            state.experience.settings.autoSave = true;
            state.money = 3000f;
            SaveSystem.SaveKeyMoment(state, "关键选择");
            state.money = 2f;
            Assert.IsTrue(SaveSystem.Load(state));
            Assert.AreEqual(3000f, state.money, .001f,
                "自动存档开启时，关键节点应写入主档（关键决策不因崩溃丢失）");
        }

        [Test]
        public void LoadCheckpointRestoresKeyMomentState()
        {
            state.money = 500f;
            state.day = 2;
            SaveSystem.Save(state);
            SaveSystem.SaveCheckpoint(state, "关键选择：价格战");

            state.money = 9999f; // checkpoint 之后又推进了
            SaveSystem.Save(state);

            Assert.IsTrue(SaveSystem.LoadCheckpoint(state));
            Assert.AreEqual(500f, state.money, .001f, "应回到关键节点时刻的状态");
            Assert.AreEqual("关键选择：价格战", state.experience.lastCheckpointReason);
        }
    }
}

#endif
