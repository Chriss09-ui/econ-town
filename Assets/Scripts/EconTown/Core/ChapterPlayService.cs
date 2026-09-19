using System;
using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public sealed class ChapterPlayPartDefinition
    {
        public readonly int number;
        public readonly LearningPart part;
        public readonly int startChapter;
        public readonly int endChapter;
        public readonly string theme;

        public ChapterPlayPartDefinition(int number, LearningPart part, int startChapter, int endChapter,
            string theme)
        {
            this.number = number;
            this.part = part;
            this.startChapter = startChapter;
            this.endChapter = endChapter;
            this.theme = theme;
        }

        public string Title { get { return ProgressionService.PartTitle(part); } }
    }

    public enum ChapterPlayStartupKind
    {
        None,
        NewStory,
        ContinueStory,
        StartPart,
        ContinuePart
    }

    public struct ChapterPlayStartupResult
    {
        public bool handled;
        public bool success;
        public bool showStartMenu;
        public bool loadedState;
        public bool startedPart;
        public string message;
        public ChapterPlayPartDefinition part;
    }

    /// <summary>
    /// 篇章游玩的单一权威：8 篇定义、稳定篇首世界、场景重载请求与两档共享设置。
    /// 它不新建第四种游戏模式；进入后仍由原有 Progression/WorldSettlement 管线推进。
    /// </summary>
    public static class ChapterPlayService
    {
        const string SharedSettingsKey = "EconTown.SharedSettings.V1";

        static readonly ChapterPlayPartDefinition[] parts =
        {
            Part(1, LearningPart.Enterprise, 1, 5, "开店、需求、成本与利润"),
            Part(2, LearningPart.Market, 6, 10, "份额、差异化、价格战与市场结构"),
            Part(3, LearningPart.Resident, 11, 16, "稀缺、机会成本、预算与劳动—闲暇"),
            Part(4, LearningPart.Government, 17, 22, "公共品、外部性、税收、管制与财政"),
            Part(5, LearningPart.Macro, 23, 30, "GDP、通胀、失业、经济周期与增长"),
            Part(6, LearningPart.Finance, 31, 42, "货币、存贷款、利率、央行与金融稳定"),
            Part(7, LearningPart.Trade, 43, 53, "出口、进口、关税、汇率、资本流动与贸易战"),
            Part(8, LearningPart.Housing, 54, 58, "租房、房贷、房价、泡沫与资产配置")
        };

        static ChapterPlayStartupKind pendingKind;
        static int pendingPartNumber;

        public static IList<ChapterPlayPartDefinition> Parts { get { return parts; } }
        public static bool IsChapterPlay { get { return SaveSystem.IsChapterPlay; } }

        static ChapterPlayPartDefinition Part(int number, LearningPart part, int start, int end, string theme)
        {
            return new ChapterPlayPartDefinition(number, part, start, end, theme);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPendingRequest()
        {
            pendingKind = ChapterPlayStartupKind.None;
            pendingPartNumber = 0;
        }

        public static ChapterPlayPartDefinition GetPart(int number)
        {
            return number >= 1 && number <= parts.Length ? parts[number - 1] : null;
        }

        public static ChapterPlayPartDefinition GetPart(LearningPart part)
        {
            for (int i = 0; i < parts.Length; i++) if (parts[i].part == part) return parts[i];
            return null;
        }

        public static ChapterPlayPartDefinition GetPartForChapter(int chapter)
        {
            for (int i = 0; i < parts.Length; i++)
                if (chapter >= parts[i].startChapter && chapter <= parts[i].endChapter) return parts[i];
            return chapter > parts[parts.Length - 1].endChapter ? parts[parts.Length - 1] : null;
        }

        public static ChapterPlayPartDefinition CurrentPart(GameState state)
        {
            if (state == null || state.progression == null) return null;
            return GetPartForChapter(state.progression.currentChapter);
        }

        public static int EntryPartNumber(GameState state)
        {
            if (state != null && state.chapterPlayEntryPart >= 1 && state.chapterPlayEntryPart <= parts.Length)
                return state.chapterPlayEntryPart;
            ChapterPlayPartDefinition current = CurrentPart(state);
            return current != null ? current.number : 1;
        }

        public static void RequestNewStory()
        {
            pendingKind = ChapterPlayStartupKind.NewStory;
            pendingPartNumber = 0;
        }

        public static void RequestContinueStory()
        {
            pendingKind = ChapterPlayStartupKind.ContinueStory;
            pendingPartNumber = 0;
        }

        public static bool RequestStartPart(int partNumber)
        {
            if (GetPart(partNumber) == null) return false;
            pendingKind = ChapterPlayStartupKind.StartPart;
            pendingPartNumber = partNumber;
            return true;
        }

        public static void RequestContinuePart()
        {
            pendingKind = ChapterPlayStartupKind.ContinuePart;
            pendingPartNumber = 0;
        }

        /// <summary>必须在地图搭建前调用；这样城区解锁和视觉都直接读取目标世界。</summary>
        public static ChapterPlayStartupResult ApplyPendingStartup(GameState state)
        {
            ChapterPlayStartupKind kind = pendingKind;
            int requestedPart = pendingPartNumber;
            pendingKind = ChapterPlayStartupKind.None;
            pendingPartNumber = 0;
            if (kind == ChapterPlayStartupKind.None)
                return new ChapterPlayStartupResult { showStartMenu = true };

            ChapterPlayStartupResult result = new ChapterPlayStartupResult { handled = true };
            if (kind == ChapterPlayStartupKind.NewStory)
            {
                SaveSystem.UseSlot(SaveSlot.Story);
                SaveSystem.Delete(SaveSlot.Story);
                state.chapterPlayEntryPart = 0;
                ApplySharedSettings(state);
                result.success = true;
                result.message = "新的剧情已开始，从第一篇继续前行";
                return result;
            }

            if (kind == ChapterPlayStartupKind.StartPart)
            {
                ChapterPlayPartDefinition part = GetPart(requestedPart);
                if (part == null)
                {
                    result.showStartMenu = true;
                    result.message = "没有找到要开始的篇章";
                    return result;
                }
                SaveSystem.UseSlot(SaveSlot.ChapterPlay);
                SaveSystem.Delete(SaveSlot.ChapterPlay);
                PreparePartStart(state, part);
                ApplySharedSettings(state);
                SaveSystem.Save(state);
                result.success = true;
                result.startedPart = true;
                result.part = part;
                result.message = part.Title + "已准备完成";
                return result;
            }

            SaveSlot slot = kind == ChapterPlayStartupKind.ContinuePart
                ? SaveSlot.ChapterPlay : SaveSlot.Story;
            SaveSystem.UseSlot(slot);
            if (!SaveSystem.Load(state))
            {
                result.showStartMenu = true;
                result.message = slot == SaveSlot.ChapterPlay
                    ? "篇章游玩档无法读取，请重新选择一篇"
                    : "剧情存档无法读取，已保留原文件";
                return result;
            }
            ApplySharedSettings(state);
            result.success = true;
            result.loadedState = true;
            result.part = slot == SaveSlot.ChapterPlay ? CurrentPart(state) : null;
            result.message = slot == SaveSlot.ChapterPlay
                ? "已继续篇章游玩：Day " + state.day
                : "已继续剧情：Day " + state.day;
            return result;
        }

        /// <summary>从确定性标准世界出发，通过现有收日管线生成后期篇需要的真实账本。</summary>
        public static void PreparePartStart(GameState state, ChapterPlayPartDefinition part)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (part == null) throw new ArgumentNullException(nameof(part));

            state.EnsureEconomyState();
            state.money = part.number == 1 ? 2000f : 12000f;
            state.startingMoney = state.money;
            state.materials = part.number == 1 ? 300 : 2000;
            state.inventory = 0;
            state.employees = part.number == 1 ? 1 : 2;
            state.planProduction = 100;
            state.phase = GamePhase.Morning;

            // 预热期停在不会被自然结算触发的最后一章，账本生成后再换成目标篇首。
            state.progression = new ProgressionState
            {
                currentChapter = 58,
                completedChapter = 57,
                chapterStartedWorldDay = state.day,
                notifiedUnlockChapter = 57,
                initialized = true
            };
            ProgressionService.Ensure(state);

            if (part.number >= 4 && !state.resident.profileCreated)
                ResidentEconomy.CreateProfile(state, "篇章镇民", ResidentProfession.StoreClerk,
                    ResidentPreference.Saver);

            GovernmentSimulator.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            FinancialSystemService.Ensure(state);
            InternationalTradeService.Ensure(state);
            HousingService.Ensure(state);

            // 预热只用来生成世界账本，不提前产生第 54 章的「租金已支付」事实。
            state.housing.renting = false;
            SeedStandardWorld(state, part.number);
            int warmupPeriods = part.number == 1 ? 0 : part.number == 2 ? 1 : part.number;
            for (int i = 0; i < warmupPeriods; i++)
            {
                if (state.government.currentEvent != null && !state.government.currentEvent.resolved)
                    GovernmentSimulator.ResolveEvent(state, 2);
                state.phase = GamePhase.Morning;
                CompetitorAI.PrepareDay(state);
                int produced = DaySimulator.Production(state);
                CompetitionSimulator.Settle(state, CompetitionSimulator.RollMarket(state), produced);
                state.phase = GamePhase.Report;
                if (!WorldSettlementService.TrySettleAndAdvance(state, out _, out string blockedReason))
                {
                    Debug.LogWarning("篇章标准世界预热提前结束：" + blockedReason);
                    break;
                }
            }

            state.money = Mathf.Max(state.money, part.number == 1 ? 2000f : 12000f);
            state.startingMoney = state.money;
            state.materials = Mathf.Max(state.materials, part.number == 1 ? 300 : 1200);
            state.inventory = 0;
            state.housing.renting = true;
            state.housing.rentMissed = 0;
            state.housing.homeAppraisal = 0f;
            state.housing.purchasePrice = 0f;
            state.housing.downPaymentPaid = 0f;
            state.housing.mortgageLoanId = "";
            if (part.number == 8) SetResidentSavings(state, 20000f);

            state.competitionIntroduced = part.number >= 3;
            state.priceWarActive = false;
            state.priceWarRound = 0;
            state.todayStrategyChoice = null;
            state.todayCompetitionEventName = null;
            state.exportChannelEnabled = false;
            state.worldTrade.pendingTradeWarChoice = false;
            state.government.currentEvent = null;
            state.todayEventName = null;
            state.todayEventId = null;
            state.demandMul = state.studentMul = state.touristMul = state.materialPriceMul = 1f;
            state.experience.todayLongTermEvent = null;
            state.experience.pendingCrisisReview = null;
            state.phase = GamePhase.Morning;

            ProgressionState target = new ProgressionState
            {
                currentChapter = part.startChapter,
                completedChapter = part.startChapter - 1,
                chapterStartedWorldDay = state.day,
                notifiedUnlockChapter = part.startChapter - 1,
                initialized = true
            };
            for (int i = 0; i < part.startChapter - 1; i++)
                target.signals.Add(ProgressionService.Chapters[i].requiredSignal);
            state.progression = target;
            state.chapterPlayEntryPart = part.number;
            state.currentView = StartView(part.part);
            // ApplyUnlocks 只会单向解锁；预热用的 cc=57 曾打开过这两处，
            // 落目标篇首前必须先按真实前置关回，再让原有解锁规则重算。
            state.economy.market.sunnyTea.isOpen = part.startChapter > 5;
            state.centralBank.unlocked = part.startChapter > 36;
            ProgressionService.Ensure(state);
            KnowledgeSystem.Ensure(state);
            SandboxService.Ensure(state);
            state.Changed();
        }

        static PlayerViewMode StartView(LearningPart part)
        {
            switch (part)
            {
                case LearningPart.Resident:
                case LearningPart.Housing: return PlayerViewMode.Resident;
                case LearningPart.Government: return PlayerViewMode.Government;
                case LearningPart.Macro: return PlayerViewMode.Macro;
                case LearningPart.Finance: return PlayerViewMode.Bank;
                default: return PlayerViewMode.Enterprise;
            }
        }

        static void SeedStandardWorld(GameState state, int partNumber)
        {
            int seed = 918000 + partNumber;
            state.economy.random.Reset(seed);
            state.economy.tradeRandom.Reset(seed ^ 1013904223);
            state.economy.atmosphereRandom.Reset(seed ^ unchecked((int)0x5f3759df));
        }

        static void SetResidentSavings(GameState state, float savings)
        {
            ResidentEconomy.Ensure(state);
            FinancialSystemService.Ensure(state);
            float oldDeposits = Mathf.Max(0f, state.bank.householdDeposits);
            float next = Mathf.Max(0f, savings);
            state.resident.savings = next;
            state.bank.householdDeposits = next;
            state.bank.reserves += next - oldDeposits;
            state.bank.equity = state.bank.TotalAssets - state.bank.TotalLiabilities;
        }

        public static void SaveSharedSettings(GameState state)
        {
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            PlayerPrefs.SetString(SharedSettingsKey, JsonUtility.ToJson(state.experience.settings));
            PlayerPrefs.Save();
        }

        public static void ApplySharedSettings(GameState state)
        {
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            if (!PlayerPrefs.HasKey(SharedSettingsKey))
            {
                SaveSharedSettings(state);
                return;
            }
            try
            {
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SharedSettingsKey), state.experience.settings);
                LongTermExperienceService.Ensure(state);
            }
            catch (Exception e)
            {
                Debug.LogWarning("全局设置无法读取，已保留当前设置：" + e.Message);
                SaveSharedSettings(state);
            }
        }
    }
}
