using System.Collections.Generic;
using Dalamud.Plugin;

namespace PluginPresetManager;

public static class PluginKey
{
    public const string DevSuffix = "#dev";

    public static string Get(IExposedPlugin plugin) =>
        plugin.IsDev ? plugin.InternalName + DevSuffix : plugin.InternalName;

    public static string Get(string internalName, bool isDev) =>
        isDev ? internalName + DevSuffix : internalName;

    public static bool IsDev(string key) => key.EndsWith(DevSuffix);

    public static string GetInternalName(string key) =>
        key.EndsWith(DevSuffix) ? key[..^DevSuffix.Length] : key;

    public static string GetDisplayName(string key) =>
        IsDev(key) ? $"{GetInternalName(key)} (Dev)" : key;

    public static Dictionary<string, IExposedPlugin> BuildInstalledDictionary(IEnumerable<IExposedPlugin> plugins)
    {
        var dict = new Dictionary<string, IExposedPlugin>();
        foreach (var plugin in plugins)
        {
            dict[Get(plugin)] = plugin;
        }
        return dict;
    }
}
