#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EconTown
{
    /// <summary>端到端截图工具（V0.9.6b 新增）。街景/院落这类需求靠结构测试只能保几何、保不了好看，
    /// 历次 P1 验收（PRD §166-6/§173-6/§181）都要人工截图；此前每轮都靠临时脚本，本版收进仓库。
    ///
    /// 两种用法：
    ///   ① 编辑器菜单 EconTown/截图/小镇街景八张；
    ///   ② 命令行无头出图（不需要开编辑器、不进 Play）——
    ///      Unity -batchmode -projectPath &lt;工程&gt; -executeMethod EconTown.EconTownSceneShot.Shoot -quit
    ///
    /// 两条必须照抄的前提（踩过才知道）：
    ///   ① 必须自己补一盏与 GameBootstrap 同款的 Global Light2D——URP 2D 下 lit 精灵没有全局光会全黑；
    ///   ② URP 不支持 Camera.Render()，要走 SubmitRenderRequest(SingleCameraRequest)。</summary>
    public static class EconTownSceneShot
    {
        struct Shot
        {
            public string name;
            public float x, y, size;
            public int w, h;
            public string interiorKind;   // V0.9.7 店内机位;V0.9.11 起标 kind——出图前激活 ShopInteriorRoot+该子房间,街景机位为 null
            public int chapter, day;
            public bool night;
        }

        // “双街一轴、四区成镇”固定复验组：开局/全落成全景、四区近景、夜景、冬季。
        static readonly Shot[] layoutShots =
        {
            new Shot { name = "01_opening_overview",   x = 0f,    y = .5f,  size = 21f,   w = 1860, h = 1000, chapter = 0,  day = 1 },
            new Shot { name = "02_completed_overview", x = 0f,    y = .5f,  size = 21f,   w = 1860, h = 1000, chapter = 53, day = 1 },
            new Shot { name = "03_living_education",   x = -22.5f,y = 12f,  size = 8.5f,  w = 1400, h = 800,  chapter = 53, day = 1 },
            new Shot { name = "04_civic_culture",      x = -.5f,  y = 11f,  size = 9f,    w = 1400, h = 800,  chapter = 53, day = 1 },
            new Shot { name = "05_financial_knowledge",x = 20.5f, y = 11.25f,size = 9.5f, w = 1500, h = 800,  chapter = 53, day = 1 },
            new Shot { name = "06_trade_industry",     x = 0f,    y = -10f, size = 12.5f, w = 2000, h = 650,  chapter = 53, day = 1 },
            new Shot { name = "07_night",              x = 0f,    y = .5f,  size = 21f,   w = 1860, h = 1000, chapter = 53, day = 1,  night = true },
            new Shot { name = "08_winter",             x = 0f,    y = .5f,  size = 21f,   w = 1860, h = 1000, chapter = 53, day = 40 }
        };

        // 机位表：全景 + 三处院界 + 广场/校园/南街/西北排 + 四间店内。纵向 50px/世界单位 = 像素画整数 4 倍放大。
        static readonly Shot[] shots =
        {
            new Shot { name = "01_overview",  x = 0f,     y = 0f,    size = 21.5f, w = 1860, h = 1000 },
            new Shot { name = "02_home_yard", x = -16f,   y = 14f,   size = 6.5f,  w = 1000, h = 650 },
            new Shot { name = "03_shop_yard", x = -32f,   y = -7f,   size = 6.5f,  w = 1000, h = 650 },
            new Shot { name = "04_park",      x = -25f,   y = -17.5f,size = 5.5f,  w = 1150, h = 750 },
            new Shot { name = "05_plaza",     x = -.5f,   y = 9f,    size = 10f,   w = 1150, h = 750 },
            new Shot { name = "06_school",    x = -6f,    y = -16.5f,size = 6.5f,  w = 1080, h = 700 },
            new Shot { name = "07_south_row", x = 0f,     y = -8f,   size = 15.5f, w = 1800, h = 800 },
            new Shot { name = "08_northwest", x = -24f,   y = 12f,   size = 10.5f, w = 1400, h = 800 },
            // 店内:果汁店 16×11(V0.9.7,机位不变供基线逐像素比对);V0.9.11 三间 18×12@origin+40x——机位=各房中心
            new Shot { name = "09_shop_interior",       x = 8f,   y = 5.5f, size = 5.5f, w = 1200, h = 750, interiorKind = "shop" },
            new Shot { name = "10_restaurant_interior", x = 49f,  y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "restaurant" },
            new Shot { name = "11_cinema_interior",     x = 89f,  y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "cinema" },
            new Shot { name = "12_market_interior",     x = 129f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "market" },
            // V0.9.12 第二批六间:茶室/家/便利屋/书店/花店 18×12,零嘴亭小间 14×10——机位=各房中心
            new Shot { name = "13_tea_interior",          x = 169f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "competitor" },
            new Shot { name = "14_home_interior",         x = 209f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "home" },
            new Shot { name = "15_convenience_interior",  x = 249f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "convenience" },
            new Shot { name = "16_bookstore_interior",    x = 289f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "bookstore" },
            new Shot { name = "17_flower_interior",       x = 329f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "decor_b" },
            new Shot { name = "18_snack_interior",        x = 367f, y = 5f, size = 5.5f, w = 1200, h = 750, interiorKind = "decor_a" },

            // V0.9.13 第三批五间(机构与产业)
            new Shot { name = "19_bank_interior",          x = 409f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "bank" },
            new Shot { name = "20_centralbank_interior",   x = 449f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "centralbank" },
            new Shot { name = "21_hall_interior",          x = 489f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "hall" },
            new Shot { name = "22_training_interior",      x = 529f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "training" },
            new Shot { name = "23_factory_interior",       x = 569f, y = 6f, size = 6f, w = 1200, h = 750, interiorKind = "factory" }
        };

        [MenuItem("EconTown/截图/小镇街景二十三张")]
        public static void Shoot()
        {
            // 默认 <工程>/Temp/EconTownShots（编辑器内用）；无头跑要用 ECONTOWN_SHOT_DIR 指到别处——
            // Unity 在 batchmode 退出时会清掉整个 Temp/，图会跟着没。
            string outDir = System.Environment.GetEnvironmentVariable("ECONTOWN_SHOT_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/EconTownShots");
            Directory.CreateDirectory(outDir);

            GameObject stateGo = new GameObject("ShotState");
            GameObject lightGo = new GameObject("ShotGlobalLight2D");
            GameObject mapGo = new GameObject("ShotMap");
            GameObject camGo = new GameObject("ShotCam");
            try
            {
                GameState.I = stateGo.AddComponent<GameState>();
                ProgressionService.Ensure(GameState.I);

                Light2D light2d = lightGo.AddComponent<Light2D>();
                light2d.lightType = Light2D.LightType.Global;
                light2d.color = Color.white;
                light2d.intensity = 1f;

                MapBuilder.Build(mapGo.transform);

                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.08f, .09f, .12f, 1f);
                cam.nearClipPlane = -50f;
                cam.farClipPlane = 50f;

                for (int i = 0; i < shots.Length; i++)
                {
                    // 店内根与小镇坐标重叠:店内机位要同时藏 Town(否则广场/银行叠印进店里),
                    // 其余机位恢复 Town、保持店内隐藏。V0.9.11 根下多间子房间:
                    // 根一开若不逐间独占激活,四间会互相叠印——按机位 kind 只亮一间。
                    bool interiorShot = shots[i].interiorKind != null;
                    if (MapBuilder.TownRoot != null)
                        MapBuilder.TownRoot.gameObject.SetActive(!interiorShot);
                    if (MapBuilder.ShopInteriorRoot != null)
                        MapBuilder.ShopInteriorRoot.gameObject.SetActive(interiorShot);
                    // 底色随店(复审 V0911):运行时进店把相机底色换 spec 暗框色,截图同步——
                    // 围墙无贴图,窗口宽于房间时四边露出的就是这层底色
                    cam.backgroundColor = interiorShot
                        ? InteriorCatalog.Get(shots[i].interiorKind).backgroundColor
                        : new Color(.08f, .09f, .12f, 1f);
                    for (int r = 0; r < InteriorCatalog.All.Length; r++)
                    {
                        Transform room = MapBuilder.InteriorRoom(InteriorCatalog.All[r].kind);
                        if (room != null)
                            room.gameObject.SetActive(shots[i].interiorKind == InteriorCatalog.All[r].kind);
                    }
                    Capture(cam, shots[i], outDir);
                }
                Debug.Log("[EconTownSceneShot] " + shots.Length + " 张已出图 → " + outDir);
            }
            finally
            {
                GameState.I = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(mapGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(stateGo);
            }
        }

        [MenuItem("EconTown/截图/建筑布局复验八张")]
        public static void ShootTownLayout()
        {
            string outDir = System.Environment.GetEnvironmentVariable("ECONTOWN_LAYOUT_SHOT_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/EconTownLayoutShots");
            Directory.CreateDirectory(outDir);

            GameObject stateGo = new GameObject("LayoutShotState");
            GameObject lightGo = new GameObject("LayoutShotGlobalLight2D");
            GameObject mapGo = new GameObject("LayoutShotMap");
            GameObject camGo = new GameObject("LayoutShotCam");
            try
            {
                GameState state = stateGo.AddComponent<GameState>();
                GameState.I = state;
                ProgressionService.Ensure(state);
                GovernmentSimulator.Ensure(state);

                Light2D light2d = lightGo.AddComponent<Light2D>();
                light2d.lightType = Light2D.LightType.Global;
                MapBuilder.Build(mapGo.transform);

                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.nearClipPlane = -50f;
                cam.farClipPlane = 50f;

                for (int i = 0; i < layoutShots.Length; i++)
                {
                    Shot shot = layoutShots[i];
                    state.day = shot.day;
                    state.progression.completedChapter = shot.chapter;
                    bool completed = shot.chapter >= 53;
                    for (int p = 0; p < state.government.projects.Count; p++)
                        state.government.projects[p].built = completed;

                    MapBuilder.RefreshGrowthDistricts(state);
                    MapBuilder.RefreshGovernmentProjects(state);
                    MapBuilder.ApplySeason(state);

                    light2d.color = shot.night ? new Color(.46f, .55f, .74f) : Color.white;
                    light2d.intensity = shot.night ? .4f : 1f;
                    cam.backgroundColor = shot.night
                        ? new Color(.025f, .035f, .08f, 1f)
                        : new Color(.08f, .09f, .12f, 1f);
                    float nightAlpha = shot.night ? .95f : 0f;
                    MapBuilder.ApplyWindowLightAlpha(nightAlpha, nightAlpha);
                    MapBuilder.ApplyPublicLightAlpha(nightAlpha);
                    MapBuilder.ApplyBankFacadeLightsAlpha(nightAlpha);
                    Capture(cam, shot, outDir);
                }
                Debug.Log("[EconTownSceneShot] 建筑布局复验 8 张已出图 → " + outDir);
            }
            finally
            {
                GameState.I = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(mapGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(stateGo);
            }
        }

        static void Capture(Camera cam, Shot s, string outDir)
        {
            cam.transform.position = new Vector3(s.x, s.y, -10f);
            cam.orthographicSize = s.size;
            RenderTexture rt = new RenderTexture(s.w, s.h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.SubmitRenderRequest(new UniversalRenderPipeline.SingleCameraRequest { destination = rt });

            Texture2D tex = new Texture2D(s.w, s.h, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, s.name + ".png"), tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}

#endif
