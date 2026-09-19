#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>V0.9.13《全镇可进入店内·第三批(机构与产业五间)》回归:新间目录钉子、
    /// 五门注册与河畔工厂转正改名(旧「查看」label 防残留)、店内柜台回调互异、
    /// 各间核心家具命名件抽查。结构/换场/注册/护栏四组通用断言已在 V0911InteriorTests
    /// 参数表覆盖十五间,此处只钉第三批特有事实。</summary>
    public class EconTownV0913InteriorBatch3Tests
    {
        GameObject stateObject;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V0913InteriorState");
            GameState.I = stateObject.AddComponent<GameState>();
            ProgressionService.Ensure(GameState.I);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        // ————— 目录钉子:五新间网格错开与站位(坐标是契约,换场/相机全查表) —————

        [Test]
        public void InteriorCatalog_Batch3OriginsAndAnchors()
        {
            // +40x 递进续排(第二批六间 360 之后),五间全部标准 18×12
            Assert.AreEqual(new Vector3(400f, 0f, 0f), InteriorCatalog.Get("bank").origin);
            Assert.AreEqual(new Vector3(440f, 0f, 0f), InteriorCatalog.Get("centralbank").origin);
            Assert.AreEqual(new Vector3(480f, 0f, 0f), InteriorCatalog.Get("hall").origin);
            Assert.AreEqual(new Vector3(520f, 0f, 0f), InteriorCatalog.Get("training").origin);
            Assert.AreEqual(new Vector3(560f, 0f, 0f), InteriorCatalog.Get("factory").origin);
            // 站位钉子:四间柜台台前模式 y=4.7(全镇统一),培训中心讲台贴黑板破例 y=7.0
            Assert.AreEqual(new Vector3(411f, 4.7f, 0f), InteriorCatalog.Get("bank").CounterWorld);
            Assert.AreEqual(new Vector3(451f, 4.7f, 0f), InteriorCatalog.Get("centralbank").CounterWorld);
            Assert.AreEqual(new Vector3(489f, 4.7f, 0f), InteriorCatalog.Get("hall").CounterWorld);
            Assert.AreEqual(new Vector3(529f, 7f, 0f), InteriorCatalog.Get("training").CounterWorld,
                "培训中心柜台=讲台,必须贴北墙黑板(y=7.0 破例)。");
            Assert.AreEqual(new Vector3(571f, 4.7f, 0f), InteriorCatalog.Get("factory").CounterWorld);
        }

        // ————— 五门注册:机构五间门口无条件进店;央行未解锁只拦面板不拦门口(market 清晨门口径);
        // 河畔工厂转正改名,旧「查看河畔工厂」不得残留 —————

        [Test]
        public void Batch3Doors_RegisterUnconditional_FactoryRelabelClean()
        {
            GameObject mapObject = new GameObject("V0913DoorMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                System.Reflection.MethodInfo register = typeof(GameBootstrap).GetMethod("RegisterInteractables",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.IsNotNull(register);
                register.Invoke(null, null);   // completedChapter 默认 0(央行未解锁/一切未开)

                // 五门全部注册且位置=门点属性(五间都是 TownPlacements 常驻建筑,门点总有值)
                string[] labels = { "进入小镇银行", "进入中央银行", "进入市政厅", "进入职业培训中心", "进入河畔工厂" };
                Vector3[] doors = { MapBuilder.BankDoor, MapBuilder.CentralBankDoor, MapBuilder.HallDoor, MapBuilder.TrainingDoor, MapBuilder.FactoryDoor };
                for (int i = 0; i < labels.Length; i++)
                {
                    Interactable door = FindByLabel(labels[i]);
                    Assert.IsNotNull(door, labels[i] + " 门口应注册(第三批转正)。");
                    Assert.AreEqual(doors[i], door.position, labels[i] + " 门口位置=门点属性。");
                    Assert.IsNotNull(door.onUse, labels[i] + " 回调应为进店(EnterInterior)。");
                }
                // 央行门口在未解锁(cc=0)时也注册——unlocked 门在 OpenCentralBank 面板回调内,
                // 与原料市场清晨门同口径(进店永远可行,功能门在店内)
                // 河畔工厂旧 label「查看河畔工厂」不得残留(直开面板时代的旧条目)
                Assert.IsNull(FindByLabel("查看河畔工厂"),
                    "旧「查看河畔工厂」条目应随 V0.9.13 转正消失(改名不留双条目)。");
            }
            finally
            {
                Interactables.All.Clear();
                Object.DestroyImmediate(mapObject);
            }
        }

        // ————— 店内柜台回调:五间互异且不落 default(果汁店营业面板) —————

        [Test]
        public void Batch3CounterCallbacks_IndependentFromDefault()
        {
            // 反射取回调不 Invoke(Invoke 会碰 UIManager 单例)。复审(tests):原 AreNotSame 比委托引用,
            // 但 C# 9(langversion 实证)无方法组转换缓存,每次 Invoke 各 new 委托实例,引用恒不等=重言式
            // (删 case 落 default 或误绑 OpenBank 都照绿)——改比委托身份 MethodInfo:
            // 落 default 时与 fallback 同方法会红;误绑时两 kind 同方法会红;lambda 各编译为不同生成方法
            System.Reflection.MethodInfo use = typeof(GameBootstrap).GetMethod("InteriorCounterUse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(use);
            System.Action fallback = (System.Action)use.Invoke(null, new object[] { "shop" });
            System.Action bank = (System.Action)use.Invoke(null, new object[] { "bank" });
            System.Action centralBank = (System.Action)use.Invoke(null, new object[] { "centralbank" });
            System.Action hall = (System.Action)use.Invoke(null, new object[] { "hall" });
            System.Action training = (System.Action)use.Invoke(null, new object[] { "training" });
            System.Action factory = (System.Action)use.Invoke(null, new object[] { "factory" });
            Assert.AreNotEqual(fallback.Method, bank.Method, "银行柜台不得回落 default(果汁店营业面板)。");
            Assert.AreNotEqual(fallback.Method, centralBank.Method, "央行柜台不得回落 default。");
            Assert.AreNotEqual(fallback.Method, hall.Method, "市政厅柜台不得回落 default。");
            Assert.AreNotEqual(fallback.Method, training.Method, "培训中心柜台不得回落 default。");
            Assert.AreNotEqual(fallback.Method, factory.Method, "工厂柜台不得回落 default。");
            // 政府三间是三个不同分页的 lambda,互异防误绑;银行/央行是不同方法组
            Assert.AreNotEqual(hall.Method, training.Method, "市政厅与培训中心是不同政府分页,回调应互异。");
            Assert.AreNotEqual(hall.Method, factory.Method, "市政厅与工厂是不同政府分页,回调应互异。");
            Assert.AreNotEqual(training.Method, factory.Method, "培训中心与工厂是不同政府分页,回调应互异。");
            Assert.AreNotEqual(bank.Method, centralBank.Method, "银行与央行是不同面板入口,回调应互异。");
        }

        // ————— 核心件抽查:各间命名件存在(布局辨识度下限,防布局函数被误删一整段) —————

        [Test]
        public void Batch3Rooms_SignatureFurniturePresent()
        {
            GameObject mapObject = new GameObject("V0913SignatureMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                // (kind, 件名):铜护栏柜台+等候椅阵/大理石柱+利率牌/政务台+旗杆/黑板+课桌+讲台/传送带+机床
                string[,] checks =
                {
                    { "bank", "counter" }, { "bank", "bench_4" },
                    { "centralbank", "counter" }, { "centralbank", "column_b" }, { "centralbank", "rate_board" },
                    { "hall", "counter" }, { "hall", "flag_a" },
                    { "training", "blackboard" }, { "training", "podium" }, { "training", "desk_1" },
                    { "factory", "conveyor" }, { "factory", "machine_a" },
                };
                for (int i = 0; i < checks.GetLength(0); i++)
                {
                    Transform furn = MapBuilder.InteriorRoom(checks[i, 0]).Find("furniture");
                    Assert.IsNotNull(furn.Find(checks[i, 1]),
                        checks[i, 0] + " 店内应有一件 \"" + checks[i, 1] + "\"(布局辨识度下限)。");
                }
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
