using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AmongUsRevamped;

internal static class LobbyPlayerHistory
{
    private sealed class LobbySection
    {
        public DateTime CreatedUtc { get; init; }
        public string RoomCode { get; set; }
    }

    private sealed class PlayerRecord
    {
        public string FriendCode { get; init; }
        public string PlayerName { get; set; }
        public HashSet<int> LobbyIds { get; } = new();
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, PlayerRecord> Players = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string HistoryPath = Path.Combine(BanManager.DataPath, "AUR-DATA", "LobbyPlayerHistory.txt");
    private static readonly TimeSpan LobbyRetentionWindow = TimeSpan.FromDays(14);
    private static readonly Dictionary<int, LobbySection> LobbySections = new();
    private static readonly HashSet<string> NotifiedReturningPlayersThisLobby = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> PendingRoundPlayers = new(StringComparer.OrdinalIgnoreCase);
    private static int pendingRoundLobbyId;
    private static int currentLobbyId;
    private static int lastLobbyId;
    private static bool loaded;

    public static void BeginLobby()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        lock (Sync)
        {
            LoadIfNeeded();
            PruneExpiredLobbies();
            NotifiedReturningPlayersThisLobby.Clear();
            PendingRoundPlayers.Clear();
            pendingRoundLobbyId = 0;
            StartNewSection(GetCurrentRoomCode());
        }
    }

    public static void NotifyReturningPlayerJoined(InnerNet.ClientData client)
    {
        if (client == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
            !Options.NotifyReturningPlayers.GetBool()) return;

        string friendCode = (client.FriendCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(friendCode)) return;

        lock (Sync)
        {
            LoadIfNeeded();
            PruneExpiredLobbies();
            if (currentLobbyId <= 0 || !Players.TryGetValue(friendCode, out PlayerRecord record) ||
                !record.LobbyIds.Any(lobbyId => lobbyId != currentLobbyId) ||
                !NotifiedReturningPlayersThisLobby.Add(friendCode)) return;

            string name = CleanPlayerName(client.PlayerName);
            string message = $"{name} played a round with you before and has returned.";
            Logger.Info(message, "LobbyPlayerHistoryMatch");
            InGameCommandConsole.ShowNotificationPopup("Returning player", message);
        }
    }

    public static void CaptureRoundPlayers()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer?.Data == null) return;

        lock (Sync)
        {
            LoadIfNeeded();
            string roomCode = GetCurrentRoomCode();
            if (currentLobbyId <= 0 || !LobbySections.TryGetValue(currentLobbyId, out LobbySection currentSection) ||
                DateTime.UtcNow.Date != currentSection.CreatedUtc.Date ||
                (roomCode.Length > 0 && currentSection.RoomCode.Length > 0 && currentSection.RoomCode != roomCode))
            {
                StartNewSection(roomCode);
            }
            else if (roomCode.Length > 0 && currentSection.RoomCode.Length == 0)
            {
                currentSection.RoomCode = roomCode;
                RewriteHistoryFile();
            }

            PendingRoundPlayers.Clear();
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player?.Data == null || player.Data.Disconnected) continue;
                string friendCode = (player.Data.FriendCode ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(friendCode) || friendCode.Contains(',') || friendCode.Contains(';')) continue;

                string name = CleanPlayerName(player.Data.PlayerName);
                if (!Players.TryGetValue(friendCode, out PlayerRecord record))
                {
                    record = new PlayerRecord { FriendCode = friendCode, PlayerName = name };
                    Players.Add(friendCode, record);
                }
                else
                {
                    record.PlayerName = name;
                }

                PendingRoundPlayers[friendCode] = name;
            }

            pendingRoundLobbyId = currentLobbyId;
            Logger.Info($"Captured {PendingRoundPlayers.Count} round participants for history section {pendingRoundLobbyId}; they will be saved if the round completes.", "LobbyPlayerHistory");
        }
    }

    public static void RecordRoundPlayers()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        lock (Sync)
        {
            LoadIfNeeded();
            if (pendingRoundLobbyId <= 0 || pendingRoundLobbyId != currentLobbyId || PendingRoundPlayers.Count == 0)
            {
                Logger.Info("No pending completed-round player history to save.", "LobbyPlayerHistory");
                PendingRoundPlayers.Clear();
                pendingRoundLobbyId = 0;
                return;
            }

            foreach (KeyValuePair<string, string> player in PendingRoundPlayers)
            {
                if (!Players.TryGetValue(player.Key, out PlayerRecord record))
                {
                    record = new PlayerRecord { FriendCode = player.Key, PlayerName = player.Value };
                    Players.Add(player.Key, record);
                }
                else
                {
                    record.PlayerName = player.Value;
                }

                if (record.LobbyIds.Add(pendingRoundLobbyId))
                    AppendLine($"{player.Key},{player.Value};");
            }

            Logger.Info($"Saved {PendingRoundPlayers.Count} completed-round participants to history section {pendingRoundLobbyId}.", "LobbyPlayerHistory");
            PendingRoundPlayers.Clear();
            pendingRoundLobbyId = 0;
        }
    }

    private static void LoadIfNeeded()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            string directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(HistoryPath)) return;

            int lobbyId = 0;
            foreach (string rawLine in File.ReadLines(HistoryPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                if (line.EndsWith(":", StringComparison.Ordinal))
                {
                    string header = line.Substring(0, line.Length - 1);
                    string[] sectionFields = header.Split('|');
                    if (sectionFields.Length == 3 && int.TryParse(sectionFields[0], out int sectionId))
                    {
                        DateTime createdUtc;
                        if (long.TryParse(sectionFields[1], out long ticks) && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                            createdUtc = new DateTime(ticks, DateTimeKind.Utc);
                        else if (DateTime.TryParse(sectionFields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsedUtc))
                            createdUtc = parsedUtc.Kind == DateTimeKind.Utc ? parsedUtc : parsedUtc.ToUniversalTime();
                        else
                            continue;

                        LobbySections[sectionId] = new LobbySection
                        {
                            CreatedUtc = createdUtc,
                            RoomCode = NormalizeRoomCode(sectionFields[2])
                        };
                        lobbyId = sectionId;
                        lastLobbyId = Math.Max(lastLobbyId, sectionId);
                        continue;
                    }

                    int colon = header.IndexOf(':');
                    if (colon >= 0 && int.TryParse(header.Substring(0, colon), out int timestampedLegacyId))
                    {
                        string timestampText = header.Substring(colon + 1);
                        DateTime createdUtc = DateTime.TryParse(timestampText, out DateTime parsedTimestamp)
                            ? (parsedTimestamp.Kind == DateTimeKind.Utc ? parsedTimestamp : parsedTimestamp.ToUniversalTime())
                            : DateTime.UtcNow;
                        LobbySections[timestampedLegacyId] = new LobbySection { CreatedUtc = createdUtc, RoomCode = string.Empty };
                        lobbyId = timestampedLegacyId;
                        lastLobbyId = Math.Max(lastLobbyId, timestampedLegacyId);
                        continue;
                    }

                    if (int.TryParse(header, out int legacyLobbyId))
                    {
                        lobbyId = legacyLobbyId;
                        // Old files did not store dates, so start their 14-day retention window at migration.
                        LobbySections[legacyLobbyId] = new LobbySection { CreatedUtc = DateTime.UtcNow, RoomCode = string.Empty };
                        lastLobbyId = Math.Max(lastLobbyId, legacyLobbyId);
                        continue;
                    }
                }

                int comma = line.IndexOf(',');
                int semicolon = line.LastIndexOf(';');
                if (lobbyId <= 0 || comma <= 0 || semicolon < comma) continue;

                string friendCode = line.Substring(0, comma).Trim();
                string playerName = line.Substring(comma + 1, semicolon - comma - 1).Trim();
                if (friendCode.Length == 0) continue;

                if (!Players.TryGetValue(friendCode, out PlayerRecord record))
                {
                    record = new PlayerRecord { FriendCode = friendCode, PlayerName = playerName };
                    Players.Add(friendCode, record);
                }
                else if (playerName.Length > 0)
                {
                    record.PlayerName = playerName;
                }
                record.LobbyIds.Add(lobbyId);
            }

            PruneExpiredLobbies();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "LobbyPlayerHistoryLoad");
        }
    }

    private static void PruneExpiredLobbies()
    {
        DateTime cutoffUtc = DateTime.UtcNow - LobbyRetentionWindow;
        List<int> expiredLobbyIds = LobbySections
            .Where(pair => pair.Value.CreatedUtc < cutoffUtc)
            .Select(pair => pair.Key)
            .ToList();

        if (expiredLobbyIds.Count == 0) return;

        foreach (int lobbyId in expiredLobbyIds)
        {
            LobbySections.Remove(lobbyId);
            foreach (PlayerRecord record in Players.Values)
            {
                record.LobbyIds.Remove(lobbyId);
            }
        }

        RewriteHistoryFile();
    }

    private static void RewriteHistoryFile()
    {
        try
        {
            string directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            List<string> lines = new();
            foreach (KeyValuePair<int, LobbySection> lobby in LobbySections.OrderBy(pair => pair.Key))
            {
                int lobbyId = lobby.Key;
                LobbySection section = lobby.Value;
                lines.Add($"{lobbyId}|{section.CreatedUtc.ToString("O", CultureInfo.InvariantCulture)}|{section.RoomCode}:");
                foreach (KeyValuePair<string, PlayerRecord> entry in Players.Where(pair => pair.Value.LobbyIds.Contains(lobbyId)).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add($"{entry.Key},{entry.Value.PlayerName};");
                }
            }

            File.WriteAllLines(HistoryPath, lines, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "LobbyPlayerHistoryRewrite");
        }
    }

    private static void AppendLine(string line)
    {
        try
        {
            string directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(HistoryPath, line + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "LobbyPlayerHistoryWrite");
        }
    }

    private static void StartNewSection(string roomCode)
    {
        currentLobbyId = ++lastLobbyId;
        LobbySection section = new()
        {
            CreatedUtc = DateTime.UtcNow,
            RoomCode = NormalizeRoomCode(roomCode)
        };
        LobbySections[currentLobbyId] = section;
        AppendLine($"{currentLobbyId}|{section.CreatedUtc.ToString("O", CultureInfo.InvariantCulture)}|{section.RoomCode}:");
        Logger.Info($"Started player-history section {currentLobbyId} for {section.CreatedUtc:yyyy-MM-dd} room {section.RoomCode}.", "LobbyPlayerHistory");
    }

    private static string GetCurrentRoomCode()
    {
        try
        {
            string roomCode = GameStartManager.Instance?.GameRoomNameCode?.text;
            return NormalizeRoomCode(roomCode);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "LobbyPlayerHistoryRoomCode");
            return string.Empty;
        }
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return string.Empty;
        return new string(roomCode.Where(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            .ToArray()).ToUpperInvariant();
    }

    private static string CleanPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return "Unknown";
        return playerName.Replace(',', ' ').Replace(';', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
