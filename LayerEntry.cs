using System;
using System.Collections.Generic;
using System.ComponentModel;
using BepInEx.Configuration;
using SeaPower;
using SeapowerUI;

namespace Rats.Shared;

/// <summary>
/// An entry in the map's Layers menu (the menu lists the layer manager's layers, so one more MapLayer there gets a checkbox like
/// the stock layers). Its state is kept in the mod's config.
/// Numbers: Better Tac Map uses 500-509, Topographic Map 600, Borders 601, Cities 602, Sensor lines 603, Sensor coverage 604.
/// </summary>
internal sealed class LayerEntry
{
    private readonly int preferredNumber;
    private readonly string name, displayName;
    private readonly ConfigEntry<bool> visible;

    private MapLayer layer;
    private bool failed;

    public LayerEntry(int preferredNumber, string name, string displayName, ConfigEntry<bool> visible)
    {
        this.preferredNumber = preferredNumber;
        this.name = name;
        this.displayName = displayName;
        this.visible = visible;
    }

    public bool Visible => layer?.Visible ?? visible.Value;

    /// <summary>Adds the entry once the game's layer manager exists (it lives for the whole session).</summary>
    public void EnsureRegistered()
    {
        if (layer != null || failed)
            return;
        try
        {
            Register();
        }
        catch (Exception ex)
        {
            failed = true;
            ModLog.Error($"Could not add the map layer '{displayName}', use the toggle key instead: {ex}");
        }
    }

    public void Toggle()
    {
        if (layer != null)
            layer.Visible = !layer.Visible;
        else
            visible.Value = !visible.Value;
    }

#if FLATMAP
    // Sea Power 0.8.7: one layer manager for the whole game.
    private void Register()
    {
        GizmoLayerManager manager = GizmoLayerManager.Instance;
        if (manager.LayersByName.TryGetValue(name, out MapLayer existing))
        {
            Adopt(existing);
            return;
        }
        int number = FreeNumber(manager.LayersById);
        NameInMenu();
        var created = new MapLayer(number, name, true);
        manager.Layers.Add(created);
        manager.LayersByName[name] = created;
        manager.LayersById[number] = created;
        Added(created, number);
    }
#else
    // The 3D-globe build: every map has its own display state with its own layer list.
    private void Register()
    {
        MapDisplayState state = MapDisplayState.Tactical;
        if (state.LayersByName.TryGetValue(name, out MapLayer existing))
        {
            Adopt(existing);
            return;
        }
        int number = FreeNumber(state.LayersById);
        NameInMenu();
        MapLayer created = GameReflection.NewLayer(state, number, name, true);
        if (created == null || !(state.LayersByName is IDictionary<string, MapLayer> byName) ||
            !(state.LayersById is IDictionary<int, MapLayer> byId))
        {
            failed = true;
            ModLog.Warn($"The map's layer list has changed - no '{displayName}' entry in the Layers menu; the toggle key still works.");
            return;
        }
        state.Layers.Add(created);
        byName[name] = created;
        byId[number] = created;
        Added(created, number);
    }
#endif

    private int FreeNumber(IReadOnlyDictionary<int, MapLayer> taken)
    {
        int number = preferredNumber;
        while (taken.ContainsKey(number))
            number++;
        return number;
    }

    // MapLayer looks its menu text up in the language table; give it ours first.
    private void NameInMenu() =>
        Singleton<LanguageResourceHandler>.Instance.LanguageDictionary[("MapLayers" + name).ToLowerInvariant()] = displayName;

    private void Added(MapLayer created, int number)
    {
        Adopt(created);
        ModLog.Info($"Map layer '{displayName}' added to the Layers menu (#{number}), {(created.Visible ? "on" : "off")}.");
    }

    private void Adopt(MapLayer l)
    {
        layer = l;
        l.Visible = visible.Value;
        l.PropertyChanged += OnLayerChanged;
    }

    private void OnLayerChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapLayer.Visible) && layer != null && visible.Value != layer.Visible)
            visible.Value = layer.Visible;
    }
}
