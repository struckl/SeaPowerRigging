using System;
using UnityEngine;

namespace Rats.Shared;

/// <summary>
/// The mod's per-frame object, on a hidden object that lives for the whole game. It applies the hooks that wait for NoesisGUI,
/// passes mission start and end on, writes the settings, and stops all Noesis work when the game closes. A mod's host derives
/// from it and fills in Tick, OnMissionReady, OnMissionEnded and Teardown.
/// </summary>
internal abstract class ModHost : MonoBehaviour
{
    private const int ErrorCap = 5;

    public static ModHost Instance { get; private set; }

    /// <summary>True from the moment the game starts to close: nothing touches Noesis after that.</summary>
    public static bool Quitting { get; private set; }

    private enum StageTwo { Waiting, Applied }
    private StageTwo stageTwo;
    private float nextHookCheck;
    private int errors;

    /// <summary>Logged once the AfterNoesis hooks are in place; null for none.</summary>
    protected virtual string StageTwoReady => null;

    /// <summary>How often to look whether NoesisGUI runs yet.</summary>
    protected virtual float HookPollSeconds => 1f;

    /// <summary>The host object, from ModEntry once the load-stage hooks are in.</summary>
    internal static T Create<T>() where T : ModHost
    {
        var go = new GameObject(ModInfo.LogSource);
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        return go.AddComponent<T>();
    }

    protected virtual void Awake()
    {
        Instance = this;
        MissionHooks.Started += MissionStarted;
        MissionHooks.Ended += MissionEnded;
    }

    protected virtual void OnDestroy()
    {
        MissionHooks.Started -= MissionStarted;
        MissionHooks.Ended -= MissionEnded;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        PollStageTwo();
        ModSettings.FlushIfDue();
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
            ModLog.Error(text);
    }

    private void PollStageTwo()
    {
        if (stageTwo != StageTwo.Waiting || Time.unscaledTime < nextHookCheck)
            return;
        nextHookCheck = Time.unscaledTime + HookPollSeconds;
        if (!GameBuild.NoesisUp())
            return;
        stageTwo = StageTwo.Applied;
        if (HookRegistry.ApplyStage(HookStage.AfterNoesis) && StageTwoReady != null)
            ModLog.Info(StageTwoReady);
    }

    private void MissionStarted() => OnMissionReady();

    private void MissionEnded()
    {
        if (!Quitting)
            OnMissionEnded();
    }

    /// <summary>
    /// Unity calls this before any OnDestroy. The game's GameMain.OnDestroy (the mission-end hook) comes later, in the middle of
    /// the shutdown, when Noesis is being taken apart already: Pop-Out tore its UI down there and crashed in Noesis.dll on quit
    /// (Keel report #6). So the teardown runs here and the mission end is skipped from now on.
    /// </summary>
    private void OnApplicationQuit()
    {
        ModSettings.Flush();
        Quitting = true;
        try { Teardown(); }
        catch (Exception ex) { ModLog.Debug($"teardown on quit: {ex.Message}"); }
    }
}
