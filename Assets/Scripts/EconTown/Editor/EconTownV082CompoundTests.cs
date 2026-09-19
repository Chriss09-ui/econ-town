#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    /// <summary>地块化院落护栏：七处需要围合的用地必须四面闭合，且院门到建筑门点全程净空。</summary>
    public class EconTownV082CompoundTests
    {
        GameObject stateObject;
        GameObject mapObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V082State");
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
        public void SevenCompounds_AreClosedExceptForTheirDeclaredGate()
        {
            Build();
            string[] southGateParcels = { "hall", "centralbank", "training", "home", "factory" };
            for (int i = 0; i < southGateParcels.Length; i++)
                AssertClosed(southGateParcels[i], "south", "north");
            AssertClosed("school", "north", "south");
            AssertClosed("park", "north", "south");

            List<BoxCollider2D> fences = new List<BoxCollider2D>();
            CollectFences(MapBuilder.TownRoot, fences);
            Assert.GreaterOrEqual(fences.Count, 70, "七处完整院落应至少生成 70 根围栏/门柱碰撞体。");
            for (int i = 0; i < fences.Count; i++)
                Assert.IsTrue(EconTownV081LandmarkTests.IsBarrier(fences[i]), "院界必须归入屏障类：" + fences[i].name);

            string[] forbidden = { "fence_plot_", "fence_divider_", "fence_finance_belt" };
            for (int i = 0; i < forbidden.Length; i++)
                Assert.IsNull(FindPrefix(MapBuilder.TownRoot, forbidden[i]), "不得留下孤立半截围栏：" + forbidden[i]);
        }

        [Test]
        public void CompoundGates_AndDoorCorridors_AreClear()
        {
            Build();
            Physics2D.SyncTransforms();
            AssertGateAndCorridor(new Vector2(-.5f, 8f), 5f, MapBuilder.HallDoor, "市政厅");
            AssertGateAndCorridor(new Vector2(26f, 8f), 4f, MapBuilder.CentralBankDoor, "中央银行");
            Vector2 campusGate = new Vector2(-29f, 8.5f);
            AssertGateAndCorridor(campusGate, 3.5f, MapBuilder.TrainingDoor, "培训中心");
            Transform school = MapBuilder.TownRoot.Find("B_社区学校_school");
            Vector3 schoolDoor = school.position + BuildingSpecs.Get("school").doorOffset;
            AssertNorthGateAndCorridor(new Vector2(-3.5f, -13f), 3f,
                new Vector2(-3.5f, schoolDoor.y), schoolDoor, "社区学校");
            AssertGateAndCorridor(new Vector2(-16f, 9f), 2.5f, MapBuilder.HomeDoor, "住宅");
            AssertGateAndCorridor(new Vector2(10f, -10.5f), 4f, MapBuilder.FactoryDoor, "工厂");
            AssertGateClear(new Vector2(-25f, -15f), 2.5f, "口袋公园");
        }

        [Test]
        public void LandmarkCourts_HaveDistinctPavingAndFurniture()
        {
            Build();
            Tilemap ground = MapBuilder.TownRoot.Find("Ground").GetComponent<Tilemap>();
            Assert.AreEqual(PixelArt.ForecourtStoneTile(), ground.GetSprite(new Vector3Int(-1, 10, 0)), "市政前庭应整片铺官家石砖。");
            Assert.AreEqual(PixelArt.ForecourtStoneTile(), ground.GetSprite(new Vector3Int(25, 10, 0)), "央行前庭应有独立规则石铺。");
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "hall_court_planter_w"));
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "hall_court_planter_e"));
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "centralbank_court_lamp_w"));
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "centralbank_court_lamp_e"));
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "training_bench"), "扩图后校园必须恢复真实院内活动空间。");
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "factory_pallets"));
            Assert.IsNotNull(Find(MapBuilder.TownRoot, "factory_drums"));
        }

        [Test]
        public void FenceBudget_AndBuildOutput_AreDeterministic()
        {
            Build();
            int count = 0;
            CountFenceSprites(MapBuilder.TownRoot, ref count);
            Assert.That(count, Is.InRange(110, 170), "完整七院落应保持有限且可读的围栏预算。");
            string first = FenceSnapshot();

            Object.DestroyImmediate(mapObject);
            mapObject = null;
            Build();
            Assert.AreEqual(first, FenceSnapshot(), "两次 Build 的院界必须逐位一致。");
        }

        void Build()
        {
            mapObject = new GameObject("V082Map");
            MapBuilder.Build(mapObject.transform);
        }

        static void AssertClosed(string id, string gateSide, string oppositeSide)
        {
            string[] names =
            {
                "fence_" + id + "_" + gateSide + "_w",
                "fence_" + id + "_" + gateSide + "_e",
                "fence_" + id + "_" + oppositeSide,
                "fence_" + id + "_west",
                "fence_" + id + "_east",
                "fence_pier_" + id + "_gate_w",
                "fence_pier_" + id + "_gate_e"
            };
            for (int i = 0; i < names.Length; i++)
                Assert.IsNotNull(Find(MapBuilder.TownRoot, names[i]), "闭合院界缺失：" + names[i]);
        }

        static void AssertGateAndCorridor(Vector2 gate, float width, Vector3 door, string label)
        {
            AssertGateClear(gate, width, label);
            Vector2 from = gate + Vector2.up * .65f;
            AssertCorridorClear(from, door, label);
        }

        static void AssertNorthGateAndCorridor(Vector2 gate, float width, Vector2 turn, Vector3 door, string label)
        {
            Collider2D hit = Physics2D.OverlapBox(gate + Vector2.down * .25f,
                new Vector2(width - .2f, .35f), 0f);
            Assert.IsNull(hit, label + " 北向院门实体净宽不足：" + (hit != null ? hit.name : ""));
            AssertCorridorClear(gate + Vector2.down * .65f, turn, label);
            AssertCorridorClear(turn, door, label);
        }

        static void AssertCorridorClear(Vector2 from, Vector2 to, string label)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .25f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                Collider2D hit = Physics2D.OverlapBox(point, new Vector2(.6f, .6f), 0f);
                Assert.IsNull(hit, label + " 院门到门点的走廊被阻挡：" + (hit != null ? hit.name : ""));
            }
        }

        static void AssertGateClear(Vector2 gate, float width, string label)
        {
            Collider2D hit = Physics2D.OverlapBox(gate + Vector2.up * .25f, new Vector2(width - .2f, .35f), 0f);
            Assert.IsNull(hit, label + " 院门实体净宽不足：" + (hit != null ? hit.name : ""));
        }

        static void CollectFences(Transform root, List<BoxCollider2D> into)
        {
            BoxCollider2D own = root.GetComponent<BoxCollider2D>();
            if (own != null && root.name.StartsWith("fence")) into.Add(own);
            for (int i = 0; i < root.childCount; i++) CollectFences(root.GetChild(i), into);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindPrefix(Transform root, string prefix)
        {
            if (root.name.StartsWith(prefix)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindPrefix(root.GetChild(i), prefix);
                if (found != null) return found;
            }
            return null;
        }

        static void CountFenceSprites(Transform root, ref int count)
        {
            if (root.name.StartsWith("fence"))
            {
                count += root.GetComponentsInChildren<SpriteRenderer>(true).Length;
                return;
            }
            for (int i = 0; i < root.childCount; i++) CountFenceSprites(root.GetChild(i), ref count);
        }

        static string FenceSnapshot()
        {
            var lines = new List<string>();
            SnapFences(MapBuilder.TownRoot, lines);
            lines.Sort();
            var text = new StringBuilder();
            for (int i = 0; i < lines.Count; i++) text.Append(lines[i]).Append('\n');
            return text.ToString();
        }

        static void SnapFences(Transform root, List<string> lines)
        {
            if (root.name.StartsWith("fence"))
            {
                lines.Add(root.name + "@" + root.position.x.ToString("F3") + "," + root.position.y.ToString("F3") +
                          ":" + root.GetComponentsInChildren<SpriteRenderer>(true).Length);
                return;
            }
            for (int i = 0; i < root.childCount; i++) SnapFences(root.GetChild(i), lines);
        }
    }
}

#endif
