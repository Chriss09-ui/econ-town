using UnityEngine;

namespace EconTown
{
    /// <summary>店内房间主题档(V0.9.11):tile 四件套的调色板序号,与 PixelArt.InteriorXxxTile(theme,…) 对应。
    /// 0=果汁店原样(键与像素不变);1=暖炉餐厅;2=星光电影院;3=原料市场。
    /// V0.9.12:4=SUNNY TEA 茶室;5=我的家;6=便利屋;7=东街书店;8=花店;9=零嘴亭。
    /// V0.9.13:10=小镇银行;11=中央银行;12=市政厅;13=职业培训中心;14=河畔工厂。</summary>
    public enum InteriorTheme { Shop = 0, Restaurant = 1, Cinema = 2, Market = 3, Tea = 4, Home = 5, Convenience = 6, Bookstore = 7, Flower = 8, Snack = 9, Bank = 10, CentralBank = 11, CityHall = 12, Classroom = 13, Factory = 14 }

    /// <summary>
    /// V0.9.11 per-kind 店内登记表:几何/主题/文案/锚点(局部坐标),纯数据、零场景装配(照 BuildingSpec 表驱动先例)。
    /// 消费方:MapBuilder(装配房间+家具)、ShopInteriorController(换场)、GameBootstrap(店内交互注册)。
    /// 家具与 NPC 布局不在此(由 MapBuilder 的 DecorateInterior&lt;Kind&gt; 实现);
    /// 交互回调不在此(由 GameBootstrap 的 InteriorCounterUse 实现——表只放数据,行为留接线层)。
    /// 各房间在世界坐标网格错开(+40x 递进):店内交互点共用 WorldArea.ShopInterior 而坐标天然不冲突
    /// (E 键 1.7 半径扫不到邻店),Prop/围墙/NPC 全世界坐标、Tilemap 局部格子——换算集中在派生属性。
    /// </summary>
    public class InteriorSpec
    {
        public string kind;                 // 与 BuildingClick.kind 一致("shop"/"restaurant"/"cinema"/"market")
        public int width, height;           // 房间格尺寸(shop 16×11 原样;新店 18×12)
        public Vector3 origin;              // 世界原点(shop (0,0) 保持不动=V097 锚点像素级兼容)
        public InteriorTheme theme;
        public Color32 backgroundColor;     // 相机底色(房间比窗口窄,四周底色读作「盒子视图」暗框)
        public string enterToast, exitToast;
        public int[] windowColumns;         // 上墙窗位列(shop {3,8,13};18 宽 {4,9,14})
        public Vector3 entryLocal;          // 进店落点(门垫内侧)
        public Vector3 exitLocal;           // 门垫交互点
        public string exitLabel;            // 门垫按 E 提示
        public Vector3 counterLocal;        // 原功能面板交互点(站位)
        public string counterLabel;         // 面板交互点按 E 提示
        public System.Func<Vector3> door;   // 小镇侧门点(出店返回兜底),闭包惰性读 MapBuilder 静态属性

        /// <summary>进店落点(世界)。</summary>
        public Vector3 EntryWorld { get { return origin + entryLocal; } }

        /// <summary>门垫交互点(世界)。</summary>
        public Vector3 ExitWorld { get { return origin + exitLocal; } }

        /// <summary>面板交互点(世界)。</summary>
        public Vector3 CounterWorld { get { return origin + counterLocal; } }

        /// <summary>房间矩形(世界,minX/minY/maxX/maxY)=相机 clamp 域。</summary>
        public Vector4 RoomBounds
        {
            get { return new Vector4(origin.x, origin.y, origin.x + width, origin.y + height); }
        }

        /// <summary>店内常驻 NPC 漫游矩形(世界):贴墙留 0.7、顶到墙带底(H-3)再让 0.8——
        /// shop (0.7,0.6,15.3,7.2) 与 V0.9.7 手写值一致。</summary>
        public Vector4 NpcBounds
        {
            get { return new Vector4(origin.x + .7f, origin.y + .6f, origin.x + width - .7f, origin.y + height - 3f - .8f); }
        }
    }

    /// <summary>V0.9.11 店内目录:打样批四间(果汁店+餐厅/影院/市场);后续批次在此追加行。</summary>
    public static class InteriorCatalog
    {
        public static readonly InteriorSpec[] All =
        {
            New("shop", 16, 11, Vector3.zero, InteriorTheme.Shop, new Color32(0x2E, 0x22, 0x1A, 255),
                new int[] { 3, 8, 13 },
                new Vector3(7.8f, 1.8f), new Vector3(7.8f, .75f), new Vector3(11.2f, 5.55f),
                "收银台 · 营业", "走出果汁店",
                "欢迎光临我的果汁店 · 收银台按 E 营业,门垫按 E 出店", "走出了果汁店"),
            New("restaurant", 18, 12, new Vector3(40f, 0f, 0f), InteriorTheme.Restaurant, new Color32(0x36, 0x20, 0x19, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(11f, 5.55f),
                "点餐台 · 消费", "走出暖炉餐厅",
                "欢迎光临暖炉餐厅 · 点餐台按 E 消费,门垫按 E 出店", "走出了暖炉餐厅"),
            New("cinema", 18, 12, new Vector3(80f, 0f, 0f), InteriorTheme.Cinema, new Color32(0x24, 0x1A, 0x2E, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(16.2f, 5.55f),
                "售票亭 · 购票", "走出星光电影院",
                "欢迎光临星光电影院 · 售票亭按 E 购票,门垫按 E 出店", "走出了星光电影院"),
            New("market", 18, 12, new Vector3(120f, 0f, 0f), InteriorTheme.Market, new Color32(0x2A, 0x21, 0x18, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(9f, 4.7f),
                "采购台 · 进货", "走出原料市场",
                "走进了原料市场 · 采购台按 E 进货,门垫按 E 出店", "走出了原料市场"),
            // ————— V0.9.12 第二批:生活与商业六间(打样批 +40x 递进续排) —————
            New("competitor", 18, 12, new Vector3(160f, 0f, 0f), InteriorTheme.Tea, new Color32(0x2A, 0x30, 0x22, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(11f, 4.7f),
                "茶台 · 经营情报", "走出 SUNNY TEA",
                "走进 SUNNY TEA · 茶台按 E 查看经营情报,门垫按 E 出店", "走出了 SUNNY TEA"),
            New("home", 18, 12, new Vector3(200f, 0f, 0f), InteriorTheme.Home, new Color32(0x32, 0x28, 0x1E, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(5f, 4.7f),
                "书桌 · 今日账本", "走出家门",
                "回到我的家 · 书桌按 E 翻看今日账本,门垫按 E 出门", "走出了家门"),
            New("convenience", 18, 12, new Vector3(240f, 0f, 0f), InteriorTheme.Convenience, new Color32(0x22, 0x28, 0x2E, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(13.5f, 4.7f),
                "收银台 · 购物", "走出便利屋",
                "走进便利屋 · 收银台按 E 选购日用品,门垫按 E 出店", "走出了便利屋"),
            New("bookstore", 18, 12, new Vector3(280f, 0f, 0f), InteriorTheme.Bookstore, new Color32(0x2A, 0x20, 0x16, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(15f, 4.7f),
                "书台 · 翻看", "走出东街书店",
                "走进东街书店 · 书台按 E 翻看,门垫按 E 出店", "走出了东街书店"),
            New("decor_b", 18, 12, new Vector3(320f, 0f, 0f), InteriorTheme.Flower, new Color32(0x30, 0x24, 0x26, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(14f, 4.7f),
                "花台 · 挑花", "走出花店",
                "走进花店 · 花台按 E 挑一束花,门垫按 E 出店", "走出了花店"),
            New("decor_a", 14, 10, new Vector3(360f, 0f, 0f), InteriorTheme.Snack, new Color32(0x32, 0x28, 0x1A, 255),
                new int[] { 3, 7, 11 },
                new Vector3(7f, 1.8f), new Vector3(7f, .75f), new Vector3(9.5f, 4.6f),
                "小卖窗 · 买零嘴", "走出零嘴亭",
                "走进零嘴亭 · 小卖窗按 E 买零嘴,门垫按 E 出店", "走出了零嘴亭"),
            // ————— V0.9.13 第三批:机构与产业五间(+40x 递进续排) —————
            New("bank", 18, 12, new Vector3(400f, 0f, 0f), InteriorTheme.Bank, new Color32(0x1E, 0x26, 0x2E, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(11f, 4.7f),
                "柜台 · 存取业务", "走出小镇银行",
                "走进小镇银行 · 柜台按 E 办理存取,门垫按 E 出店", "走出了小镇银行"),
            New("centralbank", 18, 12, new Vector3(440f, 0f, 0f), InteriorTheme.CentralBank, new Color32(0x26, 0x22, 0x18, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(11f, 4.7f),
                "利率台 · 央行政策", "走出中央银行",
                "走进中央银行 · 利率台按 E 查看央行政策,门垫按 E 出店", "走出了中央银行"),
            New("hall", 18, 12, new Vector3(480f, 0f, 0f), InteriorTheme.CityHall, new Color32(0x24, 0x1A, 0x18, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(9f, 4.7f),
                "政务台 · 财政总览", "走出市政厅",
                "走进市政厅 · 政务台按 E 查看财政总览,门垫按 E 出店", "走出了市政厅"),
            New("training", 18, 12, new Vector3(520f, 0f, 0f), InteriorTheme.Classroom, new Color32(0x1C, 0x26, 0x20, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(9f, 7f),
                "讲台 · 培训补贴", "走出职业培训中心",
                "走进职业培训中心 · 讲台按 E 查看培训与补贴,门垫按 E 出店", "走出了职业培训中心"),
            New("factory", 18, 12, new Vector3(560f, 0f, 0f), InteriorTheme.Factory, new Color32(0x1A, 0x1E, 0x24, 255),
                new int[] { 4, 9, 14 },
                new Vector3(9f, 1.8f), new Vector3(9f, .75f), new Vector3(11f, 4.7f),
                "产线台 · 环境治理", "走出河畔工厂",
                "走进河畔工厂 · 产线台按 E 查看环境治理,门垫按 E 出店", "走出了河畔工厂"),
        };

        /// <summary>按 kind 查目录;未知 kind 回落 shop(守卫语义:门表配错不至于进不了店)。</summary>
        public static InteriorSpec Get(string kind)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].kind == kind) return All[i];
            return All[0];
        }

        public static bool Exists(string kind)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].kind == kind) return true;
            return false;
        }

        static InteriorSpec New(string kind, int w, int h, Vector3 origin, InteriorTheme theme,
            Color32 bg, int[] windows, Vector3 entry, Vector3 exit, Vector3 counter,
            string counterLabel, string exitLabel, string enterToast, string exitToast)
        {
            // 小镇侧门点(出店返回兜底):惰性闭包读 MapBuilder 静态属性(注册/构造时门属性可能未建)。
            // V0.9.12 十间后三元链不可读,改 if-else 链;V0.9.13 第三批续至十五间——语义不变(仍逐 kind 一条闭包)
            System.Func<Vector3> door;
            if (kind == "shop") door = () => MapBuilder.ShopDoor;
            else if (kind == "restaurant") door = () => MapBuilder.RestaurantDoor;
            else if (kind == "cinema") door = () => MapBuilder.CinemaDoor;
            else if (kind == "market") door = () => MapBuilder.MarketDoor;
            else if (kind == "competitor") door = () => MapBuilder.CompetitorDoor;
            else if (kind == "home") door = () => MapBuilder.HomeDoor;
            else if (kind == "convenience") door = () => MapBuilder.ConvenienceDoor;
            else if (kind == "bookstore") door = () => MapBuilder.BookstoreDoor;
            else if (kind == "decor_b") door = () => MapBuilder.DecorBDoor;
            else if (kind == "decor_a") door = () => MapBuilder.DecorADoor;
            else if (kind == "bank") door = () => MapBuilder.BankDoor;
            else if (kind == "centralbank") door = () => MapBuilder.CentralBankDoor;
            else if (kind == "hall") door = () => MapBuilder.HallDoor;
            else if (kind == "training") door = () => MapBuilder.TrainingDoor;
            else door = () => MapBuilder.FactoryDoor;   // factory
            return new InteriorSpec
            {
                kind = kind,
                width = w,
                height = h,
                origin = origin,
                theme = theme,
                backgroundColor = bg,
                windowColumns = windows,
                entryLocal = entry,
                exitLocal = exit,
                counterLocal = counter,
                counterLabel = counterLabel,
                exitLabel = exitLabel,
                enterToast = enterToast,
                exitToast = exitToast,
                door = door,
            };
        }
    }
}
