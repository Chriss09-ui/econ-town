#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    /// <summary>V0.9.7《店里的柜台》回归:店内世界根结构、进出换场、交互注册与家具护栏。
    /// 进出切换照 V072PortTests 的过渡测试样板;交互计数照 V073 CountLabels 样板;
    /// 家具护栏照 V072 PortCargoDecor 样板。</summary>
    public class EconTownV097InteriorTests
    {
        GameObject stateObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V097InteriorState");
            GameState.I = stateObject.AddComponent<GameState>();
            ProgressionService.Ensure(GameState.I);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void InteriorRoot_HidesAtBoot_WithFloorWallWindowsAndAnchors()
        {
            GameObject mapObject = new GameObject("V097InteriorStructureMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Transform interior = mapObject.transform.Find("ShopInterior");   // V0.9.11:店内世界根(下挂 interior_<kind> 子房间)
                Assert.IsNotNull(interior, "店内应有独立世界根 ShopInterior(与 Town/PortScene 平级)。");
                interior = interior.Find("interior_shop");   // 果汁店子房间(V0.9.11 根下多间,家具/锚点断言全在此间)
                Assert.IsNotNull(interior, "店内应有 interior_shop 子房间。");
                Assert.IsFalse(interior.gameObject.activeSelf, "启动时只显示小镇,店内等待门口按 E 进入。");
                Assert.IsNotNull(interior.Find("Ground"), "店内应有 Ground Tilemap。");
                Assert.IsNotNull(interior.Find("furniture"), "店内应有家具根。");

                Tilemap ground = interior.Find("Ground").GetComponent<Tilemap>();
                Assert.IsNotNull(ground.GetTile(new Vector3Int(3, 3, 0)), "地板应铺满房间下部。");
                Assert.IsNotNull(ground.GetTile(new Vector3Int(7, 10, 0)), "上墙应有墙面砖。");
                Assert.IsNotNull(ground.GetTile(new Vector3Int(8, 9, 0)), "上墙中排 x=8 应是窗位。");
                Assert.IsNotNull(ground.GetTile(new Vector3Int(7, 8, 0)), "上墙底行应是墙裙。");

                Transform furn = interior.Find("furniture");
                Assert.IsNotNull(furn.Find("counter"), "收银柜台是店内交互锚点,必须在位。");
                Assert.IsNotNull(furn.Find("juice_press"), "榨汁台陈设缺失。");
                Assert.IsNotNull(furn.Find("door_mat"), "门垫是出店交互锚点,必须在位。");

                // 围墙四面齐全(挡角色不挡视线):北墙压在可见墙带下缘 y=8
                Assert.IsNotNull(interior.Find("interior_wall_north"));
                Assert.IsNotNull(interior.Find("interior_wall_south"));
                Assert.IsNotNull(interior.Find("interior_wall_west"));
                Assert.IsNotNull(interior.Find("interior_wall_east"));

                Assert.Greater(MapBuilder.InteriorW, 10f, "房间宽度应是一间店,不是一条走廊。");
                Assert.Greater(Vector3.Distance(MapBuilder.InteriorEntryPosition, MapBuilder.InteriorExitSpot), .5f,
                    "进店落点须与门垫错开,避免出店立即再触发。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void InteriorTransition_HidesTownTeleportsPlayer_AndReturns_PortExclusive()
        {
            GameObject mapObject = new GameObject("V097InteriorTransitionMap");
            GameObject playerObject = new GameObject("V097InteriorTransitionPlayer");
            GameObject cameraObject = new GameObject("V097InteriorTransitionCamera");
            GameObject followObject = new GameObject("V097InteriorTransitionFollow");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 7.6f;
                CameraFollow follow = followObject.AddComponent<CameraFollow>();
                follow.target = playerObject.transform;
                follow.cam = camera;
                Vector3 originalPosition = MapBuilder.ShopDoor + new Vector3(0f, -.8f, 0f);
                playerObject.transform.position = originalPosition;

                PortSceneController.Create(playerObject.transform, follow, camera);
                ShopInteriorController.Create(playerObject.transform, follow, camera);

                // 与港区互斥:在港内时进店被拒,港区状态不受扰动
                PortSceneController.EnterPort();
                ShopInteriorController.EnterShopInterior();
                Assert.IsTrue(PortSceneController.IsInsidePort, "港内进店必须被拒绝。");
                Assert.IsFalse(ShopInteriorController.IsInsideShopInterior);
                PortSceneController.ExitPort();
                Assert.AreEqual(originalPosition, playerObject.transform.position);

                ShopInteriorController.EnterShopInterior();
                Assert.IsTrue(ShopInteriorController.IsInsideShopInterior);
                Assert.IsFalse(MapBuilder.TownRoot.gameObject.activeSelf, "进店后 Town 必须隐藏(与港区同款互斥)。");
                Assert.IsFalse(MapBuilder.PortDistrictRoot.gameObject.activeSelf);
                Assert.IsTrue(MapBuilder.ShopInteriorRoot.gameObject.activeSelf);
                Assert.AreEqual(MapBuilder.InteriorEntryPosition, playerObject.transform.position);

                ShopInteriorController.ExitShopInterior();
                Assert.IsFalse(ShopInteriorController.IsInsideShopInterior);
                Assert.IsTrue(MapBuilder.TownRoot.gameObject.activeSelf);
                Assert.IsFalse(MapBuilder.ShopInteriorRoot.gameObject.activeSelf);
                Assert.AreEqual(originalPosition, playerObject.transform.position);
            }
            finally
            {
                if (PortSceneController.I != null) Object.DestroyImmediate(PortSceneController.I.gameObject);
                if (ShopInteriorController.I != null) Object.DestroyImmediate(ShopInteriorController.I.gameObject);
                Object.DestroyImmediate(followObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void InteriorInteractables_DoorEnters_CounterOpensPanel_ExitLeaves()
        {
            GameObject mapObject = new GameObject("V097InteriorInteractMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                // RegisterInteractables 是 private static(GameBootstrap.Start 末尾调用),
                // 反射触发一次完整注册(注册闭包不执行,EditMode 下无需 UIManager)
                MethodInfo register = typeof(GameBootstrap).GetMethod("RegisterInteractables",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(register, "GameBootstrap.RegisterInteractables 应存在。");
                register.Invoke(null, null);

                Interactable door = FindByLabel("进入我的果汁店");
                Assert.IsNotNull(door, "店门交互点应注册。");
                Assert.AreEqual(MapBuilder.ShopDoor, door.position, "店门交互点位置不得移动(V072 钉子)。");
                Assert.AreEqual(WorldArea.Town, door.area);

                Interactable counter = FindByLabel("收银台 · 营业");
                Assert.IsNotNull(counter, "店内收银台交互点应注册(营业面板的门内入口)。");
                Assert.AreEqual(MapBuilder.InteriorCounterSpot, counter.position);
                Assert.AreEqual(WorldArea.ShopInterior, counter.area);

                Interactable exit = FindByLabel("走出果汁店");
                Assert.IsNotNull(exit, "门垫出店交互点应注册。");
                Assert.AreEqual(MapBuilder.InteriorExitSpot, exit.position);
                Assert.AreEqual(WorldArea.ShopInterior, exit.area);
            }
            finally
            {
                // 与 V073 范式对齐:注册表不留给后续用例(残留闭包持有 UIManager/GameState 引用)
                Interactables.All.Clear();
                if (ShopInteriorController.I != null) Object.DestroyImmediate(ShopInteriorController.I.gameObject);
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void InteriorFurniture_SolidsWithFootprints_KeepPathsClear()
        {
            GameObject mapObject = new GameObject("V097InteriorFurnitureMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Transform furn = mapObject.transform.Find("ShopInterior/interior_shop/furniture");
                Assert.IsNotNull(furn, "店内家具根缺失。");

                int counters = 0, presses = 0, mats = 0, tables = 0, shelves = 0, coolers = 0, plants = 0;
                Vector3[] protectedPoints = { MapBuilder.InteriorEntryPosition, MapBuilder.InteriorExitSpot };
                for (int i = 0; i < furn.childCount; i++)
                {
                    Transform child = furn.GetChild(i);
                    if (child.name == "counter") counters++;
                    else if (child.name == "juice_press") presses++;
                    else if (child.name == "door_mat") mats++;
                    else if (child.name.StartsWith("table")) tables++;
                    else if (child.name == "shelf_unit") shelves++;
                    else if (child.name == "fruit_cooler") coolers++;
                    else if (child.name.StartsWith("plant")) plants++;

                    if (child.name == "door_mat")
                    {
                        // 门垫平铺不挡人,但要有一张 isTrigger 点击体(点击出店的射线目标)
                        BoxCollider2D matClick = child.GetComponent<BoxCollider2D>();
                        Assert.IsNotNull(matClick, "门垫缺点击碰撞体(点击出店会失效)。");
                        Assert.IsTrue(matClick.isTrigger, "门垫碰撞体必须是 isTrigger(不得挡人)。");
                        continue;
                    }
                    BoxCollider2D solid = child.GetComponent<BoxCollider2D>();
                    Assert.IsNotNull(solid, child.name + " 家具应有落地脚印碰撞体。");
                    for (int p = 0; p < protectedPoints.Length; p++)
                        Assert.Greater(Vector2.Distance(child.position, protectedPoints[p]), 1.2f,
                            child.name + " 不得堵住进店落点或门垫。");
                    if (child.name != "counter")                                   // 柜台自己的站位除外
                        Assert.Greater(Vector2.Distance(child.position, MapBuilder.InteriorCounterSpot), 1f,
                            child.name + " 不得堵住收银台站位。");
                }

                Assert.AreEqual(1, counters, "收银柜台应恰一台。");
                Assert.AreEqual(1, presses);
                Assert.AreEqual(1, mats, "门垫应恰一块。");
                Assert.AreEqual(4, tables, "座位区应有四张圆桌(V0.9.7b 加密)。");
                Assert.AreEqual(1, shelves, "货架应恰一座。");
                Assert.AreEqual(1, coolers, "果汁冷柜应恰一台。");
                Assert.AreEqual(2, plants, "盆栽应恰两盆。");

                // V0.9.7b:店内常驻两名顾客(挂 customers 节点,随店显隐)
                Transform customers = mapObject.transform.Find("ShopInterior/interior_shop/customers");
                Assert.IsNotNull(customers, "店内应有顾客节点。");
                Assert.AreEqual(2, customers.childCount, "店内应常驻两名顾客。");
                for (int i = 0; i < customers.childCount; i++)
                    Assert.IsNotNull(customers.GetChild(i).GetComponent<NpcAgent>(),
                        customers.GetChild(i).name + " 应是 NpcAgent(房间边界内漫游)。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        static Interactable FindByLabel(string label)
        {
            for (int i = 0; i < Interactables.All.Count; i++)
                if (Interactables.All[i].label == label) return Interactables.All[i];
            return null;
        }
    }
}
#endif
