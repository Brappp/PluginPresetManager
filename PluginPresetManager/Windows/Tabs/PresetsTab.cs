using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Newtonsoft.Json;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class PresetsTab
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;

    private Preset? selectedPreset;
    private bool isSelectedPresetShared;
    private bool showAlwaysOn;

    private string presetSearchFilter = string.Empty;
    private string alwaysOnSearchFilter = string.Empty;

    private Preset? renameTarget;
    private string renameBuffer = string.Empty;
    private string? renameError;

    private string importError = string.Empty;
    private bool showImportFromCharacter;
    private ulong importSourceCharacterId;

    private Preset? presetToDelete;
    private bool openDeleteModal;

    private Dictionary<string, IExposedPlugin>? cachedPlugins;
    private int lastPluginCount = -1;
    private DateTime lastPluginCacheRefresh = DateTime.MinValue;

    public PresetsTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterData Data => presetManager.CurrentData;

    private static float Scale => ImGuiHelpers.GlobalScale;

    private Dictionary<string, IExposedPlugin> GetInstalledPlugins()
    {
        var currentCount = Plugin.PluginInterface.InstalledPlugins.Count();
        if (cachedPlugins == null
            || lastPluginCount != currentCount
            || (DateTime.UtcNow - lastPluginCacheRefresh).TotalSeconds > 3)
        {
            cachedPlugins = PluginKey.BuildInstalledDictionary(Plugin.PluginInterface.InstalledPlugins);
            lastPluginCount = currentCount;
            lastPluginCacheRefresh = DateTime.UtcNow;
        }
        return cachedPlugins;
    }

    public void Draw()
    {
        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.Warning, "Please log in to a character to use presets.");
            return;
        }

        DrawCharacterBar();
        ImGui.Separator();
        ImGui.Spacing();

        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();

        using (ImRaii.Disabled(presetManager.IsApplying))
        using (var left = ImRaii.Child("LeftPanel", new Vector2(230 * Scale, 0), false))
        {
            if (left)
            {
                DrawToolbar();
                ImGui.Spacing();
                using var list = ImRaii.Child("PresetList", new Vector2(0, 0), true);
                if (list)
                    DrawPresetList();
            }
        }

        ImGui.SameLine();

        using (var right = ImRaii.Child("RightPanel", new Vector2(0, 0), true))
        {
            if (right)
            {
                if (presetManager.IsApplying)
                    DrawApplyingPanel();
                else if (showImportFromCharacter)
                    DrawImportFromCharacter();
                else if (showAlwaysOn)
                    DrawAlwaysOnEditor();
                else if (selectedPreset != null)
                    DrawPresetEditor(selectedPreset, isSelectedPresetShared, effectiveAlwaysOn);
                else
                    UIHelpers.EmptyState(FontAwesomeIcon.MousePointer, "Select a preset to edit");
            }
        }

        DrawDeleteConfirmation();
    }

    private void DrawCharacterBar()
    {
        var characters = presetManager.GetAllCharacters();
        var currentId = presetManager.CurrentCharacterId;

        if (characters.Count <= 1)
        {
            ImGui.Text($"Character: {Data.DisplayName}");
        }
        else
        {
            ImGui.Text("Character:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200 * Scale);

            var current = characters.FirstOrDefault(c => c.ContentId == currentId) ?? characters[0];
            using (ImRaii.Disabled(presetManager.IsApplying))
            using (var combo = ImRaii.Combo("##CharSelect", current.DisplayName))
            {
                if (combo)
                {
                    foreach (var character in characters)
                    {
                        var isSelected = character.ContentId == currentId;
                        var label = character.DisplayName;
                        if (character.ContentId == Plugin.PlayerState.ContentId)
                            label += " (you)";

                        if (ImGui.Selectable(label, isSelected) && character.ContentId != currentId)
                        {
                            presetManager.SwitchCharacter(character.ContentId);
                            plugin.SaveConfiguration();
                            selectedPreset = null;
                            renameTarget = null;
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                }
            }
        }

        string activeText;
        Vector4 activeColor;
        if (presetManager.IsApplying)
        {
            activeText = "Applying...";
            activeColor = Colors.Warning;
        }
        else if (presetManager.WasLastAppliedAlwaysOn)
        {
            activeText = "Active: Always-On Only";
            activeColor = Colors.Success;
        }
        else if (presetManager.GetLastAppliedPreset() is { } lastApplied)
        {
            activeText = $"Active: {lastApplied.Name}";
            activeColor = Colors.Success;
        }
        else
        {
            activeText = "No preset active";
            activeColor = Colors.TextMuted;
        }

        var textWidth = ImGui.CalcTextSize(activeText).X;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX() + 12 * Scale, ImGui.GetContentRegionMax().X - textWidth));
        ImGui.TextColored(activeColor, activeText);
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("+ New"))
        {
            var preset = new Preset
            {
                Name = "New Preset",
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };
            presetManager.AddPreset(preset);
            SelectPreset(preset, false);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Create an empty preset");

        ImGui.SameLine();
        if (ImGui.Button("Save Current"))
        {
            var preset = presetManager.CreatePresetFromCurrent("Current Plugins");
            presetManager.AddPreset(preset);
            SelectPreset(preset, false);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Create a preset from the currently enabled plugins");

        ImGui.SameLine();
        if (ImGui.Button("Import"))
            ImGui.OpenPopup("ImportMenu");

        using (var popup = ImRaii.Popup("ImportMenu"))
        {
            if (popup)
            {
                if (ImGui.MenuItem("From Clipboard"))
                    ImportPresetFromClipboard();

                if (ImGui.MenuItem("From Character..."))
                {
                    showImportFromCharacter = true;
                    importSourceCharacterId = 0;
                }
            }
        }

        if (!string.IsNullOrEmpty(importError))
            ImGui.TextColored(Colors.Error, importError);
    }

    private void SelectPreset(Preset preset, bool isShared)
    {
        selectedPreset = preset;
        isSelectedPresetShared = isShared;
        showAlwaysOn = false;
        showImportFromCharacter = false;
        renameTarget = null;
        renameError = null;
    }

    private void DrawPresetList()
    {
        var lastApplied = presetManager.GetLastAppliedPreset();
        var isAlwaysOnActive = presetManager.WasLastAppliedAlwaysOn;
        var totalAlwaysOn = presetManager.GetAlwaysOnPlugins().Count + presetManager.GetSharedAlwaysOnPlugins().Count;

        UIHelpers.StatusDot(isAlwaysOnActive);
        ImGui.SameLine();
        if (presetManager.UseAlwaysOnAsDefault)
        {
            DrawStar();
            ImGui.SameLine();
        }
        var aoClicked = DrawListRow($"Always-On ({totalAlwaysOn})##alwayson", showAlwaysOn,
            () =>
            {
                if (ImGui.IsItemHovered())
                {
                    UIHelpers.BeginTooltip("Always-On");
                    ImGui.Text("Plugins kept enabled by every preset.");
                    ImGui.TextColored(Colors.TextDisabled, "Click to edit, play button disables everything else");
                    UIHelpers.EndTooltip();
                }
            },
            () => _ = presetManager.ApplyAlwaysOnOnlyAsync());
        if (aoClicked)
        {
            showAlwaysOn = true;
            showImportFromCharacter = false;
            selectedPreset = null;
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);
        ImGui.Separator();
        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        var characterPresets = presetManager.GetAllPresets().ToList();
        UIHelpers.SectionHeader($"Presets ({characterPresets.Count})", FontAwesomeIcon.LayerGroup);
        foreach (var preset in characterPresets)
            DrawPresetRow(preset, false, lastApplied, isAlwaysOnActive);

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        var sharedPresets = presetManager.GetSharedPresets().ToList();
        UIHelpers.SectionHeader($"Shared ({sharedPresets.Count})", FontAwesomeIcon.Globe);
        foreach (var preset in sharedPresets)
            DrawPresetRow(preset, true, lastApplied, isAlwaysOnActive);
    }

    private void DrawPresetRow(Preset preset, bool isShared, Preset? lastApplied, bool isAlwaysOnActive)
    {
        var isActive = !isAlwaysOnActive && lastApplied == preset;
        var isSelected = selectedPreset == preset;
        var isDefault = Data.DefaultPreset == preset.Name;

        UIHelpers.StatusDot(isActive);
        ImGui.SameLine();
        if (isDefault)
        {
            DrawStar();
            ImGui.SameLine();
        }

        var suffix = isShared ? "shared" : "char";
        var clicked = DrawListRow($"{preset.Name}##{suffix}_{preset.Name}", isSelected,
            () =>
            {
                if (ImGui.IsItemHovered())
                    DrawPresetRowTooltip(preset, isShared, isDefault);
                DrawPresetContextMenu(preset, isShared);
            },
            () => _ = presetManager.ApplyPresetAsync(preset));
        if (clicked)
            SelectPreset(preset, isShared);
    }

    private bool DrawListRow(string label, bool isSelected, Action afterSelectable, Action onApply)
    {
        var applyWidth = ImGui.GetFrameHeight() + 6 * Scale;
        var clicked = ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.None,
            new Vector2(ImGui.GetContentRegionAvail().X - applyWidth, 0));
        afterSelectable();

        ImGui.SameLine(ImGui.GetContentRegionMax().X - applyWidth + 4 * Scale);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Success))
        {
            if (ImGui.SmallButton($"{FontAwesomeIcon.Play.ToIconString()}##apply_{label}"))
            {
                if (!presetManager.IsApplying)
                    onApply();
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Apply");

        return clicked;
    }

    private void DrawPresetRowTooltip(Preset preset, bool isShared, bool isDefault)
    {
        var installedPlugins = GetInstalledPlugins();
        var alwaysOnCount = presetManager.GetAlwaysOnPlugins().Count + presetManager.GetSharedAlwaysOnPlugins().Count;
        var missingCount = preset.Plugins.Count(p => !installedPlugins.ContainsKey(p));

        UIHelpers.BeginTooltip(isShared ? $"{preset.Name} (Shared)" : preset.Name);

        if (isDefault)
        {
            ImGui.TextColored(Colors.Star, presetManager.ApplyDefaultOnLogin ? "★ Default (applies on login)" : "★ Default");
            ImGui.Spacing();
        }

        ImGui.Text($"{preset.Plugins.Count} preset + {alwaysOnCount} always-on plugins");
        if (missingCount > 0)
            ImGui.TextColored(Colors.Warning, $"{missingCount} missing");

        if (!string.IsNullOrEmpty(preset.Description))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(preset.Description);
        }

        ImGui.Spacing();
        ImGui.TextColored(Colors.TextDisabled, "Click to edit, play button to apply");
        UIHelpers.EndTooltip();
    }

    private static void DrawStar()
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
        }
    }

    private void DrawPresetContextMenu(Preset preset, bool isShared)
    {
        var suffix = isShared ? "_shared" : "";
        using var ctx = ImRaii.ContextPopupItem($"PresetCtx_{preset.Name}{suffix}");
        if (!ctx) return;

        if (ImGui.MenuItem("Apply"))
        {
            if (!presetManager.IsApplying)
                _ = presetManager.ApplyPresetAsync(preset);
        }
        ImGui.Separator();

        if (ImGui.MenuItem("Duplicate"))
        {
            if (isShared)
                presetManager.CopySharedPresetToCharacter(preset);
            else
                SelectPreset(presetManager.DuplicatePreset(preset), false);
        }
        if (ImGui.MenuItem("Export to Clipboard"))
        {
            ExportPresetToClipboard(preset);
        }
        ImGui.Separator();

        if (isShared)
        {
            if (ImGui.MenuItem("Copy to Character"))
                presetManager.CopySharedPresetToCharacter(preset);
        }
        else
        {
            if (ImGui.MenuItem("Move to Shared"))
            {
                presetManager.MovePresetToShared(preset);
                if (selectedPreset == preset)
                    isSelectedPresetShared = true;
            }
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete"))
        {
            presetToDelete = preset;
            openDeleteModal = true;
        }
    }

    private void DrawApplyingPanel()
    {
        var availHeight = ImGui.GetContentRegionAvail().Y;
        ImGui.Dummy(new Vector2(0, availHeight * 0.3f));

        UIHelpers.CenteredText(presetManager.ApplyingStatus, Colors.Warning);
        ImGui.Spacing();

        var barWidth = ImGui.GetContentRegionAvail().X * 0.8f;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - barWidth) * 0.5f + ImGui.GetCursorPosX());
        ImGui.ProgressBar(presetManager.ApplyingProgress, new Vector2(barWidth, 18 * Scale),
            $"{(int)(presetManager.ApplyingProgress * 100)}%");

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        var cancelWidth = Sizing.ButtonMedium * Scale;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - cancelWidth) * 0.5f + ImGui.GetCursorPosX());
        if (ImGui.Button("Cancel", new Vector2(cancelWidth, 0)))
            presetManager.CancelApply();
    }

    private void DrawPresetEditor(Preset preset, bool isShared, HashSet<string> effectiveAlwaysOn)
    {
        if (!ReferenceEquals(renameTarget, preset))
        {
            renameTarget = preset;
            renameBuffer = preset.Name;
            renameError = null;
        }

        var applyWidth = Sizing.ButtonMedium * Scale;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - applyWidth - 8 * Scale);
        if (ImGui.InputText("##PresetName", ref renameBuffer, 100))
            TryRename(preset, isShared);

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonPrimary))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Colors.ButtonPrimaryHover))
        {
            if (ImGui.Button("Apply", new Vector2(applyWidth, 0)))
            {
                if (!presetManager.IsApplying)
                    _ = presetManager.ApplyPresetAsync(preset);
            }
        }

        if (renameError != null)
            ImGui.TextColored(Colors.Error, renameError);

        if (isShared)
            ImGui.TextColored(Colors.TextMuted, "Shared preset - available to all characters");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        var desc = preset.Description;
        if (ImGui.InputTextMultiline("##PresetDesc", ref desc, 500, new Vector2(-1, 40 * Scale)))
        {
            preset.Description = desc;
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }

        ImGui.Spacing();
        DrawPresetActions(preset, isShared);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPresetPluginList(preset, isShared, effectiveAlwaysOn);
    }

    private void TryRename(Preset preset, bool isShared)
    {
        var newName = renameBuffer.Trim();
        if (newName == preset.Name)
        {
            renameError = null;
            return;
        }

        if (newName.Length == 0)
        {
            renameError = "Name cannot be empty";
            return;
        }

        if (presetManager.IsPresetNameTaken(newName, preset))
        {
            renameError = "Name already in use";
            return;
        }

        presetManager.RenamePreset(preset, newName, isShared);
        renameError = null;
    }

    private void DrawPresetActions(Preset preset, bool isShared)
    {
        var isDefault = Data.DefaultPreset == preset.Name;

        if (isDefault)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonDefault))
            {
                if (ImGui.Button("★ Default"))
                    presetManager.SetDefaultPreset(null);
            }
        }
        else
        {
            if (ImGui.Button("Set Default"))
                presetManager.SetDefaultPreset(preset.Name);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(isDefault ? "Click to unset as default" : "Make this the default preset");

        ImGui.SameLine();
        var applyOnLogin = presetManager.ApplyDefaultOnLogin;
        if (ImGui.Checkbox("Apply on login", ref applyOnLogin))
            presetManager.SetApplyDefaultOnLogin(applyOnLogin);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatically apply the ★ default when logging in");

        ImGui.SameLine();
        if (ImGui.Button("Duplicate"))
        {
            if (isShared)
                presetManager.CopySharedPresetToCharacter(preset);
            else
                SelectPreset(presetManager.DuplicatePreset(preset), false);
        }

        ImGui.SameLine();
        if (ImGui.Button("Export"))
            ExportPresetToClipboard(preset);

        ImGui.SameLine();
        if (isShared)
        {
            if (ImGui.Button("Copy to Character"))
                presetManager.CopySharedPresetToCharacter(preset);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy this preset to the current character");
        }
        else
        {
            if (ImGui.Button("Move to Shared"))
            {
                presetManager.MovePresetToShared(preset);
                isSelectedPresetShared = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Make this preset available to all characters");
        }
    }

    private void DrawPresetPluginList(Preset preset, bool isShared, HashSet<string> effectiveAlwaysOn)
    {
        var installedPlugins = GetInstalledPlugins();

        var missingPlugins = preset.Plugins
            .Where(p => !installedPlugins.ContainsKey(p))
            .ToList();

        if (missingPlugins.Count > 0)
        {
            ImGui.TextColored(Colors.Warning, $"Missing plugins ({missingPlugins.Count})");
            foreach (var key in missingPlugins)
                ImGui.TextColored(Colors.Error, $"  • {PluginKey.GetDisplayName(key)}");

            if (ImGui.SmallButton("Remove missing from preset"))
            {
                preset.Plugins.ExceptWith(missingPlugins);
                if (isShared)
                    presetManager.UpdateSharedPreset(preset);
                else
                    presetManager.UpdatePreset(preset);
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        var candidates = installedPlugins
            .Where(kv => !effectiveAlwaysOn.Contains(kv.Key))
            .OrderBy(kv => kv.Value.Name)
            .ThenBy(kv => kv.Key)
            .ToList();
        var selectedCount = candidates.Count(kv => preset.Plugins.Contains(kv.Key));

        ImGui.SetNextItemWidth(Sizing.InputMedium * Scale);
        ImGui.InputTextWithHint("##PluginSearch", "Search...", ref presetSearchFilter, 100);

        ImGui.SameLine();
        if (ImGui.Button("Add Current"))
            AddCurrentlyEnabledPlugins(preset, isShared);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add all currently enabled plugins to this preset");

        var countText = $"{selectedCount} of {candidates.Count} selected";
        var countWidth = ImGui.CalcTextSize(countText).X;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - countWidth));
        ImGui.TextColored(Colors.TextMuted, countText);

        ImGui.Spacing();

        using (var child = ImRaii.Child("PluginList", new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() - 4 * Scale), false))
        {
            if (child)
            {
                var anyShown = false;
                foreach (var (key, p) in candidates)
                {
                    if (!MatchesFilter(key, p, presetSearchFilter)) continue;

                    anyShown = true;
                    var isInPreset = preset.Plugins.Contains(key);
                    if (ImGui.Checkbox($"##{key}", ref isInPreset))
                    {
                        if (isInPreset)
                            preset.Plugins.Add(key);
                        else
                            preset.Plugins.Remove(key);

                        if (isShared)
                            presetManager.UpdateSharedPreset(preset);
                        else
                            presetManager.UpdatePreset(preset);
                    }

                    ImGui.SameLine();
                    ImGui.TextColored(p.IsLoaded ? Colors.LoadedDot : Colors.UnloadedDot, "●");
                    ImGui.SameLine();
                    ImGui.Text(p.Name);
                    DrawPluginTags(p);
                }

                if (!anyShown && !string.IsNullOrEmpty(presetSearchFilter))
                    ImGui.TextColored(Colors.TextMuted, "No plugins match your search.");
            }
        }

        ImGui.TextColored(Colors.TextDisabled, "● shows current state. Always-on plugins are managed separately and included automatically.");
    }

    private void DrawAlwaysOnEditor()
    {
        var charSet = presetManager.GetAlwaysOnPlugins();
        var sharedSet = presetManager.GetSharedAlwaysOnPlugins();

        UIHelpers.SectionHeader("Always-On Plugins", FontAwesomeIcon.Lock);
        ImGui.TextColored(Colors.TextMuted, $"{charSet.Count} character + {sharedSet.Count} shared. Character = this character only, Shared = every character.");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(Sizing.InputMedium * Scale);
        ImGui.InputTextWithHint("##AOSearch", "Search...", ref alwaysOnSearchFilter, 100);
        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var rows = installedPlugins
            .OrderBy(kv => kv.Value.Name)
            .ThenBy(kv => kv.Key)
            .Select(kv => (kv.Key, Plugin: (IExposedPlugin?)kv.Value))
            .ToList();
        rows.AddRange(charSet.Union(sharedSet)
            .Where(k => !installedPlugins.ContainsKey(k))
            .OrderBy(k => k)
            .Select(k => (k, (IExposedPlugin?)null)));

        var redundant = charSet.Where(sharedSet.Contains).ToList();
        var tableHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() - 4 * Scale;

        using (var outer = ImRaii.Child("AlwaysOnTableChild", new Vector2(0, tableHeight), false))
        {
            if (outer)
            {
                using var table = ImRaii.Table("##AlwaysOnTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
                if (table)
                {
                    ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 76 * Scale);
                    ImGui.TableSetupColumn("Shared", ImGuiTableColumnFlags.WidthFixed, 76 * Scale);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    foreach (var (key, p) in rows)
                    {
                        if (!MatchesFilter(key, p, alwaysOnSearchFilter)) continue;

                        var isSelf = key == presetManager.SelfKey;
                        var inChar = charSet.Contains(key);
                        var inShared = sharedSet.Contains(key);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        if (p != null)
                        {
                            ImGui.Text(p.Name);
                            DrawPluginTags(p);
                        }
                        else
                        {
                            ImGui.TextColored(Colors.TextMuted, $"{PluginKey.GetDisplayName(key)} (not installed)");
                        }

                        if (isSelf)
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(Colors.Warning, "(required)");
                        }
                        else if (inChar && inShared)
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(Colors.TextMuted, "(redundant - already shared)");
                        }

                        ImGui.TableNextColumn();
                        DrawCenteredCheckbox($"##{key}_char", inChar, isSelf, v =>
                        {
                            if (v) presetManager.AddAlwaysOnPlugin(key);
                            else presetManager.RemoveAlwaysOnPlugin(key);
                        });

                        ImGui.TableNextColumn();
                        DrawCenteredCheckbox($"##{key}_shared", inShared, isSelf, v =>
                        {
                            if (v) presetManager.AddSharedAlwaysOnPlugin(key);
                            else presetManager.RemoveSharedAlwaysOnPlugin(key);
                        });
                    }
                }
            }
        }

        ImGui.Spacing();

        if (redundant.Count > 0)
        {
            if (ImGui.Button($"Remove redundant ({redundant.Count})"))
            {
                foreach (var key in redundant)
                    presetManager.RemoveAlwaysOnPlugin(key);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove character entries that are already covered by shared");
            ImGui.SameLine();
        }

        var isDefault = presetManager.UseAlwaysOnAsDefault;
        if (isDefault)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonDefault))
            {
                if (ImGui.Button("★ Default"))
                    presetManager.SetAlwaysOnAsDefault(false);
            }
        }
        else
        {
            if (ImGui.Button("Set Default"))
                presetManager.SetAlwaysOnAsDefault(true);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(isDefault ? "Click to unset as default" : "Use always-on only mode as the default");

        var applyText = "Apply Always-On Only";
        var applyWidth = ImGui.CalcTextSize(applyText).X + 16 * Scale;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - applyWidth));
        using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonPrimary))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Colors.ButtonPrimaryHover))
        {
            if (ImGui.Button(applyText))
            {
                if (!presetManager.IsApplying)
                    _ = presetManager.ApplyAlwaysOnOnlyAsync();
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Disable everything except always-on plugins");
    }

    private static void DrawCenteredCheckbox(string id, bool value, bool disabled, Action<bool> onChange)
    {
        var offset = (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight()) * 0.5f;
        if (offset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        using (ImRaii.Disabled(disabled))
        {
            if (ImGui.Checkbox(id, ref value))
                onChange(value);
        }
    }

    private void DrawImportFromCharacter()
    {
        UIHelpers.SectionHeader("Import from Character", FontAwesomeIcon.UserFriends);

        var sources = presetManager.GetAllCharacters()
            .Where(c => c.ContentId != presetManager.CurrentCharacterId)
            .ToList();

        if (sources.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "No other characters available.");
            ImGui.Spacing();
            if (ImGui.Button("Close"))
                showImportFromCharacter = false;
            return;
        }

        if (sources.All(s => s.ContentId != importSourceCharacterId))
            importSourceCharacterId = sources[0].ContentId;

        var currentSource = sources.First(s => s.ContentId == importSourceCharacterId);

        ImGui.SetNextItemWidth(200 * Scale);
        using (var combo = ImRaii.Combo("##SourceChar", currentSource.DisplayName))
        {
            if (combo)
            {
                foreach (var source in sources)
                {
                    if (ImGui.Selectable(source.DisplayName, source.ContentId == importSourceCharacterId))
                        importSourceCharacterId = source.ContentId;
                }
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Close"))
            showImportFromCharacter = false;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (currentSource.Presets.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "This character has no presets.");
            return;
        }

        using var child = ImRaii.Child("ImportPresetList", new Vector2(0, 0), false);
        if (!child) return;

        foreach (var preset in currentSource.Presets)
        {
            if (ImGui.Button($"Import##{preset.Name}"))
            {
                var imported = presetManager.ImportPresetFromCharacter(importSourceCharacterId, preset.Name);
                if (imported != null)
                    SelectPreset(imported, false);
            }
            ImGui.SameLine();
            ImGui.Text($"{preset.Name} ({preset.Plugins.Count} plugins)");
        }
    }

    private void DrawDeleteConfirmation()
    {
        if (openDeleteModal && presetToDelete != null)
        {
            UIHelpers.OpenConfirmationModal("DeletePreset", "Delete Preset");
            openDeleteModal = false;
        }

        if (presetToDelete != null)
        {
            var isSharedPreset = presetManager.IsSharedPreset(presetToDelete);
            var typeLabel = isSharedPreset ? "shared preset" : "preset";

            var result = UIHelpers.ConfirmationModal(
                "DeletePreset",
                "Delete Preset",
                $"Are you sure you want to delete {typeLabel} '{presetToDelete.Name}'?\n\nThis cannot be undone.");

            if (result == true)
            {
                if (isSharedPreset)
                    presetManager.DeleteSharedPreset(presetToDelete);
                else
                    presetManager.DeletePreset(presetToDelete);

                if (selectedPreset == presetToDelete)
                {
                    selectedPreset = null;
                    renameTarget = null;
                }
                presetToDelete = null;
            }
            else if (result == false)
            {
                presetToDelete = null;
            }
        }
    }

    private static void DrawPluginTags(IExposedPlugin plugin)
    {
        if (plugin.IsDev)
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.TagDev, "[DEV]");
        }
        if (plugin.IsThirdParty)
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.TagThirdParty, "[3rd]");
        }
    }

    private static bool MatchesFilter(string key, IExposedPlugin? plugin, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        if (key.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        return plugin != null && plugin.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void AddCurrentlyEnabledPlugins(Preset preset, bool isShared)
    {
        presetManager.NormalizeStoredKeys();
        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();
        var added = 0;
        foreach (var p in Plugin.PluginInterface.InstalledPlugins)
        {
            var key = PluginKey.Get(p);
            if (p.IsLoaded &&
                !preset.Plugins.Contains(key) &&
                !effectiveAlwaysOn.Contains(key))
            {
                preset.Plugins.Add(key);
                added++;
            }
        }
        if (added > 0)
        {
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }
    }

    private void ExportPresetToClipboard(Preset preset)
    {
        try
        {
            var exportData = new
            {
                preset.Name,
                preset.Description,
                Plugins = preset.Plugins.ToList()
            };
            ImGui.SetClipboardText(JsonConvert.SerializeObject(exportData, Formatting.Indented));
            Plugin.Log.Info($"Exported preset '{preset.Name}' to clipboard");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to export preset");
        }
    }

    private void ImportPresetFromClipboard()
    {
        try
        {
            importError = string.Empty;
            var json = ImGui.GetClipboardText();

            if (string.IsNullOrWhiteSpace(json))
            {
                importError = "Clipboard empty";
                return;
            }

            var data = JsonConvert.DeserializeAnonymousType(json, new
            {
                Name = "",
                Description = (string?)null,
                Plugins = new List<string>(),
                EnabledPlugins = new List<string>()
            });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                importError = "Invalid data";
                return;
            }

            var plugins = data.Plugins?.Count > 0 ? data.Plugins : data.EnabledPlugins;

            var newPreset = new Preset
            {
                Name = data.Name,
                Description = data.Description ?? string.Empty,
                Plugins = new HashSet<string>(plugins ?? new List<string>()),
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            presetManager.AddPreset(newPreset);
            SelectPreset(newPreset, false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to import preset from clipboard");
            importError = "Parse failed";
        }
    }
}
