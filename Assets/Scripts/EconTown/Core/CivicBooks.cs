using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// 市政府、国家账本、国际收支的显示口径。
    /// 模拟内部仍是果汁店的「元」和 320 人样本，测试锁定的公式不改。
    /// 这些面板把 1 模型元显示成 1 万元，把 1 模型人显示成 1 千人，
    /// 所以人均要再乘 10，单位仍是元。
    /// 一杯果汁、时薪、房价、玩家口袋、店里的钱、个人存款不走这里。
    /// </summary>
    public static class CivicBooks
    {
        public const float YuanPerModelUnit = 10000f;
        public const float PeoplePerModelPerson = 1000f;

        public static string Money(float modelYuan)
        {
            float yuan = modelYuan * YuanPerModelUnit;
            if (yuan < 0f) return "−¥" + Amount(-yuan);
            return "¥" + Amount(yuan);
        }

        public static string Signed(float modelYuan)
        {
            float yuan = modelYuan * YuanPerModelUnit;
            if (Mathf.Abs(yuan) < 5000f) return "¥0";
            return (yuan > 0f ? "+¥" : "−¥") + Amount(Mathf.Abs(yuan));
        }

        public static string MoneyPerPerson(float modelYuan)
        {
            float yuan = modelYuan * (YuanPerModelUnit / PeoplePerModelPerson);
            return "¥" + Amount(Mathf.Abs(yuan));
        }

        public static string Tick(float modelYuan)
        {
            return Amount(Mathf.Abs(modelYuan * YuanPerModelUnit));
        }

        public static string Heads(float modelPeople)
        {
            return Count(modelPeople, "人");
        }

        public static string Count(float modelUnits, string unit)
        {
            float n = modelUnits * PeoplePerModelPerson;
            if (Mathf.Abs(n) >= 10000f)
            {
                float wan = n / 10000f;
                string num = Mathf.Abs(wan) >= 100f ? wan.ToString("0") : wan.ToString("0.#");
                return num + "万" + unit;
            }
            return Mathf.RoundToInt(n).ToString("0") + unit;
        }

        public static string Foreign(float modelB)
        {
            return Amount(Mathf.Abs(modelB * YuanPerModelUnit)) + " B币";
        }

        public static string SignedForeign(float modelB)
        {
            float v = modelB * YuanPerModelUnit;
            if (Mathf.Abs(v) < 50f) return "0 B币";
            return (v > 0f ? "+" : "−") + Amount(Mathf.Abs(v)) + " B币";
        }

        public static string Amount(float yuan)
        {
            float v = Mathf.Abs(yuan);
            if (v >= 100000000f)
            {
                float yi = v / 100000000f;
                return (yi >= 100f ? yi.ToString("0") : yi.ToString("0.00")) + "亿";
            }
            if (v >= 10000f)
            {
                float wan = v / 10000f;
                return (wan >= 100f ? wan.ToString("0") : wan.ToString("0.#")) + "万";
            }
            return Mathf.RoundToInt(v).ToString("0");
        }
    }
}
