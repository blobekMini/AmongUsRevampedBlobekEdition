using InnerNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AmongUsRevamped;

internal static class TurkishTextRainbow
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly HashSet<byte> TriggeredPlayerIds = new();
    private static readonly string[] TurkishAmongUsTerms =
    {
        "mürettebat", "görev", "görev listesi", "şüpheli", "sahtekâr", "havalandırma",
        "uzay gemisi", "toplantı", "acil toplantı", "öldürüldü", "güvenlik kamerası",
        "katil", "sabotaj", "oylama", "kalkan", "kaçış", "astronot", "gözcü", "merhaba"
    };
    private static readonly string[] UsernameOnlyTerms = { "apollo" };

    public static void ResetForLobby()
    {
        TriggeredPlayerIds.Clear();
    }

    public static void CheckJoinedClient(ClientData client)
    {
        if (!IsEnabled() || client == null || !ContainsUsernameTrigger(client.PlayerName)) return;

        PlayerControl player = FindPlayer(client.Id);
        if (player != null)
        {
            TryApply(player, "username");
            return;
        }

        _ = new LateTask(() =>
        {
            PlayerControl joinedPlayer = FindPlayer(client.Id);
            if (joinedPlayer != null) TryApply(joinedPlayer, "username");
        }, 1f, "TurkishTextRainbowJoin", false);
    }

    public static void CheckPlayerName(PlayerControl player)
    {
        if (player?.Data != null && ContainsUsernameTrigger(player.Data.PlayerName))
            TryApply(player, "username");
    }

    public static void CheckChatMessage(PlayerControl player, string message)
    {
        if (player?.Data == null || !ContainsUsernameTrigger(player.Data.PlayerName) && !ContainsTurkishTrigger(message)) return;
        TryApply(player, ContainsUsernameTrigger(player.Data.PlayerName) ? "username" : "chat message");
    }

    private static bool IsEnabled() => AmongUsClient.Instance?.AmHost == true && Options.TurkishTextRainbow?.GetBool() == true;

    private static PlayerControl FindPlayer(int clientId)
    {
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data != null && player.Data.ClientId == clientId) return player;
        }
        return null;
    }

    private static bool TryApply(PlayerControl player, string source)
    {
        if (!IsEnabled() || player?.Data == null || IsImmune(player) || !TriggeredPlayerIds.Add(player.PlayerId)) return false;

        // This is the same rainbow-cycle effect as /ucolor gay <player>, applied by player ID
        // so Unicode player names do not need to be parsed as a chat-command selector.
        RainbowColorCycle.Start(player);
        Logger.Info($"Started the cosmetic rainbow cycle for {player.Data.PlayerName} after matching Turkish text in their {source}.", "TurkishTextRainbow");
        return true;
    }

    private static bool IsImmune(PlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost == true && player == PlayerControl.LocalPlayer) return true;
        return Utils.CheckAccessLevel(player.Data.FriendCode ?? string.Empty) > 0;
    }

    private static bool ContainsTurkishTrigger(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Any(character => "ÇĞÖÜçğöü".Contains(character))) return true;

        string normalizedText = text.ToUpper(TurkishCulture);
        return TurkishAmongUsTerms.Any(term => normalizedText.Contains(term.ToUpper(TurkishCulture), StringComparison.Ordinal));
    }

    private static bool ContainsUsernameTrigger(string text)
    {
        if (ContainsTurkishTrigger(text)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        string normalizedText = text.ToUpper(TurkishCulture);
        return UsernameOnlyTerms.Any(term => normalizedText.Contains(term.ToUpper(TurkishCulture), StringComparison.Ordinal));
    }
}