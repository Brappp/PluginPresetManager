using System;
using System.Collections.Generic;

namespace PluginPresetManager.Models;

[Serializable]
public class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime LastModified { get; set; } = DateTime.Now;

    public HashSet<string> EnabledPlugins { get; set; } = new();
}
