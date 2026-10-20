using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public enum PlayerViewMode { Enterprise, Resident, Government, Macro, Bank, CentralBank }
    public enum ResidentProfession { StoreClerk, Courier, Freelancer }
    public enum ResidentPreference { Foodie, Entertainment, Saver }

    public enum ResidentItemKind
    {
        BasicMeal, PremiumMeal,
        Water, PlayerJuice, RivalTea,
        ParkWalk, Movie, Popcorn,
        Soap, HomeDecor,
        ImportedSnack   // V0.7 进口最终消费品:basePrice 字段此时表示 B币标价,经到岸价公式折算
    }

    [Serializable]
    public class ResidentChoiceRecord
    {
        public int day;
        public string itemId;
        public string itemName;
        public string category;
        public float price;
        public float utility;
        public float marginalUtility;
        public float consumerSurplus;
        public float hours;
        public string reason;
    }

    [Serializable]
    public class ResidentDayRecord
    {
        public int day;
        public float cashStart;
        public float cashEnd;
        public float savingsEnd;
        public float income;
        public float spending;
        public float saved;
        public float emergencyWithdrawal;
        public float interestIncome;
        public float interestExpense;
        public float totalUtility;
        public float happinessEnd;
        public float energyEnd;
        public float satietyEnd;
        public float entertainmentEnd;
        public float freeHoursUsed;
        public string eventName;
        public string summary;
        public List<ResidentChoiceRecord> choices = new List<ResidentChoiceRecord>();
    }

    [Serializable]
    public class ResidentState
    {
        public bool profileCreated;
        public string residentName = "小经";
        public ResidentProfession profession = ResidentProfession.StoreClerk;
        public ResidentPreference preference = ResidentPreference.Foodie;
        public int lifeDay = 1;
        public float cash = 100f;
        public float savings = 1000f;
        public float interestIncome;
        public float interestExpense;
        public float todayInterestIncome;
        public float todayInterestExpense;
        public float dailyIncome = 60f;
        public float freeHours = 4f;
        public float energy = 100f;
        public float happiness = 60f;
        public float satiety = 50f;
        public float entertainment = 45f;
        public float household = 55f;
        public float dailyUtility;
        public float todaySpending;
        public float todayIncome;
        public float todaySaved;
        public float todayEmergencyWithdrawal;
        public float cashAtDayStart = 100f;
        public float freeHoursAtDayStart = 4f;
        // bug搜#8:技能的清晨快照(照 cashAtDayStart 惯例)——重放回滚时把当日学习/长经节点
        // 白拿的技能退回清晨值,否则「学习→完成今日→checkpoint→恢复」每轮白拿 2 点(封顶 40)。
        // -1=哨兵「未快照/旧档」(JsonUtility 缺字段保留初始化器,旧档读出 -1 跳过回滚);
        // CreateProfile 显式置 0——首日清晨技能 0 是合法快照,回滚到 0 才封得死白拿循环。
        public float skillLevelAtDayStart = -1f;
        // bug搜#11:精神状态清晨快照(照 skillLevelAtDayStart 的 -1 哨兵惯例)——重放回滚把
        // 日终衰减/白天消费影响退回清晨值;否则重放日从「昨日日末」状态开局,WorkEfficiency
        // 系统性偏低,连续存读循环可把状态叠到 0 附近。
        public float energyAtDayStart = -1f;
        public float satietyAtDayStart = -1f;
        public float happinessAtDayStart = -1f;
        public float entertainmentAtDayStart = -1f;
        public int foodCount;
        public int drinkCount;
        public int entertainmentCount;
        public int householdCount;
        public bool reportReady;
        public int chapter = 1;
        public int chapterMask = 1;
        public int experimentMask;
        public int completedDays;
        public int playerJuiceBought;
        public int rivalTeaBought;
        public int savingsActions;
        public int leisureActions;
        public int workActions;
        public int studyActions;
        public float skillLevel;
        public int basicPurchases;
        public int premiumPurchases;
        public bool gigUnavailable;
        public float todayDiscount;
        public bool wageAdvanced;
        public bool employedToday = true;
        public int unemployedDays;
        public string todayEventName;
        public string todayEventDetail;
        public string lastChoiceReason;
        public string lastExperimentSummary;
        public List<ResidentChoiceRecord> todayChoices = new List<ResidentChoiceRecord>();
        public List<ResidentDayRecord> history = new List<ResidentDayRecord>();
    }

    public struct ResidentItemDef
    {
        public ResidentItemKind kind;
        public string name;
        public string category;
        public bool premium;
        public float basePrice;
        public float baseUtility;
        public float hours;
        public float satiety;
        public float happiness;
        public float energy;
        public float entertainment;
        public float household;
    }

    public struct ResidentActionResult
    {
        public bool success;
        public string message;
        public ResidentChoiceRecord choice;
    }

    /// <summary>V0.3 居民消费模拟。它只处理规则与数据，不依赖任何 UI。</summary>
    public static class ResidentEconomy
    {
        public const int TotalLifeDays = 30;
        public const float DailyFreeHours = 4f;

        static readonly ResidentItemDef[] items =
        {
            Item(ResidentItemKind.BasicMeal, "家常套餐", "食物", false, 12f, 22f, .75f, 30f, 2f, 1f, 0f, 0f),
            Item(ResidentItemKind.PremiumMeal, "餐厅招牌餐", "食物", true, 28f, 40f, 1.25f, 42f, 9f, 1f, 0f, 0f),
            Item(ResidentItemKind.Water, "白水", "饮料", false, 2f, 9f, .25f, 0f, 1f, 7f, 0f, 0f),
            Item(ResidentItemKind.PlayerJuice, "我的果汁店鲜榨果汁", "饮料", true, 15f, 30f, .5f, 2f, 7f, 9f, 0f, 0f),
            Item(ResidentItemKind.RivalTea, "Sunny Tea 水果茶", "饮料", true, 18f, 28f, .5f, 1f, 6f, 8f, 0f, 0f),
            Item(ResidentItemKind.ParkWalk, "公园散步", "娱乐", false, 0f, 14f, 1f, 0f, 8f, 2f, 10f, 0f),
            Item(ResidentItemKind.Movie, "电影院看电影", "娱乐", true, 35f, 38f, 2f, 0f, 17f, -3f, 28f, 0f),
            Item(ResidentItemKind.Popcorn, "爆米花", "娱乐", false, 8f, 12f, .25f, 5f, 5f, 0f, 4f, 0f),
            Item(ResidentItemKind.Soap, "日用清洁包", "日用品", false, 9f, 16f, .25f, 0f, 2f, 0f, 0f, 24f),
            Item(ResidentItemKind.HomeDecor, "房间装饰", "日用品", true, 26f, 31f, 1f, 0f, 13f, 0f, 3f, 35f),
            // 进口商品:B币标价 0.85(≈¥6@锚),价格随汇率×(1+关税)波动——输入型通胀的微观入口。
            Item(ResidentItemKind.ImportedSnack, "进口巧克力礼盒", "食物", true, .85f, 26f, .25f, 14f, 15f, 3f, 0f, 0f)
        };

        static ResidentItemDef Item(ResidentItemKind kind, string name, string category, bool premium,
            float price, float utility, float hours, float satiety, float happiness, float energy,
            float entertainment, float household)
        {
            return new ResidentItemDef
            {
                kind = kind, name = name, category = category, premium = premium,
                basePrice = price, baseUtility = utility, hours = hours,
                satiety = satiety, happiness = happiness, energy = energy,
                entertainment = entertainment, household = household
            };
        }

        public static IList<ResidentItemDef> Items { get { return items; } }

        public static void Ensure(GameState s)
        {
            if (s == null) return;
            if (s.resident == null) s.resident = new ResidentState();
            ResidentState r = s.resident;
            if (r.todayChoices == null) r.todayChoices = new List<ResidentChoiceRecord>();
            if (r.history == null) r.history = new List<ResidentDayRecord>();
            if (r.lifeDay < 1) r.lifeDay = 1;
            if (r.dailyIncome <= 0f) r.dailyIncome = IncomeFor(r.profession);
            if (r.freeHoursAtDayStart <= 0f) r.freeHoursAtDayStart = DailyFreeHours;
        }

        public static void CreateProfile(GameState s, string name, ResidentProfession profession, ResidentPreference preference)
        {
            if (s == null) return;
            s.resident = new ResidentState
            {
                profileCreated = true,
                residentName = string.IsNullOrWhiteSpace(name) ? "小经" : name.Trim(),
                profession = profession,
                preference = preference,
                lifeDay = s.day,
                skillLevelAtDayStart = 0f,   // bug搜#8复审:首日清晨快照=0(否则哨兵 -1 跳过回滚,白拿循环在首日仍开放)
                // bug搜#11:首日精神状态快照=字段初值(100/50/60/45),重放同样可回滚。
                energyAtDayStart = 100f, satietyAtDayStart = 50f,
                happinessAtDayStart = 60f, entertainmentAtDayStart = 45f,
                dailyIncome = IncomeFor(profession) * GovernmentSimulator.ResidentIncomeMultiplier(s)
            };
            ApplyEvent(s);
            UnlockChapter(s, 1);
            s.UnlockCard("preference");
            s.Changed();
        }

        public static float IncomeFor(ResidentProfession profession)
        {
            switch (profession)
            {
                case ResidentProfession.Courier: return 70f;
                case ResidentProfession.Freelancer: return 82f;
                default: return 60f;
            }
        }

        public static float WorkEfficiency(ResidentState resident)
        {
            if (resident == null) return 1f;
            float energy = resident.energy >= 30f ? 1f : Mathf.Lerp(.55f, 1f, resident.energy / 30f);
            float satiety = resident.satiety >= 25f ? 1f : Mathf.Lerp(.60f, 1f, resident.satiety / 25f);
            float morale = resident.happiness < 35f ? Mathf.Lerp(.85f, 1f, resident.happiness / 35f) :
                resident.happiness > 80f ? Mathf.Lerp(1f, 1.05f, (resident.happiness - 80f) / 20f) : 1f;
            return Mathf.Clamp(energy * satiety * morale, .40f, 1.05f);
        }

        public static float SkillIncomeMultiplier(ResidentState resident)
        {
            return resident == null ? 1f : 1f + Mathf.Clamp(resident.skillLevel, 0f, 40f) * .01f;
        }

        public static bool IsWelfareEligible(ResidentState resident)
        {
            return resident != null && (!resident.employedToday || resident.todayIncome < GovernmentSimulator.WelfareEligibilityIncomeLine);
        }

        public static string ProfessionName(ResidentProfession profession)
        {
            switch (profession)
            {
                case ResidentProfession.Courier: return "小镇配送员";
                case ResidentProfession.Freelancer: return "自由创作者";
                default: return "市场帮工";
            }
        }

        public static string PreferenceName(ResidentPreference preference)
        {
            switch (preference)
            {
                case ResidentPreference.Entertainment: return "娱乐优先";
                case ResidentPreference.Saver: return "稳健储蓄";
                default: return "美食爱好者";
            }
        }

        public static ResidentItemDef GetItem(ResidentItemKind kind)
        {
            for (int i = 0; i < items.Length; i++) if (items[i].kind == kind) return items[i];
            return items[0];
        }

        public static float Price(GameState s, ResidentItemKind kind)
        {
            if (kind == ResidentItemKind.PlayerJuice)
                return GovernmentSimulator.DrinkConsumerPrice(s, PriceService.PlayerListedPrice(s));
            if (kind == ResidentItemKind.RivalTea)
                return GovernmentSimulator.DrinkConsumerPrice(s,
                    Mathf.Max(1f, s.competitor != null ? s.competitor.price : 18f));
            // V0.7 进口品:basePrice 是 B币标价,到岸价 = 外币价×汇率×(1+本国关税),不走本地折扣。
            if (kind == ResidentItemKind.ImportedSnack)
                return InternationalPriceService.ImportedSkuConsumerPrice(s, GetItem(kind).basePrice);
            float price = GetItem(kind).basePrice;
            if (kind == ResidentItemKind.Movie && s.residentCinemaDiscountDays > 0) price *= .5f;
            if (s.resident != null && s.resident.todayDiscount > 0f) price *= 1f - Mathf.Clamp(s.resident.todayDiscount, 0f, .5f);
            return price;
        }

        public static ResidentActionResult Consume(GameState s, ResidentItemKind kind)
        {
            Ensure(s);
            ResidentState r = s.resident;
            ResidentItemDef def = GetItem(kind);
            float price = Price(s, kind);
            if (r.reportReady) return Fail("今天已经记账完成，先进入下一天");
            if (r.cash + .001f < price) return Fail("现金不够：还差 ¥" + (price - r.cash).ToString("0"));
            if (r.freeHours + .001f < def.hours) return Fail("今天的自由时间不够了");

            int previous = CategoryCount(r, def.category);
            float preference = PreferenceMultiplier(r.preference, def.category, def.premium);
            float complement = def.kind == ResidentItemKind.Popcorn && HasChoice(r, ResidentItemKind.Movie) ? 2f : 1f;
            float marginalUtility = def.baseUtility * preference * complement / (1f + previous * .65f);
            // 围猎三(#11):进口品 basePrice 是 B币标价(0.85),进 WTP 前先折本币参考价——
            // 关税不进效用锚,只折汇率;旧写法把 B币 0.85 当 ¥0.85 用,剩余读数口径错。
            float wtpBase = kind == ResidentItemKind.ImportedSnack
                ? def.basePrice * ExchangeRateService.Rate(s)
                : def.basePrice;
            float willingnessToPay = wtpBase + def.baseUtility * preference * .55f;
            float surplus = Mathf.Max(0f, willingnessToPay - price);

            r.cash -= price;
            r.todaySpending += price;
            r.freeHours -= def.hours;
            r.dailyUtility += marginalUtility;
            r.satiety = Mathf.Clamp(r.satiety + def.satiety, 0f, 100f);
            r.happiness = Mathf.Clamp(r.happiness + def.happiness, 0f, 100f);
            r.energy = Mathf.Clamp(r.energy + def.energy, 0f, 100f);
            r.entertainment = Mathf.Clamp(r.entertainment + def.entertainment, 0f, 100f);
            r.household = Mathf.Clamp(r.household + def.household, 0f, 100f);
            IncrementCategory(r, def.category);
            if (def.premium) r.premiumPurchases++; else r.basicPurchases++;

            string reason = BuildChoiceReason(r, def, price, marginalUtility, previous, complement > 1f);
            ResidentChoiceRecord choice = new ResidentChoiceRecord
            {
                day = r.lifeDay, itemId = kind.ToString(), itemName = def.name, category = def.category,
                price = price, utility = r.dailyUtility, marginalUtility = marginalUtility,
                consumerSurplus = surplus, hours = def.hours, reason = reason
            };
            r.todayChoices.Add(choice);
            r.lastChoiceReason = reason;

            if (kind == ResidentItemKind.PlayerJuice)
            {
                r.playerJuiceBought++;
                s.residentPlayerDemandMul = Mathf.Clamp(s.residentPlayerDemandMul + .05f, .55f, 1.5f);
                // 口径说明(C5):居民是独立的代表性账本,这笔消费不直接进门店收入
                // (门店收入按当日市场需求结算,宏观口径也不重复相加)——不写清玩家会以为
                // "自己店里卖出去了、钱却没进账"。
                const string ledgerNote = "（记在居民账本,不直接进门店收入;门店收入按当日市场需求结算）";
                choice.reason += ledgerNote;
                r.lastChoiceReason = choice.reason;
            }
            else if (kind == ResidentItemKind.RivalTea)
            {
                r.rivalTeaBought++;
                s.residentPlayerDemandMul = Mathf.Clamp(s.residentPlayerDemandMul - .04f, .55f, 1.5f);
                s.residentCompetitorDemandMul = Mathf.Clamp(s.residentCompetitorDemandMul + .06f, .55f, 1.5f);
                s.UnlockCard("substitute");
            }

            UnlockConsumptionCards(s, kind, previous, complement > 1f);
            // V0.7 进口记账钩子:收日结算并入贸易账本与关税。
            if (kind == ResidentItemKind.ImportedSnack)
            {
                InternationalTradeService.Ensure(s);
                // 围猎三(#10):累加税前完税基(=外币价×汇率)——SettleDayTrade 以此基征关税,
                // 旧写法累加含税实付价,等于对关税再征一次关税(税上税,25% 关税虚增 25%)。
                s.worldTrade.todayConsumerImportSpend +=
                    price / (1f + InternationalPriceService.TariffRateConsumer(s));
                s.UnlockCard("intl.imports");
            }
            UpdateChapter(s);
            s.Changed();
            return new ResidentActionResult { success = true, message = def.name + "：花 ¥" + price.ToString("0") + "，获得 " + marginalUtility.ToString("0.0") + " 边际效用", choice = choice };
        }

        public static ResidentActionResult Work(GameState s, bool overtime)
        {
            Ensure(s);
            ResidentState r = s.resident;
            float hours = overtime ? 2f : 1f;
            float income = (overtime ? 34f : 15f) * GovernmentSimulator.ResidentIncomeMultiplier(s) *
                SkillIncomeMultiplier(r) * WorkEfficiency(r);
            if (r.reportReady) return Fail("今天已经记账完成");
            if (!overtime && r.gigUnavailable) return Fail("今天的外卖兼职临时取消了，可以选择加班、休息或学习");
            if (overtime && r.energy < 30f) return Fail("精力低于 30，今天无法继续加班；先休息或补充食物");
            if (r.freeHours < hours) return Fail("没有足够自由时间接这份工作");
            r.freeHours -= hours;
            r.cash += income;
            r.todayIncome += income;
            r.workActions++;
            r.energy = Mathf.Clamp(r.energy - (overtime ? 18f : 8f), 0f, 100f);
            r.happiness = Mathf.Clamp(r.happiness - (overtime ? 8f : 3f), 0f, 100f);
            r.lastChoiceReason = "你用 " + hours.ToString("0") + " 小时闲暇换来 ¥" + income.ToString("0") + " 收入；放弃的休息就是机会成本。";
            s.UnlockCard("laborleisure");
            s.UnlockCard("oppcost");
            UpdateChapter(s);
            s.Changed();
            return new ResidentActionResult { success = true, message = "收入 +¥" + income.ToString("0") + "，自由时间 -" + hours.ToString("0") + " 小时" };
        }

        /// <summary>加班兼职(V0.9.1):仅 DSR&gt;40%(房贷高压)时解锁——月供压力进入资源与自由时间的权衡(PRD §193.5)。</summary>
        public static ResidentActionResult SideJob(GameState s)
        {
            Ensure(s);
            ResidentState r = s.resident;
            if (r.reportReady) return Fail("今天已经记账完成");
            if (HousingService.DsrBand(s) < 2) return Fail("只有月供压力偏高(DSR>40%)时才需要这份兼职");
            if (r.energy < 8f) return Fail("精力不足，先休息或补充食物");
            if (r.freeHours < 1f) return Fail("没有足够自由时间接这份工作");
            float income = 15f * GovernmentSimulator.ResidentIncomeMultiplier(s) * SkillIncomeMultiplier(r) * WorkEfficiency(r);
            r.freeHours -= 1f;
            r.cash += income;
            r.todayIncome += income;
            r.workActions++;
            r.energy = Mathf.Clamp(r.energy - 8f, 0f, 100f);
            r.happiness = Mathf.Clamp(r.happiness - 2f, 0f, 100f);
            r.lastChoiceReason = "月供压力大，你用 1 小时闲暇多换 ¥" + income.ToString("0") + "——压力之下的选择也是机会成本。";
            UpdateChapter(s);
            s.Changed();
            return new ResidentActionResult { success = true, message = "兼职收入 +¥" + income.ToString("0") + "，自由时间 -1 小时" };
        }

        public static ResidentActionResult Leisure(GameState s, bool study)
        {
            Ensure(s);
            ResidentState r = s.resident;
            float hours = study ? 2f : 1f;
            if (r.reportReady) return Fail("今天已经记账完成");
            if (r.freeHours < hours) return Fail("没有足够自由时间");
            r.freeHours -= hours;
            r.leisureActions++;
            r.energy = Mathf.Clamp(r.energy + (study ? 4f : 14f), 0f, 100f);
            r.happiness = Mathf.Clamp(r.happiness + (study ? 8f : 10f), 0f, 100f);
            float trainingSubsidy = GovernmentSimulator.PolicyValue(s, PolicyType.TrainingSubsidy);
            float studyBonus = study ? trainingSubsidy * 0.25f : 0f;
            r.dailyUtility += study ? 17f + studyBonus : 13f;
            // bug搜#3:培训补贴走效用通道(studyBonus 进 dailyUtility),不动 dailyIncome——旧代码
            // 此处 += 补贴×0.08 是死写入:EndDay 在发薪前用不含补贴项的公式无条件重算覆写,
            // 加成既不进当天实发也活不过日终,与「眼前收入没增加」的注释/文案相反。
            if (study)
            {
                r.studyActions++;
                r.skillLevel = Mathf.Clamp(r.skillLevel + 2f, 0f, 40f);
            }
            r.lastChoiceReason = study
                ? "你把闲暇投入学习：眼前收入没增加，技能会提高之后的工资。培训补贴让个人收益之外的社会收益更容易实现。"
                : "你保留了闲暇，少赚一点钱，换回精力与幸福感。";
            s.UnlockCard("laborleisure");
            UpdateChapter(s);
            s.Changed();
            return new ResidentActionResult { success = true, message = study ? "学习完成：能力感与效用上升" : "休息完成：精力与幸福感上升" };
        }

        public static ResidentActionResult Save(GameState s, float amount)
        {
            Ensure(s);
            ResidentState r = s.resident;
            amount = Mathf.Max(0f, Mathf.Floor(amount));
            if (r.reportReady) return Fail("今天已经记账完成");
            if (amount <= 0f || r.cash < amount) return Fail("没有这么多现金可以存入");
            // 找 BUG 修复轮回A:savings 是银行对居民的负债镜像,存钱必须双边入账(镜像 DepositResidentCash)。
            // Ensure 先行——银行首初始化会把 householdDeposits 对齐到动作前的储蓄额,再双边加本次金额;
            // 单边写会让两账脱钩,后续双端扣款(租金/首付/还贷)把负债端记负、破 BalanceGap。
            CreditService.Ensure(s);
            r.cash -= amount;
            r.savings += amount;
            r.todaySaved += amount;
            r.savingsActions++;
            s.bank.reserves += amount;
            s.bank.householdDeposits += amount;
            r.lastChoiceReason = "你减少今天可消费的钱，换取未来的购买力和安全感。";
            s.UnlockCard("saving");
            s.UnlockCard("budgetconstraint");
            UpdateChapter(s);
            s.Changed();
            return new ResidentActionResult { success = true, message = "已存入 ¥" + amount.ToString("0") + "，储蓄变为 ¥" + r.savings.ToString("0") };
        }

        public static ResidentDayRecord EndDay(GameState s)
        {
            Ensure(s);
            ResidentState r = s.resident;
            if (r.reportReady && r.history.Count > 0) return r.history[r.history.Count - 1];

            // 日薪先入账再写日报,让 cashEnd/income 含当天日薪,与次日 cashAtDayStart 严格衔接;
            // 「发工资」事件日清晨已提前入账,晚间不再重复发一次。
            if (r.satiety < 25f)
            {
                r.energy = Mathf.Clamp(r.energy - 6f, 0f, 100f);
                r.happiness = Mathf.Clamp(r.happiness - 6f, 0f, 100f);
            }
            r.dailyIncome = r.employedToday
                ? IncomeFor(r.profession) * GovernmentSimulator.ResidentIncomeMultiplier(s) *
                    SkillIncomeMultiplier(r) * WorkEfficiency(r)
                : 0f;
            if (!r.wageAdvanced) { r.cash += r.dailyIncome; r.todayIncome += r.dailyIncome; }
            r.wageAdvanced = false;

            float welfare = GovernmentSimulator.WelfarePerEligibleResident(s);
            if (welfare > 0f && IsWelfareEligible(r))
            {
                r.cash += welfare;
                r.todayIncome += welfare;
                r.happiness = Mathf.Clamp(r.happiness + welfare * 0.05f, 0f, 100f);
            }

            ResidentDayRecord day = new ResidentDayRecord
            {
                day = r.lifeDay,
                cashStart = r.cashAtDayStart,
                cashEnd = r.cash,
                savingsEnd = r.savings,
                income = r.todayIncome,
                spending = r.todaySpending,
                saved = r.todaySaved,
                emergencyWithdrawal = r.todayEmergencyWithdrawal,
                interestIncome = r.todayInterestIncome,
                interestExpense = r.todayInterestExpense,
                totalUtility = r.dailyUtility,
                happinessEnd = r.happiness,
                energyEnd = r.energy,
                satietyEnd = r.satiety,
                entertainmentEnd = r.entertainment,
                freeHoursUsed = r.freeHoursAtDayStart - r.freeHours,
                eventName = r.todayEventName,
                summary = DaySummary(r),
                choices = new List<ResidentChoiceRecord>(r.todayChoices)
            };
            r.history.Add(day);
            r.completedDays++;
            r.reportReady = true;
            // V0.9.1:DSR 25–40% 档日终衰减 −8→−18(月供压力进 24 小时;R2:唯一日终衰减写点)
            float energyDecay = 8f + (HousingService.DsrBand(s) == 1 ? 10f : 0f);
            r.energy = Mathf.Clamp(r.energy - energyDecay, 0f, 100f);
            r.satiety = Mathf.Clamp(r.satiety - 18f, 0f, 100f);
            r.entertainment = Mathf.Clamp(r.entertainment - 8f, 0f, 100f);
            s.UnlockCard("budgetconstraint");
            s.UnlockCard("realpurchasingpower");
            UpdateChapter(s);
            s.Changed();
            return day;
        }

        public static void BeginNextDay(GameState s)
        {
            Ensure(s);
            ResidentState r = s.resident;
            if (!r.reportReady) return;
            r.reportReady = false;
            r.freeHours = DailyFreeHours;
            r.freeHoursAtDayStart = DailyFreeHours;
            r.cashAtDayStart = r.cash;
            r.skillLevelAtDayStart = r.skillLevel;   // bug搜#8:技能随清晨基线快照(旧档缺字段=0)
            r.energyAtDayStart = r.energy;           // bug搜#11:精神状态随清晨基线快照
            r.satietyAtDayStart = r.satiety;
            r.happinessAtDayStart = r.happiness;
            r.entertainmentAtDayStart = r.entertainment;
            r.dailyUtility = 0f;
            r.todaySpending = 0f;
            r.todayIncome = 0f;
            r.todaySaved = 0f;
            r.todayEmergencyWithdrawal = 0f;
            r.todayInterestIncome = 0f;
            r.todayInterestExpense = 0f;
            r.foodCount = r.drinkCount = r.entertainmentCount = r.householdCount = 0;
            r.todayChoices.Clear();
            r.lastChoiceReason = "";
            if (s.residentCinemaDiscountDays > 0) s.residentCinemaDiscountDays--;
            float unemploymentRisk = s.economy == null || s.economy.labor == null
                ? 0f : s.economy.labor.unemploymentRate / 100f;
            bool recession = s.macro != null &&
                (s.macro.cyclePhase == MacroCyclePhase.Recession || s.macro.cyclePhase == MacroCyclePhase.Trough);
            r.employedToday = !recession || DaySimulator.NextRandom01(s) >= unemploymentRisk;
            if (!r.employedToday) r.unemployedDays++;
            if (!r.employedToday && r.cash < 30f && r.savings > 0f)
            {
                // 围猎三复审:强制应急取款也守偿付能力(照 WithdrawResidentDeposit/挤兑口径)——
                // reserves 全库无非负钳位,不钳会把银行储备打穿成负数。
                float withdrawal = Mathf.Min(Mathf.Min(r.savings, 30f - r.cash),
                    Mathf.Max(0f, s.bank.householdDeposits), Mathf.Max(0f, s.bank.reserves));
                r.savings -= withdrawal;
                r.cash += withdrawal;
                r.todayEmergencyWithdrawal = withdrawal;
                // 围猎三(#22):应急取款走双边记账(镜像 CreditService.WithdrawResidentDeposit)——
                // 旧单边写法让 resident.savings 与 bank.householdDeposits 镜像永久脱钩,
                // 居民对已提现的钱继续领利息、挤兑基数虚高。
                s.bank.householdDeposits -= withdrawal;
                s.bank.reserves -= withdrawal;
            }
            ApplyEvent(s);
            UpdateChapter(s);
            s.Changed();
        }

        public static string RunExperiment(GameState s, int experiment)
        {
            Ensure(s);
            ResidentState r = s.resident;
            int bit = 1 << Mathf.Clamp(experiment, 0, 3);
            if ((r.experimentMask & bit) != 0)
                return string.IsNullOrEmpty(r.lastExperimentSummary) ? "这个实验已经完成，可以去曲线页回看结果。" : r.lastExperimentSummary;
            string result;
            switch (experiment)
            {
                case 0:
                    r.cash += 100f;
                    result = "突然多了 ¥100：预算线整体向外移动。你可以买更多商品，但偏好仍决定钱怎么花。";
                    s.UnlockCard("normalgood"); s.UnlockCard("inferiorgood"); s.UnlockCard("incomeeffect"); s.UnlockCard("budgetline");
                    break;
                case 1:
                    float old = PriceService.PlayerListedPrice(s);
                    s.economy.SetTemporaryPlayerPrice(8f, s.day, "居民替代效应实验");
                    float temporary = PriceService.PlayerListedPrice(s);
                    result = "果汁从 ¥" + old.ToString("0") + " 临时涨到 ¥" + temporary.ToString("0") + "：同样预算买得更少，居民会转向 Sunny Tea 与白水；进入下一天后货架价自动恢复。";
                    s.UnlockCard("substitutioneffect"); s.UnlockCard("substitute"); s.UnlockCard("budgetline");
                    break;
                case 2:
                    s.residentCinemaDiscountDays = 2;
                    result = "电影票限时半价：电影消费上升时，爆米花的额外效用也会上升；互补品会一起变化。";
                    s.UnlockCard("complement");
                    break;
                default:
                    result = "把 24 小时重新分配：多工作 2 小时可多赚 ¥34，但会失去休息带来的精力与幸福；没有唯一正确答案。";
                    s.UnlockCard("laborleisure"); s.UnlockCard("oppcost");
                    break;
            }
            r.experimentMask |= bit;
            r.lastExperimentSummary = result;
            UpdateChapter(s);
            s.Changed();
            return result;
        }

        public static string LifeProfile(ResidentState r)
        {
            if (r == null || r.history == null || r.history.Count == 0) return "还没有足够的生活记录。";
            float spend = 0f, utility = 0f, leisure = 0f;
            for (int i = 0; i < r.history.Count; i++)
            {
                spend += r.history[i].spending;
                utility += r.history[i].totalUtility;
                leisure += Mathf.Max(0f, DailyFreeHours - r.history[i].freeHoursUsed);
            }
            float avgSpend = spend / r.history.Count;
            float avgUtility = utility / r.history.Count;
            string style;
            if (r.workActions >= Mathf.Max(5, r.completedDays / 2)) style = "工作狂";
            else if (r.savingsActions >= 4 && r.savings >= 1250f) style = "储蓄达人";
            else if (r.premiumPurchases > r.basicPurchases && avgUtility >= 35f) style = "快乐主义者";
            else if (r.basicPurchases >= Mathf.Max(3, r.premiumPurchases * 2) && avgSpend <= 35f) style = "精打细算者";
            else style = "均衡生活者";
            return style + " · " + r.history.Count + " 天平均消费 ¥" + (spend / r.history.Count).ToString("0") +
                "，平均效用 " + (utility / r.history.Count).ToString("0.0") + "，平均保留闲暇 " + (leisure / r.history.Count).ToString("0.0") + " 小时。";
        }

        public static int CurrentChapter(ResidentState r) { return r == null ? 1 : Mathf.Clamp(r.chapter, 1, 7); }

        public static string ChapterTitle(int chapter)
        {
            string[] titles = { "", "第一章 · 钱不够花", "第二章 · 今晚干什么", "第三章 · 第一杯最好喝",
                "第四章 · 100 元怎么花", "第五章 · 突然涨价", "第六章 · 工资上涨", "第七章 · 到底要不要加班" };
            return titles[Mathf.Clamp(chapter, 1, 7)];
        }

        static void UpdateChapter(GameState s)
        {
            ResidentState r = s.resident;
            int chapter = r.chapter;
            if (chapter == 1 && (r.todayChoices.Count > 0 || r.workActions + r.leisureActions + r.savingsActions > 0)) chapter = 2;
            if (chapter == 2 && (r.foodCount >= 3 || r.drinkCount >= 3 || r.entertainmentCount >= 3 || r.householdCount >= 3)) chapter = 3;
            if (chapter == 3 && r.completedDays >= 1) chapter = 4;
            if (chapter == 4 && ((r.experimentMask & (1 << 1)) != 0 || r.rivalTeaBought > 0)) chapter = 5;
            if (chapter == 5 && (r.experimentMask & (1 << 0)) != 0) chapter = 6;
            if (chapter == 6 && (r.workActions > 0 || r.leisureActions > 0 || (r.experimentMask & (1 << 3)) != 0)) chapter = 7;
            if (chapter > r.chapter) UnlockChapter(s, chapter);
        }

        static void UnlockChapter(GameState s, int chapter)
        {
            ResidentState r = s.resident;
            r.chapter = Mathf.Max(r.chapter, chapter);
            for (int i = 1; i <= r.chapter; i++) r.chapterMask |= 1 << (i - 1);
        }

        static void UnlockConsumptionCards(GameState s, ResidentItemKind kind, int previous, bool complemented)
        {
            s.UnlockCard("utility");
            s.UnlockCard("totalutility");
            s.UnlockCard("marginalutility");
            s.UnlockCard("consumersurplus");
            if (previous > 0) s.UnlockCard("diminishingutility");
            if (complemented) s.UnlockCard("complement");
            if (kind == ResidentItemKind.PlayerJuice || kind == ResidentItemKind.RivalTea) s.UnlockCard("substitute");
            if (s.resident.todayChoices.Count >= 3)
            {
                s.UnlockCard("indifferencecurve");
                s.UnlockCard("optimalbundle");
            }
        }

        static float PreferenceMultiplier(ResidentPreference preference, string category, bool premium)
        {
            if (preference == ResidentPreference.Foodie && category == "食物") return 1.25f;
            if (preference == ResidentPreference.Entertainment && category == "娱乐") return 1.28f;
            if (preference == ResidentPreference.Saver && premium) return .82f;
            if (preference == ResidentPreference.Saver && !premium) return 1.12f;
            return 1f;
        }

        static int CategoryCount(ResidentState r, string category)
        {
            if (category == "食物") return r.foodCount;
            if (category == "饮料") return r.drinkCount;
            if (category == "娱乐") return r.entertainmentCount;
            return r.householdCount;
        }

        static void IncrementCategory(ResidentState r, string category)
        {
            if (category == "食物") r.foodCount++;
            else if (category == "饮料") r.drinkCount++;
            else if (category == "娱乐") r.entertainmentCount++;
            else r.householdCount++;
        }

        static bool HasChoice(ResidentState r, ResidentItemKind kind)
        {
            string id = kind.ToString();
            for (int i = 0; i < r.todayChoices.Count; i++) if (r.todayChoices[i].itemId == id) return true;
            return false;
        }

        static string BuildChoiceReason(ResidentState r, ResidentItemDef def, float price, float mu, int previous, bool complement)
        {
            string reason = "你用 ¥" + price.ToString("0") + " 和 " + def.hours.ToString("0.##") + " 小时换来 " + mu.ToString("0.0") + " 边际效用。";
            if (previous > 0) reason += " 同类消费已经有 " + previous + " 次，所以新增满足感递减。";
            if (complement) reason += " 因为已经看了电影，爆米花作为互补品更有价值。";
            if (r.preference == ResidentPreference.Saver && def.premium) reason += " 这与稳健储蓄偏好有一点冲突，但你仍可选择体验。";
            return reason;
        }

        static string DaySummary(ResidentState r)
        {
            if (r.todayChoices.Count == 0) return "你今天保留了现金和时间；不消费本身也是一种选择。";
            return "你完成 " + r.todayChoices.Count + " 笔消费，花费 ¥" + r.todaySpending.ToString("0") +
                "，获得 " + r.dailyUtility.ToString("0.0") + " 总效用，仍有 " + r.freeHours.ToString("0.##") + " 小时自由时间。";
        }

        static void ApplyEvent(GameState s)
        {
            ResidentState r = s.resident;
            r.gigUnavailable = false;
            r.todayDiscount = 0f;
            int nameLength = string.IsNullOrEmpty(r.residentName) ? 0 : r.residentName.Length;
            int eventIndex = Mathf.Abs(r.lifeDay * 37 + nameLength * 11) % 6;
            switch (eventIndex)
            {
                case 0:
                    r.todayEventName = "发工资";
                    r.todayEventDetail = !r.employedToday ? "受失业影响，本日没有工资到账。" :
                        r.lifeDay == 1 ? "第一笔工资会在完成今天后到账。" : "工资提前到账，今天的预算线向外移动。";
                    // 标记"已提前入账",EndDay 据此不再重复发当天日薪(首日事件不发,晚间照常)。
                    // 口径必须与晚间一致(含技能/效率乘数):此前清晨只乘政策乘数,
                    // 技能 40 满级时发工资日实发比普通日少 40%,dailyIncome 字段也被晚间覆写成另一个值(P2-6)。
                    if (r.lifeDay > 1 && r.employedToday)
                    {
                        r.dailyIncome = IncomeFor(r.profession) * GovernmentSimulator.ResidentIncomeMultiplier(s) *
                            SkillIncomeMultiplier(r) * WorkEfficiency(r);
                        r.cash += r.dailyIncome;
                        r.todayIncome += r.dailyIncome;
                        r.wageAdvanced = true;
                    }
                    break;
                case 1:
                    r.todayEventName = "意外奖金";
                    r.todayEventDetail = r.lifeDay == 1 ? "老板承诺完成首日后发放奖金。" : "获得 ¥30 奖金，可以消费也可以储蓄。";
                    if (r.lifeDay > 1) { r.cash += 30f; r.todayIncome += 30f; }
                    break;
                case 2:
                    r.todayEventName = "失去兼职";
                    r.todayEventDetail = "今天的外卖零工临时取消，少了一种用时间换收入的选择。";
                    r.gigUnavailable = true;
                    break;
                case 3:
                    r.todayEventName = "商家促销";
                    r.todayEventDetail = "商业街全场九折，预算可以买到更多东西。";
                    r.todayDiscount = .1f;
                    break;
                case 4:
                    r.todayEventName = "商品涨价";
                    r.todayEventDetail = "果汁价格临时上涨 ¥3，替代品相对更划算。";
                    s.economy.SetTemporaryPlayerPrice(3f, s.day, "居民随机事件：商品涨价");
                    break;
                default:
                    r.todayEventName = "好友邀请聚餐";
                    r.todayEventDetail = "朋友约你去餐厅；赴约会花钱和时间，拒绝也没有惩罚。";
                    r.entertainment = Mathf.Max(0f, r.entertainment - 5f);
                    break;
            }
        }

        static ResidentActionResult Fail(string message)
        {
            return new ResidentActionResult { success = false, message = message };
        }
    }
}
