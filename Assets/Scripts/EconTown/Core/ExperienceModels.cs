using System;
using System.Collections.Generic;

namespace EconTown
{
    public enum DifficultyMode { Teaching, Standard, Simulation }
    public enum GuidanceLevel { Detailed, Standard, Concise }
    public enum NewsCategory { Enterprise, Resident, Government, Macro, Special, International }

    [Serializable]
    public class WorldExperienceSettings
    {
        public DifficultyMode difficulty = DifficultyMode.Standard;
        public GuidanceLevel guidance = GuidanceLevel.Standard;
        public float eventFrequency = 1f;
        public float competitionIntensity = 1f;
        public float marketScale = 1f;
        public bool chartAutoScale = true;
        public bool autoSave = true;
        // V0.7.1 呈现层双开关(默认开,随存档走;旧档缺字段由声明默认值补齐)。
        // 氛围效果=时段色调/季节/天气粒子/演出；经济状态可视化=街景人数/橱窗灯光/价目牌——
        // 它们是经济状态的仪表,关闭后街景不再反映就业与企业情绪(设置面板描述须写明)。
        public bool atmosphereEffects = true;
        public bool ecoVisibility = true;
        // V0.7.2 港口演出：true=首次靠港/档位变化播放完整驶入，false=永远直接停泊。
        public bool fullPortAnimations = true;
        public bool performanceTipShown;  // §119-4 帧率偏低一次性提示标记(每存档只提示一次)
        public string numericUnit = "自动";
    }

    [Serializable]
    public class PortPresentationState
    {
        public bool hasSeenArrival;
        public int lastArrivalTier = -1;
        public int lastArrivalConsideredWorldDay;
    }

    [Serializable]
    public class EventCooldownState
    {
        public string eventId;
        public int nextEligibleWorldDay;
    }

    [Serializable]
    public class LongTermEventRecord
    {
        public string id;
        public int worldDay;
        public NewsCategory category;
        public string title;
        public string cause;
        public string effect;
        public string viewTarget;
    }

    [Serializable]
    public class EconomicNewsItem
    {
        public string id;
        public int worldDay;
        public NewsCategory category;
        public string headline;
        public string detail;
        public string cause;
        public string viewTarget;
    }

    [Serializable]
    public class EconomicHistoryEntry
    {
        public string id;
        public int worldDay;
        public string title;
        public string summary;
    }

    [Serializable]
    public class EnterpriseRecoveryState
    {
        public bool active;
        public int startedWorldDay;
        public int positiveCashFlowStreak;
        // streak 最近计数依据的结算日：同一 DayRecord 不得重复 +1（读档重放日防双记，
        // 与 FinancialHealthService.financialHealthLastWorldDay 同一模式）。
        public int positiveCashFlowStreakLastDay;
        public string cause;
        public string selectedPlan;
        public List<string> actions = new List<string>();
        public string latestStatus;
    }

    [Serializable]
    public class CrisisReviewRecord
    {
        public string id;
        public string kind;
        public int startedWorldDay;
        public int recoveredWorldDay;
        public string cause;
        public string recovery;
        public string result;
    }

    [Serializable]
    public class LongTermExperienceState
    {
        public WorldExperienceSettings settings = new WorldExperienceSettings();
        public PortPresentationState portPresentation = new PortPresentationState();
        public List<EventCooldownState> eventCooldowns = new List<EventCooldownState>();
        public List<LongTermEventRecord> eventHistory = new List<LongTermEventRecord>();
        public List<EconomicNewsItem> news = new List<EconomicNewsItem>();
        public List<EconomicHistoryEntry> economicHistory = new List<EconomicHistoryEntry>();
        public EnterpriseRecoveryState enterpriseRecovery = new EnterpriseRecoveryState();
        public List<CrisisReviewRecord> crisisReviews = new List<CrisisReviewRecord>();
        public CrisisReviewRecord pendingCrisisReview;
        public LongTermEventRecord todayLongTermEvent;
        public int lastLongTermEventWorldDay;
        public int totalLongTermEvents;
        public bool residentCrisisActive;
        public bool fiscalPressureActive;
        public string macroCrisisStatus = "正常";
        public int recessionStreak;
        public int recoveryStreak;
        public string lastCheckpointReason;
        // V0.7.4 过场一次性标记与首播档案(PRD §150):看过即写入、永不重播;
        // 严禁用 SaveKeyMoment 充当标记(覆盖式快照会污染恢复点)。
        public List<int> viewedCutscenes = new List<int>();
        public List<CutsceneRecord> cutsceneRecords = new List<CutsceneRecord>();
    }
}
