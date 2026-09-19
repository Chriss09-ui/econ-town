#if UNITY_EDITOR

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    /// <summary>V0.9.11《全镇可进入店内·打样批》回归:目录表网格分离、四间房间结构、
    /// per-kind 换场(同刻只亮一间)、门口 E=进店+店内面板交互注册、家具护栏与 NPC 越界。
    /// 结构/换场/注册/护栏四组均照 V097InteriorTests 样板参数化到四间打样店。
    /// V0.9.12 第二批(茶室/家/便利屋/书店/花店/零嘴亭)直接在四组参数表追加——
    /// 目录驱动的护栏断言对新间零特判(home 无 NPC/decor_a 单 NPC 走口径表)。</summary>
    public class EconTownV0911InteriorTests
    {
        GameObject stateObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V0911InteriorState");
            GameState.I = stateObject.AddComponent<GameState>();
            ProgressionService.Ensure(GameState.I);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— 目录表:网格分离 + 果汁店锚点兼容钉子 —————

        [Test]
        public void InteriorCatalog_OriginsGridSeparated_ShopAnchorsUnchanged()
        {
            for (int i = 0; i < InteriorCatalog.All.Length; i++)
                for (int j = i + 1; j < InteriorCatalog.All.Length; j++)
                {
                    InteriorSpec a = InteriorCatalog.All[i], b = InteriorCatalog.All[j];
                    Assert.AreNotEqual(a.kind, b.kind, "kind 不得重复(交互/换场按 kind 寻址)。");
                    // x 向净距:两房矩形最近边之间的空隙 ≥22(1.7 扫描半径的十倍余量,防交互点串扰)
                    float gap = Mathf.Abs(a.origin.x - b.origin.x) - (a.width + b.width) / 2f;
                    Assert.GreaterOrEqual(gap, 22f,
                        a.kind + " 与 " + b.kind + " 房间净距 " + gap + " 应 ≥22(交互点共用 ShopInterior 区)。");
                }
            InteriorSpec shop = InteriorCatalog.Get("shop");
            Assert.AreEqual(0f, shop.origin.x);
            Assert.AreEqual(0f, shop.origin.y);
            // V0.9.7 锚点钉子:迁移后三个世界坐标必须逐位不变(V097/相机/E 键都读它)
            Assert.AreEqual(new Vector3(7.8f, 1.8f, 0f), shop.EntryWorld, "果汁店进店落点漂移。");
            Assert.AreEqual(new Vector3(11.2f, 5.55f, 0f), shop.CounterWorld, "果汁店收银台站位漂移。");
            Assert.AreEqual(new Vector3(7.8f, .75f, 0f), shop.ExitWorld, "果汁店门垫交互点漂移(V0.9.7 原值 .75)。");
            Assert.AreEqual(new Vector4(.7f, .6f, 15.3f, 7.2f), shop.NpcBounds, "果汁店 NPC 漫游界漂移。");
            Assert.IsFalse(InteriorCatalog.Exists("nonsense"), "目录外 kind 应拒绝。");
            Assert.AreSame(shop, InteriorCatalog.Get("nonsense"), "未知 kind 回落 shop(门表配错不至于进不了店)。");
        }

        // ————— 房间结构:boot 隐藏 + 墙带/窗位/地板 + 围墙四面 + 边界查表 —————

        [TestCase("shop")]
        [TestCase("restaurant")]
        [TestCase("cinema")]
        [TestCase("market")]
        [TestCase("competitor")]
        [TestCase("home")]
        [TestCase("convenience")]
        [TestCase("bookstore")]
        [TestCase("decor_b")]
        [TestCase("decor_a")]
        [TestCase("bank")]
        [TestCase("centralbank")]
        [TestCase("hall")]
        [TestCase("training")]
        [TestCase("factory")]
        public void InteriorRoom_Structure_HidesAtBoot_FullWallBand(string kind)
        {
            GameObject mapObject = new GameObject("V0911StructureMap_" + kind);
            try
            {
                MapBuilder.Build(mapObject.transform);
                InteriorSpec s = InteriorCatalog.Get(kind);
                Transform room = MapBuilder.InteriorRoom(kind);
                Assert.IsNotNull(room, "interior_" + kind + " 子房间应存在。");
                Assert.IsFalse(room.gameObject.activeSelf, "启动时各房间一律隐藏,等门口按 E 激活。");
                Assert.AreEqual(s.origin, room.position, "子房间应落在目录表 origin。");

                Tilemap ground = room.Find("Ground").GetComponent<Tilemap>();
                Assert.IsNotNull(ground.GetTile(new Vector3Int(2, 2, 0)), "地板应铺满房间下部。");
                Assert.IsNotNull(ground.GetTile(new Vector3Int(s.width / 2, s.height - 1, 0)), "上墙顶行应有墙面砖。");
                Assert.IsNotNull(ground.GetTile(new Vector3Int(s.width / 2, s.height - 3, 0)), "上墙底行应是墙裙。");
                for (int w = 0; w < s.windowColumns.Length; w++)
                    Assert.IsNotNull(ground.GetTile(new Vector3Int(s.windowColumns[w], s.height - 2, 0)),
                        "上墙中排 x=" + s.windowColumns[w] + " 应是窗位。");

                Assert.IsNotNull(room.Find("interior_wall_north"), "北墙缺失。");
                Assert.IsNotNull(room.Find("interior_wall_south"), "南墙缺失。");
                Assert.IsNotNull(room.Find("interior_wall_west"), "西墙缺失。");
                Assert.IsNotNull(room.Find("interior_wall_east"), "东墙缺失。");

                Vector4 bounds = MapBuilder.InteriorBounds(kind);
                Assert.AreEqual(new Vector4(s.origin.x, s.origin.y, s.origin.x + s.width, s.origin.y + s.height),
                    bounds, "相机 clamp 域应等于房间矩形(盒子视图)。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 换场:进店只亮本间,出店全还原;港内拒、店内换店拒 —————

        [TestCase("shop")]
        [TestCase("restaurant")]
        [TestCase("cinema")]
        [TestCase("market")]
        [TestCase("competitor")]
        [TestCase("home")]
        [TestCase("convenience")]
        [TestCase("bookstore")]
        [TestCase("decor_b")]
        [TestCase("decor_a")]
        [TestCase("bank")]
        [TestCase("centralbank")]
        [TestCase("hall")]
        [TestCase("training")]
        [TestCase("factory")]
        public void InteriorTransition_HidesTown_ActivatesOnlyRoom_Restores(string kind)
        {
            GameObject mapObject = new GameObject("V0911TransitionMap_" + kind);
            GameObject playerObject = new GameObject("V0911TransitionPlayer_" + kind);
            GameObject cameraObject = new GameObject("V0911TransitionCamera_" + kind);
            GameObject followObject = new GameObject("V0911TransitionFollow_" + kind);
            try
            {
                MapBuilder.Build(mapObject.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 7.6f;
                CameraFollow follow = followObject.AddComponent<CameraFollow>();
                follow.target = playerObject.transform;
                follow.cam = camera;
                InteriorSpec s = InteriorCatalog.Get(kind);
                Vector3 originalPosition = s.door() + new Vector3(0f, -.8f, 0f);
                playerObject.transform.position = originalPosition;

                PortSceneController.Create(playerObject.transform, follow, camera);
                ShopInteriorController.Create(playerObject.transform, follow, camera);

                // 港内进店被拒(两把场景钥匙不同时持有,与 V097 同语义)
                PortSceneController.EnterPort();
                ShopInteriorController.EnterInterior(kind);
                Assert.IsTrue(PortSceneController.IsInsidePort, "港内进店必须被拒绝。");
                PortSceneController.ExitPort();

                ShopInteriorController.EnterInterior(kind);
                Assert.IsTrue(ShopInteriorController.IsInsideShopInterior);
                Assert.AreEqual(kind, ShopInteriorController.CurrentKind, "当前店 kind 应被登记。");
                Assert.IsFalse(MapBuilder.TownRoot.gameObject.activeSelf, "进店后 Town 必须隐藏。");
                Assert.IsTrue(MapBuilder.ShopInteriorRoot.gameObject.activeSelf);
                for (int i = 0; i < InteriorCatalog.All.Length; i++)
                {
                    Transform room = MapBuilder.InteriorRoom(InteriorCatalog.All[i].kind);
                    if (room == null) continue;
                    Assert.AreEqual(InteriorCatalog.All[i].kind == kind, room.gameObject.activeSelf,
                        "同刻只亮 interior_" + kind + ",其余房间保持隐藏(坐标错开防叠印)。");
                }
                Assert.AreEqual(s.EntryWorld, playerObject.transform.position, "玩家应被传到本店落点。");
                Assert.AreEqual((Color)s.backgroundColor, camera.backgroundColor, "相机底色应换成本店暗框色。");

                // 店内再进另一间被拒(inside 闸,不经出店不得换店)
                ShopInteriorController.EnterInterior("market");
                Assert.AreEqual(kind, ShopInteriorController.CurrentKind, "店内直接换店必须被拒绝。");

                ShopInteriorController.ExitInterior();
                Assert.IsFalse(ShopInteriorController.IsInsideShopInterior);
                Assert.IsTrue(MapBuilder.TownRoot.gameObject.activeSelf);
                Assert.IsFalse(MapBuilder.ShopInteriorRoot.gameObject.activeSelf);
                Assert.AreEqual(originalPosition, playerObject.transform.position, "出店应回到门口原位。");
                Assert.AreEqual("shop", MapBuilder.ActiveInteriorKind, "出店后锚点属性复位果汁店语义。");
            }
            finally
            {
                MapBuilder.SetActiveInteriorKind("shop");   // 用例中途失败时 kind 残留会污染后续用例(复审)
                if (PortSceneController.I != null) Object.DestroyImmediate(PortSceneController.I.gameObject);
                if (ShopInteriorController.I != null) Object.DestroyImmediate(ShopInteriorController.I.gameObject);
                Object.DestroyImmediate(followObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 交互注册:门口 E=进店(Town 区)、店内面板点与门垫(ShopInterior 区) —————

        [TestCase("shop", "进入我的果汁店", "收银台 · 营业", "走出果汁店")]
        [TestCase("restaurant", "进入暖炉餐厅", "点餐台 · 消费", "走出暖炉餐厅")]
        [TestCase("cinema", "进入星光电影院", "售票亭 · 购票", "走出星光电影院")]
        [TestCase("market", "进入原料市场", "采购台 · 进货", "走出原料市场")]
        [TestCase("competitor", "进入 SUNNY TEA", "茶台 · 经营情报", "走出 SUNNY TEA")]
        [TestCase("home", "回到我的家", "书桌 · 今日账本", "走出家门")]
        [TestCase("convenience", "进入便利屋", "收银台 · 购物", "走出便利屋")]
        [TestCase("bookstore", "进入东街书店", "书台 · 翻看", "走出东街书店")]
        [TestCase("decor_b", "进入花店", "花台 · 挑花", "走出花店")]
        [TestCase("decor_a", "进入零嘴亭", "小卖窗 · 买零嘴", "走出零嘴亭")]
        [TestCase("bank", "进入小镇银行", "柜台 · 存取业务", "走出小镇银行")]
        [TestCase("centralbank", "进入中央银行", "利率台 · 央行政策", "走出中央银行")]
        [TestCase("hall", "进入市政厅", "政务台 · 财政总览", "走出市政厅")]
        [TestCase("training", "进入职业培训中心", "讲台 · 培训补贴", "走出职业培训中心")]
        [TestCase("factory", "进入河畔工厂", "产线台 · 环境治理", "走出河畔工厂")]
        public void InteriorInteractables_DoorEnters_CounterOpens_ExitLeaves(
            string kind, string doorLabel, string counterLabel, string exitLabel)
        {
            GameObject mapObject = new GameObject("V0911InteractMap_" + kind);
            try
            {
                // 书店门口注册随东商业新区动工解锁(RegisterDistrictInteractables 守卫,概念黑板先例)
                if (kind == "bookstore") GameState.I.progression.completedChapter = 5;
                MapBuilder.Build(mapObject.transform);
                MethodInfo register = typeof(GameBootstrap).GetMethod("RegisterInteractables",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(register);
                register.Invoke(null, null);

                InteriorSpec s = InteriorCatalog.Get(kind);
                Interactable door = FindByLabel(doorLabel);
                Assert.IsNotNull(door, kind + " 店门交互点应注册。");
                Assert.AreEqual(s.door(), door.position, "店门交互点位置不得移动(LayoutGuard/V072 钉子)。");
                Assert.AreEqual(WorldArea.Town, door.area);
                Assert.IsNotNull(door.onUse, "门口回调不得为空(应换场进店)。");

                Interactable counter = FindByLabel(counterLabel);
                Assert.IsNotNull(counter, kind + " 店内面板交互点应注册(原门口回调的门内入口)。");
                Assert.AreEqual(s.CounterWorld, counter.position, "面板交互点应在 counterLocal(台前站位)。");
                Assert.AreEqual(WorldArea.ShopInterior, counter.area);
                Assert.IsNotNull(counter.onUse, "面板回调不得为空(条件门都在面板函数内部)。");

                Interactable exit = FindByLabel(exitLabel);
                Assert.IsNotNull(exit, kind + " 门垫出店交互点应注册。");
                Assert.AreEqual(s.ExitWorld, exit.position);
                Assert.AreEqual(WorldArea.ShopInterior, exit.area);
            }
            finally
            {
                Interactables.All.Clear();   // 注册表不留给后续用例(闭包持有 UIManager/GameState 引用)
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 家具护栏:脚印碰撞体、路径净空、门垫 isTrigger+ExitClick、NPC 不越房 —————

        [TestCase("shop")]
        [TestCase("restaurant")]
        [TestCase("cinema")]
        [TestCase("market")]
        [TestCase("competitor")]
        [TestCase("home")]
        [TestCase("convenience")]
        [TestCase("bookstore")]
        [TestCase("decor_b")]
        [TestCase("decor_a")]
        [TestCase("bank")]
        [TestCase("centralbank")]
        [TestCase("hall")]
        [TestCase("training")]
        [TestCase("factory")]
        public void InteriorFurniture_SolidsFootprints_PathsAndCustomersClear(string kind)
        {
            GameObject mapObject = new GameObject("V0911FurnitureMap_" + kind);
            try
            {
                MapBuilder.Build(mapObject.transform);
                InteriorSpec s = InteriorCatalog.Get(kind);
                Transform furn = MapBuilder.InteriorRoom(kind).Find("furniture");
                Assert.IsNotNull(furn, "interior_" + kind + " 应有家具根。");

                Vector3[] protectedPoints = { s.EntryWorld, s.ExitWorld };
                Vector4 roomRect = s.RoomBounds;
                int mats = 0, solids = 0;
                for (int i = 0; i < furn.childCount; i++)
                {
                    Transform child = furn.GetChild(i);
                    // 全部件先验「落在本房矩形内」:布局函数手写 o+偏移,漏写 origin 时家具会静默
                    // 落进 shop 房隐藏区而距离断言全绿(复审——对 NPC 已设防,对家具补同款防线)
                    Assert.IsTrue(child.position.x >= roomRect.x && child.position.x <= roomRect.z
                        && child.position.y >= roomRect.y && child.position.y <= roomRect.w,
                        child.name + " 落在本房矩形外——家具坐标忘加 origin 的错位。");
                    if (child.name == "door_mat")
                    {
                        mats++;
                        BoxCollider2D matClick = child.GetComponent<BoxCollider2D>();
                        Assert.IsNotNull(matClick, "门垫缺点击碰撞体(点击出店会失效)。");
                        Assert.IsTrue(matClick.isTrigger, "门垫碰撞体必须是 isTrigger(不得挡人)。");
                        Assert.IsNotNull(child.GetComponent<ShopInteriorExitClick>(),
                            "门垫应挂 ShopInteriorExitClick(出店不分店,复用同一组件)。");
                        continue;
                    }
                    // 围猎五落账:壁面件(小卖窗吊灯,MapBuilder「壁面件不加实体碰撞」设计口径)
                    // 高悬墙面不落地,无脚印可言——不进实心件断言与密度下限。
                    if (child.name == "snack_pendant") continue;
                    solids++;
                    BoxCollider2D solid = child.GetComponent<BoxCollider2D>();
                    Assert.IsNotNull(solid, child.name + " 家具应有落地脚印碰撞体。");
                    // V0.9.14:红毯/地毯类 isTrigger 可踩地件不挡人——距离/站位盒/贴地断言不适用
                    // (门垫先例),只保留上方已验的「落在本房矩形内」与碰撞体存在性
                    if (solid.isTrigger)
                    {
                        solids--;   // 也不计入实心件密度下限
                        continue;
                    }
                    for (int p = 0; p < protectedPoints.Length; p++)
                        Assert.Greater(Vector2.Distance(child.position, protectedPoints[p]), 1.2f,
                            child.name + " 不得堵住进店落点或门垫。");
                    if (Vector2.Distance(child.position, s.CounterWorld) <= 1f && !IsCounterPiece(kind, child.name))
                        Assert.Fail(child.name + " 不得堵住面板站位(柜台本件除外)。");
                    // V0.9.12 复审根治:中心距 >1.2 拦不住「脚印罩住玩家站位盒」——shelf_c 脚印南伸
                    // 1.4 格全包含玩家 0.6×0.8/offset(0,0.5) 的进店盒(中心距 1.46 绿灯、进店即嵌进
                    // 被物理弹出)。直接断言家具世界 AABB 与三处玩家站位盒(进店落点/门垫/面板站位,
                    // 站位盒=p.x±0.3, p.y+0.5±0.4)不相交;面板站位对本件豁免(台前本来就贴着它)
                    Vector3[] standPoints = { s.EntryWorld, s.ExitWorld, s.CounterWorld };
                    string[] standNames = { "进店落点", "门垫", "面板站位" };
                    bool counterPiece = IsCounterPiece(kind, child.name);
                    // 复审(V0.9.13)根治:Collider2D.bounds 在 EditMode 未经物理步进时退化为一个点
                    // (实测 min==max==transform.position,V0.9.12 起的站位盒断言一直拿零尺寸盒空转)——
                    // 手算世界 AABB:position+offset±size/2(店内件无旋转无缩放,不依赖 Physics2D 同步)
                    Bounds fp = new Bounds(
                        new Vector3(child.position.x + solid.offset.x, child.position.y + solid.offset.y, 0f),
                        (Vector3)solid.size);
                    for (int p = 0; p < standPoints.Length; p++)
                    {
                        if (p == 2 && counterPiece) continue;
                        Vector3 sp = standPoints[p];
                        Assert.IsTrue(fp.max.x < sp.x - .3f || fp.min.x > sp.x + .3f
                            || fp.max.y < sp.y + .1f || fp.min.y > sp.y + .9f,
                            child.name + " 脚印与玩家" + standNames[p] + "盒相交——玩家落点会被家具碰撞体弹出。");
                    // V0.9.13 复审根治:34px 高件的 collider 曾比 sprite 落地脚南伸 0.79 格(空气墙,
                    // 中心距/站位盒断言都拦不住)——补「碰撞南缘不得比 sprite 底缘再南伸 0.45 格」
                    // (全店先例带 0.11~0.425;挂墙黑板类薄脚印 0.36 亦在带内)
                    SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        float spriteBottom = child.position.y + sr.sprite.bounds.min.y;   // pivot 居中,scale=1
                        Assert.LessOrEqual(fp.min.y - spriteBottom, .45f,
                            child.name + " 碰撞脚印比 sprite 落地脚南伸超 0.45 格——贴脸前会被空气墙弹住。");
                    }
                    }
                }
                Assert.AreEqual(1, mats, "门垫应恰一块。");
                Assert.GreaterOrEqual(solids, 5, "演出级陈设:除门垫外应至少 5 件家具(防空洞走廊)。");

                Transform customers = MapBuilder.InteriorRoom(kind).Find("customers");
                Assert.IsNotNull(customers);
                // V0.9.11b:市场三摊各配一名站摊商贩(3 商贩+2 顾客);其余店两名顾客
                // V0.9.12:我的家不设常驻 NPC(自家,customers 留空);零嘴亭小间单名小孩
                Assert.AreEqual(InteriorCustomerCount(kind), customers.childCount, "店内常驻 NPC 数(市场含三名摊主)。");
                Vector4 b = s.RoomBounds;
                const BindingFlags fFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                System.Reflection.FieldInfo fx = typeof(NpcAgent).GetField("wanderMinX", fFlags),
                    fy = typeof(NpcAgent).GetField("wanderMinY", fFlags),
                    fz = typeof(NpcAgent).GetField("wanderMaxX", fFlags),
                    fw = typeof(NpcAgent).GetField("wanderMaxY", fFlags);
                for (int i = 0; i < customers.childCount; i++)
                {
                    NpcAgent npc = customers.GetChild(i).GetComponent<NpcAgent>();
                    Assert.IsNotNull(npc, customers.GetChild(i).name + " 应是 NpcAgent。");
                    Vector3 p = customers.GetChild(i).position;
                    Assert.IsTrue(p.x >= b.x && p.x <= b.z && p.y >= b.y && p.y <= b.w,
                        customers.GetChild(i).name + " 出生在本房矩形外——NPC 坐标忘加 origin 的错位。");
                    // 漫游界传递也要锁(复审):出生点与 bounds 是 Spawn 的两个独立参数。V0.9.11b 起
                    // 商贩/观众可用布局自定义小 bounds(锁摊后/锁过道),断言口径=四分量都落在本房矩形内
                    // ——误留旧 shop 手写值(x 0.7..15.3)时对新店(如 market 120..138)仍必红
                    float wMinX = (float)fx.GetValue(npc), wMinY = (float)fy.GetValue(npc),
                        wMaxX = (float)fz.GetValue(npc), wMaxY = (float)fw.GetValue(npc);
                    Assert.IsTrue(wMinX >= b.x && wMaxX <= b.z && wMinY >= b.y && wMaxY <= b.w,
                        customers.GetChild(i).name + " 漫游界越出本房矩形——bounds 忘加 origin 或填错。");
                }
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 注册残留防线:ActiveInteriorKind 残留时果汁店三条注册不得漂移 —————

        [Test]
        public void InteriorInteractables_ShopEntries_ImmuneToStaleActiveKind()
        {
            // 复审实锤的失败链:进 market 店内 → 设置面板「重新开始」→ LoadScene 重载(不触发
            // SubsystemRegistration)→ ActiveInteriorKind 残留 "market" → 新一轮 RegisterInteractables
            // 把果汁店收银台/门垫注册到 market 坐标。修复=果汁店三条用 shop 快照;本用例钉住该防线。
            GameObject mapObject = new GameObject("V0911StaleKindMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                MapBuilder.SetActiveInteriorKind("market");   // 模拟残留(故意不出店不复位)
                MethodInfo register = typeof(GameBootstrap).GetMethod("RegisterInteractables",
                    BindingFlags.NonPublic | BindingFlags.Static);
                register.Invoke(null, null);
                Interactable counter = FindByLabel("收银台 · 营业");
                Assert.IsNotNull(counter);
                Assert.AreEqual(new Vector3(11.2f, 5.55f, 0f), counter.position,
                    "果汁店收银台注册必须用 shop 快照,不得随残留 kind 漂到别店坐标。");
                Interactable exit = FindByLabel("走出果汁店");
                Assert.AreEqual(new Vector3(7.8f, .75f, 0f), exit.position,
                    "果汁店门垫注册必须用 shop 快照。");
            }
            finally
            {
                MapBuilder.SetActiveInteriorKind("shop");
                Interactables.All.Clear();
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 相机 clamp 域:查表值即房间矩形,出店复位后果汁店边界不变 —————

        [Test]
        public void CameraClamp_InteriorBounds_PerKind()
        {
            Assert.AreEqual(new Vector4(0f, 0f, 16f, 11f), MapBuilder.InteriorBounds("shop"),
                "果汁店 clamp 域应保持 V0.9.7 原值(16×11@原点)。");
            Assert.AreEqual(new Vector4(40f, 0f, 58f, 12f), MapBuilder.InteriorBounds("restaurant"));
            Assert.AreEqual(new Vector4(80f, 0f, 98f, 12f), MapBuilder.InteriorBounds("cinema"));
            Assert.AreEqual(new Vector4(120f, 0f, 138f, 12f), MapBuilder.InteriorBounds("market"));
            // 五旧属性的默认读数(未进店)应落果汁店域——CameraFollow 两处三元链读的就是它们
            Assert.AreEqual(0f, MapBuilder.InteriorMinX);
            Assert.AreEqual(16f, MapBuilder.InteriorMaxX);
            Assert.AreEqual(0f, MapBuilder.InteriorMinY);
            Assert.AreEqual(11f, MapBuilder.InteriorMaxY);
        }

        static bool IsCounterPiece(string kind, string name)
        {
            // 各店「面板站位旁的本件」豁免距离护栏(站位本来就贴着它);V0.9.12 六间同规则追加
            return (kind == "shop" && name == "counter")
                || (kind == "restaurant" && name == "counter")
                || (kind == "cinema" && name == "ticket_booth")
                || (kind == "market" && name == "stall_buy")
                || (kind == "competitor" && name == "tea_counter")
                || (kind == "home" && name == "desk")
                || (kind == "convenience" && name == "counter")
                || (kind == "bookstore" && name == "counter")
                || (kind == "decor_b" && name == "counter")
                || (kind == "decor_a" && name == "snack_window")
                || (kind == "bank" && name == "counter")
                || (kind == "centralbank" && name == "counter")
                || (kind == "hall" && name == "counter")
                || (kind == "training" && name == "podium")
                || (kind == "factory" && name == "conveyor");
        }

        static int InteriorCustomerCount(string kind)
        {
            if (kind == "market") return 5;    // 三商贩+两顾客
            if (kind == "home") return 0;      // 自家无常驻
            if (kind == "decor_a") return 1;   // 零嘴亭小间单名小孩
            // V0.9.14 加密批:八间升三——柜台后营业者(店员/店主/行长/公务员)+影院第三名观众+教室第三名学生
            if (kind == "competitor" || kind == "convenience" || kind == "bookstore" || kind == "cinema"
                || kind == "bank" || kind == "centralbank" || kind == "hall" || kind == "training") return 3;
            return 2;
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
