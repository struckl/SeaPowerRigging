using System;

namespace Rats.Rigging;

/// <summary>
/// Mission ready: MainGameViewModel.OnStart (once, after every unit is loaded). Mission gone: GameMain.OnDestroy (GameMain lives
/// in the mission scene, so it dies on every way out). Both run inside game code, so every subscriber is called on its own: a
/// broken one must never break the game method it was called from.
/// </summary>
public sealed class RigMission
{
    public const string FeatureName = "Mission";

    private readonly RigMod mod;

    internal RigMission(RigMod mod)
    {
        this.mod = mod;
    }

    public event Action Started;

    public event Action Ended;

    /// <summary>
    /// Declares the feature that stands for the two hooks (Rigging installs them once for every mod). Required by default: most
    /// mods have nothing to start from without them. A mod with features that work outside missions too passes optional, so a game
    /// update that moves these methods switches off only the mission-bound features (those check Hooks.IsActive(FeatureName)).
    /// </summary>
    public HookFeature Declare(bool optional = false) =>
        mod.Hooks.AddShared(FeatureName, HookStage.Load, optional ? "mission-bound features off" : "mod inactive", optional, GameHooks.Mission);

    internal void RaiseStarted() => Safe(Started, "mission start");

    internal void RaiseEnded() => Safe(Ended, "mission end");

    private void Safe(Action handlers, string name)
    {
        if (handlers == null)
            return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { mod.Log.Error($"{name} handler failed: {ex}"); }
        }
    }
}
