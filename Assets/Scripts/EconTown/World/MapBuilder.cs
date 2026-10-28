using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    /// <summary>世界区域:交互点归属与玩家交互扫描的过滤键(V0.9.7 起店内为第三区)。</summary>
    public enum WorldArea { Town, Port, ShopInterior }

    /// <summary>可交互点:建筑门口、NPC 等。</summary>
    public class Interactable
    {
        public string label;
        public Vector3 position;
        public System.Action onUse;
        public WorldArea area;
    }

    public static class Interactables
    {
        public static readonly System.Collections.Generic.List<Interactable> All =
            new System.Collections.Generic.List<Interactable>();

        public static Interactable Register(string label, Vector3 pos, System.Action onUse,
            WorldArea area = WorldArea.Town)
        {
            Interactable it = new Interactable { label = label, position = pos, onUse = onUse, area = area };
            All.Add(it);
            return it;
        }

        /// <summary>按标签注销首个匹配条目(V0.9.12)——区段门控条目读档回滚时对齐存档状态用;
        /// RegisterDistrictInteractables 的「补注册」从此有了对偶的「补注销」。</summary>
        public static void Remove(string label)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].label == label)
                {
                    All.RemoveAt(i);
                    return;
                }
        }
    }

    /// <summary>Kenney(CC0)像素小镇:草地/沥青路/人行道/花砖广场 + 拼合建筑 + 装饰。</summary>
    public static class MapBuilder
    {
        // 地块化重构：扩展的 72×42 主城先划法定地块，再放建筑和道具。
        // 港区仍为独立场景，不随主城扩图改变。
        public const int W = 72, H = 42;
        public const int OriginX = -36;
        public const int TownMaxX = 35;
        public const int PortW = 32, PortH = 20, PortOriginX = -16;
        public const int PortYardMinX = -16, PortYardMaxX = 6;
        public const int PortShoreX = 7;
        public const int PortSeaMinX = 8, PortSeaMaxX = 15;
        public const float WorldMinX = OriginX - .5f;
        public const float WorldMaxX = OriginX + W + .5f;
        public const float WorldMinY = -H / 2f - .5f;
        public const float WorldMaxY = H / 2f + .5f;
        public const float PortWorldMinX = PortOriginX - .5f;
        public const float PortWorldMaxX = PortOriginX + PortW + .5f;
        public const float PortWorldMinY = -PortH / 2f - .5f;
        public const float PortWorldMaxY = PortH / 2f + .5f;
        // ————— V0.9.7 果汁店店内(独立世界根;门口按 E 由 ShopInteriorController 换场)—————
        // V0.9.11 店内体系泛化:单一 ShopInteriorRoot 下每店一个子房间(interior_<kind>),
        // 房间在世界坐标 +40x 网格错开(见 InteriorCatalog);几何/主题/锚点唯一登记处在 InteriorCatalog。
        // 相机 clamp 边界 = 当前激活房间矩形:窗口比房间宽时 ClampInsideMap 自动居中成「盒子视图」。
        // 果汁店 16×11(首稿 24×14 渲染复看太空,缩一间小店并全自绘家具);新店 18×12。
        public const int InteriorW = 16, InteriorH = 11;   // 果汁店房间尺寸(V097 测试引用,保留)
        // 四边界改为按当前激活店查表(原 V0.9.7 const;唯一读点 CameraFollow 两处三元链,签名不变零改动)
        public static float InteriorMinX { get { return InteriorBounds(ActiveInteriorKind).x; } }
        public static float InteriorMaxX { get { return InteriorBounds(ActiveInteriorKind).z; } }
        public static float InteriorMinY { get { return InteriorBounds(ActiveInteriorKind).y; } }
        public static float InteriorMaxY { get { return InteriorBounds(ActiveInteriorKind).w; } }
        public const float NpcWanderMinX = -34f, NpcWanderMaxX = 33f;
        public const float NpcWanderMinY = -19f, NpcWanderMaxY = 18f;
        public static Vector3 ShopDoor { get; private set; }
        public static Vector3 MarketDoor { get; private set; }
        public static Vector3 CompetitorDoor { get; private set; }
        public static Vector3 BankDoor { get; private set; }
        public static Vector3 CentralBankDoor { get; private set; }
        public static Vector3 PortDoor { get; private set; }      // V0.7 国际贸易港
        public static Vector3 CustomsDoor { get; private set; }   // V0.7.2 报关行黑板
        public static Vector3 PortExitDoor { get; private set; }
        public static Vector3 PortEntryPosition { get; private set; }
        public static Vector3 HallDoor { get; private set; }
        public static Vector3 FactoryDoor { get; private set; }
        public static Vector3 TrainingDoor { get; private set; }
        public static Vector3 HomeDoor { get; private set; }
        public static Vector3 RestaurantDoor { get; private set; }
        public static Vector3 CinemaDoor { get; private set; }
        public static Vector3 ConvenienceDoor { get; private set; }
        public static Vector3 BookstoreDoor { get; private set; }   // V0.9.12 东商业新区(随章节预建,门属性总有值)
        public static Vector3 DecorADoor { get; private set; }      // V0.9.12 零嘴亭(装饰铺转正)
        public static Vector3 DecorBDoor { get; private set; }      // V0.9.12 花店(装饰铺转正)
        public static Vector3 PlazaCenter { get; private set; }

        // ————— 主城建筑摆放表（PRD §162/§164；双街一轴重构）—————
        // 对外兼容入口保持不变；实际坐标统一收在 TownLayoutData，附属物从同一锚点推导。

        public struct BuildingPlacement
        {
            public string kind;
            public string name;
            public Vector3 center;
            public bool interactive;
        }

        public static readonly BuildingPlacement[] TownPlacements = TownLayoutData.TownPlacements;

        static Texture2D whiteTex;
        static Sprite whiteSprite;
        static Transform publicProjectsRoot;
        // V0.7.1 季节重建所需静态引用(照 publicProjectsRoot 先例):Ground Tilemap 与 Town root
        // 原为 Build() 局部变量,换季刷贴图时按名/引用定位。全程禁调 PixelArt.ResetCache(PRD §122)。
        static Transform townRoot;
        public static Transform TownRoot => townRoot;
        public static Transform PortDistrictRoot { get; private set; }
        public static Transform ShopInteriorRoot { get; private set; }      // V0.9.7 店内世界根(V0.9.11 起根下多间子房间)
        // 三锚点改为按当前激活店查表(原 V0.9.7 静态字段;默认 shop,V097 断言原样过)
        public static Vector3 InteriorEntryPosition { get { return InteriorCatalog.Get(ActiveInteriorKind).EntryWorld; } }
        public static Vector3 InteriorCounterSpot { get { return InteriorCatalog.Get(ActiveInteriorKind).CounterWorld; } }
        public static Vector3 InteriorExitSpot { get { return InteriorCatalog.Get(ActiveInteriorKind).ExitWorld; } }
        // V0.9.11 子房间登记:kind → interior_<kind> 根(装配时写入;Controller 换场/SceneShot/测试按名激活)
        static readonly System.Collections.Generic.Dictionary<string, Transform> interiorRooms =
            new System.Collections.Generic.Dictionary<string, Transform>();
        public static string ActiveInteriorKind { get; private set; } = "shop";
        /// <summary>仅 ShopInteriorController 换场时调用;未知 kind 拒绝(保持原值)。</summary>
        public static void SetActiveInteriorKind(string kind)
        {
            if (!InteriorCatalog.Exists(kind)) return;
            ActiveInteriorKind = kind;
        }
        public static Transform InteriorRoom(string kind)
        {
            Transform room;
            interiorRooms.TryGetValue(kind, out room);
            return room;
        }
        /// <summary>房间矩形(世界 minX/minY/maxX/maxY)=相机 clamp 域。</summary>
        public static Vector4 InteriorBounds(string kind) { return InteriorCatalog.Get(kind).RoomBounds; }
        // V0.7.3 三处新区(施工/完工两态由 RefreshGrowthDistricts 按 completedChapter 切换)
        public static Transform GrowthEastRoot { get; private set; }
        public static Transform GrowthPlazaRoot { get; private set; }
        public static Transform GrowthFinancialRoot { get; private set; }
        public static Vector3 ConceptBoardSpot { get; private set; }   // 书店门口小黑板(按 E/点击)
        public static Vector3 CivicSteleSpot { get; private set; }     // 行政广场公告碑
        public static Vector3 RateBoardSpot { get; private set; }      // 央行前政策利率牌
        public static Transform GrowthFlagCloth { get; private set; }  // 旗面(染色=财政立场)
        static Transform growthFlagPole;
        static SpriteRenderer bankFacadeLights;                        // 金融街:银行外立面亮化 overlay
        static int appliedGrowthSeason = -1;
        static Tilemap groundTilemap;
        static readonly System.Collections.Generic.Dictionary<Tile, int> grassTileVariant =
            new System.Collections.Generic.Dictionary<Tile, int>();
        static int appliedSeason = -1;

        // ————— V0.7.1 经济状态可视化:建筑外观注册表(窗光/转让牌/告示/价目牌/杂物) —————

        class ShopFacade
        {
            public string kind;
            public Transform building;
            public SpriteRenderer glow;       // 橱窗暖光(α 由 AtmosphereDirector 按 信心档×夜幕 驱动)
            public bool isPlayerShop;
            public int decorativeIndex = -1;  // 纯装饰铺的固定池索引(转让牌候选),交互建筑为 -1
            public GameObject signHolder;     // 动态挂牌(transfer/hours_notice)
            public string signKind = "";
        }

        class PriceBoard
        {
            public TextMesh text;
            public int series;                // 0食品 1其他 2娱乐(CPI 分项指数投影)
            public float basePriceYuan;
            public string label;
            public float lastIndex = float.NaN;   // 围猎五#6:差值短路缓存(指数只在收日结算变化)
        }

        static readonly System.Collections.Generic.List<ShopFacade> facades =
            new System.Collections.Generic.List<ShopFacade>();
        static readonly System.Collections.Generic.List<PriceBoard> priceBoards =
            new System.Collections.Generic.List<PriceBoard>();
        static GameObject overheatClutter;
        static Material unlitSpriteMaterial;   // 窗光等自发光 overlay 用(Sprites/Default 不受 Light2D 影响)

        public static void Build(Transform parent)
        {
            Interactables.All.Clear();
            ActiveInteriorKind = "shop";   // 复审(V0911):场景重载(LoadScene)不触发 SubsystemRegistration,
                                            // 店内「重新开始」后重建时在此归零,防锚点属性/注册读到残留 kind
            GameObject root = new GameObject("Town");
            root.transform.SetParent(parent, false);
            townRoot = root.transform;
            appliedSeason = -1;
            festivalShown = false;
            festivalNight = false;
            facades.Clear();
            priceBoards.Clear();
            overheatClutter = null;

            root.AddComponent<Grid>();
            GameObject groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(root.transform, false);
            Tilemap tl = groundGo.AddComponent<Tilemap>();
            TilemapRenderer tr = groundGo.AddComponent<TilemapRenderer>();
            tr.sortingOrder = -1000;
            groundTilemap = tl;

            int season = GameState.I != null ? AtmosphereService.SeasonIndex(GameState.I.day) : 0;
            Tile[] grass = { MakeGrassTile(season, 0), MakeGrassTile(season, 1), MakeGrassTile(season, 2) };
            Tile[] road = { MakeTile(PixelArt.Road(0)), MakeTile(PixelArt.Road(1)), MakeTile(PixelArt.Road(2)) };
            Tile[] walk = { MakeTile(PixelArt.Plaza(0)), MakeTile(PixelArt.Plaza(1)) };
            Tile[] plaza = { MakeTile(PixelArt.Plaza(0)), MakeTile(PixelArt.Plaza(1)) };

            var rng = new System.Random(20260818);
            Vector3Int origin = new Vector3Int(OriginX, -H / 2, 0);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    Tile t = grass[rng.Next(100) < 72 ? 0 : rng.Next(3)];
                    tl.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), t);
                }

            // 双街一轴：全镇只保留两条东西机动车街与一条市政中轴，其余全部降为步行巷。
            PaintHorizontalStreet(tl, road, walk, rng, OriginX, TownMaxX, TownLayoutData.MainStreetY);
            PaintHorizontalStreet(tl, road, walk, rng, OriginX, TownMaxX, TownLayoutData.TradeStreetY);
            PaintVerticalStreet(tl, road, walk, rng, TownLayoutData.CivicAxisLeftX,
                TownLayoutData.CivicAxisMinY, TownLayoutData.CivicAxisMaxY, true);

            // 市民广场是市政院外的公共空间，不再与市政厅院内铺装混成一片。
            for (int x = Mathf.RoundToInt(TownLayoutData.PlazaBounds.xMin);
                 x <= Mathf.RoundToInt(TownLayoutData.PlazaBounds.xMax); x++)
                for (int y = Mathf.RoundToInt(TownLayoutData.PlazaBounds.yMin);
                     y <= Mathf.RoundToInt(TownLayoutData.PlazaBounds.yMax); y++)
                    tl.SetTile(new Vector3Int(x, y, 0), plaza[((x + y) % plaza.Length + plaza.Length) % plaza.Length]);
            PlazaCenter = TownLayoutData.PlazaCenter;

            // 生活巷与金融步道各自成片，并通过一条明确连接巷接入主街；广场两侧仍留草地断口。
            for (int i = 0; i < TownLayoutData.PedestrianPaths.Length; i++)
            {
                TownPathRect path = TownLayoutData.PedestrianPaths[i];
                PaintPath(tl, walk, rng, path.x0, path.y0, path.x1, path.y1);
            }

            PaintParcelGround(tl);
            BuildLandmarkCompounds(root.transform);

            // 南部商贸街:果汁竞争→原料市场→工厂→港口，经济链条由西向东逐步展开。
            // V0.8.1：核心建筑坐标提取为 TownPlacements 静态表（可测试数据，PRD §162）；
            // 升档迁移只改表与规格表，Build 循环与门属性赋值不动。
            var doors = new System.Collections.Generic.Dictionary<string, Vector3>();
            for (int i = 0; i < TownPlacements.Length; i++)
                doors[TownPlacements[i].kind] = Building(root.transform, TownPlacements[i].kind,
                    TownPlacements[i].name, TownPlacements[i].center, TownPlacements[i].interactive);

            ShopDoor = doors["shop"];
            CompetitorDoor = doors["competitor"];
            MarketDoor = doors["market"];
            FactoryDoor = doors["factory"];
            PortDoor = doors["port"];

            // 中央市政区:市政厅是广场的正面视觉锚点，文教与便民服务分居主街两侧。
            HallDoor = doors["hall"];
            TrainingDoor = doors["training"];
            ConvenienceDoor = doors["convenience"];

            // 西北生活区与广场两侧的餐饮娱乐，保持居住、公共与商业的步行距离。
            HomeDoor = doors["home"];
            RestaurantDoor = doors["restaurant"];
            CinemaDoor = doors["cinema"];

            // 东北金融区:商业银行靠近市场，中央银行位于较安静的城市东缘。
            BankDoor = doors["bank"];
            CentralBankDoor = doors["centralbank"];

            // V0.7.1 两间纯装饰小铺(PRD §125):作为社区店分布在西北支路和主街路口。
            // V0.9.6 错落:两间铺子各自脱开所在街线(北排退 0.6 / 中排前移 0.6),不设门无门砖约束。
            // V0.9.6d 匀开:decor_a 西移 0.2、decor_b 西移 0.55——decor_b 原 −22.5 的贴图与校舍**重叠 0.42**
            // (中排最刺眼的一处),西移后净距 1.13;再往西不行,东西边界断言钉在 ±23。
            // V0.9.12 装饰铺转正:门点属性化(零嘴亭/花店可进店,门口 E=进店)
            DecorADoor = Building(root.transform, "decor_a", "临街小铺", TownLayoutData.DecorACenter);
            DecorBDoor = Building(root.transform, "decor_b", "临街小铺", TownLayoutData.DecorBCenter);

            Decorate(root.transform);
            publicProjectsRoot = new GameObject("PublicProjects").transform;
            publicProjectsRoot.SetParent(root.transform, false);
            RefreshGovernmentProjects(GameState.I);
            BuildGrowthDistricts(root.transform);   // V0.7.3 三处新区(内容全量预建,可见性随章节切换)
            BuildHousingPlaque(root.transform);     // V0.9.3 要素①:自家门侧三态铭牌(§196.6)
            BuildHousingStartsSite(root.transform); // V0.9.3 要素②:Inflating 新开工地块(§196.6)

            float mapCenterX = (WorldMinX + WorldMaxX) * .5f;
            MakeWall(root.transform, new Vector3(mapCenterX, WorldMaxY, 0), new Vector2(W + 2, 1));
            MakeWall(root.transform, new Vector3(mapCenterX, WorldMinY, 0), new Vector2(W + 2, 1));
            MakeWall(root.transform, new Vector3(WorldMinX, 0, 0), new Vector2(1, H + 2));
            MakeWall(root.transform, new Vector3(WorldMaxX, 0, 0), new Vector2(1, H + 2));

            appliedSeason = season;
            if (season == 3) SetSnowOverlays(true);   // 冬季开局直接带雪帽
            EnsurePriceBoards();
            BuildPortScene(parent);
            BuildShopInterior(parent);   // V0.9.7 店内世界根(门口按 E 换场进出)
        }

        static Vector3 Building(Transform parent, string kind, string name, Vector3 center, bool interactive = true)
        {
            // V0.8.1：缩放/影子/碰撞体/门偏移全部读 BuildingSpecs 规格表（PRD §162）
            BuildingSpec spec = BuildingSpecs.Get(kind);
            GameObject go = new GameObject("B_" + name + "_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            SpriteRenderer shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, spec.shadowLocalY, 0f);
            shadow.sprite = PixelArt.BuildingShadow(spec.shadowWidth);
            shadow.sortingOrder = SortingForY(center.y) - 1;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Building(kind);
            sr.sortingOrder = SortingForY(center.y);
            go.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = spec.colliderSize;
            col.offset = spec.colliderOffset;

            BuildingIdentity identity = go.AddComponent<BuildingIdentity>();
            identity.kind = kind;
            identity.interactive = interactive;

            // V0.9.6g:行走碰撞体收成「基线→中心+0.35」临界带横带(§171.8)后,OnMouseDown 的
            // 点击命中区跟着变小——点屋顶/点墙上部会没反应(旧盒时代整盒可点)。另配一张
            // isTrigger 的整贴图盒碰撞体专供点击:触发体不参与物理阻挡(从屋顶两侧绕行房后
            // 不受影响),LayoutGuard 收集净距账时跳过 isTrigger(PRD §171.8 纪律 4)。
            if (interactive)
            {
                BoxCollider2D clickArea = go.AddComponent<BoxCollider2D>();
                clickArea.isTrigger = true;
                clickArea.size = new Vector2(spec.canvasW / (float)PixelArt.PPU, spec.canvasH / (float)PixelArt.PPU);
            }

            // V0.7.1 橱窗暖黄窗光:与建筑贴图同几何的发光 overlay,α 由 AtmosphereDirector 驱动。
            // 必须用不受 Light2D 全局光照制的 unlit 材质——否则夜幕把窗光同步压暗,
            // "夜幕+窗光"的对比效果永远出不来(端到端截图实测发现的缺陷)。
            SpriteRenderer glow = new GameObject("window_glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(go.transform, false);
            glow.sprite = PixelArt.WindowGlow(kind);
            glow.sortingOrder = sr.sortingOrder + 1;
            glow.color = new Color(1f, 1f, 1f, 0f);
            if (unlitSpriteMaterial == null)
                unlitSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            glow.sharedMaterial = unlitSpriteMaterial;

            facades.Add(new ShopFacade
            {
                kind = kind,
                building = go.transform,
                glow = glow,
                isPlayerShop = kind == "shop",
                // 纯装饰铺池:临街小铺 0/1 常驻;东商业新区两间空铺 2/3 落成后入池(PRD §125/§141)
                decorativeIndex = kind == "decor_a" ? 0 : kind == "decor_b" ? 1 :
                    kind == "empty_a" ? 2 : kind == "empty_b" ? 3 : -1
            });

            if (interactive)
            {
                BuildingClick click = go.AddComponent<BuildingClick>();
                click.kind = kind;
            }

            return center + spec.doorOffset;
        }

        static void PaintParcelGround(Tilemap tilemap)
        {
            Tile court = MakeTile(PixelArt.ForecourtStoneTile());
            Tile bed = MakeTile(PixelArt.FlowerbedTile());

            // 地标前庭只铺中轴和仪式性前区，不再把整块院子浇成一片硬地。
            PaintSolidRect(tilemap, court, -5, 8, 4, 11);
            PaintSolidRect(tilemap, court, 22, 8, 30, 11);

            // 知识街区是两栋并列的共享庭院；北校园、住宅与南部学校各有独立入口。
            PaintSolidRect(tilemap, court, 9, 11, 18, 11);
            PaintSolidRect(tilemap, court, 11, 9, 12, 10);
            PaintSolidRect(tilemap, court, 15, 9, 16, 10);
            PaintSolidRect(tilemap, court, -31, 8, -27, 10);
            PaintSolidRect(tilemap, court, -17, 9, -15, 12);
            PaintSolidRect(tilemap, court, -8, -20, -3, -19);
            PaintSolidRect(tilemap, court, -4, -19, -3, -14);

            // 沿街组只铺窄前廊，建筑尺度不同但门口界面连成一条街墙。
            PaintSolidRect(tilemap, court, -35, 2, -12, 3);
            PaintSolidRect(tilemap, court, 8, 2, 24, 3);
            PaintSolidRect(tilemap, court, 29, 2, 35, 3);
            PaintSolidRect(tilemap, court, -35, -10, -8, -9);

            // 市场后场→工厂装卸院→过渡商铺→东界港口货运坪连续展开。
            PaintSolidRect(tilemap, court, -21, -8, -14, -6);
            PaintSolidRect(tilemap, court, 5, -10, 15, -3);
            PaintSolidRect(tilemap, court, 18, -10, 22, -9);
            PaintSolidRect(tilemap, court, 24, -10, 35, -5);

            // 商铺边界只用两格深的花池节点，撤掉“条形码”式竖向分隔。
            for (int i = 0; i < TownLayoutData.ParcelBoundaryStrips.Length; i++)
            {
                TownPathRect strip = TownLayoutData.ParcelBoundaryStrips[i];
                PaintSolidRect(tilemap, bed, strip.x0, strip.y0, strip.x1, strip.y1);
            }
        }

        static void PaintSolidRect(Tilemap tilemap, Tile tile, int x0, int y0, int x1, int y1)
        {
            int minX = Mathf.Min(x0, x1), maxX = Mathf.Max(x0, x1);
            int minY = Mathf.Min(y0, y1), maxY = Mathf.Max(y0, y1);
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        static void Decorate(Transform parent)
        {
            int season = GameState.I != null ? AtmosphereService.SeasonIndex(GameState.I.day) : 0;
            SpawnTreesAndFlowers(parent, season);
            SpawnGreenSpaceFurniture(parent);
            SpawnStreetFurniture(parent);   // V0.9.6 街具(交付 PRD §172 顺延项)

            // 广场中心与公告板是视觉锚点；双层喷泉作为市政轴线的正中景观。
            GameObject fountain = Prop(parent, "plaza_fountain", PixelArt.Fountain(),
                PlazaCenter + new Vector3(0f, .35f), 1.05f);
            if (fountain != null)
            {
                // 只挡主水盆的落地脚印，两侧保留环行通道；排序也以盆底而非贴图中心为准。
                BoxCollider2D fountainCollider = fountain.AddComponent<BoxCollider2D>();
                fountainCollider.size = new Vector2(3.25f, .8f);
                fountainCollider.offset = new Vector2(0f, -1.25f);
                SpriteRenderer fountainRenderer = fountain.GetComponent<SpriteRenderer>();
                if (fountainRenderer != null)
                    fountainRenderer.sortingOrder = SortingForY(fountain.transform.position.y - 1.25f);
            }
            GameObject notice = Prop(parent, "plaza_notice", PixelArt.NoticeBoard(),
                TownLayoutData.DecorPoint("plaza_notice"), 1f);   // 信息区独立收在广场西南侧
            if (notice != null)
            {
                GameObject noticeShadow = new GameObject("shadow");
                noticeShadow.transform.SetParent(notice.transform, false);
                noticeShadow.transform.localPosition = new Vector3(0f, .05f, 0f);
                SpriteRenderer nsSr = noticeShadow.AddComponent<SpriteRenderer>();
                nsSr.sprite = PixelArt.PropShadow();
                SpriteRenderer noticeSr = notice.GetComponent<SpriteRenderer>();
                if (noticeSr != null) nsSr.sortingOrder = noticeSr.sortingOrder - 1;
            }

            // 复用现有 Kenney 街景模型:垃圾桶、路灯和路牌。中心铺装不再塞独立花坛，
            // 绿化收到广场外缘，避免与喷泉、旗杆、公告碑争抢中心。
            Prop(parent, "shop_bin", KenneyArt.TileSprite(325),
                ShopDoor + new Vector3(-2f, .6f), 1f);
            Prop(parent, "competitor_bin", KenneyArt.TileSprite(326),
                CompetitorDoor + new Vector3(2f, .6f), 1f);
            Prop(parent, "west_lamp", KenneyArt.TileSprite(597),
                TownLayoutData.PlazaCenter + new Vector3(-6.3f, -3.25f), 1.15f);
            Prop(parent, "east_lamp", KenneyArt.TileSprite(597),
                TownLayoutData.PlazaCenter + new Vector3(6.3f, -3.25f), 1.15f);
            Prop(parent, "neighborhood_lamp", KenneyArt.TileSprite(597),
                TownLayoutData.ParcelCenter("training") + new Vector3(6.5f, -5.25f), 1.1f);
            Prop(parent, "trade_lamp", KenneyArt.TileSprite(597),
                TownLayoutData.ParcelCenter("market") + new Vector3(6.2f, -4.25f), 1.1f);
            Prop(parent, "market_sign", KenneyArt.TileSprite(599),
                MarketDoor + new Vector3(-3f, .6f), 1.1f);

            Prop(parent, "market_crate_a", PixelArt.Crate(),
                TownLayoutData.ParcelCenter("market") + new Vector3(5f, -.8f), 1f);
            Prop(parent, "market_crate_b", PixelArt.Crate(),
                TownLayoutData.ParcelCenter("market") + new Vector3(5.8f, -1.6f), .9f);

            // V0.8.1 工厂独立大烟囱:厂区西北角、基座落地(与 crates 同一地平线),烟口高出厂房屋脊;
            // 碰撞体压在基座处,与工厂 M 碰撞体净距 0.5(LayoutGuard 覆盖);烟为静态贴图不动粒子预算
            // V0.9.6g:西移 0.1——工厂碰撞体按临界带实测补到满檐宽(40px)后,原位净距是数学上
            // 恰 0.400(|15−10.9|−(3.2+0.5)),浮点会判 0.3999998 红;退 0.1 拿到 0.5 的实余量。
            GameObject chimney = Prop(parent, "factory_chimney", PixelArt.FactoryChimney(),
                TownLayoutData.DecorPoint("factory_chimney"), 1f);
            if (chimney != null)
            {
                BoxCollider2D chCol = chimney.AddComponent<BoxCollider2D>();
                chCol.size = new Vector2(1f, 1f);
                chCol.offset = new Vector2(0f, -2.2f);
                GameObject chShadow = new GameObject("shadow");                    // V0.8.2b:基座接地落影
                chShadow.transform.SetParent(chimney.transform, false);
                chShadow.transform.localPosition = new Vector3(0f, -2.24f, 0f);
                SpriteRenderer chShSr = chShadow.AddComponent<SpriteRenderer>();
                chShSr.sprite = PixelArt.PropShadow();
                chShSr.sortingOrder = SortingForY(chimney.transform.position.y) - 1;
                GameObject smoke = new GameObject("smoke_wisp");
                smoke.transform.SetParent(chimney.transform, false);
                smoke.transform.localPosition = new Vector3(0.4f, 2.7f, 0f);
                SpriteRenderer smokeSr = smoke.AddComponent<SpriteRenderer>();
                smokeSr.sprite = PixelArt.FactorySmoke();
                smokeSr.sortingOrder = SortingForY(chimney.transform.position.y) - 1;   // 烟在烟囱后方
                smokeSr.color = new Color(1f, 1f, 1f, 0.9f);
            }

        }

        // ————— V0.8.2 地标院落(PRD §170):用地类型学——市政=铁艺前庭/文教=绿篱校园/工业=链网后场 —————
        // 围栏与门柱命名 fence_*,入 LayoutGuard 屏障类(与地图围墙同享两两贴接豁免);
        // 静态常驻:不进雪线/季节表、不读任何状态、不产生交互点。

        static void BuildLandmarkCompounds(Transform parent)
        {
            for (int i = 0; i < TownLayoutData.Parcels.Length; i++)
            {
                TownParcel parcel = TownLayoutData.Parcels[i];
                if (parcel.HasFence) BuildClosedParcelFence(parent, parcel);
            }

            // 市政庭、央行庭、校园和工厂各有自己的院落语言，不再混用散点街具。
            Prop(parent, "hall_court_planter_w", KenneyArt.TileSprite(179), TownLayoutData.DecorPoint("hall_planter_w"), 1.25f);
            Prop(parent, "hall_court_planter_e", KenneyArt.TileSprite(180), TownLayoutData.DecorPoint("hall_planter_e"), 1.25f);
            Prop(parent, "centralbank_court_lamp_w", PixelArt.Lamp(), TownLayoutData.DecorPoint("centralbank_lamp_w"), 1.05f);
            Prop(parent, "centralbank_court_lamp_e", PixelArt.Lamp(), TownLayoutData.DecorPoint("centralbank_lamp_e"), 1.05f);
            Prop(parent, "centralbank_planter_w", KenneyArt.TileSprite(179), TownLayoutData.DecorPoint("centralbank_planter_w"), 1.15f);
            Prop(parent, "centralbank_planter_e", KenneyArt.TileSprite(180), TownLayoutData.DecorPoint("centralbank_planter_e"), 1.15f);

            Prop(parent, "training_sign", KenneyArt.TileSprite(599),
                TownLayoutData.DecorPoint("training_sign"), 1.1f);
            Prop(parent, "training_bench", PixelArt.ParkBench(), TownLayoutData.DecorPoint("training_bench"), 1f);

            Prop(parent, "factory_pallets", PixelArt.PalletStack(), TownLayoutData.DecorPoint("factory_pallets"), 1f);
            Prop(parent, "factory_drums", PixelArt.OilDrums(), TownLayoutData.DecorPoint("factory_drums"), 1f);
            Prop(parent, "port_yard_pallets", PixelArt.PalletStack(), TownLayoutData.DecorPoint("port_pallets"), .95f);
            Prop(parent, "port_yard_crates", PixelArt.Crate(), TownLayoutData.DecorPoint("port_crates"), 1.05f);
        }

        static void BuildClosedParcelFence(Transform parent, TownParcel parcel)
        {
            Rect r = parcel.bounds;
            int style = FenceStyle(parcel.boundary);
            float gateY = parcel.gateOnNorth ? r.yMax : r.yMin;
            float oppositeY = parcel.gateOnNorth ? r.yMin : r.yMax;
            float gateLeft = parcel.gateCenterX - parcel.gateWidth * .5f - .35f;
            float gateRight = parcel.gateCenterX + parcel.gateWidth * .5f + .35f;
            string gateSide = parcel.gateOnNorth ? "north" : "south";
            string oppositeSide = parcel.gateOnNorth ? "south" : "north";

            FenceLine(parent, parcel.id + "_" + gateSide + "_w", style,
                new Vector2(r.xMin, gateY), new Vector2(gateLeft, gateY));
            FenceLine(parent, parcel.id + "_" + gateSide + "_e", style,
                new Vector2(gateRight, gateY), new Vector2(r.xMax, gateY));
            FenceLine(parent, parcel.id + "_" + oppositeSide, style,
                new Vector2(r.xMin, oppositeY), new Vector2(r.xMax, oppositeY));
            FenceLine(parent, parcel.id + "_west", style,
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax));
            FenceLine(parent, parcel.id + "_east", style,
                new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax));

            FencePier(parent, parcel.id + "_gate_w", style, new Vector2(gateLeft, gateY));
            FencePier(parent, parcel.id + "_gate_e", style, new Vector2(gateRight, gateY));
            FencePier(parent, parcel.id + "_corner_sw", style, new Vector2(r.xMin, r.yMin));
            FencePier(parent, parcel.id + "_corner_nw", style, new Vector2(r.xMin, r.yMax));
            FencePier(parent, parcel.id + "_corner_se", style, new Vector2(r.xMax, r.yMin));
            FencePier(parent, parcel.id + "_corner_ne", style, new Vector2(r.xMax, r.yMax));
        }

        static int FenceStyle(TownBoundaryStyle boundary)
        {
            switch (boundary)
            {
                case TownBoundaryStyle.Hedge: return 1;
                case TownBoundaryStyle.Chain: return 2;
                case TownBoundaryStyle.Picket: return 3;
                default: return 0;
            }
        }

        static Sprite FenceSprite(int style, int px, bool vertical)
        {
            return vertical ? PixelArt.FenceVertical(style, px) :
                (style == 1 ? PixelArt.FenceHedge(px) : style == 2 ? PixelArt.FenceChain(px) :
                 style == 3 ? PixelArt.FencePicket(px) : PixelArt.FenceIron(px));
        }

        /// <summary>围栏线:水平线整段单 sprite;纵向线按 ≤3.2 世界分段逐段排序(贴图旋转 90° 惯例)。
        /// 每线一根薄碰撞体(0.3 厚)居线中,GO 命名 fence_* 入 LayoutGuard 屏障类。</summary>
        static void FenceLine(Transform parent, string id, int style, Vector2 from, Vector2 to)
        {
            float len = Vector2.Distance(from, to);
            if (len < .05f) return;
            bool vertical = Mathf.Abs(to.y - from.y) > Mathf.Abs(to.x - from.x);
            GameObject run = new GameObject("fence_" + id);
            run.transform.SetParent(parent, false);
            Vector2 mid = (from + to) * .5f;
            run.transform.position = new Vector3(mid.x, mid.y, 0f);
            // V0.8.2b:贴图抬高只到落影上沿(底部 2px 接触落影压在线下),碰撞中心沿用 V0.8.2 验收位不动。
            float halfH = (style == 1 ? 16f : style == 2 ? 18f : style == 3 ? 14f : 22f) / 32f - 2f / 16f;
            float colHalf = (style == 1 ? 14f : style == 2 ? 16f : style == 3 ? 12f : 18f) / 32f;

            if (!vertical)
            {
                int px = Mathf.Max(8, Mathf.CeilToInt(len * 4f) * 4);          // 0.25 世界步进、只外扩不留缝
                GameObject seg = new GameObject("seg0");
                seg.transform.SetParent(run.transform, false);
                seg.transform.localPosition = new Vector3(0f, halfH, 0f);       // pivot 居中→抬半高使底缘贴线
                SpriteRenderer sr = seg.AddComponent<SpriteRenderer>();
                sr.sprite = FenceSprite(style, px, false);
                sr.sortingOrder = SortingForY(from.y);
            }
            else
            {
                int segs = Mathf.Max(1, Mathf.CeilToInt(len / 3.2f));           // 纵向逐段排序:人/树/建筑穿插不穿帮
                float segLen = len / segs;
                float yLow = Mathf.Min(from.y, to.y);
                for (int i = 0; i < segs; i++)
                {
                    float y0 = yLow + segLen * i;
                    int px = Mathf.Max(8, Mathf.CeilToInt(segLen * 4f) * 4);
                    GameObject seg = new GameObject("seg" + i);
                    seg.transform.SetParent(run.transform, false);
                    seg.transform.localPosition = new Vector3(0f, y0 + segLen * .5f - mid.y, 0f);
                    SpriteRenderer sr = seg.AddComponent<SpriteRenderer>();
                    sr.sprite = FenceSprite(style, px, true);
                    sr.sortingOrder = SortingForY(y0);
                }
            }

            BoxCollider2D col = run.AddComponent<BoxCollider2D>();
            col.size = vertical ? new Vector2(.3f, len) : new Vector2(len, .3f);
            col.offset = vertical ? Vector2.zero : new Vector2(0f, colHalf);
        }

        /// <summary>院落门柱:砖/钢柱身+压顶,style 0 顶灯/1 石球/2 短杆;薄碰撞体+椭圆落影接地。</summary>
        static void FencePier(Transform parent, string id, int style, Vector2 pos)
        {
            GameObject go = new GameObject("fence_pier_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y + 17f / 16f, 0f); // 贴图 34px 高,pivot 居中
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.GatePier(style);
            sr.sortingOrder = SortingForY(pos.y);
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(.7f, .5f);
            col.offset = new Vector2(0f, -17f / 16f + .25f);                    // 碰撞体压在柱基
            GameObject shadow = new GameObject("shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -17f / 16f + .08f, 0f);
            SpriteRenderer shSr = shadow.AddComponent<SpriteRenderer>();
            shSr.sprite = PixelArt.PropShadow();
            shSr.sortingOrder = sr.sortingOrder - 1;
        }

        static void BuildPortScene(Transform parent)
        {
            PortDistrictRoot = new GameObject("PortScene").transform;
            PortDistrictRoot.SetParent(parent, false);

            PortDistrictRoot.gameObject.AddComponent<Grid>();
            GameObject groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(PortDistrictRoot, false);
            Tilemap ground = groundGo.AddComponent<Tilemap>();
            TilemapRenderer renderer = groundGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -1000;

            Tile[] yard = { MakeTile(PixelArt.HarborYard(0)), MakeTile(PixelArt.HarborYard(1)) };
            Tile shore = MakeTile(PixelArt.HarborShore());
            Tile[] water = { MakeTile(PixelArt.HarborWater(0)), MakeTile(PixelArt.HarborWater(1)),
                MakeTile(PixelArt.HarborWater(2)) };
            Tile[] road = { MakeTile(PixelArt.Road(0)), MakeTile(PixelArt.Road(1)), MakeTile(PixelArt.Road(2)) };
            Tile[] walk = { MakeTile(PixelArt.Plaza(0)), MakeTile(PixelArt.Plaza(1)) };
            var rng = new System.Random(20260828);
            Vector3Int origin = new Vector3Int(PortOriginX, -PortH / 2, 0);
            for (int x = 0; x < PortW; x++)
                for (int y = 0; y < PortH; y++)
                {
                    int worldX = origin.x + x;
                    Tile tile = worldX >= PortSeaMinX ? water[(x + y) % water.Length]
                        : worldX == PortShoreX ? shore : yard[(x + y) & 1];
                    ground.SetTile(new Vector3Int(worldX, origin.y + y, 0), tile);
                }

            // 接驳站、报关行与泊位由一条服务车道串联，进场后能立刻读懂返回方向。
            for (int x = PortOriginX; x <= PortShoreX - 1; x++)
            {
                ground.SetTile(new Vector3Int(x, -8, 0), road[rng.Next(road.Length)]);
                ground.SetTile(new Vector3Int(x, -7, 0), road[rng.Next(road.Length)]);
                ground.SetTile(new Vector3Int(x, -6, 0), walk[rng.Next(walk.Length)]);
            }

            GameObject terminal = Prop(PortDistrictRoot, "port_scene_terminal", PixelArt.Building("port"),
                new Vector3(-10f, -3.4f, 0f), BuildingSpecs.Get("port").scale);
            if (terminal != null)
            {
                BoxCollider2D col = terminal.AddComponent<BoxCollider2D>();
                BuildingSpec portSpec = BuildingSpecs.Get("port");
                col.size = portSpec.colliderSize;
                col.offset = portSpec.colliderOffset;
                // V0.9.6g:同 Building(),点击触发体保住「点接驳站上部也能返回小镇」
                BoxCollider2D terminalClick = terminal.AddComponent<BoxCollider2D>();
                terminalClick.isTrigger = true;
                terminalClick.size = new Vector2(portSpec.canvasW / (float)PixelArt.PPU,
                    portSpec.canvasH / (float)PixelArt.PPU);
                terminal.AddComponent<PortExitClick>();
                MakeLabel(terminal.transform, "返回小镇", new Vector3(0f, 2.25f, 0f));
            }
            PortExitDoor = new Vector3(-10f, -5.85f, 0f);
            PortEntryPosition = PortExitDoor + new Vector3(0f, -1.2f, 0f);

            Prop(PortDistrictRoot, "port_dock_north", PixelArt.HarborDock(), new Vector3(9.5f, 3.4f), 1f);
            Prop(PortDistrictRoot, "port_dock_south", PixelArt.HarborDock(), new Vector3(9.5f, -3.2f), 1f);
            Prop(PortDistrictRoot, "port_flag", PixelArt.PortFlag(false), new Vector3(5.6f, 7.8f), 1f);
            DecoratePortCargo(PortDistrictRoot);

            GameObject board = Prop(PortDistrictRoot, "customs_board", PixelArt.CustomsBoard(),
                new Vector3(-4.8f, -4.8f), 1f);
            if (board != null)
            {
                BoxCollider2D col = board.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2.4f, 1.8f);
                col.offset = new Vector2(0f, .45f);
                board.AddComponent<CustomsBoardClick>();
                MakeLabel(board.transform, "报关行", new Vector3(0f, 1.5f, 0f));
            }
            CustomsDoor = new Vector3(-6.3f, -4.8f, 0f);

            float centerX = (PortWorldMinX + PortWorldMaxX) * .5f;
            MakeNamedWall(PortDistrictRoot, "port_wall_north",
                new Vector3(centerX, PortWorldMaxY, 0f), new Vector2(PortW + 2, 1f));
            MakeNamedWall(PortDistrictRoot, "port_wall_south",
                new Vector3(centerX, PortWorldMinY, 0f), new Vector2(PortW + 2, 1f));
            MakeNamedWall(PortDistrictRoot, "port_wall_west",
                new Vector3(PortWorldMinX, 0f, 0f), new Vector2(1f, PortH + 2));
            MakeNamedWall(PortDistrictRoot, "port_wall_east",
                new Vector3(PortWorldMaxX, 0f, 0f), new Vector2(1f, PortH + 2));
            MakeNamedWall(PortDistrictRoot, "port_sea_barrier",
                new Vector3(PortSeaMinX - .15f, 0f, 0f), new Vector2(.7f, PortH));

            // 与 Town 互斥显示；PortSceneController 进入时再激活并移动到独立运行时 Scene。
            PortDistrictRoot.gameObject.SetActive(false);
        }

        // ————— V0.9.7 果汁店店内(照 BuildPortScene 范式:独立世界根,门口按 E 换场进出)—————
        // 纯呈现层:零状态读取、零存档字段、零经济改动;坐标空间与小镇/港区各自独立,
        // 四周围墙与家具碰撞体只在店内根激活时存在于物理世界。

        /// <summary>店内 16×11:上墙三行(灰泥/嵌三扇窗/墙裙)、木地板两变体交铺、货架+冷柜+
        /// 收银柜台+榨汁台+圆桌+盆栽(全部自绘多格尺寸)。交互锚点收银台/门垫由
        /// GameBootstrap.RegisterInteractables 注册;有体量的家具带落地脚印碰撞体
        /// (照港区 port_decor_solid_* 先例)。</summary>
        // ————— V0.9.11 店内体系:薄壳循环 + BuildInteriorRoom 通用装配 + per-kind 布局函数 —————
        // 几何/主题/锚点唯一登记处在 InteriorCatalog(表只放数据);本组函数只管场景装配与家具摆位。
        // 纪律(V0.9.7 起):纯呈现层——零状态读取、零存档字段、零经济改动。
        static void BuildShopInterior(Transform parent)
        {
            Transform root = new GameObject("ShopInterior").transform;   // 根名不变(V097 按名找)
            root.SetParent(parent, false);
            ShopInteriorRoot = root;
            for (int i = 0; i < InteriorCatalog.All.Length; i++)
                BuildInteriorRoom(root, InteriorCatalog.All[i]);
            // 与 Town 互斥显示;ShopInteriorController 进入时再激活并移动到独立运行时 Scene。
            root.gameObject.SetActive(false);
        }

        /// <summary>通用房间装配:Grid+Ground Tilemap(顶三行墙带/地板棋盘)、furniture 根、
        /// 门垫(isTrigger 点击出店,出店不分店)、customers 节点、四面围墙;家具细节由布局函数补。
        /// 坐标语义:Tilemap 写房间局部格子;Prop/围墙/NPC 全世界坐标(spec.origin+局部)。</summary>
        static void BuildInteriorRoom(Transform root, InteriorSpec s)
        {
            Transform room = new GameObject("interior_" + s.kind).transform;
            room.SetParent(root, false);
            room.position = s.origin;                       // shop 在原点(0,0),果汁店像素级不动
            interiorRooms[s.kind] = room;

            room.gameObject.AddComponent<Grid>();
            GameObject groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(room, false);
            Tilemap ground = groundGo.AddComponent<Tilemap>();
            TilemapRenderer renderer = groundGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -1000;

            int theme = (int)s.theme;
            Tile[] floor = { MakeTile(PixelArt.InteriorFloorTile(theme, 0)), MakeTile(PixelArt.InteriorFloorTile(theme, 1)) };
            Tile wall = MakeTile(PixelArt.InteriorWallTile(theme));
            Tile window = MakeTile(PixelArt.InteriorWindowTile(theme));
            Tile wainscot = MakeTile(PixelArt.InteriorWainscotTile(theme));
            bool[] isWindow = new bool[s.width];
            for (int w = 0; w < s.windowColumns.Length; w++) isWindow[s.windowColumns[w]] = true;
            for (int x = 0; x < s.width; x++)
                for (int y = 0; y < s.height; y++)
                {
                    // 上墙三行:上两行灰泥(中排嵌窗,列=spec.windowColumns)、底行墙裙;
                    // 地板 (x*7+y*5)%2 交铺(≡(x+y)%2 棋盘,theme 1 双色底即奶油/浅棕拼板)
                    if (y >= s.height - 3)
                        ground.SetTile(new Vector3Int(x, y, 0),
                            y == s.height - 3 ? wainscot : y == s.height - 2 && isWindow[x] ? window : wall);
                    else
                        ground.SetTile(new Vector3Int(x, y, 0), floor[(x * 7 + y * 5) % 2]);
                }

            Transform furn = new GameObject("furniture").transform;
            furn.SetParent(room, false);
            // 门垫:进出交互锚点,平铺不挡人;点击出店靠 isTrigger 薄碰撞体
            // (OnMouseDown 只认带 Collider 的对象,不挂的话「点击门垫出店」是死代码)。
            // 贴图中心 y 固定 .7(V0.9.7 原值,像素级回归基准);E 键交互点另有 spec.ExitWorld(.75)
            GameObject mat = Prop(furn, "door_mat", PixelArt.InteriorDoorMat(),
                s.origin + new Vector3(s.exitLocal.x, .7f), 1.2f);
            if (mat != null)
            {
                BoxCollider2D matClick = mat.AddComponent<BoxCollider2D>();
                matClick.isTrigger = true;
                matClick.size = new Vector2(1.2f, .5f);
                mat.AddComponent<ShopInteriorExitClick>();   // 出店退当前间,逻辑不分店——类零改动复用
            }
            // 店内常驻 NPC 挂 customers 节点随店显隐(布局函数往里 Spawn;房间矩形内漫游,
            // 不设 dialogue/ambient,避开街道人口的对话与回收管线)
            Transform customers = new GameObject("customers").transform;
            customers.SetParent(room, false);

            DecorateInterior(s.kind)(furn, customers, s);

            // 四周围墙:北墙压在可见墙带下缘(y=H-3+0.5),角色贴墙站不进墙面;南/西/东沿房间边界
            float cx = s.origin.x + s.width * .5f - .5f, cy = s.origin.y + s.height * .5f - .5f;
            MakeNamedWall(room, "interior_wall_north",
                new Vector3(cx, s.origin.y + s.height - 2.5f), new Vector2(s.width + 2f, 1f));
            MakeNamedWall(room, "interior_wall_south", new Vector3(cx, s.origin.y - .5f), new Vector2(s.width + 2f, 1f));
            MakeNamedWall(room, "interior_wall_west", new Vector3(s.origin.x - .5f, cy), new Vector2(1f, s.height + 2f));
            MakeNamedWall(room, "interior_wall_east", new Vector3(s.origin.x + s.width + .5f, cy), new Vector2(1f, s.height + 2f));

            room.gameObject.SetActive(false);   // 各房间独立隐藏;Controller 换场/SceneShot 按 kind 激活
        }

        static System.Action<Transform, Transform, InteriorSpec> DecorateInterior(string kind)
        {
            switch (kind)
            {
                case "restaurant": return DecorateInteriorRestaurant;
                case "cinema": return DecorateInteriorCinema;
                case "market": return DecorateInteriorMarket;
                case "competitor": return DecorateInteriorCompetitor;     // V0.9.12 六间
                case "home": return DecorateInteriorHome;
                case "convenience": return DecorateInteriorConvenience;
                case "bookstore": return DecorateInteriorBookstore;
                case "decor_b": return DecorateInteriorFlower;
                case "decor_a": return DecorateInteriorSnack;
                // V0.9.13 第三批:机构与产业五间
                case "bank": return DecorateInteriorBank;
                case "centralbank": return DecorateInteriorCentralBank;
                case "hall": return DecorateInteriorHall;
                case "training": return DecorateInteriorTraining;
                case "factory": return DecorateInteriorFactory;
                default: return DecorateInteriorShop;
            }
        }

        /// <summary>果汁店布局(V0.9.7 逐行迁入,origin=0 数值不变):靠墙一线盆栽/货架/冷柜,
        /// 营业排收银台(老板位在柜台后侧,East gap 绕行)+榨汁台+原料箱,四张圆桌。</summary>
        static void DecorateInteriorShop(Transform furn, Transform customers, InteriorSpec s)
        {
            // 靠墙一线:盆栽/货架/果汁冷柜(中心 y≈8.5,叠加在墙裙之前=「立在墙前」)
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), new Vector3(.9f, 8.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "shelf_unit", PixelArt.ShelfUnit(), new Vector3(3.1f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "fruit_cooler", PixelArt.FruitCooler(), new Vector3(5.9f, 8.5f), 1f),
                new Vector2(1.4f, 1.1f), new Vector2(0f, -.65f));
            AddInteriorSolid(Prop(furn, "plant_b", PixelArt.PottedPlant(), new Vector3(15.1f, 8.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            // 营业排:收银柜台(老板位在柜台后侧,East gap 绕行)+榨汁台+原料箱
            AddInteriorSolid(Prop(furn, "counter", PixelArt.JuiceCounter(), new Vector3(11.2f, 4.7f), 1f),
                new Vector2(2.4f, .8f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "juice_press", PixelArt.JuicePress(), new Vector3(14.5f, 4.7f), 1f),
                new Vector2(1.3f, .8f), new Vector2(0f, -.5f));
            AddInteriorSolid(Prop(furn, "crate", PixelArt.Crate(), new Vector3(9.4f, 4.5f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            // 座位区:四张圆桌(二稿渲染复看「桌子太少显得很空」后加密)
            AddInteriorSolid(Prop(furn, "table_a", PixelArt.CafeTable(), new Vector3(2.6f, 3.2f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "table_b", PixelArt.CafeTable(), new Vector3(5.2f, 2f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "table_c", PixelArt.CafeTable(), new Vector3(10.4f, 2.2f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "table_d", PixelArt.CafeTable(), new Vector3(1.8f, 6.2f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));

            // V0.9.14 加密(碰撞=画布精确尺寸 offset(0,0),AABB 已手算):墙上水果静物+北墙水果箱堆
            AddInteriorSolid(Prop(furn, "art_a", PixelArt.WallArt(0), new Vector3(11f, 9f), 1f),   // 复看:13.5 压窗(13)(shop 窗列 3/8/13),西移让位
                new Vector2(1.25f, 1f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "crates_f", PixelArt.FruitCrateStack(), new Vector3(7.9f, 8.3f), 1f),
                new Vector2(1.25f, 1.125f), new Vector2(0f, 0f));

            NpcAgent.Spawn("游客", new Vector3(6.4f, 3.4f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("白领", new Vector3(12.7f, 6.5f), false, false, customers, s.NpcBounds);
        }

        /// <summary>暖炉餐厅布局(V0.9.11b 用户复看二稿:桌椅要规律、加桌):北墙暖炉+东北灶台,
        /// 中部点餐台(消费站位台前),座位区两列三行等距圆桌×6(餐厅的摆法不是随机散放);
        /// 布距硬约束:实心件离 entry/exit >1.2、离 counter 站位 >1.0(本件豁免)。</summary>
        static void DecorateInteriorRestaurant(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "hearth", PixelArt.RestaurantHearth(), o + new Vector3(2.6f, 9.4f), 1f),
                new Vector2(1.5f, 1f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "stove", PixelArt.KitchenStove(), o + new Vector3(16.4f, 8.9f), 1f),
                new Vector2(1.6f, 1f), new Vector2(0f, -.5f));
            AddInteriorSolid(Prop(furn, "counter", PixelArt.DiningCounter(), o + new Vector3(11f, 4.7f), 1f),
                new Vector2(2.4f, .8f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "wine_shelf", PixelArt.ShelfUnit(), o + new Vector3(6.2f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            // 座位区:两列(x=3.0/6.5)×三行(y=2.4/4.6/6.8)等距网格——成排成列的餐厅摆法
            float[] cols = { 3.0f, 6.5f }, rows = { 2.4f, 4.6f, 6.8f };
            int n = 0;
            for (int c = 0; c < cols.Length; c++)
                for (int r = 0; r < rows.Length; r++)
                {
                    n++;
                    AddInteriorSolid(Prop(furn, "table_" + n, PixelArt.CafeTable(), o + new Vector3(cols[c], rows[r]), 1f),
                        new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
                }
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(.9f, 8.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "crate", PixelArt.Crate(), o + new Vector3(14f, 4.5f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            // V0.9.14 加密:窗间壁灯×2+点餐台旁今日例汤黑板+西墙山景画(撤东南绿植,灶台旁本就挤)
            AddInteriorSolid(Prop(furn, "lamp_a", PixelArt.WallLamp(), o + new Vector3(6.5f, 10f), 1f),
                new Vector2(.6f, .75f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "lamp_b", PixelArt.WallLamp(), o + new Vector3(11.5f, 10f), 1f),
                new Vector2(.6f, .75f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "menu", PixelArt.ChalkMenuBoard(), o + new Vector3(13.8f, 6.6f), 1f),
                new Vector2(1f, 1.5f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "art_a", PixelArt.WallArt(0), o + new Vector3(2.6f, 10f), 1f),         // 复审:WallArt(1) 与茶室重复——餐厅回水果静物 v0,v1 归茶室独家
                new Vector2(1.25f, 1f), new Vector2(0f, 0f));

            NpcAgent.Spawn("白领", o + new Vector3(4.7f, 3.6f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("居民代表", o + new Vector3(4.7f, 5.9f), false, false, customers, s.NpcBounds);
        }

        /// <summary>星光电影院布局(V0.9.11b 用户复看二稿:要过道+大屏+几排椅子,地板去花哨):
        /// 北墙整幅加宽银幕,观众席三排×四段三联座(左右各两段、中央过道对准门口),东侧售票亭。
        /// 布距硬约束同餐厅。</summary>
        static void DecorateInteriorCinema(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "screen", PixelArt.CinemaScreen(), o + new Vector3(9f, 9.6f), 1f),
                new Vector2(3.8f, 1.1f), new Vector2(0f, -.6f));
            // 三排×四段(左 x=3.9/6.7,右 x=11.3/14.1;排 y=5.4/3.7/2.0)——段间紧贴连成长排,
            // 中央过道 x∈[8.1,9.9] 正对门垫 x=9,进门即见银幕
            float[] seatX = { 3.9f, 6.7f, 11.3f, 14.1f }, seatY = { 5.4f, 3.7f, 2.0f };
            for (int row = 0; row < seatY.Length; row++)
                for (int col = 0; col < seatX.Length; col++)
                    AddInteriorSolid(Prop(furn, "seats_r" + (row + 1) + "c" + (col + 1), PixelArt.CinemaSeatRow(),
                        o + new Vector3(seatX[col], seatY[row]), 1f),
                        new Vector2(2.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "ticket_booth", PixelArt.CinemaTicketBooth(), o + new Vector3(16.2f, 4.7f), 1f),
                new Vector2(1.8f, .9f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "snack_shelf", PixelArt.ShelfUnit(), o + new Vector3(1.6f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "snack_cooler", PixelArt.FruitCooler(), o + new Vector3(4.2f, 8.5f), 1f),
                new Vector2(1.4f, 1.1f), new Vector2(0f, -.65f));
            AddInteriorSolid(Prop(furn, "plant_b", PixelArt.PottedPlant(), o + new Vector3(17.1f, 8.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            // V0.9.14 加密:门厅海报×2(窗间)+过道旁爆米花机+门内红毯(isTrigger 可踩,撤西侧绿植)
            AddInteriorSolid(Prop(furn, "poster_a", PixelArt.MoviePoster(0), o + new Vector3(2.8f, 10.2f), 1f),
                new Vector2(1f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "poster_b", PixelArt.MoviePoster(1), o + new Vector3(15.5f, 10.2f), 1f),  // 复审:原左缘压窗 14 右缘 0.3,东移让位
                new Vector2(1f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "popcorn", PixelArt.PopcornMachine(), o + new Vector3(6.2f, 8.4f), 1f),   // 复审:原 (7.2,4.4) 穿座椅 r1c2/r2c2,挪北墙小卖部区
                new Vector2(1.25f, 1.625f), new Vector2(0f, 0f));
            GameObject cinemaCarpet = Prop(furn, "carpet", PixelArt.RedCarpet(), o + new Vector3(9f, 2.6f), 1f);
            if (cinemaCarpet != null)
            {
                BoxCollider2D carpetCol = cinemaCarpet.AddComponent<BoxCollider2D>();
                carpetCol.size = new Vector2(1f, 2.75f); carpetCol.isTrigger = true;   // 红毯可踩行
            }

            // 观众厅 NPC 漫游锁中央过道(不穿座椅排)
            NpcAgent.Spawn("学生", o + new Vector3(9f, 4.4f), false, false,
                customers, new Vector4(o.x + 8.2f, o.y + 1.2f, o.x + 9.8f, o.y + 5.0f));
            NpcAgent.Spawn("学生", o + new Vector3(9f, 2.7f), false, false,
                customers, new Vector4(o.x + 8.2f, o.y + 1.2f, o.x + 9.8f, o.y + 5.0f));
            NpcAgent.Spawn("学生", o + new Vector3(9f, 4.0f), false, false,   // V0.9.14:候场第三名观众
                customers, new Vector4(o.x + 8.2f, o.y + 1.2f, o.x + 9.8f, o.y + 5.0f));
        }

        /// <summary>原料市场布局(V0.9.11b 用户复看二稿:要像原料市场——商人摊位):三张摊台
        /// 成排朝南(中台=采购台,站位台前),每摊一名商贩 NPC 站摊后(bounds 锁摊后小带,只微动),
        /// 吊秤在中台旁过秤;西南仓储角麻袋堆+货板,北墙果箱批发排。布距硬约束同餐厅。</summary>
        static void DecorateInteriorMarket(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            float[] stallX = { 4.0f, 9.0f, 14.0f };
            string[] vendors = { "水果摊主", "粮油老板", "菜贩" };
            for (int i = 0; i < stallX.Length; i++)
                AddInteriorSolid(Prop(furn, i == 1 ? "stall_buy" : "stall_" + i, PixelArt.MarketStallTable(),
                    o + new Vector3(stallX[i], 6.2f), 1f),
                    new Vector2(2.1f, 1f), new Vector2(0f, -.5f));
            AddInteriorSolid(Prop(furn, "scale", PixelArt.HangingScale(), o + new Vector3(11.4f, 6.2f), 1f),
                new Vector2(.9f, .8f), new Vector2(0f, -.3f));
            // 仓储角(西南)+批发箱排(北墙)
            AddInteriorSolid(Prop(furn, "sack_a", PixelArt.SackPile(), o + new Vector3(1.6f, 3.0f), 1f),
                new Vector2(1.1f, .8f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "sack_b", PixelArt.SackPile(), o + new Vector3(1.6f, 4.6f), 1f),
                new Vector2(1.1f, .8f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "pallets", PixelArt.PalletStack(), o + new Vector3(3.4f, 2.2f), 1f),
                new Vector2(1.2f, .8f), new Vector2(0f, -.3f));
            float[] crateX = { 4.0f, 8.0f, 12.0f, 16.0f };
            for (int i = 0; i < crateX.Length; i++)
                AddInteriorSolid(Prop(furn, "crate_" + (char)('a' + i), PixelArt.Crate(),
                    o + new Vector3(crateX[i], 8.35f), 1f),
                    new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            // V0.9.14 复审(sameness):东北角盆栽六间同位——市场货堆满,墙角绿植撤

            // 三名商贩站各自摊后(bounds 锁摊后小带,只在摊位后微步走动)——「有人看摊」是市场辨识度核心
            for (int i = 0; i < stallX.Length; i++)
                NpcAgent.Spawn(vendors[i], o + new Vector3(stallX[i], 7.0f), false, false,
                    customers, new Vector4(o.x + stallX[i] - 1.0f, o.y + 6.7f, o.x + stallX[i] + 1.0f, o.y + 7.8f));
            // 顾客在摊前选购通道漫游(南半场)
            // V0.9.14 加密:东侧今日菜价立牌+东南水果箱堆(批发感)
            AddInteriorSolid(Prop(furn, "price_sign", PixelArt.PromoSign(), o + new Vector3(16.5f, 6f), 1f),
                new Vector2(.9f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "crates_f", PixelArt.FruitCrateStack(), o + new Vector3(16.4f, 2.6f), 1f),
                new Vector2(1.25f, 1.125f), new Vector2(0f, 0f));

            NpcAgent.Spawn("居民代表", o + new Vector3(6.5f, 3.2f), false, false,
                customers, new Vector4(o.x + 1.2f, o.y + 1.2f, o.x + 16.8f, o.y + 5.0f));
            NpcAgent.Spawn("白领", o + new Vector3(12.2f, 3.6f), false, false,
                customers, new Vector4(o.x + 1.2f, o.y + 1.2f, o.x + 16.8f, o.y + 5.0f));
        }

        // ————— V0.9.12 第二批六间(生活与商业):摆位照 V0.9.11b 复看纪律——成排成列、内容可辨识 —————

        /// <summary>SUNNY TEA 茶室布局(V0.9.12):北墙茶叶架,东侧茶台(经营情报站位台前),
        /// 座位区两列两行茶桌×4——竞争者的店也要有店的体面。布距硬约束同餐厅。</summary>
        static void DecorateInteriorCompetitor(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "tea_counter", PixelArt.TeaCounter(), o + new Vector3(11f, 4.7f), 1f),
                new Vector2(2.4f, .8f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "shelf_tea", PixelArt.ShelfUnit(), o + new Vector3(3.1f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "crate_tea", PixelArt.Crate(), o + new Vector3(14f, 4.5f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            // 座位区:两列(x=4.5/7.8)×两行(y=2.6/5.2)等距网格。
            // 复审(guardrails):右列 8.0→7.8 往左收——脚印 x 右缘 8.75 与玩家进店盒 x≥8.7 擦碰 0.05,
            // 收到 8.55 后双边分离 0.15(右移反而更贴 entry,方向反不得)
            float[] cols = { 4.5f, 7.8f }, rows = { 2.6f, 5.2f };
            int n = 0;
            for (int c = 0; c < cols.Length; c++)
                for (int r = 0; r < rows.Length; r++)
                {
                    n++;
                    AddInteriorSolid(Prop(furn, "table_" + n, PixelArt.TeaTable(), o + new Vector3(cols[c], rows[r]), 1f),
                        new Vector2(1.5f, .85f), new Vector2(0f, -.35f));
                }
            // V0.9.14 复审(sameness):东北角盆栽六间同位——茶室有屏风+挂画+壁灯,此角绿植撤
            // V0.9.14 加密:西侧木格屏风(半私密茶席,替代千篇一律的角绿植)+山景画+窗间壁灯
            AddInteriorSolid(Prop(furn, "screen", PixelArt.WoodenScreen(), o + new Vector3(2.2f, 5.6f), 1f),
                new Vector2(1.5f, 2.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "art_a", PixelArt.WallArt(1), o + new Vector3(13f, 10f), 1f),
                new Vector2(1.25f, 1f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "lamp_a", PixelArt.WallLamp(), o + new Vector3(11.4f, 10f), 1f),       // 复审:原 (6.6,10) 与餐厅壁灯近同点,挪窗 9/14 之间
                new Vector2(.6f, .75f), new Vector2(0f, 0f));

            NpcAgent.Spawn("白领", o + new Vector3(5.8f, 3.8f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(6.7f, 6.4f), false, false, customers, s.NpcBounds);
            // V0.9.14:店主站茶台后(bounds 锁台后小带,市场商贩先例)
            NpcAgent.Spawn("居民代表", o + new Vector3(11f, 6f), false, false,   // 复审:三间店员同造型同点位——茶室店主换居民代表造型
                customers, new Vector4(o.x + 10.2f, o.y + 5.7f, o.x + 11.8f, o.y + 7.2f));
        }

        /// <summary>我的家布局(V0.9.12):东北卧室区(床+床头箱),西北书架,西侧书桌(今日账本
        /// 站位台前),东南餐桌——自家没有常驻 NPC(customers 留空,与「回家」语义一致)。</summary>
        static void DecorateInteriorHome(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "desk", PixelArt.HomeDesk(), o + new Vector3(5f, 4.7f), 1f),
                new Vector2(1.8f, .85f), new Vector2(0f, -.4f));
            AddInteriorSolid(Prop(furn, "bed", PixelArt.HomeBed(), o + new Vector3(14.5f, 8.3f), 1f),
                new Vector2(2.1f, 1.15f), new Vector2(0f, -.6f));
            AddInteriorSolid(Prop(furn, "bedside_crate", PixelArt.Crate(), o + new Vector3(11.9f, 8.35f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            AddInteriorSolid(Prop(furn, "bookshelf", PixelArt.ShelfUnit(), o + new Vector3(1.8f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "dining_table", PixelArt.CafeTable(), o + new Vector3(12.5f, 3.4f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(.9f, 2.0f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "plant_b", PixelArt.PottedPlant(), o + new Vector3(11.8f, 7.9f), 1f),  // 复审:原 (17.1,8.2) 与 training 整组克隆,挪床西侧
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            // V0.9.14 加密:客厅地毯(isTrigger 可踩)+全家福+挂钟+床尾落地灯——家的生活痕迹
            GameObject homeRug = Prop(furn, "rug", PixelArt.AreaRug(0), o + new Vector3(8.2f, 3.2f), 1f);
            if (homeRug != null)
            {
                BoxCollider2D rugCol = homeRug.AddComponent<BoxCollider2D>();
                rugCol.size = new Vector2(2.5f, 1.5f); rugCol.isTrigger = true;   // 地毯可踩行
            }
            AddInteriorSolid(Prop(furn, "art_family", PixelArt.WallArt(3), o + new Vector3(12.4f, 10f), 1f),  // 复审:原 (9,10) 右半压窗 9,挪窗 9/14 之间
                new Vector2(1.25f, 1f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "clock", PixelArt.WallClock(), o + new Vector3(6.6f, 10f), 1f),       // 复审:原 (4.6,10) 整钟压窗 4,挪窗 4/9 之间
                new Vector2(.75f, .875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "floor_lamp", PixelArt.FloorLamp(), o + new Vector3(16.2f, 6.6f), 1f),
                new Vector2(1f, 1.875f), new Vector2(0f, 0f));
        }

        /// <summary>便利屋布局(V0.9.12):两列两行货架网格(选购通道),东墙冷柜,收银台
        /// 居东南(日用品消费站位台前),台后北墙香烟柜。布距硬约束同餐厅。</summary>
        static void DecorateInteriorConvenience(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.StoreCounter(), o + new Vector3(13.5f, 4.7f), 1f),
                new Vector2(2.2f, .8f), new Vector2(0f, -.55f));
            // 购物区:两列(x=4.2/8.6)×两行(y=4.2/6.4)等距货架网格。
            // 四架分别陈列零食/饮料/日化/速食，保留规整动线但不再复制同一张图。
            // 复审(guardrails):南行 3.2→4.2 北移——原 (8.6,3.2) 货架脚印 y[1.8,3.1] 完全罩住
            // 玩家进店盒 y[1.9,2.7](中心距 1.46 过护栏但 AABB 全包含,进店即嵌进货架被弹出)
            float[] cols = { 4.2f, 8.6f }, rows = { 4.2f, 6.4f };
            char tag = 'a';
            for (int c = 0; c < cols.Length; c++)
                for (int r = 0; r < rows.Length; r++)
                {
                    int category = c * rows.Length + r;
                    AddInteriorSolid(Prop(furn, "shelf_" + tag, PixelArt.ConvenienceShelf(category),
                        o + new Vector3(cols[c], rows[r]), 1f),
                        new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
                    tag++;
                }
            AddInteriorSolid(Prop(furn, "cooler", PixelArt.FruitCooler(), o + new Vector3(16.4f, 8.5f), 1f),
                new Vector2(1.4f, 1.1f), new Vector2(0f, -.65f));
            AddInteriorSolid(Prop(furn, "cigarette_shelf", PixelArt.ConvenienceShelf(4), o + new Vector3(13.5f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(2f, 2f), 1f),        // 复审:原 (0.9,8.2) 西墙位三间重复,挪西南角
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            // V0.9.14 加密:北墙关东煮台(两列货架之间)+收银旁杂志架+入口促销牌+北墙补货箱
            AddInteriorSolid(Prop(furn, "oden", PixelArt.OdenCounter(), o + new Vector3(6.8f, 8.4f), 1f),
                new Vector2(1.625f, 1.5f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "magazine", PixelArt.MagazineRack(), o + new Vector3(15.9f, 6.6f), 1f),
                new Vector2(1.125f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "promo", PixelArt.PromoSign(), o + new Vector3(11.6f, 2.6f), 1f),
                new Vector2(.9f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "crate_d", PixelArt.Crate(), o + new Vector3(10.6f, 8.4f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));

            NpcAgent.Spawn("居民代表", o + new Vector3(6f, 4.7f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(11f, 6.8f), false, false, customers, s.NpcBounds);
            // V0.9.14:店员站收银台后(bounds 锁台后小带)
            NpcAgent.Spawn("居民代表", o + new Vector3(13.5f, 6.1f), false, false,
                customers, new Vector4(o.x + 12.8f, o.y + 5.8f, o.x + 14.2f, o.y + 7.1f));
        }

        /// <summary>东街书店布局(V0.9.12):两列两行书架巷道×4,东南柜台(翻看站位台前)+
        /// 东后角阅读桌,新书堆在柜台旁。布距硬约束同餐厅。</summary>
        static void DecorateInteriorBookstore(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.BookCounter(), o + new Vector3(15f, 4.7f), 1f),
                new Vector2(2.2f, .8f), new Vector2(0f, -.55f));
            // 书架巷道:两列(x=4.2/9.0)×两行(y=4.4/6.7)等距网格。
            // 复审(guardrails):南行 3.4→4.4 北移——原 (9.0,3.4) 书架脚印 y[1.85,3.35] 同样完全包含玩家进店盒
            float[] cols = { 4.2f, 9.0f }, rows = { 4.4f, 6.7f };
            char tag = 'a';
            for (int c = 0; c < cols.Length; c++)
                for (int r = 0; r < rows.Length; r++)
                {
                    AddInteriorSolid(Prop(furn, "shelf_" + tag, PixelArt.BookshelfRow(), o + new Vector3(cols[c], rows[r]), 1f),
                        new Vector2(2.3f, 1.5f), new Vector2(0f, -.8f));
                    tag++;
                }
            AddInteriorSolid(Prop(furn, "reading_table", PixelArt.CafeTable(), o + new Vector3(15.5f, 7.9f), 1f),
                new Vector2(1.6f, .9f), new Vector2(0f, -.35f));
            AddInteriorSolid(Prop(furn, "new_crates", PixelArt.Crate(), o + new Vector3(12.2f, 4.5f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
                        // V0.9.14 复审(sameness):西墙位 (0.9,8.2) 三间重复——书店有落地灯+地图,绿植撤
            // V0.9.14 加密:高架旁取书梯+西架尾落地灯(阅读角)+世界地图挂墙+地书堆
            AddInteriorSolid(Prop(furn, "ladder", PixelArt.LibraryLadder(), o + new Vector3(10.9f, 7.6f), 1f),
                new Vector2(1.125f, 2.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "floor_lamp", PixelArt.FloorLamp(), o + new Vector3(2.4f, 6.9f), 1f),
                new Vector2(1f, 1.875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "map", PixelArt.WorldMap(), o + new Vector3(12.4f, 10.1f), 1f),   // 复看:14.4 压窗(14),西移让位
                new Vector2(1.625f, 1.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "books_a", PixelArt.BookStack(), o + new Vector3(12.4f, 5.8f), 1f),
                new Vector2(.875f, .75f), new Vector2(0f, 0f));

            NpcAgent.Spawn("学生", o + new Vector3(6.5f, 4.7f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("白领", o + new Vector3(11.5f, 7.2f), false, false, customers, s.NpcBounds);
            // V0.9.14:店主站书台后(bounds 锁台后小带)
            NpcAgent.Spawn("白领", o + new Vector3(15f, 6.2f), false, false,
                customers, new Vector4(o.x + 14.3f, o.y + 5.9f, o.x + 15.7f, o.y + 7.3f));
        }

        /// <summary>花店布局(V0.9.12):北墙花架×2,南窗前三只花桶成排,东侧包花台(挑花
        /// 站位台前)+花土箱。布距硬约束同餐厅。</summary>
        static void DecorateInteriorFlower(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.FlowerWrapCounter(), o + new Vector3(14f, 4.7f), 1f),
                new Vector2(2.2f, .8f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "rack_a", PixelArt.FlowerRack(), o + new Vector3(4.2f, 8.4f), 1f),
                new Vector2(1.8f, 1.1f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "rack_b", PixelArt.FlowerRack(), o + new Vector3(7.8f, 8.4f), 1f),
                new Vector2(1.8f, 1.1f), new Vector2(0f, -.55f));
            // 南窗前三只花桶等距成排(y=3.5,离门垫东让位)。
            // 复审(guardrails):y 3.1→3.5 北移——原 bucket_c(9.6,3.1) 脚印角部与玩家进店盒重叠 0.15×0.30
            // V0.9.14b 加密二稿:南窗前花桶 3→5 只等距成排(桶行密一倍,中庭不再空)
            float[] bucketX = { 2.4f, 4.8f, 7.2f, 9.6f, 12f };
            for (int i = 0; i < bucketX.Length; i++)
                AddInteriorSolid(Prop(furn, "bucket_" + (char)('a' + i), PixelArt.FlowerBucket(),
                    o + new Vector3(bucketX[i], 3.5f), 1f),
                    new Vector2(.9f, .8f), new Vector2(0f, -.3f));
            // V0.9.14b:西墙下盆栽花排+东墙三层阶梯花架(每层不同色花)——花房丰满度主件
            AddInteriorSolid(Prop(furn, "flower_row", PixelArt.PottedFlowerRow(), o + new Vector3(2f, 6.4f), 1f),
                new Vector2(2f, .875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "tiered_stand", PixelArt.FlowerStandTiered(), o + new Vector3(16.6f, 6f), 1f),
                new Vector2(2.125f, 2f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "soil_crates", PixelArt.Crate(), o + new Vector3(11.6f, 4.6f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            // V0.9.14 复审(sameness):东北角盆栽六间同位——花店满是鲜花,此角绿植撤
            // V0.9.14 加密:上墙吊挂花篮×2+倒挂干花束×2(全避窗列 4/9/14)——花房墙面
            AddInteriorSolid(Prop(furn, "basket_a", PixelArt.HangingBasket(), o + new Vector3(6f, 9.9f), 1f),
                new Vector2(1f, 1.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "basket_b", PixelArt.HangingBasket(), o + new Vector3(10.6f, 9.9f), 1f),
                new Vector2(1f, 1.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "dried_a", PixelArt.DriedFlowers(), o + new Vector3(2.6f, 9.9f), 1f),
                new Vector2(.875f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "dried_b", PixelArt.DriedFlowers(), o + new Vector3(12.5f, 9.9f), 1f),  // 复看:14.6 压窗(14),西移让位
                new Vector2(.875f, 1.25f), new Vector2(0f, 0f));

            NpcAgent.Spawn("白领", o + new Vector3(5.6f, 5.2f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("居民代表", o + new Vector3(11.5f, 6.6f), false, false, customers, s.NpcBounds);
        }

        /// <summary>零嘴亭布局(V0.9.12,小间 14×10):西北零食架+冷柜,东南小卖窗(买零嘴
        /// 站位台前),亭内一只货箱一盆栽——小卖部不堆满。布距硬约束同餐厅。</summary>
        static void DecorateInteriorSnack(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "snack_window", PixelArt.SnackWindow(), o + new Vector3(9.5f, 4.6f), 1f),
                new Vector2(2.2f, .95f), new Vector2(0f, -.55f));
            // V0.9.14b:通用货架(彩色小瓶读作饮料)换专属零食架——薯片/糖果罐/饼干盒/挂装四品类
            AddInteriorSolid(Prop(furn, "shelf", PixelArt.SnackShelf(), o + new Vector3(3.2f, 3.4f), 1f),
                new Vector2(2.25f, 2.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "cooler", PixelArt.FruitCooler(), o + new Vector3(3.2f, 5.9f), 1f),
                new Vector2(1.4f, 1.1f), new Vector2(0f, -.65f));
            AddInteriorSolid(Prop(furn, "crates", PixelArt.Crate(), o + new Vector3(6.2f, 6.3f), 1f),
                new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(12.9f, 6.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));

            // V0.9.14 加密:上墙挂零食袋串+海景小画(14×10 小间的墙面也用起来,避窗列 3/7/11)
            AddInteriorSolid(Prop(furn, "snack_string", PixelArt.SnackString(), o + new Vector3(5f, 8f), 1f),
                new Vector2(1.125f, .875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "art_a", PixelArt.WallArt(2), o + new Vector3(12.8f, 7.9f), 1f),  // 复看:12.2 压窗(11),东移让位
                new Vector2(1.25f, 1f), new Vector2(0f, 0f));

            // 小卖窗上方独立吊灯：灯具常驻，暖光层使用 unlit 材质，
            // 夜间不再靠“整块取货窗发白”冒充照明。壁面件不加实体碰撞。
            GameObject lamp = Prop(furn, "snack_pendant", PixelArt.SnackPendantLamp(),
                o + new Vector3(9.5f, 8.1f), 1f);
            if (lamp != null)
            {
                Transform glow = NewChild(lamp.transform, "glow");
                SpriteRenderer glowRenderer = glow.gameObject.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = PixelArt.SnackPendantGlow();
                glowRenderer.sortingOrder = lamp.GetComponent<SpriteRenderer>().sortingOrder + 1;
                if (unlitSpriteMaterial == null)
                    unlitSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
                glowRenderer.sharedMaterial = unlitSpriteMaterial;
                glowRenderer.color = new Color(1f, 1f, 1f, .9f);
            }

            NpcAgent.Spawn("学生", o + new Vector3(5.8f, 1.9f), false, false, customers, s.NpcBounds);
        }

        // ————— V0.9.13 第三批五间(机构与产业):摆位照 V0.9.11b 复看纪律——成排成列、内容可辨识 —————

        /// <summary>小镇银行布局(V0.9.13 建,V0.9.14 排队营业厅重排):东侧高柜台(铜护栏+玻璃窗+
        /// 存折台牌,存取业务站位台前),等候椅一列竖排(x=5.5)+隔离柱×2 引导动线+西北取号机,
        /// 北墙存贷利率公示牌+公告板+挂钟,东南大盆栽,柜台后驻职员。</summary>
        static void DecorateInteriorBank(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.BankCounter(), o + new Vector3(11f, 4.7f), 1f),
                new Vector2(2.4f, .8f), new Vector2(0f, -.55f));
            // V0.9.14 重排(排队营业厅):等候椅改一列竖排(x=5.5)面向柜台,隔离柱×2 引导动线,
            // 西北取号机——「进门取号-排队-叫号」的银行动线取代原两行两列椅阵
            float[] benchY = { 2.2f, 3.6f, 5.0f, 6.4f };
            for (int i = 0; i < benchY.Length; i++)
                AddInteriorSolid(Prop(furn, "bench_" + (i + 1), PixelArt.WaitingBench(), o + new Vector3(5.5f, benchY[i]), 1f),
                    new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "stanchion_a", PixelArt.RopeStanchion(), o + new Vector3(7f, 2.8f), 1f),
                new Vector2(.5f, 1.7f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "stanchion_b", PixelArt.RopeStanchion(), o + new Vector3(7f, 4.4f), 1f),
                new Vector2(.5f, 1.7f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "queue", PixelArt.QueueMachine(), o + new Vector3(2.9f, 6.2f), 1f),
                new Vector2(.875f, 1.5f), new Vector2(0f, 0f));
            // V0.9.14 复审(sameness):门口红毯与影院同位同款——红毯语义属影院首映,
            // 银行排队动线已由取号机+隔离柱+椅列构成,红毯撤
            // 北墙:存贷利率公示牌(复看返工件)+公告板+挂钟(避窗列 4/9/14)+大盆栽替代角绿植
            AddInteriorSolid(Prop(furn, "rate_board", PixelArt.RateBoard(), o + new Vector3(2.6f, 8.2f), 1f),
                new Vector2(1.9f, 1.4f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "notice", PixelArt.NoticeBoard(), o + new Vector3(5.6f, 8.5f), 1f),
                new Vector2(1.4f, 1f), new Vector2(0f, -.4f));
            AddInteriorSolid(Prop(furn, "clock", PixelArt.WallClock(), o + new Vector3(16f, 10f), 1f),
                new Vector2(.75f, .875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "big_plant", PixelArt.BigPlant(), o + new Vector3(.7f, 7.8f), 1f),    // 复审同位+V0.9.14b 东南让位给 ATM 自助区,大盆栽驻西北
                new Vector2(1.25f, 1.875f), new Vector2(0f, 0f));
            // V0.9.14b 差异化二稿(营业厅做实):东墙 ATM×2 自助区+柜台正后叫号屏+东南填单台+柜台上点钞机
            AddInteriorSolid(Prop(furn, "atm_a", PixelArt.AtmMachine(), o + new Vector3(17f, 2.3f), 1f),
                new Vector2(1.375f, 1.75f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "atm_b", PixelArt.AtmMachine(), o + new Vector3(17f, 4.2f), 1f),
                new Vector2(1.375f, 1.75f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "teller_screen", PixelArt.TellerScreen(), o + new Vector3(11f, 9.9f), 1f),
                new Vector2(1.625f, 1.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "fill_desk", PixelArt.FillDesk(), o + new Vector3(13.6f, 2.8f), 1f),
                new Vector2(1.875f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "money_counter", PixelArt.MoneyCounter(), o + new Vector3(13.2f, 5.1f), 1f),
                new Vector2(.875f, .75f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(.8f, 2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));

            NpcAgent.Spawn("白领", o + new Vector3(3.2f, 3.4f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("居民代表", o + new Vector3(13.2f, 6.8f), false, false, customers, s.NpcBounds);
            // V0.9.14:职员站柜台后(bounds 锁台后小带)
            NpcAgent.Spawn("白领", o + new Vector3(11.9f, 6f), false, false,    // 复审:三间店员同点位——错到柜台东北段
                customers, new Vector4(o.x + 11.1f, o.y + 5.7f, o.x + 12.7f, o.y + 7.2f));
        }

        /// <summary>中央银行布局(V0.9.13 建,V0.9.14 加密):大理石双柱分立大厅两侧(柱廊感),
        /// 利率长柜居东(央行政策站位台前)+柜后驻行长,利率牌挂长柜正后墙,东北嵌墙金库门,
        /// 窗间徽章挂旗,柜台北文件柜,南侧大盆栽。央行未解锁(cc&lt;36)时只拦面板不拦进店——
        /// 门在 OpenCentralBank 面板回调内,与原料市场清晨门同口径。</summary>
        static void DecorateInteriorCentralBank(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.PolicyCounter(), o + new Vector3(11f, 4.7f), 1f),
                new Vector2(2.6f, .8f), new Vector2(0f, -.55f));
            float[] colX = { 5.2f, 13f };
            for (int i = 0; i < colX.Length; i++)
                // 复审(guardrails):原 offset(0,-.9) 使碰撞底缘=y-1.85,比 sprite 落地脚(34px 高居中
                // pivot,底=y-1.0625)南伸 0.79 格=空气墙——改贴 sprite 底缘(offset(0,-.11))
                AddInteriorSolid(Prop(furn, "column_" + (char)('a' + i), PixelArt.MarbleColumn(),
                    o + new Vector3(colX[i], 6.2f), 1f),
                    new Vector2(.8f, 1.9f), new Vector2(0f, -.11f));
            // V0.9.14b 差异化二稿:央行不办个人业务——撤「等候椅」,西侧改成货币政策会议区
            // (长桌+桌端两椅,「政策利率是开会定的」直接演出来),金库门前堆钱垛(发行银行的货)
            AddInteriorSolid(Prop(furn, "meeting_table", PixelArt.MeetingTable(), o + new Vector3(2.8f, 3.4f), 1f),
                new Vector2(2.5f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "chair_w", PixelArt.WaitingBench(), o + new Vector3(.75f, 3.4f), 1f),
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "chair_e", PixelArt.WaitingBench(), o + new Vector3(4.8f, 3.4f), 1f),
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "cash_pallet", PixelArt.CashPallet(), o + new Vector3(15.2f, 5.4f), 1f),
                new Vector2(1.625f, 1.25f), new Vector2(0f, 0f));
            // V0.9.14 加密:利率牌挪西墙,东北嵌墙金库门(大圆门顶入墙带是刻意的库房语义)+
            // 窗间徽章挂旗+柜台北侧文件柜+南侧大盆栽(替代千篇一律的角绿植,西侧原位撤)
            AddInteriorSolid(Prop(furn, "rate_board", PixelArt.RateBoard(), o + new Vector3(10.6f, 8.3f), 1f),  // 复审:原 (1.9,8.3) 仍与银行利率牌同西北角,真挪到利率长柜正后墙
                new Vector2(1.9f, 1.4f), new Vector2(0f, -.55f));
            AddInteriorSolid(Prop(furn, "vault", PixelArt.VaultDoor(), o + new Vector3(16.6f, 8.8f), 1f),
                new Vector2(1.875f, 1.875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "emblem", PixelArt.EmblemBanner(), o + new Vector3(6.5f, 9.9f), 1f),
                new Vector2(1.125f, 1.5f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "filing", PixelArt.FilingCabinet(), o + new Vector3(7.6f, 8.3f), 1f),
                new Vector2(1.125f, 1.625f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "big_plant", PixelArt.BigPlant(), o + new Vector3(5f, 2.4f), 1f),
                new Vector2(1.25f, 1.875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "plant_b", PixelArt.PottedPlant(), o + new Vector3(16.9f, 2.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));

            NpcAgent.Spawn("白领", o + new Vector3(8.4f, 3.2f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(13.6f, 3.4f), false, false, customers, s.NpcBounds);
            // V0.9.14:行长站利率长柜后(bounds 锁台后小带)
            NpcAgent.Spawn("白领", o + new Vector3(10.1f, 6f), false, false,    // 复审:三间店员同点位——错到利率牌正前
                customers, new Vector4(o.x + 9.3f, o.y + 5.7f, o.x + 10.9f, o.y + 7.2f));
        }

        /// <summary>市政厅布局(V0.9.13):政务长台居中(财政总览站位台前),北墙双旗杆分立,
        /// 台前两椅对仗分列,东墙公告板。布距硬约束同餐厅。</summary>
        static void DecorateInteriorHall(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "counter", PixelArt.GovernmentDesk(), o + new Vector3(9f, 4.7f), 1f),
                new Vector2(2.6f, .8f), new Vector2(0f, -.55f));
            // 复看返工:52px 旗杆顶穿墙带且无旗读作路灯——杆下移贴墙带底(y=7.3,顶 8.93),
            // 并照广场旗杆先例挂旗面子件(固定中性绿,不参与财政立场染色)
            float[] flagX = { 3f, 15f };
            for (int i = 0; i < flagX.Length; i++)
            {
                GameObject flag = Prop(furn, "flag_" + (char)('a' + i), PixelArt.Flagpole(),
                    o + new Vector3(flagX[i], 7.3f), 1f);
                AddInteriorSolid(flag, new Vector2(.5f, 1.6f), new Vector2(0f, -.7f));
                if (flag != null)
                {
                    SpriteRenderer cloth = new GameObject("flag_cloth").AddComponent<SpriteRenderer>();
                    cloth.transform.SetParent(flag.transform, false);
                    cloth.transform.localPosition = new Vector3(.7f, 1.1f, 0f);
                    cloth.sprite = PixelArt.FlagCloth();
                    cloth.color = new Color(.72f, .82f, .7f);
                    // 复审(visual):不设排序默认 0,东旗面被公告板(order 132)盖住 1/3——
                    // 照广场旗面先例(:2260)取 SortingForY+1(=143)
                    cloth.sortingOrder = SortingForY(7.3f) + 1;
                }
            }
            AddInteriorSolid(Prop(furn, "bench_a", PixelArt.WaitingBench(), o + new Vector3(4.5f, 2.2f), 1f),   // 复审:椅列 y 与央行等候角同值,错开
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "bench_b", PixelArt.WaitingBench(), o + new Vector3(13.5f, 2.2f), 1f),   // 复审:椅列 y 与央行等候角同值,错开
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "notice", PixelArt.NoticeBoard(), o + new Vector3(13.6f, 8.6f), 1f),
                new Vector2(1.4f, 1f), new Vector2(0f, -.4f));
            // V0.9.14 加密:窗间镇徽挂旗+意见箱+挂钟,台前隔离柱×2 引导办事动线,长椅加倍两排,
            // 双侧大盆栽替代小绿植——政务窗口的完整语汇
            AddInteriorSolid(Prop(furn, "emblem", PixelArt.EmblemBanner(), o + new Vector3(13.2f, 9.9f), 1f),  // 复审:原 (6.5,9.9) 与央行徽章完全同位,挪窗 9/14 之间
                new Vector2(1.125f, 1.5f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "suggest", PixelArt.SuggestionBox(), o + new Vector3(11.5f, 9.8f), 1f),
                new Vector2(.875f, 1f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "clock", PixelArt.WallClock(), o + new Vector3(2.2f, 10f), 1f),
                new Vector2(.75f, .875f), new Vector2(0f, 0f));
            // 隔离柱原先用整根柱身做碰撞（0.5×1.7，中心无偏移），南缘几乎贴进店落点、北缘顶到政务台，
            // 中缝又被柜台封死，玩家从门口走进去会被两根柱子别住。碰撞只留铜座脚印，并拉开到走道两侧。
            AddInteriorSolid(Prop(furn, "stanchion_a", PixelArt.RopeStanchion(), o + new Vector3(6.8f, 2.7f), 1f),
                new Vector2(.45f, .4f), new Vector2(0f, -.675f));
            AddInteriorSolid(Prop(furn, "stanchion_b", PixelArt.RopeStanchion(), o + new Vector3(11.2f, 2.7f), 1f),
                new Vector2(.45f, .4f), new Vector2(0f, -.675f));
            AddInteriorSolid(Prop(furn, "bench_c", PixelArt.WaitingBench(), o + new Vector3(4.5f, 3.8f), 1f),   // 复审:椅列 y 与央行等候角同值,错开
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "bench_d", PixelArt.WaitingBench(), o + new Vector3(13.5f, 3.8f), 1f),   // 复审:椅列 y 与央行等候角同值,错开
                new Vector2(1.7f, .55f), new Vector2(0f, -.25f));
            AddInteriorSolid(Prop(furn, "big_plant_a", PixelArt.BigPlant(), o + new Vector3(.9f, 7.9f), 1f),
                new Vector2(1.25f, 1.875f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "big_plant_b", PixelArt.BigPlant(), o + new Vector3(17f, 7.9f), 1f),
                new Vector2(1.25f, 1.875f), new Vector2(0f, 0f));

            NpcAgent.Spawn("白领", o + new Vector3(5.8f, 3.6f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("居民代表", o + new Vector3(12.4f, 6.6f), false, false, customers, s.NpcBounds);
            // V0.9.14:公务员站政务台后(bounds 锁台后小带)
            NpcAgent.Spawn("白领", o + new Vector3(9f, 6.2f), false, false,
                customers, new Vector4(o.x + 8.2f, o.y + 5.9f, o.x + 9.8f, o.y + 7.3f));
        }

        /// <summary>职业培训中心布局(V0.9.13,教室):北墙黑板,板前讲台(培训补贴站位台前,
        /// 本间柜台破例 y=7.0——讲台必须贴黑板),双人课桌两行两列成阵让开中走道。
        /// AABB 手算:南行课桌 y=3.5 脚印 y≥2.85 与进店盒 y≤2.7 分离 0.15。</summary>
        static void DecorateInteriorTraining(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "blackboard", PixelArt.Blackboard(), o + new Vector3(9f, 8.5f), 1f),
                new Vector2(2.2f, .5f), new Vector2(0f, -.2f));
            AddInteriorSolid(Prop(furn, "podium", PixelArt.Podium(), o + new Vector3(9f, 7f), 1f),
                new Vector2(1.1f, 1f), new Vector2(0f, -.45f));
            // 课桌阵:两列(x=5.4/12.2)×两行(y=3.5/5.7),中走道留给玩家直行到讲台
            float[] cols = { 5.4f, 12.2f }, rows = { 3.5f, 5.7f };
            int n = 0;
            for (int c = 0; c < cols.Length; c++)
                for (int r = 0; r < rows.Length; r++)
                {
                    n++;
                    AddInteriorSolid(Prop(furn, "desk_" + n, PixelArt.SchoolDeskRow(), o + new Vector3(cols[c], rows[r]), 1f),
                        new Vector2(1.9f, .7f), new Vector2(0f, -.3f));
                }
            AddInteriorSolid(Prop(furn, "bookshelf", PixelArt.ShelfUnit(), o + new Vector3(1.8f, 8.6f), 1f),
                new Vector2(2.1f, 1.3f), new Vector2(0f, -.75f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(.9f, 2.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));
            // V0.9.14 复审(sameness):东北角盆栽与 home 整组克隆——教室有地图/地球仪/课程牌,此角绿植撤

            // V0.9.14 加密:讲台旁地球仪+黑板西侧世界地图(避窗 4)+东墙课程公告+墙角灭火器
            AddInteriorSolid(Prop(furn, "globe", PixelArt.GlobeStand(), o + new Vector3(10.6f, 7.2f), 1f),
                new Vector2(1f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "map", PixelArt.WorldMap(), o + new Vector3(6.2f, 9.9f), 1f),     // 复看:5.4 压窗(4),东移让位
                new Vector2(1.625f, 1.125f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "course_board", PixelArt.ChalkMenuBoard(), o + new Vector3(12.6f, 6.9f), 1f),  // 复审:原 NoticeBoard(13.4,8.6) 与市政厅公告板近同位——换黑板课程牌挪东列课桌旁
                new Vector2(1f, 1.5f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "fire_ext", PixelArt.FireExtinguisher(), o + new Vector3(16.2f, 8.6f), 1f),   // 复审:原 (16.9,8.5) 穿 plant_b 且与工厂同位,西移让位
                new Vector2(.625f, 1.125f), new Vector2(0f, 0f));

            NpcAgent.Spawn("学生", o + new Vector3(5.8f, 4.4f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(12.6f, 4.6f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(8.6f, 5.2f), false, false, customers, s.NpcBounds);   // V0.9.14:第三名学生
        }

        /// <summary>河畔工厂布局(V0.9.13,车间):灌装传送带居东(产线台=环境治理站位台前,
        /// 带上一排果汁瓶呼应主线),两台机床分列北墙,西南仓储角货板+麻袋,北墙成品箱
        /// 等距成排。布距硬约束同餐厅(机床 y≥6.65 与传送带 y≤5.05 分离)。</summary>
        static void DecorateInteriorFactory(Transform furn, Transform customers, InteriorSpec s)
        {
            Vector3 o = s.origin;
            AddInteriorSolid(Prop(furn, "conveyor", PixelArt.ConveyorUnit(), o + new Vector3(11f, 4.7f), 1f),
                new Vector2(2.4f, .8f), new Vector2(0f, -.45f));
            float[] machineX = { 4f, 15.6f };
            for (int i = 0; i < machineX.Length; i++)
                // 复审(guardrails):同大理石柱——碰撞贴 sprite 底缘,原 (0,-.9) 南伸 0.79 格空气墙
                AddInteriorSolid(Prop(furn, "machine_" + (char)('a' + i), PixelArt.FactoryMachine(),
                    o + new Vector3(machineX[i], 7.6f), 1f),
                    new Vector2(1.5f, 1.9f), new Vector2(0f, -.11f));
            AddInteriorSolid(Prop(furn, "pallets", PixelArt.PalletStack(), o + new Vector3(1.8f, 2.4f), 1f),
                new Vector2(1.2f, .8f), new Vector2(0f, -.3f));
            AddInteriorSolid(Prop(furn, "sacks", PixelArt.SackPile(), o + new Vector3(3.4f, 2.4f), 1f),
                new Vector2(1f, .75f), new Vector2(0f, -.3f));
            float[] crateX = { 6.4f, 7.6f };
            for (int i = 0; i < crateX.Length; i++)
                AddInteriorSolid(Prop(furn, "crate_" + (char)('a' + i), PixelArt.Crate(),
                    o + new Vector3(crateX[i], 8.4f), 1f),
                    new Vector2(.9f, .7f), new Vector2(0f, -.2f));
            AddInteriorSolid(Prop(furn, "plant_a", PixelArt.PottedPlant(), o + new Vector3(17.1f, 2.2f), 1f),
                new Vector2(.8f, .7f), new Vector2(0f, -.3f));

            // V0.9.14 加密:北墙工具挂板(窗间 4/9 之间)+仓储角双油桶(蓝/橙)+墙角灭火器——车间安全语汇
            AddInteriorSolid(Prop(furn, "toolwall", PixelArt.ToolWall(), o + new Vector3(6.5f, 9.8f), 1f),
                new Vector2(1.625f, 1.375f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "drum_a", PixelArt.OilDrum(0), o + new Vector3(2f, 4.6f), 1f),
                new Vector2(1f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "drum_b", PixelArt.OilDrum(1), o + new Vector3(3.2f, 4.6f), 1f),
                new Vector2(1f, 1.25f), new Vector2(0f, 0f));
            AddInteriorSolid(Prop(furn, "fire_ext", PixelArt.FireExtinguisher(), o + new Vector3(1.5f, 8.5f), 1f),    // 复审:原 (16.9,8.5) 与培训中心同位,挪西墙
                new Vector2(.625f, 1.125f), new Vector2(0f, 0f));

            NpcAgent.Spawn("工厂主", o + new Vector3(7.6f, 3.2f), false, false, customers, s.NpcBounds);
            NpcAgent.Spawn("学生", o + new Vector3(13.4f, 3f), false, false, customers, s.NpcBounds);
        }

        /// <summary>店内家具体碰撞体(落地脚印,照港区 AddPortCargoCollider 先例,带偏移)。</summary>
        static void AddInteriorSolid(GameObject prop, Vector2 size, Vector2 offset)
        {
            if (prop == null) return;
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = offset;
        }

        static void DecoratePortCargo(Transform port)
        {
            Transform cargo = new GameObject("port_static_cargo").transform;
            cargo.SetParent(port, false);

            Vector3[] stackPositions = {
                new Vector3(-13.2f, 2.35f), new Vector3(-10.2f, 2.45f), new Vector3(-7.3f, 2.3f),
                new Vector3(-3.15f, 3.05f), new Vector3(-.15f, 3.05f),
                new Vector3(-2.65f, -1.15f), new Vector3(.2f, -1.1f)
            };
            int[] palettes = { 0, 2, 1, 3, 0, 1, 3 };
            int[] tiers = { 2, 1, 2, 2, 1, 1, 2 };
            for (int i = 0; i < stackPositions.Length; i++)
            {
                GameObject stack = Prop(cargo, "port_decor_solid_container_stack_" + i,
                    PixelArt.HarborContainerStack(palettes[i], tiers[i]), stackPositions[i], 1f);
                AddPortCargoCollider(stack, new Vector2(2.75f, tiers[i] == 2 ? 1.75f : .9f));
            }

            Vector3[] palletPositions = {
                new Vector3(-14f, -2.25f), new Vector3(-7.4f, -.75f), new Vector3(3.7f, 6.65f)
            };
            for (int i = 0; i < palletPositions.Length; i++)
            {
                GameObject pallet = Prop(cargo, "port_decor_solid_pallet_" + i,
                    PixelArt.HarborPalletCargo(), palletPositions[i], i == 2 ? .9f : 1f);
                AddPortCargoCollider(pallet, new Vector2(1.7f, 1.05f));
            }

            Vector3[] drumPositions = { new Vector3(-4.9f, -.55f), new Vector3(4.35f, 6.55f) };
            for (int i = 0; i < drumPositions.Length; i++)
            {
                GameObject drums = Prop(cargo, "port_decor_solid_drums_" + i,
                    PixelArt.HarborDrumRack(), drumPositions[i], 1f);
                AddPortCargoCollider(drums, new Vector2(1.65f, 1f));
            }

            GameObject forklift = Prop(cargo, "port_decor_solid_forklift", PixelArt.HarborForklift(),
                new Vector3(3.35f, -2.05f), .95f);
            AddPortCargoCollider(forklift, new Vector2(1.9f, 1.15f));

            float[] bollardY = { -7.9f, -4f, 0f, 4f, 7.8f };
            for (int i = 0; i < bollardY.Length; i++)
                Prop(cargo, "port_decor_bollard_" + i, PixelArt.HarborBollard(),
                    new Vector3(6.25f, bollardY[i], 0f), .9f);

            Vector3[] lanePositions = {
                new Vector3(-11f, .15f), new Vector3(-5f, .15f),
                new Vector3(1.3f, -3.25f), new Vector3(1.4f, 6.15f)
            };
            for (int i = 0; i < lanePositions.Length; i++)
            {
                GameObject lane = Prop(cargo, "port_decor_lane_marking_" + i,
                    PixelArt.HarborLaneMarking(), lanePositions[i], 1f);
                if (lane != null) lane.GetComponent<SpriteRenderer>().sortingOrder = -900;
            }
        }

        static void AddPortCargoCollider(GameObject prop, Vector2 size)
        {
            if (prop == null) return;
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        static void SpawnGreenSpaceFurniture(Transform parent)
        {
            Vector3 park = TownLayoutData.ParcelCenter("park");
            Vector3[] benches = {
                park + new Vector3(-3f, -1.3f), park + new Vector3(3f, -1.3f),
                TownLayoutData.ParcelCenter("home") + new Vector3(-3.5f, -3.7f)
            };
            for (int i = 0; i < benches.Length; i++)
                Prop(parent, "park_bench_" + i, PixelArt.ParkBench(), benches[i], 1f);

            Vector3[] rocks = {
                park + new Vector3(-3.4f, .8f), park + new Vector3(3.5f, .3f)
            };
            for (int i = 0; i < rocks.Length; i++)
                Prop(parent, "rock_cluster_" + i, PixelArt.RockCluster(), rocks[i], i % 2 == 0 ? 1f : .85f);
        }

        /// <summary>V0.9.6 街具:装饰矮灯/花箱/邮筒/自行车架/铺装长椅/凉亭——常驻、无状态、不进季节表。
        /// 摆位三条纪律：
        ///   ① 伪透视下建筑贴图会遮住其**基线以北**的地面,街具必须摆在基线之南才看得见
        ///      (踩过的坑:摆在门口附近但高于基线,结果整件被墙挡掉);
        ///   ② 只有凉亭带碰撞,其余零碰撞——门通走廊与 LayoutGuard 净距不容街具挤占;
        ///   ③ V0.9.6b 新增:同类街具彼此、与既有 Kenney 街景之间**至少留 1.2 世界单位**,
        ///      且不得落在门洞/院门柱/院界栅线的贴图上——首版 25 件里有 9 件违反,整片读作"乱"
        ///      (用户复验:邮筒紧贴广场公告板、矮灯紧贴既有路灯、长椅横在校园门洞里、
        ///       自行车架与校园门牌同一个 x、店面花箱骑在院门柱上),本版删并到 16 件。
        /// 装饰灯故意不带夜辉:亮灯是「路灯」公共项目的成果(政府要花钱建),这是教学口径不是遗漏。</summary>
        static void SpawnStreetFurniture(Transform parent)
        {
            Vector3 emptyBDoor = TownLayoutData.DoorPoint("empty_b");
            Vector3[] lamps = {
                (ShopDoor + CompetitorDoor) * .5f + new Vector3(0f, .45f),
                MarketDoor + new Vector3(9.5f, .45f),
                (FactoryDoor + emptyBDoor) * .5f + new Vector3(0f, -.25f),
                (emptyBDoor + PortDoor) * .5f + new Vector3(0f, -.95f),
                RestaurantDoor + new Vector3(5f, .45f),
                CinemaDoor + new Vector3(-4f, .45f)
            };
            for (int i = 0; i < lamps.Length; i++)
                Prop(parent, "park_lamp_" + i, PixelArt.ParkLamp(), lamps[i], 1f);

            Vector3[] planters = {
                (ConvenienceDoor + DecorADoor) * .5f + new Vector3(0f, .65f),
                (DecorADoor + RestaurantDoor) * .5f + new Vector3(0f, .65f),
                (CinemaDoor + BankDoor) * .5f + new Vector3(0f, .65f),
                (BankDoor + DecorBDoor) * .5f + new Vector3(0f, .65f),
                (CompetitorDoor + MarketDoor) * .5f + new Vector3(0f, .65f)
            };
            for (int i = 0; i < planters.Length; i++)
                Prop(parent, "planter_box_" + i, PixelArt.PlanterBox(), planters[i], 1f);

            Prop(parent, "mailbox_shop", PixelArt.Mailbox(), ShopDoor + new Vector3(-3f, .5f), 1f);
            Prop(parent, "bike_rack_store", PixelArt.BikeRack(), BankDoor + new Vector3(2.5f, .5f), 1f);
            Prop(parent, "park_bin", KenneyArt.TileSprite(326),
                TownLayoutData.DecorPoint("park_bin"), 1f);

            // 座椅按功能分区而非机械对称：西侧座椅服务信息区，东南座椅避开东北仪典区。
            Prop(parent, "plaza_bench_a", PixelArt.ParkBench(),
                TownLayoutData.DecorPoint("plaza_bench_w"), 1f);
            Prop(parent, "plaza_bench_b", PixelArt.ParkBench(),
                TownLayoutData.DecorPoint("plaza_bench_se"), 1f);

            // 凉亭:口袋公园的视觉中心,立在小径西侧、基线(−5.5)在小径(y=−6 那排砖)之北
            Vector3 gazeboPosition = TownLayoutData.DecorPoint("park_gazebo");
            GameObject gazebo = Prop(parent, "park_gazebo", PixelArt.Gazebo(), gazeboPosition, 1f);
            if (gazebo != null)
            {
                BoxCollider2D col = gazebo.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1.6f, .8f);
                col.offset = new Vector2(0f, -.66f);
                GameObject shadow = new GameObject("shadow");
                shadow.transform.SetParent(gazebo.transform, false);
                shadow.transform.localPosition = new Vector3(0f, -1.02f, 0f);
                SpriteRenderer sr = shadow.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.PropShadow();
                sr.sortingOrder = SortingForY(gazeboPosition.y) - 1;
            }
        }

        /// <summary>V0.7.1 季节性树木/花丛：换季销毁重建的对象仅限这两类(tree_/flower_ 前缀)。
        /// 花:春 27 次(基线 +50%)/夏秋 18 次/冬不生成(PRD §122);rng 种子固定,分布与旧版一致。</summary>
        static void SpawnTreesAndFlowers(Transform parent, int season)
        {
            Vector3 training = TownLayoutData.ParcelCenter("training");
            Vector3 home = TownLayoutData.ParcelCenter("home");
            Vector3 hall = TownLayoutData.ParcelCenter("hall");
            Vector3 finance = TownLayoutData.ParcelCenter("centralbank");
            Vector3 park = TownLayoutData.ParcelCenter("park");
            Vector3 housing = TownLayoutData.ParcelCenter("housing_growth");

            // 树木只在院落边缘、街区缓冲带和南缘草坡成组出现。
            Vector3[] spots = {
                training + new Vector3(-4.5f, 3.5f), training + new Vector3(5f, 3.5f),
                training + new Vector3(-4.5f, -3f), home + new Vector3(-4.8f, 3.5f),
                home + new Vector3(3.5f, 3.5f), hall + new Vector3(-7f, 4.5f),
                hall + new Vector3(7f, 4.5f), finance + new Vector3(-7f, 4f),
                finance + new Vector3(7f, 4f), park + new Vector3(-3.5f, 1f),
                park + new Vector3(3.5f, 1f), housing + new Vector3(-3f, 1.5f),
                housing + new Vector3(3f, 1.5f),
                TownLayoutData.ParcelCenter("factory") + new Vector3(-4f, -11.5f),
                TownLayoutData.ParcelCenter("factory") + new Vector3(4f, -11.5f),
                TownLayoutData.ParcelCenter("port") + new Vector3(-3f, -11.5f),
                TownLayoutData.ParcelCenter("port") + new Vector3(3f, -11.5f),
                TownLayoutData.ParcelCenter("empty_b") + new Vector3(0f, -11.5f)
            };
            for (int i = 0; i < spots.Length; i++)
                Prop(parent, "tree_" + i, PixelArt.Tree(season), spots[i], 1.2f);

            // 灌木按绿地边缘成组布置：给大块草地层次，但不切断通往建筑的视线。
            Vector3[] shrubs = {
                training + new Vector3(-5f, 1f), training + new Vector3(5f, 1f),
                training + new Vector3(-5f, -1.5f), home + new Vector3(-3.2f, 1.5f),
                home + new Vector3(3.2f, 1.5f), hall + new Vector3(-7f, 1.5f),
                hall + new Vector3(7f, 1.5f), finance + new Vector3(-7f, 1f),
                finance + new Vector3(7f, 1f), park + new Vector3(-4f, -1.7f),
                park + new Vector3(4f, -1.7f), housing + new Vector3(-3f, -1.5f),
                housing + new Vector3(3f, -1.5f),
                TownLayoutData.ParcelCenter("factory") + new Vector3(-5f, -9f),
                TownLayoutData.ParcelCenter("factory") + new Vector3(0f, -9f),
                TownLayoutData.ParcelCenter("port") + new Vector3(-5.5f, -9f),
                TownLayoutData.ParcelCenter("port") + new Vector3(.5f, -9f),
                TownLayoutData.ParcelCenter("empty_b") + new Vector3(-1f, -9f)
            };
            for (int i = 0; i < shrubs.Length; i++)
                Prop(parent, "shrub_" + i, PixelArt.Shrub(season), shrubs[i], i % 3 == 0 ? 1.05f : .9f);

            // 花木只做成可读的小组团，撤掉全图随机散点。
            if (season != 3)
            {
                Vector3[] meadowFlowers = {
                    park + new Vector3(-4f, .3f), park + new Vector3(-3.3f, .6f), park + new Vector3(-3.5f, -.1f),
                    housing + new Vector3(-3f, -.8f), housing + new Vector3(-2.3f, -.5f), housing + new Vector3(-2.6f, -1.2f),
                    hall + new Vector3(-7f, -1f), hall + new Vector3(-6.3f, -.7f), hall + new Vector3(-6.6f, -1.4f),
                    finance + new Vector3(7f, -1f), finance + new Vector3(6.3f, -.7f), finance + new Vector3(6.6f, -1.4f),
                    training + new Vector3(5f, -2.2f), training + new Vector3(4.3f, -2f), training + new Vector3(4.6f, -2.7f),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(-4f, -9.5f),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(-3.3f, -9.2f),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(-3.6f, -9.9f)
                };
                for (int i = 0; i < meadowFlowers.Length; i++)
                    Prop(parent, "flower_cluster_" + i, PixelArt.Flower(season, i % 3), meadowFlowers[i], 1.05f);
            }

            // 高草只标记已声明的绿化缓冲带，不再沿地图边界撒一圈。
            Vector3[] grassTufts = {
                park + new Vector3(-4f, -1.8f), park + new Vector3(4f, -1.8f),
                housing + new Vector3(-3f, -2f), housing + new Vector3(3f, -2f),
                training + new Vector3(-5f, 1.5f), home + new Vector3(4f, 1.5f),
                hall + new Vector3(-6.5f, 1f), finance + new Vector3(6.5f, 1f),
                TownLayoutData.ParcelCenter("factory") + new Vector3(-4f, -11f),
                TownLayoutData.ParcelCenter("port") + new Vector3(-4f, -11f),
                TownLayoutData.ParcelCenter("empty_b") + new Vector3(0f, -10f)
            };
            for (int i = 0; i < grassTufts.Length; i++)
                Prop(parent, "grass_tuft_" + i, PixelArt.GrassTuft(season), grassTufts[i], i % 4 == 0 ? 1.15f : 1f);

            if (season != 3)
            {
                Vector3[] meadowPatches = {
                    park + new Vector3(-4f, -1f), park + new Vector3(4f, -1f),
                    housing + new Vector3(-3f, 1f), housing + new Vector3(3f, 1f),
                    TownLayoutData.DecorPoint("south_meadow_factory"),
                    TownLayoutData.DecorPoint("south_meadow_port"),
                    TownLayoutData.DecorPoint("south_meadow_empty"),
                    TownLayoutData.DecorPoint("finance_east_meadow")
                };
                for (int i = 0; i < meadowPatches.Length; i++)
                    Prop(parent, "meadow_patch_" + i, PixelArt.MeadowPatch(season, i % 3),
                        meadowPatches[i], i % 2 == 0 ? 1.05f : .92f);
            }
        }

        static bool IsGrassCell(Vector3 position)
        {
            if (groundTilemap == null) return false;
            Tile tile = groundTilemap.GetTile<Tile>(Vector3Int.FloorToInt(position));
            return tile != null && grassTileVariant.ContainsKey(tile);
        }

        /// <summary>V0.7.1 换季刷新：同变体号替换草地 Tile、重建树/花、建筑增删雪帽。
        /// 不调用 PixelArt.ResetCache(它无条件 Destroy 全部在渲染贴图)；路/广场 Tile 与全部建筑对象原样保留。</summary>
        public static void ApplySeason(GameState state)
        {
            if (townRoot == null || groundTilemap == null) return;
            int season = state != null ? AtmosphereService.SeasonIndex(state.day) : 0;
            bool winterChanged = appliedSeason != season;
            if (!winterChanged) return;

            // 1) 草地:遍历既有 cell,凡注册过的草 Tile 按变体号换当季贴图(路面/广场不动)
            Tile[] fresh = null;
            foreach (Vector3Int pos in groundTilemap.cellBounds.allPositionsWithin)
            {
                Tile cur = groundTilemap.GetTile<Tile>(pos);
                if (cur == null || !grassTileVariant.TryGetValue(cur, out int variant)) continue;
                if (fresh == null) fresh = new[] { MakeGrassTile(season, 0), MakeGrassTile(season, 1), MakeGrassTile(season, 2) };
                groundTilemap.SetTile(pos, fresh[variant]);
            }
            grassTileVariant.Clear();
            if (fresh != null)
                for (int v = 0; v < 3; v++) grassTileVariant[fresh[v]] = v;

            // 2) 树/花:精准销毁前缀命中的季节装饰后按当季重挂
            ClearSeasonalDecor();
            SpawnTreesAndFlowers(townRoot, season);

            // 3) 建筑雪帽:冬季挂 overlay 子贴图,其余季移除
            SetSnowOverlays(season == 3);
            appliedSeason = season;
        }

        static void ClearSeasonalDecor()
        {
            for (int i = townRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = townRoot.GetChild(i);
                string n = child.name;
                if (n.StartsWith("tree_") || n.StartsWith("flower_") || n.StartsWith("shrub_") ||
                    n.StartsWith("grass_tuft_") || n.StartsWith("meadow_patch_"))
                    DestroyObject(child.gameObject);
            }
        }

        static void SetSnowOverlays(bool winter)
        {
            SpriteRenderer[] renderers = townRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Transform t = renderers[i].transform;
                if (t.name != "snow_overlay" || !t.parent.name.StartsWith("B_")) continue;
                if (!winter) { DestroyObject(t.gameObject); continue; }
                renderers[i].sprite = PixelArt.SnowCap(KindOfBuilding(t.parent));   // 冬季保持存在时刷新贴图引用
            }
            if (!winter) return;
            // bug搜#15:添加路径与销毁/刷新路径同口径递归——东商业新区的书店/空置铺挂在
            // growth_east/completed 之下,只扫 townRoot 直接子级会漏掉它们(雪景中一排不积雪)。
            Transform[] all = townRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform building = all[i];
                if (!building.name.StartsWith("B_")) continue;
                if (building.Find("snow_overlay") != null) continue;
                SpriteRenderer sr = new GameObject("snow_overlay").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(building, false);
                sr.sprite = PixelArt.SnowCap(KindOfBuilding(building));             // 每栋按登记雪线描白
                // 围猎三#23:+1 与窗光 overlay(夜光)同层,渲染顺序随挂载先后不稳定——
                // 压到 +2 让雪帽明确盖在窗光之上(冬季雪檐在夜光窗外的物理位置)。
                sr.sortingOrder = SortingForY(building.position.y) + 2;
            }
        }

        static string KindOfBuilding(Transform building)
        {
            BuildingIdentity identity = building.GetComponent<BuildingIdentity>();
            if (identity != null) return identity.kind;
            BuildingClick click = building.GetComponent<BuildingClick>();
            return click != null ? click.kind : "home";
        }

        static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        static Tile MakeGrassTile(int season, int variant)
        {
            Tile tile = MakeTile(PixelArt.Grass(season, variant));
            grassTileVariant[tile] = variant;
            return tile;
        }

        // ————— V0.7.1 小镇节日彩旗灯笼(事件视觉,随 atmosphere.festivalDecor 缓存切换) —————

        static bool festivalShown;
        static bool festivalNight;

        /// <summary>小镇节日的广场彩旗串;night=true 时切夜态贴图(灯笼亮黄)。want=false 全部拆除。</summary>
        public static void UpdateFestivalDecor(bool want, bool night)
        {
            if (townRoot == null || (want == festivalShown && (!want || night == festivalNight))) return;
            if (!want)
            {
                for (int i = townRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = townRoot.GetChild(i);
                    if (child.name.StartsWith("festival_")) DestroyObject(child.gameObject);
                }
                festivalShown = false;
                return;
            }

            Sprite dayNightSprite = PixelArt.FestivalBunting(night);
            Vector3[] spots = {
                TownLayoutData.DecorPoint("festival_plaza_w"),
                TownLayoutData.DecorPoint("festival_plaza_e"),
                TownLayoutData.DecorPoint("festival_hall_w"),
                TownLayoutData.DecorPoint("festival_hall_e")
            };
            int placed = 0;
            for (int i = townRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = townRoot.GetChild(i);
                if (!child.name.StartsWith("festival_bunting_")) continue;
                child.GetComponent<SpriteRenderer>().sprite = dayNightSprite;   // 已挂过:只换日夜贴图
                placed++;
            }
            for (int i = placed; i < spots.Length; i++)
                Prop(townRoot, "festival_bunting_" + i, dayNightSprite, spots[i], 1.05f);
            festivalShown = true;
            festivalNight = night;
        }

        // ————— V0.7.1 经济状态可视化(PRD §124–125):画面是状态的函数,全部只读 atmosphere 缓存 —————

        static bool IsCommercial(string kind)
        {
            return kind == "market" || kind == "competitor" || kind == "restaurant" ||
                   kind == "cinema" || kind == "convenience";
        }

        /// <summary>转让牌(装饰铺)/营业时间调整告示(商铺类交互建筑)/过热杂物/价目牌的统一驱动。
        /// AtmosphereDirector 每帧调用;内部全部按缓存值差值短路,不产生 GC。</summary>
        public static void UpdateEcoVisualization(GameState s)
        {
            if (townRoot == null || s == null || s.economy == null) return;
            var atm = s.economy.atmosphere;
            if (atm == null) return;
            bool eco = s.experience.settings.ecoVisibility;
            bool lowConfidence = atm.windowTier == 0;

            // 1) 木牌:信心 <40 时装饰铺按池选挂「转让」,商铺类交互建筑改挂「营业时间调整」
            foreach (ShopFacade f in facades)
            {
                string want = "";
                if (eco && lowConfidence)
                {
                    if (f.decorativeIndex >= 0 && f.decorativeIndex == atm.transferSignShop) want = "transfer";
                    else if (!f.isPlayerShop && f.decorativeIndex < 0 && IsCommercial(f.kind)) want = "notice";
                }
                if (want != f.signKind)
                {
                    if (f.signHolder != null) { DestroyObject(f.signHolder); f.signHolder = null; }
                    f.signKind = want;
                    f.signHolder = want != "" ? AttachSign(f.building, want) : null;
                }
            }

            // 2) 过热负信号(PRD §125「人最多≠最好」的教学对冲):垃圾桶旁小堆积杂物
            bool wantClutter = eco && s.macro.cyclePhase == MacroCyclePhase.Overheating;
            if (wantClutter && overheatClutter == null)
            {
                GameObject go = new GameObject("overheat_clutter");
                go.transform.SetParent(townRoot, false);
                // 过热杂物属于果汁店地块，始终跟随店铺门前垃圾桶，不再沿用旧城绝对坐标。
                go.transform.position = TownLayoutData.DecorPoint("shop_overheat_clutter");
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.ClutterPile();
                sr.sortingOrder = SortingForY(go.transform.position.y);
                overheatClutter = go;
            }
            else if (!wantClutter && overheatClutter != null)
            {
                DestroyObject(overheatClutter);
                overheatClutter = null;
            }

            // 3) 价目牌:基准价 × CPI 分项指数,同比 >3% 染红("钱变毛了"的日常载体)
            // 围猎五#6:拼接与染色按指数差值短路——旧代码每帧先拼字符串再比较,3 牌 6 串/帧
            // 是稳定 GC 源(与函数头「差值短路不产生 GC」的纪律注释相悖);指数只在收日结算变化。
            foreach (PriceBoard board in priceBoards)
            {
                if (board.text == null) continue;
                bool showBoard = eco;
                if (board.text.gameObject.activeSelf != showBoard) board.text.gameObject.SetActive(showBoard);
                if (!showBoard) continue;
                float index = PriceIndexOf(s, board.series);
                if (index == board.lastIndex) continue;   // NaN 首帧必不相等,正常短路
                board.lastIndex = index;
                float price = board.basePriceYuan * index / 100f;
                board.text.text = board.label + " ¥" + price.ToString("0.0");
                board.text.color = index > 103f
                    ? new Color32(0xa8, 0x32, 0x24, 255)
                    : new Color32(0x3a, 0x2d, 0x24, 235);
            }
        }

        static float PriceIndexOf(GameState s, int series)
        {
            switch (series)
            {
                case 0: return s.macro.foodPriceIndex;
                case 1: return s.macro.otherPriceIndex;
                default: return s.macro.entertainmentPriceIndex;
            }
        }

        /// <summary>橱窗灯光 α(world=非玩家建筑,tint 由档位与夜幕合成)。AtmosphereDirector 每帧调用。</summary>
        public static void ApplyWindowLightAlpha(float worldAlpha, float playerAlpha)
        {
            for (int i = 0; i < facades.Count; i++)
            {
                ShopFacade f = facades[i];
                if (f.glow == null) continue;
                float a = f.isPlayerShop ? playerAlpha : worldAlpha;
                if (!Mathf.Approximately(f.glow.color.a, a))
                    f.glow.color = new Color(1f, 1f, 1f, a);
            }
        }

        /// <summary>墙上挂牌:「转让」大字红 / 「营业时间调整」小字墨(TextMesh 世界文字,FloatText 先例)。</summary>
        static GameObject AttachSign(Transform building, string kindName)
        {
            GameObject holder = new GameObject(kindName == "transfer" ? "transfer_sign" : "hours_notice");
            holder.transform.SetParent(building, false);
            // V0.8.1：挂牌锚点随建筑规格表走（M/L 档画面更宽，锚点按比例外移）
            holder.transform.localPosition = BuildingSpecs.Get(KindOfBuilding(building)).signAnchor;
            SpriteRenderer sr = holder.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.WoodenSign();
            sr.sortingOrder = SortingForY(building.position.y) + 2;

            GameObject textGo = new GameObject("sign_text");
            textGo.transform.SetParent(holder.transform, false);
            textGo.transform.localPosition = kindName == "transfer"
                ? new Vector3(0f, 0.28f, 0f) : new Vector3(0f, 0.30f, 0f);
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.font = UiTheme.GameFont;
            if (kindName == "transfer")
            {
                tm.text = "转让";
                tm.fontSize = 30;
                tm.characterSize = 0.20f;
                tm.color = new Color32(0xa0, 0x32, 0x24, 255);
            }
            else
            {
                tm.text = "营业时间\n调整";
                tm.fontSize = 16;
                tm.characterSize = 0.17f;
                tm.color = new Color32(0x46, 0x36, 0x28, 245);
            }
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = textGo.AddComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = SortingForY(building.position.y) + 3;
            return holder;
        }

        /// <summary>三块价目牌:餐厅←食品指数×¥12 / 便利屋←其他指数×¥9 / 电影院←娱乐指数×¥35(PRD §125)。</summary>
        static void EnsurePriceBoards()
        {
            if (townRoot == null || priceBoards.Count > 0) return;
            Vector3[] spots =
            {
                RestaurantDoor + new Vector3(-3f, .7f),
                ConvenienceDoor + new Vector3(3f, .7f),
                CinemaDoor + new Vector3(-3f, .7f)
            };
            int[] series = { 0, 1, 2 };
            float[] basePrices = { 12f, 9f, 35f };
            string[] labels = { "套餐", "日用", "电影" };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject go = new GameObject("price_board_" + i);
                go.transform.SetParent(townRoot, false);
                go.transform.position = spots[i];
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.WoodenSign();
                sr.sortingOrder = SortingForY(spots[i].y);

                GameObject textGo = new GameObject("board_text");
                textGo.transform.SetParent(go.transform, false);
                textGo.transform.localPosition = new Vector3(0f, 0.34f, 0f);
                TextMesh tm = textGo.AddComponent<TextMesh>();
                tm.font = UiTheme.GameFont;
                tm.fontSize = 17;
                tm.characterSize = 0.19f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color32(0x3a, 0x2d, 0x24, 235);
                MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
                if (mr == null) mr = textGo.AddComponent<MeshRenderer>();
                if (tm.font != null) mr.sharedMaterial = tm.font.material;
                mr.sortingOrder = SortingForY(spots[i].y) + 1;

                priceBoards.Add(new PriceBoard { text = tm, series = series[i], basePriceYuan = basePrices[i], label = labels[i] });
            }
        }

        static GameObject Prop(Transform parent, string name, Sprite s, Vector3 pos, float scale)
        {
            if (s == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = SortingForY(pos.y);
            return go;
        }

        /// <summary>把公共项目状态反馈到小镇地图(V0.8.3 双态化:预留态/建成态),不创建第二份项目状态。
        /// 五项目全部只读 government.projects[].built——零新状态、零新交互点、零新存档字段(PRD §175–179)。</summary>
        public static void RefreshGovernmentProjects(GameState state)
        {
            if (publicProjectsRoot == null) return;
            for (int i = publicProjectsRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = publicProjectsRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
            publicLightGlows.Clear();
            // 既有街灯的夜辉挂在 Town 下的灯杆上(不随 publicProjectsRoot 销毁重建),须显式清理——
            // 否则重复刷新会累积、撤拨款后会残留。
            if (TownRoot != null)
            {
                string[] streetLamps = { "west_lamp", "east_lamp", "neighborhood_lamp", "trade_lamp" };
                for (int i = 0; i < streetLamps.Length; i++)
                {
                    Transform lamp = TownRoot.Find(streetLamps[i]);
                    if (lamp == null) continue;
                    Transform staleGlow = lamp.Find("head_glow");
                    if (staleGlow != null)
                    {
                        if (Application.isPlaying) Object.Destroy(staleGlow.gameObject);
                        else Object.DestroyImmediate(staleGlow.gameObject);
                    }
                }
            }
            if (state == null) return;
            GovernmentSimulator.Ensure(state);

            // ① 路灯:灯杆常驻(素头),建成=挂夜辉(unlit 材质,α 由 AtmosphereDirector 随夜幕驱动)——路灯项目=夜晚亮灯
            Prop(publicProjectsRoot, "public_light_west", PixelArt.Lamp(), TownLayoutData.PlazaCenter + new Vector3(-5.7f, -.9f), 1.05f);
            Prop(publicProjectsRoot, "public_light_east", PixelArt.Lamp(), TownLayoutData.PlazaCenter + new Vector3(5.7f, -.9f), 1.05f);
            if (ProjectBuilt(state, PublicProjectType.StreetLights)) AttachStreetLightGlows();

            // ② 公园:预留=空草地+「预留绿地」木牌;建成=矮篱周界+花砖小径+长椅+花木(地块化)
            Vector3 publicPark = TownLayoutData.PublicParkCenter;
            if (ProjectBuilt(state, PublicProjectType.Park))
            {
                Prop(publicProjectsRoot, "park_perimeter", PixelArt.ParkPerimeter(), publicPark, 1f);
                Prop(publicProjectsRoot, "park_path_0", PixelArt.Plaza(0), publicPark + new Vector3(0f, -1.25f), .95f);
                Prop(publicProjectsRoot, "park_path_1", PixelArt.Plaza(1), publicPark + new Vector3(0f, -.7f), .95f);
                Prop(publicProjectsRoot, "park_path_2", PixelArt.Plaza(0), publicPark + new Vector3(0f, -.15f), .95f);
                Prop(publicProjectsRoot, "park_tree_a", PixelArt.Tree(), publicPark + new Vector3(-.5f, .1f), 1.1f);
                Prop(publicProjectsRoot, "park_tree_b", PixelArt.Tree(), publicPark + new Vector3(1.2f, .8f), 1.0f);
                Prop(publicProjectsRoot, "public_park_bench_a", PixelArt.ParkBench(), publicPark + new Vector3(-1.2f, -.3f), 1f);
                Prop(publicProjectsRoot, "public_park_bench_b", PixelArt.ParkBench(), publicPark + new Vector3(1.3f, .9f), 1f);
                for (int i = 0; i < 5; i++)
                    Prop(publicProjectsRoot, "park_flower_" + i, PixelArt.Flower(i % 3),
                        publicPark + new Vector3(-1f + i * .65f, -.9f + (i % 2) * .35f), .9f);
            }
            else
            {
                GameObject parkSign = Prop(publicProjectsRoot, "park_reserved_sign", PixelArt.WoodenSign(),
                    publicPark + new Vector3(0f, -.2f), 1f);   // 预留木牌
                // 字标收进牌面:原 +0.45/大字号(0.36)会整幅浮出小板,读作草地上方悬着的幽灵字
                // V0.9.6d:字号 .17 在实机里糊成一片(用户截图),提到 .24——木牌宽度容得下四个字
                if (parkSign != null) MakeLabel(parkSign.transform, "预留绿地", new Vector3(0f, .08f, 0f), .24f, 22);
            }

            // ③ 公交站:预留=告示板(现状);建成=真候车亭(顶棚/座位/时刻牌,带碰撞体)
            if (ProjectBuilt(state, PublicProjectType.BusStop))
            {
                GameObject shelter = Prop(publicProjectsRoot, "public_bus_shelter", PixelArt.BusShelter(),
                    TownLayoutData.BusStopCenter, .95f);
                if (shelter != null)
                {
                    BoxCollider2D col = shelter.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(1.9f, .8f);
                    col.offset = new Vector2(0f, -.45f);
                }
            }
            else
                Prop(publicProjectsRoot, "public_bus_stop", PixelArt.NoticeBoard(),
                    TownLayoutData.BusStopCenter + new Vector3(0f, -.65f), .9f);

            // ④ 社区学校扩建:基础校舍常驻；预留态立项木牌，建成后只增加校舍屋冠，不开放室内。
            if (ProjectBuilt(state, PublicProjectType.School))
            {
                Transform school = TownRoot != null ? TownRoot.Find("B_社区学校_school") : null;
                BuildingSpec spec = BuildingSpecs.Get("school");
                const float annexCanvasHeight = 26f;
                float annexHalfHeight = annexCanvasHeight / 16f * spec.scale * .5f;
                float roofTop = (school != null ? school.position.y : TownLayoutData.BuildingCenter("school").y) +
                    spec.canvasH / 16f * spec.scale * .5f;
                // 屋冠向原校舍屋顶内嵌，只增加教育地标细节，不与培训楼或市政厅争高度。
                Prop(publicProjectsRoot, "school_annex", PixelArt.SchoolAnnex(),
                    new Vector3(school != null ? school.position.x : TownLayoutData.BuildingCenter("school").x,
                        roofTop - annexHalfHeight * .35f), spec.scale);
            }
            else
                Prop(publicProjectsRoot, "school_public_notice", PixelArt.NoticeBoard(),
                    TownLayoutData.DecorPoint("school_notice"), .85f);

            // ⑤ 环保处理设施:预留=两只木箱(现状);建成=净化塔+管道(带小碰撞体)
            if (ProjectBuilt(state, PublicProjectType.GreenFacility))
            {
                GameObject tower = Prop(publicProjectsRoot, "green_purifier", PixelArt.PurificationTower(),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(5f, 2.3f), .95f);
                if (tower != null)
                {
                    BoxCollider2D col = tower.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(1.0f, 1.0f);
                    col.offset = new Vector2(0f, .35f);
                }
            }
            else
            {
                Prop(publicProjectsRoot, "green_facility_a", PixelArt.Crate(),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(-4.2f, -.8f), .9f);
                Prop(publicProjectsRoot, "green_facility_b", PixelArt.Crate(),
                    TownLayoutData.ParcelCenter("factory") + new Vector3(-4.2f, .4f), .9f);
            }
        }

        static readonly System.Collections.Generic.List<SpriteRenderer> publicLightGlows =
            new System.Collections.Generic.List<SpriteRenderer>();

        /// <summary>路灯项目建成:给两根公共灯杆与四盏既有街灯挂夜辉(§176「挂 public_light_* 与既有街灯」)。</summary>
        static void AttachStreetLightGlows()
        {
            AttachLampGlow(publicProjectsRoot.Find("public_light_west"), new Vector3(0f, 9f / 16f, 0f));
            AttachLampGlow(publicProjectsRoot.Find("public_light_east"), new Vector3(0f, 9f / 16f, 0f));
            if (TownRoot == null) return;
            AttachLampGlow(TownRoot.Find("west_lamp"), new Vector3(0f, 5f / 16f, 0f));
            AttachLampGlow(TownRoot.Find("east_lamp"), new Vector3(0f, 5f / 16f, 0f));
            AttachLampGlow(TownRoot.Find("neighborhood_lamp"), new Vector3(0f, 5f / 16f, 0f));
            AttachLampGlow(TownRoot.Find("trade_lamp"), new Vector3(0f, 5f / 16f, 0f));
        }

        /// <summary>灯杆头部挂暖光辉(α=0 起步,unlit 材质同橱窗窗光纪律;父级缩放自动作用于世界偏移)。</summary>
        static void AttachLampGlow(Transform post, Vector3 headLocalPos)
        {
            if (post == null) return;
            GameObject glowGo = new GameObject("head_glow");
            glowGo.transform.SetParent(post, false);
            glowGo.transform.localPosition = headLocalPos;
            SpriteRenderer sr = glowGo.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.LampGlow();
            SpriteRenderer postSr = post.GetComponent<SpriteRenderer>();
            sr.sortingOrder = postSr != null ? postSr.sortingOrder + 1 : 0;
            if (unlitSpriteMaterial == null)
                unlitSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            sr.sharedMaterial = unlitSpriteMaterial;
            sr.color = new Color(1f, 1f, 1f, 0f);
            publicLightGlows.Add(sr);
        }

        /// <summary>路灯夜辉 α(0..1),由 AtmosphereDirector 每帧驱动;项目未建成时辉光列表为空,调用无副作用。</summary>
        public static void ApplyPublicLightAlpha(float alpha)
        {
            float a = Mathf.Clamp01(alpha);
            for (int i = 0; i < publicLightGlows.Count; i++)
                if (publicLightGlows[i] != null) publicLightGlows[i].color = new Color(1f, 1f, 1f, a);
        }

        static bool ProjectBuilt(GameState state, PublicProjectType type)
        {
            if (state == null || state.government == null || state.government.projects == null) return false;
            for (int i = 0; i < state.government.projects.Count; i++)
                if (state.government.projects[i].type == type) return state.government.projects[i].built;
            return false;
        }

        // ————— V0.7.3《生长的小镇》三处新区（PRD §140–146）—————
        // 内容全量预建、按 completedChapter 推导的两态切换（NotStarted 整组隐藏）。
        // 新区不迁移任何既有功能：交互点是增量观赏/投影点，既有 14 建筑交互点坐标不动。

        /// <summary>按章节推进切换施工/完工两态；换季时只重建带 gd_ 前缀的季节性绿化。
        /// 调用点：Build 末尾、次晨 TownGrowthDirector.BeginMorning、读档 NotifyStateChanged。</summary>
        public static void RefreshGrowthDistricts(GameState state)
        {
            if (townRoot == null) return;
            int cc = state != null && state.progression != null ? state.progression.completedChapter : 0;
            ApplyDistrictPhase(GrowthEastRoot, TownGrowthService.EastDistrict(cc));
            ApplyDistrictPhase(GrowthPlazaRoot, TownGrowthService.CivicPlaza(cc));
            ApplyDistrictPhase(GrowthFinancialRoot, TownGrowthService.FinancialStreet(cc));
            RefreshHousingMarkers(state);   // V0.9.3 住房要素同入口刷新(铭牌三态+新开工地块)

            int season = state != null ? AtmosphereService.SeasonIndex(state.day) : 0;
            if (season != appliedGrowthSeason)
            {
                appliedGrowthSeason = season;
                RebuildDistrictGreenery(GrowthPlazaRoot, season);
            }
        }

        // ————— V0.9.3《杠杆周期》住房地图要素(PRD §196.6,本弧 2 处封顶) —————

        static Transform housingStartsRoot;   // 要素②:Inflating 新开工地块(仅泡沫期可见)
        static Transform homePlaqueOwn, homePlaqueRent, homePlaqueForeclose;   // 要素①:门侧三态铭牌

        /// <summary>要素①:自家门侧「自有/租住/法拍」小铭牌。R8 设计禁例(V0.8.2d 撤拱徽教训):
        /// 贴墙面、不横越门位、无碰撞;三态子物体挂家 root 下按状态切换 active(照 ApplyDistrictPhase
        /// 切换形态,不重建)。</summary>
        static void BuildHousingPlaque(Transform root)
        {
            Transform home = root.Find("B_我的家_home");
            if (home == null) return;
            // 门在家 spec 南面中心(doorOffset (0,−2.45)):铭牌立门右侧墙面,离开门柱带
            Vector3 spot = home.position + new Vector3(1.05f, -1.8f, 0f);
            homePlaqueOwn = BuildPlaqueState(home, "plaque_own", "自有", spot, new Color32(0x35, 0x6f, 0x4a, 255));
            homePlaqueRent = BuildPlaqueState(home, "plaque_rent", "租住", spot, new Color32(0x8a, 0x6d, 0x3b, 255));
            homePlaqueForeclose = BuildPlaqueState(home, "plaque_foreclose", "法拍", spot, new Color32(0x8f, 0x3a, 0x30, 255));
            // bug搜#12:Build() 里 BuildGrowthDistricts(末尾 RefreshHousingMarkers)先于本函数执行,
            // 那时三块铭牌还是 null 整块跳过——创建后立即投影一次,否则新局首日「自有/租住/法拍」
            // 三块铭牌同点叠印(Unity 默认 active,要等首次收日/读档才自愈)。
            RefreshHousingMarkers(GameState.I);
        }

        static Transform BuildPlaqueState(Transform home, string name, string word, Vector3 worldPos, Color tint)
        {
            GameObject plaque = new GameObject(name);
            plaque.transform.SetParent(home, false);
            plaque.transform.position = worldPos;
            plaque.transform.localScale = new Vector3(.5f, .5f, 1f);
            SpriteRenderer sr = plaque.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.WoodenSign();
            sr.color = tint;
            sr.sortingOrder = SortingForY(worldPos.y) + 2;   // 贴墙读数(AttachSign 同款层级)
            GameObject label = new GameObject(name + "_text");
            label.transform.SetParent(plaque.transform, false);
            label.transform.localPosition = new Vector3(0f, .04f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = word;
            text.font = UiTheme.GameFont;
            text.fontSize = 22;
            text.characterSize = .17f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color32(0xff, 0xf3, 0xd9, 255);
            MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
            if (labelRenderer == null) labelRenderer = label.AddComponent<MeshRenderer>();
            if (text.font != null) labelRenderer.sharedMaterial = text.font.material;
            labelRenderer.sortingOrder = SortingForY(worldPos.y) + 3;
            return plaque.transform;
        }

        /// <summary>要素②:新开工地块脚手架——仅 Inflating 相位出现。BuildPlotSite 形态(围栏+小碰撞+
        /// 「新开工」牌),坐标东商业新区南缘空置带,以 LayoutGuard 全对净距护栏实测选定。</summary>
        static void BuildHousingStartsSite(Transform root)
        {
            housingStartsRoot = NewDistrictRoot(root, "housing_starts_site");
            // 新开工在南缘保留地块内；不再挂在某栋空铺的临时偏移上。
            Vector3 site = TownLayoutData.ParcelCenter("housing_growth");
            BuildPlotSite(housingStartsRoot, site, 2.6f, 2.2f,
                site.x - 1.2f, site.y - 1.1f, site.x - 1.2f, site.y + 1.1f);
            MakeConstructionSign(housingStartsRoot, site + new Vector3(0f, 1.5f), "新开工");
            housingStartsRoot.gameObject.SetActive(false);   // 初始相位 None:隐藏(可见性只由相位驱动)
        }

        /// <summary>住房地图要素状态投影(只读 housing 块):铭牌三态(法拍余晖优先于租住)+
        /// 新开工地块仅 Inflating。挂 RefreshGrowthDistricts 同入口刷新。</summary>
        public static void RefreshHousingMarkers(GameState state)
        {
            if (state == null || state.housing == null) return;
            HousingState h = state.housing;
            if (homePlaqueOwn != null)
            {
                bool foreclose = h.foreclosureFlash > 0;
                homePlaqueOwn.gameObject.SetActive(!h.renting);
                homePlaqueRent.gameObject.SetActive(h.renting && !foreclose);
                homePlaqueForeclose.gameObject.SetActive(h.renting && foreclose);
            }
            if (housingStartsRoot != null)
                housingStartsRoot.gameObject.SetActive(h.leveragePhase == LeveragePhase.Inflating);
        }

        static void ApplyDistrictPhase(Transform root, DistrictPhase phase)
        {
            if (root == null) return;
            if (phase == DistrictPhase.NotStarted)
            {
                root.gameObject.SetActive(false);
                return;
            }
            root.gameObject.SetActive(true);
            Transform site = root.Find("construction_site");
            Transform done = root.Find("completed");
            if (site != null) site.gameObject.SetActive(phase == DistrictPhase.UnderConstruction);
            if (done != null) done.gameObject.SetActive(phase == DistrictPhase.Completed);
        }

        /// <summary>旗面颜色 = 当前财政立场（§141：扩张红/紧缩蓝/中性绿，只读投影）。</summary>
        public static void SetGrowthFlagColor(Color color)
        {
            if (GrowthFlagCloth != null) GrowthFlagCloth.GetComponent<SpriteRenderer>().color = color;
        }

        /// <summary>旗升演出（§142 落成 3 秒）：t=0 旗在杆底，t=1 升到杆顶；打断时直接置 1 呈现完工态。</summary>
        public static void SetFlagRaised01(float t)
        {
            if (GrowthFlagCloth == null || growthFlagPole == null) return;
            // 杆高 52px/16 = 3.25 单位,旗面从杆底上方升到顶饰之下
            float y = Mathf.Lerp(-1.1f, 1.15f, Mathf.Clamp01(t));
            GrowthFlagCloth.localPosition = new Vector3(0.5f, y, 0f);
        }

        /// <summary>金融街:银行外立面亮化 overlay 的 α(夜幕驱动,落成前恒 0)。α>0 才激活节点。</summary>
        public static void ApplyBankFacadeLightsAlpha(float alpha)
        {
            if (bankFacadeLights == null) return;
            bool active = alpha > 0.005f;
            if (bankFacadeLights.gameObject.activeSelf != active) bankFacadeLights.gameObject.SetActive(active);
            if (!Mathf.Approximately(bankFacadeLights.color.a, alpha))
                bankFacadeLights.color = new Color(1f, 1f, 1f, alpha);
        }

        static void BuildGrowthDistricts(Transform root)
        {
            // V0.8.1 LayoutGuard 避让微调：两块交互牌原 y=8.5 与书店/银行碰撞体净距不足 0.4
            //（概念黑板与书店甚至重叠 0.33），下移至护栏达标位（交互点坐标自动跟随）
            ConceptBoardSpot = TownLayoutData.ParcelCenter("knowledge") + new Vector3(-3.2f, -4.2f);
            CivicSteleSpot = TownLayoutData.DecorPoint("plaza_stele");
            RateBoardSpot = TownLayoutData.ParcelCenter("centralbank") + new Vector3(-4f, -3.3f);
            appliedGrowthSeason = -1;

            // —— 东商业新区（完成第 5 章开工 / 第 10 章落成）：书店、花亭、两间空铺、长椅路灯 ——
            GrowthEastRoot = NewDistrictRoot(root, "growth_east");
            Transform eastDone = NewChild(GrowthEastRoot, "completed");
            BookstoreDoor = Building(eastDone, "bookstore", "东街书店", TownLayoutData.BookstoreCenter);
            Building(eastDone, "empty_a", "空置铺面", TownLayoutData.EmptyACenter);
            Building(eastDone, "empty_b", "空置铺面", TownLayoutData.EmptyBCenter);
            Prop(eastDone, "florist_kiosk", PixelArt.FloristKiosk(),
                TownLayoutData.ParcelCenter("knowledge") + new Vector3(0f, -4.2f), .9f);
            Prop(eastDone, "east_bench_a", PixelArt.ParkBench(),
                TownLayoutData.ParcelCenter("knowledge") + new Vector3(3.7f, -4.1f), 1f);
            Prop(eastDone, "east_bench_b", PixelArt.ParkBench(),
                TownLayoutData.ParcelCenter("empty_b") + new Vector3(2f, -3.2f), 1f);
            Prop(eastDone, "east_lamp_north", PixelArt.Lamp(),
                TownLayoutData.ParcelCenter("knowledge") + new Vector3(4.1f, -3.8f), 1.05f);
            Prop(eastDone, "east_lamp_south", PixelArt.Lamp(),
                TownLayoutData.ParcelCenter("empty_b") + new Vector3(-2.5f, -3.5f), 1.05f);
            GameObject board = Prop(eastDone, "concept_board", PixelArt.ConceptChalkboard(), ConceptBoardSpot, 1f);
            if (board != null)
            {
                BoxCollider2D boardCol = board.AddComponent<BoxCollider2D>();
                // V0.9.6e:碰撞体收成**落地脚印**(2.2×0.7,贴图基线那一带)。墙脚补齐后建筑碰撞体下沿
                // 压到了 8.08/8.56,原来 1.7 高的整块牌面碰撞体会插进央行与书店的墙里;
                // 牌子画在立面前是对的(排序在前),但牌子的"实体"只该占地面那一格。
                // 竖向空隙只有 2.21 格(便利店顶 5.87→央行底 8.08),整块牌面 1.8+两侧 0.4 放不下,只能收脚印。
                boardCol.size = new Vector2(2.2f, .7f);
                boardCol.offset = new Vector2(0f, -.5f);
                board.AddComponent<GrowthSignClick>().panel = "concept";
            }
            Transform eastSite = NewChild(GrowthEastRoot, "construction_site");
            BuildPlotSite(eastSite, TownLayoutData.BookstoreCenter, 4.6f, 4.2f,
                TownLayoutData.BookstoreCenter.x - 2.3f, TownLayoutData.BookstoreCenter.y - 1.6f,
                TownLayoutData.BookstoreCenter.x - 2.3f, TownLayoutData.BookstoreCenter.y + 1.6f);
            BuildPlotSite(eastSite, TownLayoutData.EmptyACenter, 3.6f, 3.4f,
                TownLayoutData.EmptyACenter.x - 1.9f, TownLayoutData.EmptyACenter.y - 1.3f,
                TownLayoutData.EmptyACenter.x - 1.9f, TownLayoutData.EmptyACenter.y + 1.3f);
            BuildPlotSite(eastSite, TownLayoutData.EmptyBCenter, 3.6f, 3.4f,
                TownLayoutData.EmptyBCenter.x - 1.9f, TownLayoutData.EmptyBCenter.y - 1.3f,
                TownLayoutData.EmptyBCenter.x - 1.9f, TownLayoutData.EmptyBCenter.y + 1.3f);
            Vector3 kiosk = TownLayoutData.ParcelCenter("knowledge") + new Vector3(0f, -4.2f);
            BuildPlotSite(eastSite, kiosk, 3.0f, 2.6f,
                kiosk.x - 1.9f, kiosk.y - 1f, kiosk.x - 1.9f, kiosk.y + 1f);
            MakeConstructionSign(eastSite, TownLayoutData.EmptyACenter + new Vector3(-.9f, -2f));

            // —— 行政广场（完成第 16 章开工 / 第 22 章落成）：旗杆、公告碑、绿化 ——
            GrowthPlazaRoot = NewDistrictRoot(root, "growth_plaza");
            Transform plazaDone = NewChild(GrowthPlazaRoot, "completed");
            Vector3 flagPosition = TownLayoutData.DecorPoint("plaza_flag");
            growthFlagPole = Prop(plazaDone, "plaza_flagpole", PixelArt.Flagpole(), flagPosition, 1f).transform;
            if (growthFlagPole != null)
            {
                SpriteRenderer cloth = NewChild(growthFlagPole, "flag_cloth").gameObject.AddComponent<SpriteRenderer>();
                cloth.sprite = PixelArt.FlagCloth();
                cloth.sortingOrder = MapBuilder.SortingForY(flagPosition.y) + 1;
                GrowthFlagCloth = cloth.transform;
                GrowthFlagCloth.localPosition = new Vector3(0.5f, 1.15f, 0f);
            }
            GameObject stele = Prop(plazaDone, "plaza_stele", PixelArt.Stele(), CivicSteleSpot, 1f);
            if (stele != null)
            {
                BoxCollider2D steleCol = stele.AddComponent<BoxCollider2D>();
                steleCol.size = new Vector2(1.6f, 2.6f);
                steleCol.offset = new Vector2(0f, .6f);
                stele.AddComponent<GrowthSignClick>().panel = "stele";
            }
            Transform plazaSite = NewChild(GrowthPlazaRoot, "construction_site");
            // 施工区只围合东北仪典设施的预留地，不再横切永久喷泉与市政中轴。
            Vector3 plazaSiteCenter = TownLayoutData.DecorPoint("plaza_site");
            CreateSiteSprite(plazaSite, "plaza_fence_south_w", PixelArt.ConstructionFence(),
                plazaSiteCenter + new Vector3(-.65f, -1f), .9f);
            CreateSiteSprite(plazaSite, "plaza_fence_south_e", PixelArt.ConstructionFence(),
                plazaSiteCenter + new Vector3(.65f, -1f), .9f);
            CreateSiteSprite(plazaSite, "plaza_fence_north", PixelArt.ConstructionFence(),
                plazaSiteCenter + new Vector3(0f, 1f), .9f);
            AddSiteCollider(plazaSite.gameObject, plazaSiteCenter, new Vector2(2.3f, 2.5f));
            MakeConstructionSign(plazaSite, plazaSiteCenter + new Vector3(.6f, -1.6f));

            // —— 金融街（完成第 36 章开工 / 第 42 章落成）：银行亮化、双街灯、央行前利率牌 ——
            GrowthFinancialRoot = NewDistrictRoot(root, "growth_financial");
            Transform finDone = NewChild(GrowthFinancialRoot, "completed");
            Vector3 bankCenter = TownLayoutData.BuildingCenter("bank");
            Prop(finDone, "fin_lamp_west", PixelArt.Lamp(), bankCenter + new Vector3(-1.7f, -2.6f), 1.05f);
            Prop(finDone, "fin_lamp_east", PixelArt.Lamp(), bankCenter + new Vector3(1.7f, -2.6f), 1.05f);
            GameObject rateBoard = Prop(finDone, "rate_board", PixelArt.RateBoard(), RateBoardSpot, 1f);
            if (rateBoard != null)
            {
                BoxCollider2D rateCol = rateBoard.AddComponent<BoxCollider2D>();
                rateCol.size = new Vector2(2.4f, .7f);          // V0.9.6e:同概念黑板,收成落地脚印(见上)
                rateCol.offset = new Vector2(0f, -.5f);
                rateBoard.AddComponent<GrowthSignClick>().panel = "rateboard";
            }
            Transform bank = root.Find("B_小镇银行_bank");
            if (bank != null)
            {
                SpriteRenderer lights = NewChild(bank, "bank_facade_lights").gameObject.AddComponent<SpriteRenderer>();
                lights.sprite = PixelArt.BankFacadeLights();
                lights.sortingOrder = bank.GetComponent<SpriteRenderer>().sortingOrder + 1;
                lights.color = new Color(1f, 1f, 1f, 0f);
                if (unlitSpriteMaterial == null)
                    unlitSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
                lights.sharedMaterial = unlitSpriteMaterial;   // 亮化不受 Light2D 压暗(窗光同款纪律)
                bankFacadeLights = lights;
            }
            Transform finSite = NewChild(GrowthFinancialRoot, "construction_site");
            Vector3 centralBank = TownLayoutData.BuildingCenter("centralbank");
            CreateSiteSprite(finSite, "fin_fence_west", PixelArt.ConstructionFence(), centralBank + new Vector3(-3.6f, -4.6f), 1f);
            CreateSiteSprite(finSite, "fin_fence_east", PixelArt.ConstructionFence(), centralBank + new Vector3(-1.2f, -4.6f), 1f);
            AddSiteCollider(finSite.gameObject, centralBank + new Vector3(-2.4f, -3.8f), new Vector2(2.6f, 2.6f));
            // V0.8.1:施工牌从 (17.4,10.3) 移到工地南侧空地——原位在央行(升 L 后)立面内,字会叠在墙上
            MakeConstructionSign(finSite, centralBank + new Vector3(-2.4f, -5.2f));

            // 初始可见性按当前存档章节推导(读档路径由 NotifyStateChanged 再次刷新)
            RefreshGrowthDistricts(GameState.I);
        }

        static Transform NewDistrictRoot(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform NewChild(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>单个地块的施工围挡：两段栅栏 + 地块碰撞体（不动既有道路与门面可达性）。</summary>
        static void BuildPlotSite(Transform site, Vector3 plot, float w, float h,
            float fence1X, float fence1Y, float fence2X, float fence2Y)
        {
            CreateSiteSprite(site, "plot_fence_" + (int)plot.y, PixelArt.ConstructionFence(),
                new Vector3(fence1X, fence1Y, 0f), 1f);
            CreateSiteSprite(site, "plot_fence2_" + (int)plot.y, PixelArt.ConstructionFence(),
                new Vector3(fence2X, fence2Y, 0f), 1f);
            AddSiteCollider(site.gameObject, plot, new Vector2(w, h));
        }

        static void CreateSiteSprite(Transform parent, string name, Sprite sprite, Vector3 pos, float scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingForY(pos.y);
        }

        static void AddSiteCollider(GameObject site, Vector3 center, Vector2 size)
        {
            BoxCollider2D col = site.AddComponent<BoxCollider2D>();
            col.offset = center;
            col.size = size;
        }

        /// <summary>「施工中」木牌文字（PortDistrictDirector 同款 TextMesh;V0.9.3 住房新开工地块传「新开工」）。</summary>
        static void MakeConstructionSign(Transform site, Vector3 pos, string word = "施工中")
        {
            GameObject go = new GameObject("construction_sign_text");
            go.transform.SetParent(site, false);
            go.transform.position = pos;
            TextMesh sign = go.AddComponent<TextMesh>();
            sign.text = word;
            sign.font = UiTheme.GameFont;
            sign.fontSize = 24;
            sign.characterSize = .24f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = new Color32(0x64, 0x35, 0x28, 255);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
            if (sign.font != null) renderer.sharedMaterial = sign.font.material;
            renderer.sortingOrder = 920;
        }

        /// <summary>新区季节性绿化（gd_ 前缀）：换季时随小镇树花一起换当季贴图。</summary>
        static void RebuildDistrictGreenery(Transform plazaRoot, int season)
        {
            if (plazaRoot == null) return;
            Transform done = plazaRoot.Find("completed");
            if (done == null) return;
            for (int i = done.childCount - 1; i >= 0; i--)
            {
                string n = done.GetChild(i).name;
                if (n.StartsWith("gd_")) DestroyObject(done.GetChild(i).gameObject);
            }
            Vector3 plaza = TownLayoutData.PlazaCenter;
            Prop(done, "gd_plaza_shrub_a", PixelArt.Shrub(season), plaza + new Vector3(-6f, 1.4f), .95f);
            Prop(done, "gd_plaza_shrub_b", PixelArt.Shrub(season), plaza + new Vector3(6f, 1.4f), .85f);
            Prop(done, "gd_plaza_flower_a", PixelArt.Flower(season, 0), plaza + new Vector3(-6f, -.9f), .9f);
            Prop(done, "gd_plaza_flower_b", PixelArt.Flower(season, 1), plaza + new Vector3(6f, -.9f), .9f);
            Prop(done, "gd_plaza_flower_c", PixelArt.Flower(season, 2), plaza + new Vector3(6.3f, -.2f), .9f);
        }

        static void MakeWall(Transform parent, Vector3 center, Vector2 size)
        {
            MakeNamedWall(parent, "wall", center, size);
        }

        static void MakeNamedWall(Transform parent, string name, Vector3 center, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        public static void MakeLabel(Transform parent, string text, Vector3 localOffset,
            float characterSize = 0.36f, int fontSize = 22)
        {
            GameObject go = new GameObject("label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UiTheme.GameFont;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color32(0x3a, 0x2d, 0x24, 235);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 900;
        }

        // Tile 是 ScriptableObject,不随场景销毁;不缓存的话每次重建地图都泄漏一批。
        static readonly System.Collections.Generic.Dictionary<Sprite, Tile> tileCache =
            new System.Collections.Generic.Dictionary<Sprite, Tile>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTileCache()
        {
            // 禁用 Domain Reload 时 tileCache 跨 Play 存活:键是 PixelArt.ResetCache 已销毁的 Sprite
            // (死条目永不回收),值是运行时创建的 Tile ScriptableObject(逐次累积泄漏)(P2-8)。
            foreach (Tile tile in tileCache.Values)
                if (tile != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(tile);
                    else UnityEngine.Object.DestroyImmediate(tile);
                }
            tileCache.Clear();
            publicProjectsRoot = null;
            townRoot = null;
            PortDistrictRoot = null;
            ShopInteriorRoot = null;
            interiorRooms.Clear();          // V0.9.11:子房间字典随根一起清,防关 Domain Reload 的死引用
            ActiveInteriorKind = "shop";    // 三旧锚点属性默认值复位(供下一场从果汁店语义起步)
            GrowthEastRoot = null;
            GrowthPlazaRoot = null;
            GrowthFinancialRoot = null;
            GrowthFlagCloth = null;
            growthFlagPole = null;
            bankFacadeLights = null;
            appliedGrowthSeason = -1;
            groundTilemap = null;
            grassTileVariant.Clear();
            appliedSeason = -1;
            facades.Clear();
            priceBoards.Clear();
            overheatClutter = null;
        }

        static Tile MakeTile(Sprite sprite)
        {
            if (tileCache.TryGetValue(sprite, out Tile cached) && cached != null) return cached;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tileCache[sprite] = tile;
            return tile;
        }

        static void PaintPath(Tilemap tilemap, Tile[] tiles, System.Random rng, int x0, int y0, int x1, int y1)
        {
            int minX = Mathf.Min(x0, x1), maxX = Mathf.Max(x0, x1);
            int minY = Mathf.Min(y0, y1), maxY = Mathf.Max(y0, y1);
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles[rng.Next(tiles.Length)]);
        }

        static void PaintHorizontalStreet(Tilemap tilemap, Tile[] roads, Tile[] walks, System.Random rng,
            int minX, int maxX, int upperRoadY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                tilemap.SetTile(new Vector3Int(x, upperRoadY, 0), roads[rng.Next(roads.Length)]);
                tilemap.SetTile(new Vector3Int(x, upperRoadY - 1, 0), roads[rng.Next(roads.Length)]);
                tilemap.SetTile(new Vector3Int(x, upperRoadY + 1, 0), walks[rng.Next(walks.Length)]);
                tilemap.SetTile(new Vector3Int(x, upperRoadY - 2, 0), walks[rng.Next(walks.Length)]);
            }
        }

        static void PaintVerticalStreet(Tilemap tilemap, Tile[] roads, Tile[] walks, System.Random rng,
            int leftRoadX, int minY, int maxY, bool leaveMainIntersectionOpen)
        {
            for (int y = minY; y <= maxY; y++)
            {
                tilemap.SetTile(new Vector3Int(leftRoadX, y, 0), roads[rng.Next(roads.Length)]);
                tilemap.SetTile(new Vector3Int(leftRoadX + 1, y, 0), roads[rng.Next(roads.Length)]);
                if (leaveMainIntersectionOpen && y >= -1 && y <= 1) continue;
                tilemap.SetTile(new Vector3Int(leftRoadX - 1, y, 0), walks[rng.Next(walks.Length)]);
                tilemap.SetTile(new Vector3Int(leftRoadX + 2, y, 0), walks[rng.Next(walks.Length)]);
            }
        }

        public static int SortingForY(float y)
        {
            return 200 - Mathf.RoundToInt(y * 8f);
        }

        /// <summary>1×1 白块(边墙等)。</summary>
        public static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            whiteTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            whiteTex.filterMode = FilterMode.Point;
            Color32[] px = new Color32[64];
            for (int i = 0; i < 64; i++) px[i] = new Color32(255, 255, 255, 255);
            whiteTex.SetPixels32(px); whiteTex.Apply();
            whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return whiteSprite;
        }
    }

    /// <summary>建筑身份独立于交互能力；纯场景建筑仍需参与四季贴图与布局校验。</summary>
    public class BuildingIdentity : MonoBehaviour
    {
        public string kind;
        public bool interactive;
    }

    /// <summary>点击建筑触发交互(等价于走到门口按 E)。</summary>
    public class BuildingClick : MonoBehaviour
    {
        public string kind;
        void OnMouseDown()
        {
            if (GameBootstrap.I != null) GameBootstrap.I.OnBuildingClicked(kind);
        }
    }

    public class CustomsBoardClick : MonoBehaviour
    {
        void OnMouseDown()
        {
            if (PortSceneController.IsInsidePort && UIManager.I != null && !UIManager.I.InputBlocked)
                UIManager.I.ShowCustomsBoard();
        }
    }

    public class PortExitClick : MonoBehaviour
    {
        void OnMouseDown()
        {
            if (PortSceneController.IsInsidePort && UIManager.I != null && !UIManager.I.InputBlocked)
                PortSceneController.ExitPort();
        }
    }

    /// <summary>V0.9.7 店内门垫:点击出店(与门垫按 E 等价)。</summary>
    public class ShopInteriorExitClick : MonoBehaviour
    {
        void OnMouseDown()
        {
            if (ShopInteriorController.IsInsideShopInterior && UIManager.I != null && !UIManager.I.InputBlocked)
                ShopInteriorController.ExitShopInterior();
        }
    }

    /// <summary>V0.7.3 新区只读交互牌(本周概念/公告碑/利率牌)的点击入口,与按 E 等价。</summary>
    public class GrowthSignClick : MonoBehaviour
    {
        public string panel;   // concept / stele / rateboard
        void OnMouseDown()
        {
            if (!PortSceneController.IsInsidePort && UIManager.I != null && !UIManager.I.InputBlocked)
                UIManager.I.ShowGrowthPanel(panel);
        }
    }
}
