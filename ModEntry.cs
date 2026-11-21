using System;
using BepInEx.Configuration;

namespace Rats.Shared;

/// <summary>
/// The start of every mod, called from its Plugin (the [ACPlugin] class, which Anchor Chain and Keel both start through
/// TriggerEntryPoint). Plugin keeps the entry point itself: the build check names it as the method that must run on every game.
/// <code>
/// public void TriggerEntryPoint() => ModEntry.Trigger(Start);
/// [MethodImpl(MethodImplOptions.NoInlining)]
/// private static void Start() => ModEntry.Start&lt;Host&gt;(Settings.Bind, Hooks.Declare);
/// </code>
/// </summary>
internal static class ModEntry
{
    /// <summary>
    /// Sea Power exists in two versions with different map code (0.8.7's flat map and the 3D-globe build), and a mod compiled
    /// against one fails on the other. A two-build mod ships both DLLs; each stays off on the game it is not made for. Nothing
    /// here touches a game type, and <paramref name="start"/> is only compiled once it is called, on the right game.
    /// </summary>
    public static void Trigger(Action start)
    {
#if FLATMAP
        if (GameBuild.IsGlobe)
        {
            ModLog.Info("This game has the globe map: the globe build runs, the flat-map build stays off.");
            return;
        }
#elif !RATS_SINGLE_BUILD
        if (!GameBuild.IsGlobe)
        {
            ModLog.Info("This game has the flat map: the flat-map build runs, the globe build stays off.");
            return;
        }
#endif
        start();
    }

    /// <summary>
    /// Starts the mod once: settings (<paramref name="bindSettings"/> binds the mod's entries), the hook declarations
    /// (<paramref name="declareHooks"/>), the load-stage hooks, the mod's own start steps (<paramref name="steps"/>), the host.
    /// It stays off when it runs from a second folder, is unticked in the Mod Manager, is switched off in its config, or a hook
    /// it needs is missing.
    /// </summary>
    public static void Start<THost>(Action<ConfigFile> bindSettings, Action declareHooks, Action steps = null) where THost : ModHost
    {
        // Anchor Chain loads assemblies with Assembly.LoadFile, so the same DLL found in two folders would be two separate
        // assemblies with separate statics. The marker lives on the AppDomain so only the first one starts.
        string marker = ModInfo.Guid + ".loaded";
        if (AppDomain.CurrentDomain.GetData(marker) != null)
        {
            ModLog.Warn("Already loaded from another folder; skipping this copy.");
            return;
        }
        AppDomain.CurrentDomain.SetData(marker, true);
        try
        {
            if (Starts(bindSettings, declareHooks))
            {
                steps?.Invoke();
                ModHost.Create<THost>();
                ModLog.Info(ModInfo.Name + " " + ModInfo.Version + " loaded.");
            }
        }
        catch (Exception ex)
        {
            ModLog.Error("Startup failed, mod inactive: " + ex);
        }
    }

    private static bool Starts(Action<ConfigFile> bindSettings, Action declareHooks)
    {
        // Anchor Chain loads every mod folder it finds, ticked in the game's Mod Manager or not.
        if (!ModState.TickedInModManager())
        {
            ModLog.Info("Unticked in the Mod Manager - inactive.");
            return false;
        }
        ModSettings.Open(bindSettings);
        if (!ModSettings.Enabled.Value)
        {
            ModLog.Info("Disabled in config.");
            return false;
        }
        declareHooks();
        if (HookRegistry.ApplyStage(HookStage.Load))
            return true;
        HookRegistry.RemoveAll();
        return false;
    }
}
