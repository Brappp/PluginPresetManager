using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PluginPresetManager.Windows;
using Dalamud.Interface;
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
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;

    private IDtrBarEntry? dtrBarEntry;

    public Configuration Configuration { get; init; }
    public CharacterStorage CharacterStorage { get; init; }
    public PresetManager PresetManager { get; init; }

    public readonly WindowSystem WindowSystem = new("PluginPresetManager");
    private MainWindow MainWindow { get; init; }
    private DtrPopupWindow DtrPopupWindow { get; init; }

    private ulong lastDefaultPresetAppliedForCharacter = 0;
    private int characterInitAttempts = 0;
    private const int MaxCharacterInitAttempts = 100;

    public ulong ActiveContentId { get; private set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Configuration.Migrate();
        PluginInterface.SavePluginConfig(Configuration);

        CharacterStorage = new CharacterStorage(PluginInterface, Log);

        PresetManager = new PresetManager(
            PluginInterface,
            CommandManager,
            ChatGui,
            NotificationManager,
            Framework,
            Log,
            Configuration,
            CharacterStorage);

        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        if (ClientState.IsLoggedIn)
        {
            Framework.RunOnFrameworkThread(InitializeCharacter);
        }
        else
        {
            Log.Info("Not logged in, will initialize on character login");
        }

        MainWindow = new MainWindow(this);
        DtrPopupWindow = new DtrPopupWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DtrPopupWindow);

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

        if (Configuration.ShowDtrBar)
        {
            InitializeDtrBar();
        }
        Framework.Update += OnFrameworkUpdate;

        Log.Info($"Plugin Preset Manager loaded successfully");
    }

    private void InitializeDtrBar()
    {
        if (dtrBarEntry != null)
            return;

        try
        {
            dtrBarEntry = DtrBar.Get("Preset Manager");
            dtrBarEntry.OnClick = _ => DtrPopupWindow.Toggle();
            UpdateDtrBarText();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize DTR bar entry");
        }
    }

    private void RemoveDtrBar()
    {
        if (dtrBarEntry != null)
        {
            dtrBarEntry.Remove();
            dtrBarEntry = null;
        }
    }

    public void UpdateDtrBarVisibility()
    {
        if (Configuration.ShowDtrBar)
        {
            InitializeDtrBar();
        }
        else
        {
            RemoveDtrBar();
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (dtrBarEntry == null || dtrBarEntry.UserHidden || !Configuration.ShowDtrBar)
                return;

            UpdateDtrBarText();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating DTR bar");
        }
    }

    private void UpdateDtrBarText()
    {
        if (dtrBarEntry == null)
            return;

        string presetText;

        if (PresetManager.IsApplying)
        {
            presetText = "...";
        }
        else if (PresetManager.WasLastAppliedAlwaysOn)
        {
            presetText = "Always-On";
        }
        else
        {
            var lastPreset = PresetManager.GetLastAppliedPreset();
            presetText = lastPreset?.Name ?? "None";
        }

        dtrBarEntry.Text = new SeString(new TextPayload($"PPM: {presetText}"));
    }

    public void Dispose()
    {
        PresetManager.CancelApply();

        Framework.Update -= OnFrameworkUpdate;

        RemoveDtrBar();

        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;

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

        var preset = PresetManager.GetPresetByName(argument);

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

            var allPresets = PresetManager.GetAllPresets();
            var sharedPresets = PresetManager.GetSharedPresets();
            if (allPresets.Any() || sharedPresets.Any())
            {
                ChatGui.Print("[Preset] Available presets:");
                foreach (var p in allPresets)
                {
                    ChatGui.Print($"  - {p.Name}");
                }
                foreach (var p in sharedPresets)
                {
                    ChatGui.Print($"  - {p.Name} (shared)");
                }
            }
            else
            {
                ChatGui.Print("[Preset] No presets available. Use /ppreset to create one.");
            }
            ChatGui.Print("[Preset] Special commands:");
            ChatGui.Print("  - alwayson (disable everything except always-on plugins)");
        }
    }

    public void ToggleMainUi() => MainWindow.Toggle();

    private void OpenConfigUi()
    {
        MainWindow.OpenSettings();
    }

    private void EnsureAlwaysOn()
    {
        var selfKey = PresetManager.SelfKey;
        if (!PresetManager.GetEffectiveAlwaysOnPlugins().Contains(selfKey))
        {
            Log.Info("Adding PluginPresetManager to always-on list to prevent self-disable");
            PresetManager.AddAlwaysOnPlugin(selfKey);
        }
    }

    private void OnLogin()
    {
        characterInitAttempts = 0;
        Framework.RunOnFrameworkThread(InitializeCharacter);
    }

    private void InitializeCharacter()
    {
        if (!ClientState.IsLoggedIn)
            return;

        var contentId = PlayerState.ContentId;
        var localPlayer = ObjectTable.LocalPlayer;

        if (contentId == 0 || localPlayer == null)
        {
            if (characterInitAttempts++ < MaxCharacterInitAttempts)
            {
                Framework.RunOnTick(InitializeCharacter, delayTicks: 30);
            }
            else
            {
                Log.Warning("Could not resolve logged-in character; presets will load when the window is opened");
            }
            return;
        }

        var name = localPlayer.Name.ToString();
        var world = localPlayer.HomeWorld.ValueNullable?.Name.ToString() ?? "";

        ActiveContentId = contentId;
        PresetManager.SwitchCharacter(contentId, name, world);
        Log.Info($"Character logged in: {name} @ {world}");

        EnsureAlwaysOn();
        ApplyDefaultPreset();
    }

    private void OnLogout(int type, int code)
    {
        Log.Info("Character logged out, resetting state");
        lastDefaultPresetAppliedForCharacter = 0;
        characterInitAttempts = 0;
        ActiveContentId = 0;
        PresetManager.ClearCharacter();
    }

    private async void ApplyDefaultPreset()
    {
        var currentCharacterId = PlayerState.ContentId;
        if (currentCharacterId == 0 || lastDefaultPresetAppliedForCharacter == currentCharacterId)
            return;

        if (!PresetManager.ApplyDefaultOnLogin)
            return;

        if (PresetManager.IsApplying)
        {
            Log.Info("Skipping default apply: another apply is already in progress");
            return;
        }

        if (PresetManager.UseAlwaysOnAsDefault)
        {
            lastDefaultPresetAppliedForCharacter = currentCharacterId;

            try
            {
                Log.Info("Auto-applying Always-On Only mode");
                await PresetManager.ApplyAlwaysOnOnlyAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to auto-apply Always-On Only mode");
            }
            return;
        }

        var defaultPresetName = PresetManager.DefaultPreset;
        if (string.IsNullOrEmpty(defaultPresetName))
            return;

        var defaultPreset = PresetManager.GetPresetByName(defaultPresetName);
        if (defaultPreset == null)
        {
            Log.Warning($"Default preset '{defaultPresetName}' not found");
            return;
        }

        lastDefaultPresetAppliedForCharacter = currentCharacterId;

        try
        {
            Log.Info($"Auto-applying default preset: {defaultPreset.Name}");
            await PresetManager.ApplyPresetAsync(defaultPreset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to auto-apply default preset '{defaultPreset.Name}'");
        }
    }

    public void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }
}
