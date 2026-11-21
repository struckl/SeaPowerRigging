using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace Rats.Rigging;

/// <summary>
/// One mod's Rigging object, made once in the mod's start from its <see cref="RigInfo"/>. It owns what a mod used to compile into
/// its own DLL as statics: the log, the settings, the hooks, the mission events, the Options tab and the host. Nothing of it is
/// shared between mods.
/// <code>
/// Rig.Mod = RigMod.Create(info);
/// Rig.Mod.Start&lt;Host&gt;(Settings.Bind, Hooks.Declare);
/// </code>
/// </summary>
public sealed class RigMod
{
    private static readonly List<RigMod> All = new List<RigMod>();

    private RigMod(RigInfo info)
    {
        Info = info;
        Log = new RigLog(info.LogSource, () => Settings != null && Settings.Verbose);
        Settings = new RigSettings(info, Log);
        Hooks = new RigHooks(info, Log);
        Mission = new RigMission(this);
        Options = new RigOptions(this);
    }

    /// <summary>Every mod's object, in the order the mods started.</summary>
    internal static IReadOnlyList<RigMod> Mods => All;

    public RigInfo Info { get; }

    public RigLog Log { get; }

    public RigSettings Settings { get; }

    public RigHooks Hooks { get; }

    public RigMission Mission { get; }

    public RigOptions Options { get; }

    /// <summary>The mod's host object, once <see cref="Start"/> made it.</summary>
    public RigHost Host { get; internal set; }

    /// <summary>True from the moment the game starts to close: nothing touches Noesis after that.</summary>
    public bool Quitting { get; internal set; }

    public static RigMod Create(RigInfo info)
    {
        var mod = new RigMod(info);
        All.Add(mod);
        return mod;
    }

    /// <summary>
    /// Starts the mod once: settings (<paramref name="bindSettings"/> binds the mod's entries), the hook declarations
    /// (<paramref name="declareHooks"/>), the load-stage hooks, the mod's own start steps (<paramref name="steps"/>), the host.
    /// It stays off when it runs from a second folder, is unticked in the Mod Manager, is switched off in its config, or a hook
    /// it needs is missing.
    /// </summary>
    public void Start<THost>(Action<ConfigFile> bindSettings, Action declareHooks, Action steps = null) where THost : RigHost
    {
        // Loaders that load assemblies with Assembly.LoadFile would make the same DLL found in two folders two separate
        // assemblies with separate statics. The marker lives on the AppDomain so only the first one starts.
        string marker = Info.Guid + ".loaded";
        if (AppDomain.CurrentDomain.GetData(marker) != null)
        {
            Log.Warn("Already loaded from another folder; skipping this copy.");
            return;
        }
        AppDomain.CurrentDomain.SetData(marker, true);
        try
        {
            if (Starts(bindSettings, declareHooks))
            {
                steps?.Invoke();
                RigHost.Create<THost>(this);
                Log.Info(Info.Name + " " + Info.Version + " loaded.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed, mod inactive: " + ex);
        }
    }

    private bool Starts(Action<ConfigFile> bindSettings, Action declareHooks)
    {
        if (!ModState.TickedInModManager(Info, Log))
        {
            Log.Info("Unticked in the Mod Manager - inactive.");
            return false;
        }
        Settings.Open(bindSettings);
        if (!Settings.Enabled.Value)
        {
            Log.Info("Disabled in config.");
            return false;
        }
        declareHooks();
        if (Hooks.ApplyStage(HookStage.Load))
            return true;
        Hooks.RemoveAll();
        return false;
    }
}
