using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace PluginPresetManager.UI;

public static class UIHelpers
{
    public static void SectionHeader(string text, FontAwesomeIcon icon)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Header))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.Text(icon.ToIconString());
            }
            ImGui.SameLine();
            ImGui.Text(text);
        }
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static void SectionHeader(string text)
    {
        ImGui.TextColored(Colors.Header, text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static void SectionHeaderInline(string text, FontAwesomeIcon icon)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Header))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.Text(icon.ToIconString());
            }
            ImGui.SameLine();
            ImGui.Text(text);
        }
    }

    public static float IconButtonWidth(FontAwesomeIcon icon)
    {
        float glyphWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            glyphWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        }
        return glyphWidth + ImGui.GetStyle().FramePadding.X * 2;
    }

    public static bool GhostIconButton(string id, FontAwesomeIcon icon, Vector4 color, string? tooltip = null, Vector2? size = null)
    {
        var buttonSize = size ?? new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, buttonSize);
        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();

        if (hovered)
        {
            drawList.AddRectFilled(pos, new Vector2(pos.X + buttonSize.X, pos.Y + buttonSize.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), ImGui.GetStyle().FrameRounding);
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var glyphSize = ImGui.CalcTextSize(glyph);
            drawList.AddText(
                new Vector2(pos.X + (buttonSize.X - glyphSize.X) * 0.5f, pos.Y + (buttonSize.Y - glyphSize.Y) * 0.5f),
                ImGui.GetColorU32(color), glyph);
        }

        if (hovered && tooltip != null)
            ImGui.SetTooltip(tooltip);

        return clicked;
    }

    public static bool IconButton(FontAwesomeIcon icon, string id, string? tooltip = null, float width = 0)
    {
        bool result;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            result = width > 0
                ? ImGui.Button($"{icon.ToIconString()}##{id}", new Vector2(width, 0))
                : ImGui.Button($"{icon.ToIconString()}##{id}");
        }

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return result;
    }

    public static bool IconTextButton(FontAwesomeIcon icon, string text, float width = 0)
    {
        string iconStr;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            iconStr = icon.ToIconString();
        }

        var label = $"{iconStr}  {text}";
        return width > 0
            ? ImGui.Button(label, new Vector2(width, 0))
            : ImGui.Button(label);
    }

    public static void CenteredText(string text, Vector4? color = null)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        var availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - textWidth) * 0.5f);

        if (color.HasValue)
            ImGui.TextColored(color.Value, text);
        else
            ImGui.Text(text);
    }

    public static void CenteredIcon(FontAwesomeIcon icon, Vector4? color = null)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconStr = icon.ToIconString();
            var iconWidth = ImGui.CalcTextSize(iconStr).X;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - iconWidth) * 0.5f);

            if (color.HasValue)
                ImGui.TextColored(color.Value, iconStr);
            else
                ImGui.Text(iconStr);
        }
    }

    public static bool EmptyState(FontAwesomeIcon icon, string message, string? buttonText = null)
    {
        var result = false;
        var availHeight = ImGui.GetContentRegionAvail().Y;

        ImGui.Dummy(new Vector2(0, availHeight * 0.2f));

        CenteredIcon(icon, Colors.TextMuted);
        ImGui.Spacing();
        CenteredText(message, Colors.TextMuted);

        if (buttonText != null)
        {
            ImGui.Spacing();
            ImGui.Spacing();

            var buttonWidth = ImGui.CalcTextSize(buttonText).X + 20;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - buttonWidth) * 0.5f);

            if (ImGui.Button(buttonText))
            {
                result = true;
            }
        }

        return result;
    }

    public static void StatusDot(bool active)
    {
        var color = active ? Colors.Active : Colors.Inactive;
        ImGui.TextColored(color, active ? "●" : "○");
    }

    public static void Badge(int count, Vector4? color = null)
    {
        var badgeColor = color ?? Colors.TextMuted;
        ImGui.SameLine();
        ImGui.TextColored(badgeColor, $"({count})");
    }

    public static ImRaii.TooltipDisposable Tooltip(string? header = null)
    {
        var tooltip = ImRaii.Tooltip();
        if (header != null)
        {
            ImGui.TextColored(Colors.Header, header);
            ImGui.Separator();
            ImGui.Spacing();
        }
        return tooltip;
    }

    public static void VerticalSpacing(float amount = Sizing.SpacingMedium)
    {
        ImGui.Dummy(new Vector2(0, amount));
    }

    public readonly struct TableStyleScope : IDisposable
    {
        public TableStyleScope()
        {
            ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(1f, 1f, 1f, 0.05f));
            ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(1f, 1f, 1f, 0.025f));
            ImGui.PushStyleColor(ImGuiCol.TableBorderLight, new Vector4(1f, 1f, 1f, 0.06f));
            ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, new Vector4(1f, 1f, 1f, 0.10f));
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,
                new Vector2(8f, 5f) * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
        }

        public void Dispose()
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(5);
        }
    }

    public static TableStyleScope PushTableStyle() => new();

    public static void TintedHeadersRow()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Header))
        {
            ImGui.TableHeadersRow();
        }
    }

    public static void CenteredTableText(string text, Vector4? color = null)
    {
        var offset = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f;
        if (offset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        if (color.HasValue)
            ImGui.TextColored(color.Value, text);
        else
            ImGui.Text(text);
    }

    public static void DropShadow(Vector2 pos, Vector2 size)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().ChildRounding;
        Span<(float Offset, float Alpha)> layers = stackalloc[] { (2f, 0.16f), (4f, 0.09f), (7f, 0.04f) };
        foreach (var (offset, alpha) in layers)
        {
            drawList.AddRectFilled(
                new Vector2(pos.X - offset * 0.5f, pos.Y - offset * 0.25f + 1.5f),
                new Vector2(pos.X + size.X + offset * 0.5f, pos.Y + size.Y + offset * 0.75f + 1.5f),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)),
                rounding + offset * 0.5f);
        }
    }

    public static void PanelGloss()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var max = new Vector2(min.X + size.X, min.Y + size.Y);
        var midY = min.Y + size.Y * 0.45f;

        var light = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.035f));
        var lightClear = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f));
        drawList.AddRectFilledMultiColor(
            new Vector2(min.X + 1, min.Y + 1), new Vector2(max.X - 1, midY),
            light, light, lightClear, lightClear);

        var shade = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.09f));
        var shadeClear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(
            new Vector2(min.X + 1, midY), new Vector2(max.X - 1, max.Y - 1),
            shadeClear, shadeClear, shade, shade);

        drawList.AddLine(
            new Vector2(min.X + 6, min.Y + 1), new Vector2(max.X - 6, min.Y + 1),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f)));
    }

    public static void InsetShade()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();

        var dark = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(
            new Vector2(min.X + 1, min.Y + 1),
            new Vector2(min.X + size.X - 1, min.Y + 8f),
            dark, dark, clear, clear);
    }

    /// <summary>
    /// Returns true if confirmed, false if cancelled, null if still open.
    /// </summary>
    public static bool? ConfirmationModal(string id, string title, string message, string confirmText = "Delete", string cancelText = "Cancel")
    {
        bool? result = null;

        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(300 * scale, 0));
        using (var popup = ImRaii.PopupModal($"{title}##{id}", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            if (popup)
            {
                ImGui.TextWrapped(message);
                VerticalSpacing(Sizing.SpacingLarge);

                var buttonWidth = 80f * scale;
                var spacing = 10f * scale;
                var totalWidth = buttonWidth * 2 + spacing;
                var startX = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1f)))
                {
                    if (ImGui.Button(confirmText, new Vector2(buttonWidth, 0)))
                    {
                        result = true;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine(0, spacing);

                if (ImGui.Button(cancelText, new Vector2(buttonWidth, 0)))
                {
                    result = false;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        return result;
    }

    public static void OpenConfirmationModal(string id, string title)
    {
        ImGui.OpenPopup($"{title}##{id}");
    }
}
