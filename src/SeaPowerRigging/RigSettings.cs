using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Rats.Rigging;

/// <summary>
/// The mod's BepInEx config at BepInEx\config\(Guid).cfg: section "1. General" (Enable mod, Verbose log) here, the mod's own entries
/// bound by the mod. The file is written a moment after the last change instead of on every set: the Options page and the windows
/// set values on every slider step.
/// </summary>
public sealed class RigSettings
{
    public const string DefaultVerboseHelp = "Write debug detail to BepInEx/LogOutput.log.";
    private const float SaveDelay = 1f;

    private readonly RigInfo info;
    private readonly RigLog log;
    private float saveAt = -1f;

    internal RigSettings(RigInfo info, RigLog log)
    {
        this.info = info;
        this.log = log;
    }

    public ConfigFile File { get; private set; }

    /// <summary>A config file was there before this start (a player who had the mod before).</summary>
    public bool Existed { get; private set; }

    public ConfigEntry<bool> Enabled { get; private set; }

    public ConfigEntry<bool> VerboseLog { get; private set; }

    public bool Verbose => VerboseLog != null && VerboseLog.Value;

    /// <summary>Opens the config, binds the General section, then the mod's entries through <paramref name="bind"/>, and writes the file once.</summary>
    internal void Open(Action<ConfigFile> bind, string verboseHelp = DefaultVerboseHelp)
    {
        string path = Path.Combine(Paths.ConfigPath, info.Guid + ".cfg");
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

    /// <summary>From the host every frame: writes the file once the last change is a moment old.</summary>
    internal void FlushIfDue()
    {
        if (saveAt >= 0f && Time.unscaledTime >= saveAt)
            Flush();
    }

    /// <summary>Writes pending changes now (the host, when the game closes).</summary>
    internal void Flush()
    {
        if (saveAt < 0f)
            return;
        saveAt = -1f;
        try { File?.Save(); }
        catch (Exception ex) { log.Warn($"Could not save the settings: {ex.Message}"); }
    }
}

/// <summary>
/// Counts changes of one group of settings, so each part of a mod redraws only for the settings it reads (one counter for all
/// settings made every change, even one written back every second, throw everything away).
/// </summary>
public sealed class ChangeCounter
{
    private readonly HashSet<ConfigEntryBase> entries;
    private readonly bool allBut;

    private ChangeCounter(ConfigFile file, ConfigEntryBase[] entries, bool allBut)
    {
        this.entries = new HashSet<ConfigEntryBase>(entries);
        this.allBut = allBut;
        file.SettingChanged += (s, e) =>
        {
            if (this.entries.Contains(e.ChangedSetting) != this.allBut)
                Version++;
        };
    }

    public int Version { get; private set; }

    /// <summary>Moves when one of these entries of <paramref name="file"/> changes.</summary>
    public static ChangeCounter Of(ConfigFile file, params ConfigEntryBase[] entries) => new ChangeCounter(file, entries, false);

    /// <summary>Moves when any entry of <paramref name="file"/> but these changes.</summary>
    public static ChangeCounter AllBut(ConfigFile file, params ConfigEntryBase[] entries) => new ChangeCounter(file, entries, true);
}
