using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Rats.Shared;

/// <summary>When a feature's hooks are applied.</summary>
internal enum HookStage
{
    /// <summary>At plugin load, from ModEntry. A required feature that fails here leaves the whole mod off.</summary>
    Load,

    /// <summary>Once NoesisGUI runs (ModHost polls GameBuild.NoesisUp): for targets whose classes touch Noesis.</summary>
    AfterNoesis,
}

/// <summary>
/// One mod feature and the game methods it hooks. Its hooks go in together or not at all: every target is resolved first,
/// and a patch that throws halfway takes the ones before it back out.
/// </summary>
internal sealed class Feature
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

    public bool Active { get; internal set; }

    internal Feature(string name, HookStage stage, string consequence, bool optional)
    {
        Name = name;
        Stage = stage;
        Consequence = consequence;
        Optional = optional;
    }

    /// <summary>
    /// Declares a hook. <paramref name="type"/> is the game type's full name (e.g. "SeaPower.GameMain"), <paramref name="method"/>
    /// a method name or ".ctor"; prefix and postfix name static methods of <paramref name="patchClass"/>.
    /// </summary>
    public Feature Hook(string type, string method, Type[] args, Type patchClass, string prefix = null, string postfix = null)
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
/// Every game method the mod hooks, grouped by feature. Game updates rename or remove methods: a missing target switches its
/// feature off with one log line, the rest of the mod keeps working and the game never sees an exception from us.
/// </summary>
internal static class HookRegistry
{
    private static readonly List<Feature> Features = new List<Feature>();
    private static Harmony harmony;

    public static Feature Add(string name, HookStage stage, string consequence, bool optional = false)
    {
        var feature = new Feature(name, stage, consequence, optional);
        Features.Add(feature);
        return feature;
    }

    public static bool IsActive(string feature) => Features.Any(f => f.Name == feature && f.Active);

    /// <summary>Every feature by name, the ones that are off marked "(OFF)".</summary>
    public static string Summary() => string.Join(", ", Features.Select(f => f.Name + (f.Active ? "" : " (OFF)")));

    /// <summary>Applies every feature of the stage, each on its own; false when a feature that is not optional failed.</summary>
    public static bool ApplyStage(HookStage stage)
    {
        harmony ??= new Harmony(ModInfo.Guid);
        bool ok = true;
        foreach (Feature feature in Features.Where(f => f.Stage == stage && !f.Active))
            ok &= Apply(feature) || feature.Optional;
        return ok;
    }

    /// <summary>Takes every hook of the mod out again (the mod stays off after a failed start).</summary>
    public static void RemoveAll()
    {
        harmony?.UnpatchSelf();
        foreach (Feature feature in Features)
            feature.Active = false;
    }

    private static bool Apply(Feature feature)
    {
        var resolved = new List<(Feature.Target spec, MethodBase method)>();
        foreach (Feature.Target spec in feature.Targets)
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

    private static MethodBase Resolve(Feature.Target spec)
    {
        Type type = AccessTools.TypeByName(spec.Type);
        if (type == null)
            return null;
        if (spec.Method == ".ctor")
            return AccessTools.Constructor(type, spec.Args ?? Type.EmptyTypes);
        return AccessTools.Method(type, spec.Method, spec.Args);
    }

    private static string PatchMethodProblem(Feature.Target spec)
    {
        if (spec.PrefixName == null && spec.PostfixName == null)
            return "has no patch method";
        if ((spec.PrefixName != null && spec.Prefix == null) || (spec.PostfixName != null && spec.Postfix == null))
            return $"has no patch method {spec.PrefixName ?? spec.PostfixName} in the mod";
        return null;
    }

    private static bool Patch(Feature feature, List<(Feature.Target spec, MethodBase method)> resolved)
    {
        var applied = new List<(Feature.Target spec, MethodBase method)>();
        try
        {
            foreach ((Feature.Target spec, MethodBase method) in resolved)
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
            foreach ((Feature.Target spec, MethodBase method) in applied)
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

    private static void Report(Feature feature, string text)
    {
        if (feature.Optional) ModLog.Warn(text);
        else ModLog.Error(text);
    }
}
