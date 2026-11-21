using System;
using System.Collections.Generic;

namespace Rats.Rigging;

/// <summary>
/// The game methods every mod needs hooked the same way: mission start and end, and the Options menu's page. Each is patched once,
/// under Rigging's own Harmony id, and passed on to every mod's own object: six mods patching the same method with the same static
/// method would each run it once per call.
/// </summary>
internal static class GameHooks
{
    private static RigHooks hooks;
    private static readonly Dictionary<string, bool> Installed = new Dictionary<string, bool>();

    private static RigHooks Hooks => hooks ??= new RigHooks(RigCore.Info, RigCore.Log);

    /// <summary>Mission ready (MainGameViewModel.OnStart, once, after every unit is loaded) and mission gone (GameMain.OnDestroy).</summary>
    public static bool Mission() => Once("Mission", () =>
        new HookFeature("Mission", HookStage.Load, "no mission events for the mods", optional: false)
            .Hook("SeapowerUI.ViewModels.MainGameViewModel", "OnStart", Type.EmptyTypes, typeof(GameHooks), postfix: nameof(MissionReady))
            .Hook("SeaPower.GameMain", "OnDestroy", Type.EmptyTypes, typeof(GameHooks), prefix: nameof(MissionUnloading)));

    /// <summary>The Options menu's page, once it has loaded its XAML (its constructor, also run for the page opened from the pause menu).</summary>
    public static bool OptionsPage() => Once("Options page", () =>
        new HookFeature("Options page", HookStage.AfterNoesis, "the mods' settings are only in their config files", optional: true)
            .Hook("SeapowerUI.OptionsMenuView", ".ctor", Type.EmptyTypes, typeof(GameHooks), postfix: nameof(OptionsViewCreated)));

    private static bool Once(string key, Func<HookFeature> declare)
    {
        if (!Installed.TryGetValue(key, out bool ok))
        {
            ok = Hooks.Install(declare());
            Installed[key] = ok;
        }
        return ok;
    }

    // Both run inside game code: each mod's handlers are called on their own, a broken one never breaks the game method.
    private static void MissionReady()
    {
        foreach (RigMod mod in RigMod.Mods)
            mod.Mission.RaiseStarted();
    }

    private static void MissionUnloading()
    {
        foreach (RigMod mod in RigMod.Mods)
            mod.Mission.RaiseEnded();
    }

    private static void OptionsViewCreated(object __instance)
    {
        foreach (RigMod mod in RigMod.Mods)
            mod.Options.ViewCreated(__instance);
    }
}
