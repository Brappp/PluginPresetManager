using System.Collections.Generic;

namespace PluginPresetManager.Models;

public class PresetPreview
{
    public List<PluginChange> ToEnable { get; } = new();
    public List<PluginChange> ToDisable { get; } = new();
    public List<PluginChange> NoChange { get; } = new();
    public List<string> Missing { get; } = new();

    public class PluginChange
    {
        public string InternalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAlwaysOn { get; set; }
    }
}
