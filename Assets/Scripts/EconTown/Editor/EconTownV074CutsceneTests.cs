#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.4《小镇大事记》回归套件(PRD §152):
    /// P0-1 非播放环境直接放行宿主弹窗(过场永不阻塞);P0-2 一次性(连续两次危机只播一次);
    /// P0-3 同种子双跑触发序列与文案数字一致;P0-4 零经济副作用(三随机流零消耗+账本逐日一致);
    /// §151 文案引用真实数字;§150 旧档缺字段复活 + 大事记分级预填。
    /// </summary>
    public class EconTownV074CutsceneTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V074CutsceneState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260828);
            LongTermExperienceService.Ensure(state);
            ProgressionService.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— P0-1:非播放环境(EditMode)下 TryPlay 标记已看并直接放行宿主弹窗 —————

        [Test]
        public void TryPlay_OutsidePlay_MarksViewed_AndNeverBlocksHostPopup()
        {
            ForceSunnyTeaMorning();
            bool hostShown = false;
            Assert.IsTrue(CutsceneDirector.TryPlay(state, (int)CutsceneId.SunnyTeaOpening,
                () => hostShown = true), "触发成立时 TryPlay 应接手宿主弹窗。");
            Assert.IsTrue(hostShown, "非播放环境必须同步放行宿主弹窗——过场永不阻塞(§152-1)。");
            Assert.IsTrue(CutsceneService.IsViewed(state, (int)CutsceneId.SunnyTeaOpening),
                "开始即视为已看(§149 跳过=播完口径)。");

            // 已看过:同条件重触发不再播,宿主弹窗照常出现
            hostShown = false;
            Assert.IsFalse(CutsceneDirector.TryPlay(state, (int)CutsceneId.SunnyTeaOpening,
                () => hostShown = true));
            Assert.IsTrue(hostShown, "重触发不播过场,但宿主弹窗不能被吞。");
        }

        // ————— P0-2:一次性——连续两次财务危机只播一次 —————

        [Test]
        public void CrisisCutscene_TwoConsecutiveCrises_PlayOnlyFirstTime()
        {
            TriggerCrisis();
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.CrisisClouds));
            CutsceneService.MarkViewed(state, (int)CutsceneId.CrisisClouds);

            FinancialHealthService.Resolve(state);      // 走出第一次危机
            TriggerCrisis();                             // 第二次危机:条件重新成立
            Assert.IsTrue(state.economy.financialCrisisActive, "第二次危机确实成立。");
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.CrisisClouds),
                "危机可重复发生,过场只播首次(§150)。");
        }

        void TriggerCrisis()
        {
            state.EnsureEconomyState();
            state.economy.financialDistressDays = FinancialHealthService.CrisisDays;
            state.economy.financialCrisisActive = true;
        }

        // ————— 触发判定镜像宿主条件(§148) —————

        [Test]
        public void Triggers_MirrorHostConditions_AndViewedGatesReplay()
        {
            // 1 开业:第 6 章清晨、尚未记录 CompetitorOpened
            ForceSunnyTeaMorning();
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.SunnyTeaOpening));
            CutsceneService.MarkViewed(state, (int)CutsceneId.SunnyTeaOpening);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.SunnyTeaOpening));

            // 2 价格战:RequiresPriceWarChoice 成立
            state.progression.completedChapter = 8;
            state.progression.currentChapter = 9;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
            Assert.IsTrue(ProgressionService.RequiresPriceWarChoice(state), "宿主条件应成立。");
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.PriceWarErupts));
            CutsceneService.MarkViewed(state, (int)CutsceneId.PriceWarErupts);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.PriceWarErupts));

            // 3 危机(上一条已覆盖条件镜像,这里补 viewed 之外的默认关闭态)
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.CrisisClouds), "无危机不播。");

            // 4 贸易战:待决标记成立
            state.worldTrade.pendingTradeWarChoice = true;
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.TradeWarHeadline));
            CutsceneService.MarkViewed(state, (int)CutsceneId.TradeWarHeadline);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.TradeWarHeadline));

            // 5 收官:第八篇(Housing)阶段报告待看(V0.9.5 迁址,§197.7)
            state.progression.pendingStageReports.Add((int)LearningPart.Housing);
            state.progression.stageReports.Add(new StageReportRecord { part = LearningPart.Housing });
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.Finale));
            CutsceneService.MarkViewed(state, (int)CutsceneId.Finale);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.Finale));
        }

        void ForceSunnyTeaMorning()
        {
            state.progression.completedChapter = 5;
            state.progression.currentChapter = 6;
            state.EnsureEconomyState();
            state.economy.market.sunnyTea.isOpen = true;
        }

        // ————— §151:文案引用当档真实数字,禁止模板句 —————

        [Test]
        public void Captions_AndLines_QuoteRealLedgerNumbers()
        {
            state.EnsureEconomyState();
            state.competitor.price = 18f;
            StringAssert.Contains("¥18", CutsceneService.Caption(state, (int)CutsceneId.SunnyTeaOpening),
                "开业文案必须引用对手真实定价(例:它的开业定价是 ¥18)。");
            state.competitor.price = 14f;
            StringAssert.Contains("¥14", CutsceneService.Caption(state, (int)CutsceneId.PriceWarErupts));

            state.economy.financialDistressDays = 5;
            string crisis = CutsceneService.Caption(state, (int)CutsceneId.CrisisClouds);
            StringAssert.Contains("5 天", crisis);
            StringAssert.Contains("-1000", crisis, "危机阈值来自 FinancialHealthService 常量。");

            state.worldTrade.countryB.tariffOnAExports = 0.16f;
            StringAssert.Contains("16%", CutsceneService.Caption(state, (int)CutsceneId.TradeWarHeadline));

            state.economy.worldDay = 137;
            state.macro.history.Add(new MacroPeriodRecord { nominalGdp = 41230f });
            StringAssert.Contains("137", CutsceneService.Caption(state, (int)CutsceneId.Finale));
            StringAssert.Contains("41230", CutsceneService.Caption(state, (int)CutsceneId.Finale));

            // 每段 2–4 行,且正文包含数字摘要
            for (int id = 1; id <= CutsceneService.Count; id++)
            {
                string[] lines = CutsceneService.Lines(state, id);
                Assert.That(lines.Length, Is.InRange(2, 4), "段 " + id + " 行数须在 2–4(§149)。");
                string joined = string.Join("\n", lines);
                StringAssert.Contains(CutsceneService.Caption(state, id), joined, "正文须引用数字摘要。");
            }
        }

        [Test]
        public void MarkViewed_RecordsArchive_WithoutTouchingCheckpoint()
        {
            ForceSunnyTeaMorning();
            state.competitor.price = 18f;
            string checkpointBefore = state.experience.lastCheckpointReason;
            CutsceneService.MarkViewed(state, (int)CutsceneId.SunnyTeaOpening);
            CutsceneRecord record = CutsceneService.FindRecord(state, (int)CutsceneId.SunnyTeaOpening);
            Assert.IsNotNull(record, "首播即记档(供大事记回看引用当日数字)。");
            Assert.AreEqual(state.economy.worldDay, record.worldDay);
            StringAssert.Contains("¥18", record.caption);
            Assert.AreEqual(checkpointBefore, state.experience.lastCheckpointReason,
                "严禁用 SaveKeyMoment 充当标记——不得污染恢复点理由(§150)。");
        }

        // ————— P0-3 + P0-4:同种子双跑——触发序列与文案一致 + 零经济副作用 —————

        struct TraceSample
        {
            public float nominalGdp, cash, rate, unemployment;
            public string trace;   // 当日触发的过场:id:caption(多个用 | 连接)
        }

        [Test]
        public void SameSeed_DoubleRun_TriggerSequenceAndCaptionsIdentical_ZeroLedgerSideEffect()
        {
            var runA = CaptureTrace(20260828, true);
            var runB = CaptureTrace(20260828, true);
            Assert.AreEqual(runA.Count, runB.Count);

            var idsA = new List<string>();
            for (int i = 0; i < runA.Count; i++)
            {
                Assert.AreEqual(runA[i].trace, runB[i].trace,
                    "过场触发序列与文案逐日一致(day " + (i + 1) + ",§152-3)。");
                Assert.AreEqual(runA[i].nominalGdp, runB[i].nominalGdp, .05f);
                Assert.AreEqual(runA[i].cash, runB[i].cash, .01f);
                if (runA[i].trace.Length > 0)
                    foreach (string entry in runA[i].trace.Split('|'))
                        idsA.Add(entry.Split(':')[0]);
            }
            // V0.9.5:第六段「楼市狂热」需要房价引擎累积出 Inflating 相位,14 天剧本化推进到不了——
            // 由 EconTownV095MainlineTests 直接以 Inflating 世界断言 ShouldPlay/八件套,不在此放宽剧本。
            for (int id = 1; id <= 5; id++)
                Assert.IsTrue(idsA.Contains(id.ToString()),
                    "前五段过场在剧本化推进中各触发一次(段 " + id + " 未触发)。");

            // 零副作用轴(P0-4):同一种子,闸门评估开/关,账本序列必须分毫一致
            var gated = CaptureTrace(777001, true);
            var ungated = CaptureTrace(777001, false);
            Assert.AreEqual(gated.Count, ungated.Count);
            for (int i = 0; i < gated.Count; i++)
            {
                Assert.AreEqual(gated[i].nominalGdp, ungated[i].nominalGdp, .05f, "GDP 与闸门无关(day " + (i + 1) + ")。");
                Assert.AreEqual(gated[i].cash, ungated[i].cash, .01f);
                Assert.AreEqual(gated[i].rate, ungated[i].rate, .0001f);
                Assert.AreEqual(gated[i].unemployment, ungated[i].unemployment, .0001f);
            }
        }

        [Test]
        public void GateQueries_ConsumeNoRandomness_AcrossAllStreams()
        {
            ForceSunnyTeaMorning();
            TriggerCrisis();
            state.worldTrade.pendingTradeWarChoice = true;
            long r0 = state.economy.random.calls, t0 = state.economy.tradeRandom.calls, a0 = state.economy.atmosphereRandom.calls;
            for (int round = 0; round < 3; round++)
                for (int id = 1; id <= CutsceneService.Count; id++)
                {
                    CutsceneService.ShouldPlay(state, id);
                    CutsceneService.Lines(state, id);
                    CutsceneService.Caption(state, id);
                }
            Assert.AreEqual(r0, state.economy.random.calls, "判定与文案不得碰共享流。");
            Assert.AreEqual(t0, state.economy.tradeRandom.calls);
            Assert.AreEqual(a0, state.economy.atmosphereRandom.calls, "也不碰氛围流(§123C)。");
        }

        List<TraceSample> CaptureTrace(int seed, bool evaluateGates)
        {
            GameObject root = new GameObject("V074Trace_" + seed + "_" + (evaluateGates ? "on" : "off"));
            GameState s = root.AddComponent<GameState>();
            GameState.I = s;   // CaptureTrace 期间全局指向本流(CutsceneService 不读全局,稳妥起见仍对齐)
            InternationalTradeService.Ensure(s);
            LongTermExperienceService.Ensure(s);
            ProgressionService.Ensure(s);
            s.economy.random.Reset(seed);
            s.economy.tradeRandom.Reset(seed ^ 1013904223);
            s.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "过场双跑", ResidentProfession.StoreClerk, ResidentPreference.Saver);

            List<TraceSample> samples = new List<TraceSample>();
            for (int i = 0; i < 14; i++)
            {
                if (s.government.currentEvent != null && !s.government.currentEvent.resolved)
                    GovernmentSimulator.ResolveEvent(s, 1);
                s.price = 10f + (s.day % 5);
                s.planProduction = 100;
                var demandRoll = DaySimulator.RollDemand(s);
                DaySimulator.Settle(s, demandRoll, DaySimulator.Production(s));
                s.phase = GamePhase.Morning;
                Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(s, out _, out string blocked),
                    "segment blocked: " + blocked);
                s.Changed();

                // 剧本化里程碑(结算后施加:避免 Evaluate 在结算内推进章节跳过晨闸;双跑两侧同序)
                if (i == 2)
                {
                    s.progression.completedChapter = 5;
                    s.progression.currentChapter = 6;
                    s.economy.market.sunnyTea.isOpen = true;
                    s.competitor.price = 18f;
                }
                if (i == 4)
                {
                    s.progression.completedChapter = 8;
                    s.progression.currentChapter = 9;
                    s.competitor.price = 14f;
                }
                if (i == 6) { s.economy.financialDistressDays = FinancialHealthService.CrisisDays; s.economy.financialCrisisActive = true; }
                if (i == 8) s.worldTrade.pendingTradeWarChoice = true;
                if (i == 10)
                {
                    // V0.9.5:Finale 随第八篇迁址,剧本注入 Housing 报告触发(§197.7)。
                    s.progression.pendingStageReports.Add((int)LearningPart.Housing);
                    s.progression.stageReports.Add(new StageReportRecord { part = LearningPart.Housing });
                }

                string trace = "";
                if (evaluateGates)
                {
                    // 镜像 GameBootstrap.TryNextDay 的闸门次序:贸易战晨闸 → 楼市狂热 → 收官报告 → 6 章开业 → 价格战 → 危机(阻塞路径)
                    int[] order = { 4, 6, 5, 1, 2, 3 };
                    foreach (int id in order)
                    {
                        if (!CutsceneService.ShouldPlay(s, id)) continue;
                        string entry = id + ":" + CutsceneService.Caption(s, id);
                        trace = trace.Length == 0 ? entry : trace + "|" + entry;
                        CutsceneService.MarkViewed(s, id);
                        if (id == 3) FinancialHealthService.Resolve(s);   // 危机过场后解除,推进不被卡
                    }
                }
                // 宿主面板在过场之后出现:玩家当天完成价格战选择,次日收日不被强制决策卡住。
                // 闸门开/关两侧都要做,否则副作用轴的两条跑法会真的分叉。
                if (ProgressionService.RequiresPriceWarChoice(s))
                    s.todayStrategyChoice = PlayerCompetitionChoice.HoldPrice.ToString();
                samples.Add(new TraceSample
                {
                    nominalGdp = s.macro.history[s.macro.history.Count - 1].nominalGdp,
                    cash = s.money,
                    rate = ExchangeRateService.Rate(s),
                    unemployment = s.macro.unemploymentRate,
                    trace = trace
                });
            }
            GameState.I = state;
            Object.DestroyImmediate(root);
            return samples;
        }

        // ————— §150:旧档缺字段复活 + 大事记分级预填 —————

        [Test]
        public void OldSaveJson_MissingCutsceneFields_DefaultsRevivedByEnsure()
        {
            LongTermExperienceState experience = new LongTermExperienceState();
            experience.viewedCutscenes = null;
            experience.cutsceneRecords = null;
            // 模拟旧档读回:Ensure 复活空表 = 一部都没看过,不补播(§150)
            GameState host = new GameObject("V074OldSave").AddComponent<GameState>();
            try
            {
                host.experience = experience;
                LongTermExperienceService.Ensure(host);
                Assert.IsNotNull(host.experience.viewedCutscenes);
                Assert.AreEqual(0, host.experience.viewedCutscenes.Count);
                Assert.IsNotNull(host.experience.cutsceneRecords);
                // 含已看过列表的正向序列化往返
                host.experience.viewedCutscenes.Add(3);
                string json = JsonUtility.ToJson(host.experience);
                LongTermExperienceState round = JsonUtility.FromJson<LongTermExperienceState>(json);
                Assert.AreEqual(1, round.viewedCutscenes.Count);
                Assert.AreEqual(3, round.viewedCutscenes[0], "已看过标记随存档走。");
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void ChronicleTier_PrefillByProgressAndLedger_ViewedUpgrades()
        {
            for (int id = 1; id <= CutsceneService.Count; id++)
                Assert.AreEqual(CutsceneService.ChronicleTier.Locked, CutsceneService.Tier(state, id),
                    "新档五个节点全部锁定。");

            state.progression.completedChapter = 6;
            Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, (int)CutsceneId.SunnyTeaOpening),
                "旧档按 completedChapter 预填(§150)。");
            Assert.AreEqual(CutsceneService.ChronicleTier.Locked, CutsceneService.Tier(state, (int)CutsceneId.PriceWarErupts),
                "未到节点不预填。");
            StringAssert.Contains("存档前的记录", CutsceneService.PrefillSummary((int)CutsceneId.SunnyTeaOpening),
                "预填文案标注「存档前的记录」且不引用当档数字。");

            state.experience.crisisReviews.Add(new CrisisReviewRecord { id = "c1", kind = "企业危机" });
            Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, (int)CutsceneId.CrisisClouds),
                "账本历史(危机复盘)也算「已发生」。");

            state.experience.eventHistory.Add(new LongTermEventRecord { id = "trade.warcho", title = "B 国考虑上调关税" });
            Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, (int)CutsceneId.TradeWarHeadline));

            state.progression.completedChapter = 58;
            Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, (int)CutsceneId.Finale));
            Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, (int)CutsceneId.PriceWarErupts),
                "cc>=9 也在价格战节点之后。");

            // 看过之后升级为 Viewed,且带首播档案
            CutsceneService.MarkViewed(state, (int)CutsceneId.SunnyTeaOpening);
            Assert.AreEqual(CutsceneService.ChronicleTier.Viewed, CutsceneService.Tier(state, (int)CutsceneId.SunnyTeaOpening));
            Assert.IsNotNull(CutsceneService.FindRecord(state, (int)CutsceneId.SunnyTeaOpening));
        }
    }
}

#endif
