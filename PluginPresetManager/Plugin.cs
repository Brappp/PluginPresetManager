using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PluginPresetManager.Windows;
using Dalamud.Interface.ImGuiNotification;

namespace PluginPresetManager;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ppreset";
    private const string CommandNameShort = "/ppm";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    public PresetStorage Storage { get; init; }
    public PresetManager PresetManager { get; init; }

    public readonly WindowSystem WindowSystem = new("PluginPresetManager");
    private MainWindow MainWindow { get; init; }

    private bool defaultPresetApplied = false;
    private bool needsInitialCharacterTracking = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        Configuration.Migrate();
        PluginInterface.SavePluginConfig(Configuration);
        
        Storage = new PresetStorage(PluginInterface, Log);

        PresetManager = new PresetManager(
            PluginInterface,
            CommandManager,
            ChatGui,
            NotificationManager,
            Log,
            Configuration,
            Storage);

        var thisPluginInternalName = PluginInterface.InternalName;
        if (!PresetManager.GetAlwaysOnPlugins().Contains(thisPluginInternalName))
        {
            Log.Info("Adding PluginPresetManager to always-on list to prevent self-disable");
            PresetManager.AddAlwaysOnPlugin(thisPluginInternalName);
        }

        if (ClientState.IsLoggedIn)
        {
            Log.Info("Already logged in, will track character on next frame");
            needsInitialCharacterTracking = true;
            Framework.Update += OnFrameworkUpdate;
        }
        else
        {
            ClientState.Login += OnLogin;
            Log.Info("Waiting for login to track character and apply default preset");
        }

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Plugin Preset Manager window"
        });

        CommandManager.AddHandler(CommandNameShort, new CommandInfo(OnCommandShort)
        {
            HelpMessage = "Apply a preset by name or 'alwayson' to disable all except always-on. Usage: /ppm <preset name|alwayson>"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Info($"Plugin Preset Manager loaded successfully");
    }

    public void Dispose()
    {
        ClientState.Login -= OnLogin;
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameShort);

        Log.Info("Plugin Preset Manager disposed");
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnCommandShort(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            MainWindow.Toggle();
            return;
        }

        var argument = args.Trim();

        if (argument.Equals("alwayson", StringComparison.OrdinalIgnoreCase))
        {
            Log.Info("Applying always-on only mode via command");
            _ = PresetManager.ApplyAlwaysOnOnlyAsync();
            return;
        }

        var allPresets = PresetManager.GetAllPresets();
        var preset = allPresets.FirstOrDefault(p =>
            p.Name.Equals(argument, StringComparison.OrdinalIgnoreCase));

        if (preset != null)
        {
            Log.Info($"Applying preset '{preset.Name}' via command");
            _ = PresetManager.ApplyPresetAsync(preset);
        }
        else
        {
            NotificationManager.AddNotification(new Notification
            {
                Content = $"Preset '{argument}' not found",
                Type = NotificationType.Error,
                Title = "Preset Manager"
            });
            if (allPresets.Any())
            {
                ChatGui.Print("[Preset] Available presets:");
                foreach (var p in allPresets)
                {
                    ChatGui.Print($"  - {p.Name}");
                }
                ChatGui.Print("[Preset] Special commands:");
                ChatGui.Print("  - alwayson (disable everything except always-on plugins)");
            }
            else
            {
                ChatGui.Print("[Preset] No presets available. Use /ppreset to create one.");
                ChatGui.Print("[Preset] Special commands:");
                ChatGui.Print("  - alwayson (disable everything except always-on plugins)");
            }
        }
    }

    public void ToggleMainUi() => MainWindow.Toggle();

    private void OpenConfigUi()
    {
        MainWindow.FocusSettingsTab();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (needsInitialCharacterTracking)
        {
            needsInitialCharacterTracking = false;
            Framework.Update -= OnFrameworkUpdate;

            TrackCurrentCharacter();

            if (Configuration.DefaultPresetId.HasValue)
            {
                Log.Info("Applying default preset");
                ApplyDefaultPreset();
            }
        }
    }

    private void TrackCurrentCharacter()
    {
        var contentId = ClientState.LocalContentId;
        if (contentId != 0)
        {
            var characterName = $"{ClientState.LocalPlayer?.Name}@{ClientState.LocalPlayer?.HomeWorld.Value.Name}";
            Configuration.CharacterNames[contentId] = characterName;
            PluginInterface.SavePluginConfig(Configuration);
            Log.Info($"Tracked character: {characterName} (ID: {contentId})");
        }
    }

    private void OnLogin()
    {
        Log.Info("Character logged in");
        TrackCurrentCharacter();
        ApplyDefaultPreset();
    }
    
    private void ApplyDefaultPreset()
    {
        if (defaultPresetApplied)
            return;
            
        defaultPresetApplied = true;
        ClientState.Login -= OnLogin;
        
        Guid? presetIdToApply = null;
        
        if (Configuration.UseCharacterSpecificDefaults && ClientState.LocalContentId != 0)
        {
            if (Configuration.CharacterDefaultPresets.TryGetValue(ClientState.LocalContentId, out var characterPresetId))
            {
                presetIdToApply = characterPresetId;
                Log.Info($"Using character-specific default preset for {ClientState.LocalPlayer?.Name}");
            }
        }
        
        if (!presetIdToApply.HasValue)
        {
            presetIdToApply = Configuration.DefaultPresetId;
            if (presetIdToApply.HasValue)
            {
                Log.Info("Using global default preset");
            }
        }
        
        if (!presetIdToApply.HasValue)
            return;
        
        var defaultPreset = PresetManager.GetAllPresets()
            .FirstOrDefault(p => p.Id == presetIdToApply.Value);
            
        if (defaultPreset != null)
        {
            Log.Info($"Auto-applying default preset: {defaultPreset.Name}");
            _ = PresetManager.ApplyPresetAsync(defaultPreset);
        }
        else
        {
            Log.Warning($"Default preset ID {presetIdToApply.Value} not found");
        }
    }
}
