using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// The Discord Social SDK's Krisp (noise cancellation) native plugins ship with Debug and Release
/// copies of each file, and their .meta files default to "Any Platform" enabled. Left alone, both
/// copies of e.g. discord_krisp.dll are considered compatible with every platform at once, which
/// fails Android (and other) builds with "Cannot include plugin ... since plugin with the same name
/// and architecture was already added".
/// </summary>
public class DiscordKrispPluginFix : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static readonly Dictionary<string, BuildTarget[]> KrispPluginBuildTargets = new() {
        { "discord_krisp.dll",
          new[] { BuildTarget.StandaloneWindows, BuildTarget.StandaloneWindows64 } },
        { "libdiscord_krisp.dylib", new[] { BuildTarget.StandaloneOSX } },
        { "discord_partner_sdk_krisp.aar", new[] { BuildTarget.Android } },
        { "discord_partner_sdk_krisp.framework", new[] { BuildTarget.iOS } },
    };

    private static readonly HashSet<string> EditorCompatiblePlugins = new() {
        "discord_krisp.dll",
        "libdiscord_krisp.dylib",
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        bool isDevelopment = (report.summary.options & BuildOptions.Development) != 0;
        SetPluginConfig(isDevelopment);
    }

    public void OnPostprocessBuild(BuildReport report) { SetPluginConfig(true); }

    private static void SetPluginConfig(bool useDebug)
    {
        const string pluginsRoot = "Packages/com.discord.partnersdk/Runtime/Plugins";
        if (!Directory.Exists(Path.GetFullPath(pluginsRoot)))
        {
            return;
        }

        var importers = PluginImporter.GetAllImporters()
            .Where(p => p.assetPath.StartsWith(pluginsRoot))
            .Where(p => KrispPluginBuildTargets.ContainsKey(Path.GetFileName(p.assetPath)))
            .Where(p => p.assetPath.Contains("/Debug/") || p.assetPath.Contains("/Release/"))
            .ToArray();

        // Disable first so we never have two same-named plugins enabled simultaneously.
        foreach (var importer in importers)
        {
            bool isDebugPlugin = importer.assetPath.Contains("/Debug/");
            bool shouldEnable = (useDebug && isDebugPlugin) || (!useDebug && !isDebugPlugin);
            if (!shouldEnable)
            {
                SetPluginEnabled(importer, false);
            }
        }

        foreach (var importer in importers)
        {
            bool isDebugPlugin = importer.assetPath.Contains("/Debug/");
            bool shouldEnable = (useDebug && isDebugPlugin) || (!useDebug && !isDebugPlugin);
            if (shouldEnable)
            {
                SetPluginEnabled(importer, true);
            }
        }
    }

    private static void SetPluginEnabled(PluginImporter importer, bool enabled)
    {
        bool changed = false;
        string fileName = Path.GetFileName(importer.assetPath);

        // Freshly-added plugins default to "Any Platform" enabled, which overrides
        // the per-platform settings below and causes duplicate-plugin build errors.
        if (importer.GetCompatibleWithAnyPlatform())
        {
            importer.SetCompatibleWithAnyPlatform(false);
            changed = true;
        }

        bool editorTarget = enabled && EditorCompatiblePlugins.Contains(fileName);
        if (importer.GetCompatibleWithEditor() != editorTarget)
        {
            importer.SetCompatibleWithEditor(editorTarget);
            changed = true;
        }

        var supportedSet = new HashSet<BuildTarget>(KrispPluginBuildTargets[fileName]);
        foreach (var target in new[] {
                     BuildTarget.StandaloneWindows,
                     BuildTarget.StandaloneWindows64,
                     BuildTarget.StandaloneLinux64,
                     BuildTarget.StandaloneOSX,
                     BuildTarget.Android,
                     BuildTarget.iOS,
                 })
        {
            bool targetEnabled = enabled && supportedSet.Contains(target);
            if (importer.GetCompatibleWithPlatform(target) != targetEnabled)
            {
                importer.SetCompatibleWithPlatform(target, targetEnabled);
                changed = true;
            }
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }
}
