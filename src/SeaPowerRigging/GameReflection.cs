using System;
using System.Reflection;
using SeaPower.LiveMap;
using SeapowerUI;

namespace Rats.Rigging;

/// <summary>
/// The few members of the game the mods need that it keeps internal, looked up by reflection. Each lookup that fails is logged
/// once and its feature falls back (see the callers). These are facts about the game, the same for every mod, so they are static.
/// </summary>
public static class GameReflection
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
    internal static MapLayer NewLayer(MapDisplayState owner, int number, string name, bool visible)
    {
        if (LayerCtor == null)
            return null;
        object entry = Activator.CreateInstance(LayerEntryType, number, name, true);
        return (MapLayer)LayerCtor.Invoke(new[] { owner, entry, (object)visible });
    }

    /// <summary>A getter of a property the game keeps internal, as a plain Delegate; null with a warning when it is gone.</summary>
    private static Delegate Getter<T, TResult>(string property)
    {
        MethodInfo get = typeof(T).GetProperty(property, Any)?.GetGetMethod(nonPublic: true);
        if (get == null)
        {
            RigCore.Log.Warn($"{typeof(T).Name}.{property} not found (game updated?)");
            return null;
        }
        return Delegate.CreateDelegate(typeof(Func<T, TResult>), get);
    }
}
