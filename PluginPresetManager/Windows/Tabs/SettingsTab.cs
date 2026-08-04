using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class SettingsTab
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;
    private CharacterData? characterToDelete = null;

    public SettingsTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterData Data => presetManager.CurrentData;
    private Configuration GlobalConfig => plugin.Configuration;

    private static float Scale => ImGuiHelpers.GlobalScale;

    public void Draw()
    {
        if (presetManager.HasCharacter)
        {
            DrawCharacterSection();
            UIHelpers.VerticalSpacing(Sizing.SpacingLarge);
        }

        DrawGlobalSection();
        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);
        DrawCharacterDataSection();
        DrawDeleteConfirmation();
    }

    private void DrawCharacterSection()
    {
        UIHelpers.SectionHeader($"This Character — {Data.DisplayName}", FontAwesomeIcon.User);

        var labelWidth = 130 * Scale;

        ImGui.Text("Notifications");
        ImGui.SameLine(labelWidth);
        ImGui.SetNextItemWidth(Sizing.InputMedium * Scale);
        var currentMode = (int)Data.NotificationMode;
        if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
        {
            Data.NotificationMode = (NotificationMode)currentMode;
            plugin.CharacterStorage.Save(Data);
        }

        ImGui.Text("On login");
        ImGui.SameLine(labelWidth);
        if (presetManager.UseAlwaysOnAsDefault)
        {
            ImGui.TextColored(Colors.Star, "★ Always-On Only");
        }
        else if (!string.IsNullOrEmpty(presetManager.DefaultPreset))
        {
            ImGui.TextColored(Colors.Star, $"★ {presetManager.DefaultPreset}");
        }
        else
        {
            ImGui.TextColored(Colors.TextMuted, "Nothing — no default set");
        }
        ImGui.SameLine();
        ImGui.TextColored(Colors.TextDisabled, "· set with the ★ button on a preset");
        if (ImGui.IsItemHovered() || ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("The starred default is applied automatically when this character logs in.\nStar a preset (or Always-On) in its editor to change it.");
        }
    }

    private void DrawGlobalSection()
    {
        UIHelpers.SectionHeader("All Characters", FontAwesomeIcon.Globe);

        var showDtrBar = GlobalConfig.ShowDtrBar;
        if (ImGui.Checkbox("Show preset selector in Server Info Bar", ref showDtrBar))
        {
            GlobalConfig.ShowDtrBar = showDtrBar;
            plugin.SaveConfiguration();
            plugin.UpdateDtrBarVisibility();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Adds a clickable entry to quickly switch presets.");
        }

        var useExperimental = GlobalConfig.UseExperimentalPersistence;
        if (ImGui.Checkbox("Use internal APIs for all plugins", ref useExperimental))
        {
            GlobalConfig.UseExperimentalPersistence = useExperimental;
            plugin.SaveConfiguration();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Toggle all plugins through Dalamud's internal APIs instead of chat commands.\n" +
                "Dev plugins always use this path so the exact copy is targeted.\n" +
                "Uses internal Dalamud APIs - may break on updates.");
        }
    }

    private void DrawCharacterDataSection()
    {
        var characters = presetManager.GetAllCharacters();
        UIHelpers.SectionHeader("Character Data", FontAwesomeIcon.Users);
        ImGui.TextColored(Colors.TextMuted, $"{characters.Count} character(s) stored. Delete unused character data to clean up.");
        ImGui.Spacing();

        using var tableStyle = UIHelpers.PushTableStyle();
        using var table = ImRaii.Table("##CharacterData", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Presets", ImGuiTableColumnFlags.WidthFixed, 58 * scale);
        ImGui.TableSetupColumn("Always-On", ImGuiTableColumnFlags.WidthFixed, 74 * scale);
        ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, 72 * scale);
        UIHelpers.TintedHeadersRow();

        foreach (var character in characters.OrderBy(c => c.Name))
        {
            var isLoggedIn = character.ContentId == plugin.ActiveContentId;
            var isViewing = character.ContentId == presetManager.CurrentCharacterId;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(isLoggedIn ? Colors.Active : Colors.TextNormal, character.Name);
            if (!string.IsNullOrEmpty(character.World))
            {
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextMuted, $"@ {character.World}");
            }
            if (isViewing && !isLoggedIn)
            {
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextMuted, "(viewing)");
            }

            ImGui.TableNextColumn();
            UIHelpers.CenteredTableText(character.Presets.Count.ToString(), Colors.TextMuted);

            ImGui.TableNextColumn();
            UIHelpers.CenteredTableText(character.AlwaysOn.Count.ToString(), Colors.TextMuted);

            ImGui.TableNextColumn();
            if (isLoggedIn)
            {
                UIHelpers.CenteredTableText("logged in", Colors.Active);
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.3f, 0.3f, 1f)))
                {
                    if (ImGui.SmallButton($"Delete##{character.ContentId}"))
                    {
                        characterToDelete = character;
                        UIHelpers.OpenConfirmationModal("DeleteCharacter", "Delete Character Data");
                    }
                }
            }
        }
    }

    private void DrawDeleteConfirmation()
    {
        if (characterToDelete == null)
            return;

        var result = UIHelpers.ConfirmationModal(
            "DeleteCharacter",
            "Delete Character Data",
            $"Delete all data for '{characterToDelete.DisplayName}'?\n\n" +
            $"This will remove {characterToDelete.Presets.Count} preset(s) and cannot be undone.");

        if (result == true)
        {
            var deletedId = characterToDelete.ContentId;
            presetManager.DeleteCharacter(deletedId);

            if (presetManager.CurrentCharacterId == deletedId)
            {
                if (plugin.ActiveContentId != 0)
                    presetManager.SwitchCharacter(plugin.ActiveContentId);
                else
                    presetManager.ClearCharacter();
            }
            characterToDelete = null;
        }
        else if (result == false || !ImGui.IsPopupOpen("Delete Character Data##DeleteCharacter"))
        {
            characterToDelete = null;
        }
    }
}
