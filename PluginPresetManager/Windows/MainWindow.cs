using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace PluginPresetManager.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Tabs.PresetsTab presetsTab;
    private readonly Tabs.WindowRescueTab windowRescueTab;
    private readonly Tabs.SettingsTab settingsTab;
    private readonly Tabs.HelpTab helpTab;

    private bool focusSettingsTabNextDraw = false;

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(680, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        presetsTab = new Tabs.PresetsTab(plugin, plugin.PresetManager);
        windowRescueTab = new Tabs.WindowRescueTab(plugin.WindowRescueHelper);
        settingsTab = new Tabs.SettingsTab(plugin, plugin.PresetManager);
        helpTab = new Tabs.HelpTab();
    }

    public void Dispose() { }

    public void FocusSettingsTab()
    {
        focusSettingsTabNextDraw = true;
        IsOpen = true;
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("MainTabs");
        if (!tabBar) return;

        using (var tab = ImRaii.TabItem("Presets"))
        {
            if (tab)
            {
                ImGui.Spacing();
                presetsTab.Draw();
            }
        }

        using (var tab = ImRaii.TabItem("Tools"))
        {
            if (tab)
            {
                ImGui.Spacing();
                windowRescueTab.Draw();
            }
        }

        var settingsFlags = focusSettingsTabNextDraw ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        using (var tab = ImRaii.TabItem("Settings", settingsFlags))
        {
            if (tab)
            {
                if (focusSettingsTabNextDraw) focusSettingsTabNextDraw = false;
                ImGui.Spacing();
                settingsTab.Draw();
            }
        }

        using (var tab = ImRaii.TabItem("Help"))
        {
            if (tab)
            {
                ImGui.Spacing();
                helpTab.Draw();
            }
        }
    }
}
