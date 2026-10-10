#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Giana.UnityUtils.Editor
{
    [InitializeOnLoad]
    public static class DependencyDetector
{
    private const string FEEL_SYMBOL = "GIANAUTILS_FEEL";
    private const string DOTWEEN_SYMBOL = "GIANAUTILS_DOTWEEN";
    private const string CHECKED_PREF_KEY = "GianaUtils_DependenciesChecked";

    static DependencyDetector()
    {
        AssemblyReloadEvents.afterAssemblyReload += RunCheckSilently;
        if (!SessionState.GetBool(CHECKED_PREF_KEY, false))
        {
            EditorApplication.delayCall += () => 
            {
                RunCheckSilently();
                SessionState.SetBool(CHECKED_PREF_KEY, true);
            };
        }
    }

    [MenuItem("Tools/GianaUtils/Check Dependencies")]
    public static void RunCheckManual()
    {
        RunCheck(true);
    }

    private static void RunCheckSilently()
    {
        RunCheck(false);
    }

    private static void RunCheck(bool manual)
    {
        bool hasFeel = CheckFeel(manual);
        bool hasDOTween = CheckDOTween(manual);

        bool changed = Sync(FEEL_SYMBOL, hasFeel) | Sync(DOTWEEN_SYMBOL, hasDOTween);
        if (changed || manual)
        {
            Debug.Log($"[GianaUtils] Dependencies verified. Feel: {hasFeel}, DOTween: {hasDOTween}");
        }
    }

    private static bool CheckFeel(bool manual)
    {
        var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
        bool assemblyLoaded = assemblies.Any(a => a.GetName().Name == "MoreMountains.Tools");
        bool asmdefExists = AssetDatabase.FindAssets("MoreMountains.Tools t:asmdef").Length > 0;
        return assemblyLoaded || asmdefExists;
    }

    private static bool CheckDOTween(bool manual)
    {
        var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
        bool hasDOTweenModule = assemblies.Any(a => a.GetName().Name == "DOTween.Modules" || a.GetName().Name == "DOTween");
        
        // Sometimes DOTween is installed without an asmdef in the project. Our package assemblies can't reference it unless it has one.
        bool hasAsmdef = AssetDatabase.FindAssets("DOTween t:asmdef").Length > 0 || AssetDatabase.FindAssets("DOTween.Modules t:asmdef").Length > 0;

        if (hasDOTweenModule && !hasAsmdef)
        {
            if (manual || !SessionState.GetBool("GianaUtils_DOTweenWarningShown", false))
            {
                SessionState.SetBool("GianaUtils_DOTweenWarningShown", true);
                EditorUtility.DisplayDialog(
                    "GianaUtils - DOTween Detected",
                    "DOTween is installed but it is missing its Assembly Definition (asmdef) files.\n\n" +
                    "To use DOTween with GianaUtils, please go to:\n" +
                    "Tools > Demigiant > DOTween Utility Panel > Create ASMDEF",
                    "OK"
                );
                Debug.LogWarning("[GianaUtils] DOTween detected but missing asmdef. Please run: Tools > Demigiant > DOTween Utility Panel > Create ASMDEF");
            }
            return false; // Cannot compile DOTween integration assembly if DOTween itself isn't in an asmdef
        }

        return hasDOTweenModule && hasAsmdef;
    }

    private static bool Sync(string symbol, bool enabled)
    {
        bool anyChanged = false;
        foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
        {
            if (group == BuildTargetGroup.Unknown) continue;
            
            NamedBuildTarget buildTarget;
            try
            {
                buildTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                if (buildTarget.TargetName == "Unknown") continue;
            }
            catch
            {
                continue;
            }

            string raw;
            try 
            { 
                raw = PlayerSettings.GetScriptingDefineSymbols(buildTarget); 
            } 
            catch 
            { 
                continue; 
            }

            var list = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            bool has = list.Contains(symbol);

            if (enabled == has) continue;

            if (enabled) 
            {
                list.Add(symbol); 
            }
            else 
            {
                list.Remove(symbol);
            }
            
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", list));
                anyChanged = true;
            }
            catch { }
        }
        return anyChanged;
    }
}
}
#endif