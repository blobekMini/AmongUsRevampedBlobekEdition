using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Hazel;
using InnerNet;
using UnityEngine;

namespace AmongUsRevamped
{
    // https://github.com/tukasa0001/TownOfHost
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
    public static class OptionsMenuBehaviourStartPatch
    {
        private static ClientOptionItem GM;
        private static ClientOptionItem ShowFPS;
        private static ClientOptionItem AutoStart;
        private static ClientOptionItem DarkTheme;
        private static ClientOptionItem LobbyMusic;
        private static ClientOptionItem DisableInfoWhenDead;
        private static ClientOptionItem DisableCommandHelper;
        private static ClientOptionItem DisableCompatibilityWarning;
        private static ClientOptionItem DisableChatModifier;
        private static ClientOptionItem DisableLobbyInfo;
        private static ClientOptionItem AlwaysShowChat;
        private static ClientOptionItem SeeGhosts;
        private static ClientOptionItem EnableZoom;
        private static ClientOptionItem PreviewRoles;
        private static ClientOptionItem PreviewDevices;
        private static ClientOptionItem PreviewLevels;

        public static void Postfix(OptionsMenuBehaviour __instance)
        {
            if (__instance.DisableMouseMovement == null) return;

            if (GM == null || GM.ToggleButton == null)
            {
                GM = ClientOptionItem.Create(Translator.Get("gameMaster"), Main.GM, __instance, GMButtonToggle);

                static void GMButtonToggle()
                {
                    if (Main.GM.Value && Utils.IsLobby) HudManager.Instance.ShowPopUp(Translator.Get("gameMasterWarning"));
                }
            }

            if (ShowFPS == null || ShowFPS.ToggleButton == null)
                ShowFPS = ClientOptionItem.Create(Translator.Get("showFPS"), Main.ShowFps, __instance);

            if (DisableInfoWhenDead == null || DisableInfoWhenDead.ToggleButton == null)
                DisableInfoWhenDead = ClientOptionItem.Create(Translator.Get("disableInfoWhenDead"), Main.DisableInfoWhenDead, __instance);

            if (AutoStart == null || AutoStart.ToggleButton == null)
            {
                AutoStart = ClientOptionItem.Create(Translator.Get("autoStart"), Main.AutoStart, __instance, AutoStartButtonToggle);

                static void AutoStartButtonToggle()
                {
                    if (!Main.AutoStart.Value && GameStartManager.InstanceExists &&
                        GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown)
                    {
                        GameStartManager.Instance.ResetStartState();
                        Logger.SendInGame(Translator.Get("CancelStartCountDown"));
                    }
                }
            }

            if (DarkTheme == null || DarkTheme.ToggleButton == null)
                DarkTheme = ClientOptionItem.Create(Translator.Get("enableDarkTheme"), Main.DarkTheme, __instance);

            if (LobbyMusic == null || LobbyMusic.ToggleButton == null)
                LobbyMusic = ClientOptionItem.Create(Translator.Get("enableLobbyMusic"), Main.LobbyMusic, __instance);

            if (DisableCommandHelper == null || DisableCommandHelper.ToggleButton == null)
                DisableCommandHelper = ClientOptionItem.Create(Translator.Get("disableCommandHelper"), Main.DisableCommandHelper, __instance);

            if (DisableCompatibilityWarning == null || DisableCompatibilityWarning.ToggleButton == null)
                DisableCompatibilityWarning = ClientOptionItem.Create(Translator.Get("disableCompatibilityWarning"), Main.DisableCompatibilityWarning, __instance);

            if (DisableChatModifier == null || DisableChatModifier.ToggleButton == null)
                DisableChatModifier = ClientOptionItem.Create(Translator.Get("disableChatModifier"), Main.DisableChatModifier, __instance);

            if (DisableLobbyInfo == null || DisableLobbyInfo.ToggleButton == null)
                DisableLobbyInfo = ClientOptionItem.Create(Translator.Get("disableLobbyInfo"), Main.DisableLobbyInfo, __instance);

            if (AlwaysShowChat == null || AlwaysShowChat.ToggleButton == null)
            {
                AlwaysShowChat = ClientOptionItem.Create(Translator.Get("alwaysShowChat"), Main.AlwaysShowChat, __instance, () =>
                {
                    if (Utils.InGame && HudManager.Instance != null)
                    {
                        HudManager.Instance.Chat.gameObject.SetActive(true);
                        if (Main.AlwaysShowChat.Value) HudManager.Instance.Chat.SetVisible(true);
                    }
                });
            }

            if (SeeGhosts == null || SeeGhosts.ToggleButton == null)
            {
                SeeGhosts = ClientOptionItem.Create(Translator.Get("seeGhosts"), Main.SeeGhosts, __instance, PlayerControlVisibilityPatch.RefreshGhostVisibility);
            }

            if (EnableZoom == null || EnableZoom.ToggleButton == null)
                EnableZoom = ClientOptionItem.Create(Translator.Get("enableZoom"), Main.EnableZoom, __instance);

            if (PreviewRoles == null || PreviewRoles.ToggleButton == null)
                PreviewRoles = ClientOptionItem.Create(Translator.Get("previewRoles"), Main.PreviewRoles, __instance, PlayerPreviewLabels.Refresh);

            if (PreviewDevices == null || PreviewDevices.ToggleButton == null)
                PreviewDevices = ClientOptionItem.Create(Translator.Get("previewDevices"), Main.PreviewDevices, __instance, PlayerPreviewLabels.Refresh);

            if (PreviewLevels == null || PreviewLevels.ToggleButton == null)
                PreviewLevels = ClientOptionItem.Create(Translator.Get("previewLevels"), Main.PreviewLevels, __instance, PlayerPreviewLabels.Refresh);

        }
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close))]
    public static class OptionsMenuBehaviourClosePatch
    {
        public static void Postfix()
        {
            ClientOptionItem.CustomBackground?.gameObject.SetActive(false);
        }
    }

    // MY MODSTAMP IS FASTER THAN YOURS!!11!!!1
    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
    internal class SplashLogoAnimatorPatch
    {
        public static void Prefix(SplashManager __instance)
        {
            ModManager.Instance.ShowModStamp();
            __instance.sceneChanger.AllowFinishLoadingScene();
            __instance.startedSceneLoad = true;
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class HudManager_Update
    {
	    public static void Postfix(HudManager __instance)
        {
            PlayerPreviewLabels.Update(Time.deltaTime);

            bool keepChatVisible = (Main.GM.Value && AmongUsClient.Instance.AmHost) || (Main.AlwaysShowChat.Value && Utils.InGame);
            if (keepChatVisible && __instance != null && __instance.Chat != null)
            {
			    __instance.Chat.gameObject.SetActive(true);
			}

			if (Main.GM.Value && AmongUsClient.Instance.AmHost)
			{
			    __instance.MapButton.gameObject.SetActive(true); 
            }
        }
    }

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
internal static class AlwaysShowChatPatch
{
    public static void Prefix(ref bool __0)
    {
        if (Main.AlwaysShowChat.Value && Utils.InGame) __0 = true;
    }
}

[HarmonyPatch(typeof(PlayerControl), "get_Visible")]
internal static class PlayerControlVisibilityPatch
{
    private static bool ShouldRevealGhost(PlayerControl player)
    {
        PlayerControl localPlayer = PlayerControl.LocalPlayer;
        return Main.SeeGhosts.Value && Utils.InGame && localPlayer != null && localPlayer.Data != null &&
            !localPlayer.Data.IsDead && player != null && player.Data != null && player.Data.IsDead;
    }

    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (ShouldRevealGhost(__instance)) __result = true;
    }

    public static void RefreshGhostVisibility()
    {
        if (!Utils.InGame || PlayerControl.LocalPlayer?.Data == null || PlayerControl.LocalPlayer.Data.IsDead) return;

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data != null && player.Data.IsDead)
            {
                player.Visible = Main.SeeGhosts.Value;
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), "set_Visible")]
internal static class PlayerControlVisibilitySetterPatch
{
    public static void Prefix(PlayerControl __instance, ref bool value)
    {
        PlayerControl localPlayer = PlayerControl.LocalPlayer;
        if (Main.SeeGhosts.Value && Utils.InGame && localPlayer != null && localPlayer.Data != null &&
            !localPlayer.Data.IsDead && __instance != null && __instance.Data != null && __instance.Data.IsDead)
        {
            value = true;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class GhostChatMessagePatch
{
    internal sealed class MessageState
    {
        public PlayerControl Sender;
        public string Text;
        public bool Displayed;
    }

    private static MessageState currentMessage;
    public static MessageState CurrentMessage => currentMessage;

    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader, out MessageState __state)
    {
        __state = null;
        if ((RpcCalls)callId != RpcCalls.SendChat || reader == null) return;

        try
        {
            MessageReader copy = MessageReader.Get(reader);
            __state = new MessageState { Sender = __instance, Text = copy.ReadString() };
            currentMessage = __state;
        }
        catch
        {
            currentMessage = null;
        }
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, MessageState __state)
    {
        if (__state == null || (RpcCalls)callId != RpcCalls.SendChat) return;
        if (ReferenceEquals(currentMessage, __state)) currentMessage = null;

        PlayerControl localPlayer = PlayerControl.LocalPlayer;
        if (__state.Displayed || !Main.SeeGhosts.Value || !Utils.InGame || localPlayer?.Data == null ||
            __state.Sender?.Data == null || !__state.Sender.Data.IsDead || HudManager.Instance?.Chat == null) return;

        if (localPlayer.Data.IsDead && localPlayer.PlayerId == __state.Sender.PlayerId)
        {
            return;
        }

        HudManager.Instance.Chat.AddChat(__state.Sender, __state.Text);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class GhostChatBubbleTrackingPatch
{
    public static void Postfix(object[] __args)
    {
        MessageStateMatch(__args);
    }

    private static void MessageStateMatch(object[] arguments)
    {
        GhostChatMessagePatch.MessageState state = GhostChatMessagePatch.CurrentMessage;
        if (state == null || arguments == null) return;

        PlayerControl sender = arguments.OfType<PlayerControl>().FirstOrDefault();
        string text = arguments.OfType<string>().FirstOrDefault();
        if (sender == state.Sender && string.Equals(text, state.Text, StringComparison.Ordinal)) state.Displayed = true;
    }
}

internal static class PlayerPreviewLabels
{
    private static readonly Dictionary<byte, TMPro.TextMeshPro> Labels = new();
    private static float refreshTimer;

    public static void Refresh()
    {
        refreshTimer = 0f;
        UpdateLabels();
    }

    public static void Update(float deltaTime)
    {
        refreshTimer += deltaTime;
        if (refreshTimer < 0.2f) return;
        refreshTimer = 0f;
        UpdateLabels();
    }

    private static void UpdateLabels()
    {
        bool hasPreviews = Main.PreviewRoles.Value || Main.PreviewDevices.Value || Main.PreviewLevels.Value;
        if (!Utils.InGame && !Utils.IsLobby) hasPreviews = false;

        HashSet<byte> activePlayers = new();
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data == null || player.cosmetics == null || player.cosmetics.nameText == null) continue;
            activePlayers.Add(player.PlayerId);

            if (!hasPreviews)
            {
                if (Labels.TryGetValue(player.PlayerId, out TMPro.TextMeshPro existingLabel) && existingLabel != null)
                    existingLabel.gameObject.SetActive(false);
                continue;
            }

            if (!Labels.TryGetValue(player.PlayerId, out TMPro.TextMeshPro label) || label == null)
            {
                TMPro.TextMeshPro nameText = player.cosmetics.nameText;
                label = Object.Instantiate(nameText, nameText.transform.parent);
                label.name = $"AURPlayerPreview_{player.PlayerId}";
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.fontSize = nameText.fontSize * 0.72f;
                label.transform.localScale = nameText.transform.localScale;
                Labels[player.PlayerId] = label;
            }

            label.gameObject.SetActive(true);
            label.transform.localPosition = player.cosmetics.nameText.transform.localPosition + new Vector3(0f, 0.24f, -0.01f);

            List<string> previews = new();
            if (Main.PreviewRoles.Value) previews.Add(player.Data.RoleType.ToString());
            if (Main.PreviewDevices.Value)
            {
                var client = AmongUsClient.Instance?.GetClientFromCharacter(player);
                string device = client?.PlatformData?.Platform.ToString() ?? "Unknown";
                previews.Add(device);
            }
            if (Main.PreviewLevels.Value) previews.Add($"Lv.{player.Data.PlayerLevel}");

            label.text = string.Join(" | ", previews);
            label.color = Color.white;
        }

        foreach (byte playerId in Labels.Keys.Where(id => !activePlayers.Contains(id)).ToArray())
        {
            if (Labels.TryGetValue(playerId, out TMPro.TextMeshPro label) && label != null)
                Object.Destroy(label.gameObject);
            Labels.Remove(playerId);
        }
    }
}
}