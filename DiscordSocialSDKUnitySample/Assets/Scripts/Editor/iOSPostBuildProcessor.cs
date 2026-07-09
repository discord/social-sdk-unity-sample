#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Configures the generated iOS Info.plist so the Discord Social SDK's authentication deep links
/// work, writing two keys:
///
/// - <c>CFBundleURLTypes</c>: registers the custom URI scheme <c>discord-{ApplicationId}</c> so the
///   OAuth redirect (<c>discord-{ApplicationId}:/authorize/callback</c>) can return to the app after
///   <c>Client.Authorize()</c>
/// - <c>LSApplicationQueriesSchemes</c>: registers the <c>discord</c> scheme so the app can detect
///   the installed Discord app and deep link into it for authentication
///
/// This runs after Unity generates the Xcode project, so the Info.plist is available to edit. The
/// Application ID is read from the existing <see cref="DiscordSocialSDKConfig"/> asset, so no manual
/// Info.plist editing is required.
/// </summary>
public class iOSPostBuildProcessor : IPostprocessBuildWithReport
{
    const string UrlTypesKey = "CFBundleURLTypes";
    const string UrlSchemesKey = "CFBundleURLSchemes";
    const string QueriesSchemesKey = "LSApplicationQueriesSchemes";
    const string DiscordAppScheme = "discord";

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            return;
        }

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // The custom scheme the OAuth callback redirects back to.
        RegisterUrlScheme(plist.root, $"discord-{GetDiscordApplicationId()}");

        // The scheme the app queries to detect and deep link into the installed Discord app.
        RegisterQueryScheme(plist.root, DiscordAppScheme);

        plist.WriteToFile(plistPath);
    }

    // Adds a scheme under CFBundleURLTypes if it isn't already registered, so repeated builds stay idempotent.
    private void RegisterUrlScheme(PlistElementDict root, string scheme)
    {
        var urlTypes = root.values.ContainsKey(UrlTypesKey)
            ? root[UrlTypesKey].AsArray()
            : root.CreateArray(UrlTypesKey);

        foreach (var urlType in urlTypes.values)
        {
            var dict = urlType.AsDict();
            if (!dict.values.ContainsKey(UrlSchemesKey))
            {
                continue;
            }

            foreach (var registered in dict[UrlSchemesKey].AsArray().values)
            {
                if (registered.AsString() == scheme)
                {
                    return;
                }
            }
        }

        var newUrlType = urlTypes.AddDict();
        var schemes = newUrlType.CreateArray(UrlSchemesKey);
        schemes.AddString(scheme);
    }

    // Adds a scheme under LSApplicationQueriesSchemes if it isn't already present.
    private void RegisterQueryScheme(PlistElementDict root, string scheme)
    {
        var queries = root.values.ContainsKey(QueriesSchemesKey)
            ? root[QueriesSchemesKey].AsArray()
            : root.CreateArray(QueriesSchemesKey);

        foreach (var registered in queries.values)
        {
            if (registered.AsString() == scheme)
            {
                return;
            }
        }

        queries.AddString(scheme);
    }

    private ulong GetDiscordApplicationId()
    {
        var assets = AssetDatabase.FindAssets($"t:{nameof(DiscordSocialSDKConfig)}");
        if (assets.Length != 1)
        {
            throw new Exception(
                $"Expected 1 asset with type {nameof(DiscordSocialSDKConfig)}, found {assets.Length}");
        }

        var path = AssetDatabase.GUIDToAssetPath(assets[0]);
        var config = AssetDatabase.LoadAssetAtPath<DiscordSocialSDKConfig>(path);
        return config.ApplicationId;
    }
}
#endif
