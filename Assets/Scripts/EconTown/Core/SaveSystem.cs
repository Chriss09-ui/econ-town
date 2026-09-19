using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EconTown
{
    /// <summary>当前世界的存档路由。旧版文件始终属于 Story，篇章游玩使用独立文件组。</summary>
    public enum SaveSlot
    {
        Story = 0,
        ChapterPlay = 1
    }

    /// <summary>基础存档/读档(JSON, persistentDataPath)。</summary>
    public static class SaveSystem
    {
        /// <summary>
        /// 测试/模拟驱动器注入的存档目录(null = 玩家真实目录)。注入后所有读写只落在该目录,
        /// 真实存档零接触——此前 22 个 EditMode 测试与 SimDriver 直接读写玩家真实存档,
        /// 编辑器在 TearDown/Stop 前崩溃即永久丢失进度(P1-5)。使用者必须在结束时恢复为 null。
        /// </summary>
        static string directoryOverride;

        /// <summary>仅供测试与 SimDriver 注入隔离目录;游戏运行代码不得调用。</summary>
        public static void OverrideSaveDirectory(string directory) { directoryOverride = directory; }

        /// <summary>分叉写抑制闸(R4):fork 运行期 Save/SaveKeyMoment/SaveCheckpoint/Delete 四入口
        /// 全部早退——fork 内章节完成会触发 SaveKeyMoment(ProgressionService),覆写的是主档。
        /// 同时快照/恢复 directoryOverride(本身也是不入档的静态量,R10)。</summary>
        static bool forkWriteSuppressed;
        static string forkDirectorySnapshot;
        static SaveSlot forkSlotSnapshot;
        static SaveSlot activeSlot = SaveSlot.Story;

        public static SaveSlot ActiveSlot { get { return activeSlot; } }
        public static bool IsChapterPlay { get { return activeSlot == SaveSlot.ChapterPlay; } }

        /// <summary>切换后续所有存/读/备份/检查点入口的目标档位。</summary>
        public static void UseSlot(SaveSlot slot) { activeSlot = slot; }

        /// <summary>分叉开始(WorldForkService 调用):落写闸+快照目录覆写。</summary>
        public static void BeginForkRun()
        {
            forkDirectorySnapshot = directoryOverride;
            forkSlotSnapshot = activeSlot;
            forkWriteSuppressed = true;
        }

        /// <summary>分叉结束(WorldForkService 在 finally 调用):解闸+恢复目录覆写。</summary>
        public static void EndForkRun()
        {
            forkWriteSuppressed = false;
            directoryOverride = forkDirectorySnapshot;
            forkDirectorySnapshot = null;
            activeSlot = forkSlotSnapshot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDirectoryOverrideOnPlay()
        {
            // 域重载禁用时,上一次 Play 会话(如 SimDriver 中途崩溃)注入的目录会残留成静态字段:
            // 每次进 Play 先复位,保证真实会话永远读写真实目录。
            // 例外:Play 探针模式(菜单「EconTown/Play 探针」写 EditorPrefs)——探针必须在进 Play 前
            // 预置目录,而运行前注入的静态 override 活不过本复位,所以走 EditorPrefs 通道传递
            // (V0.9.3 后审计:探针只关 autoSave 仍会写脏 .checkpoint——关键节点存档不受 autoSave 管,坑#10 同族)。
            activeSlot = SaveSlot.Story;
#if UNITY_EDITOR
            string probeDir = UnityEditor.EditorPrefs.GetString("EconTown.PlayProbeDir", "");
            if (!string.IsNullOrEmpty(probeDir))
            {
                directoryOverride = probeDir;
                return;
            }
#endif
            directoryOverride = null;
        }

        static string SaveDirectory
        {
            get { return string.IsNullOrEmpty(directoryOverride) ? Application.persistentDataPath : directoryOverride; }
        }

        static string FileName(SaveSlot slot)
        {
            return slot == SaveSlot.ChapterPlay ? "econtown_chapter_play.json" : "econtown_save.json";
        }

        public static string SavePathFor(SaveSlot slot) { return Path.Combine(SaveDirectory, FileName(slot)); }
        public static string BackupPathFor(SaveSlot slot) { return SavePathFor(slot) + ".bak"; }
        public static string CheckpointPathFor(SaveSlot slot) { return SavePathFor(slot) + ".checkpoint"; }

        public static string SavePath { get { return SavePathFor(activeSlot); } }
        /// <summary>上一份存档的滚动备份:主档损坏或含非法值时自动回退到这里。</summary>
        public static string BackupPath { get { return SavePath + ".bak"; } }
        public static string CheckpointPath { get { return SavePath + ".checkpoint"; } }
        static string CheckpointTempPath { get { return CheckpointPath + ".tmp"; } }
        static string TempPath { get { return SavePath + ".tmp"; } }

        [Serializable]
        public class SaveData
        {
            // 空 = V0.5.1 旧档(字段完全兼容,直接应用);未来格式变更必须在此按版本迁移,
            // 否则 JsonUtility 会静默忽略未知/缺失字段,玩家进度无声丢失。
            public string saveVersion;
            public int day;
            public EconomyState economy;
            public float money;
            public float price;
            public int planProduction;
            public int employees;
            public int materials;
            public int inventory;
            public float startingMoney;
            public int totalDays;
            public List<DayRecord> history = new List<DayRecord>();
            public List<string> cards = new List<string>();

            public string todayEventName;
            public string todayEventId;
            public float demandMul = 1f;
            public float studentMul = 1f;
            public float touristMul = 1f;
            public float materialPriceMul = 1f;

            public int playerQualityLevel = 1;
            public int playerQualityLevelAtDayStart;   // 0 = 旧档缺省,回滚时跳过(围猎三复审)
            public float playerBrand = 30f;
            public float marketingBudget;
            public float playerMarketingMomentum;
            public float todayStrategicSpend;
            public CompetitorState competitor;
            public bool competitionIntroduced;
            public bool priceWarActive;
            public int priceWarRound;
            public string todayStrategyChoice;
            public string todayCompetitionEventName;
            public float playerAttractionMul = 1f;
            public int marketExperimentMask;
            public PlayerViewMode currentView;
            public ResidentState resident;
            public float residentPlayerDemandMul = 1f;
            public float residentCompetitorDemandMul = 1f;
            public int residentCinemaDiscountDays;
            public GovernmentState government;
            public MacroEconomyState macro;
            public BankState bank;
            public CentralBankState centralBank;
            public HousingState housing;   // V0.9.1:旧档缺字段由 ApplyToState 复活(照 worldTrade 先例)
            public float companyCapitalInvestment;
            public float companyInterestExpense;
            public float pendingFinancedInvestment;
            public int financedEquipmentCapacityBonus;
            public WorldTradeState worldTrade;
            public bool exportChannelEnabled;
            public int exportTargetQuotaCups = 40;
            public ProgressionState progression;
            public KnowledgeProgressState knowledge;
            public SandboxState sandbox;
            public LongTermExperienceState experience;
            public int chapterPlayEntryPart;
        }

        public static bool HasSave() { return File.Exists(SavePath) || File.Exists(BackupPath); }
        public static bool HasSave(SaveSlot slot)
        {
            return File.Exists(SavePathFor(slot)) || File.Exists(BackupPathFor(slot));
        }

        public struct SaveSummary
        {
            public int day;
            public int currentChapter;
            public int completedChapter;
            public bool mainlineComplete;
            public int chapterPlayEntryPart;
        }

        /// <summary>只读取开始页需要的摘要，不切换档位、不修改当前世界。</summary>
        public static bool TryGetSummary(SaveSlot slot, out SaveSummary summary)
        {
            summary = default;
            SaveData data;
            if (!TryReadData(SavePathFor(slot), out data) && !TryReadData(BackupPathFor(slot), out data))
                return false;
            ProgressionState progression = data.progression;
            summary.day = data.day;
            summary.currentChapter = progression != null ? progression.currentChapter : 1;
            summary.completedChapter = progression != null ? progression.completedChapter : 0;
            summary.mainlineComplete = progression != null && progression.mainlineComplete;
            summary.chapterPlayEntryPart = data.chapterPlayEntryPart;
            return true;
        }

        static bool TryReadData(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.day < 1) { data = null; return false; }
                AssertSaveDataFinite(data, "saveSummary");
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        public static void Save(GameState s)
        {
            if (forkWriteSuppressed) return; // 分叉写抑制(R4):fork 状态不得覆写主档
            try
            {
                // 先写临时文件,再用原子替换把旧主档换下成为 .bak——写入中途崩溃/断电不会两头皆失。
                File.WriteAllText(TempPath, JsonUtility.ToJson(BuildSaveData(s), true));
                if (File.Exists(SavePath))
                {
                    try
                    {
                        File.Replace(TempPath, SavePath, BackupPath);
                        return;
                    }
                    catch (Exception)
                    {
                        // 个别平台/文件系统不支持 Replace:降级为 备份→删主档→搬临时档。
                        File.Copy(SavePath, BackupPath, true);
                    }
                }
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(TempPath, SavePath);
            }
            catch (Exception e) { Debug.LogError("存档失败: " + e.Message); }
        }

        /// <summary>状态→存档 DTO(V0.9.4 起为 WorldForkService 快照通道,与存档共用同一字段清单——
        /// 一个世界一本账;注意是浅引用,深拷贝须经 JsonUtility 往返)。行为变更须双跑存档测试。</summary>
        public static SaveData BuildSaveData(GameState s)
        {
            return new SaveData
            {
                saveVersion = GameState.Version,
                day = s.day,
                economy = s.economy,
                money = s.money,
                price = s.price,
                planProduction = s.planProduction,
                employees = s.employees,
                materials = s.materials,
                inventory = s.inventory,
                startingMoney = s.startingMoney,
                totalDays = s.totalDays,
                history = s.history,
                cards = new List<string>(s.unlockedCards),
                todayEventName = s.todayEventName,
                todayEventId = s.todayEventId,
                demandMul = s.demandMul,
                studentMul = s.studentMul,
                touristMul = s.touristMul,
                materialPriceMul = s.materialPriceMul,
                playerQualityLevel = s.playerQualityLevel,
                playerQualityLevelAtDayStart = s.playerQualityLevelAtDayStart,
                playerBrand = s.playerBrand,
                marketingBudget = s.marketingBudget,
                playerMarketingMomentum = s.playerMarketingMomentum,
                todayStrategicSpend = s.todayStrategicSpend,
                competitor = s.competitor,
                competitionIntroduced = s.competitionIntroduced,
                priceWarActive = s.priceWarActive,
                priceWarRound = s.priceWarRound,
                todayStrategyChoice = s.todayStrategyChoice,
                todayCompetitionEventName = s.todayCompetitionEventName,
                playerAttractionMul = s.playerAttractionMul,
                marketExperimentMask = s.marketExperimentMask,
                currentView = s.currentView,
                resident = s.resident,
                residentPlayerDemandMul = s.residentPlayerDemandMul,
                residentCompetitorDemandMul = s.residentCompetitorDemandMul,
                residentCinemaDiscountDays = s.residentCinemaDiscountDays,
                government = s.government,
                macro = s.macro,
                bank = s.bank,
                centralBank = s.centralBank,
                housing = s.housing,
                companyCapitalInvestment = s.companyCapitalInvestment,
                companyInterestExpense = s.companyInterestExpense,
                pendingFinancedInvestment = s.pendingFinancedInvestment,
                financedEquipmentCapacityBonus = s.financedEquipmentCapacityBonus,
                worldTrade = s.worldTrade,
                exportChannelEnabled = s.exportChannelEnabled,
                exportTargetQuotaCups = Mathf.Clamp(s.exportTargetQuotaCups, 0, 90),
                progression = s.progression,
                knowledge = s.knowledge,
                sandbox = s.sandbox,
                experience = s.experience,
                chapterPlayEntryPart = s.chapterPlayEntryPart
            };
        }

        /// <summary>
        /// 关键节点（章节完成/价格战/城市事件/手动保存等）的统一持久化入口，遵守自动存档开关：
        /// 开=写主档（关键决策不因崩溃丢失）+checkpoint 副本；关=只写 checkpoint——
        /// 玩家关闭自动存档的意图是主进度不被静默覆盖，需要时可从关键节点恢复。
        /// </summary>
        public static void SaveKeyMoment(GameState state, string reason)
        {
            if (forkWriteSuppressed) return; // 分叉写抑制(R4):须在一切状态改动之前早退
            if (state == null) return;
            SaveCheckpoint(state, reason);
            if (state.experience.settings.autoSave) Save(state);
        }

        /// <summary>只写 checkpoint 文件，不触碰主档与 .bak（可回滚副本语义）。</summary>
        public static void SaveCheckpoint(GameState state, string reason)
        {
            if (forkWriteSuppressed) return; // 分叉写抑制(R4):须在写 lastCheckpointReason 之前早退
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            state.experience.lastCheckpointReason = reason ?? "关键节点";
            try
            {
                // 围猎三(#12):与主档 Save 同款原子落位——半截 checkpoint 会被「从关键节点恢复」
                // 静默覆盖玩家进度(三条随机流被重置回默认种子),直写在断电/崩溃时正会产生半截档。
                File.WriteAllText(CheckpointTempPath, JsonUtility.ToJson(BuildSaveData(state), true));
                if (File.Exists(CheckpointPath))
                {
                    try { File.Replace(CheckpointTempPath, CheckpointPath, null); return; }
                    catch (Exception) { /* 平台不支持 Replace:降级为 删旧→搬临时档 */ }
                }
                if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
                File.Move(CheckpointTempPath, CheckpointPath);
            }
            catch (Exception e) { Debug.LogWarning("关键节点存档失败: " + e.Message); }
        }

        public static bool HasCheckpoint() { return File.Exists(CheckpointPath); }

        /// <summary>从最近一次关键节点恢复（设置面板入口）。会覆盖当前进度，由调用方确认。</summary>
        public static bool LoadCheckpoint(GameState s)
        {
            if (TryLoad(s, CheckpointPath))
            {
                Debug.Log("已从关键节点恢复：" + (s.experience != null ? s.experience.lastCheckpointReason : ""));
                return true;
            }
            if (File.Exists(CheckpointPath)) Debug.LogError("关键节点存档无法读取。");
            return false;
        }

        /// <summary>把存档数据应用到状态(清晨阶段才允许读取)。主档不可用时自动回退到备份。</summary>
        public static bool Load(GameState s)
        {
            if (TryLoad(s, SavePath)) return true;
            if (TryLoad(s, BackupPath))
            {
                Debug.LogWarning("主存档损坏或含非法数值,已自动从上一份备份恢复。");
                return true;
            }
            if (File.Exists(SavePath) || File.Exists(BackupPath)) Debug.LogError("存档与备份均无法读取。");
            return false;
        }

        static bool TryLoad(GameState s, string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                // 围猎三(#12 复审勘误):实测 Unity 6000.5 的 JsonUtility 对语法非法的截断 JSON
                // (半截写入的必然形态——无闭合大括号)可靠抛 ArgumentException,由下方 catch 拒读;
                // 对「语法完整但缺键」的档则给【默认实例而非 null】(实证:V0.6 模拟档 FromJson 后
                // economy/resident/bank/progression 全部非 null)——即本文件 :465 起各处注释宣称的
                // 「旧档无字段 JsonUtility 留 null」与 ?? new 复活链实际从不触发,旧档的「复活」
                // 靠的一直是默认实例+Ensure 链。半截档的真正防线是 SaveCheckpoint 的原子落位
                // (要么旧完整档要么新完整档);此处保持最小校验(空串→null/垃圾→day<1)。
                if (d == null || d.day < 1) return false;
                ApplyToState(s, d); // 校验失败在赋值前抛出,交由 Load 的回退链尝试下一份。
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>存档 DTO→状态(读档与 V0.9.4 世界分叉复活共用同一实现;含「回滚到起点日清晨」
        /// 重放语义——末条当日记录丢弃)。行为变更须双跑存档测试。</summary>
        public static void ApplyToState(GameState s, SaveData d)
        {
            // 坏档/手改档注入的 NaN/Infinity 经 Mathf.Clamp/Max 拦不住(比较运算对 NaN 恒 false,
            // 原值直通),会顺着价格→利润→就业→GDP 一路污染成全局 NaN。此前只拦 money 两个字段,
            // competitor.price/government.cash/resident.cash/macro.*/history[].profit 全部开放(P2-2)。
            AssertSaveDataFinite(d, "save");
            s.economy = d.economy ?? new EconomyState();
            s.day = d.day;
            // money 允许为负(财务危机设计),但坏档/手改档注入的天文数字要截住。
            s.money = Mathf.Clamp(d.money, -1000000f, 100000000f);
            // 与存档时 UI/模型允许的范围一致,防止坏档或手改档注入非法值(employees=-3、price=0.5 等)。
            s.price = Mathf.Clamp(d.price, 1f, 1000f);
            s.planProduction = Mathf.Clamp(d.planProduction, 0, 200);
            s.employees = Mathf.Clamp(d.employees, 0, Eco.MaxEmployees);
            s.materials = Mathf.Max(0, d.materials);
            s.inventory = Mathf.Max(0, d.inventory);
            s.startingMoney = Mathf.Clamp(d.startingMoney, 0f, 100000000f);
            s.totalDays = Mathf.Max(TimelineConfig.DefaultTotalDays, d.totalDays);
            s.history = d.history != null ? d.history : new List<DayRecord>();
            // 存档可能写于当天已结算之后(营业/报表阶段)。此时末条记录就是今天,
            // 回到清晨重玩会产生同一天两条记录、环比归因全部失真——丢弃它,保持一天一条。
            bool droppedTodayRecord = false;
            float droppedRecordProfit = 0f;
            // 围猎三复审:居民重放回滚需要同向补齐的银行两腿——须等 d.bank 装载后再改
            // (居民回滚块里改 s.bank 是即将被整体覆盖的旧引用,同 #26 陷阱)。
            float replayEmergencyWithdrawal = 0f;
            float replaySaved = 0f;
            if (s.history.Count > 0 && s.history[s.history.Count - 1].day == s.day)
            {
                droppedRecordProfit = s.history[s.history.Count - 1].profit;
                s.history.RemoveAt(s.history.Count - 1);
                droppedTodayRecord = true;
                // 围猎三(#9):重放日的资源基线也要回滚——存档写于当日结算后(Report 阶段)时
                // money/materials/inventory 已含当日增量,不回滚则重放营业把现金流再记一遍、
                // 原料再耗一遍(Report 阶段存档+读档=刷钱;fork 实验室同路径受益)。
                // 基线=被丢日前一条记录的日末值;首日回退新局口径(startingMoney+初始资源)。
                DayRecord prevDay = s.history.Count > 0 ? s.history[s.history.Count - 1] : null;
                if (prevDay != null)
                {
                    s.money = prevDay.moneyEnd;
                    s.materials = Mathf.Max(0, Mathf.RoundToInt(prevDay.materialsEnd));
                    s.inventory = Mathf.Max(0, Mathf.RoundToInt(prevDay.inventoryEnd));
                }
                else
                {
                    s.money = s.startingMoney;
                    s.materials = 300;   // GameState 字段初始化口径(materials=300/inventory=0)
                    s.inventory = 0;
                }
                // 围猎五(#4):存档若写于当日银行合约结算之后(收日链内章节完成的
                // SaveKeyMoment 窗口,晚于 SettleContracts),企业供款的经营现金腿已在上面的
                // 基线回滚里被退回,而银行侧(准备金收款/贷款余额核减)随档保留且同日闸拦住
                // 重扣——这期供款凭空白退(M0 增发)。按当日流量字段把现金腿补扣回来。
                // 复审:不钳 0——money 允许为负(财务危机设计,装载 Clamp 下限 −100 万),
                // 钳位会在「昨日末现金<供款腿」的困难期把整腿吞掉,白退原样回归。
                if (d.bank != null && d.bank.lastContractSettlementDay == d.day &&
                    d.bank.todayCompanyLoanPaymentFromCash > 0f)
                    s.money -= d.bank.todayCompanyLoanPaymentFromCash;
            }
            s.unlockedCards = new HashSet<string>();
            if (d.cards != null) for (int i = 0; i < d.cards.Count; i++) s.unlockedCards.Add(d.cards[i]);
            s.phase = GamePhase.Morning;
            s.todayEventName = d.todayEventName;
            s.todayEventId = d.todayEventId;
            s.demandMul = d.demandMul <= 0f ? 1f : d.demandMul;
            s.studentMul = d.studentMul <= 0f ? 1f : d.studentMul;
            s.touristMul = d.touristMul <= 0f ? 1f : d.touristMul;
            s.materialPriceMul = d.materialPriceMul <= 0f ? 1f : d.materialPriceMul;
            s.playerQualityLevel = Mathf.Clamp(d.playerQualityLevel <= 0 ? 1 : d.playerQualityLevel, 1, 3);
            s.playerQualityLevelAtDayStart = Mathf.Clamp(d.playerQualityLevelAtDayStart, 0, 3);
            s.competitor = d.competitor ?? new CompetitorState();
            s.competitionIntroduced = d.competitionIntroduced ||
                (d.economy != null && d.economy.market != null && d.economy.market.sunnyTea != null &&
                 d.economy.market.sunnyTea.isOpen);
            // 品牌<=0 的兜底只在竞争未引入时生效（此时只可能是坏数据）。
            // 旧写法 d.day < 16 是按天数开竞争时代的残留：V0.5.2 竞争由章节推进开放，
            // 第 16 天前竞争事件把品牌打到 0 后读档会被洗回 30。
            s.playerBrand = d.playerBrand <= 0f && !s.competitionIntroduced ? 30f : Mathf.Clamp(d.playerBrand, 0f, 100f);
            s.marketingBudget = Mathf.Clamp(d.marketingBudget, 0f, 500f);
            s.playerMarketingMomentum = Mathf.Clamp(d.playerMarketingMomentum, 0f, 4f);
            s.todayStrategicSpend = Mathf.Max(0f, d.todayStrategicSpend);
            s.priceWarActive = d.priceWarActive;
            s.priceWarRound = d.priceWarRound;
            s.todayStrategyChoice = d.todayStrategyChoice;
            s.todayCompetitionEventName = d.todayCompetitionEventName;
            s.playerAttractionMul = d.playerAttractionMul <= 0f ? 1f : d.playerAttractionMul;
            s.marketExperimentMask = d.marketExperimentMask;
            s.currentView = d.currentView;
            s.resident = d.resident ?? new ResidentState();
            // 居民侧同理:当天已完成的记录要丢弃,并把当日累计值复位到清晨,
            // 否则 reportReady 短路会让重玩日的选择被静默丢弃(ResidentEconomy.EndDay)。
            bool residentReplay = s.resident.history != null && s.resident.history.Count > 0 &&
                s.resident.history[s.resident.history.Count - 1].day == s.resident.lifeDay;
            if (residentReplay)
            {
                ResidentDayRecord droppedResidentDay = s.resident.history[s.resident.history.Count - 1];
                // 围猎三(#8):重玩日回滚不能只删记录——EndDay 已落账的日薪/福利留在现金里,
                // 重玩再收日时 EndDay 二次发薪(「完成今日→存档→读档」循环可无限刷日薪)。
                // 现金回到当日 cashStart(BeginNextDay 在应急取款之前快照);应急取款原路退回储蓄;
                // 白天「存一笔钱」同步撤回(savings−saved)——现金退回+存款保留=凭空增钱(复审补)。
                s.resident.cash = droppedResidentDay.cashStart;
                // 围猎五(#4):居民供款的现金垫付腿同病——cashStart 快照在合约结算之前,
                // 回滚会退回垫付而银行侧保留收款,按当日流量补扣(银行已结算窗口才需要)。
                // 复审:与企业腿同口径直接减不钳位——钳 0 会吞腿(白退回归);负 cash 虽是
                // 运行时不可能态,但次日应急取款公式 Min(savings,30−cash) 以储蓄封顶,
                // 搬运总量守恒,只是单日展示噪声,两害相权取守恒。
                if (d.bank != null && d.bank.lastContractSettlementDay == d.day &&
                    d.bank.todayResidentLoanPaymentFromCash > 0f)
                    s.resident.cash -= d.bank.todayResidentLoanPaymentFromCash;
                s.resident.savings += droppedResidentDay.emergencyWithdrawal;
                s.resident.savings -= droppedResidentDay.saved;
                replayEmergencyWithdrawal = droppedResidentDay.emergencyWithdrawal;
                replaySaved = droppedResidentDay.saved;
                s.resident.history.RemoveAt(s.resident.history.Count - 1);
                s.resident.completedDays = Mathf.Max(0, s.resident.completedDays - 1);
                s.resident.reportReady = false;
                s.resident.freeHours = ResidentEconomy.DailyFreeHours;
                s.resident.freeHoursAtDayStart = ResidentEconomy.DailyFreeHours;
                s.resident.cashAtDayStart = droppedResidentDay.cashStart;
                // bug搜#8:技能是当日行动的持久产物,与现金同回滚到清晨快照——否则「学习→完成今日
                // →checkpoint→恢复」每轮白拿 2 点(封顶 40 即日薪乘数永久虚高 40%,与已修的刷日薪同族)。
                // 复审:快照 0(首日/未学过)是合法清晨值,也要回滚——守卫用 >=0f,-1 哨兵
                // (旧档缺字段读出 -1)才跳过。
                if (s.resident.skillLevelAtDayStart >= 0f) s.resident.skillLevel = s.resident.skillLevelAtDayStart;
                // bug搜#11:精神状态同回滚到清晨快照(-1 哨兵=旧档跳过)——存档写于 EndDay 之后时
                // 四字段已含当日消费影响+日终衰减,不回滚则重放日从昨日日末状态开局。
                if (s.resident.energyAtDayStart >= 0f) s.resident.energy = s.resident.energyAtDayStart;
                if (s.resident.satietyAtDayStart >= 0f) s.resident.satiety = s.resident.satietyAtDayStart;
                if (s.resident.happinessAtDayStart >= 0f) s.resident.happiness = s.resident.happinessAtDayStart;
                if (s.resident.entertainmentAtDayStart >= 0f)
                    s.resident.entertainment = s.resident.entertainmentAtDayStart;
                s.resident.dailyUtility = 0f;
                s.resident.todaySpending = 0f;
                s.resident.todayIncome = 0f;
                s.resident.todaySaved = 0f;
                s.resident.todayEmergencyWithdrawal = 0f;
                s.resident.todayInterestIncome = 0f;
                s.resident.todayInterestExpense = 0f;
                // 围猎三复审(cross-pipeline):cashStart 是清晨事件入账【前】的快照(BeginNextDay
                // 先快照再 ApplyEvent)——回滚若不按确定性事件号补回「发工资/意外奖金」两笔清晨
                // 现金流,重放日 EndDay 会被 wageAdvanced 拦住不补发,日薪/奖金从世界消失
                // (与「刷日薪」相反方向的零发放)。todayIncome 刚被清零,此处一并补回。
                int nameLength = string.IsNullOrEmpty(s.resident.residentName) ? 0 : s.resident.residentName.Length;
                int eventIndex = Mathf.Abs(s.resident.lifeDay * 37 + nameLength * 11) % 6;
                if (eventIndex == 0 && s.resident.lifeDay > 1 && s.resident.employedToday)
                {
                    // 与 ApplyEvent case0 同口径:dailyIncome 已从存档恢复为清晨事件的实发值。
                    s.resident.cash += s.resident.dailyIncome;
                    s.resident.todayIncome += s.resident.dailyIncome;
                    s.resident.wageAdvanced = true;
                }
                else if (eventIndex == 1 && s.resident.lifeDay > 1)
                {
                    s.resident.cash += 30f;
                    s.resident.todayIncome += 30f;
                }
                // bug搜#11复审:case5「好友聚餐」的精神腿同口径补偿——快照取在 ApplyEvent 之前,
                // 现金腿补了「事件后」,entertainment 不补则回滚到「事件前」,聚餐日存读一次净白拿 5 点。
                else if (eventIndex == 5)
                    s.resident.entertainment = Mathf.Max(0f, s.resident.entertainment - 5f);
                else s.resident.wageAdvanced = false;
                s.resident.foodCount = s.resident.drinkCount = s.resident.entertainmentCount = s.resident.householdCount = 0;
                s.resident.todayChoices.Clear();
            }
            s.residentPlayerDemandMul = d.residentPlayerDemandMul <= 0f ? 1f : Mathf.Clamp(d.residentPlayerDemandMul, .55f, 1.5f);
            s.residentCompetitorDemandMul = d.residentCompetitorDemandMul <= 0f ? 1f : Mathf.Clamp(d.residentCompetitorDemandMul, .55f, 1.5f);
            s.residentCinemaDiscountDays = Mathf.Max(0, d.residentCinemaDiscountDays);
            s.government = d.government ?? new GovernmentState();
            if (droppedTodayRecord)
            {
                // 围猎五(#3):存档若写于当日政府周期结算之后(收日链内章节完成的
                // SaveKeyMoment 窗口,晚于 AdvanceCycle),政府 history 末条 cycle==day,
                // 重放日 AdvanceCycle 被同日闸(Budget.AdvanceCycle 入口)早退——此时再复位
                // 防重标记/回挂造价,重放日新挂的账无人消费、滞留到次日周期双计(同笔税入
                // 两次/造价扣两次)。已结算分支整体冻结:标记与 pending 保持存档值(=结算后
                // 状态:标记=当天、pending=0),重放日的税被防重标记(RecordDrinkSettlement
                // 同日 return)拦住不重挂——政府已按存档前的原时间线收过当日税,重放时间线
                // 不再重复收(政府不重结算,与「合同不回滚则贷款不撤」同口径对称)。
                bool governmentSettled = s.government.history.Count > 0 &&
                    s.government.history[s.government.history.Count - 1].cycle == s.day;
                if (!governmentSettled)
                {
                    // 重玩该日时税/补贴基要重新入账:同日防重标记一并复位,否则重放的结算会漏记账本。
                    s.government.lastDrinkTaxRecordedDay = 0;
                    s.government.lastAdditionalSalesDay = 0;
                    // 已挂账未进周期账本的税费/补贴也必须清零:存档若写于当日结算之后(Report 阶段),
                    // 重放当日结算会把这批 pending 再累加一遍,日末 AdvanceCycle 按 2× 记进政府账本(P1-2)。
                    s.government.pendingDrinkTaxRevenue = 0f;
                    s.government.pendingPollutionTaxRevenue = 0f;
                    s.government.pendingTariffRevenue = 0f;
                    s.government.lastTariffRecordedDay = 0;
                    s.government.pendingSubsidySpending = 0f;
                    // bug搜#5复审:资本挂账不能无条件清——BuildProject 已删即时扣款,这是造价唯一落账
                    // 途径,清零=路灯白拿(0×扣款)。清后只回挂「本周期建成项目」的造价:事件/长经的
                    // 资本支出不回挂(重放日可重选会重新挂账,回挂反而双计),裸挂账同样不回挂。
                    s.government.pendingCapitalSpending = 0f;
                    for (int projectIdx = 0; projectIdx < s.government.projects.Count; projectIdx++)
                    {
                        PublicProjectState p = s.government.projects[projectIdx];
                        if (p != null && p.built && p.builtCycle == s.government.cycle)
                            s.government.pendingCapitalSpending += p.buildCost;
                    }
                }
            }
            GovernmentSimulator.Ensure(s);
            s.macro = d.macro ?? new MacroEconomyState();
            MacroEconomySimulator.Ensure(s);
            ResidentEconomy.Ensure(s);
            s.bank = d.bank ?? new BankState();
            // 围猎三复审(#8 补账):居民重放回滚的银行两腿——应急取款退回储蓄(+W)、当日存款
            // 撤回(−Y)时,镜像 householdDeposits/reserves 同向补齐,保住 savings≡householdDeposits。
            if (residentReplay)
            {
                if (replayEmergencyWithdrawal > 0f)
                {
                    s.bank.householdDeposits += replayEmergencyWithdrawal;
                    s.bank.reserves += replayEmergencyWithdrawal;
                }
                if (replaySaved > 0f)
                {
                    s.bank.householdDeposits -= replaySaved;
                    s.bank.reserves -= replaySaved;
                }
            }
            // 围猎三复审(cross-mirror):重放日的「效果持久」入口随基线一并回滚——必须放在
            // d.bank/playerQualityLevel 装载之后(写在上面的回滚块里会撞 #26 旧引用/被后续
            // 装载覆盖)。①品质升级:白天单边扣款+等级跨日持久,「升级→当日章节 checkpoint→
            // 恢复」每轮白拿一级(cap 3);退回当日清晨快照(旧档无快照字段=0,跳过维持旧行为)。
            if (droppedTodayRecord && s.playerQualityLevelAtDayStart >= 1 &&
                s.playerQualityLevelAtDayStart <= s.playerQualityLevel)
                s.playerQualityLevel = s.playerQualityLevelAtDayStart;
            // ②当日新发贷款:钱端取现/花费已被 money 基线回滚覆盖,债端不撤则重玩日再贷
            // 即双重负债。贷款创造=loan(资产)+deposit(负债)双边,撤销同双边,BalanceGap 不动。
            // 镇买方按揭与在途房款同口径不撤(卖房合同不在重放撤销范围,对称性保持)。
            // bug搜#9:居民贷款的当日动作随居民单侧重放同样要撤(清晨贷→完成居民日→恢复→
            // 同日再贷=双重负债),条件与 :541 的 todayConsumerImportSpend 同口径;企业贷的
            // 钱端(companyDeposits/企业 money)只在企业侧重放时回基线,绑定 droppedTodayRecord。
            if (droppedTodayRecord || residentReplay)
            {
                for (int loanIdx = s.bank.loans.Count - 1; loanIdx >= 0; loanIdx--)
                {
                    LoanRecord loan = s.bank.loans[loanIdx];
                    if (loan == null || loan.status != LoanStatus.Current ||
                        loan.originatedWorldDay != s.day) continue;
                    if (loan.borrowerId == MortgageService.TownBuyerId) continue;
                    if (loan.borrowerType != LoanBorrowerType.Resident && !droppedTodayRecord) continue;
                    if (loan.borrowerType == LoanBorrowerType.Resident)
                    {
                        // bug搜#2:玩家按揭同口径豁免(镇买方已在上面 continue)——按揭放款腿记在
                        // 镇住户池(MortgageService 首付与放款都进 townSectorDeposits,本金从未进
                        // 居民储蓄/存款),按消费贷口径撤会把本金从居民储蓄再扣一次;且购房合同
                        // (租住状态/首付/mortgageLoanId)不在重放撤销范围,撤贷会让住房仍「已购」
                        // 而月供凭空消失、贷款 id 悬空——「合同不回滚则贷款不撤」的对称性保持。
                        if (loan.purpose == LoanPurpose.Mortgage) continue;
                        s.bank.householdDeposits -= loan.principal;
                        s.resident.savings = Mathf.Max(0f, s.resident.savings - loan.principal);
                    }
                    else s.bank.companyDeposits -= loan.principal;
                    s.bank.loans.RemoveAt(loanIdx);
                }
            }
            s.centralBank = d.centralBank ?? new CentralBankState();
            s.companyCapitalInvestment = Mathf.Max(0f, d.companyCapitalInvestment);
            s.companyInterestExpense = Mathf.Max(0f, d.companyInterestExpense);
            s.todayCompanyInterestExpense = 0f;
            s.pendingFinancedInvestment = Mathf.Max(0f, d.pendingFinancedInvestment);
            s.financedEquipmentCapacityBonus = Mathf.Clamp(d.financedEquipmentCapacityBonus, 0, 300);
            FinancialSystemService.Ensure(s);
            // V0.7:旧档无 worldTrade 字段(JsonUtility 留 null),必须复活并重跑 Ensure 链。
            s.worldTrade = d.worldTrade ?? new WorldTradeState();
            // 围猎三(#26):当日居民进口累计的复位必须清在「来自存档的」worldTrade 上——旧写法
            // 清在即将被上一行覆盖的旧引用上永远落空;且复位条件与重玩语义对齐(企业或居民任一侧
            // 回到清晨都算重放,残留会让重放日的贸易行把旧进口再计一遍、关税虚记)。
            if (droppedTodayRecord || residentReplay) s.worldTrade.todayConsumerImportSpend = 0f;
            s.exportChannelEnabled = d.exportChannelEnabled;
            // bug搜#10:0 是合法配额(UI 可减到 0=暂停出口,结算按 0 杯出口)——`<=0?40` 把玩家
            // 显式设置的 0 洗回默认 40,重启后违背意图出口;旧档缺字段由 SaveData 字段初始化器
            // (=40) 兜底,无需三目。
            s.exportTargetQuotaCups = Mathf.Clamp(d.exportTargetQuotaCups, 0, 90);
            InternationalTradeService.Ensure(s);
            // V0.9.1:旧档无 housing 字段(JsonUtility 留 null),复活并重跑 Ensure(照 worldTrade 先例)。
            s.housing = d.housing ?? new HousingState();
            HousingService.Ensure(s);
            s.progression = d.progression ?? new ProgressionState();
            ProgressionService.Ensure(s);
            s.knowledge = d.knowledge ?? new KnowledgeProgressState();
            KnowledgeSystem.Ensure(s);
            // 找BUG围猎盲区2:V0.9.5 卡解锁挂动作时点,旧档按既有状态幂等回填(修复轮回)。
            HousingService.BackfillHousingCards(s);
            s.sandbox = d.sandbox ?? new SandboxState();
            SandboxService.Ensure(s);
            s.experience = d.experience ?? new LongTermExperienceState();
            LongTermExperienceService.Ensure(s);
            s.chapterPlayEntryPart = Mathf.Clamp(d.chapterPlayEntryPart, 0, 8);
            // 丢弃当日记录后，两个按"同一结算日只计一次"防重的 streak 也要回滚：
            // 被丢弃的记录若贡献过 +1 则退回，并把 lastDay 清零允许重玩日重新判定
            // （否则重玩亏损日 streak 不清零、重玩盈利日双记，伪造"连续 N 日"达成）。
            if (droppedTodayRecord)
            {
                if (droppedRecordProfit > 0f)
                {
                    s.progression.positiveProfitStreak = Mathf.Max(0, s.progression.positiveProfitStreak - 1);
                    s.experience.enterpriseRecovery.positiveCashFlowStreak =
                        Mathf.Max(0, s.experience.enterpriseRecovery.positiveCashFlowStreak - 1);
                }
                s.progression.positiveProfitStreakLastDay = 0;
                s.experience.enterpriseRecovery.positiveCashFlowStreakLastDay = 0;
                // 围猎三(#20):财务健康的同日防重闸一并复位——存档写于 Report 阶段时该闸已=当日,
                // 重玩日营业结算会被旧闸短路,financialDistressDays 的清零/累加判定整日丢失。
                s.economy.financialHealthLastWorldDay = 0;
            }
            WorldClock.SyncDerivedPeriods(s);
            DaySimulator.LastEvent = null;
            // 首选稳定 id 重建(文案改名免疫);旧档无 id 时回退按中文名匹配(C3)。
            if (!string.IsNullOrEmpty(s.todayEventId))
                for (int i = 0; i < TownEvents.All.Length; i++)
                    if (TownEvents.All[i].id == s.todayEventId) { DaySimulator.LastEvent = TownEvents.All[i]; break; }
            if (DaySimulator.LastEvent == null && !string.IsNullOrEmpty(s.todayEventName))
                for (int i = 0; i < TownEvents.All.Length; i++)
                    if (TownEvents.All[i].name == s.todayEventName) { DaySimulator.LastEvent = TownEvents.All[i]; break; }
            s.Changed();
        }

        /// <summary>
        /// 递归校验存档数据里的全部 float 字段均为有限值;发现非有限值抛 InvalidDataException,
        /// 交由 TryLoad 捕获后走备份回退链。只遍历公共字段——JsonUtility 本就只反序列化公共字段。
        /// </summary>
        static void AssertSaveDataFinite(object value, string path)
        {
            if (value == null) return;
            Type type = value.GetType();
            if (type == typeof(string) || type.IsEnum) return;
            if (type == typeof(float))
            {
                if (!float.IsFinite((float)value))
                    throw new InvalidDataException("存档字段 " + path + " 数值非法(NaN/Infinity)。");
                return;
            }
            if (type.IsPrimitive) return;
            if (value is System.Collections.IEnumerable enumerable)
            {
                int index = 0;
                foreach (object item in enumerable)
                {
                    AssertSaveDataFinite(item, path + "[" + index + "]");
                    index++;
                }
                return;
            }
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
                AssertSaveDataFinite(fields[i].GetValue(value), path + "." + fields[i].Name);
        }

        public static void Delete()
        {
            Delete(activeSlot);
        }

        public static void Delete(SaveSlot slot)
        {
            if (forkWriteSuppressed) return; // 分叉写抑制(R4):fork 不得销毁主档
            try
            {
                // 备份与临时档一并清理,否则残留会让 HasSave() 误判"还有进度"。
                // 围猎三(#12 复审):checkpoint 临时档同入清单,否则「重新开始」后孤儿残留。
                string save = SavePathFor(slot);
                string backup = BackupPathFor(slot);
                string checkpoint = CheckpointPathFor(slot);
                if (File.Exists(save)) File.Delete(save);
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(checkpoint)) File.Delete(checkpoint);
                if (File.Exists(save + ".tmp")) File.Delete(save + ".tmp");
                if (File.Exists(checkpoint + ".tmp")) File.Delete(checkpoint + ".tmp");
            }
            catch (Exception) { }
        }
    }
}
