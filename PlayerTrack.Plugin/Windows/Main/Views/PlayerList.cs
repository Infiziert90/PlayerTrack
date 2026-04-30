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

public class PlayerList : PlayerTrackView, IViewWithPanel
{
    private readonly IMainPresenter Presenter;
    private readonly PlayerListComponent PlayerListComponent;
    private bool IsPendingSizeUpdate;
    private Vector2 LastSize;

    public PlayerList(string name, PluginConfig config, IMainPresenter presenter, ImGuiWindowFlags flags = ImGuiWindowFlags.None) : base(name, config, flags)
    {
        Presenter = presenter;
        PlayerListComponent = new PlayerListComponent(presenter);
        PlayerListComponent.OnPlayerListComponentOpenConfig += () => OnOpenConfig?.Invoke();
    }

    public delegate void OpenConfigDelegate();
    public delegate void OpenPanelViewDelegate();

    public event OpenConfigDelegate? OnOpenConfig;
    public event OpenPanelViewDelegate? OnOpenPanelView;

    public override void Draw()
    {
        UpdateWindowSizes();
        CheckResize();
        PlayerListComponent.Draw();
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

    public void HidePanel()
    {
        Config.PanelType = PanelType.None;
        IsPendingSizeUpdate = true;
    }

    public void ShowPanel(PanelType panelType)
    {
        Config.PanelType = panelType;
        OnOpenPanelView?.Invoke();
        IsOpen = true;
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
        if (ImGui.GetWindowSize() != LastSize)
        {
            LastSize = ImGui.GetWindowSize();
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
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(221f, 120f),
            MaximumSize = new Vector2(221f, 1000f),
        };
        Size = new Vector2(221f, Config.MainWindowHeight);
        SizeCondition = ImGuiCond.Always;
    }

    private void ValidateWindowConfig()
    {
        if (!Enum.IsDefined(Config.PanelType))
            Config.PanelType = PanelType.None;

        if (Config.MainWindowHeight is < 120f or > 1000f)
            Config.MainWindowHeight = 120f;
    }
}
