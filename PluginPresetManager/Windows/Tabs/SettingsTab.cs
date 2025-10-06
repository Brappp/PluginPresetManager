using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows.Tabs;

public class SettingsTab
{
	private readonly Configuration config;
	private readonly PresetManager presetManager;

	public SettingsTab(Configuration config, PresetManager presetManager)
	{
		this.config = config;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Settings");
		
		ImGui.Text("Notifications:");
		var currentMode = (int)config.NotificationMode;
		if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
		{
			config.NotificationMode = (NotificationMode)currentMode;
			Plugin.PluginInterface.SavePluginConfig(config);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("How to display notifications\nToast: Non-intrusive popup notifications\nChat: Messages in chat window\nNone: No notifications");
		}
		
		ImGui.Separator();
		
		var useCharacterDefaults = config.UseCharacterSpecificDefaults;
		if (ImGui.Checkbox("Enable Character-Specific Defaults", ref useCharacterDefaults))
		{
			config.UseCharacterSpecificDefaults = useCharacterDefaults;
			Plugin.PluginInterface.SavePluginConfig(config);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("When enabled, each character can have their own default preset.\nWhen disabled, all characters use the global default preset.");
		}
		
		if (config.UseCharacterSpecificDefaults)
		{
			ImGui.Indent();
			ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "How to set character defaults:");
			ImGui.BulletText("Go to the Presets tab");
			ImGui.BulletText("Select a preset");
			ImGui.BulletText("Click 'Set Default' button");
			ImGui.BulletText("Choose between Global or Character default");
			
			if (config.CharacterNames.Count > 0)
			{
				ImGui.Spacing();
				ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"Tracked Characters: {config.CharacterNames.Count}");
			}
			else
			{
				ImGui.Spacing();
				ImGui.TextColored(new Vector4(1f, 1f, 0.5f, 1), "No characters tracked yet - log in to track a character");
			}
			ImGui.Unindent();
		}
	}
}


