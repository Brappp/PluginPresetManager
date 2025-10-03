using System;
using Dalamud.Configuration;

namespace PluginPresetManager;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public Guid? LastAppliedPresetId { get; set; }

    public Guid? DefaultPresetId { get; set; }

    public int DelayBetweenCommands { get; set; } = 100;

    public bool ShowNotifications { get; set; } = true;

    public bool VerboseNotifications { get; set; } = false;
}
