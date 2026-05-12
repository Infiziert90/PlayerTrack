using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Extensions;
using PlayerTrack.Models;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;

namespace PlayerTrack.Windows.Main.Components;

public class PlayerListComponent : ViewComponent
{
    private const int DebounceTime = 1000;
    private readonly IMainPresenter Presenter;
    private readonly PlayerAdvancedFilterComponent AdvancedFilter;
    private List<Category> Categories = null!;
    private List<string> CategoryNames = null!;
    private bool PendingFilterUpdate;
    private long LastInputTime;
    private bool IsSearchDirty;

    public PlayerListComponent(IMainPresenter presenter)
    {
        Presenter       = presenter;
        AdvancedFilter  = new PlayerAdvancedFilterComponent(presenter);
    }

    public delegate void PlayerListComponentOpenConfigDelegate();

    public event PlayerListComponentOpenConfigDelegate? OnPlayerListComponentOpenConfig;

    public override void Draw()
    {
        if (!PlayerSearchService.IsValidSearch(Config.SearchInput))
        {
            Presenter.ClearCache();

            using var leftChild = ImRaii.Child("###LeftPanel", new Vector2(205 * ImGuiHelpers.GlobalScale, 0), false);
            if (leftChild.Success)
                DrawControls(0);

            return;
        }

        if (PendingFilterUpdate || (IsSearchDirty && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastInputTime > DebounceTime))
        {
            PendingFilterUpdate = false;
            IsSearchDirty = false;
            ServiceContext.ConfigService.SaveConfig(Config);
            ServiceContext.PlayerCacheService.Resort();
            Presenter.ClearCache();
        }

        Categories = ServiceContext.CategoryService.GetCategories(false);
        CategoryNames = ServiceContext.CategoryService.GetCategoryNames(false, false);
        var playersCount = Presenter.GetPlayersCount();

        using var panelChild = ImRaii.Child("###LeftPanel", new Vector2(205 * ImGuiHelpers.GlobalScale, 0), false);
        if (!panelChild.Success)
            return;

        DrawControls(playersCount);

        using var listChild = ImRaii.Child("###PlayerList", new Vector2(205 * ImGuiHelpers.GlobalScale, 0), true);
        if (!listChild.Success)
            return;

        if (playersCount == 0)
        {
            ImGui.TextUnformatted("");
            return;
        }

        var shouldShowSeparator = Config.ShowCategorySeparator
                                   && string.IsNullOrEmpty(Config.SearchInput)
                                   && ServiceContext.PlayerCacheService.GetSortType() == PlayerSortType.ByCategory;

        using var clipper = new ListClipper(playersCount);
        while (clipper.Step())
        {
            var players = Presenter.GetPlayers(clipper.DisplayStart, clipper.DisplayEnd);
            var anyPlayerDrawn = false;

            for (var i = 0; i < players.Count; i++)
            {
                using var group = ImRaii.Group();

                var player = players[i];
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (player != null)
                {
                    if (shouldShowSeparator && i > 0 && i < players.Count && player.PrimaryCategoryId != players[i - 1].PrimaryCategoryId)
                        ImGui.Separator();

                    DrawPlayer(player);
                    anyPlayerDrawn = true;
                }
            }

            if (!anyPlayerDrawn)
                ImGui.TextUnformatted("");
        }
    }


    private void OnPlayerFilterSelect(PlayerListFilter playerListFilter)
    {
        Config.PlayerListFilter = playerListFilter;
        PendingFilterUpdate = true;
    }

    private void DrawControls(int playersCount)
    {
        using (ImRaii.Group())
        {
            // Player Filter
            if (Config.ShowPlayerFilter)
            {
                var playerListFilter = Config.PlayerListFilter;
                if (Config.ShowPlayerCountInFilter)
                {
                    var playerCountString = $"({playersCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)})";
                    if (Helper.Combo("###PlayerList_Filter", ref playerListFilter, playerCountString))
                        OnPlayerFilterSelect(playerListFilter);
                }
                else
                {
                    if (Helper.Combo("###PlayerList_Filter", ref playerListFilter, -1, false))
                        OnPlayerFilterSelect(playerListFilter);
                }
            }

            // Category Filter
            if (Config.PlayerListFilter == PlayerListFilter.PlayersByCategory)
            {
                var categoryFilters = ServiceContext.CategoryService.GetCategoryFilters();
                using var disabled = ImRaii.Disabled(categoryFilters.TotalFilters == 0);

                var filterCategoryIndex = Config.FilterCategoryIndex;
                if (Helper.Combo("###PlayerList_CategoryFilter", ref filterCategoryIndex, categoryFilters.FilterNames, -1, false))
                {
                    Config.FilterCategoryIndex = filterCategoryIndex;
                    Config.FilterCategoryId = categoryFilters.FilterIds[Config.FilterCategoryIndex];
                    ServiceContext.ConfigService.SaveConfig(Config);
                    Presenter.ClearCache();
                }
            }

            // Tag Filter
            if (Config.PlayerListFilter == PlayerListFilter.PlayersByTag)
            {
                var tagFilters = ServiceContext.TagService.GetTagFilters();
                using var disabled = ImRaii.Disabled(tagFilters.TotalFilters == 0);

                var filterTagIndex = Config.FilterTagIndex;
                if (Helper.Combo("###PlayerList_TagFilter", ref filterTagIndex, tagFilters.FilterNames, -1, false))
                {
                    Config.FilterTagIndex = filterTagIndex;
                    Config.FilterTagId = tagFilters.FilterIds[Config.FilterTagIndex];
                    ServiceContext.ConfigService.SaveConfig(Config);
                    Presenter.ClearCache();
                }
            }

            // Search Box
            if (Config.ShowSearchBox)
            {
                var searchInput = Config.SearchInput;

                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("###PlayerList_Search", Language.SearchPlayersHint, ref searchInput, 1000))
                {
                    // Reset logic for empty input
                    if (string.IsNullOrWhiteSpace(searchInput))
                    {
                        Config.SearchInput = string.Empty;
                        IsSearchDirty = true;
                        PendingFilterUpdate = true;
                        Presenter.ClearCache();
                    }
                    else if (IsValidInput(searchInput))
                    {
                        Config.SearchInput = searchInput;
                        LastInputTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        if (PlayerSearchService.IsValidSearch(Config.SearchInput))
                        {
                            IsSearchDirty = true;
                        }
                        else
                        {
                            IsSearchDirty = false;
                            PendingFilterUpdate = false;
                        }
                    }
                }

                if (IsSearchDirty && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastInputTime > DebounceTime)
                {
                    IsSearchDirty = false;
                    PendingFilterUpdate = true;
                }
            }

            // Dummy spacing if nothing available so can still open menu
            if (Config is { ShowSearchBox: false, ShowPlayerFilter: false })
                ImGuiHelpers.ScaledDummy(new Vector2(Config.MainWindowWidth, 5f));

            // Advanced sort / filter controls
            AdvancedFilter.Draw();
        }

        // Open config popup on right click
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup("###PlayerTrack_Menu_Popup");

        // Config menu popup
        using var popup = ImRaii.Popup("###PlayerTrack_Menu_Popup", ImGuiWindowFlags.Popup);
        if (!popup.Success)
            return;

        if (ImGui.MenuItem(Language.AddPlayer))
            Presenter.TogglePanel(PanelType.AddPlayer);

        if (ImGui.MenuItem(Language.OpenSettings))
            OnPlayerListComponentOpenConfig?.Invoke();
    }


    private static bool IsValidInput(string input)
    {
        var colonIndex = input.IndexOf(':');
        if (colonIndex == input.Length - 1)
            return false;

        if (colonIndex == -1)
            return !input.Contains('!') && !input.Contains('*');

        for (var i = 0; i < colonIndex; i++)
            if (input[i] == '!' || input[i] == '*')
                return false;

        return true;
    }

    private void DrawPlayer(Player player)
    {
        bool isSelected;
        using (ImRaii.Group())
        {
            isSelected = Presenter.GetSelectedPlayer()?.Id == player.Id;
            if (ImGui.Selectable($"###PlayerSelectable{player.Id}", isSelected))
            {
                if (isSelected && Config.PanelType == PanelType.Player)
                {
                    // hide right subWindow if clicking same user while already open
                    Presenter.ClosePlayer();
                    Presenter.HidePanel();
                }
                else
                {
                    // open selectedPlayer in right subWindow
                    Presenter.SelectPlayer(player);
                    Presenter.ShowPanel(PanelType.Player);
                }
            }

            ImGuiHelpers.ScaledRelativeSameLine(-10f / ImGuiHelpers.GlobalScale);

            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextColored(player.PlayerListNameColor, player.PlayerListIconString);

            ImGui.SameLine();
            Helper.TextColored(player.PlayerListNameColor, player.Name);
        }

        // open menu options for selected player
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"###PlayerList_Popup_{player.Id}");

        // pop up for selected player
        using var popup = ImRaii.Popup($"###PlayerList_Popup_{player.Id}");
        if (!popup.Success)
            return;

        if (isSelected)
        {
            if (ImGui.MenuItem(Language.ClosePlayer))
            {
                Presenter.ClosePlayer();
                Presenter.HidePanel();
            }
        }
        else
        {
            if (ImGui.MenuItem(Language.OpenPlayer))
            {
                Presenter.SelectPlayer(player);
                Presenter.ShowPanel(PanelType.Player);
            }
        }

        if (player.IsCurrent)
        {
            if (ImGui.MenuItem(Language.TargetPlayer))
                Plugin.TargetManager.SetTarget(player.EntityId);

            if (ImGui.MenuItem(Language.FocusTargetPlayer))
                Plugin.TargetManager.SetFocusTarget(player.EntityId);

            if (ImGui.MenuItem(Language.ExaminePlayer))
                Plugin.TargetManager.ExamineTarget(player.EntityId);

            if (ImGui.MenuItem(Language.OpenPlayerPlate))
                Plugin.TargetManager.OpenPlateWindow(player.EntityId);
        }

        if (ImGui.MenuItem(Language.OpenLodestone))
            ServiceContext.LodestoneService.OpenLodestoneProfile(player.Name, player.WorldId);

        // sub menu for selecting category
        if (CategoryNames.Count > 0)
        {
            using var menu = ImRaii.Menu(Language.AssignPlayerCategory);
            if (!menu.Success)
                return;

            var playerCategoryIds = player.AssignedCategories.Select(cat => cat.Id).ToArray();
            for (var j = 0; j < CategoryNames.Count; j++)
            {
                var isCatSelected = playerCategoryIds.Contains(Categories[j].Id);
                if (ImGui.MenuItem(CategoryNames[j], string.Empty, isCatSelected, true))
                {
                    if (isCatSelected)
                        PlayerCategoryService.UnassignCategoryFromPlayer(player.Id, Categories[j].Id);
                    else
                        PlayerCategoryService.AssignCategoryToPlayer(player.Id, Categories[j].Id);
                }
            }
        }
    }
}
