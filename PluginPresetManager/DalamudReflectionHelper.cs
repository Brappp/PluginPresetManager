using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PluginPresetManager;

public class DalamudReflectionHelper
{
    private readonly IPluginLog log;

    private object? profileManager;
    private object? pluginManager;
    private object? dalamudConfig;

    private PropertyInfo? profilesProperty;
    private PropertyInfo? defaultProfileProperty;
    private MethodInfo? wantsPluginMethod;
    private MethodInfo? addOrUpdateMethod;

    private PropertyInfo? installedPluginsProperty;
    private PropertyInfo? manifestProperty;
    private PropertyInfo? internalNameProperty;
    private PropertyInfo? workingPluginIdProperty;
    private PropertyInfo? stateProperty;
    private MethodInfo? loadAsyncMethod;
    private MethodInfo? unloadAsyncMethod;
    private object? unloadDisposalMode;

    private Type? localDevPluginType;
    private PropertyInfo? startOnBootProperty;
    private MethodInfo? queueSaveMethod;

    private bool initialized = false;
    private bool initializationFailed = false;

    public bool IsAvailable => initialized && !initializationFailed;

    public DalamudReflectionHelper(IPluginLog log)
    {
        this.log = log;
    }

    public bool TryInitialize()
    {
        if (initialized) return !initializationFailed;
        if (initializationFailed) return false;

        initialized = true;
        initializationFailed = true;

        try
        {
            log.Info("Initializing reflection helper...");

            var dalamudAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Dalamud");
            if (dalamudAssembly == null)
                return Fail("Dalamud assembly not found");

            var serviceType = dalamudAssembly.GetType("Dalamud.Service`1");
            if (serviceType == null)
                return Fail("Service<T> type not found");

            object? GetService(string typeName)
            {
                var type = dalamudAssembly.GetType(typeName);
                if (type == null) return null;
                var getMethod = serviceType.MakeGenericType(type).GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
                return getMethod?.Invoke(null, null);
            }

            profileManager = GetService("Dalamud.Plugin.Internal.Profiles.ProfileManager");
            if (profileManager == null)
                return Fail("ProfileManager not available");

            pluginManager = GetService("Dalamud.Plugin.Internal.PluginManager");
            if (pluginManager == null)
                return Fail("PluginManager not available");

            dalamudConfig = GetService("Dalamud.Configuration.Internal.DalamudConfiguration");
            queueSaveMethod = dalamudConfig?.GetType().GetMethod("QueueSave", BindingFlags.Instance | BindingFlags.Public);

            var profileManagerType = profileManager.GetType();
            profilesProperty = profileManagerType.GetProperty("Profiles");
            defaultProfileProperty = profileManagerType.GetProperty("DefaultProfile");
            if (profilesProperty == null || defaultProfileProperty == null)
                return Fail("ProfileManager.Profiles/DefaultProfile not found");

            var profileType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Profiles.Profile");
            if (profileType == null)
                return Fail("Profile type not found");

            wantsPluginMethod = profileType.GetMethod("WantsPlugin", new[] { typeof(Guid) });
            addOrUpdateMethod = profileType.GetMethod("AddOrUpdateAsync",
                new[] { typeof(Guid), typeof(string), typeof(bool), typeof(bool) });
            if (wantsPluginMethod == null || addOrUpdateMethod == null)
                return Fail("Profile.WantsPlugin/AddOrUpdateAsync not found");

            installedPluginsProperty = pluginManager.GetType().GetProperty("InstalledPlugins", BindingFlags.Instance | BindingFlags.Public);
            if (installedPluginsProperty == null)
                return Fail("PluginManager.InstalledPlugins not found");

            var localPluginType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin");
            localDevPluginType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.LocalDevPlugin");
            if (localPluginType == null || localDevPluginType == null)
                return Fail("LocalPlugin/LocalDevPlugin types not found");

            manifestProperty = localPluginType.GetProperty("Manifest");
            internalNameProperty = localPluginType.GetProperty("InternalName");
            workingPluginIdProperty = localPluginType.GetProperty("EffectiveWorkingPluginId");
            stateProperty = localPluginType.GetProperty("State");
            startOnBootProperty = localDevPluginType.GetProperty("StartOnBoot");
            if (manifestProperty == null || internalNameProperty == null || workingPluginIdProperty == null || stateProperty == null)
                return Fail("LocalPlugin members not found");

            var loadReasonType = typeof(PluginLoadReason);
            loadAsyncMethod = localPluginType.GetMethod("LoadAsync",
                new[] { loadReasonType, typeof(bool), typeof(CancellationToken) });
            if (loadAsyncMethod == null)
                return Fail("LocalPlugin.LoadAsync not found");

            var disposalModeType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode");
            if (disposalModeType == null)
                return Fail("PluginLoaderDisposalMode type not found");

            unloadDisposalMode = Enum.Parse(disposalModeType, "WaitBeforeDispose");
            unloadAsyncMethod = localPluginType.GetMethod("UnloadAsync", new[] { disposalModeType });
            if (unloadAsyncMethod == null)
                return Fail("LocalPlugin.UnloadAsync not found");

            initializationFailed = false;
            log.Info("Reflection helper ready");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize reflection helper");
            return false;
        }
    }

    private bool Fail(string reason)
    {
        log.Warning($"Reflection helper unavailable: {reason}");
        return false;
    }

    public async Task<bool> SetPluginStateAsync(IExposedPlugin plugin, bool enabled)
    {
        if (!TryInitialize())
            return false;

        try
        {
            var localPlugin = ResolveLocalPlugin(plugin);
            if (localPlugin == null)
            {
                log.Warning($"Could not resolve LocalPlugin for {plugin.InternalName} (Dev: {plugin.IsDev})");
                return false;
            }

            var workingId = workingPluginIdProperty!.GetValue(localPlugin) as Guid? ?? Guid.Empty;
            if (workingId == Guid.Empty)
            {
                log.Warning($"Empty working plugin ID for {plugin.InternalName} (Dev: {plugin.IsDev})");
                return false;
            }

            var state = stateProperty!.GetValue(localPlugin)?.ToString();
            if (state is "LoadError" or "UnloadError")
            {
                log.Warning($"{plugin.InternalName} is in state {state}, not touching it");
                return false;
            }

            var profiles = ((IEnumerable)profilesProperty!.GetValue(profileManager)!)
                .Cast<object>()
                .Where(p => wantsPluginMethod!.Invoke(p, new object[] { workingId }) != null)
                .ToList();

            object? profile = profiles.Count switch
            {
                1 => profiles[0],
                0 => defaultProfileProperty!.GetValue(profileManager),
                _ => null,
            };

            if (profile == null)
            {
                log.Warning($"{plugin.InternalName} is in multiple collections, cannot toggle it");
                return false;
            }

            if (enabled)
            {
                await InvokeTask(addOrUpdateMethod!.Invoke(profile, new object[] { workingId, plugin.InternalName, true, false }));

                if (localDevPluginType!.IsInstanceOfType(localPlugin) && startOnBootProperty != null)
                {
                    startOnBootProperty.SetValue(localPlugin, true);
                    queueSaveMethod?.Invoke(dalamudConfig, null);
                }

                if (state != "Loaded")
                    await InvokeTask(loadAsyncMethod!.Invoke(localPlugin, new object[] { PluginLoadReason.Installer, false, CancellationToken.None }));
            }
            else
            {
                if (state == "Loaded")
                    await InvokeTask(unloadAsyncMethod!.Invoke(localPlugin, new[] { unloadDisposalMode! }));

                await InvokeTask(addOrUpdateMethod!.Invoke(profile, new object[] { workingId, plugin.InternalName, false, false }));
            }

            log.Info($"Set plugin state: {PluginKey.Get(plugin)} = {enabled}");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to set state for {plugin.InternalName} (Dev: {plugin.IsDev})");
            return false;
        }
    }

    private object? ResolveLocalPlugin(IExposedPlugin plugin)
    {
        if (installedPluginsProperty!.GetValue(pluginManager) is not IEnumerable installedPlugins)
            return null;

        object? fallbackMatch = null;
        foreach (var localPlugin in installedPlugins)
        {
            var manifest = manifestProperty!.GetValue(localPlugin);
            if (ReferenceEquals(manifest, plugin.Manifest))
                return localPlugin;

            if (fallbackMatch == null
                && internalNameProperty!.GetValue(localPlugin) as string == plugin.InternalName
                && localDevPluginType!.IsInstanceOfType(localPlugin) == plugin.IsDev)
            {
                fallbackMatch = localPlugin;
            }
        }

        return fallbackMatch;
    }

    private static async Task InvokeTask(object? result)
    {
        if (result is Task task)
            await task;
    }
}
