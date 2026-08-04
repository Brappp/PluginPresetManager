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
    private readonly SettingsTab settingsTab;
    private readonly HelpTab helpTab = new();
    private bool helpRequested;

    private Preset? selectedPreset;
    private bool isSelectedPresetShared;
    private bool showAlwaysOn;
    private bool showSettings = true;
    private bool showImportFromCharacter;

    private string presetSearchFilter = string.Empty;
    private string alwaysOnSearchFilter = string.Empty;

    private Preset? renamingPreset;
    private string renameBuffer = string.Empty;
    private string? renameError;
    private bool renameFocusPending;

    private Preset? descriptionTarget;
    private string descriptionBuffer = string.Empty;
    private bool openDescriptionPopup;

    private string importError = string.Empty;
    private ulong importSourceCharacterId;

    private Preset? presetToDelete;
    private bool openDeleteModal;

    private ulong lastSeenCharacterId;

    private Dictionary<string, IExposedPlugin>? cachedPlugins;
    private int lastPluginCount = -1;
    private DateTime lastPluginCacheRefresh = DateTime.MinValue;

    public PresetsTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
        settingsTab = new SettingsTab(plugin, presetManager);
    }

    private CharacterData Data => presetManager.CurrentData;

    private static float Scale => ImGuiHelpers.GlobalScale;

    public void ShowSettings()
    {
        showSettings = true;
    }

    public void ToggleSettings()
    {
        showSettings = !showSettings;
    }

    public void RequestHelp()
    {
        helpRequested = true;
    }

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
        if (presetManager.CurrentCharacterId != lastSeenCharacterId)
        {
            lastSeenCharacterId = presetManager.CurrentCharacterId;
            ClearSelection();
        }

        if (helpRequested)
        {
            ImGui.OpenPopup("PPMHelpPopup");
            helpRequested = false;
        }

        using (var helpPopup = ImRaii.Popup("PPMHelpPopup"))
        {
            if (helpPopup)
            {
                helpTab.Draw();
            }
        }

        if (!presetManager.HasCharacter)
        {
            if (showSettings)
            {
                settingsTab.Draw();
            }
            else
            {
                UIHelpers.EmptyState(FontAwesomeIcon.User, "Log in to a character to use presets.");
            }
            return;
        }

        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();

        DrawBanner(effectiveAlwaysOn);

        using (ImRaii.Disabled(presetManager.IsApplying))
        using (var left = ImRaii.Child("LeftPanel", new Vector2(Sizing.LeftPanelWidth * Scale, 0), false))
        {
            if (left)
                DrawLeftPanel();
        }

        ImGui.SameLine();

        UIHelpers.DropShadow(ImGui.GetCursorScreenPos(), ImGui.GetContentRegionAvail());

        using (ImRaii.PushColor(ImGuiCol.ChildBg, Colors.PanelBg))
        using (var right = ImRaii.Child("RightPanel", new Vector2(0, 0), true))
        {
            if (right)
            {
                UIHelpers.PanelGloss();

                using (ImRaii.Disabled(presetManager.IsApplying))
                {
                    if (showSettings)
                        DrawSettingsDetail();
                    else if (showImportFromCharacter)
                        DrawImportDetail();
                    else if (showAlwaysOn)
                        DrawAlwaysOnDetail();
                    else if (selectedPreset != null)
                        DrawPresetDetail(selectedPreset, isSelectedPresetShared, effectiveAlwaysOn);
                    else
                        DrawSettingsDetail();
                }
            }
        }

        DrawDeleteConfirmation();
    }

    private void ClearSelection()
    {
        selectedPreset = null;
        renamingPreset = null;
        renameError = null;
        presetToDelete = null;
        descriptionTarget = null;
        showAlwaysOn = false;
        showImportFromCharacter = false;
    }

    #region Banner

    private void DrawBanner(HashSet<string> effectiveAlwaysOn)
    {
        var height = ImGui.GetFrameHeight() + 12 * Scale;
        var bg = presetManager.IsApplying ? Colors.BannerApplyingBg : Colors.BannerActiveBg;
        var hasActive = presetManager.WasLastAppliedAlwaysOn || presetManager.GetLastAppliedPreset() != null;
        var countHovered = false;

        UIHelpers.DropShadow(ImGui.GetCursorScreenPos(), new Vector2(ImGui.GetContentRegionAvail().X, height));

        using (ImRaii.PushColor(ImGuiCol.ChildBg, bg))
        using (var banner = ImRaii.Child("StatusBanner", new Vector2(0, height), true,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (banner)
            {
                UIHelpers.PanelGloss();

                if (presetManager.IsApplying)
                {
                    ImGui.SetCursorPosY((height - ImGui.GetFrameHeight()) * 0.5f);
                    var cancelWidth = ImGui.CalcTextSize("Cancel").X + 14 * Scale;
                    var barSize = new Vector2(ImGui.GetContentRegionAvail().X - cancelWidth - 6 * Scale, ImGui.GetFrameHeight());
                    var barPos = ImGui.GetCursorScreenPos();
                    using (ImRaii.PushColor(ImGuiCol.PlotHistogram, Colors.ButtonPrimaryHover))
                    {
                        ImGui.ProgressBar(presetManager.ApplyingProgress, barSize, "");
                    }

                    var statusText = presetManager.ApplyingStatus;
                    var textSize = ImGui.CalcTextSize(statusText);
                    var textPos = new Vector2(
                        barPos.X + Math.Max(4 * Scale, (barSize.X - textSize.X) * 0.5f),
                        barPos.Y + (barSize.Y - textSize.Y) * 0.5f);
                    ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(Colors.DetailTitle), statusText);

                    ImGui.SameLine();
                    if (ImGui.SmallButton("Cancel"))
                        presetManager.CancelApply();
                }
                else
                {
                    ImGui.SetCursorPosY((height - ImGui.GetTextLineHeight()) * 0.5f);

                    var activeName = presetManager.WasLastAppliedAlwaysOn
                        ? "Always-On Only"
                        : presetManager.GetLastAppliedPreset()?.Name ?? "None";

                    ImGui.TextColored(hasActive ? Colors.Success : Colors.TextMuted, hasActive ? "●" : "○");
                    if (hasActive)
                    {
                        var dotCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
                        var glowList = ImGui.GetWindowDrawList();
                        glowList.AddCircleFilled(dotCenter, 8f * Scale, ImGui.GetColorU32(new Vector4(0.4f, 1f, 0.6f, 0.10f)));
                        glowList.AddCircleFilled(dotCenter, 5f * Scale, ImGui.GetColorU32(new Vector4(0.4f, 1f, 0.6f, 0.14f)));
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(Colors.TextMuted, "Active:");
                    ImGui.SameLine();
                    ImGui.TextColored(hasActive ? Colors.DetailTitle : Colors.TextMuted, activeName);

                    var installed = GetInstalledPlugins();
                    var enabledCount = installed.Values.Count(p => p.IsLoaded);
                    var countText = $"{enabledCount} plugins enabled";
                    var countWidth = ImGui.CalcTextSize(countText).X;
                    ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - countWidth));
                    ImGui.TextColored(Colors.TextMuted, countText);

                    if (ImGui.IsItemHovered())
                    {
                        countHovered = true;
                        var alwaysOnLoaded = installed.Count(kv => kv.Value.IsLoaded && effectiveAlwaysOn.Contains(kv.Key));
                        using var tooltip = UIHelpers.Tooltip("Enabled now");
                        ImGui.Text($"{alwaysOnLoaded} always-on");
                        ImGui.Text($"{enabledCount - alwaysOnLoaded} from preset or enabled manually");
                    }
                }
            }
        }

        if (!presetManager.IsApplying && hasActive && !countHovered && ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Show what's active");
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                JumpToActive();
        }
    }

    private void JumpToActive()
    {
        if (presetManager.WasLastAppliedAlwaysOn)
        {
            ClearSelection();
            showAlwaysOn = true;
            showSettings = false;
            alwaysOnSearchFilter = string.Empty;
        }
        else if (presetManager.GetLastAppliedPreset() is { } lastApplied)
        {
            SelectPreset(lastApplied, presetManager.IsSharedPreset(lastApplied));
        }
    }

    #endregion

    #region Left panel

    private void DrawLeftPanel()
    {
        DrawCharacterSelect();

        var footerHeight = ImGui.GetFrameHeightWithSpacing()
                           + (string.IsNullOrEmpty(importError) ? 0 : ImGui.GetTextLineHeightWithSpacing());

        var listAvail = ImGui.GetContentRegionAvail();
        UIHelpers.DropShadow(ImGui.GetCursorScreenPos(), new Vector2(listAvail.X, listAvail.Y - footerHeight));

        using (ImRaii.PushColor(ImGuiCol.ChildBg, Colors.PanelBg))
        using (var list = ImRaii.Child("PresetList", new Vector2(0, -footerHeight), true))
        {
            if (list)
            {
                UIHelpers.PanelGloss();
                DrawPresetList();
            }
        }

        if (ImGui.Button("+ New...", new Vector2(-1, 0)))
            ImGui.OpenPopup("NewPresetMenu");

        using (var popup = ImRaii.Popup("NewPresetMenu"))
        {
            if (popup)
            {
                if (ImGui.MenuItem("Empty preset"))
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

                if (ImGui.MenuItem("From current plugins"))
                {
                    var preset = presetManager.CreatePresetFromCurrent("Current Plugins");
                    presetManager.AddPreset(preset);
                    SelectPreset(preset, false);
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Import from Clipboard"))
                    ImportPresetFromClipboard();

                if (ImGui.MenuItem("Import from Character..."))
                {
                    ClearSelection();
                    showImportFromCharacter = true;
                    showSettings = false;
                    importSourceCharacterId = 0;
                }
            }
        }

        if (!string.IsNullOrEmpty(importError))
            ImGui.TextColored(Colors.Error, importError);
    }

    private void DrawCharacterSelect()
    {
        ImGui.SetNextItemWidth(-1);
        using (var combo = ImRaii.Combo("##CharSelect", Data.DisplayName))
        {
            if (combo)
            {
                foreach (var character in presetManager.GetAllCharacters())
                {
                    var isSelected = character.ContentId == presetManager.CurrentCharacterId;
                    var label = character.DisplayName;
                    if (character.ContentId == plugin.ActiveContentId)
                        label += " (you)";

                    if (ImGui.Selectable(label, isSelected) && !isSelected)
                    {
                        presetManager.SwitchCharacter(character.ContentId);
                        plugin.SaveConfiguration();
                        ClearSelection();
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Which character's presets you are viewing");
    }

    private void SelectPreset(Preset preset, bool isShared)
    {
        selectedPreset = preset;
        isSelectedPresetShared = isShared;
        showAlwaysOn = false;
        showImportFromCharacter = false;
        showSettings = false;
        renamingPreset = null;
        renameError = null;
        presetSearchFilter = string.Empty;
    }

    private void DrawPresetList()
    {
        var lastApplied = presetManager.GetLastAppliedPreset();
        var isAlwaysOnActive = presetManager.WasLastAppliedAlwaysOn;
        var totalAlwaysOn = presetManager.GetAlwaysOnPlugins().Count + presetManager.GetSharedAlwaysOnPlugins().Count;

        var aoClicked = DrawListRow(
            "alwayson",
            $"Always-On ({totalAlwaysOn})",
            showAlwaysOn,
            isAlwaysOnActive,
            presetManager.UseAlwaysOnAsDefault,
            () =>
            {
                if (ImGui.IsItemHovered())
                {
                    using var tooltip = UIHelpers.Tooltip("Always-On");
                    ImGui.Text("Plugins kept enabled with every preset.");
                    ImGui.TextColored(Colors.TextDisabled, "Tap the ○ to disable everything except these");
                }
            },
            () => _ = presetManager.ApplyAlwaysOnOnlyAsync());
        if (aoClicked)
        {
            ClearSelection();
            showAlwaysOn = true;
            showSettings = false;
            alwaysOnSearchFilter = string.Empty;
        }

        var characterPresets = presetManager.GetAllPresets().ToList();
        DrawSectionLabel("Presets", characterPresets.Count);
        if (characterPresets.Count == 0)
            ImGui.TextColored(Colors.TextDisabled, "None yet — use + New below");
        foreach (var preset in characterPresets)
            DrawPresetRow(preset, false, lastApplied, isAlwaysOnActive);

        var sharedPresets = presetManager.GetSharedPresets().ToList();
        DrawSectionLabel("Shared", sharedPresets.Count);
        foreach (var preset in sharedPresets)
            DrawPresetRow(preset, true, lastApplied, isAlwaysOnActive);
    }

    private static void DrawSectionLabel(string text, int count)
    {
        UIHelpers.VerticalSpacing(Sizing.SpacingSmall);
        ImGui.TextColored(Colors.TextMuted, $"{text} ({count})");
    }

    private void DrawPresetRow(Preset preset, bool isShared, Preset? lastApplied, bool isAlwaysOnActive)
    {
        var isActive = !isAlwaysOnActive && lastApplied == preset;
        var isSelected = selectedPreset == preset;
        var isDefault = Data.DefaultPreset == preset.Name;
        var suffix = isShared ? "shared" : "char";

        var clicked = DrawListRow(
            $"{suffix}_{preset.Name}",
            preset.Name,
            isSelected,
            isActive,
            isDefault,
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

    private bool DrawListRow(string id, string name, bool isSelected, bool isActive, bool isDefault, Action afterSelectable, Action onApply)
    {
        var starWidth = isDefault ? 14 * Scale : 0;

        DrawDotApplyButton($"##dot_{id}", isActive, onApply);
        ImGui.SameLine();

        var clicked = ImGui.Selectable($"{name}##row_{id}", isSelected, ImGuiSelectableFlags.None,
            new Vector2(Math.Max(20 * Scale, ImGui.GetContentRegionAvail().X - starWidth), 0));
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && !presetManager.IsApplying)
            onApply();
        afterSelectable();

        if (isDefault)
        {
            ImGui.SameLine(ImGui.GetContentRegionMax().X - starWidth);
            DrawStar();
        }

        return clicked;
    }

    private void DrawDotApplyButton(string id, bool isActive, Action onApply)
    {
        var size = new Vector2(16 * Scale, ImGui.GetTextLineHeight());
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        string glyph;
        Vector4 color;
        if (isActive)
        {
            glyph = "●";
            color = Colors.Success;
        }
        else if (hovered)
        {
            glyph = "●";
            color = new Vector4(0.4f, 1f, 0.6f, 0.55f);
        }
        else
        {
            glyph = "○";
            color = Colors.Inactive;
        }

        var glyphSize = ImGui.CalcTextSize(glyph);
        ImGui.GetWindowDrawList().AddText(
            new Vector2(pos.X + (size.X - glyphSize.X) * 0.5f, pos.Y + (size.Y - glyphSize.Y) * 0.5f),
            ImGui.GetColorU32(color), glyph);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(isActive ? "Active" : "Apply");
        }

        if (clicked && !isActive && !presetManager.IsApplying)
            onApply();
    }

    private void DrawPresetRowTooltip(Preset preset, bool isShared, bool isDefault)
    {
        var installedPlugins = GetInstalledPlugins();
        var alwaysOnCount = presetManager.GetAlwaysOnPlugins().Count + presetManager.GetSharedAlwaysOnPlugins().Count;
        var missingCount = preset.Plugins.Count(p => !installedPlugins.ContainsKey(p));

        using var tooltip = UIHelpers.Tooltip(isShared ? $"{preset.Name} (Shared)" : preset.Name);

        if (isDefault)
        {
            ImGui.TextColored(Colors.Star, "★ Default — applies on login");
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
        ImGui.TextColored(Colors.TextDisabled, "Click to open · tap the ○ (or double-click) to apply");
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

    #endregion

    #region Detail anatomy

    private void DrawDetailTitle(string title)
    {
        ImGui.TextColored(Colors.DetailTitle, title);
    }

    private void DrawHeaderStar(bool isDefault, Action onToggle)
    {
        var tooltip = isDefault
            ? "Default — applied automatically on login. Click to unset."
            : "Set as default — applied automatically on login.";
        if (UIHelpers.GhostIconButton("##defaultToggle", FontAwesomeIcon.Star,
                isDefault ? Colors.Star : Colors.TextDisabled, tooltip))
            onToggle();
    }

    private void DrawHeaderApply(bool isActive, Action onApply)
    {
        var applyWidth = Sizing.ButtonMedium * Scale;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - applyWidth));

        if (isActive)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonActive))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Colors.ButtonActive))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, Colors.ButtonActive))
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.ButtonActiveText))
            {
                ImGui.Button("Active", new Vector2(applyWidth, 0));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This is what's applied right now");
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Colors.ButtonPrimary))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Colors.ButtonPrimaryHover))
            {
                if (ImGui.Button("Apply", new Vector2(applyWidth, 0)))
                {
                    if (!presetManager.IsApplying)
                        onApply();
                }
            }
        }
    }

    private void DrawFactsRule()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    #endregion

    #region Preset detail

    private void DrawPresetDetail(Preset preset, bool isShared, HashSet<string> effectiveAlwaysOn)
    {
        var isActive = !presetManager.WasLastAppliedAlwaysOn && presetManager.GetLastAppliedPreset() == preset;
        var isDefault = Data.DefaultPreset == preset.Name;

        if (renamingPreset == preset)
        {
            DrawRenameField(preset, isShared);
        }
        else
        {
            DrawDetailTitle(preset.Name);
            ImGui.SameLine();
            if (UIHelpers.GhostIconButton("##rename", FontAwesomeIcon.Pen, Colors.TextDisabled, "Rename"))
            {
                renamingPreset = preset;
                renameBuffer = preset.Name;
                renameError = null;
                renameFocusPending = true;
            }
        }

        ImGui.SameLine();
        DrawHeaderStar(isDefault, () => presetManager.SetDefaultPreset(isDefault ? null : preset.Name));
        DrawHeaderApply(isActive, () => _ = presetManager.ApplyPresetAsync(preset));

        if (renameError != null)
            ImGui.TextColored(Colors.Error, renameError);

        DrawPresetFacts(preset, isShared, isActive, effectiveAlwaysOn);
        DrawFactsRule();

        var menuWidth = UIHelpers.IconButtonWidth(FontAwesomeIcon.EllipsisH);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - menuWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.InputTextWithHint("##PluginSearch", "Search...", ref presetSearchFilter, 100);
        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.EllipsisH, "presetActions", "More actions", menuWidth))
            ImGui.OpenPopup("PresetActionsMenu");
        DrawPresetActionsMenu(preset, isShared);
        DrawDescriptionPopup(preset, isShared);

        ImGui.Spacing();
        DrawPresetPluginList(preset, isShared, effectiveAlwaysOn);
    }

    private void DrawRenameField(Preset preset, bool isShared)
    {
        if (renameFocusPending)
        {
            ImGui.SetKeyboardFocusHere();
            renameFocusPending = false;
        }

        ImGui.SetNextItemWidth(Math.Max(80 * Scale,
            ImGui.GetContentRegionAvail().X - Sizing.ButtonMedium * Scale - 40 * Scale));
        var entered = ImGui.InputText("##PresetRename", ref renameBuffer, 100, ImGuiInputTextFlags.EnterReturnsTrue);
        var deactivated = ImGui.IsItemDeactivated();

        if (entered || (deactivated && !ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            TryRename(preset, isShared);
            if (renameError == null)
                renamingPreset = null;
            else
                renameFocusPending = true;
        }
        else if (deactivated)
        {
            renamingPreset = null;
            renameError = null;
        }
    }

    private void DrawPresetFacts(Preset preset, bool isShared, bool isActive, HashSet<string> effectiveAlwaysOn)
    {
        var alwaysOnCount = effectiveAlwaysOn.Count;
        ImGui.TextColored(Colors.TextMuted, $"{preset.Plugins.Count} plugins + {alwaysOnCount} always-on");

        if (isShared)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.TextMuted, " · shared with all characters");
        }

        if (isActive)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.Success, " · active now");
            return;
        }

        var (toEnable, toDisable) = CountChanges(preset, effectiveAlwaysOn);
        if (toEnable == 0 && toDisable == 0)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.TextMuted, " · matches current state");
            return;
        }

        ImGui.SameLine(0, 0);
        ImGui.TextColored(Colors.TextMuted, " · applying would ");
        if (toEnable > 0)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.Success, $"enable {toEnable}");
        }
        if (toEnable > 0 && toDisable > 0)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.TextMuted, ", ");
        }
        if (toDisable > 0)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.Warning, $"disable {toDisable}");
        }
    }

    private (int ToEnable, int ToDisable) CountChanges(Preset preset, HashSet<string> effectiveAlwaysOn)
    {
        var installed = GetInstalledPlugins();
        var wanted = new HashSet<string>(preset.Plugins);
        wanted.UnionWith(effectiveAlwaysOn);

        var enable = 0;
        var disable = 0;
        foreach (var (key, p) in installed)
        {
            if (wanted.Contains(key))
            {
                if (!p.IsLoaded) enable++;
            }
            else if (p.IsLoaded && key != presetManager.SelfKey)
            {
                disable++;
            }
        }
        return (enable, disable);
    }

    private void DrawPresetActionsMenu(Preset preset, bool isShared)
    {
        using var popup = ImRaii.Popup("PresetActionsMenu");
        if (!popup) return;

        if (ImGui.MenuItem("Add Current Plugins"))
            AddCurrentlyEnabledPlugins(preset, isShared);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add all currently enabled plugins to this preset");

        if (ImGui.MenuItem("Edit Description..."))
        {
            descriptionTarget = preset;
            descriptionBuffer = preset.Description;
            openDescriptionPopup = true;
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
            ExportPresetToClipboard(preset);

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
                isSelectedPresetShared = true;
                renamingPreset = null;
            }
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Delete"))
        {
            presetToDelete = preset;
            openDeleteModal = true;
        }
    }

    private void DrawDescriptionPopup(Preset preset, bool isShared)
    {
        if (openDescriptionPopup)
        {
            ImGui.OpenPopup("EditDescriptionPopup");
            openDescriptionPopup = false;
        }

        using var popup = ImRaii.Popup("EditDescriptionPopup");
        if (!popup)
        {
            if (descriptionTarget == preset && !openDescriptionPopup)
            {
                if (descriptionBuffer != preset.Description)
                {
                    preset.Description = descriptionBuffer;
                    if (isShared)
                        presetManager.UpdateSharedPreset(preset);
                    else
                        presetManager.UpdatePreset(preset);
                }
                descriptionTarget = null;
            }
            return;
        }

        if (descriptionTarget != preset)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        ImGui.TextColored(Colors.Header, "Description");
        ImGui.InputTextMultiline("##PresetDesc", ref descriptionBuffer, 500, new Vector2(280 * Scale, 70 * Scale));
        if (ImGui.IsItemDeactivatedAfterEdit() && descriptionBuffer != preset.Description)
        {
            preset.Description = descriptionBuffer;
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }

        if (ImGui.Button("Close"))
            ImGui.CloseCurrentPopup();
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

    private void DrawPresetPluginList(Preset preset, bool isShared, HashSet<string> effectiveAlwaysOn)
    {
        var installedPlugins = GetInstalledPlugins();

        var missingPlugins = preset.Plugins
            .Where(p => !installedPlugins.ContainsKey(p))
            .ToList();

        if (missingPlugins.Count > 0)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextColored(Colors.Warning, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            ImGui.SameLine();
            ImGui.TextColored(Colors.Warning, $"{missingPlugins.Count} missing plugin(s)");
            if (ImGui.IsItemHovered())
            {
                using var tooltip = UIHelpers.Tooltip("Not installed");
                foreach (var key in missingPlugins)
                    ImGui.Text(PluginKey.GetDisplayName(key));
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove from preset"))
            {
                preset.Plugins.ExceptWith(missingPlugins);
                if (isShared)
                    presetManager.UpdateSharedPreset(preset);
                else
                    presetManager.UpdatePreset(preset);
            }
            ImGui.Spacing();
        }

        var candidates = installedPlugins
            .Where(kv => !effectiveAlwaysOn.Contains(kv.Key))
            .OrderBy(kv => kv.Value.Name)
            .ThenBy(kv => kv.Key)
            .ToList();

        using (ImRaii.PushColor(ImGuiCol.ChildBg, Colors.InsetBg))
        using (var child = ImRaii.Child("PluginList", new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() - 4 * Scale), true))
        {
            if (child)
            {
                UIHelpers.InsetShade();
                var anyShown = false;
                var rowIndex = 0;
                foreach (var (key, p) in candidates)
                {
                    if (!MatchesFilter(key, p, presetSearchFilter)) continue;

                    if ((rowIndex++ & 1) == 1)
                    {
                        var stripeMin = ImGui.GetCursorScreenPos();
                        var stripeMax = new Vector2(stripeMin.X + ImGui.GetContentRegionAvail().X, stripeMin.Y + ImGui.GetFrameHeight());
                        ImGui.GetWindowDrawList().AddRectFilled(stripeMin, stripeMax, ImGui.GetColorU32(Colors.RowStripe));
                    }

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

        ImGui.TextColored(Colors.TextDisabled, "● shows current state. Always-on plugins are included automatically.");
    }

    #endregion

    #region Always-On detail

    private void DrawAlwaysOnDetail()
    {
        var charSet = presetManager.GetAlwaysOnPlugins();
        var sharedSet = presetManager.GetSharedAlwaysOnPlugins();
        var installedPlugins = GetInstalledPlugins();
        var isDefault = presetManager.UseAlwaysOnAsDefault;
        var isActive = presetManager.WasLastAppliedAlwaysOn;

        DrawDetailTitle("Always-On");
        ImGui.SameLine();
        DrawHeaderStar(isDefault, () => presetManager.SetAlwaysOnAsDefault(!isDefault));
        DrawHeaderApply(isActive, () => _ = presetManager.ApplyAlwaysOnOnlyAsync());

        ImGui.TextColored(Colors.TextMuted, $"{charSet.Count} character + {sharedSet.Count} shared · included with every preset");
        if (isActive)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(Colors.Success, " · active now");
        }

        DrawFactsRule();

        var redundant = charSet.Where(sharedSet.Contains).ToList();
        var stale = charSet.Union(sharedSet).Where(k => !installedPlugins.ContainsKey(k)).ToList();

        var menuWidth = UIHelpers.IconButtonWidth(FontAwesomeIcon.EllipsisH);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - menuWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.InputTextWithHint("##AOSearch", "Search...", ref alwaysOnSearchFilter, 100);
        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.EllipsisH, "aoActions", "Clean up", menuWidth))
            ImGui.OpenPopup("AlwaysOnMenu");

        using (var popup = ImRaii.Popup("AlwaysOnMenu"))
        {
            if (popup)
            {
                using (ImRaii.Disabled(stale.Count == 0))
                {
                    if (ImGui.MenuItem($"Remove stale entries ({stale.Count})"))
                    {
                        foreach (var key in stale)
                        {
                            presetManager.RemoveAlwaysOnPlugin(key);
                            presetManager.RemoveSharedAlwaysOnPlugin(key);
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove entries for plugins that are no longer installed");

                using (ImRaii.Disabled(redundant.Count == 0))
                {
                    if (ImGui.MenuItem($"Remove redundant entries ({redundant.Count})"))
                    {
                        foreach (var key in redundant)
                            presetManager.RemoveAlwaysOnPlugin(key);
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove character entries that are already covered by shared");
            }
        }

        ImGui.Spacing();

        var rows = installedPlugins
            .OrderBy(kv => kv.Value.Name)
            .ThenBy(kv => kv.Key)
            .Select(kv => (kv.Key, Plugin: (IExposedPlugin?)kv.Value))
            .ToList();
        rows.AddRange(charSet.Union(sharedSet)
            .Where(k => !installedPlugins.ContainsKey(k))
            .OrderBy(k => k)
            .Select(k => (k, (IExposedPlugin?)null)));

        using (ImRaii.PushColor(ImGuiCol.ChildBg, Colors.InsetBg))
        using (var outer = ImRaii.Child("AlwaysOnTableChild", new Vector2(0, 0), true))
        {
            if (outer)
            {
                UIHelpers.InsetShade();
                using var tableStyle = UIHelpers.PushTableStyle();
                using var table = ImRaii.Table("##AlwaysOnTable", 3,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY);
                if (table)
                {
                    ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 76 * Scale);
                    ImGui.TableSetupColumn("Shared", ImGuiTableColumnFlags.WidthFixed, 76 * Scale);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    UIHelpers.TintedHeadersRow();

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
                            ImGui.TextColored(p.IsLoaded ? Colors.LoadedDot : Colors.UnloadedDot, "●");
                            ImGui.SameLine();
                            ImGui.Text(p.Name);
                            DrawPluginTags(p);
                        }
                        else
                        {
                            ImGui.TextColored(Colors.UnloadedDot, "●");
                            ImGui.SameLine();
                            ImGui.TextColored(Colors.TextMuted, PluginKey.GetDisplayName(key));
                            ImGui.SameLine();
                            ImGui.TextColored(Colors.Error, "(not installed)");
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

    #endregion

    #region Settings + Import details

    private void DrawSettingsDetail()
    {
        DrawDetailTitle("Settings");
        DrawFactsRule();
        settingsTab.Draw();
    }

    private void DrawImportDetail()
    {
        DrawDetailTitle("Import from Character");

        var sources = presetManager.GetAllCharacters()
            .Where(c => c.ContentId != presetManager.CurrentCharacterId)
            .ToList();

        ImGui.TextColored(Colors.TextMuted, "Copy a preset from another character");
        DrawFactsRule();

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

    #endregion

    #region Delete confirmation

    private void DrawDeleteConfirmation()
    {
        if (openDeleteModal && presetToDelete != null)
        {
            UIHelpers.OpenConfirmationModal("DeletePreset", "Delete Preset");
            openDeleteModal = false;
        }

        if (presetToDelete == null)
            return;

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
                renamingPreset = null;
            }
            presetToDelete = null;
        }
        else if (result == false || !ImGui.IsPopupOpen("Delete Preset##DeletePreset"))
        {
            presetToDelete = null;
        }
    }

    #endregion

    #region Helpers

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

    #endregion
}
