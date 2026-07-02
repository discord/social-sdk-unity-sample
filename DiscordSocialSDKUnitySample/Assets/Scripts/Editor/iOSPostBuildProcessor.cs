#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Registers the Discord Social SDK's OAuth redirect URL scheme in the generated iOS Info.plist so
/// the authentication flow can redirect back into the app.
///
/// When <c>Client.Authorize()</c> runs, the SDK opens the system browser and expects the redirect
/// to return to the custom URI scheme <c>discord-{ApplicationId}:/authorize/callback</c>. iOS only
/// routes that redirect if the scheme is declared under <c>CFBundleURLTypes</c> in the app's
/// Info.plist; without the entry, <c>Client.Authorize()</c> has nowhere to return to and the login
/// never completes.
///
/// This runs after Unity generates the Xcode project, so the Info.plist is available to edit. The
/// Application ID is read from the existing <see cref="DiscordSocialSDKConfig"/> asset, so no manual
/// Info.plist editing is required.
/// </summary>
public class iOSPostBuildProcessor : IPostprocessBuildWithReport
{
    const string UrlTypesKey = "CFBundleURLTypes";
    const string UrlSchemesKey = "CFBundleURLSchemes";

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

        string scheme = $"discord-{GetDiscordApplicationId()}";

        // Skip if our scheme is already registered first so repeated builds stay idempotent.
        if (SchemeAlreadyRegistered(plist.root, scheme))
        {
            return;
        }

        var urlTypes = plist.root.values.ContainsKey(UrlTypesKey)
            ? plist.root[UrlTypesKey].AsArray()
            : plist.root.CreateArray(UrlTypesKey);

        var urlType = urlTypes.AddDict();
        var schemes = urlType.CreateArray(UrlSchemesKey);
        schemes.AddString(scheme);

        plist.WriteToFile(plistPath);
    }

    private bool SchemeAlreadyRegistered(PlistElementDict root, string scheme)
    {
        if (!root.values.ContainsKey(UrlTypesKey))
        {
            return false;
        }

        foreach (var urlType in root[UrlTypesKey].AsArray().values)
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
                    return true;
                }
            }
        }

        return false;
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
