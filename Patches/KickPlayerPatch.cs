using System;
using System.Linq;
using InnerNet;
using UnityEngine;

// https://github.com/tukasa0001/TownOfHost/blob/main/Patches/ClientPatch.cs
namespace AmongUsRevamped;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
internal class KickPlayerPatch
{
    public static Dictionary<string, int> AttemptedKickPlayerList = [];
    private static readonly Dictionary<int, DateTime> PendingKickRequests = new();

    public static bool Prefix(InnerNetClient __instance, int clientId, bool ban)
    {
        if (__instance == null || !__instance.AmHost) return true;

        int hostClientId = __instance.HostId;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            hostClientId = AmongUsClient.Instance.ClientId;

        if (clientId == hostClientId ||
            (PlayerControl.LocalPlayer?.Data != null && clientId == PlayerControl.LocalPlayer.Data.ClientId))
        {
            Logger.Warn($"Blocked {(ban ? "ban" : "kick")} request targeting the local lobby host (client {clientId}).", "HostKickProtection");
            return false;
        }

        ClientData target = AmongUsClient.Instance.GetRecentClient(clientId);
        Logger.Info($"Requested {(ban ? "ban" : "kick")} for {target?.PlayerName ?? $"client {clientId}"}.", "KickPlayer");
        PendingKickRequests[clientId] = DateTime.UtcNow;
        if (ban) BanManager.AddBanPlayer(target);

        return true;
    }

    public static bool WasRecentlyKicked(ClientData client)
    {
        if (client == null) return false;

        foreach (int clientId in PendingKickRequests
            .Where(entry => DateTime.UtcNow - entry.Value > TimeSpan.FromSeconds(30))
            .Select(entry => entry.Key)
            .ToArray())
        {
            PendingKickRequests.Remove(clientId);
        }

        if (!PendingKickRequests.Remove(client.Id)) return false;
        Logger.Info($"{client.PlayerName} disconnected after a kick/ban request.", "KickPlayer");
        return true;
    }
}