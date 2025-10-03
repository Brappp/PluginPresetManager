using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;
    private readonly PresetManager presetManager;

    public ConfigWindow(Plugin plugin)
        : base("Settings###PresetManagerSettings")
    {
        Size = new Vector2(450, 350);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.config = plugin.Configuration;
        this.presetManager = plugin.PresetManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Rollback");
        var enableRollback = config.EnableRollback;
        if (ImGui.Checkbox("Enable Rollback", ref enableRollback))
        {
            config.EnableRollback = enableRollback;
            Plugin.PluginInterface.SavePluginConfig(config);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Saves state before applying presets so you can rollback");
        }

        if (config.EnableRollback && config.LastState != null)
        {
            ImGui.Indent();
            if (ImGui.Button("Rollback to Previous State"))
            {
                _ = presetManager.RollbackAsync();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Restore the plugin state from before the last preset was applied");
            }
            ImGui.Unindent();
        }

        ImGui.Separator();

        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Settings");
        var showNotifications = config.ShowNotifications;
        if (ImGui.Checkbox("Show Notifications", ref showNotifications))
        {
            config.ShowNotifications = showNotifications;
            Plugin.PluginInterface.SavePluginConfig(config);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Show chat notifications when presets are applied");
        }

        if (config.ShowNotifications)
        {
            ImGui.Indent();
            var verboseNotifications = config.VerboseNotifications;
            if (ImGui.Checkbox("Verbose Notifications", ref verboseNotifications))
            {
                config.VerboseNotifications = verboseNotifications;
                Plugin.PluginInterface.SavePluginConfig(config);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Show detailed info (plugin counts, warnings, etc.)");
            }
            ImGui.Unindent();
        }

        var delay = config.DelayBetweenCommands;
        if (ImGui.SliderInt("Delay (ms)", ref delay, 50, 500))
        {
            config.DelayBetweenCommands = delay;
            Plugin.PluginInterface.SavePluginConfig(config);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Time to wait between enable/disable commands\nHigher values are more stable but slower");
        }

        ImGui.Separator();

        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Info");

        var lastPreset = presetManager.GetLastAppliedPreset();
        if (lastPreset != null)
        {
            ImGui.TextUnformatted($"Last Applied Preset: {lastPreset.Name}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "No preset applied yet");
        }

        ImGui.Text($"Presets: {presetManager.GetAllPresets().Count} | Always-On: {presetManager.GetAlwaysOnPlugins().Count}");

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Commands");
        ImGui.BulletText("/ppreset - Open window");
        ImGui.BulletText("/ppm <name> - Apply preset");
        ImGui.BulletText("/ppm alwayson - Minimal mode");
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "v2.0.0");
    }
}
