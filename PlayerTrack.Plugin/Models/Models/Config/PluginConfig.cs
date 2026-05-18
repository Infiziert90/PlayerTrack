using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Newtonsoft.Json;
using PlayerTrack.Data;

namespace PlayerTrack.Models;

public class PluginConfig : IPluginConfig
{
    public int FilterCategoryIndex { get; set; }

    public int FilterTagIndex { get; set; }

    public int FilterCategoryId { get; set; }

    public int FilterTagId { get; set; }

    public bool IsConfigOpen { get; set; } = true;

    public bool PreserveConfigState { get; set; }

    public bool PreserveMainWindowState { get; set; }

    public bool IsWindowCombined { get; set; } = true;

    public int PluginVersion { get; set; }

    public int LastVersionBackup { get; set; }

    public bool LodestoneEnableLookup { get; set; } = true;

    public LodestoneLocale LodestoneLocale { get; set; } = LodestoneLocale.NA;

    public float MainWindowHeight { get; set; } = 400f;

    public float MainWindowWidth { get; set; } = 700f;

    public PanelType PanelType { get; set; } = PanelType.None;

    [JsonIgnore] public PlayerConfig PlayerConfig { get; set; } = new(PlayerConfigType.Default);

    public PlayerListFilter PlayerListFilter { get; set; } = PlayerListFilter.AllPlayers;

    public SearchType SearchType { get; set; } = SearchType.Contains;

    public ConfigMenuOption SelectedConfigOption { get; set; } = ConfigMenuOption.Window;

    [JsonIgnore] public string SearchInput { get; set; } = string.Empty;

    public bool ShowOpenInPlayerTrack { get; set; } = true;

    public bool ShowOpenLodestone { get; set; } = true;

    public bool ShowPlayerFilter { get; set; } = true;

    public bool ShowPlayerCountInFilter { get; set; } = true;

    public bool ShowSearchBox { get; set; } = true;

    public bool ShowCategorySeparator { get; set; } = true;

    public bool SyncWithVisibility { get; set; }

    public long MaintenanceLastRunOn { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public int RecentPlayersThreshold { get; set; } = 900000;

    public bool UseCtrlNewLine { get; set; } = false;

    public List<FontAwesomeIcon> Icons { get; set; } =
    [
        FontAwesomeIcon.User,
        FontAwesomeIcon.GrinBeam,
        FontAwesomeIcon.GrinAlt,
        FontAwesomeIcon.Meh,
        FontAwesomeIcon.Frown,
        FontAwesomeIcon.Angry,
        FontAwesomeIcon.Flushed,
        FontAwesomeIcon.Surprise,
        FontAwesomeIcon.Tired
    ];

    public TrackingLocationConfig Overworld { get; set; } = new()
    {
        AddPlayers = true,
        AddEncounters = false,
    };

    public TrackingLocationConfig Content { get; set; } = new()
    {
        AddPlayers = true,
        AddEncounters = true,
    };

    public TrackingLocationConfig HighEndContent { get; set; } = new()
    {
        AddPlayers = true,
        AddEncounters = true,
    };

    public PlayerDataActionOptions PlayerDataActionOptions { get; set; } = new();

    public PlayerSettingsDataActionOptions PlayerSettingsDataActionOptions { get; set; } = new();

    public EncounterDataActionOptions EncounterDataActionOptions { get; set; } = new();

    public bool RunBackupBeforeDataActions { get; set; } = true;

    public int Id { get; set; }

    public long Created { get; set; }

    public long Updated { get; set; }

    public bool IsPluginEnabled { get; set; }

    public bool IsWindowSizeLocked { get; set; }

    public bool IsWindowPositionLocked { get; set; }

    public bool OnlyShowWindowWhenLoggedIn { get; set; }

    public NoCategoryPlacement NoCategoryPlacement { get; set; } = NoCategoryPlacement.Bottom;

    public bool UseCustomChatChannel { get; set; }

    public XivChatType CustomChatChannel { get; set; } = XivChatType.Notice;

    public TrackingLocationConfig GetTrackingLocationConfig(LocationType locType) => locType switch
    {
        LocationType.Overworld => Overworld,
        LocationType.Content => Content,
        LocationType.HighEndContent => HighEndContent,
        LocationType.None => throw new ArgumentException($"Unsupported location type: {locType}"),
        _ => throw new ArgumentException($"Unsupported location type: {locType}"),
    };

    public void ClearCategoryIds(int categoryId)
    {
        if (Overworld.DefaultCategoryId == categoryId)
            Overworld.DefaultCategoryId = 0;

        if (Content.DefaultCategoryId == categoryId)
            Content.DefaultCategoryId = 0;

        if (HighEndContent.DefaultCategoryId == categoryId)
            HighEndContent.DefaultCategoryId = 0;
    }
}
