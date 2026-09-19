#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.12《全镇可进入店内·第二批(生活与商业六间)》回归:新间目录钉子、
    /// 书店门口注册随东商业新区动工解锁(概念黑板守卫先例)、装饰铺店内氛围交互回调、
    /// 各间核心家具命名件抽查(布局辨识度下限)。结构/换场/注册/护栏四组通用断言
    /// 已在 V0911InteriorTests 参数表覆盖十间,此处只钉第二批特有事实。</summary>
    public class EconTownV0912InteriorBatch2Tests
    {
        GameObject stateObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V0912InteriorState");
            GameState.I = stateObject.AddComponent<GameState>();
            ProgressionService.Ensure(GameState.I);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— 目录钉子:六新间网格错开与站位(换场/相机全查表,坐标是契约) —————

        [Test]
        public void InteriorCatalog_Batch2OriginsAndAnchors()
        {
            // +40x 递进续排(打样批四间之后),装饰铺零嘴亭小间 14×10
            Assert.AreEqual(new Vector3(160f, 0f, 0f), InteriorCatalog.Get("competitor").origin);
            Assert.AreEqual(new Vector3(200f, 0f, 0f), InteriorCatalog.Get("home").origin);
            Assert.AreEqual(new Vector3(240f, 0f, 0f), InteriorCatalog.Get("convenience").origin);
            Assert.AreEqual(new Vector3(280f, 0f, 0f), InteriorCatalog.Get("bookstore").origin);
            Assert.AreEqual(new Vector3(320f, 0f, 0f), InteriorCatalog.Get("decor_b").origin);
            InteriorSpec snack = InteriorCatalog.Get("decor_a");
            Assert.AreEqual(new Vector3(360f, 0f, 0f), snack.origin);
            Assert.AreEqual(14, snack.width, "零嘴亭是小间 14 宽(亭子体量,非标准 18)。");
            Assert.AreEqual(10, snack.height, "零嘴亭是小间 10 高。");
            // 站位钉子:counter 台前模式(V0.9.11 三店先例),零嘴亭小间微调 4.6
            Assert.AreEqual(new Vector3(171f, 4.7f, 0f), InteriorCatalog.Get("competitor").CounterWorld);
            Assert.AreEqual(new Vector3(205f, 4.7f, 0f), InteriorCatalog.Get("home").CounterWorld);
            Assert.AreEqual(new Vector3(253.5f, 4.7f, 0f), InteriorCatalog.Get("convenience").CounterWorld);
            Assert.AreEqual(new Vector3(295f, 4.7f, 0f), InteriorCatalog.Get("bookstore").CounterWorld);
            Assert.AreEqual(new Vector3(334f, 4.7f, 0f), InteriorCatalog.Get("decor_b").CounterWorld);
            Assert.AreEqual(new Vector3(369.5f, 4.6f, 0f), snack.CounterWorld);
            // 小间窗列适配(14 宽用 3/7/11,不沿用 18 宽的 4/9/14)
            Assert.AreEqual(new int[] { 3, 7, 11 }, snack.windowColumns);
        }

        // ————— 书店门注册守卫:东商业新区未动工不注册,动工后随 RegisterDistrictInteractables 注册 —————

        [Test]
        public void BookstoreDoor_RegistersOnlyAfterEastDistrictStarts()
        {
            GameObject mapObject = new GameObject("V0912BookstoreMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                System.Reflection.MethodInfo register = typeof(GameBootstrap).GetMethod("RegisterInteractables",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.IsNotNull(register);
                register.Invoke(null, null);   // completedChapter 默认 0 → 东区 NotStarted
                Assert.IsNull(FindByLabel("进入东街书店"),
                    "东商业新区未动工时书店门口不得注册(玩家还进不去,注册了就是死交互点)。");

                GameState.I.progression.completedChapter = 5;   // 东区动工(概念黑板同门槛)
                GameBootstrap.RegisterDistrictInteractables();
                Interactable door = FindByLabel("进入东街书店");
                Assert.IsNotNull(door, "东区动工后书店门口应补注册(RegisterOnce 幂等)。");
                Assert.AreEqual(MapBuilder.BookstoreDoor, door.position, "门口位置=门点属性。");
                Assert.IsNotNull(door.onUse, "回调应为进店(EnterInterior)。");
                // 复审(wiring)回滚对账:读档回到未动工的旧档时,换场条目必须被注销——
                // 残留的「空地按 E 进书店」会把玩家传进该存档状态下不存在的店内
                GameState.I.progression.completedChapter = 3;
                GameBootstrap.RegisterDistrictInteractables();
                Assert.IsNull(FindByLabel("进入东街书店"),
                    "东区未动工时书店门口条目应被 Remove 注销(补注册要有对偶的补注销)。");
                // 装饰铺两间是常驻建筑,任何进度都注册
                Assert.IsNotNull(FindByLabel("进入零嘴亭"));
                Assert.IsNotNull(FindByLabel("进入花店"));
            }
            finally
            {
                Interactables.All.Clear();
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 装饰铺店内交互:纯氛围 toast 回调非空(零经济改动的转正边界) —————

        [Test]
        public void DecorShops_CounterCallbacksAreFlavorToasts()
        {
            // InteriorCounterUse 是私有静态,反射取三间回调(不 Invoke——Invoke 会碰 UIManager 单例)。
            // 复审(tests):每个分支都返回非空委托(含 default=果汁店营业面板),IsNotNull 是重言式
            // ——case 被删落 default 时书店店内点会开果汁店经济面板,原断言照样绿。加两道实断言:
            // ①不与 default 回调同引用(抓 case 丢失) ②三者互异(抓误绑同一经济面板)
            System.Reflection.MethodInfo use = typeof(GameBootstrap).GetMethod("InteriorCounterUse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(use);
            System.Action fallback = (System.Action)use.Invoke(null, new object[] { "shop" });   // "shop" 落 default
            System.Action bookstore = (System.Action)use.Invoke(null, new object[] { "bookstore" });
            System.Action flower = (System.Action)use.Invoke(null, new object[] { "decor_b" });
            System.Action snack = (System.Action)use.Invoke(null, new object[] { "decor_a" });
            // V0.9.13 复审(tests)顺带修:AreNotSame 比引用是重言式(C# 9 每次方法组/委托 Invoke 各 new
            // 实例)——改比 MethodInfo 身份,落 default(同为 OpenShopPanel)时会红
            Assert.AreNotEqual(fallback.Method, bookstore.Method, "书店店内点不得回落 default(果汁店营业面板)。");
            Assert.AreNotEqual(fallback.Method, flower.Method, "花店店内点不得回落 default。");
            Assert.AreNotEqual(fallback.Method, snack.Method, "零嘴亭店内点不得回落 default。");
            Assert.AreNotSame(bookstore, flower, "三间装饰铺回调应各自独立。");
            Assert.AreNotSame(bookstore, snack, "三间装饰铺回调应各自独立。");
            Assert.AreNotSame(flower, snack, "三间装饰铺回调应各自独立。");
        }

        // ————— 核心件抽查:各间命名件存在(布局辨识度下限,防布局函数被误删一整段) —————

        [Test]
        public void Batch2Rooms_SignatureFurniturePresent()
        {
            GameObject mapObject = new GameObject("V0912SignatureMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                // (kind, 件名):茶台/床+书桌/白亮收银台/书架巷道/花桶+花架/小卖窗
                string[,] checks =
                {
                    { "competitor", "tea_counter" }, { "competitor", "table_4" },
                    { "home", "bed" }, { "home", "desk" },
                    { "convenience", "counter" }, { "convenience", "shelf_d" },
                    { "bookstore", "shelf_d" }, { "bookstore", "reading_table" },
                    { "decor_b", "bucket_c" }, { "decor_b", "rack_b" },
                    { "decor_a", "snack_window" }, { "decor_a", "cooler" }, { "decor_a", "snack_pendant" },
                };
                for (int i = 0; i < checks.GetLength(0); i++)
                {
                    Transform furn = MapBuilder.InteriorRoom(checks[i, 0]).Find("furniture");
                    Assert.IsNotNull(furn.Find(checks[i, 1]),
                        checks[i, 0] + " 店内应有一件 \"" + checks[i, 1] + "\"(布局辨识度下限)。");
                }

                Transform convenience = MapBuilder.InteriorRoom("convenience").Find("furniture");
                string[] shelfNames = { "shelf_a", "shelf_b", "shelf_c", "shelf_d", "cigarette_shelf" };
                var shelfSprites = new System.Collections.Generic.HashSet<Sprite>();
                for (int i = 0; i < shelfNames.Length; i++)
                    shelfSprites.Add(convenience.Find(shelfNames[i]).GetComponent<SpriteRenderer>().sprite);
                Assert.AreEqual(5, shelfSprites.Count, "便利店零食/饮料/日化/速食/烟酒五区不得复用同一货架贴图。");

                Transform pendant = MapBuilder.InteriorRoom("decor_a").Find("furniture/snack_pendant");
                Transform glow = pendant.Find("glow");
                Assert.IsNotNull(glow, "零嘴亭吊灯必须有独立暖光层。");
                Assert.Greater(glow.GetComponent<SpriteRenderer>().sortingOrder,
                    pendant.GetComponent<SpriteRenderer>().sortingOrder, "暖光层必须绘制在灯具之上。");

                Color32[] facadeGlow = PixelArt.WindowGlow("decor_a").texture.GetPixels32();
                int litPixels = 0;
                for (int i = 0; i < facadeGlow.Length; i++) if (facadeGlow[i].a > 0) litPixels++;
                Assert.That(litPixels, Is.InRange(20, 80),
                    "零嘴亭外立面只能点亮吊灯附近，不能再把整块取货窗照成白片。");
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
