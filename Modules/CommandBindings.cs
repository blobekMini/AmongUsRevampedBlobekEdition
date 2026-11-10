using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AmongUsRevamped;

internal static class CommandBindings
{
    private sealed class Binding
    {
        public string CommandTemplate { get; init; }
        public string TriggerTemplate { get; init; }
        public string ValuePrefix { get; init; }
    }

    private static readonly List<Binding> Bindings = new();
    private static readonly object Sync = new();
    private static readonly string BindingsPath = Path.Combine(BanManager.DataPath, "AUR-DATA", "CommandBindings.txt");
    private static readonly HashSet<string> PrivateCommandRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "/give", "/set", "/add", "/rolereset", "/playerreset", "/voteout", "/cvoteout", "/revive",
        "/copyfit", "/kill", "/tm", "/tiemeeting", "/impcount", "/setimp", "/settings", "/em", "/endmeeting",
        "/sm", "/skip", "/skipmeeting", "/color", "/colour", "/col", "/colout", "/ucolor", "/tp", "/teleport",
            "/sm", "/skip", "/skipmeeting", "/color", "/colour", "/col", "/colout", "/ucolor", "/tp", "/teleport", "/impregnate",
        "/ucolour", "/ucol", "/ucolout", "/start", "/s", "/endgame", "/eg", "/mapvote", "/gmvote"
    };

    public static void Initialize()
    {
        lock (Sync)
        {
            Bindings.Clear();
            try
            {
                string directory = Path.GetDirectoryName(BindingsPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                if (!File.Exists(BindingsPath))
                {
                    File.WriteAllText(BindingsPath,
                        "# Host-local command binds. Manage with /bind; do not edit while the game is running.\n",
                        new UTF8Encoding(false));
                    return;
                }

                foreach (string line in File.ReadLines(BindingsPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    try
                    {
                        string[] fields = Encoding.UTF8.GetString(Convert.FromBase64String(line.Trim())).Split('\t');
                        if (fields.Length != 3 || !IsValidBinding(fields[0], fields[1], fields[2])) continue;
                        Bindings.Add(new Binding { CommandTemplate = fields[0], TriggerTemplate = fields[1], ValuePrefix = fields[2] });
                    }
                    catch
                    {
                        Logger.Warn("Skipped a malformed entry in CommandBindings.txt.", "CommandBindings");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CommandBindingsLoad");
            }
        }
    }

    public static bool HandleCommand(PlayerControl sender, string text)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || sender != PlayerControl.LocalPlayer)
        {
            Reply("Only the local lobby host can manage command binds.");
            return true;
        }

        string arguments = text.Substring("/bind".Length).Trim();
        if (arguments.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            string[] entries;
            lock (Sync)
            {
                entries = Bindings.Select(binding => $"{binding.TriggerTemplate} => /{binding.CommandTemplate}" +
                    (binding.ValuePrefix.Length > 0 ? $" (value prefix: {binding.ValuePrefix})" : string.Empty)).ToArray();
            }
            Reply(entries.Length == 0 ? "No command binds are configured." : string.Join(Environment.NewLine, entries));
            return true;
        }

        if (arguments.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            string trigger = arguments.Substring("remove ".Length).Trim().Trim('"', '\'');
            bool removed;
            lock (Sync)
            {
                removed = Bindings.RemoveAll(binding => binding.TriggerTemplate.Equals(trigger, StringComparison.OrdinalIgnoreCase)) > 0;
                if (removed) Save();
            }
            Reply(removed ? $"Removed bind for '{trigger}'." : $"No bind found for '{trigger}'.");
            return true;
        }

        if (arguments.Equals("help", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(arguments))
        {
            Reply("Usage: /bind '<command template>' '<trigger template>' [;:prefix;] | /bind list | /bind remove '<trigger>'\nMatched trigger text is shown in chat by default; start the trigger with # to hide it.\nExample: /bind 'ucolor gay ;' 'make ; gay' ;:!color:;");
            return true;
        }

        if (!TryReadQuotedArgument(arguments, 0, out string commandTemplate, out int nextPosition) ||
            !TryReadQuotedArgument(arguments, nextPosition, out string triggerTemplate, out nextPosition))
        {
            Reply("Usage: /bind '<command template>' '<trigger template>' [;:prefix;]");
            return true;
        }

        string transform = arguments.Substring(nextPosition).Trim();
        string valuePrefix = string.Empty;
        if (transform.Length > 0 && !TryParseValuePrefix(transform, out valuePrefix))
        {
            Reply("The optional transform must look like ;:prefix; (example: ;:!color:;).");
            return true;
        }

        commandTemplate = NormalizeCommand(commandTemplate);
        triggerTemplate = NormalizeTemplate(triggerTemplate);
        if (!IsValidBinding(commandTemplate, triggerTemplate, valuePrefix))
        {
            Reply("Invalid bind. Use one ; value in the trigger and at least one ; in a supported command template. Fixed trigger words must use ASCII letters or digits.");
            return true;
        }

        lock (Sync)
        {
            Bindings.RemoveAll(binding => binding.TriggerTemplate.Equals(triggerTemplate, StringComparison.OrdinalIgnoreCase));
            Bindings.Add(new Binding { CommandTemplate = commandTemplate, TriggerTemplate = triggerTemplate, ValuePrefix = valuePrefix });
            Save();
        }

        Reply($"Bound '{triggerTemplate}' to /{commandTemplate}.");
        Logger.Info($"Added host-local bind '{triggerTemplate}' => /{commandTemplate}.", "CommandBindings");
        return true;
    }

    public static bool TryExecute(string input)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null || string.IsNullOrWhiteSpace(input)) return false;
        string[] inputWords = SplitWords(input);
        if (inputWords.Length == 0 || inputWords.Where((word, index) => !IsValidTriggerWord(word, index)).Any()) return false;

        Binding match = null;
        string value = null;
        lock (Sync)
        {
            foreach (Binding binding in Bindings)
            {
                string[] triggerWords = SplitWords(binding.TriggerTemplate);
                if (triggerWords.Length != inputWords.Length) continue;

                int valueIndex = Array.IndexOf(triggerWords, ";");
                if (valueIndex < 0) continue;
                bool matches = true;
                for (int index = 0; index < triggerWords.Length; index++)
                {
                    if (index == valueIndex)
                    {
                        if (!IsAsciiAlphaNumeric(inputWords[index]))
                        {
                            matches = false;
                            break;
                        }
                        continue;
                    }
                    if (!triggerWords[index].Equals(inputWords[index], StringComparison.OrdinalIgnoreCase))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches) continue;

                match = binding;
                value = inputWords[valueIndex];
                break;
            }
        }

        if (match == null) return false;

        if (!match.TriggerTemplate.StartsWith("#", StringComparison.Ordinal))
        {
            string visibleInput = ChatCommandHelpers.SanitizeOutgoingChatText(input);
            if (!string.IsNullOrWhiteSpace(visibleInput))
            {
                SendChatPatch.RegisterVisibleCommandEcho(visibleInput);
                PlayerControl.LocalPlayer.RpcSendChat(visibleInput);
            }
        }

        string transformedValue = match.ValuePrefix + value;
        string command = string.Join(" ", SplitWords(match.CommandTemplate)
            .Select(word => word == ";" ? transformedValue : word));
        string result = ChatCommandHelpers.ExecutePrivateCommand(PlayerControl.LocalPlayer, "/" + command);
        Logger.Info($"Executed host-local bind '{match.TriggerTemplate}' as /{command}; result: {result}", "CommandBindings");
        return true;
    }

    private static bool IsValidBinding(string commandTemplate, string triggerTemplate, string valuePrefix)
    {
        string[] commandWords = SplitWords(commandTemplate);
        string[] triggerWords = SplitWords(triggerTemplate);
        if (commandWords.Length < 2 || triggerWords.Length < 2 || triggerWords.Count(word => word == ";") != 1 ||
            !commandWords.Contains(";") || commandWords.Any(word => word.Contains(';') && word != ";") ||
            triggerWords.Where((word, index) => !IsValidTriggerWord(word, index)).Any() ||
            valuePrefix.Any(char.IsWhiteSpace) || valuePrefix.Contains(';')) return false;

        string commandRoot = "/" + commandWords[0].TrimStart('/', '!');
        return PrivateCommandRoots.Contains(commandRoot) && LocalCommandPanel.IsAllowedConsoleCommand(commandRoot);
    }

    private static bool TryParseValuePrefix(string input, out string prefix)
    {
        prefix = string.Empty;
        input = input.Trim();
        if (input.Length < 3 || !input.StartsWith(";:", StringComparison.Ordinal) || !input.EndsWith(";", StringComparison.Ordinal)) return false;
        prefix = input.Substring(2, input.Length - 3).Trim().Trim('"', '\'');
        return prefix.Length > 0 && !prefix.Any(char.IsWhiteSpace) && !prefix.Contains(';');
    }

    private static bool TryReadQuotedArgument(string input, int start, out string value, out int nextPosition)
    {
        value = string.Empty;
        nextPosition = start;
        while (nextPosition < input.Length && char.IsWhiteSpace(input[nextPosition])) nextPosition++;
        if (nextPosition >= input.Length || input[nextPosition] is not ('\'' or '"')) return false;

        char quote = input[nextPosition++];
        int valueStart = nextPosition;
        while (nextPosition < input.Length && input[nextPosition] != quote) nextPosition++;
        if (nextPosition >= input.Length) return false;

        value = input.Substring(valueStart, nextPosition - valueStart).Trim();
        nextPosition++;
        return value.Length > 0;
    }

    private static string NormalizeCommand(string command) => NormalizeTemplate(command).TrimStart('/', '!').Trim();

    private static string NormalizeTemplate(string template) => string.Join(" ", SplitWords(template));

    private static string[] SplitWords(string text) => (text ?? string.Empty)
        .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsValidTriggerWord(string word, int index) => word == ";" || IsAsciiAlphaNumeric(word) ||
        (index == 0 && word.Length > 1 && word[0] == '#' && IsAsciiAlphaNumeric(word.Substring(1)));

    private static bool IsAsciiAlphaNumeric(string value) => value.Length > 0 && value.All(character =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9');

    private static void Reply(string message)
    {
        if (LocalCommandPanel.IsCapturingCommandOutput)
        {
            LocalCommandPanel.CaptureCommandOutput(message);
        }
        else if (HudManager.Instance?.Chat != null && PlayerControl.LocalPlayer != null)
        {
            HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, message);
        }
    }

    private static void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(BindingsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            List<string> lines = new() { "# Host-local command binds. Manage with /bind; do not edit while the game is running." };
            lines.AddRange(Bindings.Select(binding => Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{binding.CommandTemplate}\t{binding.TriggerTemplate}\t{binding.ValuePrefix}"))));
            File.WriteAllLines(BindingsPath, lines, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "CommandBindingsSave");
        }
    }
}