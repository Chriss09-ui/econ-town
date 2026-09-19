using System;
using UnityEngine;

namespace EconTown
{
    /// <summary>世界分叉引擎(V0.9.4,PRD §197.1):同一结算管线的快照重放,不是另一套引擎
    /// (母版 §187-8 铁律;SandboxService.RunScenario 简化递推零改动零调用,V1.2 退役)。
    /// 纪律:绝不调 DaySimulator.SetRandomSeed/SeedNewGame;fork 运行期抑制 SaveSystem 四写入口;
    /// 单例与四组静态量全程 try/finally 恢复(§197.1-1/2);EditMode headless 同步执行,
    /// Play 可用化(表现层冻结/分帧)留 V1.2。三条随机流是 per-state 字段且随快照入档,往返自动保真。</summary>
    public static class WorldForkService
    {
        static bool running; // 重入闸:实验室/UI 均同步调用,嵌套分叉即编程错误

        /// <summary>主世界字节级快照(BuildSaveData→ToJson)。fork 跑完后再次 Capture 相等,
        /// 即证明主世界零扰动(PRD §197.8 #1 判据)。</summary>
        public static string CaptureJson(GameState source)
        {
            return JsonUtility.ToJson(SaveSystem.BuildSaveData(source), true);
        }

        /// <summary>blocked 起点拒绝(§197.1-4):镜像 WorldSettlementService 五道前置守卫
        /// (文案同源)——终局/营业中/政府事件未决/价格战抉择/财务危机。存在即拒绝分叉并提示,
        /// 否则重放会静默空转或在 fork 内替玩家做关键决策。</summary>
        public static bool CanFork(GameState source, out string reason)
        {
            reason = null;
            if (source == null) { reason = "世界状态尚未初始化"; return false; }
            if (source.phase == GamePhase.Ended) { reason = "本局已结束"; return false; }
            if (source.phase == GamePhase.Business)
            { reason = "营业进行中——先完成或跳过今日营业，再推进到下一天"; return false; }
            if (source.government != null && source.government.currentEvent != null &&
                !source.government.currentEvent.resolved)
            { reason = "先处理当前城市事件，关键公共决策不能被跳过"; return false; }
            if (ProgressionService.RequiresPriceWarChoice(source))
            { reason = "先完成当前章节的价格战选择，关键经营决策不能被跳过"; return false; }
            if (source.economy != null && source.economy.financialCrisisActive &&
                source.experience != null && !source.experience.enterpriseRecovery.active)
            { reason = "连续 3 天资金低于 -1000，先处理财务危机再进入下一天"; return false; }
            return true;
        }

        /// <summary>带起点守卫的便捷入口:先 CanFork(拒绝则不创建任何对象、I 不动、同因返回),
        /// 通过后快照并重放。实验室一律走本入口。</summary>
        public static ForkRunResult RunClonedWorld(GameState source, int periods,
            Action<GameState> inject, Action<GameState, int> onPeriod, Action<GameState> onFinal)
        {
            ForkRunResult refused = new ForkRunResult();
            if (!CanFork(source, out refused.blockedReason)) return refused;
            return RunClonedWorld(CaptureJson(source), periods, inject, onPeriod, onFinal);
        }

        /// <summary>克隆快照并重放 N 期(1 期=1 结算日,住房/宏观期步随收日走)。
        /// inject=ApplyToState 后、第 0 期前(注入利率路径/采期初读数);onPeriod=每期收日成功后
        /// (逐期轨迹采样);onFinal=循环结束后、销毁前(期末读数)。三回调皆可空。
        /// 中途 blocked(价格战抉择/财务危机等):completed=false+blockedReason,onFinal 仍调用
        /// (读数照采,由结果标注未跑完),绝不静默。</summary>
        public static ForkRunResult RunClonedWorld(string snapshotJson, int periods,
            Action<GameState> inject, Action<GameState, int> onPeriod, Action<GameState> onFinal)
        {
            if (running) throw new InvalidOperationException("世界分叉不可嵌套:上一个分叉仍在运行。");
            ForkRunResult result = new ForkRunResult();
            running = true;
            DaySimulator.ForkStatics dayStatics = DaySimulator.BeginForkRun();
            SaveSystem.BeginForkRun();
            GameState main = GameState.I;
            GameState.I = null; // 先置空再 AddComponent:否则 Awake 单例守卫当场销毁 fork(§197.1-1)
            GameObject forkObject = null;
            try
            {
                forkObject = new GameObject("WorldFork");
                GameState fork = forkObject.AddComponent<GameState>();
                // EditMode 的 AddComponent 不跑 Awake(无 ExecuteAlways;V093 SetUp 手动 I=state 同因),
                // Play 模式 Awake 已置 I=fork——两种模式下显式对齐「运行期 I 指向 fork」(§197.1-1)。
                // 子系统无需 Awake 的 Ensure 链:ApplyToState 尾部 Ensure 全跑,收日管线入口亦各自 Ensure。
                GameState.I = fork;
                SaveSystem.SaveData data = JsonUtility.FromJson<SaveSystem.SaveData>(snapshotJson);
                SaveSystem.ApplyToState(fork, data); // 复活=读档语义:回滚到起点日清晨重放
                if (inject != null) inject(fork);
                for (int i = 0; i < periods; i++)
                {
                    string blocked;
                    if (!SettleForkDay(fork, out blocked))
                    {
                        result.blockedReason = "第 " + (i + 1) + " 期收日被拒: " + blocked;
                        break;
                    }
                    result.periodsRun++;
                    if (onPeriod != null) onPeriod(fork, i);
                }
                result.completed = result.periodsRun == periods;
                if (onFinal != null) onFinal(fork);
            }
            finally
            {
                if (forkObject != null) UnityEngine.Object.DestroyImmediate(forkObject); // OnDestroy:I=null
                GameState.I = main; // 显式恢复:不依赖 OnDestroy 置空路径,顺序确定(双保险)
                SaveSystem.EndForkRun();
                DaySimulator.EndForkRun(dayStatics);
                running = false;
            }
            return result;
        }

        /// <summary>fork 内代行玩家一日(V093 SettleOneDay 直译,去掉价格循环——
        /// 「玩家既定售价延续」语义):政府事件自动选选项 1(测试既定先例)→按需补料→
        /// 掷需求/生产/结算→收日。blocked 即中止,由调用方带原因返回。</summary>
        static bool SettleForkDay(GameState fork, out string blockedReason)
        {
            if (fork.government.currentEvent != null && !fork.government.currentEvent.resolved)
                GovernmentSimulator.ResolveEvent(fork, 1);
            if (fork.CanMaterials && fork.materials < 400 && fork.money > 3000f)
            {
                float unit = DaySimulator.MaterialUnitPrice(fork);
                fork.money -= 200 * unit;
                fork.materials += 200;
            }
            // 围猎三(#12):分叉重放与主世界同一分流(镜像 BusinessDirector.Finish)——竞争期掷
            // RollMarket/CompetitionSimulator.Settle,否则对手不生产、品牌冻结、税基缺对手销量,
            // 实验室 A/B 读数系统性失真,违背「同一结算管线的快照重放」铁律(§187-8)。
            // 只掷本模式需要的随机源,不双掷。
            int produced = DaySimulator.Production(fork);
            if (fork.CompetitionActive)
                CompetitionSimulator.Settle(fork, CompetitionSimulator.RollMarket(fork), produced);
            else
                DaySimulator.Settle(fork, DaySimulator.RollDemand(fork), produced);
            fork.phase = GamePhase.Morning;
            if (!WorldSettlementService.TrySettleAndAdvance(fork, out int _, out blockedReason))
                return false;
            // 镜像 GameBootstrap.TryNextDay:竞争期每日推进对手状态。
            if (fork.CompetitionActive) CompetitorAI.PrepareDay(fork);
            return true;
        }
    }

    /// <summary>单次分叉运行结果:completed=是否跑满 periods 期;blockedReason 非空即拒因。</summary>
    public class ForkRunResult
    {
        public bool completed;
        public int periodsRun;
        public string blockedReason;
    }
}
