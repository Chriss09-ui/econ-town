using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>单栋建筑的几何规格（V0.8.1 PRD §162–163）。
    /// 画布尺寸、缩放、碰撞体、影子宽度、门偏移、挂牌锚点——BakeBuilding / WindowGlow / SnowCap /
    /// MapBuilder.Building 全部以此表为唯一来源；此前散落各处的 64×56、3.9×2.5、(0,-2.45) 等
    /// 魔法数字收编于此。约定：画布 8px 对齐、底边贴地（画布增高时调用方需相应下移中心保持基线）；
    /// 缩放不随体量变化——大画布 = 大建筑，像素密度与全镇一致（Point 采样不糊的前提）。
    /// 碰撞体 size/offset 为局部空间量，会被 transform.localScale(×scale) 放大。
    ///
    /// V0.9.6e 纪律《碰撞体下沿必须齐墙脚》：碰撞体下沿一律压到贴图基线（局部 −canvasH/32），
    /// 门点再取基线下 0.5。此前各档下沿都悬在墙脚之上（S 0.96 / M 1.66 / L 1.98 世界格），
    /// 那条带子贴图上画的是实心墙、物理上却能站人，而角色 y 低于建筑中心时排序在前——
    /// 全图 990 格（采样面的 17.8%）站上去就是"人站在房屋里"（用户截图复验，PRD §171.6）。
    ///
    /// V0.9.6g 纪律《碰撞体贴合可见轮廓》：碰撞体 = 「基线 → 中心上方 0.35」的横带 × 每栋实测宽。
    /// 上沿 0.35 = 排序临界（角色画在建筑前面的最大高度，SortingForY 取整临界 = 中心+0.2375）+ 裕量；
    /// 带之上角色排序画在建筑后面，本该被房子挡住，封死只会造出屋顶两侧的「看不见的墙」——
    /// 用户复验「房子的范围是不是设计的太大了，前面明明是空白却走不过去」：旧盒按档共享
    /// （S 3.9/M 4.85/L 7.0 局部）且上沿到中心上方 1.15~2.69 世界格（S 1.92/M 2.11/L 2.69），
    /// 我的家 17.7 格碰撞盒里 10.1 格是空白草地。
    /// 宽度一律按临界带内贴图相对中心的最大对称伸距逐行 alpha 实测（半像素不许悬空：
    /// 工厂/餐厅的锯齿檐口反而比旧盒宽 0.095，护栏的 0.25 采样网格恰好没踩到）。PRD §171.8。</summary>
    public class BuildingSpec
    {
        public int canvasW = 64, canvasH = 56;
        public float scale = 1.28f;
        // 未知 kind 的兜底值 = 临界带横带 × 最宽 S 档实测宽（3.9 局部 > 全部已登记 S painter，
        // 未知轮廓宁可多围不可漏围）；上沿 0.35/1.28=0.2734 局部。
        public Vector2 colliderSize = new Vector2(3.9f, 2.0234f);
        public Vector2 colliderOffset = new Vector2(0f, -0.7383f);
        public Vector3 doorOffset = new Vector3(0f, -2.74f, 0f);
        public Vector3 signAnchor = new Vector3(-1.15f, -0.55f, 0f);
        public int shadowWidth = 64;
        /// <summary>落影的局部 y（V0.9.6：加高档专用）。画布增高时中心随基线上移，
        /// 落影必须同步下移同样的量，否则影子会浮到墙面中段。基准档沿用历史值 −1.55。</summary>
        public float shadowLocalY = -1.55f;

        /// <summary>贴图世界半高（排序/顶部越界检查用）。</summary>
        public float WorldHalfHeight { get { return canvasH / (float)PixelArt.PPU * .5f * scale; } }

        /// <summary>贴图世界半宽。</summary>
        public float WorldHalfWidth { get { return canvasW / (float)PixelArt.PPU * .5f * scale; } }
    }

    /// <summary>体量档与 kind → 规格的唯一登记处（V0.8.1 PRD §163 三级体量）。
    /// S 小铺 64×56（现状默认）/ M 中型 88×72 / L 地标 112×88；
    /// V0.9.6 加 T 加高档（TallenS/TallenM，画布只往上长、footprint 不动）打破「都一样大」。
    /// 门偏移按档取值的原则：门点必须落在门面铺装格上、且在碰撞体前方留出站立位——
    /// V0.9.6e 起统一为「碰撞体下沿 = 贴图基线，门点 = 基线下 0.5」，
    /// 三档各自的落砖行已逐栋核验（PRD §164 坐标总表 / §171.6 墙脚纪律）。
    /// 碰撞体一律经 BandCollider() 按临界带横带生成，宽度逐栋实测传入（V0.9.6g，PRD §171.8）。</summary>
    public static class BuildingSpecs
    {
        /// <summary>V0.9.6g 临界带碰撞体：下沿 = 贴图基线（−canvasH/32 局部，V0.9.6e 纪律），
        /// 上沿 = 中心上方 0.35 世界格（排序临界 0.2375 + 裕量；带之上角色画在建筑后面，
        /// 留空可走是伪透视纵深），宽 = 每栋实测传入的局部量。加高档画布只往上长、
        /// 中心随基线上移，本公式天然含该补偿，无需再减 local。</summary>
        static BuildingSpec BandCollider(float scale, int canvasH, float widthLocal, BuildingSpec spec)
        {
            float top = 0.35f / scale, bottom = -canvasH / 32f;
            spec.colliderSize = new Vector2(widthLocal, top - bottom);
            spec.colliderOffset = new Vector2(0f, (top + bottom) * .5f);
            return spec;
        }

        static BuildingSpec S(float widthLocal)
        {
            return BandCollider(1.28f, 56, widthLocal, new BuildingSpec());
        }

        static BuildingSpec M(float widthLocal)
        {
            return BandCollider(1.28f, 72, widthLocal, new BuildingSpec
            {
                canvasW = 88, canvasH = 72,
                doorOffset = new Vector3(0f, -3.38f, 0f),
                signAnchor = new Vector3(-1.6f, -0.7f, 0f),
                shadowWidth = 88
            });
        }

        static BuildingSpec L(float widthLocal)
        {
            return BandCollider(1.28f, 88, widthLocal, new BuildingSpec
            {
                canvasW = 112, canvasH = 88,
                // V0.8.2d:−2.75→−3.6(门点脱离墙面);V0.9.6e:−3.6→−4.02。墙脚线在 −3.52,
                // 碰撞体下沿已压到墙脚,门点须再退 0.5 才有站立位。
                doorOffset = new Vector3(0f, -4.02f, 0f),
                signAnchor = new Vector3(-2.1f, -0.9f, 0f),
                shadowWidth = 112
            });
        }

        /// <summary>市政厅专用主地标档：宽高都明显压过 L 档金融副中心，
        /// 中心上移 0.32 世界格后仍保持旧门点与落影基线不变。</summary>
        static BuildingSpec CivicXL(float widthLocal)
        {
            return BandCollider(1.28f, 96, widthLocal, new BuildingSpec
            {
                canvasW = 128, canvasH = 96,
                doorOffset = new Vector3(0f, -4.34f, 0f),
                signAnchor = new Vector3(-2.4f, -1.15f, 0f),
                shadowWidth = 128,
                shadowLocalY = -1.8f
            });
        }

        /// <summary>V0.9.6 加高档 T：画布只往上长，宽度与碰撞 footprint 一律不动——
        /// 中心按 (canvasH 增量/2) 上移以保持基线，门/挂牌/落影反向补偿同样的量，
        /// 因此全镇净距与门砖一格不变。参数 grow = 相对基准档的增高像素（须 8px 对齐）。
        /// 注意两种口径：doorOffset 直接与世界坐标相加（补世界量），
        /// colliderOffset/signAnchor/shadowLocalY 是局部量——碰撞体现在由 BandCollider 按
        /// 新画布推导（下沿天然仍齐基线），signAnchor/shadowLocalY 照旧减 local。</summary>
        static BuildingSpec TallenS(int grow, float widthLocal)
        {
            float world = grow / 16f * 1.28f * .5f, local = world / 1.28f;
            return BandCollider(1.28f, 56 + grow, widthLocal, new BuildingSpec
            {
                canvasH = 56 + grow,
                doorOffset = new Vector3(0f, -2.74f - world, 0f),
                signAnchor = new Vector3(-1.15f, -0.55f - local, 0f),
                shadowLocalY = -1.55f - local
            });
        }

        static BuildingSpec TallenM(int grow, float widthLocal)
        {
            float world = grow / 16f * 1.28f * .5f, local = world / 1.28f;
            return BandCollider(1.28f, 72 + grow, widthLocal, new BuildingSpec
            {
                canvasW = 88, canvasH = 72 + grow,
                doorOffset = new Vector3(0f, -3.38f - world, 0f),
                signAnchor = new Vector3(-1.6f, -0.7f - local, 0f),
                shadowLocalY = -1.55f - local,
                shadowWidth = 88
            });
        }

        static readonly Dictionary<string, BuildingSpec> table = new Dictionary<string, BuildingSpec>
        {
            // 碰撞宽度 = 排序临界带内贴图相对中心的最大对称伸距（16 PPU 像素 alpha 逐行实测，
            // V0.9.6g）：shop/convenience 23px、competitor 24、market 30、factory 40、port 28、
            // hall 54、training/bank 38、restaurant 39、cinema 32、centralbank 55、decor_a 27、
            // decor_b/home 22、bookstore/empty 20（×2÷16 = 局部宽）。
            // —— S 小铺（V0.8.1 重构基准：全部 kind 先按现状 S 登记，升档逐栋改表）——
            { "shop", S(2.875f) },
            { "competitor", S(3f) },
            { "market", TallenS(16, 3.75f) },   // V0.9.6 体量分化:市场加高档 64×72(摊柱加长+凉棚上移+市场招牌)
            { "factory", M(5f) },   // V0.8.1 第 3 栋升档:河畔工厂 M(88×72)+独立大烟囱;V0.9.6g 宽 40px——锯齿檐口比旧盒还宽 0.095/侧,烟囱随迁(见 MapBuilder)
            { "port", S(3.5f) },
            { "hall", CivicXL(7.25f) },   // 市政厅独占 XL(128×96)，体量必须压过 L 档央行副中心
            { "training", TallenM(24, 4.75f) },   // V0.9.6 体量分化:校舍加高档 88×96(二层教室+钟塔,西区制高点)
            { "school", TallenS(8, 3.625f) },   // 社区学校 64×64：与培训楼组成双校舍校园，体量低一级
            { "convenience", S(2.875f) },
            // V0.9.6 餐厅升 M 时曾单独把门点压到基线下 0.27(当时 M 档默认 −2.6 还悬在墙面上);
            // V0.9.6e 起 M 档默认已是基线下 0.5,餐厅不再需要例外。
            { "restaurant", M(4.875f) },
            { "cinema", M(4f) },   // V0.8.1 第 2 栋升档:星光电影院 M(88×72)
            { "bank", M(4.75f) },   // V0.8.1 第 1 栋升档:小镇银行 M(88×72)
            { "centralbank", L(6.875f) },   // V0.8.1 第 5 栋升档:中央银行 L(112×88,穹顶制高点)
            { "decor_a", S(3.375f) },
            { "decor_b", S(2.75f) },
            { "bookstore", S(2.5f) },
            // 缩放非 1.28 的三栋:基线 = canvasH/32×scale,门点按各自基线取下 0.5(不能吃 S 档默认值)
            { "empty_a", BandCollider(1f, 56, 2.5f, new BuildingSpec { scale = 1f, doorOffset = new Vector3(0f, -2.25f, 0f) }) },
            { "empty_b", BandCollider(1f, 56, 2.5f, new BuildingSpec { scale = 1f, doorOffset = new Vector3(0f, -2.25f, 0f) }) },
            { "home", BandCollider(1.18f, 56, 2.75f, new BuildingSpec { scale = 1.18f, doorOffset = new Vector3(0f, -2.57f, 0f) }) }
        };

        /// <summary>未知 kind 走 S 档默认（与历史行为一致），已知 kind 必须已在表中登记。</summary>
        public static BuildingSpec Get(string kind)
        {
            BuildingSpec spec;
            return table.TryGetValue(kind, out spec) ? spec : new BuildingSpec();
        }

        public static int CanvasW(string kind) { return Get(kind).canvasW; }
        public static int CanvasH(string kind) { return Get(kind).canvasH; }

        /// <summary>已登记的 kind 集合（测试断言规格表全覆盖用）。</summary>
        public static IEnumerable<string> RegisteredKinds { get { return table.Keys; } }
    }
}
