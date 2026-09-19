using System;
using UnityEngine;

namespace EconTown
{
    /// <summary>主城四区。仅用于布局与结构测试，不进入存档或经济模拟。</summary>
    internal enum TownDistrict
    {
        LivingEducation,
        CivicCulture,
        FinancialKnowledge,
        TradeIndustry
    }

    internal enum TownBoundaryStyle
    {
        None,
        Iron,
        Hedge,
        Chain,
        Picket
    }

    internal enum TownFrontage
    {
        None,
        North,
        South
    }

    /// <summary>闭区间 Tilemap 矩形，用于道路、步行巷、院内短道与地块分界带。</summary>
    internal readonly struct TownPathRect
    {
        public readonly int x0, y0, x1, y1;

        public TownPathRect(int x0, int y0, int x1, int y1)
        {
            this.x0 = x0;
            this.y0 = y0;
            this.x1 = x1;
            this.y1 = y1;
        }
    }

    /// <summary>法定地块。有围栏的地块必须四面闭合，只允许在声明的院门处断开。</summary>
    internal readonly struct TownParcel
    {
        public readonly string id;
        public readonly TownDistrict district;
        public readonly Rect bounds;
        public readonly TownFrontage frontage;
        public readonly float setback;
        public readonly TownBoundaryStyle boundary;
        public readonly bool gateOnNorth;
        public readonly float gateCenterX;
        public readonly float gateWidth;

        public TownParcel(string id, TownDistrict district, Rect bounds,
            TownBoundaryStyle boundary = TownBoundaryStyle.None,
            float gateCenterX = 0f, float gateWidth = 0f, bool gateOnNorth = false,
            TownFrontage frontage = TownFrontage.None, float setback = .25f)
        {
            this.id = id;
            this.district = district;
            this.bounds = bounds;
            this.frontage = frontage;
            this.setback = setback;
            this.boundary = boundary;
            this.gateCenterX = gateCenterX;
            this.gateWidth = gateWidth;
            this.gateOnNorth = gateOnNorth;
        }

        public bool HasFence { get { return boundary != TownBoundaryStyle.None; } }
    }

    /// <summary>建筑槽位。沿街建筑按门口目标线反算中心，不再手工微调各自的 y。</summary>
    internal readonly struct TownBuildingSlot
    {
        public readonly string kind;
        public readonly string name;
        public readonly string parcelId;
        public readonly Vector3 center;
        public readonly bool usesDoorLine;
        public readonly float doorTargetY;
        public readonly bool inTownPlacements;
        public readonly bool interactive;

        public TownBuildingSlot(string kind, string name, string parcelId, Vector3 center,
            bool inTownPlacements, bool interactive = true)
        {
            this.kind = kind;
            this.name = name;
            this.parcelId = parcelId;
            this.center = center;
            usesDoorLine = false;
            doorTargetY = float.NaN;
            this.inTownPlacements = inTownPlacements;
            this.interactive = interactive;
        }

        public TownBuildingSlot(string kind, string name, string parcelId,
            float frontageX, float doorTargetY, bool inTownPlacements, bool interactive = true)
        {
            this.kind = kind;
            this.name = name;
            this.parcelId = parcelId;
            center = new Vector3(frontageX, doorTargetY - BuildingSpecs.Get(kind).doorOffset.y);
            usesDoorLine = true;
            this.doorTargetY = doorTargetY;
            this.inTownPlacements = inTownPlacements;
            this.interactive = interactive;
        }
    }

    /// <summary>地块内的室外道具槽位；坐标始终相对地块中心，避免建筑迁移后遗留散点。</summary>
    internal readonly struct TownDecorSlot
    {
        public readonly string id;
        public readonly string parcelId;
        public readonly Vector3 offset;

        public TownDecorSlot(string id, string parcelId, Vector3 offset)
        {
            this.id = id;
            this.parcelId = parcelId;
            this.offset = offset;
        }
    }

    /// <summary>“先划地块、再放建筑”的主城唯一布局数据源。</summary>
    internal static class TownLayoutData
    {
        public const int MainStreetY = 0;
        public const int TradeStreetY = -12;
        public const int CivicAxisLeftX = -1;
        public const int CivicAxisMinY = -11;
        public const int CivicAxisMaxY = 1;
        public const float MainFrontDoorY = 1.25f;
        public const float TradeFrontDoorY = -10.75f;

        public static readonly Rect PlazaBounds = new Rect(-6f, 2f, 11f, 5f);
        public static readonly Vector3 PlazaCenter = new Vector3(-.5f, 4.5f);
        public static readonly Vector3 PocketParkCenter = new Vector3(-25f, -17.5f);
        public static readonly Vector3 PublicParkCenter = PocketParkCenter;
        public static readonly Vector3 BusStopCenter = new Vector3(6.8f, -2.8f);

        public static readonly TownParcel[] Parcels =
        {
            // 北区只保留职业培训中心，避免两栋大体量校舍挤在同一庭院。
            new TownParcel("training", TownDistrict.LivingEducation, new Rect(-35f, 8.5f, 12f, 10.5f),
                TownBoundaryStyle.Hedge, -29f, 3.5f, false, TownFrontage.South, .3f),
            new TownParcel("home", TownDistrict.LivingEducation, new Rect(-19.5f, 9f, 7f, 9f),
                TownBoundaryStyle.Picket, -16f, 2.5f, false, TownFrontage.South, 1.4f),
            new TownParcel("convenience", TownDistrict.LivingEducation, new Rect(-35f, 1.5f, 6.4f, 6.5f), frontage: TownFrontage.South),
            new TownParcel("snack", TownDistrict.LivingEducation, new Rect(-28f, 1.5f, 5.6f, 6.5f), frontage: TownFrontage.South),
            new TownParcel("restaurant", TownDistrict.LivingEducation, new Rect(-21.8f, 1.5f, 9.8f, 6.5f), frontage: TownFrontage.South),

            new TownParcel("hall", TownDistrict.CivicCulture, new Rect(-8f, 8f, 15f, 12.8f),
                TownBoundaryStyle.Iron, -.5f, 5f, false, TownFrontage.South, 1.2f),
            new TownParcel("plaza", TownDistrict.CivicCulture, PlazaBounds),

            new TownParcel("cinema", TownDistrict.CivicCulture, new Rect(8f, 1.5f, 8f, 6.5f), frontage: TownFrontage.South, setback: 0f),
            new TownParcel("bank", TownDistrict.FinancialKnowledge, new Rect(16.5f, 1.5f, 7.8f, 6.5f), frontage: TownFrontage.South, setback: 0f),
            new TownParcel("flower", TownDistrict.FinancialKnowledge, new Rect(29f, 1.5f, 6f, 6.5f), frontage: TownFrontage.South),
            new TownParcel("knowledge", TownDistrict.FinancialKnowledge, new Rect(9f, 9f, 9.2f, 10f),
                frontage: TownFrontage.South),
            new TownParcel("centralbank", TownDistrict.FinancialKnowledge, new Rect(19f, 8f, 15f, 11.5f),
                TownBoundaryStyle.Iron, 26f, 4f, false, TownFrontage.South, 1.2f),

            new TownParcel("shop", TownDistrict.TradeIndustry, new Rect(-35f, -10.5f, 6f, 8f), frontage: TownFrontage.South),
            new TownParcel("competitor", TownDistrict.TradeIndustry, new Rect(-28.5f, -10.5f, 6f, 8f), frontage: TownFrontage.South),
            new TownParcel("market", TownDistrict.TradeIndustry, new Rect(-22f, -10.5f, 14f, 8f), frontage: TownFrontage.South),
            new TownParcel("factory", TownDistrict.TradeIndustry, new Rect(4f, -10.5f, 12f, 8f),
                TownBoundaryStyle.Chain, 10f, 4f, false, TownFrontage.South, .4f),
            // 东南端按“过渡商铺→货运堆场→港口终端”收束；港口法定地块直接贴住地图东界。
            new TownParcel("empty_b", TownDistrict.TradeIndustry, new Rect(17.5f, -10.5f, 5f, 8f), frontage: TownFrontage.South, setback: .5f),
            new TownParcel("port", TownDistrict.TradeIndustry, new Rect(23.5f, -10.5f, 11.5f, 8f), frontage: TownFrontage.South, setback: 1.2f),
            new TownParcel("housing_growth", TownDistrict.LivingEducation, new Rect(-19f, -20.5f, 8f, 5f)),

            // 社区学校单独落在南部公共空间，与口袋公园、住宅成长区共同组成生活服务带。
            new TownParcel("school", TownDistrict.LivingEducation, new Rect(-10.5f, -20.75f, 9f, 7.75f),
                TownBoundaryStyle.Hedge, -3.5f, 3f, true, TownFrontage.North, .4f),

            new TownParcel("park", TownDistrict.LivingEducation, new Rect(-30f, -20f, 10f, 5f),
                TownBoundaryStyle.Hedge, -25f, 2.5f, true, TownFrontage.North, 1f)
        };

        public static readonly TownBuildingSlot[] BuildingSlots =
        {
            new TownBuildingSlot("shop", "我的果汁店", "shop", -32f, TradeFrontDoorY, true),
            new TownBuildingSlot("competitor", "SUNNY TEA", "competitor", -25.5f, TradeFrontDoorY, true),
            new TownBuildingSlot("market", "原料市场", "market", -17.5f, TradeFrontDoorY, true),
            new TownBuildingSlot("factory", "河畔工厂", "factory", new Vector3(10f, -6.5f), true),
            new TownBuildingSlot("port", "国际贸易港", "port", 31f, -9.35f, true),

            // XL 画布比旧 L 档增高 8px：中心上移 .32，门点仍保持 y=11.18。
            new TownBuildingSlot("hall", "市政厅", "hall", new Vector3(-.5f, 15.52f), true),
            new TownBuildingSlot("training", "职业培训中心", "training", new Vector3(-29f, 13.5f), true),
            // 校舍向西让出东侧校内通道；北门面向小镇，再沿 L 形前庭到达校舍正门。
            new TownBuildingSlot("school", "社区学校", "school", new Vector3(-6.75f, -16.29f), true, false),
            new TownBuildingSlot("convenience", "便利屋", "convenience", -32f, MainFrontDoorY, true),
            new TownBuildingSlot("home", "我的家", "home", new Vector3(-16f, 14.1f), true),
            new TownBuildingSlot("restaurant", "暖炉餐厅", "restaurant", -17f, MainFrontDoorY, true),
            new TownBuildingSlot("cinema", "星光电影院", "cinema", 12f, MainFrontDoorY, true),
            new TownBuildingSlot("bank", "小镇银行", "bank", 20.5f, MainFrontDoorY, true),
            new TownBuildingSlot("centralbank", "中央银行", "centralbank", new Vector3(26f, 14.7f), true),

            new TownBuildingSlot("decor_a", "临街小铺", "snack", -25f, MainFrontDoorY, false),
            new TownBuildingSlot("decor_b", "临街小铺", "flower", 32f, MainFrontDoorY, false),
            new TownBuildingSlot("bookstore", "东街书店", "knowledge", 11.6f, 11.5f, false),
            new TownBuildingSlot("empty_a", "空置铺面", "knowledge", 16f, 11.5f, false),
            new TownBuildingSlot("empty_b", "空置铺面", "empty_b", 20f, TradeFrontDoorY, false)
        };

        public static readonly MapBuilder.BuildingPlacement[] TownPlacements = BuildTownPlacements();

        public static readonly Vector3 DecorACenter = BuildingCenter("decor_a");
        public static readonly Vector3 DecorBCenter = BuildingCenter("decor_b");
        public static readonly Vector3 BookstoreCenter = BuildingCenter("bookstore");
        public static readonly Vector3 EmptyACenter = BuildingCenter("empty_a");
        public static readonly Vector3 EmptyBCenter = BuildingCenter("empty_b");

        public static readonly TownPathRect[] PedestrianPaths =
        {
            new TownPathRect(-6, 2, 5, 6),
            new TownPathRect(-35, 8, -11, 9),
            new TownPathRect(-11, 1, -10, 9),
            new TownPathRect(8, 8, 34, 9),
            new TownPathRect(25, 1, 27, 9),

            new TownPathRect(-31, 8, -27, 10),
            new TownPathRect(-17, 8, -15, 12),
            new TownPathRect(-3, 7, 2, 12),
            new TownPathRect(9, 11, 18, 11),
            new TownPathRect(11, 9, 12, 10),
            new TownPathRect(15, 9, 16, 10),
            new TownPathRect(25, 8, 27, 11),
            new TownPathRect(8, -11, 11, -8),
            new TownPathRect(30, -11, 32, -9),
            new TownPathRect(-8, -20, -3, -19),
            new TownPathRect(-4, -19, -3, -14),
            new TownPathRect(-26, -17, -24, -14),
            new TownPathRect(-28, -18, -23, -17)
        };

        public static readonly TownPathRect[] ParcelBoundaryStrips =
        {
            new TownPathRect(-29, 2, -29, 3),
            new TownPathRect(-22, 2, -22, 3),
            new TownPathRect(16, 2, 16, 3),
            new TownPathRect(24, 2, 24, 3),
            new TownPathRect(28, 2, 28, 3),
            new TownPathRect(-29, -10, -29, -9),
            new TownPathRect(-22, -10, -22, -9),
            new TownPathRect(-8, -10, -8, -9),
            new TownPathRect(17, -10, 17, -9),
            new TownPathRect(23, -10, 23, -9)
        };

        public static readonly TownDecorSlot[] DecorSlots =
        {
            new TownDecorSlot("plaza_notice", "plaza", new Vector3(-4.3f, -1.2f)),
            new TownDecorSlot("plaza_bench_w", "plaza", new Vector3(-3.9f, .8f)),
            new TownDecorSlot("plaza_bench_se", "plaza", new Vector3(3.7f, -1.85f)),
            new TownDecorSlot("plaza_flag", "plaza", new Vector3(2.6f, 2.1f)),
            new TownDecorSlot("plaza_stele", "plaza", new Vector3(4.1f, .8f)),
            new TownDecorSlot("plaza_site", "plaza", new Vector3(4.3f, 1.25f)),
            new TownDecorSlot("hall_planter_w", "hall", new Vector3(-4.8f, -4.3f)),
            new TownDecorSlot("hall_planter_e", "hall", new Vector3(4.8f, -4.3f)),
            new TownDecorSlot("centralbank_lamp_w", "centralbank", new Vector3(-4.5f, -4f)),
            new TownDecorSlot("centralbank_lamp_e", "centralbank", new Vector3(4.5f, -4f)),
            new TownDecorSlot("centralbank_planter_w", "centralbank", new Vector3(-6f, -3.3f)),
            new TownDecorSlot("centralbank_planter_e", "centralbank", new Vector3(6f, -3.3f)),
            new TownDecorSlot("training_sign", "training", new Vector3(-4.5f, -3.8f)),
            new TownDecorSlot("training_bench", "training", new Vector3(4f, -3.8f)),
            new TownDecorSlot("school_notice", "school", new Vector3(-3.8f, -2.3f)),
            new TownDecorSlot("factory_chimney", "factory", new Vector3(-4.5f, 4.1f)),
            new TownDecorSlot("factory_pallets", "factory", new Vector3(4.3f, 1.3f)),
            new TownDecorSlot("factory_drums", "factory", new Vector3(4.8f, 0f)),
            // 港口主体贴东界，货物集中在西侧装卸坪，避免货箱顶到地图墙。
            new TownDecorSlot("port_pallets", "port", new Vector3(-3.6f, 1.1f)),
            new TownDecorSlot("port_crates", "port", new Vector3(-1.8f, .4f)),
            new TownDecorSlot("park_gazebo", "park", new Vector3(-2f, -.2f)),
            new TownDecorSlot("park_bin", "park", new Vector3(3.5f, 1f)),
            new TownDecorSlot("festival_plaza_w", "plaza", new Vector3(-2.7f, -2.9f)),
            new TownDecorSlot("festival_plaza_e", "plaza", new Vector3(4.3f, -2.9f)),
            new TownDecorSlot("festival_hall_w", "hall", new Vector3(-4.1f, -5.6f)),
            new TownDecorSlot("festival_hall_e", "hall", new Vector3(5.5f, -5.6f)),
            new TownDecorSlot("shop_overheat_clutter", "shop", new Vector3(-.8f, -3.55f)),
            new TownDecorSlot("south_meadow_factory", "factory", new Vector3(-4f, -9.5f)),
            new TownDecorSlot("south_meadow_port", "port", new Vector3(-4.5f, -9.5f)),
            new TownDecorSlot("south_meadow_empty", "empty_b", new Vector3(-2f, -9.5f)),
            new TownDecorSlot("finance_east_meadow", "centralbank", new Vector3(8.5f, -3.75f)),
            new TownDecorSlot("ceremony_east", "centralbank", new Vector3(-.5f, -4.35f)),
            new TownDecorSlot("ceremony_plaza", "plaza", new Vector3(3.8f, 1.5f)),
            new TownDecorSlot("ceremony_financial", "bank", new Vector3(.1f, -1.35f))
        };

        /// <summary>主城 NPC 的公共活动锚点。点位只落在门前步行面、院门外或广场，
        /// 避免扩图后仍在旧地图中心随机游荡。</summary>
        public static readonly Vector3[] InitialNpcSpawns =
        {
            DoorPoint("convenience") + new Vector3(0f, -.65f),
            DoorPoint("decor_a") + new Vector3(0f, -.65f),
            DoorPoint("restaurant") + new Vector3(0f, -.65f),
            GateApproach("training", .35f),
            DoorPoint("cinema") + new Vector3(0f, -.65f),
            DoorPoint("bank") + new Vector3(0f, -.65f),
            GateApproach("centralbank", .35f),
            GateApproach("hall", .35f),
            PlazaCenter + new Vector3(-3.5f, -.6f),
            GateApproach("park", .35f),
            GateApproach("factory", .35f),
            GateApproach("home", .35f)
        };

        /// <summary>漫步者的公共空间目标点；覆盖两条主街、市政轴、广场和各院门，
        /// 不把建筑后场或未定义草地当作随机目标。</summary>
        public static readonly Vector3[] StreetActivityPoints =
        {
            DoorPoint("convenience") + new Vector3(0f, -.65f),
            DoorPoint("decor_a") + new Vector3(0f, -.65f),
            DoorPoint("restaurant") + new Vector3(0f, -.65f),
            PlazaCenter + new Vector3(0f, -4.8f),
            DoorPoint("cinema") + new Vector3(0f, -.65f),
            DoorPoint("bank") + new Vector3(0f, -.65f),
            DoorPoint("decor_b") + new Vector3(0f, -.65f),
            GateApproach("training", .35f),
            GateApproach("home", .35f),
            GateApproach("hall", .35f),
            PlazaCenter,
            DoorPoint("bookstore") + new Vector3(0f, -.65f),
            GateApproach("centralbank", .35f),
            DoorPoint("shop") + new Vector3(0f, -.65f),
            DoorPoint("competitor") + new Vector3(0f, -.65f),
            DoorPoint("market") + new Vector3(0f, -.65f),
            GateApproach("factory", .35f),
            DoorPoint("port") + new Vector3(0f, -1.4f),
            DoorPoint("empty_b") + new Vector3(0f, -.65f),
            GateApproach("park", .35f)
        };

        static MapBuilder.BuildingPlacement[] BuildTownPlacements()
        {
            int count = 0;
            for (int i = 0; i < BuildingSlots.Length; i++)
                if (BuildingSlots[i].inTownPlacements) count++;
            var result = new MapBuilder.BuildingPlacement[count];
            int at = 0;
            for (int i = 0; i < BuildingSlots.Length; i++)
            {
                TownBuildingSlot slot = BuildingSlots[i];
                if (!slot.inTownPlacements) continue;
                result[at++] = new MapBuilder.BuildingPlacement
                {
                    kind = slot.kind,
                    name = slot.name,
                    center = slot.center,
                    interactive = slot.interactive
                };
            }
            return result;
        }

        public static TownParcel Parcel(string id)
        {
            for (int i = 0; i < Parcels.Length; i++)
                if (Parcels[i].id == id) return Parcels[i];
            throw new ArgumentOutOfRangeException(nameof(id), id, "未登记的主城地块。");
        }

        public static string ParcelOf(string kind)
        {
            for (int i = 0; i < BuildingSlots.Length; i++)
                if (BuildingSlots[i].kind == kind) return BuildingSlots[i].parcelId;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "未登记的建筑地块。");
        }

        public static Vector3 ParcelCenter(string id)
        {
            Rect r = Parcel(id).bounds;
            return new Vector3(r.center.x, r.center.y);
        }

        public static Vector3 DecorPoint(string id)
        {
            for (int i = 0; i < DecorSlots.Length; i++)
            {
                TownDecorSlot slot = DecorSlots[i];
                if (slot.id == id) return ParcelCenter(slot.parcelId) + slot.offset;
            }
            throw new ArgumentOutOfRangeException(nameof(id), id, "未登记的室外道具槽位。");
        }

        public static TownDistrict DistrictOf(string kind)
        {
            return Parcel(ParcelOf(kind)).district;
        }

        public static Vector3 BuildingCenter(string kind)
        {
            for (int i = 0; i < BuildingSlots.Length; i++)
                if (BuildingSlots[i].kind == kind) return BuildingSlots[i].center;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "未登记的主城建筑坐标。");
        }

        public static Vector3 DoorPoint(string kind)
        {
            return BuildingCenter(kind) + BuildingSpecs.Get(kind).doorOffset;
        }

        public static Vector3 GateApproach(string parcelId, float outwardDistance)
        {
            TownParcel parcel = Parcel(parcelId);
            float y = parcel.gateOnNorth
                ? parcel.bounds.yMax + outwardDistance
                : parcel.bounds.yMin - outwardDistance;
            return new Vector3(parcel.gateCenterX, y);
        }
    }
}
