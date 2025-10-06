using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows.Tabs;

public class CharactersTab
{
    private readonly Plugin plugin;
    private readonly Configuration config;
    private readonly PresetManager presetManager;

    public CharactersTab(Plugin plugin, Configuration config, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.config = config;
        this.presetManager = presetManager;
    }

    public void Draw()
    {
        // Header with enable toggle
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Multi-Character Preset Management");
        ImGui.Spacing();
        
        var enabled = config.UseCharacterSpecificDefaults;
        if (ImGui.Checkbox("Enable Character-Specific Presets", ref enabled))
        {
            config.UseCharacterSpecificDefaults = enabled;
            Plugin.PluginInterface.SavePluginConfig(config);
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, each character can have their own preset that applies on login.\n" +
                           "When disabled, all characters use the global default preset.");
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        if (!config.UseCharacterSpecificDefaults)
        {
            // Show global default when character-specific is disabled
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.5f, 1), "Global Default Preset (All Characters):");
            ImGui.Spacing();
            
            var allPresets = presetManager.GetAllPresets();
            var currentGlobalDefault = allPresets.FirstOrDefault(p => p.Id == config.DefaultPresetId);
            
            DrawPresetSelector("##GlobalDefault", currentGlobalDefault, (preset) =>
            {
                config.DefaultPresetId = preset?.Id;
                Plugin.PluginInterface.SavePluginConfig(config);
            });
            
            if (currentGlobalDefault != null)
            {
                ImGui.SameLine();
                if (ImGui.Button("Apply Now##GlobalApply"))
                {
                    _ = presetManager.ApplyPresetAsync(currentGlobalDefault);
                }
            }
            
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), 
                "This preset will automatically apply when any character logs in.");
        }
        else
        {
            // Character-specific mode
            DrawCharacterList();
            
            ImGui.Separator();
            ImGui.Spacing();
            
            // Global fallback
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Fallback Preset (For New Characters):");
            var allPresets = presetManager.GetAllPresets();
            var currentGlobalDefault = allPresets.FirstOrDefault(p => p.Id == config.DefaultPresetId);
            
            DrawPresetSelector("##GlobalFallback", currentGlobalDefault, (preset) =>
            {
                config.DefaultPresetId = preset?.Id;
                Plugin.PluginInterface.SavePluginConfig(config);
            });
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This preset will be used for characters without a specific preset assigned.");
            }
        }
    }
    
    private void DrawCharacterList()
    {
        if (config.CharacterNames.Count == 0)
        {
            ImGui.TextColored(new Vector4(1f, 1f, 0.5f, 1), "No characters tracked yet");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "Log in with a character to start tracking.");
            return;
        }
        
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.5f, 1), "Character Presets:");
        ImGui.Spacing();
        
        var allPresets = presetManager.GetAllPresets();
        var presetLookup = allPresets.ToDictionary(p => p.Id, p => p);
        
        if (ImGui.BeginTable("CharacterPresets", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Preset", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();
            
            foreach (var kvp in config.CharacterNames.OrderBy(x => x.Value))
            {
                var characterId = kvp.Key;
                var characterName = kvp.Value;
                var isCurrentChar = Plugin.ClientState.LocalContentId == characterId;
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                
                // Character name with online indicator
                if (isCurrentChar)
                {
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "● ");
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.8f, 1f, 0.8f, 1), characterName);
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "○ ");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(characterName);
                }
                
                ImGui.TableNextColumn();
                
                // Preset selector
                Models.Preset? currentPreset = null;
                if (config.CharacterDefaultPresets.TryGetValue(characterId, out var presetId) && 
                    presetId.HasValue && 
                    presetLookup.TryGetValue(presetId.Value, out var preset))
                {
                    currentPreset = preset;
                }
                
                ImGui.SetNextItemWidth(-1);
                DrawPresetSelector($"##CharPreset{characterId}", currentPreset, (selectedPreset) =>
                {
                    if (selectedPreset == null)
                    {
                        config.CharacterDefaultPresets.Remove(characterId);
                    }
                    else
                    {
                        config.CharacterDefaultPresets[characterId] = selectedPreset.Id;
                    }
                    Plugin.PluginInterface.SavePluginConfig(config);
                });
                
                ImGui.TableNextColumn();
                
                // Status
                if (isCurrentChar && config.LastAppliedPresetId.HasValue && currentPreset?.Id == config.LastAppliedPresetId.Value)
                {
                    ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), "Applied");
                }
                else if (isCurrentChar)
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "Online");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Offline");
                }
                
                ImGui.TableNextColumn();
                
                // Actions
                if (isCurrentChar && currentPreset != null)
                {
                    if (ImGui.SmallButton($"Apply##Apply{characterId}"))
                    {
                        _ = presetManager.ApplyPresetAsync(currentPreset);
                    }
                    ImGui.SameLine();
                }
                
                if (ImGui.SmallButton($"Delete##Delete{characterId}"))
                {
                    config.CharacterDefaultPresets.Remove(characterId);
                    config.CharacterNames.Remove(characterId);
                    Plugin.PluginInterface.SavePluginConfig(config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Delete this character from tracking");
                }
            }
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), 
            "● = Currently logged in | Presets apply automatically on character login");
    }
    
    private void DrawPresetSelector(string id, Models.Preset? currentPreset, Action<Models.Preset?> onSelect)
    {
        var previewText = currentPreset?.Name ?? "(none)";
        var allPresets = presetManager.GetAllPresets();
        
        if (ImGui.BeginCombo(id, previewText))
        {
            // None option
            if (ImGui.Selectable("(none)", currentPreset == null))
            {
                onSelect(null);
            }
            
            ImGui.Separator();
            
            // All presets
            foreach (var preset in allPresets.OrderBy(p => p.Name))
            {
                var isSelected = currentPreset?.Id == preset.Id;
                if (ImGui.Selectable($"{preset.Name}##{preset.Id}", isSelected))
                {
                    onSelect(preset);
                }
                
                if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(preset.Description))
                {
                    ImGui.SetTooltip(preset.Description);
                }
            }
            
            ImGui.EndCombo();
        }
    }
}
