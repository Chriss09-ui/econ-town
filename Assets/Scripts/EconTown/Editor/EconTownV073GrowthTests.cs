#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.3《生长的小镇》回归套件(PRD §145):
    /// P0-1 新区状态=completedChapter 纯函数(4/9/15/21/35/41/42 档位);
    /// P0-2 既有建筑交互点与新地图结构断言(含东扩常量与新区坐标);
    /// P0-3 零经济副作用双跑(§119-3);P1 周概念同种子稳定/转让牌池扩容/一次性演出队列/旧档兼容。
    /// </summary>
    public class EconTownV073GrowthTests
    {
        GameObject stateObject;
        GameObject mapObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V073GrowthState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260828);
            LongTermExperienceService.Ensure(state);
            Interactables.All.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            Interactables.All.Clear();
            if (mapObject != null) Object.DestroyImmediate(mapObject);
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— P0-1 三态纯函数(PRD §145-1 档位) —————

        [Test]
        public void DistrictPhases_ArePureFunctionOfCompletedChapter()
        {
            int[] probes = { 4, 9, 15, 21, 35, 41, 42 };
            foreach (int cc in probes)
            {
                DistrictPhase east = TownGrowthService.EastDistrict(cc);
                DistrictPhase plaza = TownGrowthService.CivicPlaza(cc);
                DistrictPhase financial = TownGrowthService.FinancialStreet(cc);
                switch (cc)
                {
                    case 4:
                        Assert.AreEqual(DistrictPhase.NotStarted, east, "cc=4:东新区未开工。");
                        Assert.AreEqual(DistrictPhase.NotStarted, plaza);
                        Assert.AreEqual(DistrictPhase.NotStarted, financial);
                        break;
                    case 9:
                        Assert.AreEqual(DistrictPhase.UnderConstruction, east, "cc=9:东新区施工中。");
                        Assert.AreEqual(DistrictPhase.NotStarted, plaza);
                        Assert.AreEqual(DistrictPhase.NotStarted, financial);
                        break;
                    case 15:
                        Assert.AreEqual(DistrictPhase.Completed, east, "cc=15:东新区已落成。");
                        Assert.AreEqual(DistrictPhase.NotStarted, plaza, "cc=15:行政广场未开工。");
                        Assert.AreEqual(DistrictPhase.NotStarted, financial);
                        break;
                    case 21:
                        Assert.AreEqual(DistrictPhase.Completed, east);
                        Assert.AreEqual(DistrictPhase.UnderConstruction, plaza, "cc=21:广场施工中。");
                        Assert.AreEqual(DistrictPhase.NotStarted, financial);
                        break;
                    case 35:
                        Assert.AreEqual(DistrictPhase.Completed, plaza);
                        Assert.AreEqual(DistrictPhase.NotStarted, financial, "cc=35:金融街未开工。");
                        break;
                    case 41:
                        Assert.AreEqual(DistrictPhase.UnderConstruction, financial, "cc=41:金融街施工中。");
                        break;
                    case 42:
                        Assert.AreEqual(DistrictPhase.Completed, financial, "cc=42:金融街落成。");
                        break;
                }
            }
        }

        // ————— 周口径与 GDP 彩蛋档位(§123C/§144) —————

        [Test]
        public void WeekOf_MatchesPrdFloorFormula()
        {
            Assert.AreEqual(1, TownGrowthService.WeekOf(1));
            Assert.AreEqual(1, TownGrowthService.WeekOf(7));
            Assert.AreEqual(2, TownGrowthService.WeekOf(8));
            Assert.AreEqual(2, TownGrowthService.WeekOf(14));
            Assert.AreEqual(3, TownGrowthService.WeekOf(15));
            Assert.AreEqual(53, TownGrowthService.WeekOf(365));
        }

        [Test]
        public void GdpTierIndex_SixTiers_CappedAndBounded()
        {
            Assert.AreEqual(0, TownGrowthService.GdpTierIndex(18570.12f), "开局基准=0 档。");
            Assert.AreEqual(0, TownGrowthService.GdpTierIndex(18570.12f * 1.2f), "不足 +50% 不升档。");
            Assert.AreEqual(1, TownGrowthService.GdpTierIndex(18570.12f * 1.5f), "+50% → 档 1。");
            Assert.AreEqual(2, TownGrowthService.GdpTierIndex(18570.12f * 2f));
            Assert.AreEqual(4, TownGrowthService.GdpTierIndex(18570.12f * 3f));
            Assert.AreEqual(6, TownGrowthService.GdpTierIndex(18570.12f * 4f), "+300% → 档 6。");
            Assert.AreEqual(6, TownGrowthService.GdpTierIndex(18570.12f * 10f), "封顶不再升档。");
            Assert.AreEqual(0, TownGrowthService.GdpTierIndex(1000f), "衰退不产生档位。");
        }

        // ————— 转让牌池扩容(PRD §125/§141:空铺落成后入池) —————

        [Test]
        public void TransferSignPool_GrowsOnlyAfterEastDistrictCompleted()
        {
            Assert.AreEqual(2, AtmosphereService.DecorativeShopPoolSize(0), "常驻池=两间临街小铺。");
            Assert.AreEqual(2, AtmosphereService.DecorativeShopPoolSize(9), "施工期未入池。");
            Assert.AreEqual(4, AtmosphereService.DecorativeShopPoolSize(10), "落成后空铺入池。");
            Assert.AreEqual(4, AtmosphereService.DecorativeShopPoolSize(53));
        }

        // ————— P0-2 地图结构:东扩常量 + 地块锚点稳定(§145-2) —————

        [Test]
        public void MapStructure_TownUsesExpandedParcelLayout()
        {
            BuildMap();
            Assert.AreEqual(72, MapBuilder.W);
            Assert.AreEqual(35, MapBuilder.TownMaxX);
            Assert.AreEqual(-36, MapBuilder.OriginX);
            Assert.AreEqual(36.5f, MapBuilder.WorldMaxX, 0.001f, "相机 clamp 边界随扩图自动外扩。");

            // “双街一轴、四区成镇”批准坐标；港口随东界迁移后仍须防止成长区刷新误挪。
            Assert.AreEqual(new Vector3(-32f, -8.01f, 0f), Building("B_我的果汁店_shop").position);
            Assert.That(Vector3.Distance(new Vector3(31f, -6.61f, 0f), Building("B_国际贸易港_port").position),
                Is.LessThan(.01f), "由门线反算的中心允许浮点舍入误差。");
            Assert.AreEqual(new Vector3(26f, 14.7f, 0f), Building("B_中央银行_centralbank").position);
            Assert.AreEqual(new Vector3(-.5f, 15.52f, 0f), Building("B_市政厅_hall").position);
            Assert.AreEqual(new Vector3(32f, 3.99f, 0f), Building("B_临街小铺_decor_b").position);

            int direct = 0;
            for (int i = 0; i < Town().childCount; i++)
                if (Town().GetChild(i).name.StartsWith("B_")) direct++;
            Assert.AreEqual(16, direct, "新增常驻社区学校后应有 16 栋直建建筑(新区建筑仍在新区根下)。");

            Assert.IsNotNull(Town().Find("growth_east"), "新区根已预建。");
            Assert.IsNotNull(Town().Find("growth_plaza"));
            Assert.IsNotNull(Town().Find("growth_financial"));
        }

        [Test]
        public void DistrictVisuals_SwitchByCompletedChapter_TwoStatesOnly()
        {
            BuildMap();
            Transform east = Town().Find("growth_east");
            Transform eastSite = east.Find("construction_site");
            Transform eastDone = east.Find("completed");

            Assert.IsFalse(east.gameObject.activeSelf, "开局(未开工)整组隐藏。");
            state.progression.completedChapter = 5;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(east.gameObject.activeSelf);
            Assert.IsTrue(eastSite.gameObject.activeSelf, "施工期:围挡可见。");
            Assert.IsFalse(eastDone.gameObject.activeSelf, "施工期:完工内容隐藏。");

            state.progression.completedChapter = 9;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(eastSite.gameObject.activeSelf);

            state.progression.completedChapter = 10;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsFalse(eastSite.gameObject.activeSelf, "落成次日:围挡拆除。");
            Assert.IsTrue(eastDone.gameObject.activeSelf, "落成:完工内容呈现。");

            Transform plaza = Town().Find("growth_plaza");
            state.progression.completedChapter = 16;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(plaza.Find("construction_site").gameObject.activeSelf);
            state.progression.completedChapter = 22;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(plaza.Find("completed").gameObject.activeSelf);

            Transform financial = Town().Find("growth_financial");
            state.progression.completedChapter = 36;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(financial.Find("construction_site").gameObject.activeSelf);
            state.progression.completedChapter = 42;
            MapBuilder.RefreshGrowthDistricts(state);
            Assert.IsTrue(financial.Find("completed").gameObject.activeSelf);
            Assert.IsFalse(financial.Find("construction_site").gameObject.activeSelf);
        }

        [Test]
        public void GrowthProps_FlagStanceTint_AndBankLightsAlpha()
        {
            BuildMap();
            state.progression.completedChapter = 42;
            MapBuilder.RefreshGrowthDistricts(state);

            Assert.IsNotNull(MapBuilder.GrowthFlagCloth, "旗面已挂载。");
            MapBuilder.SetGrowthFlagColor(new Color32(0xc2, 0x3b, 0x2e, 255));
            Assert.AreEqual(new Color32(0xc2, 0x3b, 0x2e, 255),
                (Color32)MapBuilder.GrowthFlagCloth.GetComponent<SpriteRenderer>().color, "扩张=红旗。");

            MapBuilder.SetFlagRaised01(0f);
            Assert.That(MapBuilder.GrowthFlagCloth.localPosition.y, Is.LessThan(0f), "旗升演出起点在杆底。");
            MapBuilder.SetFlagRaised01(1f);
            Assert.That(MapBuilder.GrowthFlagCloth.localPosition.y, Is.GreaterThan(1f), "完工态旗在杆顶。");

            MapBuilder.ApplyBankFacadeLightsAlpha(0f);
            Assert.IsFalse(MapBuilder.TownRoot.Find("B_小镇银行_bank/bank_facade_lights").gameObject.activeSelf,
                "落成前/白天:亮化熄灭。");
            MapBuilder.ApplyBankFacadeLightsAlpha(0.6f);
            Assert.IsTrue(MapBuilder.TownRoot.Find("B_小镇银行_bank/bank_facade_lights").gameObject.activeSelf);
            Assert.AreEqual(0.6f, MapBuilder.TownRoot.Find("B_小镇银行_bank/bank_facade_lights")
                .GetComponent<SpriteRenderer>().color.a, 0.001f);
        }

        [Test]
        public void DistrictInteractables_RegisterOnlyAfterConstructionStarts()
        {
            BuildMap();
            GameBootstrap.RegisterDistrictInteractables();
            Assert.AreEqual(0, CountLabels("阅读本周概念", "查看公告碑", "查看政策利率牌"), "未开工不注册。");

            state.progression.completedChapter = 5;
            GameBootstrap.RegisterDistrictInteractables();
            Assert.AreEqual(1, CountLabels("阅读本周概念"), "东新区动工 → 黑板交互点出现。");
            Assert.AreEqual(0, CountLabels("查看公告碑"));

            state.progression.completedChapter = 22;
            GameBootstrap.RegisterDistrictInteractables();
            state.progression.completedChapter = 36;
            GameBootstrap.RegisterDistrictInteractables();
            Assert.AreEqual(1, CountLabels("查看公告碑"), "重复调用不产生重复注册。");
            Assert.AreEqual(1, CountLabels("查看政策利率牌"));
        }

        int CountLabels(params string[] labels)
        {
            int n = 0;
            for (int i = 0; i < Interactables.All.Count; i++)
                foreach (string label in labels)
                    if (Interactables.All[i].label == label) n++;
            return n;
        }

        // ————— 一次性演出队列(§137 双保险/§142 里程碑) —————

        [Test]
        public void CeremonyQueue_EnqueuedAtCompletionMilestones_PortBeforeFinancial()
        {
            CompleteChapters(1, 10);
            Assert.AreEqual(10, state.progression.completedChapter);
            Assert.IsTrue(state.progression.pendingCeremonies.Contains(ProgressSignals.EastDistrictCeremony),
                "完成第 10 章现场入队(东商业新区)。");
            Assert.IsFalse(state.progression.pendingCeremonies.Contains(ProgressSignals.PortOpeningCeremony));

            CompleteChapters(11, 22);
            Assert.IsTrue(state.progression.pendingCeremonies.Contains(ProgressSignals.CivicPlazaCeremony));

            CompleteChapters(23, 42);
            int port = state.progression.pendingCeremonies.IndexOf(ProgressSignals.PortOpeningCeremony);
            int financial = state.progression.pendingCeremonies.IndexOf(ProgressSignals.FinancialStreetCeremony);
            Assert.GreaterOrEqual(port, 0, "完成第 42 章同时触发开港演出入队。");
            Assert.GreaterOrEqual(financial, 0, "金融街落成演出入队。");
            Assert.Less(port, financial, "排队顺序:拆围挡(开港) > 落成(金融街)。");
        }

        [Test]
        public void MarkCeremonyViewed_ConsumesQueueAndWritesSignal_ExactlyOnce()
        {
            CompleteChapters(1, 10);
            Assert.IsTrue(ProgressionService.MarkCeremonyViewed(state, ProgressSignals.EastDistrictCeremony));
            Assert.IsTrue(ProgressionService.HasSignal(state, ProgressSignals.EastDistrictCeremony));
            Assert.IsFalse(state.progression.pendingCeremonies.Contains(ProgressSignals.EastDistrictCeremony));
            Assert.IsFalse(ProgressionService.MarkCeremonyViewed(state, ProgressSignals.EastDistrictCeremony),
                "二次消费失败:队列+signals 双保险防重播。");
        }

        void CompleteChapters(int from, int to)
        {
            IList<ChapterDefinition> chapters = ProgressionService.Chapters;
            for (int i = from - 1; i < to; i++)
                ProgressionService.RecordSignal(state, chapters[i].requiredSignal);
        }

        // ————— P1 书店「本周概念」(§141:落成首日即掷,同种子稳定,取数纪律) —————

        [Test]
        public void WeeklyConcept_OnlyAfterEastBuilt_FirstDayOfCompletion()
        {
            state.unlockedCards.Add("scarcity");
            state.unlockedCards.Add("supply");
            state.unlockedCards.Add("demand");
            state.todayEventId = null;

            state.progression.completedChapter = 9;   // 施工中:不占周号、不掷
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(0, state.economy.atmosphere.conceptWeek);
            Assert.AreEqual(-1, state.economy.atmosphere.weeklyConceptCard);

            long callsBefore = state.economy.atmosphereRandom.calls;
            state.progression.completedChapter = 10;  // 落成(结算现场越过):同一天即掷首期
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(TownGrowthService.WeekOf(state.day), state.economy.atmosphere.conceptWeek);
            Assert.Greater(state.economy.atmosphere.weeklyConceptCard, -1, "落成首日即有概念可读。");
            Assert.That(state.economy.atmosphereRandom.calls, Is.GreaterThan(callsBefore), "概念取数走 atmosphereRandom。");

            // 同日重算不重掷概念(取数纪律):第二次 ResolveDaily 只再消耗装饰天气 1 次,
            // 概念缓存原样(conceptWeek 未切换 → 不再走 PickWeeklyConcept)
            long callsAfterFirst = state.economy.atmosphereRandom.calls;
            int firstCard = state.economy.atmosphere.weeklyConceptCard;
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(firstCard, state.economy.atmosphere.weeklyConceptCard, "同周概念卡不变。");
            Assert.AreEqual(callsAfterFirst + 1, state.economy.atmosphereRandom.calls,
                "同日重算只重复天气掷骰,概念不再消耗随机流。");
        }

        [Test]
        public void WeeklyConcept_SameSeed_DoubleRunIdentical()
        {
            state.todayEventId = null;
            state.progression.completedChapter = 10;
            state.unlockedCards.Add("scarcity");
            state.unlockedCards.Add("marginalcost");
            state.unlockedCards.Add("elasticity");
            state.unlockedCards.Add("equilibrium");
            AtmosphereService.ResolveDaily(state);
            int runA = state.economy.atmosphere.weeklyConceptCard;
            int weekA = state.economy.atmosphere.conceptWeek;

            state.economy.atmosphereRandom.Reset(20260828 ^ unchecked((int)0x5f3759df));
            state.economy.atmosphere.conceptWeek = 0;
            state.economy.atmosphere.weeklyConceptCard = -1;
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(weekA, state.economy.atmosphere.conceptWeek);
            Assert.AreEqual(runA, state.economy.atmosphere.weeklyConceptCard, "同种子重掷同卡(双跑一致)。");
            Assert.IsTrue(state.unlockedCards.Contains(CardLibrary.All[runA].id), "命中的卡必须来自已解锁集合。");
        }

        // ————— P0-3 零经济副作用双跑(§119-3,含新区代码路径) —————

        [Test]
        public void VisualToggles_NoEconomicSideEffect_WithGrowthSystemsActive()
        {
            const int seedA = 20260828;
            var on = CaptureSegment(seedA, true, true);
            var off = CaptureSegment(seedA, false, false);
            Assert.AreEqual(on.Count, off.Count);
            for (int i = 0; i < on.Count; i++)
            {
                Assert.AreEqual(on[i].nominalGdp, off[i].nominalGdp, .05f, "GDP 与呈现开关无关(day " + (i + 1) + ")。");
                Assert.AreEqual(on[i].rate, off[i].rate, .0001f);
                Assert.AreEqual(on[i].cash, off[i].cash, .01f);
                Assert.AreEqual(on[i].unemployment, off[i].unemployment, .0001f);
                Assert.AreEqual(on[i].conceptCard, off[i].conceptCard, "周概念缓存与开关无关。");
            }
        }

        [Test]
        public void SameSeed_DoubleRun_WeeklyConceptSequenceIdentical()
        {
            const int seedA = 882311;
            var runA = CaptureSegment(seedA, true, true);
            var runB = CaptureSegment(seedA, true, true);
            for (int i = 0; i < runA.Count; i++)
            {
                Assert.AreEqual(runA[i].conceptCard, runB[i].conceptCard, "概念卡逐日一致(day " + (i + 1) + ")。");
                Assert.AreEqual(runA[i].weather, runB[i].weather);
                Assert.AreEqual(runA[i].transferSignShop, runB[i].transferSignShop);
            }
        }

        struct GrowthSample
        {
            public float nominalGdp, rate, cash, unemployment;
            public int weather, transferSignShop, conceptCard;
        }

        /// <summary>照 V0.7.1 双跑口径:预录 1–10 章信号让东商业新区在运行期保持落成态,
        /// 14 天结算里每周概念、转让牌、天气全走新区/氛围真实取数路径。</summary>
        List<GrowthSample> CaptureSegment(int seed, bool atmosphereEffects, bool ecoVisibility)
        {
            GameObject root = new GameObject("V073Segment_" + seed + "_" + atmosphereEffects);
            GameState s = root.AddComponent<GameState>();
            InternationalTradeService.Ensure(s);
            s.economy.random.Reset(seed);
            s.economy.tradeRandom.Reset(seed ^ 1013904223);
            s.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
            s.experience.settings.atmosphereEffects = atmosphereEffects;
            s.experience.settings.ecoVisibility = ecoVisibility;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            IList<ChapterDefinition> chapters = ProgressionService.Chapters;
            for (int i = 0; i < 10; i++)
                ProgressionService.RecordSignal(s, chapters[i].requiredSignal);
            ResidentEconomy.CreateProfile(s, "新区双跑", ResidentProfession.StoreClerk, ResidentPreference.Saver);

            List<GrowthSample> samples = new List<GrowthSample>();
            for (int i = 0; i < 16; i++)   // 跨周界(第 8 天)验证周切换取数
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
                samples.Add(new GrowthSample
                {
                    nominalGdp = s.macro.history[s.macro.history.Count - 1].nominalGdp,
                    rate = ExchangeRateService.Rate(s),
                    cash = s.money,
                    unemployment = s.macro.unemploymentRate,
                    weather = s.economy.atmosphere.weather,
                    transferSignShop = s.economy.atmosphere.transferSignShop,
                    conceptCard = s.economy.atmosphere.weeklyConceptCard
                });
            }
            Object.DestroyImmediate(root);
            return samples;
        }

        // ————— 旧档兼容(§143:缺字段走默认值,Ensure 复活) —————

        [Test]
        public void OldSaveJson_MissingWeeklyConceptFields_DefaultsWithoutException()
        {
            EconomyState restored = new EconomyState();
            JsonUtility.FromJsonOverwrite("{\"worldDay\":201,\"random\":{\"seed\":7,\"state\":7,\"calls\":3}}", restored);
            restored.Ensure();
            Assert.AreEqual(0, restored.atmosphere.conceptWeek, "旧档未占周号,落成首日才掷。");
            Assert.AreEqual(-1, restored.atmosphere.weeklyConceptCard, "旧档默认无概念卡。");
            Assert.AreEqual(201, restored.worldDay);

            // 旧档已完成 ≥42 章:新区直接呈现完工态,不补施工、不补播演出(§143)
            Assert.AreEqual(DistrictPhase.Completed, TownGrowthService.EastDistrict(50));
            Assert.AreEqual(DistrictPhase.Completed, TownGrowthService.CivicPlaza(50));
            Assert.AreEqual(DistrictPhase.Completed, TownGrowthService.FinancialStreet(50));
            Assert.AreEqual(0, state.progression.pendingCeremonies.Count, "旧档不回填演出队列。");
        }

        // ————— 工具 —————

        void BuildMap()
        {
            mapObject = new GameObject("V073GrowthMap");
            MapBuilder.Build(mapObject.transform);
        }

        Transform Town()
        {
            return mapObject.transform.Find("Town");
        }

        Transform Building(string name)
        {
            Transform t = Town().Find(name);
            Assert.IsNotNull(t, name + " 必须存在。");
            return t;
        }
    }
}

#endif
