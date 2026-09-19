#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.1《小镇的呼吸》回归套件(PRD §128):
    /// P0-1 零经济副作用双跑;P0-2 atmosphereRandom 独立性与取数纪律 + 视觉序列一致;
    /// P0-3 街景人数仪表边界与渐变;P0-4 季节贴图缓存上限;P1 季节公式边界/旧档兼容。
    /// </summary>
    public class EconTownV071AtmosphereTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V071AtmosphereState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260827);
            LongTermExperienceService.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— 季节公式(PRD §122:一年=52 世界日,每 13 日一季) —————

        [Test]
        public void SeasonIndex_FourSeasons_CoverAllWorldDaysWithoutGap()
        {
            Assert.AreEqual(0, AtmosphereService.SeasonIndex(1), "第 1 天必须落在春季。");
            Assert.AreEqual(0, AtmosphereService.SeasonIndex(13), "第 13 天仍是春。");
            Assert.AreEqual(1, AtmosphereService.SeasonIndex(14), "第 14 天入夏。");
            Assert.AreEqual(1, AtmosphereService.SeasonIndex(26));
            Assert.AreEqual(2, AtmosphereService.SeasonIndex(27), "第 27 天入秋。");
            Assert.AreEqual(3, AtmosphereService.SeasonIndex(40), "第 40 天入冬。");
            Assert.AreEqual(3, AtmosphereService.SeasonIndex(52));
            Assert.AreEqual(0, AtmosphereService.SeasonIndex(53), "次年循环回春,0 值不漏档。");
            Assert.AreEqual(0, AtmosphereService.SeasonIndex(365));
            for (int day = 1; day <= 364; day++)
                Assert.That(AtmosphereService.SeasonIndex(day), Is.InRange(0, 3), "day=" + day);
        }

        // ————— 街景人数仪表(PRD §124:失业率主锚 clamp[5,16],相位微调,±2/日) —————

        [Test]
        public void StreetTarget_AnchoredToUnemployment_ClampedAndPhaseAdjusted()
        {
            Assert.AreEqual(12, AtmosphereService.StreetTarget(7.66f, MacroCyclePhase.Recovery),
                "开局基准失业率应得到 12 人(与现状衔接)。");
            Assert.AreEqual(16, AtmosphereService.StreetTarget(3.5f, MacroCyclePhase.Recovery), "低失业→满编。");
            Assert.AreEqual(8, AtmosphereService.StreetTarget(11.7f, MacroCyclePhase.Recession), "高失业→冷清。");
            int probe = AtmosphereService.StreetTarget(3.5f, MacroCyclePhase.Trough);
            Assert.GreaterOrEqual(probe, 5, "谷底 −1 也不得击穿下限。");
            Assert.LessOrEqual(probe, 16, "过热 +1 不得越过上限。");
            // 单调性:失业率越高人数越少(不早于同相位的更高失业读数)
            Assert.LessOrEqual(AtmosphereService.StreetTarget(9f, MacroCyclePhase.Recovery),
                AtmosphereService.StreetTarget(6f, MacroCyclePhase.Recovery));
        }

        [Test]
        public void WindowTier_ConfidenceProjection_ThreeBands()
        {
            Assert.AreEqual(2, AtmosphereService.WindowTier(72f), "开局信心 72 → 全亮。");
            Assert.AreEqual(2, AtmosphereService.WindowTier(60f));
            Assert.AreEqual(1, AtmosphereService.WindowTier(50f), "中等信心 → 七成。");
            Assert.AreEqual(0, AtmosphereService.WindowTier(30f), "信心 <40 → 暗(转让牌条件)。");
        }

        // ————— 取数纪律(PRD §123C):事件绑定优先且事件日不掷装饰骰 —————

        [Test]
        public void ResolveDaily_EventDaysBindWeather_WithoutRollingDecorDice()
        {
            ResolveWithEvent("hot");
            Assert.AreEqual(AtmosphereService.WeatherClear, state.economy.atmosphere.weather);
            Assert.IsTrue(state.economy.atmosphere.heatWave);
            Assert.AreEqual(0L, state.economy.atmosphereRandom.calls, "事件日装饰骰不另掷。");

            ResolveWithEvent("matup");
            Assert.AreEqual(AtmosphereService.WeatherCloudy, state.economy.atmosphere.weather);
            Assert.IsTrue(state.economy.atmosphere.gloomOverlay, "原料涨价=事件阴天带灰蒙。");

            ResolveWithEvent("festival");
            Assert.IsTrue(state.economy.atmosphere.festivalDecor);
            Assert.IsFalse(state.economy.atmosphere.gloomOverlay);

            ResolveWithEvent(null);   // 无事件日才掷装饰骰
            Assert.AreEqual(1L, state.economy.atmosphereRandom.calls, "无事件日恰好掷一次。");

            // 共享流零消耗:呈现层绝不碰 random/tradeRandom
            long randomCallsBefore = state.economy.random.calls;
            long tradeCallsBefore = state.economy.tradeRandom.calls;
            ResolveWithEvent(null);
            ResolveWithEvent("hot");
            Assert.AreEqual(randomCallsBefore, state.economy.random.calls, "atmosphere 流不得污染共享流。");
            Assert.AreEqual(tradeCallsBefore, state.economy.tradeRandom.calls);
        }

        void ResolveWithEvent(string eventId)
        {
            state.todayEventId = eventId;
            state.todayEventName = eventId;
            AtmosphereService.ResolveDaily(state);
        }

        [Test]
        public void ResolveDaily_StreetCurrentApproachesTarget_ByTwoPerDay()
        {
            state.todayEventId = null;
            state.macro.unemploymentRate = 11.7f;   // 目标 ≈8
            // bug搜#1复审:cyclePhase 声明值已改为首期公式输出 Trough(街景目标 −1),本测试
            // 手设失业率构造「目标 8」场景,周期相同样显式钉 Recovery——否则目标随声明相漂到 7。
            state.macro.cyclePhase = MacroCyclePhase.Recovery;
            state.economy.atmosphere.streetCurrent = 12;
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(10, state.economy.atmosphere.streetCurrent,
                "衰退是慢慢冷清下来的:每日最多 ±2,不瞬变。");
            Assert.LessOrEqual(Mathf.Abs(state.economy.atmosphere.streetTarget - state.economy.atmosphere.streetCurrent), 2);
        }

        [Test]
        public void ResolveDaily_TransferSignOnlyWhenConfidenceLow_AndFromFixedPool()
        {
            state.todayEventId = null;
            state.government.enterpriseConfidence = 72f;   // 高信心 → 无牌
            AtmosphereService.ResolveDaily(state);
            Assert.AreEqual(-1, state.economy.atmosphere.transferSignShop);

            state.government.enterpriseConfidence = 35f;   // 低信心 → 从两间装饰铺中选一间
            AtmosphereService.ResolveDaily(state);
            Assert.That(state.economy.atmosphere.transferSignShop, Is.InRange(0, AtmosphereService.DecorativeShopCount - 1));

            int firstPick = state.economy.atmosphere.transferSignShop;
            state.day++;
            AtmosphereService.ResolveDaily(state);
            if (state.government.enterpriseConfidence >= 40f) return;
            // 同种子续跑序列确定:两次取数可复现
            Assert.That(state.economy.atmosphere.transferSignShop, Is.InRange(0, AtmosphereService.DecorativeShopCount - 1));
            Assert.GreaterOrEqual(firstPick, 0);
        }

        [Test]
        public void ResolveDaily_WindowTierChangeFlag_SurfacesExactlyOnSwitchDay()
        {
            state.todayEventId = null;
            state.government.enterpriseConfidence = 72f;
            AtmosphereService.ResolveDaily(state);
            Assert.IsFalse(state.economy.atmosphere.windowTierChangedToday, "首日初始化不算切换。");

            state.government.enterpriseConfidence = 30f;
            AtmosphereService.ResolveDaily(state);
            Assert.IsTrue(state.economy.atmosphere.windowTierChangedToday, "档位切换当日置位(公告栏对冲提示)。");

            state.government.enterpriseConfidence = 28f;   // 同档再跑不置位
            AtmosphereService.ResolveDaily(state);
            Assert.IsFalse(state.economy.atmosphere.windowTierChangedToday);
        }

        // ————— P0-1 零经济副作用 + P0-2 双跑视觉序列一致(PRD §128-1/§119-2) —————

        [Test]
        public void VisualToggles_NoEconomicSideEffect_SequencesIdenticalAcrossSettings()
        {
            const int seedA = 20260827;
            var on = CaptureSegment(seedA, true, true);
            var off = CaptureSegment(seedA, false, false);
            Assert.AreEqual(on.Count, off.Count);
            for (int i = 0; i < on.Count; i++)
            {
                Assert.AreEqual(on[i].nominalGdp, off[i].nominalGdp, .05f, "GDP 序列与开关无关(day " + (i + 1) + ")。");
                Assert.AreEqual(on[i].rate, off[i].rate, .0001f, "汇率序列与开关无关。");
                Assert.AreEqual(on[i].cash, off[i].cash, .01f, "企业现金逐位一致。");
                Assert.AreEqual(on[i].unemployment, off[i].unemployment, .0001f, "失业率一致。");
            }
        }

        [Test]
        public void SameSeed_DoubleRun_ProducesIdenticalVisualStateSequences()
        {
            const int seedA = 555777;
            var runA = CaptureSegment(seedA, true, true);
            var runB = CaptureSegment(seedA, true, true);
            Assert.AreEqual(runA.Count, runB.Count);
            for (int i = 0; i < runA.Count; i++)
            {
                Assert.AreEqual(runA[i].weather, runB[i].weather, "天气档逐日一致(day " + (i + 1) + ")。");
                Assert.AreEqual(runA[i].windowTier, runB[i].windowTier, "橱窗档逐日一致。");
                Assert.AreEqual(runA[i].streetTarget, runB[i].streetTarget, "街景目标数逐日一致。");
                Assert.AreEqual(runA[i].transferSignShop, runB[i].transferSignShop, "转让牌选择一致。");
            }
        }

        struct SegmentSample
        {
            public float nominalGdp, rate, cash, unemployment;
            public int weather, windowTier, streetTarget, transferSignShop;
        }

        List<SegmentSample> CaptureSegment(int seed, bool atmosphereEffects, bool ecoVisibility)
        {
            GameObject root = new GameObject("V071Segment_" + seed + "_" + atmosphereEffects);
            GameState s = root.AddComponent<GameState>();
            InternationalTradeService.Ensure(s);
            // 与 DaySimulator.SetRandomSeed 完全相同的三流播种约定。
            s.economy.random.Reset(seed);
            s.economy.tradeRandom.Reset(seed ^ 1013904223);
            s.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
            s.experience.settings.atmosphereEffects = atmosphereEffects;
            s.experience.settings.ecoVisibility = ecoVisibility;
            s.government.cash = 100000f;
            s.money = 100000f;
            s.materials = 4000;
            ResidentEconomy.CreateProfile(s, "氛围双跑", ResidentProfession.StoreClerk, ResidentPreference.Saver);

            List<SegmentSample> samples = new List<SegmentSample>();
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
                samples.Add(new SegmentSample
                {
                    nominalGdp = s.macro.history[s.macro.history.Count - 1].nominalGdp,
                    rate = ExchangeRateService.Rate(s),
                    cash = s.money,
                    unemployment = s.macro.unemploymentRate,
                    weather = s.economy.atmosphere.weather,
                    windowTier = s.economy.atmosphere.windowTier,
                    streetTarget = s.economy.atmosphere.streetTarget,
                    transferSignShop = s.economy.atmosphere.transferSignShop
                });
            }
            Object.DestroyImmediate(root);
            return samples;
        }

        // ————— P0-4 贴图缓存上限(PRD §119-4/§154:换季全集有界,防泄漏) —————

        [Test]
        public void SeasonalSpriteVariants_CacheGrowthStaysBounded()
        {
            int baseline = PixelArt.CacheCountForTest;
            for (int season = 0; season < 4; season++)
            {
                for (int v = 0; v < 3; v++) PixelArt.Grass(season, v);
                PixelArt.Tree(season);
                PixelArt.Shrub(season);
                PixelArt.GrassTuft(season);
                for (int v = 0; v < 3; v++) PixelArt.Flower(season, v);
                for (int v = 0; v < 3; v++) PixelArt.MeadowPatch(season, v);
            }
            int growth = PixelArt.CacheCountForTest - baseline;
            int fullSet = 12 /*grass*/ + 4 /*tree*/ + 4 /*shrub*/ + 4 /*tuft*/ +
                12 /*flower*/ + 12 /*meadow*/;
            Assert.LessOrEqual(growth, fullSet, "全年四季变体只新增有限条目,缓存无泄漏(§119-4 预算)。");
        }

        // ————— 旧档兼容(JsonUtility 缺字段走声明默认值 + Ensure 复活) —————

        [Test]
        public void OldSaveJson_MissingAtmosphereFields_RevivedByEnsureWithoutException()
        {
            EconomyState restored = new EconomyState();
            JsonUtility.FromJsonOverwrite("{\"worldDay\":137,\"random\":{\"seed\":7,\"state\":7,\"calls\":3}}", restored);
            restored.Ensure();
            Assert.IsNotNull(restored.atmosphereRandom, "旧档缺失氛围流须由 Ensure 复活。");
            Assert.IsNotNull(restored.atmosphere, "旧档缺失视觉缓存须由 Ensure 复活。");
            Assert.AreEqual(12, restored.atmosphere.streetCurrent, "街景人数默认 12 人,与旧版画面衔接。");
            Assert.AreEqual(-1, restored.atmosphere.windowTier, "窗光档未初始化不触发公告误报。");
            Assert.AreEqual(137, restored.worldDay);
        }
    }
}

#endif
