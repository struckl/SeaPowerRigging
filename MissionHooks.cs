using System;

namespace Rats.Shared;

/// <summary>
/// Mission ready: MainGameViewModel.OnStart (once, after every unit is loaded). Mission gone: GameMain.OnDestroy (GameMain lives
/// in the mission scene, so it dies on every way out). Both run inside game code, so every subscriber is called on its own: a
/// broken one must never break the game method it was called from.
/// </summary>
internal static class MissionHooks
{
    public const string FeatureName = "Mission";

    public static event Action Started;
    public static event Action Ended;

    /// <summary>
    /// Declares the two hooks as a load-stage feature. Required by default: most mods have nothing to start from without them.
    /// A mod with features that work outside missions too (Command Tools' links, roles, RTB colours) passes optional, so a game
    /// update that moves these methods switches off only the mission-bound features (those check IsActive("Mission")).
    /// </summary>
    public static Feature Declare(bool optional = false) =>
        HookRegistry.Add(FeatureName, HookStage.Load, optional ? "mission-bound features off" : "mod inactive", optional)
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "OnStart", Type.EmptyTypes, typeof(MissionHooks), postfix: nameof(MissionReady))
        .Hook("SeaPower.GameMain", "OnDestroy", Type.EmptyTypes, typeof(MissionHooks), prefix: nameof(MissionUnloading));

    private static void MissionReady() => Safe(Started, "mission start");

    private static void MissionUnloading() => Safe(Ended, "mission end");

    private static void Safe(Action handlers, string name)
    {
        if (handlers == null)
            return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { ModLog.Error($"{name} handler failed: {ex}"); }
        }
    }
}
