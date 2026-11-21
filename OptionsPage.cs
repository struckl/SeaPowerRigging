using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace Rats.Shared;

/// <summary>
/// The mod's settings in the game's Options menu. When an options page (SeapowerUI.OptionsMenuView, a Noesis UserControl) has
/// loaded, a tab with the mod's title is added to its tab control, styled like the game's own tabs; if the page has no tab
/// control (a game update), the settings go as a group at the end of the Audio tab instead, found through its named controls.
/// Changes apply at once and are saved by the mod (ModSettings writes the file a moment after the last change); the page's
/// Accept / Revert do not touch them.
/// Only plain Noesis controls are made here, no XAML types: Noesis resolves those by full name across all loaded DLLs, and this
/// file's names are the same in every mod.
/// </summary>
internal static class OptionsPage
{
    private static string title;
    private static Action<OptionsBuilder> fill;
    private static bool reported;

    private static string Tag => ModInfo.LogSource + ".Options";

    /// <summary>
    /// Declares the hook on the options page (once NoesisGUI runs: the page registers dependency properties). <paramref name="tabTitle"/>
    /// is the tab's header, <paramref name="rows"/> adds the mod's rows.
    /// </summary>
    public static void Declare(string tabTitle, Action<OptionsBuilder> rows)
    {
        title = tabTitle;
        fill = rows;
        HookRegistry.Add("Options page", HookStage.AfterNoesis, "the settings are only in the config file", optional: true)
            .Hook("SeapowerUI.OptionsMenuView", ".ctor", Type.EmptyTypes, typeof(OptionsPage), postfix: nameof(ViewCreated));
    }

    private static void ViewCreated(object __instance)
    {
        try
        {
            if (__instance is Noesis.FrameworkElement view)
                view.Loaded += (s, e) => Attach(view);
        }
        catch (Exception ex) { ModLog.Warn("options page: " + ex.Message); }
    }

    private static void Attach(Noesis.FrameworkElement view)
    {
        try
        {
            Noesis.TabControl tabs = Find<Noesis.TabControl>(view, 24);
            if (tabs != null)
                AddTab(view, tabs);
            else
                AddAudioGroup(view);
        }
        catch (Exception ex)
        {
            Report($"{title} settings not added to the Options menu: " + ex.Message, warn: true);
        }
    }

    private static void AddTab(Noesis.FrameworkElement view, Noesis.TabControl tabs)
    {
        foreach (object item in tabs.Items)
            if (item is Noesis.FrameworkElement fe && fe.Tag as string == Tag) return;
        var panel = new Noesis.StackPanel { Orientation = Noesis.Orientation.Vertical, Margin = new Noesis.Thickness(20, 16, 20, 16) };
        fill(new OptionsBuilder(view, panel));
        var tab = new Noesis.TabItem
        {
            Tag = Tag,
            Header = title,
            Content = new Noesis.ScrollViewer { VerticalScrollBarVisibility = Noesis.ScrollBarVisibility.Auto, Content = panel },
        };
        foreach (object item in tabs.Items)
            if (item is Noesis.TabItem other && other.Style != null) { tab.Style = other.Style; break; }
        tabs.Items.Add(tab);
        Report($"{title} settings added as a tab of the Options menu.");
    }

    private static void AddAudioGroup(Noesis.FrameworkElement view)
    {
        Noesis.StackPanel audio = FindAudioPanel(view);
        if (audio == null)
        {
            Report($"Options menu layout not recognised (game update?) - the {title} settings are only in the config file.", warn: true);
            return;
        }
        for (int i = 0; i < audio.Children.Count; i++)
            if (audio.Children[i] is Noesis.FrameworkElement fe && fe.Tag as string == Tag) return;
        var group = new Noesis.StackPanel { Tag = Tag, Orientation = Noesis.Orientation.Vertical };
        var builder = new OptionsBuilder(view, group);
        builder.Header(ModInfo.Name);
        fill(builder);
        audio.Children.Add(group);
        Report($"{title} settings added to the Audio tab of the Options menu (no tab control found).");
    }

    private static void Report(string text, bool warn = false)
    {
        if (reported) return;
        reported = true;
        if (warn) ModLog.Warn(text); else ModLog.Info(text);
    }

    private static T Find<T>(Noesis.DependencyObject root, int maxDepth) where T : Noesis.DependencyObject
    {
        var queue = new Queue<(Noesis.DependencyObject node, int depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            if (node is T hit) return hit;
            if (depth >= maxDepth) continue;
            int n = Noesis.VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < n; i++)
                queue.Enqueue((Noesis.VisualTreeHelper.GetChild(node, i), depth + 1));
        }
        return null;
    }

    private static Noesis.StackPanel FindAudioPanel(Noesis.FrameworkElement view)
    {
        foreach (string name in new[] { "cb_MuteSoundInPause", "cb_SimultaneousVoiceovers", "sliderVoiceVolume", "sliderMasterVolume" })
        {
            Noesis.DependencyObject at = view.FindName(name) as Noesis.DependencyObject;
            for (int depth = 0; at != null && depth < 6; depth++)
            {
                at = at is Noesis.FrameworkElement fe && fe.Parent != null ? fe.Parent : Noesis.VisualTreeHelper.GetParent(at);
                if (at is Noesis.StackPanel sp && sp.Orientation == Noesis.Orientation.Vertical) return sp;
            }
        }
        return null;
    }
}

/// <summary>Adds rows to the mod's part of the options page, in the page's own styles where it has them.</summary>
internal sealed class OptionsBuilder
{
    private readonly Noesis.StackPanel panel;
    private readonly Noesis.Style headerStyle, labelStyle;
    private readonly Noesis.Brush highlight;
    private readonly float noteSize;

    public OptionsBuilder(Noesis.FrameworkElement view, Noesis.StackPanel panel)
    {
        this.panel = panel;
        headerStyle = view.TryFindResource("OptionsSectionHeader") as Noesis.Style;
        labelStyle = view.TryFindResource("TextBlockLabel") as Noesis.Style;
        highlight = view.TryFindResource("Brush.Foreground.Highlight") as Noesis.Brush;
        object size = view.TryFindResource("Font.Size.ToolTip");
        noteSize = size is float f ? f : size is double d ? (float)d : 0f;
    }

    public void Header(string text)
    {
        var header = new Noesis.TextBlock { Text = text, Margin = new Noesis.Thickness(0, 14, 0, 8), FontSize = 18f };
        if (headerStyle != null) header.Style = headerStyle;
        else if (highlight != null) header.Foreground = highlight;
        panel.Children.Add(header);
    }

    public void Note(string text)
    {
        var note = new Noesis.TextBlock { Text = text, TextWrapping = Noesis.TextWrapping.Wrap, Margin = new Noesis.Thickness(0, 0, 0, 12), Opacity = 0.8f };
        if (highlight != null) note.Foreground = highlight;
        if (noteSize > 0f) note.FontSize = noteSize;
        panel.Children.Add(note);
    }

    public void Check(string label, ConfigEntry<bool> entry, string tip)
    {
        var box = new Noesis.CheckBox { Content = label, Margin = new Noesis.Thickness(0, 0, 0, 12), IsChecked = entry.Value, ToolTip = tip };
        if (highlight != null) box.Foreground = highlight;
        box.Click += (s, e) => entry.Value = box.IsChecked == true;
        panel.Children.Add(box);
    }

    public void Choice(string label, ConfigEntry<string> entry, IList<string> values, string tip)
    {
        Noesis.Grid row = Row(label, tip);
        var combo = new Noesis.ComboBox { MinWidth = 240, HorizontalAlignment = Noesis.HorizontalAlignment.Left };
        foreach (string v in values) combo.Items.Add(v);
        combo.SelectedIndex = Math.Max(0, values.IndexOf(entry.Value));
        combo.SelectionChanged += (s, e) =>
        {
            int i = combo.SelectedIndex;
            if (i >= 0 && entry.Value != values[i]) entry.Value = values[i];
        };
        Noesis.Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        panel.Children.Add(row);
    }

    /// <summary>A slider in steps of <paramref name="step"/>, its value shown through <paramref name="format"/>.</summary>
    public void Slider(string label, string tip, ConfigEntry<float> entry, float min, float max, float step, Func<float, string> format)
    {
        Noesis.Grid row = Row(label, tip);
        var slider = new Noesis.Slider
        {
            Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true,
            Value = Math.Max(min, Math.Min(max, entry.Value)), MinWidth = 200,
        };
        Noesis.Grid.SetColumn(slider, 1);
        var value = new Noesis.TextBlock
        {
            Text = format(slider.Value), MinWidth = 64, Margin = new Noesis.Thickness(12, 0, 0, 0),
            VerticalAlignment = Noesis.VerticalAlignment.Center,
        };
        if (highlight != null) value.Foreground = highlight;
        Noesis.Grid.SetColumn(value, 2);
        slider.ValueChanged += (s, e) =>
        {
            float v = (float)(Math.Round(slider.Value / step) * step);
            value.Text = format(v);
            if (Math.Abs(entry.Value - v) > step * 0.01f) entry.Value = v;
        };
        row.Children.Add(slider);
        row.Children.Add(value);
        panel.Children.Add(row);
    }

    // A label in the first column; the control goes into column 1 (and a slider's value into column 2).
    private Noesis.Grid Row(string label, string tip)
    {
        var row = new Noesis.Grid { Margin = new Noesis.Thickness(0, 0, 0, 12), ToolTip = tip };
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(200f) });
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Star) });
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Auto) });
        var text = new Noesis.TextBlock { Text = label, VerticalAlignment = Noesis.VerticalAlignment.Center };
        if (labelStyle != null) text.Style = labelStyle;
        else if (highlight != null) text.Foreground = highlight;
        Noesis.Grid.SetColumn(text, 0);
        row.Children.Add(text);
        return row;
    }
}
