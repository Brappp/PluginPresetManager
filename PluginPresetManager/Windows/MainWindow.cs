using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace PluginPresetManager.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Tabs.PresetsTab presetsTab;

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(620, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        presetsTab = new Tabs.PresetsTab(plugin, plugin.PresetManager);

        TitleBarButtons = new List<TitleBarButton>
        {
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new Vector2(2, 2),
                Click = _ => presetsTab.ToggleSettings(),
                ShowTooltip = () => ImGui.SetTooltip("Settings"),
            },
            new()
            {
                Icon = FontAwesomeIcon.QuestionCircle,
                IconOffset = new Vector2(2, 2),
                Click = _ => presetsTab.RequestHelp(),
                ShowTooltip = () => ImGui.SetTooltip("Commands & help"),
            },
        };
    }

    public void Dispose() { }

    public void OpenSettings()
    {
        presetsTab.ShowSettings();
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 5) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(7, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 4f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1f, 1f, 1f, 0.07f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1f, 1f, 1f, 0.11f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1f, 1f, 1f, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, UI.Colors.Primary);
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1f, 1f, 1f, 0.09f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1f, 1f, 1f, 0.05f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1f, 1f, 1f, 0.13f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(1f, 1f, 1f, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.09f));
        ImGui.PushStyleColor(ImGuiCol.NavHighlight, new Vector4(0f, 0f, 0f, 0f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(14);
        ImGui.PopStyleVar(9);
    }

    public override void Draw()
    {
        presetsTab.Draw();
    }
}
