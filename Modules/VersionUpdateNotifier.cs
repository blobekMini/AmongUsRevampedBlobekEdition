using Il2CppInterop.Runtime.Injection;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace AmongUsRevamped;

internal static class VersionUpdateNotifier
{
    private const string LatestReleaseApi = "https://api.github.com/repos/blobekMini/AmongUsRevampedBlobekEdition/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static Task<ReleaseInfo> pendingCheck;
    private static bool checkHandled;
    private static bool popupTypeRegistered;
    private static VersionUpdatePopup popup;

    private sealed class ReleaseInfo
    {
        public string TagName { get; init; }
        public string Url { get; init; }
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AmongUsRevampedBlobekEdition");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static void Initialize()
    {
        if (IsSnoozed())
        {
            checkHandled = true;
            return;
        }

        pendingCheck = FetchLatestReleaseAsync();
    }

    public static void Pump()
    {
        if (checkHandled || pendingCheck == null || !pendingCheck.IsCompleted) return;
        checkHandled = true;

        if (pendingCheck.IsFaulted)
        {
            Logger.Warn($"Could not check the latest release: {pendingCheck.Exception?.GetBaseException().Message}", "UpdateCheck");
            return;
        }
        if (pendingCheck.IsCanceled) return;

        ReleaseInfo release = pendingCheck.Result;
        if (!TryCompareVersions(release.TagName, Main.ModVersion, out int comparison))
        {
            Logger.Warn($"Could not compare release tag '{release.TagName}' with local version '{Main.ModVersion}'.", "UpdateCheck");
            return;
        }
        if (comparison <= 0) return;

        Logger.Info($"Update available: {Main.ModVersion} -> {release.TagName}.", "UpdateCheck");
        ShowPopup(Main.ModVersion, release.TagName, release.Url);
    }

    private static async Task<ReleaseInfo> FetchLatestReleaseAsync()
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(LatestReleaseApi).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        JsonElement root = document.RootElement;
        string tagName = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() : null;
        string url = root.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(tagName)) throw new InvalidOperationException("GitHub returned no release tag.");
        return new ReleaseInfo
        {
            TagName = tagName.Trim(),
            Url = string.IsNullOrWhiteSpace(url) ? "https://github.com/blobekMini/AmongUsRevampedBlobekEdition/releases/latest" : url.Trim()
        };
    }

    private static bool IsSnoozed()
    {
        string snoozeValue = Main.UpdateCheckSnoozeUntil?.Value;
        return DateTime.TryParse(snoozeValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime snoozeUntil) &&
            snoozeUntil.ToUniversalTime() > DateTime.UtcNow;
    }

    private static bool TryCompareVersions(string left, string right, out int comparison)
    {
        comparison = 0;
        if (!TryParseVersion(left, out int[] leftParts) || !TryParseVersion(right, out int[] rightParts)) return false;

        for (int index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            int leftPart = index < leftParts.Length ? leftParts[index] : 0;
            int rightPart = index < rightParts.Length ? rightParts[index] : 0;
            if (leftPart == rightPart) continue;
            comparison = leftPart.CompareTo(rightPart);
            return true;
        }
        return true;
    }

    private static bool TryParseVersion(string value, out int[] parts)
    {
        parts = Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(value)) return false;
        Match match = Regex.Match(value, @"\d+(?:\.\d+){2,4}");
        if (!match.Success) return false;

        string[] versionParts = match.Value.Split('.');
        int[] parsedParts = new int[versionParts.Length];
        for (int index = 0; index < versionParts.Length; index++)
        {
            if (!int.TryParse(versionParts[index], NumberStyles.None, CultureInfo.InvariantCulture, out parsedParts[index]))
                return false;
        }
        parts = parsedParts;
        return true;
    }

    private static void ShowPopup(string currentVersion, string latestVersion, string releaseUrl)
    {
        try
        {
            if (!popupTypeRegistered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<VersionUpdatePopup>();
                popupTypeRegistered = true;
            }
            if (popup == null)
            {
                GameObject popupObject = new("AURVersionUpdatePopup");
                UnityEngine.Object.DontDestroyOnLoad(popupObject);
                popup = popupObject.AddComponent<VersionUpdatePopup>();
            }
            popup.Open(currentVersion, latestVersion, releaseUrl);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "UpdateCheckPopup");
        }
    }

    internal static void Dismiss(bool snooze)
    {
        if (snooze && Main.UpdateCheckSnoozeUntil != null)
        {
            Main.UpdateCheckSnoozeUntil.Value = DateTime.UtcNow.AddDays(30).ToString("O", CultureInfo.InvariantCulture);
            Main.Instance?.Config.Save();
        }
        popup?.Close();
    }
}

public sealed class VersionUpdatePopup : MonoBehaviour
{
    private bool visible;
    private string currentVersion = string.Empty;
    private string latestVersion = string.Empty;
    private string releaseUrl = string.Empty;

    public VersionUpdatePopup(IntPtr ptr) : base(ptr) { }

    public void Open(string installedVersion, string availableVersion, string url)
    {
        currentVersion = installedVersion;
        latestVersion = availableVersion;
        releaseUrl = url;
        visible = true;
    }

    public void Close() => visible = false;

    private void OnGUI()
    {
        if (!visible) return;

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            visible = false;
            currentEvent.Use();
            return;
        }

        float width = Math.Min(560f, Math.Max(320f, Screen.width - 32f));
        float height = 270f;
        Rect popupRect = new((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(popupRect, GUIContent.none);
        GUILayout.BeginArea(new Rect(popupRect.x + 18f, popupRect.y + 14f, popupRect.width - 36f, popupRect.height - 28f));
        GUILayout.Label("Among Us Revamped update available", GUI.skin.box, GUILayout.Height(36f));
        GUILayout.Space(10f);
        GUILayout.Label($"Installed version: {currentVersion}");
        GUILayout.Label($"Latest version: {latestVersion}");
        GUIStyle wrappedUrl = new(GUI.skin.label) { wordWrap = true };
        GUILayout.Label(releaseUrl, wrappedUrl, GUILayout.Height(34f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Height(36f)))
            VersionUpdateNotifier.Dismiss(false);
        GUILayout.Space(5f);
        if (GUILayout.Button("Don't ask me again for 30 days", GUILayout.Height(36f)))
            VersionUpdateNotifier.Dismiss(true);
        GUILayout.EndArea();
    }
}
