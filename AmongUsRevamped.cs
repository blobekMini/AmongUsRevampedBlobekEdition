global using HarmonyLib;
global using System.Collections.Generic;
global using System.Linq;
global using Object = UnityEngine.Object;

using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using System;
using System.Reflection;
using UnityEngine;

namespace AmongUsRevamped;

// dotnet build -c release
// dotnet build -c android

[BepInPlugin(Id, "Among Us Revamped Blobek Edition", "2.3.0")]
[BepInProcess("Among Us.exe")]
public partial class Main : BasePlugin
{
    public const string Id = "apemv.amongusrevamped";

    public Harmony Harmony { get; } = new(Id);
    public static BepInEx.Logging.ManualLogSource Logger;
    public static BasePlugin Instance;

    public static ConfigEntry<string> Preset1 { get; private set; }
    public static ConfigEntry<string> Preset2 { get; private set; }
    public static ConfigEntry<string> Preset3 { get; private set; }
    public static ConfigEntry<string> Preset4 { get; private set; }
    public static ConfigEntry<string> Preset5 { get; private set; }

    public static ConfigEntry<bool> GM { get; private set; }
    public static ConfigEntry<bool> ShowFps { get; private set; }
    public static ConfigEntry<bool> AutoStart { get; private set; }
    public static ConfigEntry<bool> DarkTheme { get; private set; }
    public static ConfigEntry<bool> LobbyMusic { get; private set; }
    public static ConfigEntry<bool> DisableInfoWhenDead { get; private set; }
    public static ConfigEntry<bool> DisableCommandHelper { get; private set; }
    public static ConfigEntry<bool> DisableCompatibilityWarning { get; private set; }
    public static ConfigEntry<bool> DisableChatModifier { get; private set; }
    public static ConfigEntry<bool> DisableLobbyInfo { get; private set; }
    public static ConfigEntry<bool> AlwaysShowChat { get; private set; }
    public static ConfigEntry<bool> SeeGhosts { get; private set; }
    public static ConfigEntry<bool> EnableZoom { get; private set; }
    public static ConfigEntry<bool> PreviewRoles { get; private set; }
    public static ConfigEntry<bool> PreviewDevices { get; private set; }
    public static ConfigEntry<bool> PreviewLevels { get; private set; }

    public static NormalGameOptionsV12 NormalOptions => GameOptionsManager.Instance != null ? GameOptionsManager.Instance.currentNormalGameOptions : null;
    public static HideNSeekGameOptionsV12 HideNSeekOptions => GameOptionsManager.Instance != null ? GameOptionsManager.Instance.currentHideNSeekGameOptions : null;
    public static MapNames CurrentMap => (MapNames)NormalOptions.MapId;

    public static bool HasArgumentException;
    public static string CredentialsText;
    public static string ModVersion => $"v{typeof(Main).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "2.3.0.1.30"}";
    public static bool CommandPreviewsEnabled { get; private set; }

    public static void SetHostModVersion(string version)
    {
        CommandPreviewsEnabled = string.Equals(version, ModVersion, StringComparison.OrdinalIgnoreCase);
        Logger?.LogInfo($"Host mod version {version}; local version {ModVersion}; command previews {(CommandPreviewsEnabled ? "enabled" : "disabled")}.");
    }

    public static float GameTimer;
    public static float AfkTimer;

    public static readonly Dictionary<int, int> SayStartTimes = [];
    public static readonly Dictionary<int, int> SayDeniedWordTimes = [];

    public static PlayerControl[] AllPlayerControls
    {
        get
        {
            int count = PlayerControl.AllPlayerControls.Count;
            var result = new PlayerControl[count];
            var i = 0;

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.PlayerId >= 254) continue;

                result[i++] = pc;
            }

            if (i == 0) return [];

            Array.Resize(ref result, i);
            return result;
        }
    }

    public override void Load()
    {
        var handler = AmongUsRevamped.Logger.Handler("GitVersion");        
        Logger = BepInEx.Logging.Logger.CreateLogSource("AUR Blobek Edition");
        AmongUsRevamped.Logger.Enable();
        Instance = this;

        Preset1 = Config.Bind("Preset Name Options", "Preset1", "Preset 1");
        Preset2 = Config.Bind("Preset Name Options", "Preset2", "Preset 2");
        Preset3 = Config.Bind("Preset Name Options", "Preset3", "Preset 3");
        Preset4 = Config.Bind("Preset Name Options", "Preset4", "Preset 4");
        Preset5 = Config.Bind("Preset Name Options", "Preset5", "Preset 5");

        AutoStart = Config.Bind("Client Options", "Auto Start", false);
        GM = Config.Bind("Client Options", "Game Master", false);
        ShowFps = Config.Bind("Client Options", "Show FPS", false);
        AutoStart = Config.Bind("Client Options", "Auto Start", false);
        DarkTheme = Config.Bind("Client Options", "Dark Theme", false);
        LobbyMusic = Config.Bind("Client Options", "Lobby Music", false);
        DisableInfoWhenDead = Config.Bind("Client Options", "Disable Task/Kill View", false);
        DisableCommandHelper = Config.Bind("Client Options", "Disable Command Helper", false);
        DisableCompatibilityWarning = Config.Bind("Client Options", "Disable Compatibility Warning", false);
        DisableChatModifier = Config.Bind("Client Options", "Disable Chat Modifications", false);
        DisableLobbyInfo = Config.Bind("Client Options", "Disable Lobby Info", false);
        AlwaysShowChat = Config.Bind("Client Options", "Always Show Chat", false);
        SeeGhosts = Config.Bind("Client Options", "See Ghosts", false);
        EnableZoom = Config.Bind("Client Options", "Scrollwheel Zoom", false);
        PreviewRoles = Config.Bind("Client Options", "Preview Roles", false);
        PreviewDevices = Config.Bind("Client Options", "Preview Devices", false);
        PreviewLevels = Config.Bind("Client Options", "Preview Levels", false);
        Config.Save();

        Translator.Init();
        Translator.Reload();
        BanManager.Init();

        Harmony.PatchAll();

        ConsoleManager.DetachConsole();
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    class ModManagerLateUpdatePatch
    {
        public static void Prefix(ModManager __instance)
        {
            LateTask.Update(Time.deltaTime);
        }
    }

    public enum ColorToString
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Pink = 3,
        Orange = 4,
        Yellow = 5,
        Black = 6,
        White = 7,
        Purple = 8,
        Brown = 9,
        Cyan = 10,
        Lime = 11,
        Maroon = 12,
        Rose = 13,
        Banana = 14,
        Gray = 15,
        Tan = 16,
        Coral = 17,
        Fortegreen = 18
    }

    // Innersloth now censors messages with more than 5 numbers. Sucks to be them.
    public static readonly char[] CircledDigits =
    {
        '⓪', // 0
        '①', // 1
        '②', // 2
        '③', // 3
        '④', // 4
        '⑤', // 5
        '⑥', // 6
        '⑦', // 7
        '⑧', // 8
        '⑨'  // 9
    };
}