using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Rats.Rigging;

/// <summary>When a feature's hooks are applied.</summary>
public enum HookStage
{
    /// <summary>At plugin load, from the mod's start. A required feature that fails here leaves the whole mod off.</summary>
    Load,

    /// <summary>Once NoesisGUI runs (the host polls GameBuild.NoesisUp): for targets whose classes touch Noesis.</summary>
    AfterNoesis,
}

/// <summary>
/// One mod feature and the game methods it hooks. Its hooks go in together or not at all: every target is resolved first,
/// and a patch that throws halfway takes the ones before it back out.
/// </summary>
public sealed class HookFeature
{
    internal sealed class Target
    {
        public string Type, Method;
        public Type[] Args;              // null = unique by name
        public MethodInfo Prefix, Postfix;
        public string PrefixName, PostfixName;
    }

    public readonly string Name;
    public readonly HookStage Stage;
    public readonly bool Optional;

    /// <summary>What the player loses when the feature is off, for the log line.</summary>
    public readonly string Consequence;

    internal readonly List<Target> Targets = new List<Target>();

    // A feature whose hooks Rigging installs once for every mod (mission start and end, the options page): true when they are in place.
    internal readonly Func<bool> Shared;

    internal HookFeature(string name, HookStage stage, string consequence, bool optional, Func<bool> shared = null)
    {
        Name = name;
        Stage = stage;
        Consequence = consequence;
        Optional = optional;
        Shared = shared;
    }

    public bool Active { get; internal set; }

    /// <summary>
    /// Declares a hook. <paramref name="type"/> is the game type's full name (e.g. "SeaPower.GameMain"), <paramref name="method"/>
    /// a method name or ".ctor"; prefix and postfix name static methods of <paramref name="patchClass"/>.
    /// </summary>
    public HookFeature Hook(string type, string method, Type[] args, Type patchClass, string prefix = null, string postfix = null)
    {
        Targets.Add(new Target
        {
            Type = type,
            Method = method,
            Args = args,
            PrefixName = prefix,
            PostfixName = postfix,
            Prefix = prefix == null ? null : AccessTools.Method(patchClass, prefix),
            Postfix = postfix == null ? null : AccessTools.Method(patchClass, postfix),
        });
        return this;
    }
}

/// <summary>
/// Every game method a mod hooks, grouped by feature. Game updates rename or remove methods: a missing target switches its
/// feature off with one log line, the rest of the mod keeps working and the game never sees an exception from us. The hooks go
/// in under the mod's own Harmony id.
/// </summary>
public sealed class RigHooks
{
    private readonly List<HookFeature> features = new List<HookFeature>();
    private readonly RigInfo info;
    private readonly RigLog log;
    private Harmony harmony;

    internal RigHooks(RigInfo info, RigLog log)
    {
        this.info = info;
        this.log = log;
    }

    public HookFeature Add(string name, HookStage stage, string consequence, bool optional = false)
    {
        var feature = new HookFeature(name, stage, consequence, optional);
        features.Add(feature);
        return feature;
    }

    // A feature that stands for hooks Rigging installs once (see GameHooks); it is active when they are in place.
    internal HookFeature AddShared(string name, HookStage stage, string consequence, bool optional, Func<bool> install)
    {
        var feature = new HookFeature(name, stage, consequence, optional, install);
        features.Add(feature);
        return feature;
    }

    public bool IsActive(string feature) => features.Any(f => f.Name == feature && f.Active);

    /// <summary>Every feature by name, the ones that are off marked "(OFF)".</summary>
    public string Summary() => string.Join(", ", features.Select(f => f.Name + (f.Active ? "" : " (OFF)")));

    /// <summary>Applies every feature of the stage, each on its own; false when a feature that is not optional failed.</summary>
    internal bool ApplyStage(HookStage stage)
    {
        harmony ??= new Harmony(info.Guid);
        bool ok = true;
        foreach (HookFeature feature in features.Where(f => f.Stage == stage && !f.Active).ToList())
            ok &= Apply(feature) || feature.Optional;
        return ok;
    }

    /// <summary>Takes every hook of the mod out again (the mod stays off after a failed start).</summary>
    internal void RemoveAll()
    {
        harmony?.UnpatchSelf();
        foreach (HookFeature feature in features)
            feature.Active = false;
    }

    /// <summary>Applies one feature now (the hooks Rigging installs for every mod); true when it is in place.</summary>
    internal bool Install(HookFeature feature)
    {
        features.Add(feature);
        harmony ??= new Harmony(info.Guid);
        return Apply(feature);
    }

    private bool Apply(HookFeature feature)
    {
        if (feature.Shared != null)
        {
            feature.Active = feature.Shared();
            if (!feature.Active)
                Report(feature, $"Rigging's hooks for '{feature.Name}' are not in place - {feature.Consequence}.");
            return feature.Active;
        }
        var resolved = new List<(HookFeature.Target spec, MethodBase method)>();
        foreach (HookFeature.Target spec in feature.Targets)
        {
            MethodBase method = Resolve(spec);
            string problem = method == null ? "not found (game updated?)" : PatchMethodProblem(spec);
            if (problem != null)
            {
                Report(feature, $"{spec.Type}.{spec.Method} {problem} - {feature.Consequence}.");
                return false;
            }
            resolved.Add((spec, method));
        }
        return Patch(feature, resolved);
    }

    private static MethodBase Resolve(HookFeature.Target spec)
    {
        Type type = AccessTools.TypeByName(spec.Type);
        if (type == null)
            return null;
        if (spec.Method == ".ctor")
            return AccessTools.Constructor(type, spec.Args ?? Type.EmptyTypes);
        return AccessTools.Method(type, spec.Method, spec.Args);
    }

    private static string PatchMethodProblem(HookFeature.Target spec)
    {
        if (spec.PrefixName == null && spec.PostfixName == null)
            return "has no patch method";
        if ((spec.PrefixName != null && spec.Prefix == null) || (spec.PostfixName != null && spec.Postfix == null))
            return $"has no patch method {spec.PrefixName ?? spec.PostfixName} in the mod";
        return null;
    }

    private bool Patch(HookFeature feature, List<(HookFeature.Target spec, MethodBase method)> resolved)
    {
        var applied = new List<(HookFeature.Target spec, MethodBase method)>();
        try
        {
            foreach ((HookFeature.Target spec, MethodBase method) in resolved)
            {
                harmony.Patch(method, prefix: WithPriority(spec.Prefix), postfix: WithPriority(spec.Postfix));
                applied.Add((spec, method));
            }
            feature.Active = true;
            return true;
        }
        catch (Exception ex)
        {
            Report(feature, $"Feature '{feature.Name}': patching failed - {feature.Consequence}: {ex.Message}");
            foreach ((HookFeature.Target spec, MethodBase method) in applied)
            {
                if (spec.Prefix != null) harmony.Unpatch(method, spec.Prefix);
                if (spec.Postfix != null) harmony.Unpatch(method, spec.Postfix);
            }
            return false;
        }
    }

    /// <summary>The patch method with its [HarmonyPriority] set explicitly, so the order against other mods' patches holds.</summary>
    private static HarmonyMethod WithPriority(MethodInfo method)
    {
        if (method == null)
            return null;
        var hm = new HarmonyMethod(method);
        if (method.GetCustomAttributes(typeof(HarmonyPriority), false).FirstOrDefault() is HarmonyPriority p)
            hm.priority = p.info.priority;
        return hm;
    }

    private void Report(HookFeature feature, string text)
    {
        if (feature.Optional) log.Warn(text);
        else log.Error(text);
    }
}
