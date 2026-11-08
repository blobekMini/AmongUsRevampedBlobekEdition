using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using System;
using InnerNet;
using UnityEngine;

namespace AmongUsRevamped;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class CoStartGamePatch
{
    public static void Postfix(AmongUsClient __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        Logger.Info(" -------- GAME STARTED --------", "StartGame");
        Logger.Info($" Gamemode: {Options.Gamemode.GetString()}", "StartGame");
        Logger.Info($" Players: {PlayerControl.AllPlayerControls.Count}", "StartGame");
        Logger.Info($" Map: {(MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId}", "StartGame");

        LobbyPlayerHistory.CaptureRoundPlayers();
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            TurkishTextRainbow.CheckPlayerName(player);
        }

        if (Main.GM.Value)
        {
            Logger.Info($" Game Master Enabled", "StartGame");      
        }

        NormalGameEndChecker.imps.Clear();
        NormalGameEndChecker.LastWinReason = "";

    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
class PlayerControlSetRolePatch
{
    public static bool FirstAssign;
    public static readonly HashSet<byte> ProcessedPlayers = new();
    public static HashSet<byte> Seekers = new();
    public static HashSet<byte> manualSeekers = new();
    public static readonly Dictionary<byte, RoleTypes> PendingRoleAssignments = new();
    public static readonly Dictionary<RoleTypes, HashSet<byte>> ExclusiveRolePlayers = new();
    public static int? DesiredImpostorCount;
    private static readonly System.Random rand = new System.Random();
    private static readonly HashSet<byte> PlannedImpostorPlayers = new();
    private static bool ImpostorPlanPrepared;

    private static bool IsImpostorRole(RoleTypes roleType)
    {
        return roleType is RoleTypes.Impostor or RoleTypes.ImpostorGhost or RoleTypes.Shapeshifter or RoleTypes.Phantom or RoleTypes.Viper;
    }

    public static int GetQueuedImpostorCount() => PendingRoleAssignments.Count(assignment => IsImpostorRole(assignment.Value));

    private static RoleTypes GetExclusiveFallbackRole(RoleTypes excludedRole)
    {
        RoleTypes[] crewRoles =
        {
            RoleTypes.Crewmate, RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Noisemaker,
            RoleTypes.Tracker, RoleTypes.Detective, RoleTypes.Judge, RoleTypes.SpiritGuide
        };
        RoleTypes[] impostorRoles = { RoleTypes.Impostor, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper };
        RoleTypes[] candidates = IsImpostorRole(excludedRole) ? impostorRoles : crewRoles;

        foreach (RoleTypes candidate in candidates)
        {
            if (candidate != excludedRole && !ExclusiveRolePlayers.ContainsKey(candidate)) return candidate;
        }

        return IsImpostorRole(excludedRole) ? RoleTypes.Crewmate : RoleTypes.Impostor;
    }

    private static RoleTypes GetAvailableImpostorRole(byte playerId)
    {
        RoleTypes[] candidates = { RoleTypes.Impostor, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper };
        foreach (RoleTypes candidate in candidates)
        {
            if (!ExclusiveRolePlayers.TryGetValue(candidate, out HashSet<byte> allowedPlayers) || allowedPlayers.Contains(playerId))
            {
                return candidate;
            }
        }

        return RoleTypes.Impostor;
    }

    private static RoleTypes GetAvailableCrewmateRole()
    {
        RoleTypes[] candidates =
        {
            RoleTypes.Crewmate, RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Noisemaker,
            RoleTypes.Tracker, RoleTypes.Detective, RoleTypes.Judge, RoleTypes.SpiritGuide
        };
        foreach (RoleTypes candidate in candidates)
        {
            if (!ExclusiveRolePlayers.ContainsKey(candidate)) return candidate;
        }

        return RoleTypes.Crewmate;
    }

    private static void PrepareImpostorPlan()
    {
        if (ImpostorPlanPrepared) return;
        ImpostorPlanPrepared = true;
        PlannedImpostorPlayers.Clear();

        if (!DesiredImpostorCount.HasValue || Utils.isHideNSeek) return;

        HashSet<byte> forcedImpostors = PendingRoleAssignments
            .Where(assignment => IsImpostorRole(assignment.Value))
            .Select(assignment => assignment.Key)
            .ToHashSet();
        PlannedImpostorPlayers.UnionWith(forcedImpostors);

        int targetCount = Math.Max(DesiredImpostorCount.Value, forcedImpostors.Count);
        List<PlayerControl> candidates = new();
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player.Data == null || PendingRoleAssignments.ContainsKey(player.PlayerId) || forcedImpostors.Contains(player.PlayerId)) continue;
            if (Main.GM.Value && player == PlayerControl.LocalPlayer) continue;
            candidates.Add(player);
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (PlayerControl player in candidates)
        {
            if (PlannedImpostorPlayers.Count >= targetCount) break;
            PlannedImpostorPlayers.Add(player.PlayerId);
        }

        Logger.Info($"Impostor roll: {forcedImpostors.Count} pre-assigned, {PlannedImpostorPlayers.Count - forcedImpostors.Count} randomly selected, target {targetCount}.", "RoleManaging");
    }

    public static bool Prefix(PlayerControl __instance, ref RoleTypes roleType, ref bool canOverrideRole)
    {
        if (__instance?.Data != null && roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
        {
            ChatCommandHelpers.RememberRoleBeforeDeath(__instance, __instance.Data.RoleType);
        }

        if (!FirstAssign || !AmongUsClient.Instance.AmHost) return true;

        if (!ProcessedPlayers.Add(__instance.PlayerId)) return true;
        PrepareImpostorPlan();

        if (PendingRoleAssignments.ContainsKey(__instance.PlayerId))
        {
            ChatCommandHelpers.RememberOriginalRole(__instance, roleType);
        }

        // canOverrideRole = false;

        if (Utils.isHideNSeek && Seekers.Count() == 0 && Options.NumSeekers.GetInt() > 0)
        {
            int seekersCount = Options.NumSeekers.GetInt();

            if (manualSeekers.Count > seekersCount) seekersCount = manualSeekers.Count;

            var candidates = new List<PlayerControl>();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!Main.GM.Value || p != PlayerControl.LocalPlayer) candidates.Add(p);
            }

            if (manualSeekers.Count == 0)
            {
                seekersCount = Math.Min(seekersCount, candidates.Count);
            }
            else
            {
                seekersCount = Math.Min(seekersCount, candidates.Count + manualSeekers.Count);                
            }

            foreach (byte id in manualSeekers)
            {
                if (Seekers.Count >= seekersCount) break;
                Seekers.Add(id);
            }

            candidates.RemoveAll(p => manualSeekers.Contains(p.PlayerId));

            for (int j = candidates.Count - 1; j > 0; j--)
            {
                int k = rand.Next(j + 1);
                (candidates[j], candidates[k]) = (candidates[k], candidates[j]);
            }

            foreach (var p in candidates)
            {
                if (Seekers.Count >= seekersCount) break;
                
                Seekers.Add(p.PlayerId);
            }
        }

        if (Utils.isHideNSeek && Options.NumSeekers.GetInt() > 0)
        {
            if (Seekers.Contains(__instance.PlayerId)) roleType = RoleTypes.Impostor;
            else roleType = RoleTypes.Engineer;
        }

        if (Options.Gamemode.GetValue() == 2 && !Utils.isHideNSeek)
        {
            if (Options.EngineerMode.GetBool()) roleType = RoleTypes.Engineer;
            else roleType = RoleTypes.Crewmate;
        }

        bool hasQueuedRole = PendingRoleAssignments.TryGetValue(__instance.PlayerId, out RoleTypes pendingRole);
        if (hasQueuedRole)
        {
            roleType = pendingRole;
        }
        else
        {
            foreach (var roleGroup in ExclusiveRolePlayers)
            {
                if (roleType == roleGroup.Key && !roleGroup.Value.Contains(__instance.PlayerId))
                {
                    roleType = GetExclusiveFallbackRole(roleGroup.Key);
                    break;
                }

            }
        }

        if (hasQueuedRole)
        {
            PendingRoleAssignments.Remove(__instance.PlayerId);
            Logger.Info($" {__instance.Data.PlayerName} assigned queued role {pendingRole}", "RoleManaging");
        }
        else if (DesiredImpostorCount.HasValue && !Utils.isHideNSeek)
        {
            if (PlannedImpostorPlayers.Contains(__instance.PlayerId))
            {
                roleType = GetAvailableImpostorRole(__instance.PlayerId);
            }
            else if (IsImpostorRole(roleType))
            {
                roleType = GetAvailableCrewmateRole();
            }
        }

        if (ProcessedPlayers.Count >= PlayerControl.AllPlayerControls.Count)
        {
            Seekers.Clear();
            ExclusiveRolePlayers.Clear();
            PlannedImpostorPlayers.Clear();
            ImpostorPlanPrepared = false;
            FirstAssign = false;
            Logger.Info(" PCSRP successful", "RoleManaging");
        }
    
        return true;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.OnGameStart))]
internal static class OnGameStartPatch
{
    public static bool PastStartScreen;
    public static void Postfix()
    {
        if (AmongUsClient.Instance.AmHost && Options.Gamemode.GetValue() == 1 && Options.SNSChatInGame.GetBool() && !Utils.isHideNSeek)
        {
            PlayerControl.LocalPlayer.CmdReportDeadBody(null);
            if (MeetingHud.Instance != null) MeetingHud.Instance.RpcClose();
        }

        if (AmongUsClient.Instance.AmHost && Options.Gamemode.GetValue() == 3 && Options.PNSChatInGame.GetBool() && !Utils.isHideNSeek)
        {
            PlayerControl.LocalPlayer.CmdReportDeadBody(null);
            if (MeetingHud.Instance != null) MeetingHud.Instance.RpcClose();
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            Utils.StoredRoleText[p.PlayerId] = Utils.GetRoleText(p);
        }

        PastStartScreen = true;

        if (AmongUsClient.Instance.AmHost && Main.GM.Value)
        {
            if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3)) return;
            
            Logger.Info($" Game Master Successful", "StartGame");
            PlayerControl.LocalPlayer.Exiled();
            PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.CrewmateGhost);
        }
    }

    // All Patches below are for the speeded up Chat In Game.
    // We force the complete intro to skip using 2 patches.
    // Some PlayerData related things return null when you do this, so we have to set those all as well.
    [HarmonyPatch(typeof(ShhhBehaviour), nameof(ShhhBehaviour.PlayAnimation))]
    public static class ShhhBehaviourPlayAnimationPatch
	{
		static bool Prefix()
		{
            if (!AmongUsClient.Instance.AmHost) return true;

            if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3))
            {
			    HudManager.Instance.shhhEmblem.gameObject.SetActive(false);
			    return false;
            }
            else return true;
        }
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
    public static class IntroCutsceneCoBeginPatch
    {
        public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            
            if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3))
            {
                __result = SkipIntro(__instance, __result).WrapToIl2Cpp();
            }
        }

        private static System.Collections.IEnumerator SkipIntro(IntroCutscene i, Il2CppSystem.Collections.IEnumerator k)
        {
            int steps = 0;

            while (k.MoveNext() && steps++ < 1024) {}

            if (steps >= 1024)
            {
                if (i != null && i.gameObject != null) i.gameObject.SetActive(false);
            }
            yield break;
        }
    }

    private static NamePlateViewData meetingNameplate;
    [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetNameplate))]
    public static class CosmeticsCacheGetNameplatePatch
    {
        public static bool Prefix(ref NamePlateViewData __result)
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3))
            {
                if (MeetingHud.Instance == null) return true;

                if (meetingNameplate == null)
                {
                    meetingNameplate = ScriptableObject.CreateInstance<NamePlateViewData>();
                    meetingNameplate.Image = null;
                }

                __result = meetingNameplate;
                return false;
            }
            else return true;
        }
    }

    [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.Destroy))]
    public static class CosmeticsCacheDestroyPatch
    {
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3))
            {
                if (meetingNameplate != null) Object.Destroy(meetingNameplate);
                meetingNameplate = null;
            }
        }
    }

    [HarmonyPatch(typeof(ShapeshifterPanel), nameof(ShapeshifterPanel.SetPlayer))]
    public static class ShapeshifterPanelSetPlayerPatch
    {
        public static bool Prefix(ShapeshifterPanel __instance, int index, NetworkedPlayerInfo playerInfo, Il2CppSystem.Action onShift)
        {
            if (__instance == null || playerInfo == null || !AmongUsClient.Instance.AmHost) return true;

           if ((Options.SNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 1) || (Options.PNSChatInGameFast.GetBool() && Options.Gamemode.GetValue() == 3))
           {
                __instance.shapeshift = onShift;
                __instance.PlayerIcon.SetFlipX(false);
                __instance.PlayerIcon.ToggleName(false);

                SpriteRenderer[] componentsInChildren = __instance.GetComponentsInChildren<SpriteRenderer>();
                foreach (var spriteRenderer in componentsInChildren)
                {
                    spriteRenderer.material.SetInt(PlayerMaterial.MaskLayer, index + 2);
                }

                __instance.PlayerIcon.SetMaskLayer(index + 2);
                __instance.PlayerIcon.UpdateFromEitherPlayerDataOrCache(playerInfo, PlayerOutfitType.Default, PlayerMaterial.MaskType.ComplexUI, false, null);

                __instance.NameText.text = playerInfo.PlayerName;
                __instance.LevelNumberText.text = ProgressionManager.FormatVisualLevel(playerInfo.PlayerLevel);

                return false;
            }
            else return true;
        }
    }
}