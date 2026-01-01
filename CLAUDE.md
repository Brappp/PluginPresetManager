# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build PluginPresetManager/PluginPresetManager.csproj

# Build release
dotnet build PluginPresetManager/PluginPresetManager.csproj -c Release
```

This is a Dalamud plugin using Dalamud.NET.Sdk 14.0.1, which handles most build configuration automatically.

## Architecture Overview

A Dalamud plugin for FFXIV that manages plugin presets - allowing users to save, restore, and automate sets of enabled/disabled plugins.

### Core Components

- **Plugin.cs** - Entry point implementing `IDalamudPlugin`. Handles Dalamud service injection via `[PluginService]` attributes, command registration (`/ppreset`, `/ppm`), and login event handling for auto-applying default presets.

- **PresetManager.cs** - Core business logic for applying presets asynchronously. Enables/disables plugins via Dalamud commands (`/xlenableplugin`, `/xldisableplugin`) with progress tracking, 30-second timeout per plugin, and undo functionality. Exposes `IsApplying`, `ApplyingProgress`, and `ApplyingStatus` for UI feedback.

- **CharacterStorage.cs** - Per-character data persistence using JSON files. Directory structure:
  - `global/` - Shared presets and always-on plugins
  - `characters/{contentId}/` - Per-character data
  - Each contains: `presets/*.json`, `always-on.json`, `config.json`

- **DalamudReflectionHelper.cs** - Optional experimental feature using reflection to access Dalamud's internal `ProfileManager` for persistent plugin states that survive restarts. Falls back to commands if unavailable. May break on Dalamud updates.

### Key Concepts

- **Always-On Plugins** - Plugins that remain enabled regardless of preset. The plugin automatically adds itself to prevent self-disable.
- **Preset Preview** - `PresetPreview` model shows what changes before applying (ToEnable, ToDisable, NoChange, Missing lists).
- **Global vs Per-Character** - Users switch between shared global config and character-specific configs via `PresetManager.SwitchCharacter()`.

### UI Structure

ImGui-based windowing in `Windows/`:
- `MainWindow.cs` - Tab container with character selector header
- `Tabs/ProfilesTab.cs` - View and apply presets
- `Tabs/ManageTab.cs` - Create/edit presets and always-on plugins
- `Tabs/SettingsTab.cs` - Configuration options
- `Tabs/HelpTab.cs` - Usage information

Shared utilities in `UI/UIConstants.cs` and `UI/UIHelpers.cs`.
