# Plugin Preset Manager

A robust plugin preset manager for Dalamud with always-on plugin support.

## Features

- **Create and Manage Presets**: Save your current plugin configuration as presets
- **Always-On Plugins**: Mark essential plugins that should always be enabled regardless of preset
- **Preview Changes**: See exactly what will change before applying a preset
- **Rollback Support**: Automatically save state before applying presets so you can undo
- **Plugin Discovery**: View all installed plugins with search and filtering
- **Validation**: Warns about missing plugins before applying presets
- **Snapshots**: Remembers plugin versions and metadata when presets are created

## Usage

### Command
- `/ppreset` - Open the Plugin Preset Manager window

### Creating a Preset
1. Enable the plugins you want in the preset
2. Open the Preset Manager (`/ppreset`)
3. Go to the "Presets" tab
4. Enter a name and click "Create from Current"

### Always-On Plugins
1. Go to the "Always-On Plugins" tab
2. Click "Add Plugin to Always-On"
3. Select the plugins you want always enabled

Alternatively, use the "All Plugins" tab and check the "Always-On" checkbox for any plugin.

### Applying a Preset
1. Select a preset from the list
2. Review the preview showing what will change
3. Click "Apply Preset"

The preset will enable all plugins in the preset PLUS any always-on plugins, and disable everything else.

### Rollback
If you have rollback enabled in settings, you can restore the previous state:
1. Open Settings (Config button in plugin installer or `/ppreset` settings tab)
2. Click "Rollback to Previous State"

## Settings

- **Enable Rollback**: Saves state before applying presets
- **Confirm Before Apply**: Show confirmation dialog
- **Show Preview Before Apply**: Preview changes before applying
- **Show Notifications**: Show chat messages when presets are applied
- **Delay Between Commands**: Time to wait between plugin enable/disable commands (adjust if experiencing issues)

## Building

1. Clone this repository
2. Ensure you have .NET 8.0 SDK installed
3. Build with: `dotnet build SamplePlugin/SamplePlugin.csproj`

The plugin will be output to `bin/Debug/`

## Installation

1. Build the plugin or download a release
2. Copy the output folder to your Dalamud devPlugins directory
3. Reload plugins in Dalamud

## License

AGPL-3.0-or-later
