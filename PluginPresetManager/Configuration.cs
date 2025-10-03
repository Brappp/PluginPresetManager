using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using PluginPresetManager.Models;

namespace PluginPresetManager;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public List<Preset>? Presets { get; set; }

    public HashSet<string>? AlwaysOnPlugins { get; set; }

    public Guid? LastAppliedPresetId { get; set; }

    public Guid? DefaultPresetId { get; set; }

    public Preset? LastState { get; set; }

    public bool EnableRollback { get; set; } = true;

    public int DelayBetweenCommands { get; set; } = 100;

    public bool ShowNotifications { get; set; } = true;

    public bool VerboseNotifications { get; set; } = false;

    public bool MigrationCompleted { get; set; } = false;
}
