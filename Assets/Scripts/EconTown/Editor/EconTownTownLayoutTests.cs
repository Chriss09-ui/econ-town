#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    public class EconTownTownLayoutTests
    {
        GameObject stateObject;
        GameObject mapObject;
        Transform town;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("TownLayoutState");
            GameState.I = stateObject.AddComponent<GameState>();
            mapObject = new GameObject("TownLayoutMap");
            MapBuilder.Build(mapObject.transform);
            town = mapObject.transform.Find("Town");
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (mapObject != null) UnityEngine.Object.DestroyImmediate(mapObject);
            if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void ExpandedTown_HasOnePrimaryAndOneSecondaryLandmark()
        {
            Assert.AreEqual(72, MapBuilder.W);
            Assert.AreEqual(42, MapBuilder.H);
            Assert.AreEqual(-36, MapBuilder.OriginX);
            Assert.AreEqual(35, MapBuilder.TownMaxX);

            Vector3 hall = Building("B_市政厅_hall").position;
            Vector3 centralBank = Building("B_中央银行_centralbank").position;
            BuildingSpec hallSpec = BuildingSpecs.Get("hall");
            BuildingSpec centralBankSpec = BuildingSpecs.Get("centralbank");
            Assert.AreEqual(MapBuilder.PlazaCenter.x, hall.x, .01f, "市政厅必须严格压住市政中轴。");
            Assert.Greater(hall.y, MapBuilder.PlazaCenter.y, "市政厅必须位于广场北端。");
            Assert.Greater(centralBank.x, hall.x + 20f, "央行应成为独立东部副中心。");
            Assert.Greater(hall.y, centralBank.y, "市政厅必须高于央行，形成主中心与副中心层级。");
            Assert.Greater(hallSpec.canvasW, centralBankSpec.canvasW, "市政厅必须比金融副中心更宽。");
            Assert.Greater(hallSpec.canvasH, centralBankSpec.canvasH, "市政厅必须拥有全镇最高的公共建筑画布。");

            Vector3 shop = Building("B_我的果汁店_shop").position;
            Vector3 competitor = Building("B_SUNNY TEA_competitor").position;
            Vector3 market = Building("B_原料市场_market").position;
            Vector3 factory = Building("B_河畔工厂_factory").position;
            Vector3 port = Building("B_国际贸易港_port").position;
            Assert.That(shop.x, Is.LessThan(competitor.x));
            Assert.That(competitor.x, Is.LessThan(market.x));
            Assert.That(market.x, Is.LessThan(factory.x));
            Assert.That(factory.x, Is.LessThan(port.x));
            Assert.That(new[] { shop.y, competitor.y, market.y, factory.y, port.y },
                Has.All.LessThan(-5f), "商贸产业链必须全部位于南街。");
        }

        [Test]
        public void TownBank_UsesLayeredClassicalFacade_AndLightsStayOnMasonry()
        {
            BuildingSpec spec = BuildingSpecs.Get("bank");
            Assert.AreEqual(88, spec.canvasW, "小镇银行必须维持 M 档宽度，不得与中央银行争夺副中心层级。");
            Assert.AreEqual(72, spec.canvasH, "小镇银行必须维持 M 档高度。");
            AssertDoorLine(MapBuilder.BankDoor, 1.25f, "银行");

            Texture2D facade = PixelArt.Building("bank").texture;
            Color32[] facadePixels = facade.GetPixels32();
            var opaqueColors = new HashSet<uint>();
            for (int i = 0; i < facadePixels.Length; i++)
            {
                Color32 c = facadePixels[i];
                if (c.a < 128) continue;
                opaqueColors.Add(((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a);
            }
            Assert.GreaterOrEqual(opaqueColors.Count, 16,
                "银行立面至少需要石材、阴影、玻璃、铜门、金饰等完整色阶，不能退化成单调模板。");

            Texture2D lights = PixelArt.BankFacadeLights().texture;
            Color32[] lightPixels = lights.GetPixels32();
            int lit = 0, floating = 0;
            for (int i = 0; i < lightPixels.Length; i++)
            {
                if (lightPixels[i].a == 0) continue;
                lit++;
                if (facadePixels[i].a < 128) floating++;
            }
            Assert.Greater(lit, 40, "银行落成亮化应覆盖檐口、柱头和入口灯，而不是几个孤立亮点。");
            Assert.AreEqual(0, floating, "银行亮化像素必须落在实体立面上，不能悬浮在透明区域。");
        }

        [Test]
        public void ApprovedCentersAndFrontageLines_AreStable()
        {
            AssertCenter("hall", -.5f, 15.52f);
            AssertCenter("training", -29f, 13.5f);
            AssertCenter("school", -6.75f, -16.29f);
            AssertCenter("home", -16f, 14.1f);
            AssertCenter("factory", 10f, -6.5f);
            AssertCenter("port", 31f, -6.61f);
            AssertCenter("centralbank", 26f, 14.7f);
            AssertBuildingCenter("bookstore", 11.6f, 14.24f);
            AssertBuildingCenter("empty_a", 16f, 13.75f);
            AssertBuildingCenter("empty_b", 20f, -8.5f);

            AssertDoorLine(MapBuilder.ConvenienceDoor, 1.25f, "便利屋");
            AssertDoorLine(MapBuilder.DecorADoor, 1.25f, "零嘴亭");
            AssertDoorLine(MapBuilder.RestaurantDoor, 1.25f, "餐厅");
            AssertDoorLine(MapBuilder.CinemaDoor, 1.25f, "电影院");
            AssertDoorLine(MapBuilder.BankDoor, 1.25f, "银行");
            AssertDoorLine(MapBuilder.DecorBDoor, 1.25f, "花店");
            AssertDoorLine(MapBuilder.ShopDoor, -10.75f, "果汁店");
            AssertDoorLine(MapBuilder.CompetitorDoor, -10.75f, "茶馆");
            AssertDoorLine(MapBuilder.MarketDoor, -10.75f, "市场");
            AssertDoorLine(MapBuilder.PortDoor, -9.35f, "港口");
            AssertDoorLine(MapBuilder.BookstoreDoor, 11.5f, "书店");
        }

        [Test]
        public void CommunitySchool_IsAVisibleButNonEnterableCampusBuilding()
        {
            Transform school = Building("B_社区学校_school");
            BuildingIdentity identity = school.GetComponent<BuildingIdentity>();
            Assert.IsNotNull(identity, "社区学校必须保留建筑身份，供四季外观与布局系统识别。");
            Assert.AreEqual("school", identity.kind);
            Assert.IsFalse(identity.interactive, "社区学校按需求只作场景建筑，不开放点击进入。");
            Assert.IsNull(school.GetComponent<BuildingClick>(), "纯场景学校不应生成建筑点击入口。");

            BoxCollider2D[] colliders = school.GetComponents<BoxCollider2D>();
            Assert.AreEqual(1, colliders.Length, "学校只保留行走碰撞体，不生成整楼点击触发体。");
            Assert.IsFalse(colliders[0].isTrigger);
            Assert.AreEqual(-19.35f, school.position.y + BuildingSpecs.Get("school").doorOffset.y, .01f,
                "学校正门应落在南部独立校园前庭上。");
        }

        [Test]
        public void ParcelRegistry_DoesNotOverlap_AndContainsEveryBuildingArtwork()
        {
            Type layout = typeof(MapBuilder).Assembly.GetType("EconTown.TownLayoutData");
            Assert.IsNotNull(layout, "缺少地块布局数据源。");
            Array parcels = (Array)layout.GetField("Parcels", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Array slots = (Array)layout.GetField("BuildingSlots", BindingFlags.Public | BindingFlags.Static).GetValue(null);

            var boundsById = new Dictionary<string, Rect>();
            var frontageById = new Dictionary<string, string>();
            for (int i = 0; i < parcels.Length; i++)
            {
                object parcel = parcels.GetValue(i);
                Type type = parcel.GetType();
                string id = (string)type.GetField("id").GetValue(parcel);
                Rect bounds = (Rect)type.GetField("bounds").GetValue(parcel);
                boundsById.Add(id, bounds);
                frontageById.Add(id, type.GetField("frontage").GetValue(parcel).ToString());
                Assert.GreaterOrEqual((float)type.GetField("setback").GetValue(parcel), 0f,
                    "地块退界不得为负数：" + id);
                for (int j = 0; j < i; j++)
                {
                    object other = parcels.GetValue(j);
                    Rect otherBounds = (Rect)other.GetType().GetField("bounds").GetValue(other);
                    string otherId = (string)other.GetType().GetField("id").GetValue(other);
                    Assert.IsFalse(bounds.Overlaps(otherBounds), "法定地块重叠：" + id + " / " + otherId);
                }
            }

            Assert.AreEqual(MapBuilder.TownMaxX, boundsById["port"].xMax, .01f,
                "港口地块必须贴住小镇东界，形成真正的边缘贸易节点。");
            Assert.Less(boundsById["empty_b"].xMax, boundsById["port"].xMin,
                "空铺 B 应作为工厂与边缘港口之间的过渡地块。");
            Assert.Greater(MapBuilder.PortDoor.x, 30f - .01f,
                "港口入口应位于东南边缘，而不是产业街中段。");

            var generatedKinds = new HashSet<string>();
            SpriteRenderer[] renderers = town.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                BuildingIdentity identity = renderers[i].GetComponent<BuildingIdentity>();
                if (identity != null && renderers[i].name.StartsWith("B_")) generatedKinds.Add(identity.kind);
            }

            for (int i = 0; i < slots.Length; i++)
            {
                object slot = slots.GetValue(i);
                Type type = slot.GetType();
                string kind = (string)type.GetField("kind").GetValue(slot);
                string parcelId = (string)type.GetField("parcelId").GetValue(slot);
                Vector3 center = (Vector3)type.GetField("center").GetValue(slot);
                bool usesDoorLine = (bool)type.GetField("usesDoorLine").GetValue(slot);
                Assert.IsTrue(generatedKinds.Contains(kind), "地块内未生成登记建筑：" + kind);

                Rect parcelBounds = boundsById[parcelId];
                BuildingSpec spec = BuildingSpecs.Get(kind);
                Assert.GreaterOrEqual(center.x - spec.WorldHalfWidth, parcelBounds.xMin - .05f, kind + " 超出地块西界。");
                Assert.LessOrEqual(center.x + spec.WorldHalfWidth, parcelBounds.xMax + .05f, kind + " 超出地块东界。");
                Assert.GreaterOrEqual(center.y - spec.WorldHalfHeight, parcelBounds.yMin - .3f, kind + " 超出地块南界。");
                Assert.LessOrEqual(center.y + spec.WorldHalfHeight, parcelBounds.yMax + .05f, kind + " 超出地块北界。");
                if (usesDoorLine)
                {
                    float doorTargetY = (float)type.GetField("doorTargetY").GetValue(slot);
                    Assert.AreNotEqual("None", frontageById[parcelId], kind + " 使用门线却未登记临街方向。");
                    Assert.AreEqual(doorTargetY, center.y + spec.doorOffset.y, .01f,
                        kind + " 中心没有由登记门线反算。");
                }
            }
            Assert.AreEqual(19, generatedKinds.Count, "新增社区学校后，19 栋主城建筑必须全部保留。");
        }

        [Test]
        public void LandmarkCompounds_KeepThreeTileForecourtsAndSideClearance()
        {
            AssertLandmarkSetback("hall", new Rect(-8f, 8f, 15f, 12.8f));
            AssertLandmarkSetback("centralbank", new Rect(19f, 8f, 15f, 11.5f));
        }

        [Test]
        public void CivicPlaza_UsesZonedFurnitureAndASolidFountainFootprint()
        {
            Transform fountain = town.Find("plaza_fountain");
            Transform notice = town.Find("plaza_notice");
            Transform benchA = town.Find("plaza_bench_a");
            Transform benchB = town.Find("plaza_bench_b");
            Assert.IsNotNull(fountain);
            Assert.IsNotNull(notice);
            Assert.IsNotNull(benchA);
            Assert.IsNotNull(benchB);

            SpriteRenderer fountainRenderer = fountain.GetComponent<SpriteRenderer>();
            Assert.GreaterOrEqual(fountainRenderer.sprite.texture.width, 56, "市政喷泉必须使用广场级剪影，不能退回街景小道具。");
            Assert.GreaterOrEqual(fountainRenderer.sprite.texture.height, 58);
            Assert.AreEqual(MapBuilder.PlazaCenter.x, fountain.position.x, .01f, "喷泉必须压住市政中轴。");
            Assert.AreEqual(MapBuilder.PlazaCenter.y + .35f, fountain.position.y, .01f);

            BoxCollider2D footprint = fountain.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(footprint, "喷泉不能只有画面没有实体脚印。");
            Assert.AreEqual(new Vector2(3.25f, .8f), footprint.size);
            Assert.AreEqual(new Vector2(0f, -1.25f), footprint.offset);
            Assert.AreEqual(MapBuilder.SortingForY(fountain.position.y - 1.25f),
                fountainRenderer.sortingOrder, "喷泉遮挡顺序应以水盆底部而非贴图中心计算。");

            Assert.Less(benchA.position.x, fountain.position.x - 3f, "信息区座椅应收在西侧。");
            Assert.Greater(benchB.position.x, fountain.position.x + 3f, "东侧座椅应让出中轴。");
            Assert.Less(benchB.position.y, fountain.position.y - 1.5f, "东侧座椅应收在东南休息区，让出东北仪典区。");
            AssertNoSpriteOverlap(fountain, notice, "喷泉 / 公告牌");
            AssertNoSpriteOverlap(fountain, benchA, "喷泉 / 西座椅");
            AssertNoSpriteOverlap(fountain, benchB, "喷泉 / 东南座椅");
        }

        [Test]
        public void CivicPlaza_ConstructionAndCompletedProps_NeverCollideWithPermanentCore()
        {
            Transform fountain = town.Find("plaza_fountain");
            Transform notice = town.Find("plaza_notice");
            Transform benchA = town.Find("plaza_bench_a");
            Transform benchB = town.Find("plaza_bench_b");
            Transform growth = town.Find("growth_plaza");
            Assert.IsNotNull(growth);

            GameState.I.progression.completedChapter = 16;
            MapBuilder.RefreshGrowthDistricts(GameState.I);
            Transform site = growth.Find("construction_site");
            Assert.IsTrue(site.gameObject.activeSelf, "第 16 章应显示市政广场施工区。");
            AssertVisibleSpritesClear(site, new[] { fountain, notice, benchA, benchB }, "施工区");
            // 围猎五(#5):Collider2D.bounds 在 EditMode 未经物理步进退化为零尺寸点
            // (V0911InteriorTests:322 实测),bounds.Intersects 恒 false 使 IsFalse 断言空转——
            // 照 V081 LandmarkTests 先例手算世界 AABB(TransformPoint + lossyScale)。
            Assert.IsFalse(BoxesIntersect(site.GetComponent<BoxCollider2D>(),
                fountain.GetComponent<BoxCollider2D>()), "施工围挡实体不得横切喷泉水盆。");

            GameState.I.progression.completedChapter = 22;
            MapBuilder.RefreshGrowthDistricts(GameState.I);
            Transform completed = growth.Find("completed");
            Assert.IsFalse(site.gameObject.activeSelf, "广场落成后应拆除施工围挡。");
            Assert.IsTrue(completed.gameObject.activeSelf);
            AssertVisibleSpritesClear(completed, new[] { fountain, notice, benchA, benchB }, "落成仪典区");
        }

        [Test]
        public void VehicleRoads_AreLimitedToTwoStreetsAndOneAxis()
        {
            Tilemap ground = town.Find("Ground").GetComponent<Tilemap>();
            int mainTiles = 0, tradeTiles = 0, axisTiles = 0;
            for (int x = MapBuilder.OriginX; x <= MapBuilder.TownMaxX; x++)
                for (int y = -MapBuilder.H / 2; y < MapBuilder.H / 2; y++)
                {
                    if (!IsVehicleRoad(ground.GetSprite(new Vector3Int(x, y, 0)))) continue;
                    bool main = y == 0 || y == -1;
                    bool trade = y == -12 || y == -13;
                    bool axis = (x == -1 || x == 0) && y >= -11 && y <= 1;
                    Assert.IsTrue(main || trade || axis, "机动车路越出双街一轴：" + new Vector2Int(x, y));
                    if (main) mainTiles++;
                    if (trade) tradeTiles++;
                    if (axis) axisTiles++;
                }
            Assert.GreaterOrEqual(mainTiles, MapBuilder.W * 2);
            Assert.GreaterOrEqual(tradeTiles, MapBuilder.W * 2);
            Assert.GreaterOrEqual(axisTiles, 22);
        }

        [Test]
        public void EveryInteractiveDoor_ReachesThePlazaOnOnePavedNetwork()
        {
            Tilemap ground = town.Find("Ground").GetComponent<Tilemap>();
            var paved = new HashSet<Vector3Int>();
            for (int x = MapBuilder.OriginX; x <= MapBuilder.TownMaxX; x++)
                for (int y = -MapBuilder.H / 2; y < MapBuilder.H / 2; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (IsPaved(ground.GetSprite(cell))) paved.Add(cell);
                }

            Vector3Int start = Vector3Int.FloorToInt(MapBuilder.PlazaCenter);
            var visited = new HashSet<Vector3Int> { start };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3Int next = cell + directions[i];
                    if (paved.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }

            Vector3[] doors = InteractiveDoors();
            for (int i = 0; i < doors.Length; i++)
            {
                Vector3Int cell = Vector3Int.FloorToInt(doors[i]);
                Assert.IsTrue(paved.Contains(cell), "门口未落在步行面：" + doors[i]);
                Assert.IsTrue(visited.Contains(cell), "门口不能抵达市政广场：" + doors[i]);
            }
            Assert.AreEqual(paved.Count, visited.Count, "道路或步行铺装存在孤岛。");
        }

        [Test]
        public void NpcActivityAnchors_StayOnPublicPavingAndOutsideSolidBarriers()
        {
            Tilemap ground = town.Find("Ground").GetComponent<Tilemap>();
            Type layout = typeof(MapBuilder).Assembly.GetType("EconTown.TownLayoutData");
            Assert.IsNotNull(layout, "缺少主城布局数据源。");
            Vector3[] initial = (Vector3[])layout.GetField("InitialNpcSpawns",
                BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Vector3[] activity = (Vector3[])layout.GetField("StreetActivityPoints",
                BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.AreEqual(12, initial.Length, "开局 12 名 NPC 必须各有登记出生点。");

            for (int i = 0; i < initial.Length; i++)
                AssertPublicNpcPoint(ground, initial[i], "出生点 " + i);
            for (int i = 0; i < activity.Length; i++)
                AssertPublicNpcPoint(ground, activity[i], "活动点 " + i);
        }

        static Vector3[] InteractiveDoors()
        {
            return new[]
            {
                MapBuilder.ShopDoor, MapBuilder.CompetitorDoor, MapBuilder.MarketDoor, MapBuilder.FactoryDoor,
                MapBuilder.PortDoor, MapBuilder.HallDoor, MapBuilder.TrainingDoor, MapBuilder.ConvenienceDoor,
                MapBuilder.HomeDoor, MapBuilder.RestaurantDoor, MapBuilder.CinemaDoor, MapBuilder.BankDoor,
                MapBuilder.CentralBankDoor, MapBuilder.DecorADoor, MapBuilder.DecorBDoor, MapBuilder.BookstoreDoor
            };
        }

        static bool IsPaved(Sprite sprite)
        {
            return IsVehicleRoad(sprite) || sprite == PixelArt.Plaza(0) || sprite == PixelArt.Plaza(1) ||
                   sprite == PixelArt.ForecourtStoneTile();
        }

        static bool IsVehicleRoad(Sprite sprite)
        {
            return sprite == PixelArt.Road(0) || sprite == PixelArt.Road(1) || sprite == PixelArt.Road(2);
        }

        static void AssertPublicNpcPoint(Tilemap ground, Vector3 point, string label)
        {
            Assert.That(point.x, Is.InRange(MapBuilder.WorldMinX, MapBuilder.WorldMaxX), label + " 越出东西边界。");
            Assert.That(point.y, Is.InRange(MapBuilder.WorldMinY, MapBuilder.WorldMaxY), label + " 越出南北边界。");
            Assert.IsTrue(IsPaved(ground.GetSprite(Vector3Int.FloorToInt(point))), label + " 未落在公共铺装上：" + point);
            Collider2D[] hits = Physics2D.OverlapPointAll(point);
            for (int i = 0; i < hits.Length; i++)
                Assert.IsTrue(hits[i].isTrigger, label + " 被实体阻挡：" + hits[i].name + " @ " + point);
        }

        Transform Building(string name)
        {
            Transform found = town.Find(name);
            Assert.IsNotNull(found, "缺少建筑：" + name);
            return found;
        }

        static void AssertDoorLine(Vector3 door, float y, string label)
        {
            Assert.AreEqual(y, door.y, .01f, label + " 没有对齐统一沿街线。");
        }

        static void AssertVisibleSpritesClear(Transform group, Transform[] permanent, string label)
        {
            SpriteRenderer[] sprites = group.GetComponentsInChildren<SpriteRenderer>(false);
            Assert.Greater(sprites.Length, 0, label + " 没有可见内容。");
            for (int i = 0; i < sprites.Length; i++)
                for (int j = 0; j < permanent.Length; j++)
                    Assert.IsFalse(sprites[i].bounds.Intersects(permanent[j].GetComponent<SpriteRenderer>().bounds),
                        label + "中的 " + sprites[i].name + " 与 " + permanent[j].name + " 画面重叠。");
        }

        static void AssertNoSpriteOverlap(Transform a, Transform b, string label)
        {
            Assert.IsFalse(a.GetComponent<SpriteRenderer>().bounds.Intersects(b.GetComponent<SpriteRenderer>().bounds),
                label + " 画面重叠。");
        }

        // 围猎五(#5):碰撞体相交判定必须手算世界 AABB——Collider2D.bounds 在 EditMode 未经
        // 物理步进退化为零尺寸点(V0911InteriorTests:322 实测),bounds.Intersects 恒 false。
        // 照 V081 LandmarkTests 的 WorldCenter/WorldHalf 同款口径(TransformPoint + lossyScale)。
        static bool BoxesIntersect(BoxCollider2D a, BoxCollider2D b)
        {
            Vector2 ca = a.transform.TransformPoint(a.offset), cb = b.transform.TransformPoint(b.offset);
            Vector2 ha = new Vector2(Mathf.Abs(a.size.x * a.transform.lossyScale.x) * .5f,
                Mathf.Abs(a.size.y * a.transform.lossyScale.y) * .5f);
            Vector2 hb = new Vector2(Mathf.Abs(b.size.x * b.transform.lossyScale.x) * .5f,
                Mathf.Abs(b.size.y * b.transform.lossyScale.y) * .5f);
            return Mathf.Abs(ca.x - cb.x) < ha.x + hb.x && Mathf.Abs(ca.y - cb.y) < ha.y + hb.y;
        }

        static void AssertCenter(string kind, float x, float y)
        {
            for (int i = 0; i < MapBuilder.TownPlacements.Length; i++)
            {
                MapBuilder.BuildingPlacement placement = MapBuilder.TownPlacements[i];
                if (placement.kind != kind) continue;
                Assert.That(Vector3.Distance(new Vector3(x, y), placement.center), Is.LessThan(.01f),
                    kind + " 坐标偏离审定布局。");
                return;
            }
            Assert.Fail("TownPlacements 未登记建筑：" + kind);
        }

        void AssertBuildingCenter(string kind, float x, float y)
        {
            Transform[] all = town.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (!child.name.EndsWith("_" + kind, StringComparison.Ordinal)) continue;
                Assert.That(Vector3.Distance(new Vector3(x, y), child.position), Is.LessThan(.01f),
                    kind + " 坐标偏离审定布局。");
                return;
            }
            Assert.Fail("主城中未生成建筑：" + kind);
        }

        void AssertLandmarkSetback(string kind, Rect parcel)
        {
            Transform building = null;
            Transform[] all = town.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.EndsWith("_" + kind, StringComparison.Ordinal)) { building = all[i]; break; }
            Assert.IsNotNull(building, "缺少地标建筑：" + kind);
            BuildingSpec spec = BuildingSpecs.Get(kind);
            float west = building.position.x - spec.WorldHalfWidth - parcel.xMin;
            float east = parcel.xMax - building.position.x - spec.WorldHalfWidth;
            float front = building.position.y - spec.WorldHalfHeight - parcel.yMin;
            float back = parcel.yMax - building.position.y - spec.WorldHalfHeight;
            Assert.GreaterOrEqual(front, 3f, kind + " 正面净空不足三格。");
            Assert.GreaterOrEqual(Mathf.Min(west, east, back), 1.2f, kind + " 侧后退界不足 1.2 格。");
        }
    }
}

#endif
