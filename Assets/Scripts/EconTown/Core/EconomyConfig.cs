namespace EconTown
{
    /// <summary>经济参数与常量(数值均来自 PRD)。</summary>
    public static class Eco
    {
        public const float BasePrice = 15f;          // 基准价
        public const float MaterialUnitCost = 6f;    // 每杯原材料成本
        public const float WagePerEmployee = 200f;   // 每名员工日工资
        public const float DailyRent = 100f;         // 租金+设备折旧(固定成本)
        public const int EquipmentCap = 240;         // 设备单日上限；前四名员工的边际产量不会被过早遮蔽
        public const float SpoilageRate = 0.20f;     // 隔夜损耗率
        public const int BaseDemand = 100;           // 基础日需求(杯)
        public const int MaxEmployees = 6;

        // 员工数 → 日产能(体现边际产量递减)
        public static readonly int[] CapacityByEmployees = { 0, 80, 145, 195, 225, 248, 265 };

        public static int Capacity(int employees)
        {
            if (employees <= 0) return 0;
            if (employees >= CapacityByEmployees.Length) return CapacityByEmployees[CapacityByEmployees.Length - 1];
            return CapacityByEmployees[employees];
        }
    }

    public class ConsumerType
    {
        public string name;
        public float share;
        public float elasticity;

        public static readonly ConsumerType Student = new ConsumerType { name = "学生", share = 0.40f, elasticity = 1.8f };
        public static readonly ConsumerType WhiteCollar = new ConsumerType { name = "白领", share = 0.40f, elasticity = 1.0f };
        public static readonly ConsumerType Tourist = new ConsumerType { name = "游客", share = 0.20f, elasticity = 0.5f };
    }

    public class TownEvent
    {
        public string id;
        public string name;
        public string desc;
        public float demandMul = 1f;
        public float studentMul = 1f;
        public float touristMul = 1f;
        public float materialMul = 1f;
        public float weight = 1f;
    }

    public static class TownEvents
    {
        public static readonly TownEvent HotWeather = new TownEvent
        {
            id = "hot", name = "炎热天气", desc = "今天特别热,大家更想喝冷饮。\n饮品需求 +20%",
            demandMul = 1.20f, weight = 1f
        };
        public static readonly TownEvent MaterialUp = new TownEvent
        {
            id = "matup", name = "原料涨价", desc = "水果减产,进货价上涨。\n原材料成本 +30%",
            materialMul = 1.30f, weight = 1f
        };
        public static readonly TownEvent SchoolOff = new TownEvent
        {
            id = "schooloff", name = "学校放假", desc = "学校今天放假出游,学生顾客 -50%。",
            studentMul = 0.50f, weight = 1f
        };
        public static readonly TownEvent Festival = new TownEvent
        {
            id = "festival", name = "小镇节日", desc = "小镇举办节日集市,游客数量 +80%!",
            touristMul = 1.80f, weight = 1f
        };

        public static readonly TownEvent[] All = { HotWeather, MaterialUp, SchoolOff, Festival };

        // 按权重掷事件的真实实现只有一份:DaySimulator.RollTownEvent(走确定性随机数)。
    }
}
