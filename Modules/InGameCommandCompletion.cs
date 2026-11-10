using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace AmongUsRevamped;

internal static class InGameCommandCompletion
{
    public static string[] GetCandidates(bool completingCommand, string token, string[] commands)
    {
        IEnumerable<string> source;
        if (completingCommand)
        {
            source = commands ?? Array.Empty<string>();
        }
        else
        {
            List<string> players = new();
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player?.Data != null && !string.IsNullOrWhiteSpace(player.Data.PlayerName))
                    players.Add(player.Data.PlayerName);
            }

            string[] colors = Enum.GetNames(typeof(Main.ColorToString));
            string[] roles = Enum.GetNames(typeof(RoleTypes)).Select(role => role.ToLowerInvariant()).ToArray();
            source = new[] { "@me", "@all" }
                .Concat(players)
                .Concat(colors)
                .Concat(colors.Select(color => "!color:" + color))
                .Concat(new[] { "!color:gay", "!color:rainbow", "imp", "crew", "sci", "eng", "ss", "det", "nana", "noise", "track", "ph" })
                .Concat(roles)
                .Concat(new[]
                {
                    "rainbow", "gay", "stop", "add", "remove", "help",
                    "impostorvision", "crewmatevision", "playerspeed", "killcooldown",
                    "shorttasks", "longtasks", "commontasks", "emergencymeetings",
                    "emergencycooldown", "discussiontime", "votingtime"
                });
        }

        return source.Where(value => value.StartsWith(token ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
