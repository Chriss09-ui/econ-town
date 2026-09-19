#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.8.1《小镇地标》结构护栏（PRD §159-5 布局不变式 / §160 门槛 / §162-166）：
    /// ① 规格表全覆盖且几何自洽（画布/碰撞/门位/三贴图同画布）；
    /// ② LayoutGuard——任意两碰撞体净距 ≥0.4、门 0.8×0.8 区域无遮挡、贴图不越墙不越界；
    /// ③ Build 两次快照逐位一致（布局是静态常量的确定性要求）。</summary>
    public class EconTownV081LandmarkTests
    {
        GameObject stateObject;
        GameObject mapObject;

        static readonly string[] AllKinds =
        {
            "shop", "competitor", "market", "factory", "port", "hall", "training", "school", "convenience",
            "restaurant", "cinema", "bank", "centralbank", "decor_a", "decor_b",
            "bookstore", "empty_a", "empty_b", "home"
        };

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V081State");
            GameState.I = stateObject.AddComponent<GameState>();
            ProgressionService.Ensure(GameState.I);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (mapObject != null) Object.DestroyImmediate(mapObject);
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void SpecTable_AllKindsCovered_GeometryConsistent()
        {
            int registered = 0;
            foreach (string kind in BuildingSpecs.RegisteredKinds) registered++;
            Assert.AreEqual(AllKinds.Length, registered, "规格表 kind 数量应与全量 kind 一致，无漏登无多余。");

            foreach (string kind in AllKinds)
            {
                BuildingSpec spec = BuildingSpecs.Get(kind);
                Assert.AreEqual(0, spec.canvasW % 8, kind + " 画布宽须 8px 对齐。");
                Assert.AreEqual(0, spec.canvasH % 8, kind + " 画布高须 8px 对齐。");

                float spriteW = spec.canvasW / (float)PixelArt.PPU * spec.scale;
                float spriteH = spec.canvasH / (float)PixelArt.PPU * spec.scale;
                float colliderW = spec.colliderSize.x * spec.scale;
                float colliderH = spec.colliderSize.y * spec.scale;
                Assert.LessOrEqual(colliderW, spriteW + .01f, kind + " 碰撞体宽超出贴图 footprint。");
                Assert.LessOrEqual(colliderH, spriteH + .01f, kind + " 碰撞体高超出贴图 footprint。");

                // 门位不变式：在碰撞体前方留 ≥0.5 站立位，且不离贴图基线过远（≤0.75——
                // 空铺 scale=1.0 的门点在基线前 0.7 是既有线上行为，阈值按它取整）
                float colliderBottom = spec.colliderOffset.y * spec.scale - colliderH * .5f;
                float spriteBase = -spriteH * .5f;
                Assert.LessOrEqual(spec.doorOffset.y, colliderBottom - .5f,
                    kind + " 门前站立位不足 0.5。");
                Assert.GreaterOrEqual(spec.doorOffset.y, spriteBase - .75f,
                    kind + " 门点脱离贴图基线过远。");

                // V0.9.6e 纪律《碰撞体下沿必须齐墙脚》：下沿一格都不许悬在墙脚之上，
                // 否则那条带子贴图是实心墙、物理上却能站人 → 人站在房屋里（PRD §171.6）。
                Assert.AreEqual(spriteBase, colliderBottom, .02f,
                    kind + " 碰撞体下沿未齐贴图墙脚（悬空 " + (colliderBottom - spriteBase).ToString("F2") + "）。");

                // V0.9.6g 纪律《碰撞体贴合可见轮廓》（PRD §171.8）：上沿只到中心上方 0.35
                // （排序临界 0.2375+裕量）——再高就把屋顶两侧的透明留白封成「看不见的墙」，
                // 那里角色排序画在建筑后面、本该被房子正常挡住，封死就是「空白却走不进去」。
                float colliderTop = spec.colliderOffset.y * spec.scale + colliderH * .5f;
                Assert.AreEqual(.35f, colliderTop, .03f,
                    kind + " 碰撞体上沿应为中心上方 0.35（实际 " + colliderTop.ToString("F2") + "）。");

                // 宽度不得超过排序临界带（中心+0.25＝排序临界〔任意 center〕的理论上限，
                // 本镇各建筑逐栋实测最大 0.2375〔便利屋〕，采样取上限保守超集）内贴图
                // 相对中心的最大对称伸距——按档共享的宽盒在窄房子两侧各留出最多 0.9 格的
                // 空白碰撞带（我的家旧盒 17.7 格里 10.1 格是空白草地，用户截图复验）。
                // 用对称伸距而非最大跨度：招牌/烟囱偏一侧的画法会骗过跨度口径。
                Texture2D bandTex = PixelArt.Building(kind).texture;
                Color32[] bandPx = bandTex.GetPixels32();
                int centerU = bandTex.width / 2, centerV = bandTex.height / 2;
                int bandTopRow = Mathf.Min(bandTex.height - 1,
                    centerV + Mathf.FloorToInt(.25f / spec.scale * PixelArt.PPU));
                int halfPx = 0;
                for (int v = 0; v <= bandTopRow; v++)
                {
                    int minU = bandTex.width, maxU = -1;
                    for (int u = 0; u < bandTex.width; u++)
                        if (bandPx[v * bandTex.width + u].a >= 128) { if (u < minU) minU = u; if (u > maxU) maxU = u; }
                    if (maxU < 0) continue;
                    int ext = Mathf.Max(centerU - minU, maxU - centerU);
                    if (ext > halfPx) halfPx = ext;
                }
                float bandWidth = halfPx * 2f / (float)PixelArt.PPU * spec.scale;
                Assert.LessOrEqual(colliderW, bandWidth + .02f,
                    kind + " 碰撞体比临界带内可见轮廓宽 " + (colliderW - bandWidth).ToString("F2") + "（看不见的墙）。");

                // 三贴图同画布（对齐不错位的测试形态，PRD §160-3）
                Assert.AreEqual(spec.canvasW, PixelArt.Building(kind).texture.width, kind + " Building 画布宽。");
                Assert.AreEqual(spec.canvasH, PixelArt.Building(kind).texture.height, kind + " Building 画布高。");
                Assert.AreEqual(spec.canvasW, PixelArt.WindowGlow(kind).texture.width, kind + " WindowGlow 画布宽。");
                Assert.AreEqual(spec.canvasH, PixelArt.WindowGlow(kind).texture.height, kind + " WindowGlow 画布高。");
                Assert.AreEqual(spec.canvasW, PixelArt.SnowCap(kind).texture.width, kind + " SnowCap 画布宽。");
                Assert.AreEqual(spec.canvasH, PixelArt.SnowCap(kind).texture.height, kind + " SnowCap 画布高。");
            }
        }

        [Test]
        public void LayoutGuard_NoColliderOverlap_DoorsReachable_BuildingsInsideMap()
        {
            mapObject = new GameObject("V081Map");
            MapBuilder.Build(mapObject.transform);
            AssertLayoutGuard("开局档(cc=0)");

            GameState.I.progression.completedChapter = 53;
            MapBuilder.RefreshGrowthDistricts(GameState.I);
            AssertLayoutGuard("全落成档(cc=53)");
        }

        /// <summary>V0.9.6e《人不许站在房屋里》（PRD §171.6）。用户游戏内截图复验：NPC 整个人显形在墙面上。
        /// 根因不是排序——SpriteSwapper 每帧按 SortingForY(y)+2 给角色重排，是对的；根因是**碰撞体没盖住墙**：
        /// 修前各档下沿都悬在墙脚之上（S 0.96 / M 1.66 / L 1.98 世界格），L 档两翼左右还各短 0.38，
        /// 那些带子贴图上画的是实心墙、物理上却能站人，而角色 y 低于建筑中心时排序在建筑之前 → 人显形在墙上。
        /// 修前 0.5 格采样实测 990 格（全采样面 17.8%）中招，18 栋无一例外。
        /// 本测试按缺陷的定义直扫 NPC 漫游面（含增长区/港区），且用**贴图真实 alpha** 判定「画了墙」——
        /// 用包围盒会把屋顶两侧的透明留白误判成墙。屋顶带（角色排序在建筑之后）不算缺陷：那里角色被正常遮住。</summary>
        [Test]
        public void NoWalkableCell_OnPaintedWall_WhereCharacterDrawsInFront()
        {
            mapObject = new GameObject("V081WallFootMap");
            MapBuilder.Build(mapObject.transform);
            AssertNoWalkablePaintedWall("开局档(cc=0)");

            GameState.I.progression.completedChapter = 53;
            MapBuilder.RefreshGrowthDistricts(GameState.I);
            AssertNoWalkablePaintedWall("全落成档(cc=53)");
        }

        void AssertNoWalkablePaintedWall(string scenario)
        {
            var buildings = new List<SpriteRenderer>();
            SpriteRenderer[] all = mapObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.StartsWith("B_") && all[i].gameObject.activeInHierarchy) buildings.Add(all[i]);
            Assert.Greater(buildings.Count, 10, scenario + " 建筑数量异常，扫描口径可能失效。");

            var bad = new List<string>();
            int badCount = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingIdentity identity = buildings[i].GetComponent<BuildingIdentity>();
                string kind = identity != null ? identity.kind : "home";
                BuildingSpec spec = BuildingSpecs.Get(kind);
                Texture2D tex = PixelArt.Building(kind).texture;
                Vector3 pos = buildings[i].transform.position;
                for (float x = MapBuilder.NpcWanderMinX; x <= MapBuilder.NpcWanderMaxX; x += .25f)
                    for (float y = MapBuilder.NpcWanderMinY; y <= MapBuilder.NpcWanderMaxY; y += .25f)
                    {
                        // 角色排序 = SortingForY(y)+2；不大于建筑 order 说明会被建筑挡住，不是缺陷
                        if (MapBuilder.SortingForY(y) + 2 <= buildings[i].sortingOrder) continue;
                        int u = Mathf.FloorToInt((x - pos.x) / spec.scale * PixelArt.PPU + tex.width * .5f);
                        int v = Mathf.FloorToInt((y - pos.y) / spec.scale * PixelArt.PPU + tex.height * .5f);
                        if (u < 0 || v < 0 || u >= tex.width || v >= tex.height) continue;
                        if (tex.GetPixel(u, v).a < .5f) continue;                       // 贴图这里是透明的，不是墙
                        // V0.9.6g 修正:isTrigger 的点击碰撞体不算「已被挡住」——它没有物理,
                        // 算进去会把扫描面在建筑附近整片蒙蔽(覆盖悄悄缩水,测试照样绿)
                        Collider2D[] wallHits = Physics2D.OverlapPointAll(new Vector2(x, y));
                        bool blocked = false;
                        for (int k = 0; k < wallHits.Length; k++)
                            if (!wallHits[k].isTrigger) { blocked = true; break; }
                        if (blocked) continue;                                          // 已被实体碰撞体挡住
                        badCount++;
                        if (bad.Count < 12)
                            bad.Add(buildings[i].name + "@(" + x.ToString("F2") + "," + y.ToString("F2") + ")");
                    }
            }
            Assert.IsEmpty(bad, scenario + " 墙面上有可站立格（人站在房屋里，共 " + badCount + " 格，0.25 格采样）：" +
                string.Join(" ", bad.ToArray()));
        }

        public static void AssertLayoutGuard(string scenario)   // V0.9.3 起跨套件复用(住房要素相位档护栏)
        {
            Transform town = MapBuilder.TownRoot;
            Assert.IsNotNull(town, scenario + " 缺少 Town 根。");

            // 1) 收集全部激活碰撞体（含墙/工地/交互牌；港区为独立根不计入）。
            // V0.9.6g:isTrigger 的点击碰撞体不是屏障,不入净距账(§171.8 纪律 4)。
            var boxes = new List<BoxCollider2D>();
            CollectActiveBoxes(town, boxes);
            Assert.Greater(boxes.Count, 10, scenario + " 碰撞体数量异常。");

            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    // 屏障类(地图围墙 wall / V0.8.2 院落围栏 fence_*)彼此贴接、转角与门柱收口是设计行为,
                    // 按既有 wall-wall 四角搭接先例整类豁免;屏障 vs 建筑/门/工地仍走净距护栏。
                    if (IsBarrier(boxes[i]) && IsBarrier(boxes[j])) continue;
                    Gap(boxes[i], boxes[j], out float gapX, out float gapY);
                    Assert.IsTrue(gapX >= .4f || gapY >= .4f,
                        scenario + " 碰撞净距不足 0.4：" + boxes[i].name + "(" + boxes[i].transform.parent.name +
                        ") vs " + boxes[j].name + "(" + boxes[j].transform.parent.name +
                        ") gapX=" + gapX.ToString("F2") + " gapY=" + gapY.ToString("F2"));
                }

            // 2) 每张门 0.8×0.8 区域不被任何碰撞体遮挡
            Vector3[] doors =
            {
                MapBuilder.ShopDoor, MapBuilder.CompetitorDoor, MapBuilder.MarketDoor, MapBuilder.FactoryDoor,
                MapBuilder.PortDoor, MapBuilder.HallDoor, MapBuilder.TrainingDoor, MapBuilder.ConvenienceDoor,
                MapBuilder.HomeDoor, MapBuilder.RestaurantDoor, MapBuilder.CinemaDoor, MapBuilder.BankDoor,
                MapBuilder.CentralBankDoor
            };
            for (int d = 0; d < doors.Length; d++)
                for (int i = 0; i < boxes.Count; i++)
                {
                    Vector2 half = WorldHalf(boxes[i]);
                    Vector2 c = WorldCenter(boxes[i]);
                    bool hitX = Mathf.Abs(doors[d].x - c.x) < .4f + half.x;
                    bool hitY = Mathf.Abs(doors[d].y - c.y) < .4f + half.y;
                    Assert.IsFalse(hitX && hitY,
                        scenario + " 门口被碰撞体遮挡：" + doors[d] + " vs " + boxes[i].transform.parent.name);
                }

            // 3) 建筑贴图不越墙、不出图（含升档后的大画布）
            for (int i = 0; i < town.childCount; i++)
            {
                Transform child = town.GetChild(i);
                if (!child.name.StartsWith("B_")) continue;
                BuildingIdentity identity = child.GetComponent<BuildingIdentity>();
                string kind = identity != null ? identity.kind : "home";
                BuildingSpec spec = BuildingSpecs.Get(kind);
                float top = child.position.y + spec.WorldHalfHeight;
                float bottom = child.position.y - spec.WorldHalfHeight;
                float left = child.position.x - spec.WorldHalfWidth;
                float right = child.position.x + spec.WorldHalfWidth;
                Assert.LessOrEqual(top, MapBuilder.WorldMaxY - .2f, scenario + " " + child.name + " 贴图顶越墙。");
                Assert.GreaterOrEqual(bottom, MapBuilder.WorldMinY + .2f, scenario + " " + child.name + " 贴图底出图。");
                Assert.GreaterOrEqual(left, MapBuilder.WorldMinX + .2f, scenario + " " + child.name + " 贴图西出图。");
                Assert.LessOrEqual(right, MapBuilder.WorldMaxX - .2f, scenario + " " + child.name + " 贴图东出图。");
            }

            // 4) V0.9.6g:每栋必须有一张 isTrigger 的整贴图盒点击碰撞体——行走碰撞体收成
            //    临界带横带后,OnMouseDown 靠它保住「点屋顶也有响应」(§171.8 纪律 4)。
            //    递归遍历:增长区的书店/空铺不在 Town 直接子级(护栏口径教训 §173-12)。
            SpriteRenderer[] renderers = town.GetComponentsInChildren<SpriteRenderer>(true);
            int clickChecked = 0;
            int passiveChecked = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.StartsWith("B_") || !renderers[i].gameObject.activeInHierarchy) continue;
                Transform building = renderers[i].transform;
                BuildingIdentity identity = building.GetComponent<BuildingIdentity>();
                BuildingSpec bSpec = BuildingSpecs.Get(identity != null ? identity.kind : "home");
                BoxCollider2D[] own = building.GetComponents<BoxCollider2D>();
                bool hasClick = false;
                for (int k = 0; k < own.Length; k++)
                    if (own[k].isTrigger)
                    {
                        hasClick = true;
                        Assert.GreaterOrEqual(own[k].size.x * bSpec.scale, bSpec.WorldHalfWidth * 2f - .05f,
                            scenario + " " + building.name + " 点击区没盖住贴图宽。");
                        Assert.GreaterOrEqual(own[k].size.y * bSpec.scale, bSpec.WorldHalfHeight * 2f - .05f,
                            scenario + " " + building.name + " 点击区没盖住贴图高。");
                    }
                bool shouldClick = identity == null || identity.interactive;
                Assert.AreEqual(shouldClick, hasClick,
                    scenario + " " + building.name + (shouldClick ? " 缺点击触发体。" : " 纯场景建筑不应有点击触发体。"));
                if (shouldClick) clickChecked++;
                else passiveChecked++;
            }
            Assert.Greater(clickChecked, 10, scenario + " 点击区扫描口径失效(建筑数异常)。");
            Assert.AreEqual(1, passiveChecked, scenario + " 当前应只有社区学校一栋纯场景建筑。");
        }

        static void CollectActiveBoxes(Transform root, List<BoxCollider2D> into)
        {
            BoxCollider2D[] own = root.GetComponents<BoxCollider2D>();
            for (int i = 0; i < own.Length; i++)
                if (own[i].gameObject.activeInHierarchy && !own[i].isTrigger) into.Add(own[i]);
            for (int i = 0; i < root.childCount; i++) CollectActiveBoxes(root.GetChild(i), into);
        }

        /// <summary>屏障类碰撞体:地图围墙与 V0.8.2 院落围栏/门柱——连续屏障,彼此贴接合法。</summary>
        public static bool IsBarrier(BoxCollider2D b)
        {
            string n = b != null ? b.name : "";
            return n == "wall" || n.StartsWith("fence");
        }

        static Vector2 WorldCenter(BoxCollider2D c)
        {
            return c.transform.TransformPoint(c.offset);
        }

        static Vector2 WorldHalf(BoxCollider2D c)
        {
            Vector3 ls = c.transform.lossyScale;
            return new Vector2(
                Mathf.Abs(c.size.x * ls.x) * .5f,
                Mathf.Abs(c.size.y * ls.y) * .5f);
        }

        static void Gap(BoxCollider2D a, BoxCollider2D b, out float gapX, out float gapY)
        {
            Vector2 ac = WorldCenter(a), bc = WorldCenter(b);
            Vector2 ah = WorldHalf(a), bh = WorldHalf(b);
            float dx = Mathf.Abs(ac.x - bc.x) - (ah.x + bh.x);
            float dy = Mathf.Abs(ac.y - bc.y) - (ah.y + bh.y);
            gapX = dx;   // ≥0 为分离，<0 为重叠
            gapY = dy;
        }

        [Test]
        public void BuildTwice_SnapshotIdentical()
        {
            mapObject = new GameObject("V081MapA");
            MapBuilder.Build(mapObject.transform);
            string first = SnapshotBuildings();

            Object.DestroyImmediate(mapObject);
            mapObject = new GameObject("V081MapB");
            MapBuilder.Build(mapObject.transform);
            string second = SnapshotBuildings();

            Assert.AreEqual(first, second, "两次 Build 的建筑快照应逐位一致（布局确定性）。");
        }

        static string SnapshotBuildings()
        {
            Transform town = MapBuilder.TownRoot;
            var lines = new List<string>();
            for (int i = 0; i < town.childCount; i++)
            {
                Transform child = town.GetChild(i);
                if (!child.name.StartsWith("B_")) continue;
                lines.Add(child.name + "@" + child.position.x.ToString("F3") + "," + child.position.y.ToString("F3"));
            }
            lines.Sort();
            StringBuilder sb = new StringBuilder();
            foreach (string line in lines) sb.Append(line).Append('\n');
            return sb.ToString();
        }

        [Test]
        public void PlacementTable_CoversAllCoreBuildings_NoDuplicateKinds()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < MapBuilder.TownPlacements.Length; i++)
            {
                string kind = MapBuilder.TownPlacements[i].kind;
                Assert.IsTrue(seen.Add(kind), "摆放表 kind 重复：" + kind);
                Assert.IsFalse(string.IsNullOrEmpty(MapBuilder.TownPlacements[i].name), kind + " 缺名称。");
                Assert.AreEqual(kind != "school", MapBuilder.TownPlacements[i].interactive,
                    "仅社区学校应作为不进入的纯场景建筑。");
            }
            Assert.AreEqual(14, seen.Count, "13 栋交互建筑与 1 栋常驻社区学校应全部入表。");
        }
    }
}

#endif
