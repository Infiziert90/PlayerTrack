using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FluentDapperLite.Runner;
using PlayerTrack.API;
using PlayerTrack.Domain;
using PlayerTrack.Extensions;
using PlayerTrack.Handler;
using PlayerTrack.Infrastructure;
using PlayerTrack.Resource;
using PlayerTrack.Windows;

namespace PlayerTrack;

public class Plugin : IDalamudPlugin
{
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ICondition ConditionHandler { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectCollection { get; private set; } = null!;
    [PluginService] public static IClientState ClientStateHandler { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGuiHandler { get; private set; } = null!;
    [PluginService] public static IFramework GameFramework { get; private set; } = null!;
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; set; } = null!;
    [PluginService] public static IGameInteropProvider HookManager { get; set; } = null!;
    [PluginService] public static IContextMenu ContextMenu { get; set; } = null!;
    [PluginService] public static INamePlateGui NamePlateGuiHandler { get; set; } = null!;
    [PluginService] public static IDataManager DataManager { get; set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; set; } = null!;

    public static WindowManager WindowManager { get; set; } = null!;

    public static SocialListHandler SocialListHandler { get; set; } = null!;
    public static PlayerLocationManager PlayerLocationManager { get; set; } = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        if (pluginInterface.IsDifferentVersionLoaded())
        {
            PluginLog.Error("Terminating plugin since another version of PlayerTrack is loaded.");
            return;
        }

        var isDatabaseLoadedSuccessfully = LoadDatabase();
        if (!isDatabaseLoadedSuccessfully)
            return;

        LanguageChanged(PluginInterface.UiLanguage);

        WindowManager = new WindowManager();
        SocialListHandler = new SocialListHandler();
        PlayerLocationManager = new PlayerLocationManager();

        RepositoryContext.Initialize(PluginInterface.GetPluginConfigDirectory());
        ServiceContext.Initialize();
        RunPostStartup();

        PluginInterface.LanguageChanged += LanguageChanged;
    }

    private PlayerTrackProvider? PlayerTrackProvider { get; set; }

    public void Dispose()
    {
        PluginLog.Verbose("Entering Plugin.Dispose()");
        GC.SuppressFinalize(this);
        try
        {
            PlayerTrackProvider?.Dispose();
            CommandHandler.Dispose();
            NameplateHandler.Dispose();
            EventDispatcher.Dispose();
            ContextMenuHandler.Dispose();
            GuiController.Dispose();
            ServiceContext.Dispose();
            RepositoryContext.Dispose();
            PlayerLocationManager.Dispose();
            SocialListHandler.Dispose();
            WindowManager.Dispose();

            PluginInterface.LanguageChanged -= LanguageChanged;
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Failed to dispose plugin.");
        }
    }

    /// <summary>
    /// Sets the language to be used for loc.
    /// </summary>
    private void LanguageChanged(string langCode)
    {
        var culture = new CultureInfo(langCode);

        Language.Culture = culture;
        Utils.CurrentCulture = culture;
    }

    private static void SetPluginVersion()
    {
        PluginLog.Verbose("Entering Plugin.SetPluginVersion()");
        var pluginVersion = PluginInterface.GetPluginVersion();
        var config = ServiceContext.ConfigService.GetConfig();
        config.PluginVersion = pluginVersion;
        ServiceContext.ConfigService.SaveConfig(config);
    }

    private static bool LoadDatabase()
    {
        try
        {
            PluginLog.Verbose("Entering Plugin.LoadDatabase()");
            var dataSource = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "data.db");
            SQLiteFluentMigratorRunner.Run(dataSource, Assembly.GetExecutingAssembly());
            return true;
        }
        catch (Exception exception)
        {
            // Log the error to the console and then return false,
            // originally, this method would not return anything if an exception thrown.
            // This method still has the same result if an uncaught error was thrown.
            PluginLog.Error(exception, "Failed to load database.");
            return false;
        }
    }

    private void RunPostStartup() => Task.Run(() =>
    {
        PluginLog.Verbose("Entering Plugin.RunPostStartup()");
        EncounterService.EnsureNoOpenEncounters();
        ServiceContext.LodestoneService.Start();
        ServiceContext.ConfigService.SyncIcons();
        ServiceContext.PlayerCacheService.LoadPlayers();
        ServiceContext.VisibilityService.Initialize();
        SetPluginVersion();
        ServiceContext.BackupService.Startup();
        GuiController.Start();
        ContextMenuHandler.Start();
        EventDispatcher.Start();
        NameplateHandler.Start();
        CommandHandler.Start();
        PlayerLocationManager.Start();
        SocialListHandler.Start();
        ServiceContext.PlayerProcessService.Start();
        PlayerTrackProvider = new PlayerTrackProvider(PluginInterface, new PlayerTrackAPI());
    });
}
