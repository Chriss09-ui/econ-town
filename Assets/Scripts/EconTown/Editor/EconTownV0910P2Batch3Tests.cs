#if UNITY_EDITOR

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EconTown
{
    /// <summary>围猎三第三批(P2 演出/UI/Art,14 项)的回归测试:
    /// WeatherParticles 停止路径报 0/ParticleBudget 域重载复位/政策图负价不产 NaN/
    /// 工厂锯齿雪带逐行贴坡/小铺伞正立+雪带镜像/我的家阁楼窗不被雪盖/Toast 居面板与过场之上
    /// 逐条钉住(-0% 三处、图表数值单位文案、NPC 生成世界区域守卫、云团左回流为内联呈现逻辑,
    /// 由编译、渲图复验与对抗复审覆盖)。</summary>
    public class EconTownV0910P2Batch3Tests
    {
        [TearDown]
        public void TearDown()
        {
            // 直接调 OnDestroy() 会把静态 I 置 null,按类型兜底清理残留对象
            WeatherParticles[] weathers = Object.FindObjectsByType<WeatherParticles>(FindObjectsInactive.Include);
            for (int i = 0; i < weathers.Length; i++) Object.DestroyImmediate(weathers[i].gameObject);
            if (UIManager.I != null) Object.DestroyImmediate(UIManager.I.gameObject);
            ParticleBudget.ResetForTest();
        }

        // ————— #1/#16:WeatherParticles 全部停止路径向账本报 0 —————

        [Test]
        public void WeatherParticles_AllStopPaths_ReportZeroToBudget()
        {
            WeatherParticles.Create();
            Assert.IsNotNull(WeatherParticles.I);
            MethodInfo clear = typeof(WeatherParticles).GetMethod("ClearAllAndReportZero",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(clear, "三处早退+OnDestroy 必须收统一出口 ClearAllAndReportZero(与已修店内早退同型)。");
            ParticleBudget.Report("weather", 5);
            clear.Invoke(WeatherParticles.I, null);
            Assert.AreEqual(0, ParticleBudget.ReportedBy("weather"),
                "停止路径必须把「weather」键清零(旧实现各早退只 ClearAll 不报数,账本键长期虚高)。");
            ParticleBudget.Report("weather", 7);
            // EditMode 的 DestroyImmediate 不触发 MonoBehaviour.OnDestroy(引擎行为),
            // 直接调方法体验证「场景卸载/切世界」路径的清账逻辑。
            WeatherParticles.I.OnDestroy();
            Assert.AreEqual(0, ParticleBudget.ReportedBy("weather"),
                "OnDestroy(场景卸载/切世界)也须清账,否则账本跨场景残留。");
        }

        // ————— #17:ParticleBudget 静态账本的域重载复位 —————

        [Test]
        public void ParticleBudget_SubsystemRegistration_ResetClearsLedger()
        {
            ParticleBudget.Report("weather", 120);
            ParticleBudget.Report("dust", 30);
            Assert.Greater(ParticleBudget.TotalActive, 0);
            MethodInfo reset = typeof(ParticleBudget).GetMethod("ResetStatics",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset,
                "项目关闭 Domain Reload,静态账本必须有 SubsystemRegistration 复位钩子(照 UiTheme 等先例)。");
            reset.Invoke(null, null);
            Assert.AreEqual(0, ParticleBudget.TotalActive,
                "复位后上一场残留计数不得带进下一场(开场即误报超出全局上限)。");
        }

        // ————— #19:政策图供需函数对负价钳正,不再产 NaN 顶点 —————

        [Test]
        public void PolicyGraph_NegativePrice_Clamped_NoNaNVertices()
        {
            TaxGraphModel tax = new TaxGraphModel(default(PriceQuote), 0f);
            // 负价/楔后负价:旧实现 Pow(负底,非整指数)=NaN,DrawCurve 的 q<=0 比较恒 false 拦不住。
            Assert.IsFalse(float.IsNaN(tax.DemandAt(-5f)) || float.IsInfinity(tax.DemandAt(-5f)));
            Assert.IsFalse(float.IsNaN(tax.SupplyAt(-5f)) || float.IsInfinity(tax.SupplyAt(-5f)));
            Assert.IsFalse(float.IsNaN(tax.ShiftedSupplyAt(-5f)) || float.IsInfinity(tax.ShiftedSupplyAt(-5f)));
            ExternalityGraphModel ext = new ExternalityGraphModel(default(PriceQuote), 0f, 2f);
            Assert.IsFalse(float.IsNaN(ext.socialPrice) || float.IsNaN(ext.socialQuantity));
        }

        // ————— #13:世界区域守卫默认放行(正常小镇生成不被锁死) —————

        [Test]
        public void WorldAreaGuards_DefaultFalse_TownSpawningNotLockedOut()
        {
            Assert.IsFalse(PortSceneController.IsInsidePort,
                "无港区控制器实例时守卫必须放行(否则补刷/营业顾客全灭)。");
            Assert.IsFalse(ShopInteriorController.IsInsideShopInterior,
                "无店内控制器实例时守卫必须放行。");
        }

        // ————— #21:工厂锯齿雪带逐行贴坡 —————

        [Test]
        public void SnowLines_Factory_PerRowFollowsSawtoothSlope()
        {
            PixelArt.SnowBand[] bands = PixelArt.BuildingSnowLines("factory");
            Assert.AreEqual(16, bands.Length,
                "四齿 × 每 3 行一条(k=0/3/6/9),旧的固定 topY 41/48 两条/齿已废。");
            for (int tooth = 0; tooth < 4; tooth++)
            {
                int bx = 4 + tooth * 20;
                // 坡面行 k 的起点 xs = bx+(k*19+11)/12,雪带 (xs+1, bx+18, 39+k) 逐行跟随坡面高度
                int[] xsAtK = { 0, 5, 10, 15 };
                for (int j = 0; j < 4; j++)
                {
                    int k = j * 3;
                    PixelArt.SnowBand expect = new PixelArt.SnowBand(bx + xsAtK[j] + 1, bx + 18, 39 + k);
                    bool found = false;
                    for (int i = 0; i < bands.Length; i++)
                        if (bands[i].x0 == expect.x0 && bands[i].x1 == expect.x1 && bands[i].topY == expect.topY)
                        { found = true; break; }
                    Assert.IsTrue(found, "齿 " + tooth + " 第 k=" + k + " 行应登记 " +
                        "(" + expect.x0 + "," + expect.x1 + "," + expect.topY + "),实际:" + Describe(bands));
                }
                // 坡左段(x≤bx+6)高度仅 y38-41:本齿雪带不得在此悬空(旧 (bx,bx+6,41) 雪体 y39-41 悬在 y38 坡面上方)
                for (int i = 0; i < bands.Length; i++)
                {
                    PixelArt.SnowBand b = bands[i];
                    if (b.x0 < bx || b.x0 > bx + 19) continue;   // 只查本齿的带(邻齿 x 段不属本坡)
                    Assert.IsFalse(b.x0 <= bx + 6 && b.topY > 41 + 1,
                        "雪带 (" + b.x0 + "," + b.x1 + "," + b.topY + ") 在坡左段悬空(x0≤" + (bx + 6) + " 且 topY>42)。");
                }
            }
        }

        // ————— #22:小铺伞正立(上尖下宽)+雪带镜像 —————

        [Test]
        public void ParasolKiosk_UprightCanopy_MirroredSnowBands()
        {
            Sprite sprite = PixelArt.Building("decor_a");
            Assert.IsNotNull(sprite);
            Texture2D t = sprite.texture;
            // y 向上:窄行(28-36)在 y28(伞尖),宽行(14-50)在 y24(伞缘)。
            // 旧写法 24+i 把宽行画在最高处=上宽下尖倒漏斗,伞尖/垂穗随之错乱。
            Assert.Greater(t.GetPixel(32, 28).a, 0f, "伞尖行中央应有伞面。");
            Assert.AreEqual(0f, t.GetPixel(14, 28).a, .01f, "y28 处 x14 应在窄行外(伞尖段不得有宽伞)。");
            Assert.Greater(t.GetPixel(14, 24).a, 0f, "y24 应是宽缘行(x14 有伞)。");
            Assert.Greater(t.GetPixel(50, 24).a, 0f, "宽缘行右端 x50 有伞。");
            Assert.AreEqual(0f, t.GetPixel(14, 26).a, .01f,
                "支伞杆顶收到宽缘 y24,y26 的 x14 不再穿出伞面(旧杆顶 28)。");
            Assert.Greater(t.GetPixel(14, 23).a, 0f, "垂穗下移到宽缘之下 y23。");

            PixelArt.SnowBand[] bands = PixelArt.BuildingSnowLines("decor_a");
            Assert.AreEqual(3, bands.Length);
            bool hasWide = false, hasMid = false, hasNarrow = false;
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].x0 == 14 && bands[i].x1 == 50 && bands[i].topY == 24) hasWide = true;
                if (bands[i].x0 == 20 && bands[i].x1 == 44 && bands[i].topY == 26) hasMid = true;
                if (bands[i].x0 == 28 && bands[i].x1 == 36 && bands[i].topY == 27) hasNarrow = true;
            }
            Assert.IsTrue(hasWide && hasMid && hasNarrow,
                "雪带沿伞面铺雪(topY=伞面行,雪体向上覆 3px):宽缘 24/中段 26/窄顶 27。实际:" + Describe(bands));
            // 金色伞尖(y30)必须露出窄顶雪体(27..29)之上
            Assert.AreEqual(0f, PixelArt.SnowCap("decor_a").texture.GetPixel(31, 30).a, .05f,
                "伞尖行 y30 不得落雪(窄顶雪体只到 y29)。");
            Assert.Greater(t.GetPixel(31, 30).a, 0f, "护栏:伞尖金色两点在 y30。");
        }

        // ————— #23 复审:书店二层书房窗不被雨棚雪带盖没(镜像族回扫) —————

        [Test]
        public void BookstoreWindows_NotCoveredByAwningSnowBand()
        {
            // 两扇窗 x20-27/x36-43,y18-24;旧整条 (16,39,19) 雪体 y19-21 横切两窗顶部,
            // 雪 overlay 排序 +2 后必然盖掉窗光——照阁楼窗口径收缩到窗两侧。
            // 契约区只钉雨棚雪带修复的核心行 y19-22:窗顶 y23-25 与底 y18 是女儿墙雪带
            // (12,51,26) 垂坠(topY-1..-3)的随机落点,零星碎雪是 SnowCap 正常输出、不属「盖窗」
            // (与 home 测试同口径,不赌 FnvSeed 序列)。
            Texture2D snowTex = PixelArt.SnowCap("bookstore").texture;
            for (int y = 19; y <= 22; y++)
                for (int x = 20; x <= 27; x++)
                    Assert.AreEqual(0f, snowTex.GetPixel(x, y).a, .05f,
                        "雪帽贴图 (" + x + "," + y + ") 触及左书房窗。");
            for (int y = 19; y <= 22; y++)
                for (int x = 36; x <= 43; x++)
                    Assert.AreEqual(0f, snowTex.GetPixel(x, y).a, .05f,
                        "雪帽贴图 (" + x + "," + y + ") 触及右书房窗。");
            // 护栏:雨棚两端仍留雪(收缩不是整条删除)
            Assert.Greater(snowTex.GetPixel(17, 20).a, 0f, "雨棚左段雪保留。");
            Assert.Greater(snowTex.GetPixel(31, 20).a, 0f, "两窗之间棚段雪保留。");
        }

        // ————— #23:我的家阁楼圆窗不被雪带盖没 —————

        [Test]
        public void HomeAtticWindow_NotCoveredBySnowBands()
        {
            PixelArt.SnowBand[] bands = PixelArt.BuildingSnowLines("home");
            // 窗矩形 x30-34,y30-34(含框);雪体覆 topY-2..topY,与窗相交即 topY∈[30,36] 且 x 区间重叠。
            for (int i = 0; i < bands.Length; i++)
            {
                PixelArt.SnowBand b = bands[i];
                bool touchesWindow = b.topY >= 30 && b.topY <= 36 && b.x0 <= 35 && b.x1 >= 29;
                Assert.IsFalse(touchesWindow, "雪带 (" + b.x0 + "," + b.x1 + "," + b.topY +
                    ") 仍盖在阁楼窗上——登记窗前应收缩到窗两侧。");
            }
            bool leftKept = false, rightKept = false, eaveKept = false;
            for (int i = 0; i < bands.Length; i++)
            {
                PixelArt.SnowBand b = bands[i];
                if (b.x0 == 23 && b.x1 == 28 && b.topY == 32) leftKept = true;    // 窗左坡面雪保留
                if (b.x0 == 36 && b.x1 == 41 && b.topY == 32) rightKept = true;   // 窗右坡面雪保留
                if (b.x0 == 11 && b.x1 == 53 && b.topY == 23) eaveKept = true;    // 屋檐底行不受影响
            }
            Assert.IsTrue(leftKept && rightKept, "收缩后窗两侧坡面雪应保留(收缩不是整条删除)。");
            Assert.IsTrue(eaveKept, "屋檐底部雪带不受阁楼窗收缩影响。");
            // 黑盒:雪帽贴图在窗矩形(上下各 1px 边距)内必须全透明——雪体向上覆 3px、垂坠向下探
            // 3px,触及窗的带(topY∈[28,37])都要收缩,旧的只按「向下覆」判定漏掉了 topY=29。
            // 契约区只到窗±1px:更远的垂坠碎雪(topY-3 以内)是 SnowCap 的正常输出,不属「盖窗」
            // ——钉太宽会赌 FnvSeed 随机流的运气(复审:屋脊带垂坠 depth 恰为 0 才绿)。
            Texture2D snowTex = PixelArt.SnowCap("home").texture;
            for (int y = 30; y <= 35; y++)
                for (int x = 29; x <= 35; x++)
                    Assert.AreEqual(0f, snowTex.GetPixel(x, y).a, .05f,
                        "雪帽贴图 (" + x + "," + y + ") 触及阁楼窗。");
        }

        // ————— #18:Toast 浮在面板与过场层之上 —————

        [Test]
        public void UIManager_Toast_StaysAbovePanelsAndCutscene()
        {
            UIManager ui = UIManager.Create();
            Assert.IsNotNull(ui);
            RectTransform cutscene = ui.AcquireCutsceneLayer();   // 触发 InitializeUi(panels<cutsceneLayer<toasts)
            Assert.IsNotNull(cutscene);
            Transform canvas = cutscene.transform.parent;
            Transform toasts = canvas.Find("toasts");
            Assert.IsNotNull(toasts, "护栏:Toast 容器存在。");
            Assert.AreEqual(canvas.childCount - 1, toasts.GetSiblingIndex(),
                "初始化后 Toast 应压轴(旧顺序 toasts<panels<cutsceneLayer,面板一开就把当日提示盖到过期)。");
            cutscene.SetAsLastSibling();                          // 模拟其他系统置顶过场层
            ui.AcquireCutsceneLayer();
            Assert.AreEqual(canvas.childCount - 1, canvas.Find("toasts").GetSiblingIndex(),
                "AcquireCutsceneLayer 置顶过场层后必须把 Toast 重新压回其上。");
            ui.ReleaseCutsceneLayer();
            // 复审补:BeginPanel 每次开面板都置顶 panelRoot——若不跟随压回 Toast,
            // 在场提示被全屏 modalShade 盖到淡出(全库 62 个面板入口都走它)。
            MethodInfo beginPanel = typeof(UIManager).GetMethod("BeginPanel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(beginPanel);
            beginPanel.Invoke(ui, new object[] { "测试面板", 400f, 300f });
            Assert.AreEqual(canvas.childCount - 1, canvas.Find("toasts").GetSiblingIndex(),
                "BeginPanel 置顶 panels 后 Toast 仍须压回其上。");
        }

        static string Describe(PixelArt.SnowBand[] bands)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < bands.Length; i++)
                sb.Append("(").Append(bands[i].x0).Append(",").Append(bands[i].x1)
                  .Append(",").Append(bands[i].topY).Append(")");
            return sb.ToString();
        }
    }
}
#endif
