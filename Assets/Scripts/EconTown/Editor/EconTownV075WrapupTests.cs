#if UNITY_EDITOR

using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.5 收口(PRD §154):
    /// 粒子并发全局 ≤200 且各系统份额锁定;滤镜两层(时段 × 单一状态层,事件旗标每日互斥);
    /// PixelArt 缓存换季/落成重建不泄漏(基线 +20% 断言);仓库不新增二进制素材(绊线:只允许
    /// StreamingAssets/Kenney 遗留区)。§155 截图清单与 §156–158 为文档/人工验收项,不入 EditMode。
    /// </summary>
    public class EconTownV075WrapupTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V075WrapupState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            InternationalTradeService.Ensure(state);
            DaySimulator.SetRandomSeed(20260829);
            LongTermExperienceService.Ensure(state);
            ProgressionService.Ensure(state);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
            ParticleBudget.ResetForTest();
        }

        // ————— §154-1:粒子预算 —————

        [Test]
        public void ParticleBudget_CapsStayWithinGlobalBudget()
        {
            // 最坏并发:雨(120) + 施工尘土(30) + 过场雨丝(26) = 176 ≤ 200
            Assert.LessOrEqual(ParticleBudget.RainCap + ParticleBudget.DustCap + ParticleBudget.CutsceneRainCap,
                ParticleBudget.GlobalCap, "雨+尘土+过场雨丝的最坏并发必须落在全局 200 内。");
            // 雪/秋叶/云团与雨互斥(WeatherParticles 同屏单模式),按各自份额单独验证
            Assert.LessOrEqual(ParticleBudget.SnowCap + ParticleBudget.DustCap + ParticleBudget.CutsceneRainCap,
                ParticleBudget.GlobalCap);
            Assert.LessOrEqual(ParticleBudget.LeavesCap + ParticleBudget.DustCap + ParticleBudget.CutsceneRainCap,
                ParticleBudget.GlobalCap);
            Assert.LessOrEqual(ParticleBudget.CloudsCap + ParticleBudget.DustCap + ParticleBudget.CutsceneRainCap,
                ParticleBudget.GlobalCap);

            // 账本口径:上报-合计-清账
            ParticleBudget.ResetForTest();
            ParticleBudget.Report("weather", 120);
            ParticleBudget.Report("dust", 30);
            ParticleBudget.Report("cutscene_rain", 26);
            Assert.AreEqual(176, ParticleBudget.TotalActive);
            Assert.AreEqual(120, ParticleBudget.ReportedBy("weather"));
            ParticleBudget.Report("cutscene_rain", 0);   // 过场结束清账
            Assert.AreEqual(150, ParticleBudget.TotalActive, "系统停止必须上报 0 清账,否则账本残留虚高。");
        }

        [Test]
        public void CloudyWeather_UsesDedicatedSubtleCloudShadow_NotCutsceneCloud()
        {
            GameObject weatherObject = new GameObject("CloudShadowStyleTest");
            try
            {
                WeatherParticles weather = weatherObject.AddComponent<WeatherParticles>();
                MethodInfo spawn = typeof(WeatherParticles).GetMethod("SpawnCloud",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(spawn);
                spawn.Invoke(weather, new object[] { Vector3.zero });

                Transform cloud = weatherObject.transform.Find("weather_cloud_shadow");
                Assert.IsNotNull(cloud, "阴天应生成专用云影，而不是复用过场乌云对象。");
                SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(renderer);
                StringAssert.StartsWith("weather_cloud_shadow_", renderer.sprite.name);
                Assert.AreNotSame(PixelArt.CutsceneCloudPatch(), renderer.sprite);
                Assert.LessOrEqual(renderer.color.a, 221f / 255f, "云影实例透明度必须保持克制。");

                Color32[] pixels = renderer.sprite.texture.GetPixels32();
                byte maxAlpha = 0;
                int transparent = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    maxAlpha = (byte)Mathf.Max(maxAlpha, pixels[i].a);
                    if (pixels[i].a == 0) transparent++;
                }
                Assert.LessOrEqual(maxAlpha, 42, "贴图自身不能再是深色实心云块。");
                Assert.Greater(transparent, pixels.Length / 4, "边缘需要有足够留白形成斑驳轮廓。");
            }
            finally
            {
                Object.DestroyImmediate(weatherObject);
            }
        }

        // ————— §154-2:滤镜两层——事件状态旗标每日互斥且换日复位 —————

        [Test]
        public void AtmosphereFilterFlags_ResetDailyAndMutuallyExclusive()
        {
            state.todayEventId = "hot";
            AtmosphereService.ResolveDaily(state);
            Assert.IsTrue(state.economy.atmosphere.heatWave);
            Assert.IsFalse(state.economy.atmosphere.gloomOverlay, "炎热与灰蒙不得同时占层(§154 两层预算)。");

            state.day++;
            state.todayEventId = "matup";
            AtmosphereService.ResolveDaily(state);
            Assert.IsTrue(state.economy.atmosphere.gloomOverlay);
            Assert.IsFalse(state.economy.atmosphere.heatWave, "换日必须复位旧旗标,否则状态层跨日叠加。");

            state.day++;
            state.todayEventId = null;
            AtmosphereService.ResolveDaily(state);
            Assert.IsFalse(state.economy.atmosphere.gloomOverlay);
            Assert.IsFalse(state.economy.atmosphere.heatWave, "无事件日两层状态全部退场,只剩时段层。");
        }

        // ————— §154-3:PixelArt 缓存——换季/落成重建不泄漏(基线 +20% 断言) —————

        [Test]
        public void PixelArtCache_SeasonAndGrowthRebuilds_DoNotLeak()
        {
            GameObject mapObject = new GameObject("V075CacheMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                // 基线 = 首次建图后,把全年四季与新区两态全集各走一遍(计入基线:
                // 换季本就要触达全部季节变体,这是"全集预算"而非泄漏)
                WarmRebuildCycles(1);
                int baseline = PixelArt.CacheCountForTest;
                Assert.Greater(baseline, 0);

                // 重建循环 ×3:同量重建不得新增缓存条目(≤ 基线,断言口径 +20% 留裕量)
                WarmRebuildCycles(3);
                int after = PixelArt.CacheCountForTest;
                Assert.LessOrEqual(after, Mathf.CeilToInt(baseline * 1.2f),
                    "换季/落成重建后缓存条目不得超基线 +20%(§154 预算断言)。实际 " + after + " vs 基线 " + baseline + "。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        void WarmRebuildCycles(int times)
        {
            for (int t = 0; t < times; t++)
            {
                for (int day = 1; day <= 52; day += 13)   // 四季代表日:1 春 / 14 夏 / 27 秋 / 40 冬
                {
                    state.day = day;
                    MapBuilder.ApplySeason(state);
                }
                // 新区两态:未开工 → 施工 → 落成,每态一次全量刷新
                state.progression.completedChapter = 0; MapBuilder.RefreshGrowthDistricts(state);
                state.progression.completedChapter = 6; MapBuilder.RefreshGrowthDistricts(state);
                state.progression.completedChapter = 10; MapBuilder.RefreshGrowthDistricts(state);
                state.progression.completedChapter = 22; MapBuilder.RefreshGrowthDistricts(state);
                state.progression.completedChapter = 42; MapBuilder.RefreshGrowthDistricts(state);
            }
        }

        // ————— §154-4:仓库不新增二进制素材(绊线:只允许 Kenney 遗留区) —————

        [Test]
        public void AssetsContainNoBinaryArtifactsOutsideLegacyKenney()
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".exr", ".tif", ".bmp", ".mp3", ".wav", ".ogg" };
            string dataPath = Application.dataPath;
            int legacyCount = 0;
            foreach (string file in Directory.EnumerateFiles(dataPath, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                bool binary = false;
                for (int i = 0; i < extensions.Length; i++)
                    if (ext == extensions[i]) { binary = true; break; }
                if (!binary) continue;

                string normalized = file.Replace('\\', '/');
                Assert.IsTrue(normalized.Contains("/StreamingAssets/Kenney/"),
                    "Assets 内出现 Kenney 遗留区之外的二进制素材(§154-4 全部新贴图程序生成): " + file);
                legacyCount++;
            }
            Assert.LessOrEqual(legacyCount, 60,
                "Kenney 遗留贴图数量超出预期(基线 52):若属合法扩包请更新此绊线常量并同步 §154 说明。");
            Debug.Log("[V075] Kenney 遗留二进制素材 " + legacyCount + " 件,其余 Assets 零二进制。");
        }
    }
}

#endif
