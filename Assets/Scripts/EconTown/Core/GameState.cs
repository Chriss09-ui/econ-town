using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public enum GamePhase { Morning, Business, Report, Ended }

    [Serializable]
    public class DayRecord
    {
        public int day;
        public bool shopOpen = true;
        public float listedPrice;
        public float price;
        public int planned;
        public int produced;
        public int desiredDemand;
        public int availableSupply;
        public int actualSales;
        public int operationalStockout;
        public int operationalSurplus;
        public int demandTotal;
        public int demandStudents;
        public int demandWhitecollars;
        public int demandTourists;
        public int soldStudents;
        public int soldWhitecollars;
        public int soldTourists;
        public int sold;
        public int lostCustomers;
        public int stockoutLost;
        public float revenue;
        public float materialCost;
        public float wageCost;
        public float rentCost;
        public float interestExpense;
        public float totalCost;
        public float profit;
        public int inventoryEnd;
        public int materialsEnd;
        public int spoiled;
        public float satisfaction;
        public string eventName;
        public float moneyEnd;
        public float marginalCost;

        // V0.2 企业竞争
        public bool competitionActive;
        public float playerQuality;
        public float playerBrand;
        public float playerMarketing;
        public float competitorPrice;
        public float competitorQuality;
        public float competitorBrand;
        public float competitorMarketing;
        public int competitorDemand;
        public int competitorSold;
        public float competitorRevenue;
        public float competitorProfit;
        public string competitorStatus;
        public int totalMarketDemand;
        public int outsideDemand;
        public float playerMarketShare;
        public float competitorMarketShare;
        public int switchedToCompetitor;
        public string competitorStrategy;
        public string playerStrategyChoice;
        public bool priceWar;
        public int priceWarRound;
        public string competitionEventName;
        public float crossPriceElasticity;
        public bool hasCrossPriceElasticity;
        public float strategicSpend;
        public float counterfactualPrice;
        public int counterfactualSold;
        public float counterfactualProfit;

        // V0.4 政府与公共政策
        public float consumerPrice;
        public float producerNetPrice;
        public float salesTaxPerUnit;
        public float salesSubsidyPerUnit;
        public float salesTaxRevenue;
        public float subsidyCost;
        public float consumerTaxBurden;
        public float producerTaxBurden;
        public float deadweightLoss;
        public int policyBaselineQuantity;
        public float priceCeiling;
        public float priceFloor;
        public int policyShortage;
        public int policySurplus;
        public bool priceCeilingBinding;
        public bool priceFloorBinding;
        public DemandEffectBreakdown demandEffects;

        // V0.5 宏观核算：资本品、外销与进口投入分别记账，避免混入最终消费。
        public float capitalInvestment;
        public float exports;
        public float imports;

        // V0.7 国际贸易：exports/imports 语义升级为真实贸易流总值；出口三件套单列。
        public int exportUnits;
        public float exportRevenue;
        public float exportShippingCost;

        // V0.9.1 住房账本行(R18):章节信号与守恒断言的数据落点。
        public float rentPaid;
        public float mortgagePrincipalPaid;
        public float mortgageInterestPaid;
    }

    [Serializable]
    public class CompetitorState
    {
        public string companyName = "Sunny Tea";
        public CompetitorStrategy strategy = CompetitorStrategy.Stable;
        public float price = 18f;
        public float quality = 72f;
        public float brand = 55f;
        public float marketing = 100f;
        public int plannedProduction = 130;
        public int inventory;
        public int capacity = 150;
        public float unitCost = 8f;
        public float fixedCost = 260f;
        public float satisfaction = 76f;
        public int lastSales;
        public float lastRevenue;
        public float lastProfit;
        public float marketShare = 0.5f;
        public int promoDays;
        public float promoBasePrice;
        public float qualityBonus;
        public float attractionMul = 1f;

        public float EffectiveQuality { get { return Mathf.Clamp(quality + qualityBonus, 0f, 100f); } }
    }

    /// <summary>全局游戏状态:资源、解锁进度、历史记录。</summary>
    public class GameState : MonoBehaviour
    {
        public const string Version = "V0.7.2";
        public static GameState I;

        [Header("核心状态")]
        public EconomyState economy = new EconomyState();
        public float money = 2000f;
        public float price = 15f;
        public int planProduction = 100;
        public int employees = 1;
        public int materials = 300;
        public int inventory = 0;
        public int totalDays = TimelineConfig.DefaultTotalDays;
        public float startingMoney = 2000f;

        [Header("V0.2 企业竞争")]
        public int playerQualityLevel = 1;
        // 围猎三复审:当日清晨的品质等级快照——读档重放日据此回滚「升级费退了等级还在」的白嫖循环。
        // 0 = 旧档无此字段(缺省),回滚时跳过。
        public int playerQualityLevelAtDayStart;
        public float playerBrand = 30f;
        public float marketingBudget;
        public float playerMarketingMomentum;
        public float todayStrategicSpend;
        public CompetitorState competitor = new CompetitorState();
        public bool competitionIntroduced;
        public bool priceWarActive;
        public int priceWarRound;
        public string todayStrategyChoice;
        public string todayCompetitionEventName;
        public float playerAttractionMul = 1f;
        public int marketExperimentMask;

        [Header("V0.3 居民生活")]
        public PlayerViewMode currentView = PlayerViewMode.Enterprise;
        public ResidentState resident = new ResidentState();
        [Range(0.55f, 1.5f)] public float residentPlayerDemandMul = 1f;
        [Range(0.55f, 1.5f)] public float residentCompetitorDemandMul = 1f;
        public int residentCinemaDiscountDays;

        [Header("V0.4 政府与公共政策")]
        public GovernmentState government = new GovernmentState();

        [Header("V0.5 宏观经济")]
        public MacroEconomyState macro = new MacroEconomyState();

        [Header("V0.6 银行、货币与央行")]
        public BankState bank = new BankState();
        public CentralBankState centralBank = new CentralBankState();
        public float companyCapitalInvestment;
        public float companyInterestExpense;
        public float todayCompanyInterestExpense;
        public float pendingFinancedInvestment;
        public int financedEquipmentCapacityBonus;

        [Header("V0.7 国际经济与世界贸易")]
        public WorldTradeState worldTrade = new WorldTradeState();
        // 出海是玩家决策:完成第 43 章后由贸易面板/企业页开通;配额是每日期望上限(≤港口日处理)。
        public bool exportChannelEnabled;
        public int exportTargetQuotaCups = 40;

        [Header("V0.9 住房与资产")]
        public HousingState housing = new HousingState();

        [Header("V0.6 章节、知识与长期游玩")]
        public ProgressionState progression = new ProgressionState();
        public KnowledgeProgressState knowledge = new KnowledgeProgressState();
        public SandboxState sandbox = new SandboxState();
        public LongTermExperienceState experience = new LongTermExperienceState();
        // 0=正式剧情；1–8=本份篇章游玩档最初从哪一篇开始。
        // 用于学习路线把预置前置标为「标准前置」，不冒充玩家成绩。
        public int chapterPlayEntryPart;

        [NonSerialized] public GamePhase phase = GamePhase.Morning;

        [NonSerialized] public List<DayRecord> history = new List<DayRecord>();
        [NonSerialized] public HashSet<string> unlockedCards = new HashSet<string>();
        [NonSerialized] public string todayEventName = null;
        // 今日事件的稳定 id(如 "hot"):存档重建 LastEvent 的首选键,事件中文文案改名不影响旧档(C3)。
        [NonSerialized] public string todayEventId = null;

        // 今日事件修正系数(每日重置)
        [NonSerialized] public float demandMul = 1f;
        [NonSerialized] public float studentMul = 1f;
        [NonSerialized] public float touristMul = 1f;
        [NonSerialized] public float materialPriceMul = 1f;

        public event Action OnStateChanged;
        public event Action<string, string> OnCardUnlocked;   // id, title

        public int day
        {
            get { EnsureEconomyState(); return economy.worldDay; }
            set
            {
                EnsureEconomyState();
                economy.SyncDay(value);
                WorldClock.SyncDerivedPeriods(this);
            }
        }

        public void EnsureEconomyState()
        {
            if (economy == null) economy = new EconomyState();
            economy.Ensure();
        }

        public bool CanProduction => progression != null && progression.completedChapter >= 1;
        public bool CanMaterials => progression != null && progression.completedChapter >= 2;
        public bool CanEmployees => progression != null && progression.completedChapter >= 3;
        public bool EventsActive => day >= TimelineConfig.TownEventsStartDay;
        public bool CompetitionActive
        {
            get { EnsureEconomyState(); return economy.market.sunnyTea.isOpen; }
        }
        public bool CanQuality => progression != null && progression.currentChapter >= 8;
        public bool CanMarketing => progression != null && progression.currentChapter >= 8;
        public bool CanStrategyLab => progression != null && progression.currentChapter >= 9;
        public float PlayerQuality => CompetitionEco.QualityForLevel(playerQualityLevel);
        public float PlayerUnitCost => CostService.EffectiveInputUnitCost(this);

        public DayRecord LastRecord => history.Count > 0 ? history[history.Count - 1] : null;
        public DayRecord Yesterday => history.Count >= 2 ? history[history.Count - 2] : null;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            WorldClock.SyncDerivedPeriods(this);
            ProgressionService.Ensure(this);
            KnowledgeSystem.Ensure(this);
            SandboxService.Ensure(this);
            LongTermExperienceService.Ensure(this);
            FinancialSystemService.Ensure(this);
            InternationalTradeService.Ensure(this);
            HousingService.Ensure(this);
        }

        void OnDestroy()
        {
            // 场景卸载时清掉静态单例,避免悬挂引用只靠 Unity fake-null 侥幸兜底(C11)。
            if (I == this) I = null;
        }

        public void Changed() { if (OnStateChanged != null) OnStateChanged(); }

        public bool UnlockCard(string id)
        {
            if (id == null || unlockedCards.Contains(id)) return false;
            unlockedCards.Add(id);
            CardDef def = CardLibrary.Get(id);
            if (OnCardUnlocked != null && def != null) OnCardUnlocked(id, def.title);
            return true;
        }

        public int TotalSold { get { int s = 0; for (int i = 0; i < history.Count; i++) s += history[i].sold; return s; } }
        public float TotalProfit { get { float p = 0; for (int i = 0; i < history.Count; i++) p += history[i].profit; return p; } }
        public float AverageMarketShare
        {
            get
            {
                float total = 0f; int count = 0;
                for (int i = 0; i < history.Count; i++)
                    if (history[i].competitionActive) { total += history[i].playerMarketShare; count++; }
                return count > 0 ? total / count : 0f;
            }
        }

        public int CompletedMarketExperiments
        {
            get
            {
                int count = 0;
                if ((marketExperimentMask & 1) != 0) count++;
                if ((marketExperimentMask & 2) != 0) count++;
                if ((marketExperimentMask & 4) != 0) count++;
                return count;
            }
        }

        public float ResidentDemandChangePercent { get { return (residentPlayerDemandMul - 1f) * 100f; } }
    }
}
