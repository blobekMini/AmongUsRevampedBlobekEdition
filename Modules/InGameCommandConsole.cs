using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace AmongUsRevamped;

public sealed class InGameCommandConsole : MonoBehaviour
{
    private static InGameCommandConsole instance;
    private static bool typeRegistered;
    private static bool isOpen;
    private static bool notificationVisible;
    private static string notificationTitle = string.Empty;
    private static string notificationMessage = string.Empty;
    private static readonly Queue<string> deferredOutput = new();
    private bool focusCommandInput;

    private Rect windowRect;
    private Vector2 commandScroll;
    private Vector2 outputScroll;
    private Vector2 formScroll;
    private Vector2 playerPickerScroll;
    private Vector2 rainbowPickerScroll;
    private Vector2 notificationScroll;
    private string commandText = string.Empty;
    private string outputText = "Ready. Commands run locally as the lobby host.";
    private string selectedCategory = "All";
    private string selectedCommand = string.Empty;
    private string[] categories = Array.Empty<string>();
    private string[] commands = Array.Empty<string>();
    private string[] argumentTypes = Array.Empty<string>();
    private string[] argumentValues = Array.Empty<string>();
    private HashSet<string>[] targetSelections = Array.Empty<HashSet<string>>();
    private readonly HashSet<int> rainbowColors = new() { 0, 4, 5, 11, 2, 10, 1, 8, 3, 13 };
    private string rainbowInterval = "100";
    private string completionPrefix = string.Empty;
    private string[] completionMatches = Array.Empty<string>();
    private int completionIndex;

    public InGameCommandConsole(IntPtr ptr) : base(ptr) { }

    public static bool IsOpen => isOpen;

    public static void ShowNotificationPopup(string title, string message)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || string.IsNullOrWhiteSpace(message)) return;
        if (!EnsureInstance()) return;

        string safeTitle = string.IsNullOrWhiteSpace(title) ? "Player notification" : title.Trim();
        string safeMessage = message.Trim();
        if (notificationVisible)
        {
            notificationTitle = "Player notifications";
            notificationMessage += $"\n\n{safeTitle}:\n{safeMessage}";
        }
        else
        {
            notificationTitle = safeTitle;
            notificationMessage = safeMessage;
            instance.notificationScroll = Vector2.zero;
        }
        notificationVisible = true;
    }

    public static void Toggle()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        if (!EnsureInstance()) return;

        isOpen = !isOpen;
        if (isOpen)
        {
            instance.windowRect = new Rect(
                Math.Max(12f, (Screen.width - 900f) / 2f),
                Math.Max(12f, (Screen.height - 660f) / 2f),
                Math.Min(900f, Screen.width - 24f),
                Math.Min(660f, Screen.height - 24f));
            instance.focusCommandInput = true;
            instance.RefreshCatalog();
            while (deferredOutput.Count > 0) instance.AppendOutput(deferredOutput.Dequeue());
        }
    }

    private static bool EnsureInstance()
    {
        if (instance != null) return true;

        try
        {
            if (!typeRegistered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<InGameCommandConsole>();
                typeRegistered = true;
            }

            GameObject consoleObject = new("AURInGameCommandConsole");
            UnityEngine.Object.DontDestroyOnLoad(consoleObject);
            instance = consoleObject.AddComponent<InGameCommandConsole>();
            instance.InitializeCatalog();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "InGameCommandConsoleInit");
            return false;
        }
    }

    public static void RecordCommandOutput(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (instance == null)
        {
            deferredOutput.Enqueue(message);
            return;
        }
        instance.AppendOutput(message);
    }

    private void InitializeCatalog()
    {
        RefreshCatalog();
    }

    private void RefreshCatalog()
    {
        commands = LocalCommandPanel.GetCommandNames();
        categories = new[] { "All" }.Concat(commands
            .Select(LocalCommandPanel.GetCommandCategory)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(category => category, StringComparer.Ordinal)).ToArray();
    }

    private void OnGUI()
    {
        if ((!isOpen && !notificationVisible) || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            if (notificationVisible) notificationVisible = false;
            else isOpen = false;
            currentEvent.Use();
            return;
        }

        if (isOpen)
        {
            GUI.Box(windowRect, "AUR  /  IN-GAME COMMAND CONSOLE");
            GUILayout.BeginArea(new Rect(windowRect.x + 12f, windowRect.y + 30f, windowRect.width - 24f, windowRect.height - 42f));
            DrawWindowContents();
            GUILayout.EndArea();
            windowRect.x = Mathf.Clamp(windowRect.x, 8f, Math.Max(8f, Screen.width - windowRect.width - 8f));
            windowRect.y = Mathf.Clamp(windowRect.y, 8f, Math.Max(8f, Screen.height - windowRect.height - 8f));
        }

        if (notificationVisible) DrawNotificationPopup();
    }

    private void DrawNotificationPopup()
    {
        float width = Math.Min(620f, Math.Max(300f, Screen.width - 40f));
        float height = Math.Min(420f, Math.Max(220f, Screen.height - 40f));
        Rect popupRect = new((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(popupRect, GUIContent.none);
        GUILayout.BeginArea(new Rect(popupRect.x + 18f, popupRect.y + 14f, popupRect.width - 36f, popupRect.height - 28f));
        GUILayout.Label(notificationTitle, GUI.skin.box, GUILayout.Height(34f));

        GUIStyle wrappedText = new(GUI.skin.label) { wordWrap = true, richText = false };
        notificationScroll = GUILayout.BeginScrollView(notificationScroll, GUI.skin.box,
            GUILayout.Height(Math.Max(90f, popupRect.height - 112f)));
        GUILayout.Label(notificationMessage, wrappedText, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        GUILayout.EndScrollView();

        if (GUILayout.Button("Close", GUILayout.Height(38f)))
        {
            notificationVisible = false;
            notificationMessage = string.Empty;
        }
        GUILayout.EndArea();
    }

    private void DrawWindowContents()
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Select a command, then press RUN COMMAND or RUN USING THESE FIELDS to execute it. Tab completes commands, players, roles, and colors.");

        string[] categoryLabels = categories.Length == 0 ? new[] { "All" } : categories;
        int categoryIndex = Array.IndexOf(categoryLabels, selectedCategory);
        if (categoryIndex < 0) categoryIndex = 0;
        int nextCategory = GUILayout.Toolbar(categoryIndex, categoryLabels, GUILayout.Height(30f));
        if (nextCategory != categoryIndex)
        {
            selectedCategory = categoryLabels[nextCategory];
            commandScroll = Vector2.zero;
        }

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(225f));
        GUILayout.Label("COMMANDS", GUI.skin.box, GUILayout.Height(24f));
        commandScroll = GUILayout.BeginScrollView(commandScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
        string[] visibleCommands = commands.Where(command => selectedCategory == "All" ||
            LocalCommandPanel.GetCommandCategory(command) == selectedCategory).ToArray();
        foreach (string command in visibleCommands)
        {
            bool selected = command == selectedCommand;
            GUI.backgroundColor = selected ? new Color(0.16f, 0.48f, 0.78f) : new Color(0.15f, 0.19f, 0.25f);
            if (GUILayout.Button(command, GUILayout.Height(27f)))
            {
                SelectCommand(command);
            }
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (!string.IsNullOrEmpty(selectedCommand))
        {
            GUILayout.Label(selectedCommand, GUI.skin.box, GUILayout.Height(25f));
            GUILayout.Label(LocalCommandPanel.GetCommandDescription(selectedCommand), GUILayout.MinHeight(38f));
            GUILayout.Label("Example: " + (LocalCommandPanel.GetCommandExample(selectedCommand) ?? selectedCommand), GUI.skin.box);
            DrawCommandForm();
        }
        else
        {
            GUILayout.Label("Select a command", GUI.skin.box, GUILayout.Height(25f));
            GUILayout.Label("Commands are processed through the same host-side chat handlers and permission checks as normal chat.", GUILayout.MinHeight(55f));
        }

        GUILayout.Space(7f);
        GUILayout.Label("COMMAND INPUT");
        GUI.SetNextControlName("AURInGameCommandInput");
        commandText = GUILayout.TextField(commandText, 120, GUILayout.Height(32f));
        if (focusCommandInput)
        {
            GUI.FocusControl("AURInGameCommandInput");
            focusCommandInput = false;
        }
        HandleInputCompletion();

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.1f, 0.55f, 0.84f);
        if (GUILayout.Button("RUN COMMAND", GUILayout.Height(34f))) RunCommand();
        GUI.backgroundColor = new Color(0.22f, 0.26f, 0.32f);
        if (GUILayout.Button("CLEAR", GUILayout.Width(85f), GUILayout.Height(34f))) commandText = string.Empty;
        if (GUILayout.Button("CLOSE  [F10 / Esc]", GUILayout.Width(145f), GUILayout.Height(34f))) isOpen = false;
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("LOCAL OUTPUT  ·  NOT SENT TO GAME CHAT", GUI.skin.box, GUILayout.Height(25f));
        outputScroll = GUILayout.BeginScrollView(outputScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
        GUILayout.Label(outputText, GUI.skin.textArea, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void SelectCommand(string command)
    {
        selectedCommand = command;
        argumentTypes = LocalCommandPanel.GetCommandArgumentTypes(command);
        argumentValues = new string[argumentTypes.Length];
        targetSelections = new HashSet<string>[argumentTypes.Length];
        for (int index = 0; index < argumentTypes.Length; index++)
        {
            argumentValues[index] = argumentTypes[index] == "color" ? "red" :
                argumentTypes[index] == "action" ? "status" :
                argumentTypes[index] == "voteTarget" ? "@all" :
                argumentTypes[index] == "count" ? "2" :
                argumentTypes[index] == "room" ? "!room:security" : string.Empty;
            targetSelections[index] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        rainbowColors.Clear();
        foreach (int colorId in new[] { 0, 4, 5, 11, 2, 10, 1, 8, 3, 13 }) rainbowColors.Add(colorId);
        rainbowInterval = "100";
        commandText = LocalCommandPanel.GetCommandExample(command) ?? command;
    }

    private void DrawCommandForm()
    {
        if (argumentTypes.Length == 0) return;

        formScroll = GUILayout.BeginScrollView(formScroll, GUI.skin.box, GUILayout.Height(190f));
        GUILayout.Label("COMMAND FIELDS");
        for (int index = 0; index < argumentTypes.Length; index++)
        {
            string type = argumentTypes[index];
            GUILayout.Label(GetArgumentLabel(type, index));

            if (type is "targetPlayer" or "sourcePlayer" or "player")
            {
                string[] choices = GetPlayerChoices(false, selectedCommand == "/revive");
                int current = Array.IndexOf(choices, argumentValues[index]);
                int next = GUILayout.SelectionGrid(current, choices, 3,
                    GUILayout.Height(Math.Min(86f, 27f * Math.Max(1, (choices.Length + 2) / 3))));
                if (next >= 0 && next < choices.Length)
                    argumentValues[index] = choices[next] == "Select player..." ? string.Empty : choices[next];
            }
            else if (type == "room")
            {
                string[] choices = { "!room:security", "!room:cafeteria", "!room:o2", "!room:navigation" };
                int current = Array.IndexOf(choices, argumentValues[index]);
                int next = GUILayout.SelectionGrid(current, choices, 2, GUILayout.Height(50f));
                if (next >= 0 && next < choices.Length) argumentValues[index] = choices[next];
                GUILayout.Label("Only rooms present on the current map can be selected.");
            }
            else if (type == "voteTarget")
            {
                string[] choices = GetPlayerChoices(true);
                int current = Array.IndexOf(choices, argumentValues[index]);
                int next = GUILayout.SelectionGrid(current, choices, 3,
                    GUILayout.Height(Math.Min(86f, 27f * Math.Max(1, (choices.Length + 2) / 3))));
                if (next >= 0 && next < choices.Length) argumentValues[index] = choices[next];
            }
            else if (type is "targetPlayers" or "players")
            {
                string[] choices = GetPlayerChoices(true);
                playerPickerScroll = GUILayout.BeginScrollView(playerPickerScroll, GUI.skin.box, GUILayout.Height(85f));
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int column = 0; column < 3 && choiceIndex + column < choices.Length; column++)
                    {
                        string choice = choices[choiceIndex + column];
                        bool active = GUILayout.Toggle(targetSelections[index].Contains(choice), choice, GUILayout.MinWidth(110f));
                        if (active) targetSelections[index].Add(choice);
                        else targetSelections[index].Remove(choice);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.Label("!color:<name> selects all players currently wearing that color.");
            }
            else if (type == "color")
            {
                string[] colors = GetColorChoices(selectedCommand);
                int current = Array.IndexOf(colors, argumentValues[index]);
                int next = GUILayout.SelectionGrid(current, colors, 5, GUILayout.Height(50f));
                if (next >= 0 && next < colors.Length) argumentValues[index] = colors[next];

                if (argumentValues[index] == "rainbow" && SupportsRainbowOptions(selectedCommand))
                {
                    GUILayout.Label("Rainbow palette (clear selections to use the built-in default sequence)");
                    string[] palette = Enum.GetNames(typeof(Main.ColorToString)).Take(19).ToArray();
                    rainbowPickerScroll = GUILayout.BeginScrollView(rainbowPickerScroll, GUI.skin.box, GUILayout.Height(70f));
                    for (int colorIndex = 0; colorIndex < palette.Length; colorIndex += 4)
                    {
                        GUILayout.BeginHorizontal();
                        for (int column = 0; column < 4 && colorIndex + column < palette.Length; column++)
                        {
                            int id = colorIndex + column;
                            bool active = GUILayout.Toggle(rainbowColors.Contains(id), palette[id], GUILayout.MinWidth(95f));
                            if (active) rainbowColors.Add(id);
                            else rainbowColors.Remove(id);
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Switch interval (ms, 50–5000; blank = 100)", GUILayout.Width(255f));
                    rainbowInterval = GUILayout.TextField(rainbowInterval, 5, GUILayout.Width(75f));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                argumentValues[index] = GUILayout.TextField(argumentValues[index] ?? string.Empty, 80, GUILayout.Height(27f));
            }
        }

        if (GUILayout.Button("RUN USING THESE FIELDS", GUILayout.Height(29f)))
        {
            try
            {
                commandText = BuildFormCommand();
                RunCommand();
            }
            catch (Exception ex)
            {
                AppendOutput(ex.Message);
            }
        }
        GUILayout.EndScrollView();
    }

    private string BuildFormCommand()
    {
        List<string> arguments = new();
        for (int index = 0; index < argumentTypes.Length; index++)
        {
            string type = argumentTypes[index];
            if (type is "targetPlayers" or "players")
            {
                string targets = string.Join(",", targetSelections[index].Select(QuotePlayerName));
                if (string.IsNullOrWhiteSpace(targets)) throw new InvalidOperationException("Select at least one target player.");
                arguments.Add(targets);
                continue;
            }

            string value = (argumentValues[index] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Fill in {GetArgumentLabel(type, index).ToLowerInvariant()}.");
            if (type is "targetPlayer" or "sourcePlayer" or "player") value = QuotePlayerName(value);
            if (type == "color" && value == "rainbow" && SupportsRainbowOptions(selectedCommand))
            {
                if (!int.TryParse(rainbowInterval, out int interval)) interval = 100;
                if (interval < 50 || interval > 5000) throw new InvalidOperationException("Rainbow interval must be from 50 to 5000 ms.");
                int[] defaultSequence = { 0, 4, 5, 11, 2, 10, 1, 8, 3, 13 };
                IEnumerable<int> orderedColors = defaultSequence.Where(rainbowColors.Contains)
                    .Concat(rainbowColors.Except(defaultSequence).OrderBy(colorId => colorId));
                value = $"rainbow:{string.Join(",", orderedColors)}:{interval}";
            }
            arguments.Add(value);
        }

        string command = selectedCommand + (arguments.Count == 0 ? string.Empty : " " + string.Join(" ", arguments));
        if (command.Length > 120) throw new InvalidOperationException("Command exceeds the 120-character limit.");
        return command;
    }

    private static string QuotePlayerName(string playerName)
    {
        return playerName.Contains(' ') && !playerName.StartsWith("!color:", StringComparison.OrdinalIgnoreCase)
            ? $"\"{playerName.Replace("\"", string.Empty)}\""
            : playerName;
    }

    private static string GetArgumentLabel(string type, int index)
    {
        return type switch
        {
            "targetPlayer" => "Target player",
            "voteTarget" => "Voter(s): player, !color:<color>, @all, or @me",
            "sourcePlayer" => "Source player",
            "player" => "Player",
            "targetPlayers" or "players" => "Target players",
            "color" => "Color",
            "role" => "Role name",
            "count" => "Count",
            "template" => "Template name",
            "action" => "Action",
            "voteOption" => "Vote option",
            "room" => "Destination room",
            _ => $"Argument {index + 1}"
        };
    }

    private static string[] GetPlayerChoices(bool allowGroups, bool allowAll = false)
    {
        List<string> choices = new();
        if (!allowGroups) choices.Add("Select player...");
        if (allowAll) choices.Add("@all");
        choices.Add("@me");
        if (allowGroups)
        {
            choices.Insert(0, "@all");
            choices.AddRange(Enum.GetNames(typeof(Main.ColorToString)).Select(color => "!color:" + color));
            choices.Add("!color:gay");
            choices.Add("!color:rainbow");
        }
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data != null && !string.IsNullOrWhiteSpace(player.Data.PlayerName))
                choices.Add(player.Data.PlayerName);
        }
        return choices.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] GetColorChoices(string command)
    {
        string[] colors = Enum.GetNames(typeof(Main.ColorToString));
        return SupportsRainbowOptions(command) ? colors.Concat(new[] { "rainbow", "gay", "stop" }).ToArray() : colors;
    }

    private static bool SupportsRainbowOptions(string command)
    {
        return command is "/col" or "/color" or "/colour" or "/colout" or "/ucol" or "/ucolor" or "/ucolour" or "/ucolout";
    }

    private void HandleInputCompletion()
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.KeyDown || currentEvent.keyCode != KeyCode.Tab ||
            GUI.GetNameOfFocusedControl() != "AURInGameCommandInput") return;

        currentEvent.Use();
        string trimmed = commandText.TrimStart();
        int lastSpace = trimmed.LastIndexOf(' ');
        string prefix = lastSpace < 0 ? string.Empty : trimmed.Substring(0, lastSpace + 1);
        string token = lastSpace < 0 ? trimmed : trimmed.Substring(lastSpace + 1);
        if (!string.Equals(prefix, completionPrefix, StringComparison.Ordinal) || completionMatches.Length == 0)
        {
            completionPrefix = prefix;
            completionMatches = InGameCommandCompletion.GetCandidates(lastSpace < 0, token, commands);
            completionIndex = 0;
        }
        else
        {
            completionIndex = (completionIndex + 1) % completionMatches.Length;
        }

        if (completionMatches.Length == 0) return;
        commandText = prefix + completionMatches[completionIndex];
    }

    private void RunCommand()
    {
        string command = commandText?.Trim();
        if (string.IsNullOrWhiteSpace(command)) return;

        string result = LocalCommandPanel.ExecuteInGameCommand(command);
        AppendOutput("> " + command + Environment.NewLine + result);
    }

    private void AppendOutput(string message)
    {
        outputText = string.IsNullOrWhiteSpace(outputText) ? message : outputText + Environment.NewLine + message;
        string[] lines = outputText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        if (lines.Length > 60) outputText = string.Join(Environment.NewLine, lines.Skip(lines.Length - 60));
        outputScroll.y = float.MaxValue;
    }
}
