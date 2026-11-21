using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using Steamworks;
using UnityEngine;

namespace Rats.Rigging.Notice;

/// <summary>
/// The R.A.T.S. mods carry a built-in copy of Rigging for a while, so they run without its Workshop item. Once per game start, on
/// the settled main menu, a game message box says so while the item is not there or not switched on:
/// - Get Rigging: subscribes to the item, waits for Steam to download it, switches it on in the game's mod list. No restart: the
///   built-in copy runs this time, the item takes over at the next start.
/// - Not now: nothing changes, the box comes again at the next start.
/// - Don't show this again: the box's middle button, used as a tick. Ticked, the box stays away until the next Rigging update.
/// Every step is caught: a game build whose UI differs only loses the box. The model is the Anchor Chain 1.3 switch offer.
/// </summary>
internal sealed class RiggingNotice : MonoBehaviour
{
    private const string Header = "MESSAGE  ·  " + RigCore.Name;
    private const string TickOff = "[ ] Don't show this again";
    private const string TickOn = "[x] Don't show this again";
    private const float DownloadSeconds = 300f;
    private const string ItemDll = "SeaPowerRigging.dll";

    private const string OfferText =
        "The R.A.T.S. mods now share one part, the Workshop item R.A.T.S. Rigging.\n\n" +
        "• Your mods run on a copy built into them for now: nothing is broken\n" +
        "• Get Rigging: subscribes to it and switches it on. No restart needed, it takes over at the next start\n" +
        "• The built-in copy leaves the mods in a later update, and without Rigging they do not start then\n" +
        "• Not now: nothing changes, and this asks again at the next start\n" +
        "• Don't show this again: click it to tick it, and this stays away until the next Rigging update";

    private const System.Reflection.BindingFlags Any =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private static bool begun;

    // The version the player ticked the box for; the box stays away while it is the running version.
    private static ConfigEntry<string> hiddenFor;

    private object box;
    private object context;
    private bool cancelled;

    private static ulong Item => RigCore.WorkshopItem;

    private static RigLog Log => RigCore.Log;

    /// <summary>Called from Rigging's entry point. Never throws.</summary>
    internal static void Begin()
    {
        if (begun)
            return;
        begun = true;
        try
        {
            if (Item == 0)
            {
                Log.Info("No Workshop item is named for this build: no notice.");
                return;
            }
            hiddenFor = new ConfigFile(Path.Combine(Paths.ConfigPath, RigCore.Guid + ".cfg"), true).Bind("Notice", "Hidden for version", "",
                "The Rigging version the notice was switched off for with \"Don't show this again\". Empty: shown at every start while the item is missing.");
            if (hiddenFor.Value == RigCore.Version)
            {
                Log.Info("The notice is switched off for this version (Notice > Hidden for version in Rigging's config).");
                return;
            }
            var host = new GameObject("SeaPowerRigging.Notice");
            DontDestroyOnLoad(host);
            host.AddComponent<RiggingNotice>();
        }
        catch (Exception e)
        {
            Log.Warn($"No notice: {e.Message}");
        }
    }

    private void Start() => StartCoroutine(NoticeWhenMenuIsUp());

    private IEnumerator NoticeWhenMenuIsUp()
    {
        // The settled main menu: after the load screen and its questions, so the box does not land behind them.
        float stable = 0f;
        while (stable < 2f)
        {
            stable = OnMainMenu() ? stable + Time.unscaledDeltaTime : 0f;
            yield return null;
        }
        try
        {
            if (ItemOn())
            {
                Log.Info("Rigging's Workshop item is subscribed and switched on: no notice.");
                Destroy(gameObject);
                yield break;
            }
            Show(OfferText, "Get Rigging", GetRigging, "Not now", () => Log.Info("Not now: the built-in copies run this time."), withTick: true);
            Log.Info("Notice shown: the Workshop item is not there or not switched on.");
        }
        catch (Exception e)
        {
            Log.Warn($"The notice could not be shown: {e.Message}");
        }
    }

    private static bool OnMainMenu()
    {
        try
        {
            return MenuUp();
        }
        catch (Exception)
        {
            return false;   // a game build without these views: no box
        }
    }

    // The start-up load screen is done and the main menu view exists, with no mission or campaign scene loaded.
    // LoadScreenView.Instance cannot tell it: the game sets it once and never clears it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MenuUp() =>
        Singleton<UnitMetadataProvider>.InstanceExists() && Singleton<UnitMetadataProvider>.Instance.UnitData != null &&
        Singleton<LoadScreen>.InstanceExists() && Singleton<LoadScreen>.Instance.LoadText == "Done" &&
        MainMenuView.Instance != null &&
        !UnityEngine.SceneManagement.SceneManager.GetSceneByName("mission").isLoaded &&
        !UnityEngine.SceneManagement.SceneManager.GetSceneByName("linear-campaign").isLoaded;

    // The item is in the game's mod list and ticked: what the Mods menu shows as switched on.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ItemOn()
    {
        SearchDirectory dir = Singleton<FileManager>.Instance.Directories.FirstOrDefault(d => d.steamId.m_PublishedFileId == Item);
        return dir != null && dir.IsChecked;
    }

    // ---- the game's message box ----

    // The game's message box class is internal: built and filled by name (the same names on every 0.9 build).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Show(string message, string okLabel, Action ok, string cancelLabel = null, Action cancel = null, bool withTick = false)
    {
        CloseBox();
        Type type = typeof(MessageBoxType).Assembly.GetType("SeapowerUI.ViewModels.MessageBoxViewModel", true);
        object b = Activator.CreateInstance(type, true);
        void Set(string name, object value) => type.GetProperty(name, Any).SetValue(b, value);
        Set("MessageBoxType", MessageBoxType.Info);
        Set("Header", Header);
        Set("Message", message);
        Set("AcknowledgeCommandLabel", okLabel);
        Set("AcknowledgeCommand", new DelegateCommand(_ => { CloseBox(); Run(ok); }));
        if (cancelLabel != null)
        {
            Set("CancelCommandLabel", cancelLabel);
            Set("CancelCommand", new DelegateCommand(_ => { CloseBox(); if (cancel != null) Run(cancel); }));
        }
        if (withTick)
        {
            // The game's message box has three buttons and no check box: the middle one is the tick, its label says whether it is set.
            Set("MiddleCommandLabel", TickLabel());
            Set("MiddleCommand", new DelegateCommand(_ => Run(ToggleTick)));
        }
        context = MainMenuView.Instance;
        box = b;
        WindowHostCommands.OpenWindowCommand.Execute(b, (Noesis.UIElement)context);
    }

    private static string TickLabel() => hiddenFor != null && hiddenFor.Value == RigCore.Version ? TickOn : TickOff;

    // Saved at once (BepInEx writes the file when the value is set), so closing the box with its X counts too. The box stays open.
    private void ToggleTick()
    {
        if (hiddenFor == null)
            return;
        hiddenFor.Value = hiddenFor.Value == RigCore.Version ? "" : RigCore.Version;
        Log.Info(hiddenFor.Value == RigCore.Version ? "Ticked: the notice stays away until the next Rigging update." : "Tick taken off: the notice comes again at the next start.");
        try
        {
            box?.GetType().GetProperty("MiddleCommandLabel", Any)?.SetValue(box, TickLabel());
        }
        catch (Exception)
        {
            // the box is gone; the setting is saved all the same
        }
    }

    private void CloseBox()
    {
        if (box == null)
            return;
        try
        {
            CloseCore();
        }
        catch (Exception)
        {
            // already gone with its view
        }
        box = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CloseCore() => WindowHostCommands.CloseWindowCommand.Execute(box, (Noesis.UIElement)context);

    private void SetText(string message)
    {
        try
        {
            box?.GetType().GetProperty("Message", Any)?.SetValue(box, message);
        }
        catch (Exception)
        {
            // the box is gone; the log still says what happens
        }
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error($"Notice step failed: {e}");
        }
    }

    // ---- Get Rigging ----

    private void GetRigging() => StartCoroutine(GetRiggingSteps());

    private IEnumerator GetRiggingSteps()
    {
        Log.Info("Get Rigging: getting the Workshop item.");
        cancelled = false;
        Show("Getting R.A.T.S. Rigging from the Steam Workshop ...", "Cancel", () => { cancelled = true; Log.Info("Get Rigging cancelled while downloading."); });

        string folder = null;
        string failure = null;
        try
        {
            folder = Installed();
            if (folder == null)
                Subscribe();
        }
        catch (Exception e)
        {
            failure = e.Message;
        }

        float until = Time.realtimeSinceStartup + DownloadSeconds;
        while (folder == null && failure == null && !cancelled && Time.realtimeSinceStartup < until)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            try
            {
                folder = Installed();
                if (folder == null)
                    SetText($"Getting R.A.T.S. Rigging from the Steam Workshop ... {Progress()}");
            }
            catch (Exception e)
            {
                failure = e.Message;
            }
        }
        if (cancelled)
            yield break;
        if (folder == null)
        {
            Log.Warn($"Rigging was not downloaded: {failure ?? "Steam took too long"}.");
            Show("Steam did not finish downloading R.A.T.S. Rigging" + (failure != null ? $" ({failure})" : "") + ".\n\n" +
                 "• Subscribe to it on its Workshop page and switch it on in the Mods menu\n" +
                 "• Your mods keep running on their built-in copy meanwhile",
                 "Open the Workshop page", () => Application.OpenURL($"steam://url/CommunityFilePage/{Item}"), "Close");
            yield break;
        }

        Log.Info($"Rigging is on disk: {folder}");
        try
        {
            SwitchOn(folder);
        }
        catch (Exception e)
        {
            Log.Error($"Rigging could not be switched on: {e}");
            Show($"R.A.T.S. Rigging is downloaded but could not be switched on ({e.Message}).\n\n" +
                 "• Switch it on in the Mods menu\n" +
                 "• Your mods keep running on their built-in copy meanwhile",
                 "Open the Workshop page", () => Application.OpenURL($"steam://url/CommunityFilePage/{Item}"), "Close");
            yield break;
        }
        Log.Info("Rigging is switched on in the game's mod list; the built-in copies run this time.");
        Show("R.A.T.S. Rigging is switched on.\n\n• No restart needed: your mods run on their built-in copy this time\n• The item takes over at the next start", "OK", () => { });
    }

    /// <summary>The item's Workshop folder once Steam has it complete, else null.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Installed()
    {
        var item = new PublishedFileId_t(Item);
        var state = (EItemState)SteamUGC.GetItemState(item);
        if (!state.HasFlag(EItemState.k_EItemStateSubscribed) || !state.HasFlag(EItemState.k_EItemStateInstalled) ||
            state.HasFlag(EItemState.k_EItemStateDownloading) || state.HasFlag(EItemState.k_EItemStateDownloadPending) ||
            state.HasFlag(EItemState.k_EItemStateNeedsUpdate))
            return null;
        if (!SteamUGC.GetItemInstallInfo(item, out _, out string path, 1024u, out _) || !Directory.Exists(path))
            return null;
        return File.Exists(Path.Combine(path, ItemDll)) ? path : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Subscribe()
    {
        if (!SteamManager.Initialized)
            throw new InvalidOperationException("Steam is not running");
        var item = new PublishedFileId_t(Item);
        SteamUGC.SubscribeItem(item);
        SteamUGC.DownloadItem(item, true);
        Log.Info("Subscribed to R.A.T.S. Rigging; Steam downloads it.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Progress() =>
        SteamUGC.GetItemDownloadInfo(new PublishedFileId_t(Item), out ulong done, out ulong total) && total > 0 ? $"{done * 100 / total} %" : "";

    /// <summary>The item switched on in the game's mod list and the list saved, the way the Mods menu does it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SwitchOn(string itemFolder)
    {
        FileManager files = Singleton<FileManager>.Instance;
        TrulyObservableCollection<SearchDirectory> list = files.Directories;
        SearchDirectory entry = list.FirstOrDefault(d => d.steamId.m_PublishedFileId == Item);
        if (entry == null)
        {
            entry = new SearchDirectory(new DirectoryInfo(itemFolder), isActive: true, isEditable: true, new PublishedFileId_t(Item));
            // First in the list: Rigging holds no game data, and the mods that need it then sit below it (the loader warns about the other order).
            list.Insert(0, entry);
        }
        entry.IsChecked = true;
        files.SaveDirectories(list);
        Log.Info("Rigging is switched on in the game's mod list.");
    }
}
