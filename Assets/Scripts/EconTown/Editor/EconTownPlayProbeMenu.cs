#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace EconTown
{
    /// <summary>Play 探针工具(V0.9.3 后审计补):把存档目录整体重定向到临时目录。
    /// 根因:SaveSystem 的关键节点存档(checkpoint)不受 autoSave 开关管——那是「关自动存档也保
    /// 关键决策」的既定设计;而运行前注入的 OverrideSaveDirectory 会被 ResetDirectoryOverrideOnPlay
    /// 在进 Play 时清掉,所以只关 autoSave 的探针仍会写脏真实 .checkpoint(坑#10 同族)。
    /// 探针正解=进 Play 前用本菜单启用重定向(经 EditorPrefs 传递,活过复位),用完停用。</summary>
    public static class EconTownPlayProbeMenu
    {
        const string PrefKey = "EconTown.PlayProbeDir";

        [MenuItem("EconTown/Play 探针/启用存档重定向(临时目录)")]
        public static void Enable()
        {
            string dir = Path.Combine(Application.temporaryCachePath, "EconTownPlayProbe");
            Directory.CreateDirectory(dir);
            EditorPrefs.SetString(PrefKey, dir);
            Debug.Log("[PlayProbe] 存档重定向已启用 → " + dir + "(真实存档零接触;用完请停用)");
        }

        [MenuItem("EconTown/Play 探针/启用存档重定向(临时目录)", true)]
        public static bool EnableValidate() { return !EditorPrefs.HasKey(PrefKey); }

        [MenuItem("EconTown/Play 探针/停用存档重定向")]
        public static void Disable()
        {
            EditorPrefs.DeleteKey(PrefKey);
            Debug.Log("[PlayProbe] 存档重定向已停用(下次进 Play 恢复真实目录)");
        }

        [MenuItem("EconTown/Play 探针/停用存档重定向", true)]
        public static bool DisableValidate() { return EditorPrefs.HasKey(PrefKey); }
    }
}

#endif
