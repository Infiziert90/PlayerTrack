using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Config.Components;
using PlayerTrack.Windows.Views;

namespace PlayerTrack.Windows.Config.Views;

public class ConfigView : PlayerTrackView, IDisposable
{
    private readonly WindowComponent WindowComponent = new();
    private readonly ContextMenuComponent ContextMenuComponent = new();
    private readonly IconComponent IconComponent = new();
    private readonly TagComponent TagComponent = new();
    private readonly PlayerDefaultsComponent PlayerDefaultsComponent = new();
    private readonly CategoryComponent CategoryComponent = new();
    private readonly CategorizerComponent CategorizerComponent = new();
    private readonly LocationComponent LocationComponent = new();
    private readonly SocialListComponent SocialListComponent = new();
    private readonly IntegrationComponent IntegrationComponent = new();
    private readonly BackupComponent BackupComponent = new();
    private readonly DataComponent DataComponent = new();
    private readonly ContributeComponent ContributeComponent = new();
    private readonly HelpComponent HelpComponent = new();
    private readonly AboutComponent AboutComponent = new();
    private float NavMaxWidth;
    private bool IsLanguageChanged = true;

    public ConfigView(string name, PluginConfig config, ImGuiWindowFlags flags = ImGuiWindowFlags.None) : base(name, config, flags)
    {
        Size = new Vector2(820f, 450f);
        SizeCondition = ImGuiCond.Appearing;
        BackupComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        CategoryComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        ContextMenuComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        ContextMenuComponent.UpdateContextMenu += () => OnContextMenuUpdated?.Invoke();
        IconComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        IntegrationComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        PlayerDefaultsComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        TagComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        LocationComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        SocialListComponent.OnPlayerConfigChanged += () => OnPlayerConfigChanged?.Invoke();
        WindowComponent.WindowConfigComponent_WindowConfigChanged += () => OnWindowConfigChanged?.Invoke();
        Plugin.PluginInterface.LanguageChanged += _ => IsLanguageChanged = true;
    }

    public delegate void WindowConfigChangedDelegate();
    public delegate void PlayerConfigChangedDelegate();
    public delegate void ContextMenuUpdatedDelegate();

    public event WindowConfigChangedDelegate? OnWindowConfigChanged;
    public event PlayerConfigChangedDelegate? OnPlayerConfigChanged;
    public event ContextMenuUpdatedDelegate? OnContextMenuUpdated;

    public ConfigMenuOption SelectedMenuOption { get; set; }

    public string[] ConfigMenuOptions { get; set; } = null!;

    public override void Initialize()
    {
        ConfigMenuOptions = Enum.GetNames<ConfigMenuOption>();
        SelectedMenuOption = Config.SelectedConfigOption;
        DataComponent.Initialize();
    }

    public override void OnOpen()
    {
        Config.IsConfigOpen = true;
        ServiceContext.ConfigService.SaveConfig(Config);
    }

    public override void OnClose()
    {
        Config.IsConfigOpen = false;
        ServiceContext.ConfigService.SaveConfig(Config);
    }

    public void CalcSize()
    {
        float maxWidth = 0;
        foreach (var key in ConfigMenuOptions)
        {
            var translatedString = Utils.GetLoc(key);
            var stringSize = ImGui.CalcTextSize(translatedString);
            if (stringSize.X > maxWidth)
                maxWidth = stringSize.X;
        }

        NavMaxWidth = (maxWidth + 20) * ImGuiHelpers.GlobalScale;
        BackupComponent.CalcSize();
    }

    public override void Draw()
    {
        if (IsLanguageChanged)
        {
            CalcSize();
            IsLanguageChanged = false;
        }

        using (var child = ImRaii.Child("###Config_Navigation", ImGuiHelpers.ScaledVector2(NavMaxWidth, 0), true))
        {
            if (child.Success)
            {
                for (var i = 0; i < ConfigMenuOptions.Length; i++)
                {
                    if (ImGui.Selectable(Utils.GetLoc(ConfigMenuOptions[i]), (int)SelectedMenuOption == i))
                    {
                        SelectedMenuOption = (ConfigMenuOption)i;
                        Config.SelectedConfigOption = SelectedMenuOption;
                    }
                }
            }
        }
        ImGui.SameLine();

        using (ImRaii.Group())
        {
            switch (SelectedMenuOption)
            {
                case ConfigMenuOption.Window:
                    WindowComponent.Draw();
                    break;
                case ConfigMenuOption.ContextMenu:
                    ContextMenuComponent.Draw();
                    break;
                case ConfigMenuOption.Icons:
                    IconComponent.Draw();
                    break;
                case ConfigMenuOption.Tags:
                    TagComponent.Draw();
                    break;
                case ConfigMenuOption.PlayerDefaults:
                    PlayerDefaultsComponent.Draw();
                    break;
                case ConfigMenuOption.Categories:
                    CategoryComponent.Draw();
                    break;
                case ConfigMenuOption.Categorizer:
                    CategorizerComponent.Draw();
                    break;
                case ConfigMenuOption.Locations:
                    LocationComponent.Draw();
                    break;
                case ConfigMenuOption.SocialLists:
                    SocialListComponent.Draw();
                    break;
                case ConfigMenuOption.Integrations:
                    IntegrationComponent.Draw();
                    break;
                case ConfigMenuOption.Backups:
                    BackupComponent.Draw();
                    break;
                case ConfigMenuOption.Data:
                    DataComponent.Draw();
                    break;
                case ConfigMenuOption.Contribute:
                    ContributeComponent.Draw();
                    break;
                case ConfigMenuOption.Help:
                    HelpComponent.Draw();
                    break;
                case ConfigMenuOption.About:
                    AboutComponent.Draw();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Open(ConfigMenuOption configMenuOption)
    {
        SelectedMenuOption = configMenuOption;
        ServiceContext.ConfigService.SaveConfig(Config);
        IsOpen = true;
    }

    public void Dispose()
    {
        CategoryComponent.Dispose();
        PlayerDefaultsComponent.Dispose();
        TagComponent.Dispose();
        LocationComponent.Dispose();
        SocialListComponent.Dispose();
        IntegrationComponent.Dispose();
        BackupComponent.Dispose();
        GC.SuppressFinalize(this);
    }
}
