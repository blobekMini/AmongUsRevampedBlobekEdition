using InnerNet;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AmongUsRevamped;

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
internal static class CommandHelper
{
    private static TextMeshPro HelperText;

    public class Cmd
    {
        public string[] Name;
        public string Prompt;
        public string Description;
        public bool HasVariable;
    }

    static readonly List<Cmd> Commands = new()
    {
        new Cmd { Name = new[] { "/help" }, Prompt = "/help", Description = Translator.Get("helpDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/page" }, Prompt = "/page {number} {command} [arguments]", Description = "Show a saved output page", HasVariable = true },

        new Cmd { Name = new[] { "/sns" }, Prompt = "/sns", Description = Translator.Get("snsDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/shiftandseek" }, Prompt = "/shiftandseek", Description = Translator.Get("snsDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/sns2" }, Prompt = "/sns2", Description = Translator.Get("snsTwoDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/0kc" }, Prompt = "/0kc", Description = Translator.Get("0kcDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/0killcooldown" }, Prompt = "/0killcooldown", Description = Translator.Get("0kcDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/sr" }, Prompt = "/sr", Description = Translator.Get("SrDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/speedrun" }, Prompt = "/speedrun", Description = Translator.Get("SrDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/pns" }, Prompt = "/pns", Description = Translator.Get("pnsDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/poofandseek" }, Prompt = "/poofandseek", Description = Translator.Get("pnsDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/eg" }, Prompt = "/eg", Description = Translator.Get("egDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/endgame" }, Prompt = "/endgame", Description = Translator.Get("egDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/em" }, Prompt = "/em", Description = Translator.Get("emDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/endmeeting" }, Prompt = "/endmeeting", Description = Translator.Get("emDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/sm" }, Prompt = "/sm", Description = Translator.Get("skipMeetingDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/skip" }, Prompt = "/skip", Description = Translator.Get("skipMeetingDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/skipmeeting" }, Prompt = "/skipmeeting", Description = Translator.Get("skipMeetingDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/aur" }, Prompt = "/aur", Description = Translator.Get("aurDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/socials" }, Prompt = "/socials", Description = Translator.Get("aurDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/s" }, Prompt = "/s", Description = Translator.Get("sDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/start" }, Prompt = "/start", Description = Translator.Get("sDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/r" }, Prompt = "/r", Description = Translator.Get("rDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/roles" }, Prompt = "/roles", Description = Translator.Get("rolesDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/kick" }, Prompt = "/kick {name}", Description = Translator.Get("kickDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/ckick" }, Prompt = "/ckick {color}", Description = Translator.Get("ckickDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/ban" }, Prompt = "/ban {name}", Description = Translator.Get("banDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/cban" }, Prompt = "/cban {color}", Description = Translator.Get("cbanDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/dump" }, Prompt = "/dump", Description = Translator.Get("dumpDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/col" }, Prompt = "/col {color}", Description = Translator.Get("colDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/color" }, Prompt = "/color {color}", Description = Translator.Get("colDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/colour" }, Prompt = "/colour {color}", Description = Translator.Get("colDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/colout" }, Prompt = "/colout {color|rainbow|stop}", Description = Translator.Get("colDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/ucol", "/ucolor", "/ucolour" }, Prompt = "/ucolour {color} {player}", Description = Translator.Get("ucolDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/ucolout" }, Prompt = "/ucolout {color|rainbow|stop} {player}", Description = Translator.Get("ucolDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/give" }, Prompt = "/give {role} {player1} [player2] ... | @all", Description = Translator.Get("giveDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/set" }, Prompt = "/set {role} {player1} {player2} ... | @all", Description = Translator.Get("setDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/add" }, Prompt = "/add {role} {player1} {player2} ...", Description = Translator.Get("addDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/rolereset" }, Prompt = "/rolereset {role}", Description = Translator.Get("roleResetDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/playerreset" }, Prompt = "/playerreset {player}", Description = Translator.Get("playerResetDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/impcount", "/setimp" }, Prompt = "/setimp {0-15}", Description = "Set total impostors, preserving pre-set roles and randomly rolling the rest", HasVariable = true },
        new Cmd { Name = new[] { "/settings" }, Prompt = "/settings {help|setting value}", Description = "Show or change supported normal-mode game settings", HasVariable = true },
        new Cmd { Name = new[] { "/say" }, Prompt = "/say {message}", Description = "Send a message through the game's normal chat pipeline", HasVariable = true },
        new Cmd { Name = new[] { "/voteout" }, Prompt = "/voteout {player}", Description = Translator.Get("voteOutDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/cvoteout" }, Prompt = "/cvoteout {color}", Description = Translator.Get("colorVoteOutDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/setvotes" }, Prompt = "/setvotes {player|!color:black|@all|@me} {0-100}", Description = "Set a player's vote weight for the current meeting", HasVariable = true },
        new Cmd { Name = new[] { "/revive" }, Prompt = "/revive [player|@all]", Description = Translator.Get("reviveDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/tm", "/tiemeeting" }, Prompt = "/tm", Description = Translator.Get("tieMeetingDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/copyfit" }, Prompt = "/copyfit {source} {target1,target2,...}", Description = "Copy a player's full outfit to one or more players", HasVariable = true },
        new Cmd { Name = new[] { "/whitelist" }, Prompt = "/whitelist {add|remove} {player}", Description = "Manage command-related anti-cheat grace for admins", HasVariable = true },
        new Cmd { Name = new[] { "/kill" }, Prompt = "/kill {player}", Description = "Kill a player (host permission required)", HasVariable = true },
        new Cmd { Name = new[] { "/tp", "/teleport" }, Prompt = "/tp {player} !room:{security|cafeteria|o2|navigation}", Description = "Teleport one player to a room during a game", HasVariable = true },
            new Cmd { Name = new[] { "/impregnate" }, Prompt = "/impregnate {player1} {player2} {player3}", Description = "Run a 10-second host-controlled player movement sequence", HasVariable = true },
        new Cmd { Name = new[] { "/bind" }, Prompt = "/bind '<command>' '<trigger>' [;:prefix;] | list | remove '<trigger>'", Description = "Create host-local text command binds", HasVariable = true },

        new Cmd { Name = new[] { "/reload" }, Prompt = "/reload", Description = Translator.Get("reloadDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/vip" }, Prompt = "/vip {name}", Description = Translator.Get("vipDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/moderator" }, Prompt = "/moderator {name}", Description = Translator.Get("moderatorDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/admin" }, Prompt = "/admin {name}", Description = Translator.Get("adminDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/removevip" }, Prompt = "/removevip {name}", Description = Translator.Get("cremoveVipDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/removemoderator" }, Prompt = "/removemoderator {name}", Description = Translator.Get("removeModeratorDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/removeadmin" }, Prompt = "/removeadmin {name}", Description = Translator.Get("removeAdminDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/lastgame" }, Prompt = "/lastgame", Description = Translator.Get("lastgameDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/t" }, Prompt = "/t {template}", Description = Translator.Get("templateDesc"), HasVariable = true },

        new Cmd { Name = new[] { "/seeker" }, Prompt = "/seeker {name}", Description = Translator.Get("seekerDesc"), HasVariable = true },
        new Cmd { Name = new[] { "/seekers" }, Prompt = "/seekers", Description = Translator.Get("seekersDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/clearseeker" }, Prompt = "/clearseeker", Description = Translator.Get("clearSeekerDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/mapvote" }, Prompt = "/mapvote", Description = Translator.Get("mapVoteDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/gmvote" }, Prompt = "/gmvote", Description = Translator.Get("modeVoteDesc"), HasVariable = false },

        new Cmd { Name = new[] { "/private" }, Prompt = "/private", Description = Translator.Get("privateDesc"), HasVariable = false },
        new Cmd { Name = new[] { "/public" }, Prompt = "/public", Description = Translator.Get("publicDesc"), HasVariable = false }
    };

    [HarmonyPostfix]
    public static void Postfix(TextBoxTMP __instance)
    {
        if (!HudManager.InstanceExists || HudManager.Instance.Chat == null || HudManager.Instance.Chat.freeChatField == null || HudManager.Instance.Chat.freeChatField.textArea == null || __instance != HudManager.Instance.Chat.freeChatField.textArea) return;

        if (Main.DisableCommandHelper.Value || !AmongUsClient.Instance.AmHost)
        {
            if (HelperText != null) HelperText.enabled = false;
            return;
        }

        if (!HelperText)
        {
            HelperText = Object.Instantiate(__instance.outputText, __instance.outputText.transform.parent);
            HelperText.name = "HelperText";
            HelperText.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            HelperText.transform.localPosition = __instance.outputText.transform.localPosition;
            HelperText.enabled = false;
            HelperText.fontSharedMaterial = new Material(__instance.outputText.fontSharedMaterial);
        }

        string t = __instance.text;

        if (string.IsNullOrEmpty(t) || (!t.StartsWith("/", StringComparison.Ordinal) && !t.StartsWith("!", StringComparison.Ordinal)))
        {
            HelperText.enabled = false;
            return;
        }

        if (t.StartsWith("!", StringComparison.Ordinal)) t = "/" + t.Substring(1);

        string textCmd = t.Split(' ')[0];

        Cmd u = null;
        Cmd v = null;

        foreach (var cmd in Commands)
        {
            foreach (var a in cmd.Name)
            {
                string aliasCmd = a;

                if (aliasCmd.Equals(textCmd, StringComparison.OrdinalIgnoreCase))
                {
                    v = cmd;
                    u = cmd;
                    break;
                }

                if (aliasCmd.StartsWith(textCmd, StringComparison.OrdinalIgnoreCase))
                {
                    u ??= cmd;
                }
            }

            if (v != null) break;
        }

        if (v != null && !v.HasVariable && t.Length > textCmd.Length)
        {
            HelperText.enabled = false;
            return;
        }

        __instance.outputText.enabled = true;

        if (v != null && v.Name.Any(alias => alias.Equals(textCmd, StringComparison.OrdinalIgnoreCase)))
        {
            HelperText.text = $"\n\n<size=80%><color=#FF0000>{v.Description}</color></size>";
        }
        else if (u != null)
        {
            HelperText.text = u.Prompt;
        }
        else
        {
            HelperText.enabled = false;
            return;
        }
        HelperText.enabled = true;
    }
}