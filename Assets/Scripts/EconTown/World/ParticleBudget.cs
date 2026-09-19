using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.5 收口(PRD §154):粒子并发全局预算的唯一账本。
    /// 各粒子源每帧把在场数上报到此(只读统计,不干预任何系统的行为);
    /// 超出全局上限只在编辑器/开发构建打一条错误日志(真机静默,不做性能开销更大的处理)。
    /// 预算分配:雨 120 + 尘土 30 + 其他(过场雨丝 26 等)50 内调度,全局 200。
    /// </summary>
    public static class ParticleBudget
    {
        public const int GlobalCap = 200;
        public const int RainCap = 120;            // WeatherParticles 雨
        public const int SnowCap = 80;             // WeatherParticles 雪(与雨互斥,不同时在场)
        public const int LeavesCap = 25;           // WeatherParticles 秋叶(互斥)
        public const int CloudsCap = 6;            // WeatherParticles 云团(V0.7.5 装饰阴天,互斥)
        public const int DustCap = 30;             // TownGrowthDirector 施工尘土
        public const int CutsceneRainCap = 26;     // CutsceneDirector 过场雨丝(UI 层)

        static readonly Dictionary<string, int> reported = new Dictionary<string, int>();
        static bool overflowLogged;

        // 围猎三#17:项目关闭 Domain Reload(Enter Play Mode 设置),静态账本会跨 Play 会话残留
        // ——上一场的虚高计数让下一场开场即误报「超出全局上限」。照 UiTheme/PixelArt 等六处先例补复位。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            reported.Clear();
            overflowLogged = false;
        }

        /// <summary>当前全部上报系统在场粒子总数(测试与监控口径)。</summary>
        public static int TotalActive
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in reported) total += pair.Value;
                return total;
            }
        }

        public static int ReportedBy(string system)
        {
            int value;
            return reported.TryGetValue(system, out value) ? value : 0;
        }

        /// <summary>粒子源每帧上报在场数;系统停止时上报 0 清账。</summary>
        public static void Report(string system, int active)
        {
            if (string.IsNullOrEmpty(system)) return;
            active = Mathf.Max(0, active);
            reported[system] = active;
            if (TotalActive > GlobalCap && !overflowLogged)
            {
                overflowLogged = true;
                Debug.LogError("[ParticleBudget] 粒子并发 " + TotalActive + " 超出全局上限 " + GlobalCap +
                               "(§154 预算:雨 " + RainCap + " + 尘土 " + DustCap + " + 其他 50 内)。各系统份额见常量表。");
            }
            else if (TotalActive <= GlobalCap)
            {
                overflowLogged = false;   // 回落后允许再次报告(防止一次越限把日志开关永久锁死)
            }
        }

        /// <summary>测试用:清空账本。</summary>
        public static void ResetForTest()
        {
            reported.Clear();
            overflowLogged = false;
        }
    }
}
