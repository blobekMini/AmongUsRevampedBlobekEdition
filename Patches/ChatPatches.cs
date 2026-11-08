using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AmongUsRevamped;

internal static class ChatCommandHelpers
{
    private static readonly Dictionary<byte, byte> OriginalPlayerColors = new();
    private static readonly Dictionary<byte, RoleTypes> OriginalRolesBeforeGive = new();
    private static readonly Dictionary<byte, RoleTypes> RolesBeforeDeath = new();
    private const int PageBodyLimit = 90;
    private sealed class PagedOutput
    {
        public int ClientId { get; init; }
        public string PlayerName { get; init; }
        public string Command { get; init; }
        public string[] Pages { get; init; }
        public DateTime CreatedUtc { get; init; }
    }

    private static readonly Dictionary<int, PagedOutput> PagedOutputs = new();

    public static void RememberOriginalColor(PlayerControl player)
    {
        if (player?.Data == null) return;
        OriginalPlayerColors.TryAdd(player.PlayerId, (byte)player.Data.DefaultOutfit.ColorId);
    }

    public static void RememberOriginalRole(PlayerControl player, RoleTypes role)
    {
        if (player == null) return;
        OriginalRolesBeforeGive.TryAdd(player.PlayerId, role);
    }

    public static void RememberRoleBeforeDeath(PlayerControl player, RoleTypes role)
    {
        if (player?.Data == null || role is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost) return;
        RolesBeforeDeath.TryAdd(player.PlayerId, role);
    }

    private static RoleTypes GetRoleToRestoreAfterRevive(PlayerControl player)
    {
        if (player == null) return RoleTypes.Crewmate;
        if (RolesBeforeDeath.Remove(player.PlayerId, out RoleTypes originalRole)) return originalRole;

        return player.Data?.RoleType switch
        {
            RoleTypes.CrewmateGhost => RoleTypes.Crewmate,
            RoleTypes.ImpostorGhost => RoleTypes.Impostor,
            RoleTypes role => role
        };
    }

    public static void ClearChatInput(ChatController chatController)
    {
        if (chatController == null || chatController.freeChatField == null || chatController.freeChatField.textArea == null) return;

        chatController.freeChatField.textArea.Clear();
        chatController.freeChatField.textArea.SetText(string.Empty);
    }

    private static string[] CreatePages(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Array.Empty<string>();

        System.Text.StringBuilder plainText = new(message.Length);
        bool insideTag = false;
        foreach (char character in message)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }
            if (character == '>')
            {
                insideTag = false;
                continue;
            }
            if (!insideTag && character != '\r') plainText.Append(character);
        }

        List<string> wrappedLines = new();
        foreach (string originalLine in plainText.ToString().Replace("\r", string.Empty).Split('\n'))
        {
            string line = originalLine.Trim();
            if (line.Length == 0) continue;

            while (line.Length > PageBodyLimit)
            {
                int boundary = line.LastIndexOf(' ', PageBodyLimit);
                if (boundary <= 0)
                {
                    boundary = PageBodyLimit;
                    wrappedLines.Add(line.Substring(0, boundary));
                    line = line.Substring(boundary);
                    continue;
                }
                wrappedLines.Add(line.Substring(0, boundary).TrimEnd());
                line = line.Substring(boundary + 1).TrimStart();
            }

            if (line.Length > 0) wrappedLines.Add(line);
        }

        List<string> pages = new();
        System.Text.StringBuilder page = new();
        foreach (string line in wrappedLines)
        {
            int additionalLength = line.Length + (page.Length == 0 ? 0 : 1);
            if (page.Length > 0 && page.Length + additionalLength > PageBodyLimit)
            {
                pages.Add(page.ToString());
                page.Clear();
            }
            if (page.Length > 0) page.Append('\n');
            page.Append(line);
        }
        if (page.Length > 0) pages.Add(page.ToString());
        return pages.ToArray();
    }

    public static void ShowPagedOutput(PlayerControl requester, string command, string message)
    {
        if (requester?.Data == null || string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(message)) return;

        if (LocalCommandPanel.IsCapturingCommandOutput)
        {
            LocalCommandPanel.CaptureCommandOutput(message);
            return;
        }

        string[] pages = CreatePages(message);
        if (pages.Length == 0) return;
        if (pages.Length == 1)
        {
            SendPageToLobby(requester, pages[0], 1, 1);
            return;
        }

        PagedOutputs[requester.Data.ClientId] = new PagedOutput
        {
            ClientId = requester.Data.ClientId,
            PlayerName = requester.Data.PlayerName,
            Command = command.Trim(),
            Pages = pages,
            CreatedUtc = DateTime.UtcNow
        };
        Logger.Info($"Stored {pages.Length} output pages for {requester.Data.PlayerName} from {command}.", "CommandPages");
        SendPageToLobby(requester, pages[0], 1, pages.Length);
        if (pages.Length > 1)
        {
            string hint = $"More: /page <n> {command.Trim()}";
            if (hint.Length > 100) hint = "More: /page <n> <command> <args>";
            int requesterClientId = requester.Data.ClientId;
            new LateTask(() =>
            {
                if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
                    (requester != PlayerControl.LocalPlayer && Utils.GetClientById(requesterClientId) == null)) return;
                SendPageToLobby(requester, hint, 1, 1);
            }, 1.25f, "CommandPageHint", false);
        }
    }

    public static string SanitizeOutgoingChatText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        const string AllowedCharacters = "()1234567890QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm!@#%^&;:'./?";
        System.Text.StringBuilder sanitized = new(text.Length);

        foreach (char character in text)
        {
            if (character == '\r') continue;
            if (character == '\n' || character == ' ' || AllowedCharacters.Contains(character))
            {
                sanitized.Append(character);
            }
        }

        return sanitized.ToString().Trim();
    }

    private static void SendPageToLobby(PlayerControl requester, string page, int pageNumber, int pageCount)
    {
        if (requester?.Data == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
            PlayerControl.LocalPlayer == null) return;

        string message = pageCount == 1 ? page : $"[{pageNumber}/{pageCount}] {page}";
        string safeMessage = SanitizeOutgoingChatText(message);
        if (string.IsNullOrWhiteSpace(safeMessage)) return;

        Logger.Info($"Sending page {pageNumber}/{pageCount} ({safeMessage.Length} characters) to {requester.Data.PlayerName}: {safeMessage}", "CommandPages");
        if (requester != PlayerControl.LocalPlayer)
        {
            Logger.Info($"Suppressed public page {pageNumber}/{pageCount} requested by {requester.Data.PlayerName}.", "RemoteCommandFeedback");
            return;
        }
        SendChatPatch.RegisterVisibleCommandEcho(safeMessage);
        PlayerControl.LocalPlayer.RpcSendChat(safeMessage);
    }

    private static void SendPageFeedback(PlayerControl requester, string text)
    {
        if (requester?.Data == null || PlayerControl.LocalPlayer == null || text.Length > 100) return;
        string safeText = SanitizeOutgoingChatText(text);
        if (string.IsNullOrWhiteSpace(safeText)) return;

        Logger.Info($"Sending page feedback to {requester.Data.PlayerName}: {safeText}", "CommandPages");
        if (requester != PlayerControl.LocalPlayer)
        {
            Logger.Info($"Suppressed public page feedback requested by {requester.Data.PlayerName}: {safeText}", "RemoteCommandFeedback");
            return;
        }
        SendChatPatch.RegisterVisibleCommandEcho(safeText);
        PlayerControl.LocalPlayer.RpcSendChat(safeText);
    }

    public static bool HandlePageCommand(PlayerControl requester, string text)
    {
        string arguments = text.Substring("/page".Length).Trim();
        int separator = arguments.IndexOfAny(new[] { ' ', '\t' });
        if (separator <= 0 || !int.TryParse(arguments.Substring(0, separator), out int pageNumber))
        {
            SendPageFeedback(requester, "Usage: /page <number> <command> [arguments]");
            return true;
        }

        string requestedCommand = arguments.Substring(separator).Trim();
        if (requester?.Data == null || !PagedOutputs.TryGetValue(requester.Data.ClientId, out PagedOutput output) ||
            DateTime.UtcNow - output.CreatedUtc > TimeSpan.FromMinutes(10))
        {
            if (requester?.Data != null) PagedOutputs.Remove(requester.Data.ClientId);
            SendPageFeedback(requester, "No saved pages. Run the command again.");
            return true;
        }

        string normalizedRequestedCommand = string.Join(" ", requestedCommand.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        string normalizedSavedCommand = string.Join(" ", output.Command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        if (!normalizedRequestedCommand.Equals(normalizedSavedCommand, StringComparison.OrdinalIgnoreCase))
        {
            SendPageFeedback(requester, $"Pages are for: {output.Command}");
            return true;
        }

        if (pageNumber < 1 || pageNumber > output.Pages.Length)
        {
            SendPageFeedback(requester, $"Choose a page from 1 to {output.Pages.Length}.");
            return true;
        }

        SendPageToLobby(requester, output.Pages[pageNumber - 1], pageNumber, output.Pages.Length);
        return true;
    }

    public static void ShowHelpPages(PlayerControl requester)
    {
        string helpText = $"{Translator.Get("allCommandsFull")}\n\n{Translator.Get("recentCommandsHelp")}\n\n{Translator.Get("meetingCommandsHelp")}";
        ShowPagedOutput(requester, "/help", helpText);
    }

    public static bool TryResolvePlayerTargets(string input, bool allowAll, out List<PlayerControl> targets, out string error, PlayerControl sender = null)
    {
        targets = new List<PlayerControl>();
        error = string.Empty;
        input = input?.Trim();

        if (string.IsNullOrWhiteSpace(input) || input.Equals("@me", StringComparison.OrdinalIgnoreCase))
        {
            PlayerControl self = sender ?? PlayerControl.LocalPlayer;
            if (self?.Data == null)
            {
                error = Translator.Get("playerNameMissing");
                return false;
            }

            targets.Add(self);
            return true;
        }

        if (input.Equals("@all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player != null && player.Data != null && !player.Data.Disconnected)
                {
                    targets.Add(player);
                }
            }
            if (targets.Count > 0) return true;
            error = "There are no connected players to match @all.";
            return false;
        }

        if (input.Contains(','))
        {
            HashSet<byte> seenPlayerIds = new();
            foreach (string part in input.Split(',').Select(value => value.Trim().Trim('"')).Where(value => value.Length > 0))
            {
                if (!TryResolvePlayerTargets(part, allowAll, out List<PlayerControl> resolvedPlayers, out error, sender))
                    return false;

                foreach (PlayerControl player in resolvedPlayers)
                {
                    if (seenPlayerIds.Add(player.PlayerId)) targets.Add(player);
                }
            }
            return targets.Count > 0;
        }

        if (input.StartsWith("!color:", StringComparison.OrdinalIgnoreCase))
        {
            string colorName = input.Substring("!color:".Length);
            if (string.IsNullOrEmpty(colorName) || colorName.Any(character =>
                    character is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) ||
                !Utils.TryGetColorId(colorName, out byte colorId))
            {
                error = Translator.Get("invalidColor", colorName);
                return false;
            }

            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player?.Data != null && !player.Data.Disconnected && player.Data.DefaultOutfit.ColorId == colorId)
                    targets.Add(player);
            }

            if (targets.Count > 0) return true;
            error = $"No players currently use the color '{colorName}'.";
            return false;
        }

        if (!IsValidPlayerNamePattern(input))
        {
            error = "Player names may contain only A-Z, a-z, and 0-9. Use // as a wildcard; spaces and symbols are not allowed.";
            return false;
        }

        bool hasWildcard = input.Contains("//", StringComparison.Ordinal);
        string normalizedInput = hasWildcard
            ? string.Join("//", input.Split(new[] { "//" }, StringSplitOptions.None).Select(NormalizePlayerName))
            : NormalizePlayerName(input);
        List<PlayerControl> matches = new();

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player.Data == null || player.Data.Disconnected) continue;

            string normalizedPlayerName = NormalizePlayerName(player.Data.PlayerName);
            bool matchesName = hasWildcard
                ? WildcardPlayerNameMatches(normalizedPlayerName, normalizedInput)
                : normalizedPlayerName.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase);
            if (matchesName) matches.Add(player);
        }

        if (matches.Count > 0)
        {
            targets.AddRange(matches);
            return true;
        }

        error = Translator.Get("playerNotFound", input);
        return false;
    }

    private static string NormalizePlayerName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return string.Empty;
        return new string(playerName.Where(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
    }

    private static bool IsValidPlayerNamePattern(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        bool hasNameCharacter = false;
        for (int index = 0; index < input.Length; index++)
        {
            char character = input[index];
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                hasNameCharacter = true;
                continue;
            }

            if (character == '/' && index + 1 < input.Length && input[index + 1] == '/')
            {
                index++;
                continue;
            }

            return false;
        }

        return hasNameCharacter;
    }

    private static bool WildcardPlayerNameMatches(string playerName, string pattern)
    {
        string[] segments = pattern.Split(new[] { "//" }, StringSplitOptions.None);
        bool startsWithWildcard = pattern.StartsWith("//", StringComparison.Ordinal);
        bool endsWithWildcard = pattern.EndsWith("//", StringComparison.Ordinal);
        int searchFrom = 0;

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            string segment = segments[segmentIndex];
            if (segment.Length == 0) continue;

            int matchIndex = playerName.IndexOf(segment, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0 || (segmentIndex == 0 && !startsWithWildcard && matchIndex != 0)) return false;
            searchFrom = matchIndex + segment.Length;
        }

        if (!endsWithWildcard)
        {
            string lastSegment = segments.LastOrDefault(segment => segment.Length > 0);
            return lastSegment != null && playerName.EndsWith(lastSegment, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool HasExactPlayerName(string playerName)
    {
        string normalizedInput = NormalizePlayerName(playerName);
        if (string.IsNullOrEmpty(normalizedInput)) return false;
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data != null && NormalizePlayerName(player.Data.PlayerName).Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HandleWhitelistCommand(PlayerControl sender, string text)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Only the host can manage the command whitelist.");
            return false;
        }

        string arguments = text.Substring("/whitelist".Length).Trim();
        int separator = arguments.IndexOf(' ');
        if (separator <= 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Usage: /whitelist add|remove <player>");
            return false;
        }

        string action = arguments.Substring(0, separator).Trim();
        string playerName = arguments.Substring(separator + 1).Trim().Trim('"');
        if ((action != "add" && action != "remove") || string.IsNullOrWhiteSpace(playerName))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Usage: /whitelist add|remove <player>");
            return false;
        }

        if (!TryResolvePlayerTargets(playerName, false, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return false;
        }

        List<string> changedPlayers = new();
        List<string> rejectedPlayers = new();
            foreach (PlayerControl target in targets)
            {
                string friendCode = target.Data?.FriendCode?.Trim();
                if (string.IsNullOrWhiteSpace(friendCode))
                {
                    rejectedPlayers.Add(target.Data?.PlayerName ?? "Unknown");
                    continue;
                }

                bool changed = action == "add"
                    ? Utils.CheckAccessLevel(friendCode) >= 3 && BanManager.AddCommandWhitelist(friendCode)
                    : BanManager.RemoveCommandWhitelist(friendCode);
                if (changed) changedPlayers.Add(target.Data.PlayerName);
                else rejectedPlayers.Add(target.Data.PlayerName);
            }

            string result = action == "add" && rejectedPlayers.Count > 0 && changedPlayers.Count == 0
                ? "Only players already listed as admins can be whitelisted."
                : action == "add"
                    ? $"Added to whitelist: {string.Join(", ", changedPlayers)}"
                    : $"Removed from whitelist: {string.Join(", ", changedPlayers)}";
            if (rejectedPlayers.Count > 0) result += $"; unchanged: {string.Join(", ", rejectedPlayers)}";
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, result);
            return changedPlayers.Count > 0;
            }

    public static bool HandleMeetingEndCommand(PlayerControl sender, bool summarizeVotes)
    {
        string command = summarizeVotes ? "/em" : "/sm";
        if (!HasCommandAccess(sender, Options.SlashEndMeetingCmd))
        {
            string denied = Translator.Get("accessDenied", command);
            if (LocalCommandPanel.IsCapturingCommandOutput) LocalCommandPanel.CaptureCommandOutput(denied);
            else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, denied);
            return false;
        }

        MeetingHud meeting = MeetingHud.Instance;
        if (!Utils.IsMeeting || meeting == null)
        {
            const string noMeeting = "No meeting is currently active.";
            if (LocalCommandPanel.IsCapturingCommandOutput) LocalCommandPanel.CaptureCommandOutput(noMeeting);
            else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, noMeeting);
            return false;
        }

        if (summarizeVotes)
        {
            List<string> voteLines = new();
            foreach (PlayerVoteArea voteArea in meeting.playerStates)
            {
                if (voteArea == null || voteArea.AmDead) continue;

                byte voterId = (byte)voteArea.PlayerId;
                PlayerControl voter = null;
                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                {
                    if (player != null && player.PlayerId == voterId)
                    {
                        voter = player;
                        break;
                    }
                }
                if (voter?.Data == null) continue;

                string voteDescription;
                if (!voteArea.DidVote)
                {
                    voteDescription = "no vote";
                }
                else
                {
                    byte votedForId = (byte)voteArea.VotedForId;
                    if (votedForId == byte.MaxValue)
                    {
                        voteDescription = "skipped";
                    }
                    else
                    {
                        PlayerControl votedFor = null;
                        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                        {
                            if (player != null && player.PlayerId == votedForId)
                            {
                                votedFor = player;
                                break;
                            }
                        }
                        voteDescription = votedFor?.Data?.PlayerName ?? "unknown";
                    }
                }

                voteLines.Add($"{SanitizeOutgoingChatText(voter.Data.PlayerName)} voted {SanitizeOutgoingChatText(voteDescription)}");
            }

            List<string> messages = new();
            string currentMessage = "Votes:";
            foreach (string voteLine in voteLines)
            {
                string line = voteLine.Length > 90 ? voteLine.Substring(0, 90) : voteLine;
                if (currentMessage.Length + line.Length + 1 > 100)
                {
                    messages.Add(currentMessage);
                    currentMessage = "Votes:";
                }
                currentMessage += " " + line;
            }
            if (voteLines.Count == 0) currentMessage = "Votes: no votes cast.";
            messages.Add(currentMessage);

            if (LocalCommandPanel.IsCapturingCommandOutput)
            {
                LocalCommandPanel.CaptureCommandOutput(string.Join(Environment.NewLine, messages));
            }

            OnGameJoinedPatch.WaitingForChat = true;
            if (sender != PlayerControl.LocalPlayer)
            {
                Logger.Info($"Suppressed public /em vote summary requested by {sender.Data?.PlayerName ?? "unknown player"}.", "RemoteCommandFeedback");
                new LateTask(() =>
                {
                    if (MeetingHud.Instance != null) CloseMeeting();
                    else OnGameJoinedPatch.WaitingForChat = false;
                }, 0.5f, "CloseMeetingAfterSuppressedVoteSummary");
                return true;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                string message = messages[i];
                bool isLast = i == messages.Count - 1;
                new LateTask(() =>
                {
                    if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
                        PlayerControl.LocalPlayer == null || MeetingHud.Instance == null)
                    {
                        if (isLast) OnGameJoinedPatch.WaitingForChat = false;
                        return;
                    }

                    string safeMessage = SanitizeOutgoingChatText(message);
                    if (!string.IsNullOrWhiteSpace(safeMessage))
                    {
                        if (SendChatPatch.IsHandlingRemotePublicCommand)
                        {
                            Logger.Info($"Suppressed public meeting vote summary: {safeMessage}", "RemoteCommandFeedback");
                        }
                        else
                        {
                            Logger.Info($"Sending meeting vote summary: {safeMessage}", "MeetingVoteSummary");
                            SendChatPatch.RegisterVisibleCommandEcho(safeMessage);
                            PlayerControl.LocalPlayer.RpcSendChat(safeMessage);
                        }
                    }
                    if (isLast)
                    {
                        new LateTask(CloseMeeting, 0.5f, "CloseMeetingAfterVoteSummary");
                    }
                }, 0.2f + i * 2.2f, $"MeetingVoteSummary{i}");
            }

            return true;
        }

        CloseMeeting();
        return true;
    }

    private static void CloseMeeting()
    {
        if (MeetingHud.Instance == null) return;
        MeetingHud.Instance.RpcClose();
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data?.RoleType == RoleTypes.Shapeshifter)
            {
                player.CmdCheckRevertShapeshift(false);
            }
        }
        OnGameJoinedPatch.WaitingForChat = false;
    }

    public static bool TryResolveRole(string input, out RoleTypes roleType, out string error)
    {
        roleType = RoleTypes.Crewmate;
        error = string.Empty;

        string normalized = input.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        switch (normalized.ToLowerInvariant())
        {
            case "imp":
            case "impostor":
                roleType = RoleTypes.Impostor;
                return true;
            case "crew":
            case "crewmate":
                roleType = RoleTypes.Crewmate;
                return true;
            case "sci":
            case "scientist":
                roleType = RoleTypes.Scientist;
                return true;
            case "engi":
            case "engineer":
                roleType = RoleTypes.Engineer;
                return true;
            case "angel":
            case "guardianangel":
                roleType = RoleTypes.GuardianAngel;
                return true;
            case "noise":
            case "noisemaker":
                roleType = RoleTypes.Noisemaker;
                return true;
            case "track":
            case "tracker":
                roleType = RoleTypes.Tracker;
                return true;
            case "det":
            case "detective":
                roleType = RoleTypes.Detective;
                return true;
            case "judge":
                roleType = RoleTypes.Judge;
                return true;
            case "spirit":
            case "spiritguide":
                roleType = RoleTypes.SpiritGuide;
                return true;
            default:
                if (normalized.Any(char.IsLetter) && Enum.TryParse(normalized, true, out roleType) &&
                    Enum.IsDefined(typeof(RoleTypes), roleType) &&
                    !roleType.ToString().Equals("NotAssigned", StringComparison.OrdinalIgnoreCase) &&
                    !roleType.ToString().Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    !roleType.ToString().Equals("Invalid", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                error = Translator.Get("invalidRole", input);
                return false;
        }
    }

    public static bool HasCommandAccess(PlayerControl player, OptionItem permission)
    {
        if (player == null || player.Data == null || permission == null) return false;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && player == PlayerControl.LocalPlayer) return true;

        return Utils.CheckAccessLevel(player.Data.FriendCode ?? string.Empty) >= permission.GetValue();
    }

    public static bool HasCommandAccess(PlayerControl player, string command) =>
        HasCommandAccess(player, GetCommandPermission(command));

    public static OptionItem GetCommandPermission(string command)
    {
        string root = command?.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToLowerInvariant();

        return root switch
        {
            "/col" or "/color" or "/colour" or "/colout" => Options.SlashColorCmd,
            "/ucol" or "/ucolor" or "/ucolour" or "/ucolout" or "/copyfit" => Options.SlashUserColorCmd,
            "/give" or "/set" or "/add" => Options.SlashGiveCmd,
            "/rolereset" or "/playerreset" => Options.SlashResetCmd,
            "/impcount" or "/setimp" => Options.SlashImpCountCmd,
            "/voteout" or "/cvoteout" or "/tm" or "/tiemeeting" => Options.SlashVoteOutCmd,
            "/revive" => Options.SlashReviveCmd,
            "/r" or "/roles" or "/0kc" or "/0killcooldown" or "/sns" or "/shiftandseek" or
            "/sns2" or "/shiftandseek2" or "/sr" or "/speedrun" or "/pns" or "/poofandseek" or "/t" => Options.SlashRolesAndGamemodeCmd,
            "/l" or "/lastgame" or "/aur" or "/socials" => Options.SlashLastGameCmd,
            "/seeker" or "/seekers" or "/clearseeker" or "/mapvote" or "/allmapvote" or "/gmvote" or "/vote" => Options.SlashMapAndSeekerCmd,
            "/kick" or "/ckick" => Options.SlashKickCmd,
            "/ban" or "/cban" => Options.SlashBanCmd,
            "/em" or "/endmeeting" or "/sm" or "/skip" or "/skipmeeting" => Options.SlashEndMeetingCmd,
            "/s" or "/start" or "/eg" or "/endgame" or "/private" or "/public" => Options.SlashStartAndEndGameCmd,
            "/vip" or "/moderator" or "/admin" or "/removevip" or "/removemoderator" or
            "/removeadmin" or "/whitelist" => Options.SlashAdminListCmd,
            "/kill" => Options.SlashKillCmd,
            "/settings" => Options.SlashSettingsCmd,
            "/help" or "/page" or "/dump" or "/reload" => Options.SlashUtilityCmd,
            _ => Options.SlashUtilityCmd
        };
    }

    private static void ApplyRoleChange(PlayerControl target, RoleTypes roleType, bool canOverrideRole)
    {
        if (target == null || target.Data == null) return;

        try
        {
            target.Data.RoleType = roleType;
        }
        catch
        {
            // Some builds keep the role type read-only on the client object; keep falling through to RPC-based assignment.
        }

        try
        {
            target.RpcSetRole(roleType, canOverrideRole);
        }
        catch
        {
            // The normal RPC path may be blocked in some runtime states; continue to explicit reflection fallback below.
        }

        MethodInfo[] setRoleMethods = typeof(PlayerControl)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "SetRole" && method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(RoleTypes))
            .ToArray();

        foreach (MethodInfo setRoleMethod in setRoleMethods)
        {
            try
            {
                ParameterInfo[] parameters = setRoleMethod.GetParameters();
                object[] arguments = new object[parameters.Length];
                arguments[0] = roleType;

                for (int i = 1; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(bool)) arguments[i] = canOverrideRole;
                    else if (parameterType == typeof(int)) arguments[i] = 0;
                    else if (parameterType == typeof(byte)) arguments[i] = (byte)0;
                    else if (parameterType == typeof(string)) arguments[i] = string.Empty;
                    else if (parameterType.IsValueType) arguments[i] = Activator.CreateInstance(parameterType);
                    else arguments[i] = null;
                }

                setRoleMethod.Invoke(target, arguments);
            }
            catch (TargetInvocationException)
            {
                // Try the next overload if this build's role setter rejects the arguments or has stricter runtime requirements.
            }
            catch (TargetException)
            {
                // Same fallback when the target runtime rejects the invocation.
            }
            catch (MethodAccessException)
            {
                // Some runtime builds keep the setter non-public.
            }
            catch (Exception)
            {
                // Keep trying every compatible overload until the runtime accepts one.
            }
        }
    }

    public static bool HandleRoleCommand(PlayerControl sender, string text, bool nextRound, bool additive = false)
    {
        if (!HasCommandAccess(sender, Options.SlashGiveCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", nextRound ? additive ? "/add" : "/set" : "/give"));
            return false;
        }

        string arguments = text.Substring(nextRound ? 5 : 6).Trim();
        string[] roleParts = arguments.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (roleParts.Length == 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get(nextRound ? additive ? "addSyntax" : "setSyntax" : "giveSyntax"));
            return false;
        }

        if (!TryResolveRole(roleParts[0], out RoleTypes roleType, out string roleError))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, roleError);
            return false;
        }

        string targetText = roleParts.Length == 1 ? "@me" : roleParts[1].Trim();
        bool commaSeparated = targetText.Contains(',');
        List<string> playerNames = new();
        if (commaSeparated)
        {
            playerNames.AddRange(targetText.Split(',').Select(name => name.Trim().Trim('"')));
        }
        else
        {
            System.Text.StringBuilder name = new();
            bool insideQuotes = false;
            foreach (char character in targetText)
            {
                if (character == '"')
                {
                    insideQuotes = !insideQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(character) && !insideQuotes)
                {
                    if (name.Length > 0)
                    {
                        playerNames.Add(name.ToString());
                        name.Clear();
                    }
                    continue;
                }

                name.Append(character);
            }

            if (name.Length > 0) playerNames.Add(name.ToString());
        }

        playerNames.RemoveAll(string.IsNullOrWhiteSpace);
        if (playerNames.Count == 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get(nextRound ? additive ? "addSyntax" : "setSyntax" : "giveSyntax"));
            return false;
        }

        HashSet<byte> seenPlayers = new();
        List<PlayerControl> targets = new();
        foreach (string playerName in playerNames)
        {
            if (!TryResolvePlayerTargets(playerName, true, out List<PlayerControl> resolvedPlayers, out string playerError, sender))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, playerError);
                return false;
            }

            foreach (PlayerControl target in resolvedPlayers)
            {
                if (seenPlayers.Add(target.PlayerId)) targets.Add(target);
            }
        }

        if (nextRound && !additive)
        {
            PlayerControlSetRolePatch.PendingRoleAssignments.Clear();
            PlayerControlSetRolePatch.ExclusiveRolePlayers.Clear();
        }

        if (nextRound && !PlayerControlSetRolePatch.ExclusiveRolePlayers.ContainsKey(roleType))
        {
            PlayerControlSetRolePatch.ExclusiveRolePlayers[roleType] = new HashSet<byte>();
        }

        foreach (PlayerControl target in targets)
        {
            if (nextRound)
            {
                foreach (var group in PlayerControlSetRolePatch.ExclusiveRolePlayers.Values)
                {
                    group.Remove(target.PlayerId);
                }

                PlayerControlSetRolePatch.PendingRoleAssignments[target.PlayerId] = roleType;
                PlayerControlSetRolePatch.ExclusiveRolePlayers[roleType].Add(target.PlayerId);
            }
            else
            {
                OriginalRolesBeforeGive.TryAdd(target.PlayerId, target.Data.RoleType);
                ApplyRoleChange(target, roleType, true);
                Logger.Info($" {sender.Data.PlayerName} gave {target.Data.PlayerName} the {roleType} role immediately", "RoleCommand");
            }

            if (!nextRound)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("roleGivenNow", target.Data.PlayerName, roleType.ToString()));
            }
        }

        if (nextRound)
        {
            string targetSummary = targetText.Equals("@all", StringComparison.OrdinalIgnoreCase)
                ? "@all"
                : string.Join(", ", targets.Select(target => target.Data.PlayerName));
            string result = additive
                ? $"Added {targetSummary} to {roleType} for the next game."
                : $"Set {roleType}s to {targetSummary} for the next game.";
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, result);
            Logger.Info(result, "RoleCommand");
        }

        return true;
    }

    public static bool HandleRoleResetCommand(PlayerControl sender, string roleName)
    {
        if (!HasCommandAccess(sender, Options.SlashResetCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/rolereset"));
            return false;
        }

        if (!TryResolveRole(roleName, out RoleTypes roleType, out string error))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return false;
        }

        PlayerControlSetRolePatch.ExclusiveRolePlayers.Remove(roleType);
        foreach (byte playerId in PlayerControlSetRolePatch.PendingRoleAssignments
            .Where(assignment => assignment.Value == roleType)
            .Select(assignment => assignment.Key)
            .ToArray())
        {
            PlayerControlSetRolePatch.PendingRoleAssignments.Remove(playerId);
        }

        if (Utils.InGame)
        {
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player?.Data == null || player.Data.RoleType != roleType ||
                    !OriginalRolesBeforeGive.TryGetValue(player.PlayerId, out RoleTypes originalRole)) continue;

                ApplyRoleChange(player, originalRole, false);
                OriginalRolesBeforeGive.Remove(player.PlayerId);
            }
        }

        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("roleResetSuccess", roleType.ToString()));
        return true;
    }

    public static bool HandlePlayerResetCommand(PlayerControl sender, string playerName)
    {
        if (!HasCommandAccess(sender, Options.SlashResetCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/playerreset"));
            return false;
        }

        if (!TryResolvePlayerTargets(playerName, false, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return false;
        }

        foreach (PlayerControl target in targets)
        {
            PlayerControlSetRolePatch.PendingRoleAssignments.Remove(target.PlayerId);
            foreach (var group in PlayerControlSetRolePatch.ExclusiveRolePlayers.Values)
            {
                group.Remove(target.PlayerId);
            }

            if (OriginalPlayerColors.TryGetValue(target.PlayerId, out byte originalColor))
            {
                target.RpcSetColor(originalColor);
                OriginalPlayerColors.Remove(target.PlayerId);
            }

            if (Utils.InGame && OriginalRolesBeforeGive.TryGetValue(target.PlayerId, out RoleTypes originalRole))
            {
                ApplyRoleChange(target, originalRole, false);
            }
            OriginalRolesBeforeGive.Remove(target.PlayerId);
        }

        foreach (RoleTypes emptyGroup in PlayerControlSetRolePatch.ExclusiveRolePlayers
            .Where(group => group.Value.Count == 0)
            .Select(group => group.Key)
            .ToArray())
        {
            PlayerControlSetRolePatch.ExclusiveRolePlayers.Remove(emptyGroup);
        }

        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("playerResetSuccess", targets[0].Data.PlayerName));
        return true;
    }

    public static bool HandleImpostorCountCommand(PlayerControl sender, string text)
    {
        string command = text.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToLowerInvariant();
        if (command is not ("/impcount" or "/setimp")) return false;

        if (!HasCommandAccess(sender, Options.SlashImpCountCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", command));
            return false;
        }

        if (!Utils.IsLobby)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("impCountLobbyOnly"));
            return false;
        }

        string argument = text.Substring(command.Length).Trim();
        if (!int.TryParse(argument, out int count) || count < 0 || count > 15)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Usage: {command} {{0-15}}");
            return false;
        }

        PlayerControlSetRolePatch.DesiredImpostorCount = count;
        int presetImpostors = PlayerControlSetRolePatch.GetQueuedImpostorCount();
        string presetNote = presetImpostors > 0
            ? $" {presetImpostors} pre-set impostor role(s) will be kept; unassigned players will be randomly rolled."
            : " Remaining players will be randomly rolled as usual.";
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Desired impostor count set to {count}.{presetNote}");
        return true;
    }

    public static bool HandleSettingsCommand(PlayerControl sender, string text)
    {
        if (!HasCommandAccess(sender, Options.SlashSettingsCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/settings"));
            return false;
        }
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || Main.NormalOptions == null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "/settings requires the host in a connected normal-mode lobby or game.");
            return false;
        }

        if (Utils.isHideNSeek)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "/settings currently supports normal-mode games, not Hide and Seek.");
            return false;
        }

        string[] arguments = text.Substring("/settings".Length).Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (arguments.Length == 1 && arguments[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            ShowPagedOutput(sender, text,
                "Settings you can change (normal mode; host only; lobby or during a game):\n" +
                "impostorvision 0-600 | crewmatevision 0-600 | playerspeed 0-600 | killcooldown 0-600\n" +
                "commontasks 0-600 | shorttasks 0-600 | longtasks 0-600 | emergencymeetings 0-600\n" +
                "emergencycooldown 0-600 | discussiontime 0-600 | votingtime 0-600\n" +
                "Usage: /settings <setting> <value>");
            return true;
        }
        if (arguments.Length != 2)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Usage: /settings help  OR  /settings <setting> <value>");
            return false;
        }

        string key = new string(arguments[0].Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        string valueText = arguments[1];
        if (!float.TryParse(valueText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"'{valueText}' is not a valid number.");
            return false;
        }

        string settingName;
        bool isInteger;
        float minimum;
        float maximum;
        switch (key)
        {
            case "impostorvision":
            case "impostorvis":
            case "impvis":
                settingName = "impostorvision"; isInteger = false; minimum = 0f; maximum = 600f; break;
            case "crewmatevision":
            case "crewmatevis":
            case "crewvis":
                settingName = "crewmatevision"; isInteger = false; minimum = 0f; maximum = 600f; break;
            case "playerspeed":
            case "speed":
                settingName = "playerspeed"; isInteger = false; minimum = 0f; maximum = 600f; break;
            case "killcooldown":
                settingName = "killcooldown"; isInteger = false; minimum = 0f; maximum = 600f; break;
            case "commontasks":
                settingName = "commontasks"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "shorttasks":
                settingName = "shorttasks"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "longtasks":
                settingName = "longtasks"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "emergencymeetings":
            case "emergencymeeting":
            case "emergencies":
                settingName = "emergencymeetings"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "emergencycooldown":
                settingName = "emergencycooldown"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "discussiontime":
                settingName = "discussiontime"; isInteger = true; minimum = 0f; maximum = 600f; break;
            case "votingtime":
            case "voting":
                settingName = "votingtime"; isInteger = true; minimum = 0f; maximum = 600f; break;
            default:
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Unknown setting '{arguments[0]}'. Use /settings help.");
                return false;
        }

        if (value < minimum || value > maximum || (isInteger && value != MathF.Truncate(value)))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer,
                $"{settingName} must be {(isInteger ? "an integer" : "a number")} from {minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)} to {maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            return false;
        }

        int integerValue = (int)value;
        switch (settingName)
        {
            case "impostorvision": Main.NormalOptions.ImpostorLightMod = value; break;
            case "crewmatevision": Main.NormalOptions.CrewLightMod = value; break;
            case "playerspeed": Main.NormalOptions.PlayerSpeedMod = value; break;
            case "killcooldown": Main.NormalOptions.KillCooldown = value; break;
            case "commontasks": Main.NormalOptions.NumCommonTasks = integerValue; break;
            case "shorttasks": Main.NormalOptions.NumShortTasks = integerValue; break;
            case "longtasks": Main.NormalOptions.NumLongTasks = integerValue; break;
            case "emergencymeetings": Main.NormalOptions.NumEmergencyMeetings = integerValue; break;
            case "emergencycooldown": Main.NormalOptions.EmergencyCooldown = integerValue; break;
            case "discussiontime": Main.NormalOptions.DiscussionTime = integerValue; break;
            case "votingtime": Main.NormalOptions.VotingTime = integerValue; break;
        }

        bool synced = TrySyncCurrentGameOptions();

        string formattedValue = isInteger ? integerValue.ToString() : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, synced
            ? $"Set {settingName} to {formattedValue} and synced the game options."
            : $"Set {settingName} to {formattedValue}, but the game options could not be synced; check the mod log.");
        Logger.Info($" {sender.Data.PlayerName} set {settingName} to {formattedValue}", "SettingsCommand");
        return true;
    }

    private static bool TrySyncCurrentGameOptions()
    {
        try
        {
            GameOptionsManager manager = GameOptionsManager.Instance;
            FieldInfo factoryField = typeof(GameOptionsManager).GetField("gameOptionsFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object factory = factoryField?.GetValue(manager);
            MethodInfo serializeOptions = factory?.GetType().GetMethod("ToBytes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(IGameOptions), typeof(bool) }, null);
            if (factory == null || serializeOptions == null || Main.NormalOptions == null) return false;

            byte[] optionsBytes = serializeOptions.Invoke(factory, new object[] { Main.NormalOptions, false }) as byte[];
            if (optionsBytes == null || optionsBytes.Length == 0) return false;

            PlayerControl.LocalPlayer.RpcSyncSettings(optionsBytes);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "SettingsCommandSync");
            return false;
        }
    }

    public static bool HandleVoteOutCommand(PlayerControl sender, string text, bool byColor)
    {
        string command = byColor ? "/cvoteout" : "/voteout";
        if (!HasCommandAccess(sender, Options.SlashVoteOutCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", command));
            return false;
        }

        if (!Utils.IsMeeting || MeetingHud.Instance == null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("voteOutMeetingOnly"));
            return false;
        }

        string argument = text.Substring(command.Length).Trim();
        PlayerControl target = null;
        if (byColor)
        {
            string colorSelector = argument.StartsWith("!color:", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("@", StringComparison.Ordinal)
                ? argument
                : "!color:" + argument;
            if (!TryResolvePlayerTargets(colorSelector, true, out List<PlayerControl> colorTargets, out string colorError, sender))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, colorError);
                return false;
            }
            List<PlayerControl> matches = colorTargets.Where(player => player?.Data != null && !player.Data.IsDead).ToList();
            if (matches.Count > 1)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The /cvoteout command only accepts one living target; this selector matched multiple players.");
                return false;
            }
            if (matches.Count == 1) target = matches[0];
        }
        else if (!TryResolvePlayerTargets(argument, false, out List<PlayerControl> targets, out string playerError, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, playerError);
            return false;
        }
        else
        {
            if (targets.Count != 1)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The /voteout command requires exactly one target; choose a specific player instead of a color group.");
                return false;
            }
            target = targets.FirstOrDefault(player => player != null && player.Data != null && !player.Data.IsDead);
        }

        if (target == null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("voteOutTargetUnavailable"));
            return false;
        }

        List<MeetingHud.VoterState> votes = new();
        foreach (PlayerControl voter in PlayerControl.AllPlayerControls)
        {
            if (voter == null || voter.Data == null || voter.Data.IsDead) continue;
            votes.Add(new MeetingHud.VoterState { VoterId = voter.PlayerId, VotedForId = target.PlayerId });
        }

        MeetingHud.Instance.RpcVotingComplete(votes.ToArray(), target.Data, false, false, 0);
        Logger.Info($" {sender.Data.PlayerName} called a vote-out for {target.Data.PlayerName}", "VoteOutCommand");
        return true;
    }

    public static bool TryRevivePlayer(PlayerControl sender, string targetName = null)
    {
        if (!HasCommandAccess(sender, Options.SlashReviveCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/revive"));
            return false;
        }

        if (!AmongUsClient.Instance.AmHost || !Utils.InGame || Utils.IsMeeting || ExileController.Instance || ShipStatus.Instance == null)
        {
            return false;
        }

        if (!TryResolvePlayerTargets(string.IsNullOrWhiteSpace(targetName) ? "@me" : targetName,
            true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return false;
        }

        List<PlayerControl> deadTargets = targets.Where(player => player?.Data != null && player.Data.IsDead).ToList();
        if (deadTargets.Count == 0)
        {
            string subject = targets.Count > 0 ? targets[0].Data?.PlayerName ?? "That player" : "That player";
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"{subject} is not a dead player.");
            return false;
        }

        foreach (PlayerControl player in deadTargets)
        {
            RoleTypes roleToRestore = GetRoleToRestoreAfterRevive(player);
            player.Revive();
            if (player.Data.RoleType != roleToRestore)
            {
                ApplyRoleChange(player, roleToRestore, true);
            }

            if (player.NetTransform != null)
            {
                player.NetTransform.RpcSnapTo(ShipStatus.Instance.MeetingSpawnCenter);
            }

            Logger.Info($" {sender.Data.PlayerName} revived {player.Data.PlayerName} with /revive", "ReviveCommand");
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("reviveSuccess", player.Data.PlayerName));
        }

        return true;
    }

    public static bool HandleTieMeetingCommand(PlayerControl sender)
    {
        if (!HasCommandAccess(sender, Options.SlashVoteOutCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/tm"));
            return false;
        }

        MeetingHud meeting = MeetingHud.Instance;
        if (!Utils.IsMeeting || meeting == null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The /tm command can only be used during a meeting.");
            return false;
        }

        List<MeetingHud.VoterState> votes = new();
        foreach (PlayerControl voter in PlayerControl.AllPlayerControls)
        {
            if (voter?.Data == null || voter.Data.IsDead) continue;
            votes.Add(new MeetingHud.VoterState { VoterId = voter.PlayerId, VotedForId = byte.MaxValue });
        }

        meeting.RpcVotingComplete(votes.ToArray(), null, true, false, 0);
        Logger.Info($" {sender.Data.PlayerName} forced a tie with /tm", "TieMeetingCommand");
        return true;
    }

    public static bool HandleCopyFitCommand(PlayerControl sender, string text)
    {
        if (!HasCommandAccess(sender, Options.SlashUserColorCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/copyfit"));
            return false;
        }

        string arguments = text.Substring("/copyfit".Length).Trim();
        string sourceName;
        string targetNames;
        if (arguments.StartsWith("\"", StringComparison.Ordinal))
        {
            int closingQuote = arguments.IndexOf('"', 1);
            if (closingQuote < 0)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Syntax: /copyfit source target1,target2,...");
                return false;
            }
            sourceName = arguments.Substring(1, closingQuote - 1);
            targetNames = arguments.Substring(closingQuote + 1).Trim();
        }
        else
        {
            int separator = arguments.IndexOf(' ');
            if (separator < 0)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Syntax: /copyfit source target1,target2,...");
                return false;
            }
            sourceName = arguments.Substring(0, separator);
            targetNames = arguments.Substring(separator + 1).Trim();
        }

        if (!TryResolvePlayerTargets(sourceName, false, out List<PlayerControl> sources, out string sourceError, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, sourceError);
            return false;
        }
        if (sources.Count != 1)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "/copyfit requires exactly one source player; choose one exact name.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(targetNames))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Syntax: /copyfit source target1,target2,...");
            return false;
        }

        List<PlayerControl> targets = new();
        HashSet<byte> seen = new();
        foreach (string targetName in targetNames.Split(',').Select(name => name.Trim().Trim('"')))
        {
            if (!TryResolvePlayerTargets(targetName, true, out List<PlayerControl> resolved, out string error, sender))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
                return false;
            }
            foreach (PlayerControl target in resolved)
            {
                if (seen.Add(target.PlayerId)) targets.Add(target);
            }
        }

        NetworkedPlayerInfo.PlayerOutfit sourceOutfit = sources[0].Data.DefaultOutfit;
        foreach (PlayerControl target in targets)
        {
            target.RpcSetColor((byte)sourceOutfit.ColorId);
            target.RpcSetHat(sourceOutfit.HatId);
            target.RpcSetSkin(sourceOutfit.SkinId);
            target.RpcSetVisor(sourceOutfit.VisorId);
            target.RpcSetPet(sourceOutfit.PetId);
        }

        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Copied {sources[0].Data.PlayerName}'s outfit to {targets.Count} player(s).");
        return true;
    }

    public static bool HandleKillCommand(PlayerControl sender, string playerName)
    {
        if (!HasCommandAccess(sender, Options.SlashKillCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/kill"));
            return false;
        }
        if (!AmongUsClient.Instance.AmHost || !Utils.InGame || Utils.IsMeeting || MeetingHud.Instance != null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The /kill command is only available to the host during a game outside meetings.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(playerName))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Syntax: /kill {player}");
            return false;
        }
        if (!TryResolvePlayerTargets(playerName, false, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return false;
        }
        List<PlayerControl> aliveTargets = targets.Where(target => target?.Data != null && !target.Data.IsDead).ToList();
        if (aliveTargets.Count == 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "No living players matched that target.");
            return false;
        }

        EACR.BeginCommandGrace();
        foreach (PlayerControl target in aliveTargets)
        {
            PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
            Logger.Info($" {sender.Data.PlayerName} used /kill on {target.Data.PlayerName}", "KillCommand");
        }
        return true;
    }

    public static bool HandleKickBanCommand(PlayerControl sender, string text)
    {
        string root = text.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToLowerInvariant();
        bool isKick = root is "/kick" or "/ckick";
        bool isBan = root is "/ban" or "/cban";
        if (!isKick && !isBan) return false;

        OptionItem permission = isBan ? Options.SlashBanCmd : Options.SlashKickCmd;
        if (!HasCommandAccess(sender, permission))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
            return true;
        }

        string argument = text.Substring(root.Length).Trim();
        if (string.IsNullOrWhiteSpace(argument))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Usage: {root} <player|@all|@me|!color:name>");
            return true;
        }

        if (root is "/ckick" or "/cban" && !argument.StartsWith("!color:", StringComparison.OrdinalIgnoreCase) &&
            !argument.StartsWith('@') && Utils.TryGetColorId(argument, out _))
        {
            argument = "!color:" + argument;
        }

        if (!TryResolvePlayerTargets(argument, true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return true;
        }

        bool protectPrivilegedPlayers = sender != PlayerControl.LocalPlayer;
        targets = targets
            .Where(target => target?.Data != null && target != PlayerControl.LocalPlayer &&
                (!protectPrivilegedPlayers || Utils.CheckAccessLevel(target.Data.FriendCode ?? string.Empty) == 0))
            .GroupBy(target => target.PlayerId)
            .Select(group => group.First())
            .ToList();
        if (targets.Count == 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "No eligible target players were matched.");
            return true;
        }

        foreach (PlayerControl target in targets)
        {
            AmongUsClient.Instance.KickPlayer(target.Data.ClientId, isBan);
            Logger.Info($"{sender.Data.PlayerName} {(isBan ? "banned" : "kicked")} {target.Data.PlayerName} with {root}.", "Kick&BanCommand");
        }

        string result = $"{(isBan ? "Banned" : "Kicked")} {targets.Count} player(s): {string.Join(", ", targets.Select(target => target.Data.PlayerName))}.";
        if (LocalCommandPanel.IsCapturingCommandOutput) LocalCommandPanel.CaptureCommandOutput(result);
        else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, result);
        return true;
    }

    public static bool HandleAccessListPlayerCommand(PlayerControl sender, string command, string playerSelector)
    {
        string filePath = command switch
        {
            "/vip" or "/removevip" => BanManager.VipListPath,
            "/moderator" or "/removemoderator" => BanManager.ModeratorListPath,
            "/admin" or "/removeadmin" => BanManager.AdminListPath,
            _ => null
        };
        if (filePath == null) return false;

        bool remove = command.StartsWith("/remove", StringComparison.OrdinalIgnoreCase);
        if (!TryResolvePlayerTargets(playerSelector, true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return true;
        }

        List<string> lines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();
        List<string> changedNames = new();
        foreach (PlayerControl target in targets)
        {
            string friendCode = target.Data?.FriendCode?.Trim();
            if (string.IsNullOrWhiteSpace(friendCode)) continue;

            if (remove)
            {
                int removed = lines.RemoveAll(line => line.Trim().Equals(friendCode, StringComparison.OrdinalIgnoreCase));
                if (removed > 0) changedNames.Add(target.Data.PlayerName);
            }
            else if (!lines.Any(line => line.Trim().Equals(friendCode, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(friendCode);
                changedNames.Add(target.Data.PlayerName);
            }
        }

        if (changedNames.Count == 0)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, remove
                ? "No matching players were on that access list."
                : "No matching players could be added to that access list.");
            return true;
        }

        File.WriteAllLines(filePath, lines);
        string role = command.Contains("vip", StringComparison.OrdinalIgnoreCase) ? "VIP" :
            command.Contains("moderator", StringComparison.OrdinalIgnoreCase) ? "Moderator" : "Admin";
        string result = $"{(remove ? "Removed" : "Added")} {string.Join(", ", changedNames)} {(remove ? "from" : "to")} the {role} list.";
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, result);
        Logger.Info(result, "AccessListCommand");
        return true;
    }

    public static bool HandleSeekerCommand(PlayerControl sender, string playerSelector)
    {
        if (!HasCommandAccess(sender, Options.SlashMapAndSeekerCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/seeker"));
            return true;
        }
        if (!Utils.IsLobby || !Utils.isHideNSeek) return true;
        if (!TryResolvePlayerTargets(playerSelector, true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return true;
        }

        foreach (PlayerControl target in targets) PlayerControlSetRolePatch.manualSeekers.Add(target.PlayerId);
        List<string> names = new();
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data != null && PlayerControlSetRolePatch.manualSeekers.Contains(player.PlayerId))
                names.Add(player.Data.PlayerName);
        }
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("seekers") + string.Join(", ", names));
        return true;
    }

    public static string ExecutePrivateCommand(PlayerControl sender, string command)
    {
        if (sender?.Data == null || string.IsNullOrWhiteSpace(command)) return "Invalid private command request.";
        if (!AmongUsClient.Instance.AmHost) return "Only a modded lobby host can execute private commands.";
        if (command.Length > 120 || !command.StartsWith("/", StringComparison.Ordinal)) return "Invalid private command format or length.";
        if (!LocalCommandPanel.IsAllowedConsoleCommand(command)) return "Unknown or unsupported private command.";
        string commandRoot = command.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        if (!HasCommandAccess(sender, commandRoot)) return Translator.Get("accessDenied", commandRoot);
        if (!LocalCommandPanel.TryBeginCommandOutputCapture()) return "The host is busy processing another command.";

        string additionalOutput = string.Empty;
        try
        {
            string text = command.Trim();
            string root = commandRoot;
            switch (root)
            {
                case "/help":
                    LocalCommandPanel.CaptureCommandOutput(
                        $"{Translator.Get("allCommandsFull")}\n\n{Translator.Get("recentCommandsHelp")}\n\n{Translator.Get("meetingCommandsHelp")}");
                    break;
                case "/give":
                    HandleRoleCommand(sender, text, false);
                    break;
                case "/set":
                    HandleRoleCommand(sender, text, true);
                    break;
                case "/add":
                    HandleRoleCommand(sender, text, true, true);
                    break;
                case "/rolereset":
                    HandleRoleResetCommand(sender, text.Substring(root.Length).Trim());
                    break;
                case "/playerreset":
                    HandlePlayerResetCommand(sender, text.Substring(root.Length).Trim());
                    break;
                case "/voteout":
                    HandleVoteOutCommand(sender, text, false);
                    break;
                case "/cvoteout":
                    HandleVoteOutCommand(sender, text, true);
                    break;
                case "/revive":
                    TryRevivePlayer(sender, text.Length > root.Length ? text.Substring(root.Length).Trim() : null);
                    break;
                case "/copyfit":
                    HandleCopyFitCommand(sender, text);
                    break;
                case "/kill":
                    HandleKillCommand(sender, text.Substring(root.Length).Trim());
                    break;
                case "/tm":
                case "/tiemeeting":
                    HandleTieMeetingCommand(sender);
                    break;
                case "/impcount":
                case "/setimp":
                    HandleImpostorCountCommand(sender, text);
                    break;
                case "/settings":
                    HandleSettingsCommand(sender, text);
                    break;
                case "/bind":
                    CommandBindings.HandleCommand(sender, text);
                    break;
                case "/em":
                case "/endmeeting":
                    HandleMeetingEndCommand(sender, true);
                    break;
                case "/sm":
                case "/skip":
                case "/skipmeeting":
                    HandleMeetingEndCommand(sender, false);
                    break;
                case "/color":
                case "/colour":
                case "/col":
                case "/colout":
                    if (!HandleRainbowColorCommand(sender, text))
                    {
                        string colorArgument = text.Substring(root.Length).Trim();
                        if (!HasCommandAccess(sender, Options.SlashColorCmd))
                            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
                        else if (!Utils.TryGetColorId(colorArgument, out byte colorId))
                            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", colorArgument));
                        else if (colorId > 17 && !Options.AllowFortegreen.GetBool())
                            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", colorArgument));
                        else
                        {
                            sender.RpcSetColor(colorId);
                            additionalOutput = $"Set {sender.Data.PlayerName}'s color to {colorArgument}.";
                        }
                    }
                    break;
                case "/ucolor":
                case "/ucolour":
                case "/ucol":
                case "/ucolout":
                    if (!HandleRainbowColorCommand(sender, text))
                        ExecutePrivateUserColorCommand(sender, text, root);
                    break;
                case "/start":
                case "/s":
                    if (Utils.CheckAccessLevel(sender.Data.FriendCode ?? string.Empty) < Options.SlashStartAndEndGameCmd.GetValue())
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
                    else if (Utils.IsLobby) GameStartManager.Instance.BeginGame();
                    else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The start command is only available in a lobby.");
                    break;
                case "/endgame":
                case "/eg":
                    if (Utils.CheckAccessLevel(sender.Data.FriendCode ?? string.Empty) < Options.SlashStartAndEndGameCmd.GetValue())
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
                    else if (Utils.InGame)
                    {
                        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                    }
                    else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "The game is not in progress.");
                    break;
                case "/mapvote":
                    if (Utils.CheckAccessLevel(sender.Data.FriendCode ?? string.Empty) < Options.SlashMapAndSeekerCmd.GetValue())
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
                    else if (Utils.IsLobby) Utils.MapVote(true);
                    else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Map voting is only available in a lobby.");
                    break;
                case "/gmvote":
                    if (Utils.CheckAccessLevel(sender.Data.FriendCode ?? string.Empty) < Options.SlashMapAndSeekerCmd.GetValue())
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", root));
                    else if (Utils.IsLobby) Utils.ModeVote(true);
                    else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Mode voting is only available in a lobby.");
                    break;
                default:
                    additionalOutput = $"{root} is not available through the private mod-to-host command channel.";
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "PrivateCommandDispatch");
            additionalOutput = "Private command failed; check the host mod log.";
        }

        if (!string.IsNullOrWhiteSpace(additionalOutput)) LocalCommandPanel.CaptureCommandOutput(additionalOutput);
        return LocalCommandPanel.EndCommandOutputCapture();
    }

    private static void ExecutePrivateUserColorCommand(PlayerControl sender, string text, string command)
    {
        if (!HasCommandAccess(sender, Options.SlashUserColorCmd))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", command));
            return;
        }

        string arguments = text.Substring(command.Length).Trim();
        string[] parts = arguments.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Utils.TryGetColorId(parts[0], out byte colorId))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Usage: /ucolor <color> [player|@all]");
            return;
        }
        if (colorId > 17 && !Options.AllowFortegreen.GetBool())
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", parts[0]));
            return;
        }

        string targetArgument = parts.Length > 1 ? parts[1] : "@me";
        if (!TryResolvePlayerTargets(targetArgument, true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return;
        }
        foreach (PlayerControl target in targets) target.RpcSetColor(colorId);
        LocalCommandPanel.CaptureCommandOutput($"Set the color for {targets.Count} player(s) to {parts[0]}.");
    }

    public static bool HandleRainbowColorCommand(PlayerControl sender, string text)
    {
        string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        string mode = parts[1];
        bool start = mode.Equals("rainbow", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("gay", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("rainbowa", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("rainbow:", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("gay:", StringComparison.OrdinalIgnoreCase);
        bool stop = mode.Equals("stop", StringComparison.OrdinalIgnoreCase);
        if (!start && !stop) return false;

        bool userColor = parts[0] is "/ucol" or "/ucolor" or "/ucolour" or "/ucolout";
        OptionItem permission = userColor ? Options.SlashUserColorCmd : Options.SlashColorCmd;
        if (userColor ? !HasCommandAccess(sender, permission) :
            Utils.CheckAccessLevel(sender?.Data?.FriendCode ?? string.Empty) < permission.GetValue())
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", parts[0]));
            return true;
        }

        List<string> remaining = parts.Skip(2).ToList();
        string targetArgument = "@me";
        if (userColor && remaining.Count > 0)
        {
            string first = remaining[0];
            bool matchesPlayer = HasExactPlayerName(first);
            bool isColorList = first.Contains(',') || byte.TryParse(first, out _) || Utils.TryGetColorId(first, out _);
            bool firstIsTarget = first.StartsWith("@", StringComparison.Ordinal) ||
                first.StartsWith("!color:", StringComparison.OrdinalIgnoreCase) || matchesPlayer || !isColorList;

            if (firstIsTarget)
            {
                targetArgument = first;
                remaining.RemoveAt(0);
            }
        }

        string encodedPalette = null;
        string encodedInterval = null;
        if (mode.Contains(':'))
        {
            string[] modeSettings = mode.Split(':');
            if (modeSettings.Length != 3 || !(modeSettings[0].Equals("rainbow", StringComparison.OrdinalIgnoreCase) ||
                modeSettings[0].Equals("gay", StringComparison.OrdinalIgnoreCase)))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Rainbow format: /color rainbow [red,blue,...] [milliseconds]");
                return true;
            }
            encodedPalette = modeSettings[1];
            encodedInterval = modeSettings[2];
        }

        if (userColor && remaining.Count > 0 && !string.IsNullOrWhiteSpace(encodedPalette) &&
            !remaining[0].Contains(',') && !byte.TryParse(remaining[0], out _) && Utils.TryGetColorId(remaining[0], out _) &&
            HasExactPlayerName(remaining[0]))
        {
            targetArgument = remaining[0];
            remaining.RemoveAt(0);
        }

        if (!TryResolvePlayerTargets(targetArgument, true, out List<PlayerControl> targets, out string error, sender))
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
            return true;
        }

        byte[] rainbowColors = null;
        int intervalMs = 100;
        if (start)
        {
            string colorList = encodedPalette;
            string intervalText = encodedInterval;
            if (remaining.Count > 0 && !string.IsNullOrWhiteSpace(remaining[0]))
            {
                colorList ??= remaining[0];
                if (remaining.Count > 1) intervalText ??= remaining[1];
            }

            if (!string.IsNullOrWhiteSpace(colorList))
            {
                List<byte> parsedColors = new();
                foreach (string value in colorList.Split(','))
                {
                    bool validColor = byte.TryParse(value.Trim(), out byte colorId) && colorId <= 18;
                    if (!validColor && Utils.TryGetColorId(value.Trim(), out byte namedColor))
                    {
                        colorId = namedColor;
                        validColor = true;
                    }
                    if (!validColor || colorId > 18 ||
                        (colorId == 18 && !Options.AllowFortegreen.GetBool()))
                    {
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Invalid rainbow color id: {value}");
                        return true;
                    }
                    if (!parsedColors.Contains(colorId)) parsedColors.Add(colorId);
                }
                if (parsedColors.Count < 2)
                {
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Choose at least two colors for a rainbow; leave the list empty to use the default sequence.");
                    return true;
                }
                rainbowColors = parsedColors.ToArray();
            }

            if (!string.IsNullOrWhiteSpace(intervalText))
            {
                string numericInterval = intervalText.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
                    ? intervalText.Substring(0, intervalText.Length - 2)
                    : intervalText;
                if (!int.TryParse(numericInterval, out intervalMs) || intervalMs < 50 || intervalMs > 5000)
                {
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, "Rainbow interval must be between 50 and 5000 milliseconds.");
                    return true;
                }
            }
        }

        foreach (PlayerControl target in targets)
        {
            if (start) RainbowColorCycle.Start(target, rainbowColors, intervalMs);
            else RainbowColorCycle.Stop(target);
        }

        string targetSummary = targetArgument.Equals("@all", StringComparison.OrdinalIgnoreCase)
            ? "@all"
            : targets.Count == 1 ? targets[0].Data.PlayerName : $"{targets.Count} players";
        string colorMode = mode.StartsWith("gay", StringComparison.OrdinalIgnoreCase) ? "gay" : "rainbow";
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, start
            ? $"Color {colorMode} applied to {targetSummary}."
            : $"Rainbow color stopped for {targetSummary}.");

        return true;
    }
}

internal static class RainbowColorCycle
{
    private static readonly byte[] ColorSequence = { 0, 4, 5, 11, 2, 10, 1, 8, 3, 13 };
    private sealed class CycleState
    {
        public byte[] Colors { get; init; }
        public int IntervalMs { get; init; }
        public int ColorIndex { get; set; }
        public DateTime LastChangeUtc { get; set; }
    }

    private static readonly Dictionary<byte, CycleState> CurrentColors = new();
    private static bool updateScheduled;

    public static void Start(PlayerControl player, byte[] colors = null, int intervalMs = 100)
    {
        if (player?.Data == null) return;
        byte[] sequence = colors == null || colors.Length < 2 ? ColorSequence.ToArray() : colors.ToArray();
        int boundedInterval = Math.Clamp(intervalMs, 50, 5000);
        CurrentColors[player.PlayerId] = new CycleState
        {
            Colors = sequence,
            IntervalMs = boundedInterval,
            ColorIndex = 0,
            LastChangeUtc = DateTime.UtcNow
        };
        player.RpcSetColor(sequence[0]);
        ScheduleUpdate();
    }

    public static bool Stop(PlayerControl player)
    {
        return player != null && CurrentColors.Remove(player.PlayerId);
    }

    public static void StopAll()
    {
        CurrentColors.Clear();
        updateScheduled = false;
        LateTask.Tasks.RemoveAll(task => task.name == "RainbowColorCycle");
    }

    private static void ScheduleUpdate()
    {
        if (updateScheduled || CurrentColors.Count == 0) return;
        updateScheduled = true;
        _ = new LateTask(UpdateColors, 0.05f, "RainbowColorCycle", false);
    }

    private static void UpdateColors()
    {
        updateScheduled = false;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            StopAll();
            return;
        }

        DateTime now = DateTime.UtcNow;
        foreach (byte playerId in CurrentColors.Keys.ToArray())
        {
            PlayerControl player = null;
            foreach (PlayerControl candidate in PlayerControl.AllPlayerControls)
            {
                if (candidate?.Data != null && candidate.PlayerId == playerId)
                {
                    player = candidate;
                    break;
                }
            }
            if (player == null)
            {
                CurrentColors.Remove(playerId);
                continue;
            }

            CycleState state = CurrentColors[playerId];
            if ((now - state.LastChangeUtc).TotalMilliseconds < state.IntervalMs) continue;

            state.ColorIndex = (state.ColorIndex + 1) % state.Colors.Length;
            state.LastChangeUtc = now;
            player.RpcSetColor(state.Colors[state.ColorIndex]);
        }

        ScheduleUpdate();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetColor))]
internal static class RememberPlayerColorPatch
{
    public static void Prefix(PlayerControl __instance)
    {
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
        {
            ChatCommandHelpers.RememberOriginalColor(__instance);
        }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal static class ChatControllerUpdatePatch
{

    public static void Postfix(ChatController __instance)
    {
        if (!__instance || !__instance.freeChatField || !__instance.freeChatField.textArea || !__instance.freeChatField.background || __instance.freeChatField.textArea.compoText == null || !__instance.freeChatField.textArea.outputText) return;
        if (!__instance.quickChatField || !__instance.quickChatField.background || __instance.quickChatField.text == null) return;

        if (Main.DarkTheme.Value)
        {
            __instance.freeChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            __instance.freeChatField.textArea.compoText.Color(Color.white);
            __instance.freeChatField.textArea.outputText.color = Color.white;

            __instance.quickChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            __instance.quickChatField.text.color = Color.white;
        }
        else
        {
            __instance.freeChatField.background.color = Color.white;
            __instance.freeChatField.textArea.compoText.Color(Color.black);
            __instance.freeChatField.textArea.outputText.color = Color.black;

            __instance.quickChatField.background.color = Color.white;
            __instance.quickChatField.text.color = Color.black;
        }

        if (Main.DisableChatModifier.Value && !__instance.freeChatField.textArea.allowAllCharacters)
        {
            __instance.freeChatField.textArea.AllowSymbols = false;
            __instance.freeChatField.textArea.AllowEmail = false;
            __instance.freeChatField.textArea.characterLimit = 100;
        }
        if (!Main.DisableChatModifier.Value)
        {
            if (__instance.timeSinceLastMessage < 0.9f)
            {
                __instance.timeSinceLastMessage = 0.9f;
            }

            __instance.freeChatField.textArea.AllowSymbols = true;
            __instance.freeChatField.textArea.AllowEmail = true;
            __instance.freeChatField.textArea.characterLimit = 120;
        }
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
internal static class ChatBubbleSetNamePatch
{
    public static void Postfix(ChatBubble __instance, [HarmonyArgument(2)] bool voted)
    {
        if (!__instance || !__instance.playerInfo || !__instance.playerInfo.Object || !__instance.playerInfo.Object.Data || !__instance.TextArea || !__instance.Background) return;

        PlayerControl target = __instance.playerInfo.Object;

        if (Main.DarkTheme.Value)
        {
            __instance.Background.color = new(0.1f, 0.1f, 0.1f, 1f);
            __instance.TextArea.color = Color.white;

            if (__instance.playerInfo.Object.Data.IsDead && Utils.InGame) __instance.Background.color = new(0.1f, 0.1f, 0.1f, 0.7f);
        }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal static class SendChatPatch
{
    private static bool ownsChatCommandCapture;
    private static bool currentChatCommandIsPrivate;
    private static bool currentChatCommandIsPublic;
    private static string currentChatCommandText;
    private static bool handlingRemotePublicCommand;
    private static readonly Dictionary<string, DateTime> pendingVisibleEchoes = new(StringComparer.Ordinal);
    private static DateTime nextRemoteCommandReplyUtc;
    private static int pendingRemoteCommandReplies;
    private static int remoteCommandReplySequence;
    private const int MaxQueuedRemoteCommandReplies = 12;

    public static bool IsHandlingRemotePublicCommand => handlingRemotePublicCommand;

    public static void SetHandlingRemotePublicCommand(bool value)
    {
        handlingRemotePublicCommand = value;
    }

    public static bool ConsumeVisibleCommandEcho(PlayerControl sender, string text)
    {
        if (sender != PlayerControl.LocalPlayer || string.IsNullOrEmpty(text)) return false;
        DateTime now = DateTime.UtcNow;
        foreach (string expiredText in pendingVisibleEchoes.Where(pair => pair.Value < now).Select(pair => pair.Key).ToArray())
        {
            pendingVisibleEchoes.Remove(expiredText);
        }
        return pendingVisibleEchoes.Remove(text);
    }

    public static void RegisterVisibleCommandEcho(string text)
    {
        if (!string.IsNullOrEmpty(text)) pendingVisibleEchoes[text] = DateTime.UtcNow.AddSeconds(3);
    }

    public static bool QueueRemoteCommandReply(string text, Action afterSend = null, string logContext = "command reply")
    {
        if (handlingRemotePublicCommand)
        {
            Logger.Info($"Suppressed public {logContext}: {text}", "RemoteCommandFeedback");
            afterSend?.Invoke();
            return false;
        }

        string safeText = ChatCommandHelpers.SanitizeOutgoingChatText(text);
        if (string.IsNullOrWhiteSpace(safeText))
        {
            afterSend?.Invoke();
            return false;
        }

        if (pendingRemoteCommandReplies >= MaxQueuedRemoteCommandReplies)
        {
            Logger.Warn($"Dropped {logContext}; the remote-command reply queue is full.", "RemoteCommandFeedback");
            afterSend?.Invoke();
            return false;
        }

        DateTime now = DateTime.UtcNow;
        DateTime scheduledUtc = nextRemoteCommandReplyUtc > now ? nextRemoteCommandReplyUtc : now;
        nextRemoteCommandReplyUtc = scheduledUtc.AddSeconds(5);
        pendingRemoteCommandReplies++;
        int sequence = ++remoteCommandReplySequence;
        float delaySeconds = Math.Max(0f, (float)(scheduledUtc - now).TotalSeconds);

        new LateTask(() =>
        {
            pendingRemoteCommandReplies = Math.Max(0, pendingRemoteCommandReplies - 1);
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null)
            {
                afterSend?.Invoke();
                return;
            }

            RegisterVisibleCommandEcho(safeText);
            Logger.Info($"Sending throttled {logContext} #{sequence}: {safeText}", "RemoteCommandFeedback");
            PlayerControl.LocalPlayer.RpcSendChat(safeText);
            afterSend?.Invoke();
        }, delaySeconds, $"RemoteCommandReply_{sequence}", false);

        return true;
    }

    public static bool IsMeetingSummaryCommand(string command)
    {
        string root = command?.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return root is not null && (root.Equals("/em", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("/endmeeting", StringComparison.OrdinalIgnoreCase));
    }

    public static void BroadcastCapturedCommandFeedback(string output, string command = "/output", PlayerControl requester = null)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null ||
            string.IsNullOrWhiteSpace(output)) return;

        if (handlingRemotePublicCommand)
        {
            Logger.Info($"Suppressed public feedback for {command}: {output}", "RemoteCommandFeedback");
            return;
        }

        string message = SanitizePublicFeedback(output);
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.Length >= 100 || message.Contains('\n'))
        {
            if (requester != null && requester != PlayerControl.LocalPlayer)
            {
                Logger.Info($"Suppressed public feedback pages for {command} requested by {requester.Data?.PlayerName ?? "unknown player"}.", "RemoteCommandFeedback");
                return;
            }
            ChatCommandHelpers.ShowPagedOutput(requester ?? PlayerControl.LocalPlayer, command, message);
            return;
        }

        if (requester != null && requester != PlayerControl.LocalPlayer)
        {
            Logger.Info($"Suppressed public feedback for {command} requested by {requester.Data?.PlayerName ?? "unknown player"}: {message}", "RemoteCommandFeedback");
            return;
        }

        new LateTask(() =>
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null) return;
            RegisterVisibleCommandEcho(message);
            PlayerControl.LocalPlayer.RpcSendChat(message);
        }, 0f, "PublicCommandFeedback", false);
    }

    private static string SanitizePublicFeedback(string text)
    {
        string result = ChatCommandHelpers.SanitizeOutgoingChatText(text);
        Debug.Log($"[Sanitization Output] {result}");
        return result;
    }
    public static void Postfix(ChatController __instance, bool __runOriginal)
    {
        if (!ownsChatCommandCapture) return;

        string output = LocalCommandPanel.EndCommandOutputCapture();
        bool isPrivate = currentChatCommandIsPrivate;
        bool isPublic = currentChatCommandIsPublic;
        string command = currentChatCommandText;
        ownsChatCommandCapture = false;
        currentChatCommandIsPrivate = false;
        currentChatCommandIsPublic = false;
        currentChatCommandText = null;
        LocalCommandPanel.SetPrivateCommandDispatch(false);

        if (isPrivate && __instance != null)
            ChatCommandHelpers.ClearChatInput(__instance);

        if (isPublic && !__runOriginal && IsHandlingRemotePublicCommand)
        {
            Logger.Info($"Suppressed public echo/reply for remote command {command}: {output}", "RemoteCommandFeedback");
            InGameCommandConsole.RecordCommandOutput($"Suppressed public feedback for {command}\n{output}");
            return;
        }

        string displayedCommand = isPrivate && command?.StartsWith("/", StringComparison.Ordinal) == true
            ? "!" + command.Substring(1)
            : command;
        InGameCommandConsole.RecordCommandOutput($"{displayedCommand}\n{output}");
        if (isPublic && !__runOriginal && PlayerControl.LocalPlayer != null)
        {
            if (IsMeetingSummaryCommand(command)) return;

            if (string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase))
            {
                ChatCommandHelpers.ShowHelpPages(PlayerControl.LocalPlayer);
                return;
            }

            string sanitizedCommand = ChatCommandHelpers.SanitizeOutgoingChatText(command);
            pendingVisibleEchoes[sanitizedCommand] = DateTime.UtcNow.AddSeconds(3);
            PlayerControl.LocalPlayer.RpcSendChat(sanitizedCommand);
            BroadcastCapturedCommandFeedback(output, sanitizedCommand);
        }
    }

    public static string ConvertNum(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        int digitCount = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsDigit(input[i]) && ++digitCount > 5)
            {
                var sb = new System.Text.StringBuilder(input.Length);

                foreach (char c in input)
                {
                    if (char.IsDigit(c))
                        sb.Append(Main.CircledDigits[c - '0']);
                    else
                        sb.Append(c);
                }
                return sb.ToString();
            }
        }
        return input;
    }

    public static bool Prefix(ChatController __instance)
    {
        string msgtext = __instance.freeChatField.textArea.text.Trim();
        bool panelDispatch = LocalCommandPanel.IsCapturingCommandOutput;
        bool privateInput = msgtext.StartsWith("!", StringComparison.Ordinal);
        bool privateCommand = privateInput && !panelDispatch;
        bool publicCommand = msgtext.StartsWith("/", StringComparison.Ordinal) && !panelDispatch;

        if (privateCommand)
        {
            msgtext = "/" + msgtext.Substring(1);
            string privateRoot = msgtext.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!LocalCommandPanel.IsAllowedConsoleCommand(privateRoot))
            {
                ChatCommandHelpers.ClearChatInput(__instance);
                InGameCommandConsole.RecordCommandOutput($"Unknown private command: {privateRoot}");
                return false;
            }

            if (AmongUsClient.Instance == null)
            {
                ChatCommandHelpers.ClearChatInput(__instance);
                InGameCommandConsole.RecordCommandOutput("A lobby connection is required for private commands.");
                return false;
            }

            if (!AmongUsClient.Instance.AmHost)
            {
                bool sent = RPC.SendPrivateCommandRequest(msgtext);
                ChatCommandHelpers.ClearChatInput(__instance);
                InGameCommandConsole.RecordCommandOutput(sent
                    ? $"! {msgtext.Substring(1)}\nSent privately to the modded host; waiting for its reply."
                    : "Could not send a private command to the host.");
                return false;
            }
        }

        if (publicCommand && AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost)
            return true;

        bool recognizedCommand = !panelDispatch && LocalCommandPanel.IsAllowedConsoleCommand(msgtext);
        if (recognizedCommand && (privateCommand || publicCommand))
        {
            if (!ChatCommandHelpers.HasCommandAccess(PlayerControl.LocalPlayer, msgtext))
            {
                string deniedCommand = msgtext.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", deniedCommand));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (msgtext.StartsWith("/page ", StringComparison.OrdinalIgnoreCase) ||
                msgtext.Equals("/page", StringComparison.OrdinalIgnoreCase))
            {
                ChatCommandHelpers.HandlePageCommand(PlayerControl.LocalPlayer, msgtext);
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (!msgtext.StartsWith("/page", StringComparison.OrdinalIgnoreCase))
            {
                ownsChatCommandCapture = LocalCommandPanel.TryBeginCommandOutputCapture();
                currentChatCommandIsPrivate = privateCommand;
                currentChatCommandIsPublic = publicCommand;
                currentChatCommandText = msgtext;
                if (privateCommand) LocalCommandPanel.SetPrivateCommandDispatch(true);
            }
        }

        string text = msgtext.ToLower();
        string converted = ConvertNum(msgtext);

        if (text == "/bind" || text.StartsWith("/bind ", StringComparison.Ordinal))
        {
            CommandBindings.HandleCommand(PlayerControl.LocalPlayer, msgtext);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (!privateInput && !publicCommand && AmongUsClient.Instance?.AmHost == true && CommandBindings.TryExecute(msgtext))
        {
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text.StartsWith("/", StringComparison.Ordinal)) EACR.BeginCommandGrace();

        if (text == "/whitelist" || text.StartsWith("/whitelist ", StringComparison.Ordinal))
        {
            ChatCommandHelpers.HandleWhitelistCommand(PlayerControl.LocalPlayer, text);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/copyfit" || text.StartsWith("/copyfit ", StringComparison.Ordinal))
        {
            ChatCommandHelpers.HandleCopyFitCommand(PlayerControl.LocalPlayer, text);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/kill" || text.StartsWith("/kill ", StringComparison.Ordinal))
        {
            ChatCommandHelpers.HandleKillCommand(PlayerControl.LocalPlayer, text.Substring("/kill".Length).Trim());
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/reload")
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"{Translator.Get("translationReloaded")}");
            Translator.Reload();
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/dump")
        {
            Utils.DumpLog();
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }
        if (text == "/help")
        {
            if (privateCommand)
                LocalCommandPanel.CaptureCommandOutput($"{Translator.Get("allCommandsFull")}\n\n{Translator.Get("recentCommandsHelp")}\n\n{Translator.Get("meetingCommandsHelp")}");
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/s" || text == "/start")
        {
            GameStartManager.Instance.BeginGame();
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/eg" || text == "/endgame")
        {
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);

            if (!Utils.InGame) return false;
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
            return false;
        }

        if (text == "/em" || text == "/endmeeting")
        {
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            ChatCommandHelpers.HandleMeetingEndCommand(PlayerControl.LocalPlayer, true);
            return false;
        }

        if (text == "/sm" || text == "/skip" || text == "/skipmeeting")
        {
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            ChatCommandHelpers.HandleMeetingEndCommand(PlayerControl.LocalPlayer, false);
            return false;
        }

        if (text == "/private")
        {
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);

            if (Utils.InGame) return false;
            AmongUsClient.Instance.ChangeGamePublic(false);
            return false;
        }

        if (text == "/public")
        {
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);

            if (Utils.InGame) return false;
            AmongUsClient.Instance.ChangeGamePublic(true);
            return false;
        }

        if (text == "/revive" || text.StartsWith("/revive ", StringComparison.Ordinal))
        {
            ChatCommandHelpers.TryRevivePlayer(PlayerControl.LocalPlayer, text.Length > "/revive".Length ? text.Substring("/revive".Length).Trim() : null);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/tm" || text == "/tiemeeting")
        {
            ChatCommandHelpers.HandleTieMeetingCommand(PlayerControl.LocalPlayer);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/impcount" || text.StartsWith("/impcount ") || text == "/setimp" || text.StartsWith("/setimp "))
        {
            ChatCommandHelpers.HandleImpostorCountCommand(PlayerControl.LocalPlayer, text);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/settings" || text.StartsWith("/settings ", StringComparison.Ordinal))
        {
            ChatCommandHelpers.HandleSettingsCommand(PlayerControl.LocalPlayer, text);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        string accessListCommand = text.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (accessListCommand is "/vip" or "/moderator" or "/admin" or "/removevip" or "/removemoderator" or "/removeadmin")
        {
            string playerSelector = text.Length > accessListCommand.Length ? text.Substring(accessListCommand.Length).Trim() : string.Empty;
            ChatCommandHelpers.HandleAccessListPlayerCommand(PlayerControl.LocalPlayer, accessListCommand, playerSelector);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text.StartsWith("/vip "))
        {
            PlayerControl target = null;
            string arg = text.Substring(5).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.AppendAllText(BanManager.VipListPath, "\n" + target.Data.FriendCode);
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("vipAdd", target.Data.PlayerName));
            }

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/moderator "))
        {
            PlayerControl target = null;
            string arg = text.Substring(11).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.AppendAllText(BanManager.ModeratorListPath, "\n" + target.Data.FriendCode);
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("moderatorAdd", target.Data.PlayerName));
            }

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/admin "))
        {
            PlayerControl target = null;
            string arg = text.Substring(7).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.AppendAllText(BanManager.AdminListPath, "\n" + target.Data.FriendCode);
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("adminAdd", target.Data.PlayerName));
            }

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/removevip "))
        {
            PlayerControl target = null;
            string arg = text.Substring(11).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.WriteAllLines(BanManager.VipListPath, File.ReadAllLines(BanManager.VipListPath).Where(x => x.Trim() != target.Data.FriendCode));
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("vipRemove", target.Data.PlayerName));
            }
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/removemoderator "))
        {
            PlayerControl target = null;
            string arg = text.Substring(17).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.WriteAllLines(BanManager.ModeratorListPath, File.ReadAllLines(BanManager.ModeratorListPath).Where(x => x.Trim() != target.Data.FriendCode));
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("moderatorRemove", target.Data.PlayerName));
            }
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/removeadmin "))
        {
            PlayerControl target = null;
            string arg = text.Substring(13).Trim();

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p.Data == null || p == PlayerControl.LocalPlayer) continue;

                if (p.Data.PlayerName.Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    target = p;
                    break;
                }
            }
            if (target == null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("permFail", arg));
            }
            else
            {
                File.WriteAllLines(BanManager.AdminListPath, File.ReadAllLines(BanManager.AdminListPath).Where(x => x.Trim() != target.Data.FriendCode));
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("adminRemove", target.Data.PlayerName));
            }
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/seeker "))
        {
            ChatCommandHelpers.HandleSeekerCommand(PlayerControl.LocalPlayer, text.Substring("/seeker".Length).Trim());

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/clearseeker")
        {
            PlayerControlSetRolePatch.manualSeekers.Clear();
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("clearedSeekers"));

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/seekers")
        {
            List<string> n = new();

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (PlayerControlSetRolePatch.manualSeekers.Contains(pc.PlayerId)) n.Add(pc.Data.PlayerName);
            }
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("seekers") + string.Join(", ", n));

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (__instance.timeSinceLastMessage < 3f || OnGameJoinedPatch.WaitingForChat) return false;

        if (text == "/mapvote")
        {
            if (Utils.IsLobby)
            {
                Utils.MapVote(false);

                __instance.freeChatField.textArea.Clear();
                __instance.freeChatField.textArea.SetText(string.Empty);
                return false;
            }
        }

        if (text == "/gmvote")
        {
            if (Utils.IsLobby)
            {
                Utils.ModeVote(false);

                __instance.freeChatField.textArea.Clear();
                __instance.freeChatField.textArea.SetText(string.Empty);
                return false;
            }
        }

        if (text == "/vote a")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.skeldVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
            else if (Utils.ModeVoteActive)
            {
                if (Utils.HasModeVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.standardVotes++;
                Utils.HasModeVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }
        if (text == "/vote b")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.miraVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
            else if (Utils.ModeVoteActive)
            {
                if (Utils.HasModeVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.noKillCooldownVotes++;
                Utils.HasModeVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }
        if (text == "/vote c")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.polusVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
            else if (Utils.ModeVoteActive)
            {
                if (Utils.HasModeVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.speedrunVotes++;
                Utils.HasModeVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }
        if (text == "/vote d")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.airshipVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
            else if (Utils.ModeVoteActive)
            {
                if (Utils.HasModeVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.shiftAndSeekVotes++;
                Utils.HasModeVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }
        if (text == "/vote e")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.fungleVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
            else if (Utils.ModeVoteActive)
            {
                if (Utils.HasModeVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.poofAndSeekVotes++;
                Utils.HasModeVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }
        if (text == "/vote f")
        {
            if (Utils.MapVoteActive)
            {
                if (Utils.HasVoted.Contains(PlayerControl.LocalPlayer.Data.PlayerId)) return !privateCommand;
                Utils.dleksVotes++;
                Utils.HasVoted.Add(PlayerControl.LocalPlayer.Data.PlayerId);
            }
        }

        if (text is "/vote a" or "/vote b" or "/vote c" or "/vote d" or "/vote e" or "/vote f")
        {
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        if (text == "/l" || text == "/lastgame")
        {
            if (string.IsNullOrEmpty(NormalGameEndChecker.LastWinReason) || Utils.InGame) return false;
            Utils.ChatCommand(__instance, $"{NormalGameEndChecker.LastWinReason}", "", false);

            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text == "/aur" || text == "/socials")
        {
            Utils.ChatCommand(__instance,
                "Blobek: blobek.net/aurbe\nGitHub: github.com/blobek/AmongUsRevampedBlobekEdition\nDiscord: blobekmini",
                "Original authors: ApeMV and contributors\nSource project: github.com/ApeMV/AmongUsRevamped", true);
            return false;
        }

        if (text == "/0kc" || text == "/0killcooldown")
        {
            Utils.ChatCommand(__instance, "0 Kill Cooldown:\n\nImpostors have no kill cooldown, and optionally can't kill after a game starts for 5 seconds", "", false);
            return false;
        }

        if (text == "/sns" || text == "/shiftandseek")
        {
            Utils.ChatCommand(__instance, "Shift and Seek:\n\nImpostors can only kill someone while shapeshifted as them\nMeetings & Reports = Off", $"Crewmates win by tasks/surviving {Options.CrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing Crewmates", true);
            return false;
        }
        if (text == "/sns2" || text == "/shiftandseek2")
        {
            Utils.ChatCommand(__instance, "Correctly killing a target will directly turn them into a ghost and leave no dead body", "", false);
            return false;
        }

        if (text == "/sr" || text == "/speedrun")
        {
            Utils.ChatCommand(__instance, $"Speedrun:\n\nEveryone is a crewmate. The 1st player to finish tasks wins the game. Game auto ends after {Options.GameAutoEndsAfter.GetInt()}s", "", false);
            return false;
        }

        if (text == "/pns" || text == "/poofandseek")
        {
            Utils.ChatCommand(__instance, $"Poof and Seek:\n\nImpostors can only move while vanished\n Meetings & Reports = Off\nVisibly moving {Options.BadMoveTimeToSuicide.GetInt()}s as Phantom = Suicide", $"Crewmates win by doing tasks or surviving {Options.PNSCrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing everyone", true);
            return false;
        }

        if (text == "/r" || text == "/roles")
        {
            switch (Options.Gamemode.GetValue())
            {
                case 0:
                    if (GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown) == 0.001f)
                    {
                        Utils.ChatCommand(__instance, "0 Kill Cooldown:\n\nImpostors have no kill cooldown, and optionally can't kill after a game starts for 5 seconds", "", false);
                    }
                    break;

                case 1:
                    Utils.ChatCommand(__instance, "Shift and Seek:\n\nImpostors can only kill someone while shapeshifted as them\nMeetings & Reports = Off", $"Crewmates win by tasks/surviving {Options.CrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing Crewmates", true);
                    break;

                case 2:
                    Utils.ChatCommand(__instance, $"Speedrun:\n\nEveryone is a crewmate. The 1st player to finish tasks wins the game. Game auto ends after {Options.GameAutoEndsAfter.GetInt()}s", "", false);
                    break;

                case 3:
                    Utils.ChatCommand(__instance, $"Poof and Seek:\n\nImpostors can only move while vanished\n Meetings & Reports = Off\nVisibly moving {Options.BadMoveTimeToSuicide.GetInt()}s as Phantom = Suicide", $"Crewmates win by doing tasks or surviving {Options.PNSCrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing everyone", true);
                    break;
            }
            __instance.timeSinceLastMessage = 0.8f;
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        if (text.StartsWith("/t "))
        {
            BanManager.LoadTemplates();

            string templateName = text[3..].Trim().ToLower();

            if (BanManager.Templates.TryGetValue(templateName, out string templateMessage))
            {
                if (templateMessage.Length < 121)
                {
                    Utils.ChatCommand(__instance, BanManager.CheckTemplate(templateMessage), "", false);
                }
                else
                {
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("tooLongTemplate"));
                }
            }
            else
            {
                Logger.Info($"Could not find template '{templateName}'", "TemplateManager");
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"Template '{templateName}' not found.");
            }
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }

        bool col1 = text.StartsWith("/col ");
        bool col2 = text.StartsWith("/color ");
        bool col3 = text.StartsWith("/colour ");
        bool col4 = text.StartsWith("/colout ");

        if (col1 || col2 || col3 || col4)
        {
            if (ChatCommandHelpers.HandleRainbowColorCommand(PlayerControl.LocalPlayer, text))
            {
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            string argCol = text.Substring(col1 ? 5 : col2 ? 7 : col3 ? 8 : 8).Trim();

            if (Utils.CheckAccessLevel(PlayerControl.LocalPlayer.Data.FriendCode) < Options.SlashColorCmd.GetValue())
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/color"));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (!Utils.TryGetColorId(argCol, out byte colId))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", argCol));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (colId > 17 && !Options.AllowFortegreen.GetBool())
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", argCol));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            PlayerControl.LocalPlayer.RpcSetColor(colId);
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        bool ucol0 = text.StartsWith("/ucolout ");
        bool ucol1 = text.StartsWith("/ucol ");
        bool ucol2 = text.StartsWith("/ucolor ");
        bool ucol3 = text.StartsWith("/ucolour ");

        if (ucol0 || ucol1 || ucol2 || ucol3)
        {
            if (ChatCommandHelpers.HandleRainbowColorCommand(PlayerControl.LocalPlayer, text))
            {
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (!ChatCommandHelpers.HasCommandAccess(PlayerControl.LocalPlayer, Options.SlashUserColorCmd))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", "/ucolor"));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            string arg = text.Substring(ucol0 ? 9 : ucol1 ? 6 : ucol2 ? 7 : 8).Trim();
            string[] parts = arg.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("userColorSyntax"));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (!Utils.TryGetColorId(parts[0], out byte colorId))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", parts[0]));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            if (colorId > 17 && !Options.AllowFortegreen.GetBool())
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("invalidColor", parts[0]));
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            string targetName = parts.Length == 1 ? "@me" : parts[1];
            if (!ChatCommandHelpers.TryResolvePlayerTargets(targetName, true, out List<PlayerControl> targets, out string error, PlayerControl.LocalPlayer))
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
                ChatCommandHelpers.ClearChatInput(__instance);
                return false;
            }

            foreach (PlayerControl target in targets)
            {
                target.RpcSetColor(colorId);
            }

            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        bool give1 = text.StartsWith("/give ");
        bool set1 = text.StartsWith("/set ");
        bool add1 = text.StartsWith("/add ");
        bool roleReset1 = text.StartsWith("/rolereset ");
        bool playerReset1 = text == "/playerreset" || text.StartsWith("/playerreset ");
        bool voteOut1 = text == "/voteout" || text.StartsWith("/voteout ");
        bool voteOut2 = text.StartsWith("/cvoteout ");

        if (give1 || set1 || add1 || roleReset1 || playerReset1 || voteOut1 || voteOut2)
        {
            if (give1 || set1 || add1)
            {
                ChatCommandHelpers.HandleRoleCommand(PlayerControl.LocalPlayer, text, !give1, add1);
            }
            else if (roleReset1)
            {
                ChatCommandHelpers.HandleRoleResetCommand(PlayerControl.LocalPlayer, text.Substring("/rolereset".Length).Trim());
            }
            else if (playerReset1)
            {
                ChatCommandHelpers.HandlePlayerResetCommand(PlayerControl.LocalPlayer, text.Substring("/playerreset".Length).Trim());
            }
            else
            {
                ChatCommandHelpers.HandleVoteOutCommand(PlayerControl.LocalPlayer, text, voteOut2);
            }
            ChatCommandHelpers.ClearChatInput(__instance);
            return false;
        }

        else
        {

            bool isKick = text.StartsWith("/kick ");
            bool isBan = text.StartsWith("/ban ");

            bool isColorKick = text.StartsWith("/ckick ");
            bool isColorBan = text.StartsWith("/cban ");

            if (!isKick && !isBan && !isColorKick && !isColorBan)
            {
                if (privateCommand)
                {
                    LocalCommandPanel.CaptureCommandOutput("This command is not handled locally. Try its public / form instead.");
                    ChatCommandHelpers.ClearChatInput(__instance);
                    return false;
                }

                //__instance.freeChatField.textArea.SetText(converted);
                //Utils.ChatCommand(__instance, $"{converted}", "", false);
                Logger.Info($" {PlayerControl.LocalPlayer.Data.PlayerName}: {msgtext}", "SendChat");
                return true;
            }

            ChatCommandHelpers.HandleKickBanCommand(PlayerControl.LocalPlayer, text);
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(string.Empty);
            return false;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
public static class RPCHandlerPatch
{
    private static bool ownsRemotePublicCommandCapture;
    private static string remotePublicCommand;
    private static PlayerControl remotePublicRequester;

    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId == (byte)CustomRPC.PrivateCommandRequest)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || __instance?.Data == null || reader == null) return;
            try
            {
                string privateCommand = reader.ReadString();
                string reply = ChatCommandHelpers.ExecutePrivateCommand(__instance, privateCommand);
                InGameCommandConsole.RecordCommandOutput($"! from {__instance.Data.PlayerName}: {privateCommand}\n{reply}");
                RPC.SendPrivateCommandReply(__instance.Data.ClientId, reply);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PrivateCommandRequest");
            }
            return;
        }

        if (callId == (byte)CustomRPC.PrivateCommandReply)
        {
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost || reader == null ||
                __instance?.Data == null || __instance.Data.ClientId != AmongUsClient.Instance.HostId) return;
            try
            {
                InGameCommandConsole.RecordCommandOutput("Private reply from host:\n" + reader.ReadString());
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PrivateCommandReply");
            }
            return;
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        var rpcType = (RpcCalls)callId;
        MessageReader subReader = MessageReader.Get(reader);

        switch (rpcType)
        {
            case RpcCalls.SendChat:
                {
                    string msgtext = subReader.ReadString();
                    string text = msgtext.ToLower();

                    if (SendChatPatch.ConsumeVisibleCommandEcho(__instance, msgtext)) return;
                    TurkishTextRainbow.CheckChatMessage(__instance, msgtext);

                    if (__instance != PlayerControl.LocalPlayer && msgtext.StartsWith("/", StringComparison.Ordinal))
                    {
                        string root = msgtext.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (LocalCommandPanel.IsAllowedConsoleCommand(root))
                            SendChatPatch.SetHandlingRemotePublicCommand(true);
                        if (LocalCommandPanel.IsAllowedConsoleCommand(root) &&
                            !root.Equals("/help", StringComparison.OrdinalIgnoreCase) &&
                            !root.Equals("/page", StringComparison.OrdinalIgnoreCase) &&
                            !ownsRemotePublicCommandCapture)
                        {
                            ownsRemotePublicCommandCapture = LocalCommandPanel.TryBeginCommandOutputCapture();
                            if (ownsRemotePublicCommandCapture)
                            {
                                remotePublicCommand = msgtext;
                                remotePublicRequester = __instance;
                            }
                        }
                    }

                    Logger.Info($" {__instance.Data.PlayerName}: {msgtext}", "SendChat");

                    string incomingCommandRoot = msgtext.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (LocalCommandPanel.IsAllowedConsoleCommand(incomingCommandRoot) &&
                        !ChatCommandHelpers.HasCommandAccess(__instance, msgtext))
                    {
                        Logger.Info($"Denied command {incomingCommandRoot} from {__instance.Data.PlayerName} by its configured access level.", "CommandPermission");
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("accessDenied", incomingCommandRoot));
                        return;
                    }

                    if (text == "/page" || text.StartsWith("/page ", StringComparison.Ordinal))
                    {
                        ChatCommandHelpers.HandlePageCommand(__instance, msgtext);
                        return;
                    }

                    if (text == "/help")
                    {
                        InGameCommandConsole.RecordCommandOutput($"/help requested by {__instance.Data.PlayerName}\n" +
                            $"{Translator.Get("allCommandsFull")}\n\n{Translator.Get("recentCommandsHelp")}\n\n{Translator.Get("meetingCommandsHelp")}");
                        return;
                    }

                    if (EACR.IsRecognizedModCommand(text) && BanManager.IsCommandWhitelistedAdmin(__instance))
                    {
                        EACR.BeginCommandGrace(__instance);
                    }

                    if (text == "/bind" || text.StartsWith("/bind ", StringComparison.Ordinal))
                    {
                        CommandBindings.HandleCommand(__instance, msgtext);
                        return;
                    }

                    BanManager.IsStartWord(__instance, text);
                    BanManager.IsWordBanned(__instance, text);

                    if (text == "/revive" || text.StartsWith("/revive ", StringComparison.Ordinal))
                    {
                        ChatCommandHelpers.TryRevivePlayer(__instance, text.Length > "/revive".Length ? text.Substring("/revive".Length).Trim() : null);
                        return;
                    }

                    if (text == "/tm" || text == "/tiemeeting")
                    {
                        ChatCommandHelpers.HandleTieMeetingCommand(__instance);
                        return;
                    }

                    if (text == "/copyfit" || text.StartsWith("/copyfit ", StringComparison.Ordinal))
                    {
                        ChatCommandHelpers.HandleCopyFitCommand(__instance, text);
                        return;
                    }

                    if (text == "/kill" || text.StartsWith("/kill ", StringComparison.Ordinal))
                    {
                        ChatCommandHelpers.HandleKillCommand(__instance, text.Substring("/kill".Length).Trim());
                        return;
                    }

                    if (text == "/impcount" || text.StartsWith("/impcount ") || text == "/setimp" || text.StartsWith("/setimp "))
                    {
                        ChatCommandHelpers.HandleImpostorCountCommand(__instance, text);
                        return;
                    }

                    if (text == "/settings" || text.StartsWith("/settings ", StringComparison.Ordinal))
                    {
                        ChatCommandHelpers.HandleSettingsCommand(__instance, text);
                        return;
                    }

                    if (text.StartsWith("/seeker ") && Utils.CheckAccessLevel(__instance.Data.FriendCode) >= Options.SlashMapAndSeekerCmd.GetValue())
                    {
                        ChatCommandHelpers.HandleSeekerCommand(__instance, msgtext.Substring("/seeker".Length).Trim());
                    }

                    if (text == "/clearseeker")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashMapAndSeekerCmd.GetValue()) return;
                        PlayerControlSetRolePatch.manualSeekers.Clear();
                        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("clearedSeekers"));
                    }

                    bool col1 = text.StartsWith("/col ");
                    bool col2 = text.StartsWith("/color ");
                    bool col3 = text.StartsWith("/colour ");
                    bool col4 = text.StartsWith("/colout ");

                    if (col1 || col2 || col3 || col4)
                    {
                        if (ChatCommandHelpers.HandleRainbowColorCommand(__instance, text)) return;

                        string argCol = text.Substring(col1 ? 5 : col2 ? 7 : col3 ? 8 : 8).Trim();

                        if (!Utils.InGame && Utils.TryGetColorId(argCol, out byte colId))
                        {
                            if (Utils.CheckAccessLevel(__instance.Data.FriendCode) >= Options.SlashColorCmd.GetValue())
                            {
                                if (colId > 17 && !Options.AllowFortegreen.GetBool()) return;
                                __instance.RpcSetColor(colId);
                            }
                        }
                    }

                    bool ucol0 = text.StartsWith("/ucolout ");
                    bool ucol1 = text.StartsWith("/ucol ");
                    bool ucol2 = text.StartsWith("/ucolor ");
                    bool ucol3 = text.StartsWith("/ucolour ");

                    if (ucol0 || ucol1 || ucol2 || ucol3)
                    {
                        if (ChatCommandHelpers.HandleRainbowColorCommand(__instance, text)) return;
                        if (!ChatCommandHelpers.HasCommandAccess(__instance, Options.SlashUserColorCmd)) return;

                        string arg = text.Substring(ucol0 ? 9 : ucol1 ? 6 : ucol2 ? 7 : 8).Trim();
                        string[] parts = arg.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) return;

                        if (!Utils.TryGetColorId(parts[0], out byte colorId)) return;
                        if (colorId > 17 && !Options.AllowFortegreen.GetBool()) return;

                        string targetName = parts.Length == 1 ? "@me" : parts[1];
                        if (!ChatCommandHelpers.TryResolvePlayerTargets(targetName, true, out List<PlayerControl> targets, out string error, __instance))
                        {
                            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, error);
                            return;
                        }

                        foreach (PlayerControl target in targets)
                        {
                            target.RpcSetColor(colorId);
                        }
                    }

                    bool give1 = text.StartsWith("/give ");
                    bool set1 = text.StartsWith("/set ");
                    bool add1 = text.StartsWith("/add ");
                    bool roleReset1 = text.StartsWith("/rolereset ");
                    bool playerReset1 = text == "/playerreset" || text.StartsWith("/playerreset ");
                    bool voteOut1 = text == "/voteout" || text.StartsWith("/voteout ");
                    bool voteOut2 = text.StartsWith("/cvoteout ");

                    if (give1 || set1 || add1 || roleReset1 || playerReset1 || voteOut1 || voteOut2)
                    {
                        if (give1 || set1 || add1)
                        {
                            ChatCommandHelpers.HandleRoleCommand(__instance, text, !give1, add1);
                        }
                        else if (roleReset1)
                        {
                            ChatCommandHelpers.HandleRoleResetCommand(__instance, text.Substring("/rolereset".Length).Trim());
                        }
                        else if (playerReset1)
                        {
                            ChatCommandHelpers.HandlePlayerResetCommand(__instance, text.Substring("/playerreset".Length).Trim());
                        }
                        else
                        {
                            ChatCommandHelpers.HandleVoteOutCommand(__instance, text, voteOut2);
                        }
                    }

                    bool isKick = text.StartsWith("/kick ");
                    bool isBan = text.StartsWith("/ban ");

                    bool isColorKick = text.StartsWith("/ckick ");
                    bool isColorBan = text.StartsWith("/cban ");

                    if (isKick || isBan || isColorKick || isColorBan)
                    {
                        if (isBan || isColorBan)
                        {
                            if (!ChatCommandHelpers.HasCommandAccess(__instance, Options.SlashBanCmd)) return;
                        }
                        else if (!ChatCommandHelpers.HasCommandAccess(__instance, Options.SlashKickCmd)) return;

                        ChatCommandHelpers.HandleKickBanCommand(__instance, msgtext);
                    }

                    if (text == "/eg" || text == "/endgame")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashStartAndEndGameCmd.GetValue()) return;
                        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                    }

                    if (text == "/em" || text == "/endmeeting" || text == "/sm" || text == "/skip" || text == "/skipmeeting")
                    {
                        bool summarizeVotes = text == "/em" || text == "/endmeeting";
                        ChatCommandHelpers.HandleMeetingEndCommand(__instance, summarizeVotes);
                    }

                    if (text == "/private")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashStartAndEndGameCmd.GetValue() || Utils.InGame) return;
                        AmongUsClient.Instance.ChangeGamePublic(false);
                    }

                    if (text == "/public")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashStartAndEndGameCmd.GetValue() || Utils.InGame) return;
                        AmongUsClient.Instance.ChangeGamePublic(true);
                    }

                    if (text == "/s" || text == "/start")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashStartAndEndGameCmd.GetValue()) return;
                        GameStartManager.Instance.BeginGame();
                    }

                    if (OnGameJoinedPatch.WaitingForChat || GameStartManagerUpdatePatch.CountingDown) return;

                    if (text == "/mapvote")
                    {
                        if (Utils.IsLobby)
                        {
                            if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashMapAndSeekerCmd.GetValue()) return;
                            Utils.MapVote(true);
                        }
                    }

                    if (text == "/gmvote")
                    {
                        if (Utils.IsLobby)
                        {
                            if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashMapAndSeekerCmd.GetValue()) return;
                            Utils.ModeVote(true);
                        }
                    }

                    if (text == "/vote a")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.skeldVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                        else if (Utils.ModeVoteActive)
                        {
                            if (Utils.HasModeVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.standardVotes++;
                            Utils.HasModeVoted.Add(__instance.Data.PlayerId);
                        }
                    }
                    if (text == "/vote b")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.miraVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                        else if (Utils.ModeVoteActive)
                        {
                            if (Utils.HasModeVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.noKillCooldownVotes++;
                            Utils.HasModeVoted.Add(__instance.Data.PlayerId);
                        }
                    }
                    if (text == "/vote c")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.polusVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                        else if (Utils.ModeVoteActive)
                        {
                            if (Utils.HasModeVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.speedrunVotes++;
                            Utils.HasModeVoted.Add(__instance.Data.PlayerId);
                        }
                    }
                    if (text == "/vote d")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.airshipVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                        else if (Utils.ModeVoteActive)
                        {
                            if (Utils.HasModeVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.shiftAndSeekVotes++;
                            Utils.HasModeVoted.Add(__instance.Data.PlayerId);
                        }
                    }
                    if (text == "/vote e")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.fungleVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                        else if (Utils.ModeVoteActive)
                        {
                            if (Utils.HasModeVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.poofAndSeekVotes++;
                            Utils.HasModeVoted.Add(__instance.Data.PlayerId);
                        }
                    }
                    if (text == "/vote f")
                    {
                        if (Utils.MapVoteActive)
                        {
                            if (Utils.HasVoted.Contains(__instance.Data.PlayerId)) return;
                            Utils.dleksVotes++;
                            Utils.HasVoted.Add(__instance.Data.PlayerId);
                        }
                    }

                    if (text == "/l" || text == "/lastgame")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashLastGameCmd.GetValue()) return;
                        if (string.IsNullOrEmpty(NormalGameEndChecker.LastWinReason) || Utils.InGame) return;
                        Utils.ModeratorChatCommand($"{NormalGameEndChecker.LastWinReason}", "", false);
                    }

                    if (text == "/aur" || text == "/socials")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashLastGameCmd.GetValue()) return;
                        Utils.ModeratorChatCommand(
                            "Blobek: blobek.net/aurbe\nGitHub: github.com/blobek/AmongUsRevampedBlobekEdition\nDiscord: blobekmini",
                            "Original authors: ApeMV and contributors\nSource project: github.com/ApeMV/AmongUsRevamped", true);
                    }

                    if (text == "/0kc" || text == "/0killcooldown")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        Utils.ModeratorChatCommand("0 Kill Cooldown:\n\nImpostors have no kill cooldown, and optionally can't kill after a game starts for 5 seconds", "", false);
                    }
                    if (text == "/sns" || text == "/shiftandseek")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        Utils.ModeratorChatCommand("Shift and Seek:\n\nImpostors can only kill someone while shapeshifted as them\nMeetings & Reports = Off", $"Crewmates win by tasks/surviving {Options.CrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing Crewmates", true);
                    }
                    if (text == "/sns2" || text == "/shiftandseek2")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        Utils.ModeratorChatCommand("Correctly killing a target will directly turn them into a ghost and leave no dead body", "", false);
                    }

                    if (text == "/sr" || text == "/speedrun")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        Utils.ModeratorChatCommand($"Speedrun:\n\nEveryone is a crewmate. The 1st player to finish tasks wins the game. Game auto ends after {Options.GameAutoEndsAfter.GetInt()}s", "", false);
                    }

                    if (text == "/pns" || text == "/poofandseek")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        Utils.ModeratorChatCommand($"Poof and Seek:\n\nImpostors can only move while vanished\n Meetings & Reports = Off\nVisibly moving {Options.BadMoveTimeToSuicide.GetInt()}s as Phantom = Suicide", $"Crewmates win by doing tasks or surviving {Options.PNSCrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing everyone", true);
                    }

                    if (text == "/r" || text == "/roles")
                    {
                        if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;
                        switch (Options.Gamemode.GetValue())
                        {
                            case 0:
                                if (GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown) == 0.001f)
                                {
                                    Utils.ModeratorChatCommand("0 Kill Cooldown:\n\nImpostors have no kill cooldown, and optionally can't kill after a game starts for 5 seconds", "", false);
                                }
                                break;

                            case 1:
                                Utils.ModeratorChatCommand("Shift and Seek:\n\nImpostors can only kill someone while shapeshifted as them\nMeetings & Reports = Off", $"Crewmates win by tasks/surviving {Options.CrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing Crewmates", true);
                                break;

                            case 2:
                                Utils.ModeratorChatCommand($"Speedrun:\n\nEveryone is a crewmate. The 1st player to finish tasks wins the game. Game auto ends after {Options.GameAutoEndsAfter.GetInt()}s", "", false);
                                break;

                            case 3:
                                Utils.ModeratorChatCommand($"Poof and Seek:\n\nImpostors can only move while vanished\n Meetings & Reports = Off\nVisibly moving {Options.BadMoveTimeToSuicide.GetInt()}s as Phantom = Suicide", $"Crewmates win by doing tasks or surviving {Options.PNSCrewAutoWinsGameAfter.GetInt()}s\nImpostors win by killing everyone", true);
                                break;

                        }
                    }

                    if (Utils.CheckAccessLevel(__instance.Data.FriendCode) < Options.SlashRolesAndGamemodeCmd.GetValue()) return;

                    if (text.StartsWith("/t "))
                    {
                        BanManager.LoadTemplates();

                        string templateName = text[3..].Trim().ToLower();

                        if (BanManager.Templates.TryGetValue(templateName, out string templateMessage))
                        {
                            if (templateMessage.Length < 121)
                            {
                                Utils.ModeratorChatCommand(BanManager.CheckTemplate(templateMessage), "", false);
                            }
                            else
                            {
                                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Translator.Get("tooLongTemplate"));
                            }
                        }
                    }
                    break;
                }
        }
    }

    public static void Postfix([HarmonyArgument(0)] byte callId)
    {
        if ((RpcCalls)callId != RpcCalls.SendChat) return;
        SendChatPatch.SetHandlingRemotePublicCommand(false);
        if (!ownsRemotePublicCommandCapture) return;

        string output = LocalCommandPanel.EndCommandOutputCapture();
        ownsRemotePublicCommandCapture = false;
        string command = remotePublicCommand;
        PlayerControl requester = remotePublicRequester;
        remotePublicCommand = null;
        remotePublicRequester = null;
        if (SendChatPatch.IsMeetingSummaryCommand(command)) return;
        string requesterName = requester?.Data?.PlayerName ?? "Unknown player";
        InGameCommandConsole.RecordCommandOutput($"Command from {requesterName}: {command}\n{output}");
        if (!string.IsNullOrWhiteSpace(output))
            Logger.Info($"Suppressed public feedback for {command} from {requesterName}: {output}", "RemoteCommandFeedback");
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
public static class FreeChatInputFieldUpdateCharCountPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        if (!Main.DisableChatModifier.Value)
        {
            __instance.charCountText.SetText($"{__instance.textArea.text.Length}/120");
        }
    }
}

// https://github.com/astra1dev/AUnlocker/blob/main/src/Patches/ChatPatches.cs
[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
public static class TextBoxTMPUpdatePatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        if (!Main.DisableChatModifier.Value && __instance.hasFocus)
        {
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;

            if (Input.GetKeyDown(KeyCode.C))
            {
                GUIUtility.systemCopyBuffer = __instance.text;
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                __instance.SetText(__instance.text + GUIUtility.systemCopyBuffer);
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                GUIUtility.systemCopyBuffer = __instance.text;
                __instance.SetText("");
            }
        }
    }
}