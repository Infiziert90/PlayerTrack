using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Windows.Main.Components;
using PlayerTrack.Windows.Main.Presenters;
using PlayerTrack.Windows.Views;
using Dalamud.Interface.Windowing;
namespace PlayerTrack.Windows.Main.Views;

public class Combined : PlayerTrackView, IViewWithPanel
{
    private readonly PlayerListComponent PlayerListComponent;
    private readonly PanelComponent PanelComponent;
    private readonly IMainPresenter Presenter;
    private bool IsPendingSizeUpdate;
    private Vector2 LastSize;

    public Combined(string name, PluginConfig config, PlayerComponent playerComponent, AddPlayerComponent addPlayerComponent, IMainPresenter presenter, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        : base(name, config, flags)
    {
        Presenter = presenter;
        PlayerListComponent = new PlayerListComponent(Presenter);
        PlayerListComponent.OnPlayerListComponentOpenConfig += () => OnOpenConfig?.Invoke();
        PanelComponent = new PanelComponent(playerComponent, addPlayerComponent);
    }

    public delegate void OpenConfigDelegate();

    public event OpenConfigDelegate? OnOpenConfig;

    public override void Draw()
    {
        UpdateWindowSizes();
        CheckResize();
        PlayerListComponent.Draw();
        ImGui.SameLine();
        PanelComponent.Draw();
    }

    public override void Initialize()
    {
        SetWindowFlags();
        IsPendingSizeUpdate = true;
        UpdateWindowSizes();
        ValidateWindowConfig();
    }

    public void RefreshWindowConfig()
    {
        Config.PanelType = PanelType.None;
        Presenter.ClosePlayer();
        SetWindowFlags();
    }

    public void ShowPanel(PanelType panelType)
    {
        Config.PanelType = panelType;
        IsOpen = true;
        IsPendingSizeUpdate = true;
    }

    public void HidePanel()
    {
        Config.PanelType = PanelType.None;
        IsPendingSizeUpdate = true;
    }

    public void TogglePanel(PanelType panelType)
    {
        if (Config.PanelType == panelType)
            HidePanel();
        else
            ShowPanel(panelType);
    }

    private void CheckResize()
    {
        if (ImGui.GetWindowSize() != LastSize || IsPendingSizeUpdate)
        {
            LastSize = ImGui.GetWindowSize();
            if (Config.PanelType != PanelType.None)
                Config.MainWindowWidth = LastSize.X / ImGuiHelpers.GlobalScale;

            Config.MainWindowHeight = LastSize.Y / ImGuiHelpers.GlobalScale;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    private void UpdateWindowSizes()
    {
        if (!IsPendingSizeUpdate)
        {
            SizeCondition = ImGuiCond.FirstUseEver;
            return;
        }

        IsPendingSizeUpdate = false;
        if (Config.PanelType != PanelType.None)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(221f, 120f),
                MaximumSize = new Vector2(1000f, 1000f),
            };
            Size = new Vector2(Config.MainWindowWidth, Config.MainWindowHeight);
        }
        else
        {
            Config.PanelType = PanelType.None;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(221f, 120f),
                MaximumSize = new Vector2(221f, 1000f),
            };
            Size = new Vector2(221f, Config.MainWindowHeight);
        }

        SizeCondition = ImGuiCond.Always;
    }

    private void ValidateWindowConfig()
    {
        if (!Enum.IsDefined(Config.PanelType))
            Config.PanelType = PanelType.None;

        if (Config.MainWindowWidth is < 221f or > 1000f)
            Config.MainWindowWidth = 221f;

        if (Config.MainWindowHeight is < 120f or > 1000f)
            Config.MainWindowHeight = 120f;
    }
}
