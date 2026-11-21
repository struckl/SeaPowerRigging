using System;
using UnityEngine;

namespace Rats.Rigging;

/// <summary>
/// The mod's per-frame object, on a hidden object that lives for the whole game. It applies the hooks that wait for NoesisGUI,
/// passes mission start and end on, writes the settings, and stops all Noesis work when the game closes. A mod's host derives
/// from it and fills in Tick, OnMissionReady, OnMissionEnded and Teardown.
/// </summary>
public abstract class RigHost : MonoBehaviour
{
    private const int ErrorCap = 5;

    // Unity runs Awake inside AddComponent, before the creator can hand the mod's object over.
    private static RigMod creating;

    private enum StageTwo { Waiting, Applied }
    private StageTwo stageTwo;
    private float nextHookCheck;
    private int errors;

    /// <summary>The mod's own Rigging object: log, settings, hooks, mission events.</summary>
    protected RigMod Mod { get; private set; }

    /// <summary>Logged once the AfterNoesis hooks are in place; null for none.</summary>
    protected virtual string StageTwoReady => null;

    /// <summary>How often to look whether NoesisGUI runs yet.</summary>
    protected virtual float HookPollSeconds => 1f;

    /// <summary>The host object, made by <see cref="RigMod.Start"/> once the load-stage hooks are in.</summary>
    internal static T Create<T>(RigMod rig) where T : RigHost
    {
        creating = rig;
        try
        {
            var go = new GameObject(rig.Info.LogSource);
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<T>();
        }
        finally
        {
            creating = null;
        }
    }

    protected virtual void Awake()
    {
        Mod = creating ?? throw new InvalidOperationException("A host is made by RigMod.Start only.");
        Mod.Host = this;
        Mod.Mission.Started += MissionStarted;
        Mod.Mission.Ended += MissionEnded;
    }

    protected virtual void OnDestroy()
    {
        if (Mod == null)
            return;
        Mod.Mission.Started -= MissionStarted;
        Mod.Mission.Ended -= MissionEnded;
        if (Mod.Host == this)
            Mod.Host = null;
    }

    private void Update()
    {
        PollStageTwo();
        Mod.Settings.FlushIfDue();
        try { Tick(); }
        catch (Exception ex) { ReportError("update: " + ex); }
    }

    /// <summary>The mod's work every frame; an exception is logged (the first few only) and the next frame tries again.</summary>
    protected virtual void Tick() { }

    protected virtual void OnMissionReady() { }

    protected virtual void OnMissionEnded() { }

    /// <summary>
    /// The game closes: what the mod built into the game's UI goes now, while every Noesis view is still alive. By default what a
    /// mission end does.
    /// </summary>
    protected virtual void Teardown() => OnMissionEnded();

    /// <summary>Logs the first few errors of the host's per-frame work; a broken frame repeats every frame.</summary>
    protected void ReportError(string text)
    {
        if (errors++ < ErrorCap)
            Mod.Log.Error(text);
    }

    private void PollStageTwo()
    {
        if (stageTwo != StageTwo.Waiting || Time.unscaledTime < nextHookCheck)
            return;
        nextHookCheck = Time.unscaledTime + HookPollSeconds;
        if (!GameBuild.NoesisUp())
            return;
        stageTwo = StageTwo.Applied;
        if (Mod.Hooks.ApplyStage(HookStage.AfterNoesis) && StageTwoReady != null)
            Mod.Log.Info(StageTwoReady);
    }

    private void MissionStarted() => OnMissionReady();

    private void MissionEnded()
    {
        if (!Mod.Quitting)
            OnMissionEnded();
    }

    /// <summary>
    /// Unity calls this before any OnDestroy. The game's GameMain.OnDestroy (the mission-end hook) comes later, in the middle of
    /// the shutdown, when Noesis is being taken apart already: Pop-Out tore its UI down there and crashed in Noesis.dll on quit
    /// (Keel report #6). So the teardown runs here and the mission end is skipped from now on.
    /// </summary>
    private void OnApplicationQuit()
    {
        Mod.Settings.Flush();
        Mod.Quitting = true;
        try { Teardown(); }
        catch (Exception ex) { Mod.Log.Debug($"teardown on quit: {ex.Message}"); }
    }
}
