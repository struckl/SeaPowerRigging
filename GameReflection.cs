#if !FLATMAP
using System;
using System.Reflection;
using SeaPower.LiveMap;
using SeapowerUI;

namespace Rats.Shared;

/// <summary>
/// The few members of the 3D-globe build the mods need that the game keeps internal, looked up by reflection. Each lookup that
/// fails is logged once and its feature falls back (see the callers).
/// Fields hold only plain types (Type, ConstructorInfo, Delegate): Unity's ECS start-up loads every type of every loaded
/// assembly, and a field of a type 0.8.7 lacks (Func of MapPalette) makes the class unloadable there - ECS then fails and no
/// mission loads, although this globe DLL stays off on 0.8.7. tools/compat-check --classload guards this.
/// </summary>
internal static class GameReflection
{
    public const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Type LayerEntryType = typeof(GizmoLayerManager).GetNestedType("Entry", BindingFlags.NonPublic);

    private static readonly ConstructorInfo LayerCtor = LayerEntryType == null ? null :
        typeof(MapLayer).GetConstructor(Any, null, new[] { typeof(MapDisplayState), LayerEntryType.MakeByRefType(), typeof(bool) }, null);

    private static Delegate paletteDark;
    private static bool paletteLooked;

    /// <summary>Whether a map shows its night colours (in Auto: at the centre of the view); false if the palette is unknown.</summary>
    public static bool IsDark(MapPalette palette)
    {
        if (!paletteLooked)
        {
            paletteLooked = true;
            paletteDark = Getter<MapPalette, bool>("IsDark");
        }
        return palette != null && paletteDark != null && ((Func<MapPalette, bool>)paletteDark)(palette);
    }

    /// <summary>A new entry for a map's Layers menu; null if the game's layer type has changed.</summary>
    public static MapLayer NewLayer(MapDisplayState owner, int number, string name, bool visible)
    {
        if (LayerCtor == null)
            return null;
        object entry = Activator.CreateInstance(LayerEntryType, number, name, true);
        return (MapLayer)LayerCtor.Invoke(new[] { owner, entry, (object)visible });
    }

    /// <summary>A getter of a property the game keeps internal, as a plain Delegate (see above); null with a warning when it is gone.</summary>
    public static Delegate Getter<T, TResult>(string property)
    {
        MethodInfo get = typeof(T).GetProperty(property, Any)?.GetGetMethod(nonPublic: true);
        if (get == null)
        {
            ModLog.Warn($"{typeof(T).Name}.{property} not found (game updated?)");
            return null;
        }
        return Delegate.CreateDelegate(typeof(Func<T, TResult>), get);
    }
}
#endif
