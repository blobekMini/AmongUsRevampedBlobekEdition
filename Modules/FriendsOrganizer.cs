using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TMPro;
using UnityEngine;

namespace AmongUsRevamped;

internal static class FriendsOrganizer
{
    internal sealed class Contact
    {
        public Contact() { }
        public string Nickname { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
    }

    private sealed class OrganizerData
    {
        public Dictionary<string, Contact> Contacts { get; set; } = new();
        public List<string> Folders { get; set; } = new();
    }

    internal sealed class FriendRow
    {
        public string Key { get; init; }
        public string FriendCode { get; init; }
        public string PlayerName { get; init; }
        public string Puid { get; init; }
    }

    private static readonly string FilePath = Path.Combine(BanManager.DataPath, "AUR-DATA", "FriendsOrganizer.json");
    private static readonly Dictionary<string, Contact> Contacts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> OriginalNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DefaultFolders = new(StringComparer.OrdinalIgnoreCase) { "IRLs", "Randoms", "Onlines" };
    private static readonly HashSet<string> CustomFolders = new(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;
    private static bool typeRegistered;
    private static FriendsOrganizerOverlay overlay;

    public static void EnsureOverlay(FriendsListUI ui)
    {
        if (ui == null || !ui.IsOpen) return;
        Load();
        try
        {
            if (!typeRegistered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<FriendsOrganizerOverlay>();
                typeRegistered = true;
            }
            if (overlay == null)
                overlay = ui.gameObject.GetComponent<FriendsOrganizerOverlay>() ?? ui.gameObject.AddComponent<FriendsOrganizerOverlay>();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "FriendsOrganizerUI");
        }
    }

    public static FriendRow[] GetVisibleFriends(FriendsListUI ui)
    {
        if (ui == null) return Array.Empty<FriendRow>();
        FieldInfo barsField = typeof(FriendsListUI).GetField("friendBars", BindingFlags.Instance | BindingFlags.NonPublic);
        if (barsField?.GetValue(ui) is not IEnumerable bars) return Array.Empty<FriendRow>();

        List<FriendRow> rows = new();
        foreach (object bar in bars)
        {
            if (bar == null) continue;
            string puid = ReadStringField(bar, "puid");
            string friendCode = ReadStringField(bar, "friendCode");
            string name = ReadSenderName(bar);
            string key = GetKey(puid, friendCode);
            if (key.Length == 0) continue;
            if (!OriginalNames.TryGetValue(key, out string originalName))
            {
                originalName = name;
                OriginalNames[key] = originalName;
            }
            Contact contact = GetContact(key);
            rows.Add(new FriendRow { Key = key, FriendCode = friendCode, PlayerName = originalName, Puid = puid });
        }
        return rows.GroupBy(row => row.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
            .OrderBy(row => row.PlayerName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void ApplyToBar(FriendsListBar bar)
    {
        if (bar == null) return;
        Load();
        string puid = ReadStringField(bar, "puid");
        string friendCode = ReadStringField(bar, "friendCode");
        string key = GetKey(puid, friendCode);
        if (key.Length == 0) return;
        string original = ReadSenderName(bar);
        if (!OriginalNames.TryGetValue(key, out string originalName))
        {
            originalName = original;
            OriginalNames[key] = originalName;
        }
        Contact contact = GetContact(key);

        TMP_Text nameLabel = null;
        try { nameLabel = (TMP_Text)typeof(FriendsListBar).GetField("SenderName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(bar); }
        catch { }
        if (nameLabel == null) return;

        string displayName = string.IsNullOrWhiteSpace(contact.Nickname) ? originalName : EscapeRichText(contact.Nickname.Trim());
        if (!string.IsNullOrWhiteSpace(contact.Folder)) displayName += $" <size=70%><color=#9ed7ff>[{EscapeRichText(contact.Folder)}]</color></size>";
        nameLabel.text = displayName;
    }

    public static ContactSnapshot GetContactSnapshot(string key)
    {
        Contact contact = GetContact(key);
        return new ContactSnapshot(contact.Nickname, contact.Folder);
    }

    public static void SaveContact(string key, string nickname, string folder)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        Load();
        nickname = nickname?.Trim() ?? string.Empty;
        folder = folder?.Trim() ?? string.Empty;
        if (nickname.Length > 40) nickname = nickname.Substring(0, 40);
        if (folder.Length > 24) folder = folder.Substring(0, 24);

        if (nickname.Length == 0 && folder.Length == 0)
            Contacts.Remove(key);
        else
            Contacts[key] = new Contact { Nickname = nickname, Folder = folder };
        Persist();

        FriendsListUI ui = FriendsListUI.Instance;
        if (ui != null) ui.RefreshFriends();
    }

    public static string[] GetFolders()
    {
        Load();
        return DefaultFolders.Concat(CustomFolders).Concat(Contacts.Values.Select(contact => contact.Folder).Where(folder => !string.IsNullOrWhiteSpace(folder)))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool CreateFolder(string folder)
    {
        folder = folder?.Trim() ?? string.Empty;
        if (folder.Length == 0) return false;
        if (folder.Length > 24) folder = folder.Substring(0, 24);
        Load();
        bool added = CustomFolders.Add(folder);
        if (added) Persist();
        return added;
    }

    private static Contact GetContact(string key)
    {
        return Contacts.TryGetValue(key, out Contact contact) ? contact : new Contact();
    }

    private static string GetKey(string puid, string friendCode)
    {
        if (!string.IsNullOrWhiteSpace(puid)) return "puid:" + puid.Trim();
        return string.IsNullOrWhiteSpace(friendCode) ? string.Empty : "code:" + friendCode.Trim();
    }

    private static string ReadStringField(object instance, string name)
    {
        try
        {
            return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance) as string ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string ReadSenderName(object bar)
    {
        try
        {
            TMP_Text label = (TMP_Text)typeof(FriendsListBar).GetField("SenderName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(bar);
            return label?.text ?? ReadStringField(bar, "friendCode");
        }
        catch { return ReadStringField(bar, "friendCode"); }
    }

    private static string EscapeRichText(string value) => value.Replace("<", string.Empty).Replace(">", string.Empty);

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            if (!File.Exists(FilePath)) return;
            OrganizerData data = JsonSerializer.Deserialize<OrganizerData>(File.ReadAllText(FilePath));
            if (data == null) return;
            if (data.Contacts == null)
            {
                // Support the initial flat dictionary format if an early organizer build created it.
                Dictionary<string, Contact> legacy = JsonSerializer.Deserialize<Dictionary<string, Contact>>(File.ReadAllText(FilePath));
                data.Contacts = legacy ?? new Dictionary<string, Contact>();
            }
            foreach (KeyValuePair<string, Contact> entry in data.Contacts)
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value != null) Contacts[entry.Key] = entry.Value;
            if (data.Folders != null)
                foreach (string folder in data.Folders.Where(folder => !string.IsNullOrWhiteSpace(folder))) CustomFolders.Add(folder.Trim());
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "FriendsOrganizerLoad");
        }
    }

    private static void Persist()
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new OrganizerData { Contacts = Contacts, Folders = CustomFolders.ToList() },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "FriendsOrganizerSave");
        }
    }

    internal readonly struct ContactSnapshot
    {
        public string Nickname { get; }
        public string Folder { get; }
        public ContactSnapshot(string nickname, string folder) { Nickname = nickname; Folder = folder; }
    }
}

public sealed class FriendsOrganizerOverlay : MonoBehaviour
{
    private bool isOpen;
    private string selectedKey = string.Empty;
    private string nickname = string.Empty;
    private string folder = string.Empty;
    private string newFolder = string.Empty;
    private string folderFilter = "All friends";
    private Vector2 friendScroll;

    public FriendsOrganizerOverlay(IntPtr ptr) : base(ptr) { }

    private void OnGUI()
    {
        FriendsListUI ui = FriendsListUI.Instance;
        if (ui == null || !ui.IsOpen || ui.CurrentTab != FriendsListUI.FriendsListTab.AmongUsFriends)
        {
            isOpen = false;
            return;
        }

        if (GUI.Button(new Rect(Screen.width - 178f, 16f, 158f, 34f), "AUR Friend Folders"))
            isOpen = !isOpen;
        if (!isOpen) return;

        Rect panel = new((Screen.width - 760f) / 2f, (Screen.height - 560f) / 2f, 760f, 560f);
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
        GUILayout.Label("FRIEND FOLDERS & NICKNAMES", GUI.skin.box, GUILayout.Height(34f));

        string[] folders = new[] { "All friends" }.Concat(FriendsOrganizer.GetFolders()).ToArray();
        GUILayout.BeginHorizontal();
        foreach (string folderName in folders)
        {
            if (GUILayout.Toggle(folderFilter.Equals(folderName, StringComparison.OrdinalIgnoreCase), folderName, GUI.skin.button))
                folderFilter = folderName;
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        newFolder = GUILayout.TextField(newFolder, 24);
        if (GUILayout.Button("Create folder", GUILayout.Width(120f)) && !string.IsNullOrWhiteSpace(newFolder))
        {
            string createdFolder = newFolder.Trim();
            FriendsOrganizer.CreateFolder(createdFolder);
            folder = createdFolder;
            folderFilter = createdFolder;
            newFolder = string.Empty;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        friendScroll = GUILayout.BeginScrollView(friendScroll, GUI.skin.box, GUILayout.Width(360f), GUILayout.Height(390f));
        FriendsOrganizer.FriendRow[] friends = FriendsOrganizer.GetVisibleFriends(ui);
        foreach (FriendsOrganizer.FriendRow friend in friends)
        {
            FriendsOrganizer.ContactSnapshot snapshot = FriendsOrganizer.GetContactSnapshot(friend.Key);
            string assignedFolder = snapshot.Folder ?? string.Empty;
            if (!folderFilter.Equals("All friends", StringComparison.OrdinalIgnoreCase) &&
                !assignedFolder.Equals(folderFilter, StringComparison.OrdinalIgnoreCase)) continue;

            string title = string.IsNullOrWhiteSpace(snapshot.Nickname) ? friend.PlayerName : $"{snapshot.Nickname} ({friend.PlayerName})";
            if (GUILayout.Toggle(selectedKey.Equals(friend.Key, StringComparison.OrdinalIgnoreCase), title, GUI.skin.button))
            {
                selectedKey = friend.Key;
                FriendsOrganizer.ContactSnapshot selected = FriendsOrganizer.GetContactSnapshot(friend.Key);
                nickname = selected.Nickname ?? string.Empty;
                folder = selected.Folder ?? string.Empty;
            }
        }
        GUILayout.EndScrollView();

        GUILayout.BeginVertical(GUILayout.Width(340f));
        GUILayout.Label(string.IsNullOrWhiteSpace(selectedKey) ? "Select a friend" : "Selected friend");
        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            GUILayout.Label("Nickname (optional)");
            nickname = GUILayout.TextField(nickname, 40);
            GUILayout.Label("Folder");
            string[] folderChoices = FriendsOrganizer.GetFolders();
            foreach (string choice in folderChoices)
            {
                if (GUILayout.Toggle(folder.Equals(choice, StringComparison.OrdinalIgnoreCase), choice, GUI.skin.button))
                    folder = choice;
            }
            GUILayout.Label("Or create/use a folder name:");
            folder = GUILayout.TextField(folder, 24);
            if (GUILayout.Button("Save friend", GUILayout.Height(34f)))
                FriendsOrganizer.SaveContact(selectedKey, nickname, folder);
            if (GUILayout.Button("Clear nickname and folder", GUILayout.Height(30f)))
            {
                FriendsOrganizer.SaveContact(selectedKey, string.Empty, string.Empty);
                nickname = string.Empty;
                folder = string.Empty;
            }
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Height(34f))) isOpen = false;
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Update))]
internal static class FriendsListOrganizerUpdatePatch
{
    public static void Postfix(FriendsListUI __instance) => FriendsOrganizer.EnsureOverlay(__instance);
}

[HarmonyPatch(typeof(FriendsListBar), nameof(FriendsListBar.SetUp))]
internal static class FriendsListBarOrganizerPatch
{
    public static void Postfix(FriendsListBar __instance) => FriendsOrganizer.ApplyToBar(__instance);
}
