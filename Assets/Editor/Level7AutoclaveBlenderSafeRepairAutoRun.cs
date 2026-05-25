#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class Level7AutoclaveBlenderSafeRepairAutoRun
{
    private const string EditorPrefsKey = "OLIVIA_Level7_Autoclave_Blender_SafeRepair_20260525_Done";

    static Level7AutoclaveBlenderSafeRepairAutoRun()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (EditorPrefs.GetBool(EditorPrefsKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunOnce;
            return;
        }

        bool repaired = Level7AutoclaveBlenderInstaller.RepairAndInstallFromAutoRunner();
        if (!repaired)
            return;

        EditorPrefs.SetBool(EditorPrefsKey, true);
        Debug.Log("[OLIVIA] Level 7 Autoclave safe repair auto-run complete. Future auto-run disabled.");
    }
}
#endif
