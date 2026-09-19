#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.8.3《公共资本看得见》:五项目预留/建成双态结构测试(PRD §178)。
    /// 全部只读 government.projects[].built 注入,零新状态、零新交互点、零新存档字段。</summary>
    public class EconTownV083PublicProjectsTests
    {
        GameObject stateObject;
        GameObject mapObject;
        Transform town;
        Transform projects;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V083State");
            GameState.I = stateObject.AddComponent<GameState>();
            mapObject = new GameObject("V083Map");
            MapBuilder.Build(mapObject.transform);
            town = mapObject.transform.Find("Town");
            projects = FindProjects(mapObject.transform);
            Assert.IsNotNull(projects, "PublicProjects 根应随 Build 创建。");
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (mapObject != null) Object.DestroyImmediate(mapObject);
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void ReservedState_DefaultMap_ShowsSignsNotStructures()
        {
            Assert.IsNotNull(projects.Find("public_light_west"), "灯杆应常驻(素头)。");
            Assert.IsNotNull(projects.Find("public_light_east"), "灯杆应常驻(素头)。");
            Assert.AreEqual(0, CountGlowRenderers(), "未拨款的路灯不应挂夜辉。");

            Assert.IsNotNull(projects.Find("park_reserved_sign"), "公园预留态应有「预留绿地」木牌。");
            Assert.IsNull(projects.Find("park_perimeter"), "预留态不应出现矮篱周界。");

            Assert.IsNotNull(projects.Find("public_bus_stop"), "公交站预留态=告示板(现状)。");
            Assert.IsNull(projects.Find("public_bus_shelter"), "预留态不应有候车亭。");

            Assert.IsNotNull(projects.Find("school_public_notice"), "学校预留态=木牌(现状)。");
            Assert.IsNull(projects.Find("school_annex"), "预留态不应有扩建层。");

            Assert.IsNotNull(projects.Find("green_facility_a"), "环保设施预留态=木箱(现状)。");
            Assert.IsNull(projects.Find("green_purifier"), "预留态不应有净化塔。");
        }

        [Test]
        public void StreetLights_Built_AttachesNightGlows_ToPublicPostsAndStreetLamps()
        {
            SetBuilt(PublicProjectType.StreetLights);
            MapBuilder.RefreshGovernmentProjects(GameState.I);

            Assert.IsNotNull(projects.Find("public_light_west").Find("head_glow"), "公共灯杆应挂夜辉。");
            Assert.IsNotNull(projects.Find("public_light_east").Find("head_glow"), "公共灯杆应挂夜辉。");
            string[] streetLamps = { "west_lamp", "east_lamp", "neighborhood_lamp", "trade_lamp" };
            for (int i = 0; i < streetLamps.Length; i++)
            {
                Transform lamp = town.Find(streetLamps[i]);
                Assert.IsNotNull(lamp, "主城缺少既有街灯 " + streetLamps[i] + "。");
                Assert.IsNotNull(lamp.Find("head_glow"), "既有街灯 " + streetLamps[i] + " 应挂夜辉(§176)。");
            }
            Assert.AreEqual(6, CountGlowRenderers(), "夜辉=2 公共灯杆+4 既有街灯。");

            MapBuilder.ApplyPublicLightAlpha(.8f);
            Assert.Greater(FindFirstGlow().color.a, .7f, "夜幕 α 应作用于辉光。");
            MapBuilder.ApplyPublicLightAlpha(0f);
            Assert.LessOrEqual(FindFirstGlow().color.a, .01f, "白天 α 应归零。");

            SetBuilt(PublicProjectType.StreetLights, false);
            MapBuilder.RefreshGovernmentProjects(GameState.I);
            Assert.AreEqual(0, CountGlowRenderers(), "撤拨款后夜辉应随重建消失。");
        }

        [Test]
        public void Park_Built_PerimeterPathBenches_ReservedSignRemoved()
        {
            SetBuilt(PublicProjectType.Park);
            MapBuilder.RefreshGovernmentProjects(GameState.I);

            Assert.IsNotNull(projects.Find("park_perimeter"), "建成应有矮篱周界。");
            Assert.IsNotNull(projects.Find("park_path_0"), "建成应有花砖小径。");
            Assert.IsNotNull(projects.Find("public_park_bench_a"), "建成应有园内长椅。");
            Assert.IsNotNull(projects.Find("park_tree_a"), "建成花木保留。");
            Assert.IsNull(projects.Find("park_reserved_sign"), "建成后预留木牌应移除。");
        }

        [Test]
        public void BusStop_Built_ShelterHasCollider_AndKeepsClearOfDoors()
        {
            GameObject board = projects.Find("public_bus_stop").gameObject;
            Assert.IsNull(board.GetComponent<Collider2D>(), "预留告示板不带碰撞体。");

            SetBuilt(PublicProjectType.BusStop);
            MapBuilder.RefreshGovernmentProjects(GameState.I);

            Transform shelter = projects.Find("public_bus_shelter");
            Assert.IsNotNull(shelter, "建成应有候车亭。");
            Assert.IsNotNull(shelter.GetComponent<BoxCollider2D>(), "候车亭应带碰撞体入 LayoutGuard 口径。");
            Assert.IsNull(projects.Find("public_bus_stop"), "告示板应随建成移除。");

            Vector3[] doors = {
                MapBuilder.ShopDoor, MapBuilder.CompetitorDoor, MapBuilder.MarketDoor, MapBuilder.FactoryDoor,
                MapBuilder.PortDoor, MapBuilder.HallDoor, MapBuilder.TrainingDoor, MapBuilder.ConvenienceDoor,
                MapBuilder.HomeDoor, MapBuilder.RestaurantDoor, MapBuilder.CinemaDoor, MapBuilder.BankDoor,
                MapBuilder.CentralBankDoor
            };
            for (int i = 0; i < doors.Length; i++)
                Assert.Greater(Vector2.Distance(shelter.position, doors[i]), 1.5f,
                    "候车亭不得堵住建筑入口: " + doors[i]);
            Assert.That(shelter.position.x, Is.InRange(-23f, 23f), "候车亭应在图内。");
            Assert.That(shelter.position.y, Is.InRange(-8f, 12f), "候车亭应在图内。");
        }

        [Test]
        public void School_Built_AnnexIntegratesWithCommunitySchool_WithoutBecomingTownLandmark()
        {
            Transform school = town.Find("B_社区学校_school");
            BuildingSpec spec = BuildingSpecs.Get("school");
            Vector2 colliderBefore = school.GetComponent<BoxCollider2D>().size;
            int buildingCountBefore = CountBuildings();

            SetBuilt(PublicProjectType.School);
            MapBuilder.RefreshGovernmentProjects(GameState.I);

            Transform annex = projects.Find("school_annex");
            Assert.IsNotNull(annex, "建成应有扩建层。");
            Assert.IsNull(projects.Find("school_public_notice"), "木牌应随建成移除。");

            float schoolTop = school.position.y + spec.canvasH / 16f * spec.scale * .5f;
            float annexHalfHeight = annex.GetComponent<SpriteRenderer>().sprite.texture.height / 16f * spec.scale * .5f;
            float annexBottom = annex.position.y - annexHalfHeight;
            float annexTop = annex.position.y + annexHalfHeight;
            Assert.Less(annexBottom, schoolTop, "扩建屋冠应内嵌原校舍屋顶，而非叠成第二座高塔。");
            Assert.AreEqual(school.position.x, annex.position.x, .01f, "扩建应与社区学校同轴(同地块)。");
            Assert.AreEqual(colliderBefore, school.GetComponent<BoxCollider2D>().size,
                "扩建不得改变主楼碰撞体(footprint 不变)。");
            Assert.AreEqual(buildingCountBefore, CountBuildings(), "不应新增 B_ 建筑(overlay 方案)。");
            Assert.Greater(annexTop, schoolTop,
                "扩建层应向上生长。");
            Transform hall = town.Find("B_市政厅_hall");
            float hallTop = hall.position.y + BuildingSpecs.Get("hall").WorldHalfHeight;
            Assert.Less(annexTop, hallTop, "学校扩建不得越过市政厅的主地标天际线。");
        }

        [Test]
        public void GreenFacility_Built_TowerHasCollider_CratesRemoved()
        {
            SetBuilt(PublicProjectType.GreenFacility);
            MapBuilder.RefreshGovernmentProjects(GameState.I);

            Transform tower = projects.Find("green_purifier");
            Assert.IsNotNull(tower, "建成应有净化塔。");
            Assert.IsNotNull(tower.GetComponent<BoxCollider2D>(), "净化塔应带小碰撞体。");
            Assert.IsNull(projects.Find("green_facility_a"), "预留木箱应随建成移除。");
            Assert.That(tower.position.x, Is.InRange(-23f, 23f), "净化塔应在图内。");
            Assert.That(tower.position.y, Is.InRange(-8f, 12f), "净化塔应在图内。");
            Vector3 factory = town.Find("B_河畔工厂_factory").position;
            Assert.Greater(Vector2.Distance(tower.position, factory), 1.2f, "净化塔与工厂保持净距。");
        }

        [Test]
        public void AllBuilt_AtOnce_RefreshTwice_IsStable()
        {
            SetBuilt(PublicProjectType.StreetLights);
            SetBuilt(PublicProjectType.Park);
            SetBuilt(PublicProjectType.BusStop);
            SetBuilt(PublicProjectType.School);
            SetBuilt(PublicProjectType.GreenFacility);
            MapBuilder.RefreshGovernmentProjects(GameState.I);
            int childrenAfterFirst = projects.childCount;
            int glowsAfterFirst = CountGlowRenderers();

            MapBuilder.RefreshGovernmentProjects(GameState.I);
            Assert.AreEqual(childrenAfterFirst, projects.childCount, "重复刷新应幂等(销毁重建同构)。");
            Assert.AreEqual(glowsAfterFirst, CountGlowRenderers(), "重复刷新夜辉数量应稳定。");
            Assert.AreEqual(CountBuildings(), 16, "全建成也不应新增额外 B_ 建筑。");
        }

        [Test]
        public void SaveSchema_V083_ZeroNewProjectFields_BeginnerTipsRemoved()
        {
            SetBuilt(PublicProjectType.Park);
            MapBuilder.RefreshGovernmentProjects(GameState.I);
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "v083_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(tempDir);
            SaveSystem.OverrideSaveDirectory(tempDir);
            try
            {
                SaveSystem.Save(GameState.I);
                string json = System.IO.File.ReadAllText(System.IO.Path.Combine(tempDir, "econtown_save.json"));
                Assert.IsFalse(json.Contains("beginnerTips"), "死开关字段应已撤(教学正确性修复④)。");
                Assert.IsFalse(json.Contains("publicProjectVisual"), "呈现层不得私建存档字段。");
            }
            finally
            {
                SaveSystem.OverrideSaveDirectory(null);
                if (System.IO.Directory.Exists(tempDir)) System.IO.Directory.Delete(tempDir, true);
            }
        }

        static Transform FindProjects(Transform root)
        {
            Transform direct = root.Find("PublicProjects");
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform nested = FindProjects(root.GetChild(i));
                if (nested != null) return nested;
            }
            return null;
        }

        void SetBuilt(PublicProjectType type, bool built = true)
        {
            GovernmentSimulator.Ensure(GameState.I);
            List<PublicProjectState> list = GameState.I.government.projects;
            for (int i = 0; i < list.Count; i++)
                if (list[i].type == type) list[i].built = built;
        }

        int CountBuildings()
        {
            int count = 0;
            for (int i = 0; i < town.childCount; i++)
                if (town.GetChild(i).name.StartsWith("B_")) count++;
            return count;
        }

        int CountGlowRenderers()
        {
            // 夜辉挂两处:PublicProjects 的公共灯杆 + Town 下的既有街灯——必须扫全图
            return mapObject.GetComponentsInChildren<SpriteRenderer>(true)
                .Count(sr => sr.name == "head_glow");
        }

        SpriteRenderer FindFirstGlow()
        {
            SpriteRenderer[] all = mapObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "head_glow") return all[i];
            return null;
        }
    }
}

#endif
