# Discord Social SDK Unity Sample

A Unity sample project demonstrating integration with the Discord Social SDK designed with best practices in mind.

https://github.com/user-attachments/assets/3bfcaa43-51f1-469f-b329-cbadbeab6102

## What This Sample Includes

This sample showcases many Discord Social SDK features in a complete, interactive Unity scene:
- **Account Linking & Authentication** - Discord OAuth2 integration with secure token storage
- **Friends List** - Display and interact with Discord friends lists
- **Rich Presence** - Dynamic Discord activity cards with lobby and invite integration
- **Game Lobbies** - Create, join, and manage multiplayer lobbies

Planned features that haven't been implemented yet:
- **Friend Management** - Send friend requests and remove friends
- **Game Invitations** - Send deep-link invites to friends for instant lobby joining
- **Direct Messaging** - In-game messaging
- **Linked Channels** - Connect game chat to Discord server channels
- **User Profiles** - Fetch and display Discord user information
- **Provisional Accounts** - Support for users without Discord accounts

## Quick Start

### Prerequisites

- Unity 6 or later
- Discord Developer Account

### Setup Instructions

1. **Clone the Repository**
   ```bash
   git clone https://github.com/discord/social-sdk-unity-sample.git
   ```
   or download the ZIP file from the GitHub repository.

2. **Open in Unity**
   - Launch Unity Hub
   - Click "Add" and select "DiscordSocialSDKUnitySample" from the cloned repository or extracted ZIP
   - Open the project

3. **Configure Your Discord Application**
   - Visit the [Discord Developer Portal](https://discord.com/developers/applications)
   - Create a new application or use an existing one
   - Navigate to the Discord Social SDK > Getting Started section
   - Fill out the form
   - Click Submit and the Social SDK will be enabled for your application
   - Navigate to the OAuth2 tab:
      - Under "Client information" enable Public Client
      - Under "Redirects" add a redirect URL `http://127.0.0.1/callback`
   - Navigate to Discord Social SDK > Downloads
   - Download the latest SDK (`DiscordSocialSdk-UnityPlugin-X.X.X.zip`)
   - Either:
      - Unzip the zip file in the Unity Packages folder, or
      - Unzip the zip file and [Install Package from Disk](https://docs.unity3d.com/Manual/upm-ui-local.html). Make sure the folder is in a directory that won't get moved or deleted as your Unity project will load it from that location.

<img width="989" height="656" alt="Screenshot 2025-07-24 at 9 01 00 AM" src="https://github.com/user-attachments/assets/5ff14158-5700-4435-ad4d-f5da458fe376" />

4. **Configure the Sample**
   - In Unity open the Example scene
   - In your asset folder right click > Create > Config > Discord Social SDK
   - Click on the Social SDK Config and paste your Application ID found under "General Information" from the application you created in the [Discord Developer Portal](https://discord.com/developers/applications)
   - Drag your Discord Social SDK Config into the DiscordManager object in the scene

5. **Run**
   - Press the play button to test it in the Editor
   - Click on the "Connect to Discord" button in the UI
   - Discord or the browser should open to authorize the application
   - After authorizing, you should see your Discord friends list show up in the sample and your Rich Presence should update to show you are playing the sample game
   - You will also see all logging in the Unity Console

<img width="912" height="750" alt="Screenshot 2025-07-24 at 9 04 36 AM" src="https://github.com/user-attachments/assets/e03b1927-4dc9-461b-99a8-a1e244ce70d2" />

### Troubleshooting

#### The type or namespace name 'Discord', 'Client', etc... could not be found (are you missing a using directive or an assembly reference?)
If you see errors related to missing types or namespaces, it likely means that the Discord Social SDK is not installed correctly. Make sure you followed all the setup instructions and that the SDK is properly imported into your Unity project.

#### Mac libdiscord_partner_sdk.dylib Not Opened
On Mac you may get the error "libdiscord_partner_sdk.dylib" Not Opened because Apple couldn't verify it. If this happens press Done on the popup. You'll need to open your System Settings > Privacy & Security and scroll down to the Security section. It will tell you "libdiscord_partner_sdk.dylib" was blocked to protect your Mac. Press Open Anyway and try running again. Now when you get the pop up you'll have the option to select Open Anyway and it will be able to use it successfully.

#### Nothing happens when I click the "Connect to Discord" button
If nothing happens when you click the "Connect to Discord" button, make sure you have a `DiscordManager` in your scene and that it has a valid DiscordSocialSdkConfig assigned. You can find the `DiscordManager` prefab already set up for you in the Prefabs folder. Also check the Unity Console for any errors.

#### Opening Project in Non-Matching Editor Installation
You'll see this warning if you open the project in a different version of Unity than it was created with. It's safe to ignore as long as you're opening the sample in a compatible version (Unity 6+). You can press "Continue" and you might get another modal saying "URP Material upgrade" which you can safely press "Ok" on.

## Project Structure
This sample creates easy to use prefabs that can be used to build out parts of the Discord Social SDK. The central piece is the `DiscordManager` which handles the connection to Discord and provides access to the SDK features. In order to use the `DiscordManager`, you need to create a `DiscordSocialSdkConfig` Scriptable Object with the Application ID from the Discord Developer Portal and assign it to the `DiscordManager`. The friend list is built using the `FriendsList` prefab, which contains the logic to fetch and display friends. Each friend is represented by a `FriendUI` prefab that shows their username, status, and profile picture. Rich Presence is handled by the `RichPresence` prefab, which updates the user's activity status in Discord. Each prefab is easy to drop into your scene and customize as needed.

## Resources

### Documentation
- [Discord Social SDK Documentation](https://discord.com/developers/docs/discord-social-sdk/overview)
- [Discord Social SDK Reference](https://discord.com/developers/docs/social-sdk/index.html)

### Design Resources
- [Friends List Designs](https://www.figma.com/community/file/1512487996808869592/the-social-sdk-friend-list-starter-pack) - UI/UX reference used in this sample
- [Discord Brand Design Guidelines](https://discord.com/branding) - Official branding resources

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

This project is licensed under the [Discord Social SDK Terms](https://support-dev.discord.com/hc/en-us/articles/30225844245271-Discord-Social-SDK-Terms). See the [LICENSE](LICENSE.md) file for details.

---

**Built with ❤️ by the Discord Developer Relations team**

Questions? Check out the `#social-sdk-dev-help` channel in the [Discord Developers server](https://discord.gg/discord-developers)!
