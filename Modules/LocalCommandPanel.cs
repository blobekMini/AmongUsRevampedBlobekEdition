using System;
using System.Collections.Generic;
using System.Linq;

namespace AmongUsRevamped;

// Command metadata and local output capture used by the F10 in-game console.
// This class does not start a listener or expose a browser panel.
internal static class LocalCommandPanel
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "/help", "/page", "/reload", "/dump", "/s", "/start", "/eg", "/endgame",
        "/em", "/endmeeting", "/sm", "/skip", "/skipmeeting", "/private", "/public",
        "/revive", "/tm", "/tiemeeting", "/impcount", "/setimp", "/vip", "/moderator", "/admin",
        "/removevip", "/removemoderator", "/removeadmin", "/col", "/color", "/colour", "/ucol",
        "/ucolor", "/ucolour", "/give", "/set", "/add", "/rolereset", "/playerreset", "/voteout",
        "/cvoteout", "/setvotes", "/kick", "/ban", "/ckick", "/cban", "/seeker", "/seekers", "/clearseeker",
        "/mapvote", "/allmapvote", "/gmvote", "/lastgame", "/l", "/aur", "/socials", "/0kc",
        "/0killcooldown", "/sns", "/shiftandseek", "/sns2", "/shiftandseek2", "/sr", "/speedrun",
        "/pns", "/poofandseek", "/r", "/roles", "/t", "/vote", "/copyfit", "/kill", "/colout",
        "/ucolout", "/whitelist", "/settings", "/bind", "/tp", "/teleport", "/impregnate"
    };

    private static readonly Dictionary<string, string[]> ArgumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/vip"] = new[] { "targetPlayer" }, ["/moderator"] = new[] { "targetPlayer" }, ["/admin"] = new[] { "targetPlayer" },
        ["/removevip"] = new[] { "targetPlayer" }, ["/removemoderator"] = new[] { "targetPlayer" }, ["/removeadmin"] = new[] { "targetPlayer" },
        ["/kick"] = new[] { "targetPlayer" }, ["/ban"] = new[] { "targetPlayer" }, ["/seeker"] = new[] { "targetPlayer" },
        ["/playerreset"] = new[] { "targetPlayer" }, ["/voteout"] = new[] { "targetPlayer" }, ["/kill"] = new[] { "targetPlayer" }, ["/revive"] = new[] { "targetPlayer" },
        ["/give"] = new[] { "role", "targetPlayers" }, ["/set"] = new[] { "role", "targetPlayers" }, ["/add"] = new[] { "role", "targetPlayers" },
        ["/copyfit"] = new[] { "sourcePlayer", "targetPlayers" }, ["/rolereset"] = new[] { "role" },
        ["/impcount"] = new[] { "count" }, ["/setimp"] = new[] { "count" },
        ["/cvoteout"] = new[] { "color" }, ["/ckick"] = new[] { "color" }, ["/cban"] = new[] { "color" },
        ["/setvotes"] = new[] { "voteTarget", "count" },
        ["/tp"] = new[] { "targetPlayer", "room" }, ["/teleport"] = new[] { "targetPlayer", "room" },
            ["/impregnate"] = new[] { "targetPlayer", "targetPlayer", "targetPlayer" },
        ["/col"] = new[] { "color" }, ["/color"] = new[] { "color" }, ["/colour"] = new[] { "color" }, ["/colout"] = new[] { "color" },
        ["/ucol"] = new[] { "color", "targetPlayers" }, ["/ucolor"] = new[] { "color", "targetPlayers" },
        ["/ucolour"] = new[] { "color", "targetPlayers" }, ["/ucolout"] = new[] { "color", "targetPlayers" },
        ["/t"] = new[] { "template" }, ["/vote"] = new[] { "voteOption" }, ["/whitelist"] = new[] { "text" },
        ["/settings"] = new[] { "text" }, ["/page"] = new[] { "text" }, ["/bind"] = new[] { "text" }
    };

    private static bool captureCommandOutput;
    private static string capturedCommandOutput;
    private static bool privateCommandDispatch;
    private static bool consoleCommandDispatch;

    public static bool IsCapturingCommandOutput => captureCommandOutput;
    public static bool IsPrivateCommandDispatch => privateCommandDispatch;
    public static bool IsConsoleCommandDispatch => consoleCommandDispatch;

    public static void SetPrivateCommandDispatch(bool enabled) => privateCommandDispatch = enabled;
    public static void SetConsoleCommandDispatch(bool enabled) => consoleCommandDispatch = enabled;

    public static bool TryBeginCommandOutputCapture()
    {
        if (captureCommandOutput) return false;
        capturedCommandOutput = string.Empty;
        captureCommandOutput = true;
        return true;
    }

    public static string EndCommandOutputCapture()
    {
        string output = capturedCommandOutput;
        captureCommandOutput = false;
        return output ?? string.Empty;
    }

    public static bool IsAllowedConsoleCommand(string command) => IsAllowedCommand(command);

    public static void CaptureCommandOutput(string message)
    {
        if (!captureCommandOutput || string.IsNullOrWhiteSpace(message)) return;
        capturedCommandOutput = string.IsNullOrWhiteSpace(capturedCommandOutput)
            ? message.Trim()
            : capturedCommandOutput + Environment.NewLine + message.Trim();
    }

    public static string[] GetCommandNames() => AllowedCommands.OrderBy(command => command, StringComparer.OrdinalIgnoreCase).ToArray();

    internal static string[] GetCommandArgumentTypes(string command) => GetArgumentTypes(command);

    public static string ExecuteInGameCommand(string command)
    {
        command = command?.Trim();
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
            PlayerControl.LocalPlayer == null || HudManager.Instance?.Chat == null)
            return "The in-game console is available only to the lobby host.";
        if (string.IsNullOrWhiteSpace(command) || command.Length > 120 || !IsAllowedCommand(command))
            return "Unsupported command or invalid command length.";

        ChatController chat = HudManager.Instance.Chat;
        if (chat.freeChatField?.textArea == null) return "Game chat is not ready.";

        string previousChatText = chat.freeChatField.textArea.text;
        capturedCommandOutput = string.Empty;
        captureCommandOutput = true;
        try
        {
            SetConsoleCommandDispatch(true);
            chat.freeChatField.textArea.SetText(command);
            chat.SendChat();
            return capturedCommandOutput ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "InGameCommandDispatch");
            return "Command failed; see BepInEx/LogOutput.log.";
        }
        finally
        {
            SetConsoleCommandDispatch(false);
            captureCommandOutput = false;
            chat.freeChatField.textArea.SetText(previousChatText ?? string.Empty);
        }
    }

    private static bool IsAllowedCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || !command.StartsWith('/')) return false;
        int separator = command.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        string root = separator < 0 ? command : command[..separator];
        return AllowedCommands.Contains(root);
    }

    private static string[] GetArgumentTypes(string command)
    {
        if (ArgumentTypes.TryGetValue(command, out string[] types)) return types;
        return command is "/help" or "/reload" or "/dump" or "/s" or "/start" or "/eg" or "/endgame" or
            "/em" or "/endmeeting" or "/sm" or "/skip" or "/skipmeeting" or "/private" or "/public" or
            "/tm" or "/tiemeeting" or "/seekers" or "/clearseeker" or "/mapvote" or "/allmapvote" or "/gmvote" or
            "/lastgame" or "/l" or "/aur" or "/socials" or "/0kc" or "/0killcooldown" or "/sns" or
            "/shiftandseek" or "/sns2" or "/shiftandseek2" or "/sr" or "/speedrun" or "/pns" or "/poofandseek" or "/r" or "/roles"
            ? Array.Empty<string>() : new[] { "text" };
    }

    internal static string GetCommandCategory(string command) => command switch
    {
        "/help" or "/reload" or "/dump" or "/lastgame" or "/l" or "/aur" or "/socials" or "/r" or "/roles" => "Information",
        "/vip" or "/moderator" or "/admin" or "/removevip" or "/removemoderator" or "/removeadmin" or
        "/kick" or "/ban" or "/ckick" or "/cban" or "/voteout" or "/cvoteout" or "/whitelist" => "Moderation",
        "/give" or "/set" or "/add" or "/rolereset" or "/playerreset" or "/seeker" or "/seekers" or "/clearseeker" => "Roles & Players",
        "/col" or "/color" or "/colour" or "/colout" or "/ucol" or "/ucolor" or "/ucolour" or "/ucolout" or "/copyfit" => "Appearance",
        "/impcount" or "/setimp" or "/mapvote" or "/allmapvote" or "/gmvote" or "/0kc" or "/0killcooldown" or
        "/sns" or "/shiftandseek" or "/sns2" or "/sr" or "/speedrun" or "/pns" or "/poofandseek" or "/vote" or "/t" => "Game Setup",
        "/eg" or "/endgame" or "/em" or "/endmeeting" or "/sm" or "/skip" or "/skipmeeting" or "/revive" or "/kill" or "/tm" or "/tiemeeting" or "/setvotes" or "/tp" or "/teleport" => "Game Control",
            "/eg" or "/endgame" or "/em" or "/endmeeting" or "/sm" or "/skip" or "/skipmeeting" or "/revive" or "/kill" or "/tm" or "/tiemeeting" or "/setvotes" or "/tp" or "/teleport" or "/impregnate" => "Game Control",
        "/private" or "/public" or "/s" or "/start" => "Lobby",
        "/settings" => "Game Setup",
        _ => "Other"
    };

    internal static string GetCommandExample(string command) => command switch
    {
        "/page" => "/page 2 /settings help",
        "/settings" => "/settings help (or: /settings killcooldown 30)",
        "/bind" => "/bind 'ucolor gay ;' 'make ; gay' ;:!color:;",
        "/whitelist" => "/whitelist add PlayerName (or: /whitelist remove PlayerName)",
        "/copyfit" => "/copyfit SourcePlayer PlayerOne,PlayerTwo",
        "/setvotes" => "/setvotes @all 2",
        "/tp" or "/teleport" => $"{command} PlayerName !room:security",
            "/impregnate" => "/impregnate PlayerOne PlayerTwo PlayerThree",
        "/revive" => "/revive PlayerName or /revive @all",
        "/tm" or "/tiemeeting" => command,
        "/give" => "/give scientist @all",
        "/set" => "/set scientist @all",
        "/add" => "/add crewmate PlayerOne PlayerTwo",
        "/ucol" or "/ucolor" or "/ucolour" or "/ucolout" => $"{command} rainbow PlayerName",
        "/col" or "/color" or "/colour" or "/colout" => $"{command} rainbow",
        "/kill" or "/kick" or "/ban" or "/seeker" or "/voteout" or "/playerreset" or
        "/vip" or "/moderator" or "/admin" or "/removevip" or "/removemoderator" or "/removeadmin" => $"{command} PlayerName",
        "/rolereset" => "/rolereset impostor",
        "/impcount" => "/impcount 2",
        "/setimp" => "/setimp 2",
        "/t" => "/t welcome",
        "/cvoteout" or "/ckick" or "/cban" => $"{command} red",
        _ => GetArgumentTypes(command).Length == 0 ? command : command + " " + string.Join(" ", GetArgumentTypes(command).Select((type, index) => type switch
        {
            "player" or "targetPlayer" => "PlayerName",
            "sourcePlayer" => "SourcePlayer",
            "players" or "targetPlayers" => "PlayerOne,PlayerTwo",
            "color" => "fortegreen",
            "role" => "crewmate",
            "template" => "welcome",
            "count" => "2",
            "voteOption" => "OptionName",
            _ => $"Argument{index + 1}"
        }))
    };

    internal static string GetCommandDescription(string command)
    {
        string key = command switch
        {
            "/help" => "helpDesc", "/reload" => "reloadDesc", "/dump" => "dumpDesc",
            "/s" or "/start" => "sDesc", "/eg" or "/endgame" => "egDesc",
            "/em" or "/endmeeting" => "emDesc", "/sm" or "/skip" or "/skipmeeting" => "skipMeetingDesc",
            "/private" => "privateDesc", "/public" => "publicDesc", "/revive" => "reviveDesc",
            "/tm" or "/tiemeeting" => "tieMeetingDesc", "/impcount" or "/setimp" => "impCountDesc",
            "/vip" => "vipDesc", "/moderator" => "moderatorDesc", "/admin" => "adminDesc",
            "/removevip" => "cremoveVipDesc", "/removemoderator" => "removeModeratorDesc", "/removeadmin" => "removeAdminDesc",
            "/col" or "/color" or "/colour" or "/colout" => "colDesc",
            "/ucol" or "/ucolor" or "/ucolour" or "/ucolout" => "ucolDesc",
            "/give" => "giveDesc", "/set" => "setDesc", "/add" => "addDesc",
            "/rolereset" => "roleResetDesc", "/playerreset" => "playerResetDesc",
            "/voteout" => "voteOutDesc", "/cvoteout" => "colorVoteOutDesc", "/kick" => "kickDesc", "/ban" => "banDesc",
            "/ckick" => "ckickDesc", "/cban" => "cbanDesc", "/seeker" => "seekerDesc", "/seekers" => "seekersDesc",
            "/clearseeker" => "clearSeekerDesc", "/mapvote" => "mapVoteDesc", "/allmapvote" => "allMapVoteDesc",
            "/gmvote" => "modeVoteDesc", "/lastgame" or "/l" => "lastgameDesc", "/aur" or "/socials" => "aurDesc",
            "/0kc" or "/0killcooldown" => "0kcDesc", "/sns" or "/shiftandseek" => "snsDesc",
            "/sns2" or "/shiftandseek2" => "snsTwoDesc", "/sr" or "/speedrun" => "SrDesc",
            "/pns" or "/poofandseek" => "pnsDesc", "/r" => "rDesc", "/roles" => "rolesDesc", "/t" => "templateDesc",
            _ => null
        };

        if (key != null) return Translator.Get(key);
        return command switch
        {
            "/page" => "Show a saved output page using its original command.",
            "/setvotes" => "Set the vote weight of living player(s) for the current meeting (host only).",
            "/copyfit" => "Copy the source player's outfit to one target player.",
            "/settings" => "Show or change supported normal-mode game settings in the lobby or during a game.",
            "/bind" => "Create a host-local text trigger for a supported private command.",
            "/whitelist" => "Add or remove an existing admin from the command whitelist.",
            "/kill" => "Kill the selected player (host permission required).",
            "/vote" => "Vote for a listed map or game mode option.",
            _ => "Run this host command; permission and game-state rules apply."
        };
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class LocalCommandPanelChatFeedbackPatch
{
    public static bool Prefix(object[] __args)
    {
        if (!LocalCommandPanel.IsCapturingCommandOutput) return true;
        LocalCommandPanel.CaptureCommandOutput(__args?.OfType<string>().FirstOrDefault());
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class LocalCommandPanelRpcChatFeedbackPatch
{
    public static bool Prefix(object[] __args)
    {
        if (!LocalCommandPanel.IsCapturingCommandOutput) return true;
        LocalCommandPanel.CaptureCommandOutput(__args?.OfType<string>().FirstOrDefault());
        return false;
    }
}