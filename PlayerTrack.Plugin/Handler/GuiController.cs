using System;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Windows.Config.Views;
using PlayerTrack.Windows.Main.Presenters;
using PlayerTrack.Windows.Main.Views;

namespace PlayerTrack.Handler;

public static class GuiController
{
    private const string Name = "PlayerTrack";
    private static ConfigView? ConfigView;
    private static PluginConfig Config = null!;

    private static MainPresenter Presenter = null!;
    private static Combined? CombinedView;
    private static PlayerList? PlayerListView;
    private static PanelView? PanelView;
    private static bool IsCombinedView = true;

    public static void Start()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.Start()");
        Initialize();
        ConfigView = new ConfigView($"{Name}###Config", Config) { IsOpen = Config is { IsConfigOpen: true, PreserveConfigState: true } };
        ConfigView.OnWindowConfigChanged += WindowConfigChanged;
        ConfigView.OnPlayerConfigChanged += Presenter.ReloadPlayer;
        ConfigView.OnPlayerConfigChanged += NameplateHandler.RefreshNameplates;
        ConfigView.OnContextMenuUpdated += ContextMenuHandler.Restart;
        Plugin.WindowManager.AddWindows(ConfigView);
        if (Config.IsWindowCombined)
        {
            StartCombinedWindow();
            IsCombinedView = true;
        }
        else
        {
            StartSeparateWindows();
            IsCombinedView = false;
        }

        Plugin.PluginInterface.UiBuilder.OpenMainUi += OnPlayerWindowToggled;
        Plugin.PluginInterface.UiBuilder.OpenConfigUi += OnConfigWindowToggled;
        Plugin.WindowManager.Enable();
    }

    public static void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.Dispose()");
        try
        {
            Plugin.PluginInterface.UiBuilder.OpenMainUi -= OnPlayerWindowToggled;
            Plugin.PluginInterface.UiBuilder.OpenConfigUi -= OnConfigWindowToggled;
            CommandHandler.OnConfigWindowToggled -= OnConfigWindowToggled;
            CommandHandler.OnPlayerWindowToggled -= OnPlayerWindowToggled;
            ServiceContext.PlayerProcessService.OnPlayerSelected -= OnPlayerSelected;
            if (CombinedView != null)
                CombinedView.OnOpenConfig -= OpenConfig;

            if (ConfigView == null)
                return;

            ConfigView.OnWindowConfigChanged -= WindowConfigChanged;
            ConfigView.OnPlayerConfigChanged -= Presenter.ReloadPlayer;
            ConfigView.OnPlayerConfigChanged -= NameplateHandler.RefreshNameplates;
            ConfigView.OnContextMenuUpdated -= ContextMenuHandler.Restart;
        }
        catch (Exception)
        {
            Plugin.PluginLog.Warning("Failed to dispose GuiController.");
        }
    }

    private static void StartCombinedWindow()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.StartCombinedWindow()");
        CombinedView = Presenter.Combined;
        CombinedView.OnOpenConfig += OpenConfig;
        Plugin.WindowManager.AddWindows(CombinedView);
    }

    private static void CloseCombinedWindow()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.CloseCombinedWindow()");
        if (CombinedView == null)
            return;

        CombinedView.OnOpenConfig -= OpenConfig;
        Plugin.WindowManager.RemoveWindows(CombinedView);
    }

    private static void StartSeparateWindows()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.StartSeparateWindows()");
        PlayerListView = Presenter.PlayerList;
        PlayerListView.OnOpenConfig += OpenConfig;
        PanelView = Presenter.PanelView;
        Plugin.WindowManager.AddWindows(PlayerListView, PanelView);
    }

    private static void CloseSeparateWindows()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.CloseSeparateWindows()");
        if (PlayerListView == null || PanelView == null)
            return;

        PlayerListView.OnOpenConfig -= OpenConfig;
        Plugin.WindowManager.RemoveWindows(PlayerListView, PanelView);
    }

    private static void OpenConfig() => ConfigView?.Toggle();

    private static void WindowConfigChanged()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.WindowConfigChanged()");
        if (IsCombinedView && !Config.IsWindowCombined)
        {
            CloseCombinedWindow();
            StartSeparateWindows();
            IsCombinedView = false;
            PanelView!.IsOpen = true;
            PlayerListView!.IsOpen = true;
        }
        else if (!IsCombinedView && Config.IsWindowCombined)
        {
            CloseSeparateWindows();
            StartCombinedWindow();
            IsCombinedView = true;
            CombinedView!.IsOpen = true;
        }

        CombinedView?.RefreshWindowConfig();
        PlayerListView?.RefreshWindowConfig();
        PanelView?.ResetWindow();
    }

    private static void Initialize()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.Initialize()");
        ServiceContext.ConfigService.GetConfig().PanelType = PanelType.None;
        Config = ServiceContext.ConfigService.GetConfig();
        Presenter = new MainPresenter { OnConfigMenuOptionSelected = configMenuOption => ConfigView?.Open(configMenuOption), };
        ServiceContext.PlayerCacheService.OnCacheUpdated += Presenter.ClearCache;
        ServiceContext.PlayerProcessService.OnPlayerSelected += OnPlayerSelected;
        CommandHandler.OnConfigWindowToggled += OnConfigWindowToggled;
        CommandHandler.OnPlayerWindowToggled += OnPlayerWindowToggled;
    }

    private static void OnPlayerSelected(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering GuiController.OnPlayerSelected(): {player.Id}");
        Presenter.SelectPlayer(player);
        Presenter.ShowPanel(PanelType.Player);
    }

    private static void OnConfigWindowToggled()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.OnConfigWindowToggled()");
        ConfigView?.Toggle();
    }

    private static void OnPlayerWindowToggled()
    {
        Plugin.PluginLog.Verbose("Entering GuiController.OnPlayerWindowToggled()");
        if (IsCombinedView)
        {
            CombinedView?.Toggle();
        }
        else
        {
            PlayerListView?.Toggle();
            PanelView?.Toggle();
        }
    }
}
