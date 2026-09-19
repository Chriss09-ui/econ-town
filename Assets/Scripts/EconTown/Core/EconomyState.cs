using System;
using UnityEngine;

namespace EconTown
{
    [Serializable]
    public class EnterpriseMarketState
    {
        public bool isOpen;
    }

    [Serializable]
    public class MarketState
    {
        // 玩家的开门/歇业不由状态位表达,而是"当天是否有营业记录"——playerShop.isOpen 曾是恒 true 的死状态,已删。
        public EnterpriseMarketState sunnyTea = new EnterpriseMarketState();
    }

    [Serializable]
    public class LaborMarketState
    {
        public int employedWorkers;
        public float effectiveDailyWage = Eco.WagePerEmployee;
        public int laborPotentialCapacity;
        public int equipmentCapacity = Eco.EquipmentCap;
        public int actualCapacity;
        public int actualMarginalProduct;
        public int workingAgePopulation;
        public int laborForce;
        public int employment;
        public int unemployment;
        public float unemploymentRate;
        public bool minimumWageBinding;
    }

    /// <summary>可被 JsonUtility 保存的确定性随机数状态。</summary>
    [Serializable]
    public class DeterministicRandomState
    {
        public int seed = 20260824;
        public int state = 20260824;
        public long calls;

        public void Reset(int value)
        {
            seed = value == 0 ? 1 : value;
            state = seed;
            calls = 0;
        }

        public float Next01()
        {
            uint x = unchecked((uint)state);
            if (x == 0u) x = 0x6D2B79F5u;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = unchecked((int)x);
            calls++;
            return (x & 0x00FFFFFFu) / 16777216f;
        }

        public int Next(int minimum, int maximum)
        {
            if (maximum <= minimum) return minimum;
            return minimum + Mathf.FloorToInt(Next01() * (maximum - minimum));
        }
    }

    /// <summary>
    /// V0.7.1 当日氛围视觉缓存：由 AtmosphereService 在收日管线固定点一次性算好并落档。
    /// 一切 UI 与渲染只读缓存，严禁在面板打开、按 E 交互、读档回清晨等路径上取数——
    /// 否则流的推进次数取决于玩家操作序列，同种子双跑一致与读档可复现全部失效（PRD §123C）。
    /// </summary>
    [Serializable]
    public class AtmosphereState
    {
        public int weather;               // 装饰天气档 0晴 / 1多云 / 2雨 / 3雪(仅冬)
        public bool gloomOverlay;         // 事件阴天灰蒙(原料涨价)；装饰阴天只画云层不加灰蒙，二者可区分
        public bool festivalDecor;        // 小镇节日彩旗灯笼
        public bool heatWave;             // 炎热天气暖滤镜加强
        public int windowTier = -1;       // 企业信心投影 -1未初始化 / 0暗(<40) / 1中(40–60) / 2全亮(≥60)
        public bool windowTierChangedToday; // 档位切换当日置位(公告栏对冲提示用)
        public int transferSignShop = -1; // 转让牌命中的装饰铺索引(仅信心<40 时选出)，-1 无牌
        public int streetTarget = 12;     // 街景目标人数(失业率仪表,与开局 12 人现状衔接)
        public int streetCurrent = 12;    // 当前街景人数(±2/日渐变逼近 target)
        public bool seasonRequest;        // 换季重建请求(场景层消费后清位)
        // V0.7.3 书店「本周概念」(§141):仅东商业新区落成后,在收日管线的周切换日由
        // atmosphereRandom 掷一次并落档;0 周号 = 尚未占周(新区未落成不消耗随机流次数)。
        public int conceptWeek;
        public int weeklyConceptCard = -1; // 命中的 CardLibrary.All 索引,-1 无(未落成/无已解锁卡)
    }

    /// <summary>V0.5.1 唯一世界经济状态；其他周期只能由 worldDay 派生。</summary>
    [Serializable]
    public class EconomyState
    {
        public int worldDay = 1;
        public int spoilageAtDayStart;
        public int competitorSpoilageAtDayStart;
        public float playerTemporaryPriceDelta;
        public int playerTemporaryPriceExpiresAfterDay;
        public string playerTemporaryPriceReason;
        public int financialDistressDays;
        public int financialHealthLastWorldDay;
        public bool financialCrisisActive;
        public bool emergencySupportUsed;
        public MarketState market = new MarketState();
        public LaborMarketState labor = new LaborMarketState();
        public DeterministicRandomState random = new DeterministicRandomState();
        // V0.7 国际贸易专用随机流:与共享流隔离——中途在共享流取数会平移旧档全部
        // 确定性序列,国际模块只准用这一条(SeedNewGame/SetRandomSeed 同步 Reset)。
        public DeterministicRandomState tradeRandom = new DeterministicRandomState();
        // V0.7.1 氛围专用随机流:呈现层只准用这一条(天气/转让牌),取数点只在收日管线固定顺序,
        // 与 random/tradeRandom 隔离(SeedNewGame/SetRandomSeed 同步 Reset,异或偏移各不相同)。
        public DeterministicRandomState atmosphereRandom = new DeterministicRandomState();
        public AtmosphereState atmosphere = new AtmosphereState();

        public void Ensure()
        {
            if (worldDay < 1) worldDay = 1;
            if (market == null) market = new MarketState();
            if (market.sunnyTea == null) market.sunnyTea = new EnterpriseMarketState();
            if (labor == null) labor = new LaborMarketState();
            if (random == null) random = new DeterministicRandomState();
            if (tradeRandom == null) tradeRandom = new DeterministicRandomState();
            if (atmosphereRandom == null) atmosphereRandom = new DeterministicRandomState();
            if (atmosphere == null) atmosphere = new AtmosphereState();
            SyncDay(worldDay);
        }

        public void SyncDay(int value)
        {
            worldDay = Mathf.Max(1, value);
            if (playerTemporaryPriceExpiresAfterDay > 0 && worldDay > playerTemporaryPriceExpiresAfterDay)
            {
                playerTemporaryPriceDelta = 0f;
                playerTemporaryPriceExpiresAfterDay = 0;
                playerTemporaryPriceReason = null;
            }
            // V0.5.2 起竞争者由企业篇关键行为完成后开放。WorldDay 只负责时间，不能再偷偷推进主线。
        }

        public void SetTemporaryPlayerPrice(float delta, int throughWorldDay, string reason)
        {
            playerTemporaryPriceDelta = delta;
            playerTemporaryPriceExpiresAfterDay = Mathf.Max(worldDay, throughWorldDay);
            playerTemporaryPriceReason = reason;
        }
    }
}
