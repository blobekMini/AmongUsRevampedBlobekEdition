using System;
using System.IO;
using InnerNet;

namespace AmongUsRevamped;

internal static class LobbyPresenceNotifier
{
    public static string ConfigPath => Path.Combine(BanManager.DataPath, "AUR-DATA", "notifypresence.txt");

    public static void NotifyJoined(ClientData client)
    {
        if (client == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
            !Options.NotifyTrackedPlayers.GetBool() ||
            !File.Exists(ConfigPath)) return;

        try
        {
            string friendCode = (client.FriendCode ?? string.Empty).Trim();
            string playerName = (client.PlayerName ?? string.Empty).Trim();
            foreach (string rawEntry in File.ReadLines(ConfigPath))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0 || entry.StartsWith("#", StringComparison.Ordinal)) continue;

                int separator = entry.IndexOf(',');
                bool matches = separator >= 0
                    ? friendCode.Equals(entry.Substring(0, separator).Trim(), StringComparison.OrdinalIgnoreCase) &&
                      playerName.Equals(entry.Substring(separator + 1).Trim(), StringComparison.OrdinalIgnoreCase)
                    : friendCode.Equals(entry, StringComparison.OrdinalIgnoreCase) ||
                      playerName.Equals(entry, StringComparison.OrdinalIgnoreCase);
                if (!matches) continue;

                string message = $"Tracked player joined: {playerName} ({friendCode})";
                Logger.Info(message, "LobbyPresenceNotifier");
                InGameCommandConsole.ShowNotificationPopup("Tracked player joined", $"{playerName}\nFriend code: {friendCode}");
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "LobbyPresenceNotifier");
        }
    }
}
