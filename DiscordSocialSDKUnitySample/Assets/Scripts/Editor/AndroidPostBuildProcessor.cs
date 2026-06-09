#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;

/// <summary>
/// Registers the Discord Social SDK's authentication activity in the generated Android manifest so
/// the OAuth flow can redirect back into the app.
///
/// When <c>Client.Authorize()</c> runs, the SDK opens the system browser and expects the redirect
/// to return to the custom URI scheme <c>discord-{ApplicationId}:/authorize/callback</c>. Android
/// only routes that redirect if a matching activity with an intent filter is declared in the
/// manifest. The SDK ships <c>com.discord.socialsdk.AuthenticationActivity</c> for this purpose;
/// without the manifest entry, <c>Client.Authorize()</c> has nowhere to return to and crashes.
///
/// This runs after Unity generates the Gradle project but before Gradle builds it, so the manifest
/// is still editable. The Application ID is read from the existing <see cref="DiscordSocialSDKConfig"/>
/// asset, so no manual manifest editing is required.
/// </summary>
public class AndroidPostBuildProcessor : IPostGenerateGradleAndroidProject
{
    const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    const string AuthenticationActivityName = "com.discord.socialsdk.AuthenticationActivity";

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var manifest = new XmlDocument();
        manifest.Load(manifestPath);

        var namespaceManager = new XmlNamespaceManager(manifest.NameTable);
        namespaceManager.AddNamespace("android", AndroidNamespace);

        var application = manifest.SelectSingleNode("/manifest/application");
        if (application == null)
        {
            return;
        }

        // Remove any existing entry first so repeated builds stay idempotent.
        var existingActivity = application.SelectSingleNode(
            $"activity[@android:name='{AuthenticationActivityName}']",
            namespaceManager);
        if (existingActivity != null)
        {
            application.RemoveChild(existingActivity);
        }

        var activity = manifest.CreateElement("activity");
        activity.SetAttribute("name", AndroidNamespace, AuthenticationActivityName);
        activity.SetAttribute("exported", AndroidNamespace, "true");

        var intentFilter = manifest.CreateElement("intent-filter");

        var action = manifest.CreateElement("action");
        action.SetAttribute("name", AndroidNamespace, "android.intent.action.VIEW");
        intentFilter.AppendChild(action);

        var defaultCategory = manifest.CreateElement("category");
        defaultCategory.SetAttribute("name", AndroidNamespace, "android.intent.category.DEFAULT");
        intentFilter.AppendChild(defaultCategory);

        var browsableCategory = manifest.CreateElement("category");
        browsableCategory.SetAttribute("name", AndroidNamespace, "android.intent.category.BROWSABLE");
        intentFilter.AppendChild(browsableCategory);

        var data = manifest.CreateElement("data");
        data.SetAttribute("scheme", AndroidNamespace, $"discord-{GetDiscordApplicationId()}");
        intentFilter.AppendChild(data);

        activity.AppendChild(intentFilter);
        application.AppendChild(activity);

        manifest.Save(manifestPath);
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
