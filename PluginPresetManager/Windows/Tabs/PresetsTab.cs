using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using PluginPresetManager.Models;

namespace PluginPresetManager.Windows.Tabs;

public class PresetsTab
{
	private readonly Plugin plugin;
	private readonly Configuration config;
	private readonly PresetManager presetManager;

	private string newPresetName = string.Empty;
	private Preset? selectedPreset;
	private string searchFilter = string.Empty;
	private bool openAddPluginPopup = false;

	public PresetsTab(Plugin plugin, Configuration config, PresetManager presetManager)
	{
		this.plugin = plugin;
		this.config = config;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		if (ImGui.BeginChild("PresetList", new Vector2(170, 0), true))
		{
			ImGui.SetNextItemWidth(-1);
			ImGui.InputTextWithHint("##NewPreset", "New preset name...", ref newPresetName, 100);

			if (ImGui.Button("Create Empty", new Vector2(-1, 0)))
			{
				if (!string.IsNullOrWhiteSpace(newPresetName))
				{
					var newPreset = new Preset
					{
						Name = newPresetName,
						CreatedAt = DateTime.Now,
						LastModified = DateTime.Now
					};
					presetManager.AddPreset(newPreset);
					selectedPreset = newPreset;
					newPresetName = string.Empty;
				}
			}

			ImGui.Separator();
			ImGui.TextUnformatted("Presets:");
			ImGui.Separator();

			foreach (var preset in presetManager.GetAllPresets())
			{
				var isSelected = selectedPreset == preset;
				var isLastApplied = config.LastAppliedPresetId == preset.Id;
				var isGlobalDefault = config.DefaultPresetId == preset.Id;
				
				var characterDefaults = new List<string>();
				if (config.UseCharacterSpecificDefaults)
				{
					foreach (var kvp in config.CharacterDefaultPresets)
					{
						if (kvp.Value == preset.Id && config.CharacterNames.TryGetValue(kvp.Key, out var charName))
						{
							characterDefaults.Add(charName);
						}
					}
				}

				if (isLastApplied)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0.5f, 1));
				}

				var displayName = preset.Name;
				if (isGlobalDefault && !config.UseCharacterSpecificDefaults)
				{
					displayName = $"★ {preset.Name}";
				}
				else if (isGlobalDefault && config.UseCharacterSpecificDefaults)
				{
					displayName = $"☆ {preset.Name}";
				}
				
				if (characterDefaults.Count > 0 && config.UseCharacterSpecificDefaults)
				{
					displayName += $" [{characterDefaults.Count}]";
				}

				if (ImGui.Selectable($"{displayName}##{preset.Id}", isSelected))
				{
					selectedPreset = preset;
				}

				if (isLastApplied)
				{
					ImGui.PopStyleColor();
				}

				if (ImGui.IsItemHovered())
				{
					ImGui.BeginTooltip();
					ImGui.TextUnformatted($"Plugins: {preset.EnabledPlugins.Count}");
					ImGui.TextUnformatted($"Created: {preset.CreatedAt:g}");
					if (preset.LastModified != preset.CreatedAt)
					{
						ImGui.TextUnformatted($"Modified: {preset.LastModified:g}");
					}
					if (!string.IsNullOrEmpty(preset.Description))
					{
						ImGui.Separator();
						ImGui.TextWrapped(preset.Description);
					}
					if (isGlobalDefault)
					{
						ImGui.Separator();
						ImGui.TextColored(new Vector4(1, 1, 0, 1), config.UseCharacterSpecificDefaults ? 
							"☆ Global Default (Fallback)" : "★ Default (Auto-applies on login)");
					}
					if (characterDefaults.Count > 0)
					{
						ImGui.Separator();
						ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Character Defaults:");
						foreach (var charName in characterDefaults)
						{
							var isCurrentChar = Plugin.ClientState.LocalContentId != 0 && 
								config.CharacterNames.TryGetValue(Plugin.ClientState.LocalContentId, out var currentChar) && 
								currentChar == charName;
							if (isCurrentChar)
							{
								ImGui.TextColored(new Vector4(0, 1, 0, 1), $"• {charName} (current)");
							}
							else
							{
								ImGui.TextUnformatted($"• {charName}");
							}
						}
					}
					if (isLastApplied)
					{
						ImGui.Separator();
						ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), "Currently Applied");
					}
					ImGui.EndTooltip();
				}
			}

			ImGui.EndChild();
		}

		ImGui.SameLine();

		if (ImGui.BeginChild("PresetDetails"))
		{
			if (selectedPreset != null)
			{
				DrawPresetDetails(selectedPreset);
			}
			else
			{
				ImGui.TextUnformatted("Select a preset to view details");
				ImGui.Spacing();
				ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1),
					"Create a new preset using the button on the left,\nor select an existing preset from the list.");
			}

			ImGui.EndChild();
		}

		if (selectedPreset != null && openAddPluginPopup)
		{
			ImGui.OpenPopup($"AddPluginToPreset###{selectedPreset.Id}");
			openAddPluginPopup = false;
		}

		if (selectedPreset != null && ImGui.BeginPopup($"AddPluginToPreset###{selectedPreset.Id}"))
		{
			ImGui.TextUnformatted("Add plugins:");
			ImGui.InputTextWithHint("##AddPluginSearch", "Search...", ref searchFilter, 100);

			if (ImGui.BeginChild("AddPluginList", new Vector2(400, 300)))
			{
				var installedPlugins = Plugin.PluginInterface.InstalledPlugins
					.OrderBy(p => p.Name)
					.ToList();

				foreach (var plugin in installedPlugins)
				{
					if (selectedPreset.EnabledPlugins.Contains(plugin.InternalName))
						continue;

					if (presetManager.GetAlwaysOnPlugins().Contains(plugin.InternalName))
						continue;

					if (!string.IsNullOrEmpty(searchFilter) &&
						!plugin.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) &&
						!plugin.InternalName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (ImGui.Selectable($"{plugin.Name}##{plugin.InternalName}"))
					{
						selectedPreset.EnabledPlugins.Add(plugin.InternalName);
						presetManager.UpdatePreset(selectedPreset);
					}

					if (plugin.IsDev)
					{
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 0, 1, 1), "[DEV]");
					}
					if (plugin.IsThirdParty)
					{
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 1, 0, 1), "[3rd]");
					}
				}

				ImGui.EndChild();
			}

			ImGui.EndPopup();
		}
	}

	private void DrawPresetDetails(Preset preset)
	{
		var presetName = preset.Name;
		ImGui.SetNextItemWidth(-1);
		if (ImGui.InputText("##PresetName", ref presetName, 100))
		{
			preset.Name = presetName;
			presetManager.UpdatePreset(preset);
		}
		ImGui.Separator();

		var description = preset.Description;
		if (ImGui.InputTextMultiline("##Desc", ref description, 500, new Vector2(-1, 35)))
		{
			preset.Description = description;
			presetManager.UpdatePreset(preset);
		}
		if (string.IsNullOrEmpty(description) && !ImGui.IsItemActive() && !ImGui.IsItemFocused())
		{
			var min = ImGui.GetItemRectMin();
			var dl = ImGui.GetWindowDrawList();
			dl.AddText(new Vector2(min.X + 4, min.Y + 4), ImGui.GetColorU32(ImGuiCol.TextDisabled), "Description...");
		}

		var preview = presetManager.GetPresetPreview(preset);
		if (preview.ToEnable.Any() || preview.ToDisable.Any())
		{
			ImGui.TextColored(new Vector4(1, 1, 0, 1), "Changes:");
			ImGui.SameLine();
			if (preview.ToEnable.Any())
				ImGui.TextColored(new Vector4(0, 1, 0, 1), $"+{preview.ToEnable.Count}");
			ImGui.SameLine();
			if (preview.ToDisable.Any())
				ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"-{preview.ToDisable.Count}");
			if (preview.Missing.Any())
			{
				ImGui.SameLine();
				ImGui.TextColored(new Vector4(1, 0, 0, 1), $"{preview.Missing.Count} missing");
			}
		}
		else
		{
			ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ Applied");
		}

		var style = ImGui.GetStyle();
		var totalButtonsWidth = 80f + 100f + 70f + (2 * style.ItemSpacing.X);
		var right = ImGui.GetContentRegionMax().X;
		ImGui.SetCursorPosX(right - totalButtonsWidth);
		if (ImGui.Button("Apply", new Vector2(80, 0)))
		{
			_ = presetManager.ApplyPresetAsync(preset);
		}
		ImGui.SameLine();

		var isGlobalDefault = config.DefaultPresetId == preset.Id;
		var isCharacterDefault = false;
		if (config.UseCharacterSpecificDefaults && Plugin.ClientState.LocalContentId != 0)
		{
			config.CharacterDefaultPresets.TryGetValue(Plugin.ClientState.LocalContentId, out var charDefault);
			isCharacterDefault = charDefault == preset.Id;
		}
		
		var buttonText = "Set Default";
		var buttonColor = new Vector4(0.2f, 0.6f, 0.2f, 1);
		if (isGlobalDefault && !config.UseCharacterSpecificDefaults)
		{
			buttonText = "Default ✓";
		}
		else if (isGlobalDefault && config.UseCharacterSpecificDefaults)
		{
			buttonText = "Global ✓";
			buttonColor = new Vector4(0.2f, 0.5f, 0.6f, 1);
		}
		else if (isCharacterDefault)
		{
			buttonText = "Character ✓";
			buttonColor = new Vector4(0.5f, 0.2f, 0.6f, 1);
		}
		
		if (isGlobalDefault || isCharacterDefault)
		{
			ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
		}
		
		if (ImGui.Button(buttonText + "###SetDefault", new Vector2(100, 0)))
		{
			if (config.UseCharacterSpecificDefaults)
			{
				ImGui.OpenPopup("DefaultPresetOptions");
			}
			else
			{
				if (isGlobalDefault)
				{
					config.DefaultPresetId = null;
				}
				else
				{
					config.DefaultPresetId = preset.Id;
				}
				Plugin.PluginInterface.SavePluginConfig(config);
			}
		}
		
		if (isGlobalDefault || isCharacterDefault)
		{
			ImGui.PopStyleColor();
		}
		
		if (ImGui.IsItemHovered())
		{
			if (config.UseCharacterSpecificDefaults)
			{
				ImGui.SetTooltip("Click to set default options");
			}
			else
			{
				ImGui.SetTooltip(isGlobalDefault 
					? "Click to unset as default preset" 
					: "Set this preset to apply automatically when you log in");
			}
		}
		
		if (ImGui.BeginPopup("DefaultPresetOptions"))
		{
			ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Set Default For:");
			ImGui.Separator();
			
			if (ImGui.Selectable("Global Default", isGlobalDefault))
			{
				config.DefaultPresetId = isGlobalDefault ? null : preset.Id;
				Plugin.PluginInterface.SavePluginConfig(config);
				ImGui.CloseCurrentPopup();
			}
			if (ImGui.IsItemHovered())
			{
				ImGui.SetTooltip("Used as fallback for characters without specific defaults");
			}
			
			if (Plugin.ClientState.LocalContentId != 0)
			{
				var charName = config.CharacterNames.TryGetValue(Plugin.ClientState.LocalContentId, out var name) 
					? name : $"{Plugin.ClientState.LocalPlayer?.Name}@{Plugin.ClientState.LocalPlayer?.HomeWorld.Value.Name}";
				
				if (ImGui.Selectable($"{charName}", isCharacterDefault))
				{
					if (isCharacterDefault)
					{
						config.CharacterDefaultPresets.Remove(Plugin.ClientState.LocalContentId);
					}
					else
					{
						config.CharacterDefaultPresets[Plugin.ClientState.LocalContentId] = preset.Id;
					}
					Plugin.PluginInterface.SavePluginConfig(config);
					ImGui.CloseCurrentPopup();
				}
			}
			
			ImGui.EndPopup();
		}
		ImGui.SameLine();
		if (ImGui.Button("Delete", new Vector2(70, 0)))
		{
			ImGui.OpenPopup($"DeleteConfirm###{preset.Id}");
		}

		if (ImGui.Button("Add Enabled Plugins", new Vector2(150, 0)))
		{
			var alwaysOn = presetManager.GetAlwaysOnPlugins();
			var addedCount = 0;
			foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
			{
				if (plugin.IsLoaded &&
					!preset.EnabledPlugins.Contains(plugin.InternalName) &&
					!alwaysOn.Contains(plugin.InternalName))
				{
					preset.EnabledPlugins.Add(plugin.InternalName);
					addedCount++;
				}
			}
			if (addedCount > 0)
			{
				presetManager.UpdatePreset(preset);
			}
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Add all currently enabled plugins to this preset");
		}

		var trueValue = true;
		if (ImGui.BeginPopupModal($"DeleteConfirm###{preset.Id}", ref trueValue, ImGuiWindowFlags.AlwaysAutoResize))
		{
			ImGui.Text($"Are you sure you want to delete '{preset.Name}'?");
			ImGui.Spacing();

			if (ImGui.Button("Yes", new Vector2(120, 0)))
			{
				presetManager.DeletePreset(preset);
				selectedPreset = null;
				ImGui.CloseCurrentPopup();
			}

			ImGui.SameLine();

			if (ImGui.Button("No", new Vector2(120, 0)))
			{
				ImGui.CloseCurrentPopup();
			}

			ImGui.EndPopup();
		}

		ImGui.Separator();

		if (ImGui.Button($"Add Plugin##AddPlugin{preset.Id}", new Vector2(100, 0)))
		{
			searchFilter = string.Empty;
			openAddPluginPopup = true;
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Add individual plugins to this preset");
		}
		ImGui.SameLine();
		ImGui.Text($"Plugins: {preset.EnabledPlugins.Count}");

		if (ImGui.BeginChild("PresetPlugins", new Vector2(0, 0), true))
		{
			var installedPlugins = Plugin.PluginInterface.InstalledPlugins
				.GroupBy(p => p.InternalName)
				.ToDictionary(g => g.Key, g => g.First());
			var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();

			if (alwaysOnPlugins.Any())
			{
				ImGui.TextColored(new Vector4(0.5f, 0.5f, 1, 1), $"Always-On ({alwaysOnPlugins.Count}):");
				foreach (var pluginName in alwaysOnPlugins.OrderBy(p => installedPlugins.ContainsKey(p) ? installedPlugins[p].Name : p))
				{
					if (installedPlugins.TryGetValue(pluginName, out var plugin))
					{
						var color = plugin.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
						ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
						ImGui.SameLine();
						ImGui.TextUnformatted(plugin.Name);
					}
					else
					{
						ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 0, 0, 1), "(missing)");
					}
				}
				ImGui.Separator();
			}

			if (preset.EnabledPlugins.Any())
			{
				ImGui.TextColored(new Vector4(1, 1, 1, 1), $"Selected ({preset.EnabledPlugins.Count}):");
				if (ImGui.BeginTable("PresetSelectedTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
				{
					ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
					foreach (var pluginName in preset.EnabledPlugins.OrderBy(x => x))
					{
						var isInstalled = installedPlugins.TryGetValue(pluginName, out var plugin);
						ImGui.TableNextRow();
						ImGui.TableNextColumn();
						if (isInstalled)
						{
							var color = plugin!.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
							ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
							ImGui.SameLine();
							ImGui.TextUnformatted(plugin.Name);
						}
						else
						{
							ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
							ImGui.SameLine();
							ImGui.TextColored(new Vector4(1, 0, 0, 1), "(missing)");
						}
						ImGui.TableNextColumn();
						if (ImGui.SmallButton($"Remove##{pluginName}"))
						{
							preset.EnabledPlugins.Remove(pluginName);
							presetManager.UpdatePreset(preset);
						}
					}
					ImGui.EndTable();
				}
			}
			else
			{
				ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "No plugins. Click 'Add' to add.");
			}
			
			if (config.UseCharacterSpecificDefaults)
			{
				ImGui.Separator();
				ImGui.Spacing();
				
				var assignedCharacters = new List<(ulong id, string name)>();
				foreach (var kvp in config.CharacterDefaultPresets)
				{
					if (kvp.Value == preset.Id && config.CharacterNames.TryGetValue(kvp.Key, out var charName))
					{
						assignedCharacters.Add((kvp.Key, charName));
					}
				}
				
				if (assignedCharacters.Count > 0)
				{
					ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), $"Character Defaults ({assignedCharacters.Count}):");
					foreach (var character in assignedCharacters.OrderBy(x => x.name))
					{
						var isCurrentChar = Plugin.ClientState.LocalContentId == character.id;
						if (isCurrentChar)
						{
							ImGui.TextColored(new Vector4(0, 1, 0, 1), $"• {character.name} (current)");
						}
						else
						{
							ImGui.TextUnformatted($"• {character.name}");
						}
					}
				}
				else
				{
					ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No characters assigned");
				}
			}

			ImGui.EndChild();
		}
	}
}


