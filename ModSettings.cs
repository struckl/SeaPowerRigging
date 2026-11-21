using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Rats.Shared;

/// <summary>
/// The mod's BepInEx config at BepInEx\config\(ModInfo.Guid).cfg: section "1. General" (Enable mod, Verbose log) here, the mod's
/// own entries bound by the mod. The file is written a moment after the last change instead of on every set: the Options page
/// and the windows set values on every slider step.
/// </summary>
internal static class ModSettings
{
    public const string DefaultVerboseHelp = "Write debug detail to BepInEx/LogOutput.log.";
    private const float SaveDelay = 1f;

    public static ConfigFile File { get; private set; }

    /// <summary>A config file was there before this start (a player who had the mod before).</summary>
    public static bool Existed { get; private set; }

    public static ConfigEntry<bool> Enabled { get; private set; }
    public static ConfigEntry<bool> VerboseLog { get; private set; }

    public static bool Verbose => VerboseLog != null && VerboseLog.Value;

    private static float saveAt = -1f;

    /// <summary>Opens the config, binds the General section, then the mod's entries through <paramref name="bind"/>, and writes the file once.</summary>
    public static void Open(Action<ConfigFile> bind, string verboseHelp = DefaultVerboseHelp)
    {
        string path = Path.Combine(Paths.ConfigPath, ModInfo.Guid + ".cfg");
        Existed = System.IO.File.Exists(path);
        File = new ConfigFile(path, saveOnInit: true) { SaveOnConfigSet = false };
        Enabled = File.Bind("1. General", "Enable mod", true, "Master switch. Needs a restart.");
        VerboseLog = File.Bind("1. General", "Verbose log", false, verboseHelp);
        bind?.Invoke(File);
        File.Save();
        File.SettingChanged += (s, e) => saveAt = Time.unscaledTime + SaveDelay;
    }

    /// <summary>
    /// One-time changes of a saved config, in order. <paramref name="done"/> is the mod's own counter entry (the number of the
    /// last step that ran); a step runs once, so a setting the player changes back afterwards is kept.
    /// </summary>
    public static void Migrate(ConfigEntry<int> done, params (int step, Action run)[] steps)
    {
        foreach ((int step, Action run) in steps)
        {
            if (done.Value >= step)
                continue;
            run();
            done.Value = step;
        }
    }

    /// <summary>From ModHost every frame: writes the file once the last change is a moment old.</summary>
    public static void FlushIfDue()
    {
        if (saveAt >= 0f && Time.unscaledTime >= saveAt)
            Flush();
    }

    /// <summary>Writes pending changes now (ModHost, when the game closes).</summary>
    public static void Flush()
    {
        if (saveAt < 0f)
            return;
        saveAt = -1f;
        try { File?.Save(); }
        catch (Exception ex) { ModLog.Warn($"Could not save the settings: {ex.Message}"); }
    }
}

/// <summary>
/// Counts changes of one group of settings, so each part of a mod redraws only for the settings it reads (one counter for all
/// settings made every change, even one written back every second, throw everything away).
/// </summary>
internal sealed class ChangeCounter
{
    private readonly HashSet<ConfigEntryBase> entries;
    private readonly bool allBut;

    public int Version { get; private set; }

    private ChangeCounter(ConfigEntryBase[] entries, bool allBut)
    {
        this.entries = new HashSet<ConfigEntryBase>(entries);
        this.allBut = allBut;
        ModSettings.File.SettingChanged += (s, e) =>
        {
            if (this.entries.Contains(e.ChangedSetting) != this.allBut)
                Version++;
        };
    }

    /// <summary>Moves when one of these entries changes.</summary>
    public static ChangeCounter Of(params ConfigEntryBase[] entries) => new ChangeCounter(entries, false);

    /// <summary>Moves when any entry but these changes.</summary>
    public static ChangeCounter AllBut(params ConfigEntryBase[] entries) => new ChangeCounter(entries, true);
}
