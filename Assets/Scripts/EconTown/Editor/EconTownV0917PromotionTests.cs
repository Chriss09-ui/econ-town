#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.17 篇间晋升线回归:五段过场的触发镜像(队首 pending 报告 part)/一次性/
    /// 大事记分层边界/文案真实数字/编年展示序/四幕与「下一程」文案——逐条钉死。(编译标记 v2)</summary>
    public class EconTownV0917PromotionTests
    {
        readonly List<GameObject> worlds = new List<GameObject>();
        GameState state;

        GameState NewWorld(string name)
        {
            GameObject go = new GameObject(name);
            worlds.Add(go);
            GameState s = go.AddComponent<GameState>();
            GameState.I = s;
            InternationalTradeService.Ensure(s);
            ResidentEconomy.CreateProfile(s, "晋升镇民", ResidentProfession.StoreClerk,
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
            state = NewWorld("V0917World");
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            for (int i = 0; i < worlds.Count; i++)
                if (worlds[i] != null) Object.DestroyImmediate(worlds[i]);
            worlds.Clear();
        }

        /// <summary>照真实收日链语义构造「待看报告」:stageReports 有本体 + pendingStageReports 在队,
        /// 否则 PeekPendingStageReport 按悬挂条目返回 null(判定面会假阴性)。</summary>
        static void QueuePendingReport(GameState s, LearningPart part)
        {
            s.progression.stageReports.Add(new StageReportRecord { part = part, completedWorldDay = s.day });
            s.progression.pendingStageReports.Add((int)part);
        }

        // ————— 映射与展示序 —————

        [Test]
        public void PromotionCutscene_Mapping_CoversFivePartsOnly()
        {
            Assert.AreEqual((int)CutsceneId.MoveInTown, CutsceneService.PromotionCutsceneFor(LearningPart.Market));
            Assert.AreEqual((int)CutsceneId.CouncilAppointment, CutsceneService.PromotionCutsceneFor(LearningPart.Resident));
            Assert.AreEqual((int)CutsceneId.BureauHandover, CutsceneService.PromotionCutsceneFor(LearningPart.Government));
            Assert.AreEqual((int)CutsceneId.BankMandate, CutsceneService.PromotionCutsceneFor(LearningPart.Macro));
            Assert.AreEqual((int)CutsceneId.Homecoming, CutsceneService.PromotionCutsceneFor(LearningPart.Trade));
            // Housing 由 Finale 收弧、Enterprise/Finance 无晋升过场——映射必须为 0,
            // 否则 DeliverPendingStageReport 会对 0 以外的未知 id 调 TryPlay。
            Assert.AreEqual(0, CutsceneService.PromotionCutsceneFor(LearningPart.Enterprise));
            Assert.AreEqual(0, CutsceneService.PromotionCutsceneFor(LearningPart.Finance));
            Assert.AreEqual(0, CutsceneService.PromotionCutsceneFor(LearningPart.Housing));
        }

        [Test]
        public void ChronicleOrder_IsChronologicalPermutationOfAllEleven()
        {
            Assert.AreEqual(11, CutsceneService.Count, "六段旧过场 + 五段晋升过场。");
            Assert.AreEqual(CutsceneService.Count, CutsceneService.ChronicleOrder.Length,
                "编年展示序必须覆盖全部过场,少一段大事记就缺一行。");
            bool[] seen = new bool[CutsceneService.Count + 1];
            for (int i = 0; i < CutsceneService.ChronicleOrder.Length; i++)
            {
                int id = CutsceneService.ChronicleOrder[i];
                Assert.GreaterOrEqual(id, 1);
                Assert.LessOrEqual(id, CutsceneService.Count);
                Assert.IsFalse(seen[id], "展示序里 id=" + id + " 出现两次。");
                seen[id] = true;
            }
            // 编年约束:开业(1)→搬进(7)→聘书(8)→钥匙(9)→委任状(10)→房契(11)→收官(5)
            // 必须按各自宿主章的先后排,否则档案叙事错乱。复审#7:旧段 2/3/4/6 的相对位置
            // 也要锚定(价格战9章<搬进11晨、危机锚前段、贸易战42+<房契54晨、狂热后期<房契)——
            // 否则重排数组时旧段漂移测试照绿。
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 1),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 7));
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 2),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 7), "价格战(9章)须排在搬进(11晨)之前。");
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 7),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 8));
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 3),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 8), "危机(前期锚位)须排在聘书(17晨)之前。");
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 8),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 9));
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 9),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 10));
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 10),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 4), "贸易战(42章后)须排在委任状(31晨)之后。");
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 4),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 11), "贸易战(42章后)须排在房契(54晨)之前。");
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 6),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 11), "狂热(后期锚位)须排在房契之前。");
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 10),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 11));
            Assert.Less(System.Array.IndexOf(CutsceneService.ChronicleOrder, 11),
                System.Array.IndexOf(CutsceneService.ChronicleOrder, 5), "收官必须排在房契之后。");
        }

        // ————— 触发判定(镜像宿主:队首 pending 报告的 part) —————

        [Test]
        public void ShouldPlay_MirrorsQueuedReportPart_Exclusively()
        {
            QueuePendingReport(state, LearningPart.Resident);
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.CouncilAppointment),
                "队首是居民篇报告时,聘书过场应播(宿主=报告交付)。");
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.MoveInTown),
                "part 不匹配的晋升过场不得播。");
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.BankMandate),
                "part 不匹配的晋升过场不得播。");
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.Finale),
                "Finale 只认 Housing 报告(收弧),居民篇报告不得误触。");
        }

        [Test]
        public void ShouldPlay_AlreadyViewed_OrNoPending_StaysFalse()
        {
            // 空队列:五段全不播(悬挂/无报告的清晨)
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.MoveInTown));
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.Homecoming));
            QueuePendingReport(state, LearningPart.Market);
            Assert.IsTrue(CutsceneService.ShouldPlay(state, (int)CutsceneId.MoveInTown));
            CutsceneService.MarkViewed(state, (int)CutsceneId.MoveInTown);
            Assert.IsFalse(CutsceneService.ShouldPlay(state, (int)CutsceneId.MoveInTown),
                "看过即一次性(跳过=已看),同队列不得重播。");
        }

        [Test]
        public void MarkViewed_WritesArchiveWithRealCaption()
        {
            state.economy.worldDay = 23;
            QueuePendingReport(state, LearningPart.Trade);
            CutsceneService.MarkViewed(state, (int)CutsceneId.Homecoming);
            CutsceneRecord record = CutsceneService.FindRecord(state, (int)CutsceneId.Homecoming);
            Assert.IsNotNull(record, "看过必须留档(大事记回看引用首播数字)。");
            Assert.AreEqual(23, record.worldDay);
            Assert.IsNotEmpty(record.caption);
        }

        // ————— 大事记三级分层边界(下一篇开篇章) —————

        [Test]
        public void Happened_TierBoundsAtTransitionChapter()
        {
            int[] ids = { (int)CutsceneId.MoveInTown, (int)CutsceneId.CouncilAppointment,
                (int)CutsceneId.BureauHandover, (int)CutsceneId.BankMandate, (int)CutsceneId.Homecoming };
            // 复审#2 口径:篇末章(转换章)完成即已跨过——停在篇界且报告已看的旧档
            // (正常推进档的常态)按「已完成转换章」预填,不得误显「尚未发生」。
            int[] transitionChapters = { 10, 16, 22, 30, 53 };
            for (int k = 0; k < ids.Length; k++)
            {
                state.progression.completedChapter = transitionChapters[k] - 1;
                Assert.IsFalse(CutsceneService.Happened(state, ids[k]),
                    "id=" + ids[k] + " 在转换章(篇末章)完成前不应判定为已发生。");
                Assert.AreEqual(CutsceneService.ChronicleTier.Locked, CutsceneService.Tier(state, ids[k]));
                state.progression.completedChapter = transitionChapters[k];
                Assert.IsTrue(CutsceneService.Happened(state, ids[k]),
                    "id=" + ids[k] + " 转换章完成即已跨过该篇界(旧档篇界预填口径)。");
                Assert.AreEqual(CutsceneService.ChronicleTier.Prefilled, CutsceneService.Tier(state, ids[k]));
            }
        }

        // ————— 文案:真实数字 + 打字机四行 + 预填标注 —————

        [Test]
        public void Caption_QuotesRealLedgerNumbers()
        {
            state.history.Add(new DayRecord { day = 3, shopOpen = true, listedPrice = 15f,
                sold = 80, profit = 1234f, competitionActive = true, playerMarketShare = .42f });
            state.resident.happiness = 67f;
            state.government.budgetBalance = -5f;
            state.bank.householdDeposits = 8800f;
            state.bank.companyDeposits = 1200f;
            state.centralBank.policyRate = 2.5f;
            state.worldTrade.history.Add(new TradeDayRecord { worldDay = 40, exportsTotal = 777f });

            string c7 = CutsceneService.Caption(state, (int)CutsceneId.MoveInTown);
            StringAssert.Contains("42.0", c7, "搬进小镇须引用账本里的真实份额。");
            string c8 = CutsceneService.Caption(state, (int)CutsceneId.CouncilAppointment);
            StringAssert.Contains("1234", c8);
            StringAssert.Contains("67", c8);
            string c9 = CutsceneService.Caption(state, (int)CutsceneId.BureauHandover);
            StringAssert.Contains("-5", c9);
            string c10 = CutsceneService.Caption(state, (int)CutsceneId.BankMandate);
            StringAssert.Contains("10000", c10, "存款=居民 8800+企业 1200,须是账本合计口径。");
            StringAssert.Contains("2.50", c10);
            string c11 = CutsceneService.Caption(state, (int)CutsceneId.Homecoming);
            StringAssert.Contains("777", c11, "房契须引用贸易账本累计出口。");
        }

        [Test]
        public void Lines_AreFourTypewriterRows_PrefillIsMarked()
        {
            int[] ids = { (int)CutsceneId.MoveInTown, (int)CutsceneId.CouncilAppointment,
                (int)CutsceneId.BureauHandover, (int)CutsceneId.BankMandate, (int)CutsceneId.Homecoming };
            for (int k = 0; k < ids.Length; k++)
            {
                string[] lines = CutsceneService.Lines(state, ids[k]);
                Assert.AreEqual(4, lines.Length, "id=" + ids[k] + " 打字机正文固定四行。");
                for (int i = 0; i < lines.Length; i++) Assert.IsNotEmpty(lines[i]);
                StringAssert.Contains("(存档前的记录)", CutsceneService.PrefillSummary(ids[k]),
                    "id=" + ids[k] + " 旧档预填必须标注口径。");
                Assert.IsNotEmpty(CutsceneService.Title(ids[k]));
            }
        }

        // ————— 四幕与「下一程」 —————

        [Test]
        public void ActLabel_GroupsTwoPartsPerAct()
        {
            Assert.AreEqual(ProgressionService.ActLabel(LearningPart.Enterprise), ProgressionService.ActLabel(LearningPart.Market));
            Assert.AreEqual(ProgressionService.ActLabel(LearningPart.Resident), ProgressionService.ActLabel(LearningPart.Government));
            Assert.AreEqual(ProgressionService.ActLabel(LearningPart.Macro), ProgressionService.ActLabel(LearningPart.Finance));
            Assert.AreEqual(ProgressionService.ActLabel(LearningPart.Trade), ProgressionService.ActLabel(LearningPart.Housing));
            // 四组互异且非空(空串会把报告头部的幕行渲染成空段)。复审#6:不等不具传递性,
            // 完全图 6 条边全部断言(相邻对漏掉 a1/a3、a1/a4、a2/a4——复制粘贴改错一幕文案
            // 时恰是这些对失守)。
            string a1 = ProgressionService.ActLabel(LearningPart.Enterprise);
            string a2 = ProgressionService.ActLabel(LearningPart.Resident);
            string a3 = ProgressionService.ActLabel(LearningPart.Macro);
            string a4 = ProgressionService.ActLabel(LearningPart.Trade);
            Assert.IsNotEmpty(a1); Assert.IsNotEmpty(a2); Assert.IsNotEmpty(a3); Assert.IsNotEmpty(a4);
            Assert.AreNotEqual(a1, a2); Assert.AreNotEqual(a1, a3); Assert.AreNotEqual(a1, a4);
            Assert.AreNotEqual(a2, a3); Assert.AreNotEqual(a2, a4); Assert.AreNotEqual(a3, a4);
        }

        [Test]
        public void NextJourneyText_CoversAllParts_FinaleEchoesHousing()
        {
            LearningPart[] parts = { LearningPart.Enterprise, LearningPart.Market, LearningPart.Resident,
                LearningPart.Government, LearningPart.Macro, LearningPart.Finance, LearningPart.Trade,
                LearningPart.Housing };
            for (int i = 0; i < parts.Length; i++)
                Assert.IsNotEmpty(ProgressionService.NextJourneyText(parts[i]),
                    "part=" + parts[i] + " 的「下一程」文案缺失(阶段报告尾部会渲染空段)。");
            StringAssert.Contains("经济实验", ProgressionService.NextJourneyText(LearningPart.Housing),
                "收官篇的下一程须与 Finale 过场口径呼应。");
        }

        // ————— 表现层冒烟:11 张底图全部可画且内容非空 —————

        [Test]
        public void CutsceneSprites_AllElevenPainted()
        {
            for (int id = 1; id <= CutsceneService.Count; id++)
            {
                Sprite sprite = PixelArt.Cutscene(id);
                Assert.IsNotNull(sprite, "id=" + id + " 底图缺失。");
                Assert.Greater(sprite.rect.width, 0f);
                // 复审#5:IsNotNull+宽度对「掉 Paint case/方法清空/全透明」恒绿(NewTex 零初始化
                // 全透明,switch 无 default 穿透仍 Finish)——扫描非零 alpha 像素数,空图必红。
                Color32[] pixels = sprite.texture.GetPixels32();
                int opaque = 0;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a > 0) opaque++;
                Assert.Greater(opaque, pixels.Length / 10,
                    "id=" + id + " 底图不透明像素不足一成,疑似掉 case 或画成了透明幽灵。");
            }
        }
    }
}
#endif
