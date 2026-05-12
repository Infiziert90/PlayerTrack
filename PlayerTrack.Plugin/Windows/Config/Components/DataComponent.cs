using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Enums;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using PlayerTrack.Resource;

namespace PlayerTrack.Windows.Config.Components;

public class DataComponent : ConfigViewComponent
{
    private Player? DeletePlayer;
    private Player? UpdatePlayer;
    private FilterComboBox DeletePlayerComboBox = null!;
    private FilterComboBox UpdatePlayerComboBox = null!;
    private List<Player> Players = [];
    private List<string> PlayerDisplayNames = [];
    private int SelectedActionIndex;
    private int ItemsToDeleteCount;
    private int TotalItemsCount;
    private Task? DeleteTask;
    private string StatusMessage = string.Empty;
    private Vector4 StatusColor = Vector4.Zero;
    private bool IsDirty = true;
    private string SqlQuery = string.Empty;
    private string SqlResult = string.Empty;
    private string SqlResultDisplay = string.Empty;
    private Tuple<ActionRequest, LocalPlayer>? LocalPlayerToDelete;

    // UnassignCategories action state
    private List<Category> _unassignCategories = [];
    private readonly HashSet<int> _selectedCategoryIds = [];

    public override void Draw() => DrawControls();

    private void DrawControls()
    {
        using var tabBar = ImRaii.TabBar("###Data_TabBar", ImGuiTabBarFlags.None);
        if (!tabBar.Success)
            return;

        DrawPurge();
        DrawMerge();
        DrawSqlExecutor();
        DrawLocalPlayers();
    }

    private void DrawLocalPlayers()
    {
        using var tabItem = ImRaii.TabItem(Language.LocalPlayers);
        if (!tabItem.Success)
            return;

        var localPlayers = LocalPlayerService.GetLocalPlayers();
        if (localPlayers.Count == 0)
        {
            Helper.TextColored(ImGuiColors.DalamudYellow, Language.NoLocalPlayers);
        }
        else
        {
            Helper.TextColored(ImGuiColors.DalamudViolet, Language.LocalPlayers);
            ImGui.Spacing();
            foreach (var player in localPlayers)
            {
                var playerName = LocalPlayerService.GetLocalPlayerFullName(player.ContentId);
                ImGui.TextUnformatted(playerName);
                ImGui.SameLine();
                HandleLocalPlayerDeletion(player);
            }
        }
    }

    private void HandleLocalPlayerDeletion(LocalPlayer localPlayer)
    {
        Helper.Confirm(localPlayer, FontAwesomeIcon.Trash, Language.ConfirmDelete, ref LocalPlayerToDelete);
        if (LocalPlayerToDelete?.Item1 == ActionRequest.Confirmed)
            DeleteLocalPlayer();
        else if (LocalPlayerToDelete?.Item1 == ActionRequest.None)
            LocalPlayerToDelete = null;
    }

    private void DeleteLocalPlayer()
    {
        var player = LocalPlayerToDelete?.Item2;
        if (player != null)
            LocalPlayerService.DeleteLocalPlayer(player);

        LocalPlayerToDelete = null;

    }

    private void DrawPurge()
    {
        using var tabItem = ImRaii.TabItem(Language.Purge);
        if (!tabItem.Success)
            return;

        DrawSelectAction();
        DrawActionOptions();
        DrawGlobalOptions();
        DrawRun();
        DrawMessage();
    }


    public void Initialize()
    {
        Players = ServiceContext.PlayerCacheService.GetPlayers();
        if (Players.Count == 0)
            return;

        PlayerDisplayNames = Players.Select(player => player.FullyQualifiedName()).ToList();
        DeletePlayerComboBox = new FilterComboBox(PlayerDisplayNames, Language.SearchPlayersInputHint, Language.NoPlayersFound);
        UpdatePlayerComboBox = new FilterComboBox(PlayerDisplayNames, Language.SearchPlayersInputHint, Language.NoPlayersFound);
        DeletePlayer = null;
        UpdatePlayer = null;
    }

    private void DrawMerge()
    {
        using var tabItem = ImRaii.TabItem(Language.Merge);
        if (!tabItem.Success)
            return;

        DrawMergeInstructions();
        DrawPlayerToDelete();
        DrawPlayerToUpdate();
        DrawMergeControls();
    }
    private static void DrawMergeInstructions()
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.MergePlayersInstructions);
        ImGui.Spacing();
    }

    private void DrawPlayerToDelete()
    {
        DrawPlayer(ref DeletePlayer, DeletePlayerComboBox, Language.SelectPlayerToDelete);
    }

    private void DrawPlayerToUpdate()
    {
        DrawPlayer(ref UpdatePlayer, UpdatePlayerComboBox, Language.SelectPlayerToUpdate);
    }

    private void DrawPlayer(ref Player? selectedPlayer, FilterComboBox? comboBox, string? label)
    {
        var isPlayerSelected = selectedPlayer != null;
        var buttonLabel = Language.OpenPlayer;

        using (ImRaii.Disabled(!isPlayerSelected))
        {
            if (ImGui.Button($"{buttonLabel}##{selectedPlayer?.Id ?? '0'}") && isPlayerSelected)
                OpenPlayer(selectedPlayer);
        }

        ImGui.SameLine();
        var selectedIndex = comboBox?.Draw(label ?? "null", 300f);
        if (selectedIndex.HasValue)
            selectedPlayer = Players[selectedIndex.Value];
    }

    private static void OpenPlayer(Player? player)
    {
        if (player != null)
            ServiceContext.PlayerProcessService.SelectPlayer(player.Id);
    }

    private void DrawMergeControls()
    {
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2f);
        var areBothSelected = DeletePlayer != null && UpdatePlayer != null;
        var isDupeSelected = areBothSelected && DeletePlayer!.Id == UpdatePlayer!.Id;
        var isDisabled = !areBothSelected || isDupeSelected;

        if (ImGui.Button(Language.ResetPlayers))
            Initialize();

        ImGui.SameLine();

        using (ImRaii.Disabled(isDisabled))
        {
            if (ImGui.Button(Language.MergePlayers) && !isDisabled)
            {
                ServiceContext.PlayerDataService.MergePlayers(DeletePlayer!, UpdatePlayer!);
                OpenPlayer(UpdatePlayer);
                Initialize();
            }
        }

        if (isDupeSelected)
        {
            ImGui.Spacing();
            Helper.TextColored(ImGuiColors.DalamudYellow, Language.MergePlayersDupe);
        }
    }

    private void DrawSqlExecutor()
    {
        using var tabItem = ImRaii.TabItem(Language.SQLExecutor);
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(1f);

        var query = SqlQuery;
        if (ImGui.InputTextMultiline("###SQLInput", ref query, 1000, ImGuiHelpers.ScaledVector2(-1, 90), ImGuiInputTextFlags.None))
            SqlQuery = query;

        ImGuiHelpers.ScaledDummy(1f);

        using (var child = ImRaii.Child("##SQLResultChild", ImGuiHelpers.ScaledVector2(-1, 90), true, ImGuiWindowFlags.None))
        {
            if (child.Success)
                ImGui.TextUnformatted(SqlResultDisplay);
        }

        ImGuiHelpers.ScaledDummy(1f);
        if (ImGui.Button(Language.Execute))
            ExecuteSql();

        ImGui.SameLine();

        if (ImGui.Button(Language.CopyToClipboard))
            ImGui.SetClipboardText(SqlResult);
    }

    private void ExecuteSql()
    {
        SqlResult = string.Empty;
        SqlResultDisplay = string.Empty;
        if (string.IsNullOrEmpty(SqlQuery))
            return;

        SqlResult = RepositoryContext.ExecuteSqlQuery(SqlQuery);
        SqlResultDisplay = SqlResult.Length > 1000 ? SqlResult[..1000] : SqlResult;
    }

    private void DrawSelectAction()
    {
        var actions = Enum.GetNames<DataActionType>();
        if (Helper.Combo(Language.Actions, ref SelectedActionIndex, actions, 180))
            IsDirty = true;

        ImGui.Spacing();
    }

    private void DrawMessage()
    {
        ImGui.SameLine();
        if (!string.IsNullOrEmpty(StatusMessage))
        {
            Helper.TextColored(StatusColor, StatusMessage);
        }
        else if (ItemsToDeleteCount != 0 && TotalItemsCount != 0 && DeleteTask == null)
        {
            var percentageDelete = (float)ItemsToDeleteCount / TotalItemsCount * 100;
            percentageDelete = (float)Math.Round(percentageDelete, 2);
            var deleteMessage = string.Format(
                Language.NumberOfRecordsToBeDeleted,
                ItemsToDeleteCount.ToString("N0", CultureInfo.CurrentCulture),
                TotalItemsCount.ToString("N0", CultureInfo.CurrentCulture),
                percentageDelete);
            Helper.TextColored(ImGuiColors.DalamudRed, deleteMessage);
            ImGui.Spacing();
        }
        else
        {
            Helper.TextColored(ImGuiColors.DalamudYellow, Language.NoRecordsToDelete);
        }
    }

    private void DrawRun()
    {
        using var popup = ImRaii.Popup(Language.Confirmation);
        if (!popup.Success)
            return;

        ImGui.TextUnformatted(Language.ConfirmationQuestion);
        ImGui.Separator();
        if (ImGui.Button(Language.Yes))
        {
            ImGui.CloseCurrentPopup();

            StatusMessage = Language.DeleteRecordsInProgress;
            StatusColor = ImGuiColors.DalamudYellow;
            DeleteTask = (DataActionType)SelectedActionIndex switch
            {
                DataActionType.DeletePlayers => Task.Run(() =>
                    {
                        RunBackup();
                        ServiceContext.PlayerDataService.DeletePlayers();
                        HandleTaskSuccess();
                    })
                    .ContinueWith(_ =>
                    {
                        HandlePostTaskSuccess();
                        ItemsToDeleteCount = ServiceContext.PlayerCacheService.GetPlayersForDeletionCount();
                        TotalItemsCount = ServiceContext.PlayerCacheService.GetAllPlayersCount();
                    }),
                DataActionType.DeletePlayerSettings => Task.Run(() =>
                    {
                        RunBackup();
                        ServiceContext.PlayerDataService.DeletePlayerConfigs();
                        HandleTaskSuccess();
                    })
                    .ContinueWith(_ =>
                    {
                        HandlePostTaskSuccess();
                        ItemsToDeleteCount = ServiceContext.PlayerCacheService.GetPlayerConfigsForDeletionCount();
                        TotalItemsCount = ServiceContext.PlayerCacheService.GetPlayerConfigCount();
                    }),
                DataActionType.DeleteEncounters => Task.Run(() =>
                    {
                        RunBackup();
                        ServiceContext.EncounterService.DeleteEncounters();
                        HandleTaskSuccess();
                    })
                    .ContinueWith(_ =>
                    {
                        HandlePostTaskSuccess();
                        ItemsToDeleteCount = ServiceContext.EncounterService.GetEncountersForDeletionCount();
                        TotalItemsCount = EncounterService.GetEncountersCount();
                    }),
                DataActionType.UnassignCategories => Task.Run(() =>
                    {
                        RunBackup();
                        var ids = _selectedCategoryIds.ToArray();
                        foreach (var categoryId in ids)
                            PlayerCategoryService.UnassignCategoryFromAllPlayers(categoryId);
                        ServiceContext.PlayerDataService.RecalculatePlayerRankings();
                        HandleTaskSuccess();
                    })
                    .ContinueWith(_ =>
                    {
                        HandlePostTaskSuccess();
                        _selectedCategoryIds.Clear();
                        RefreshUnassignCounts();
                    }),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        ImGui.SameLine();

        if (ImGui.Button(Language.No))
            ImGui.CloseCurrentPopup();
    }

    private void RunBackup()
    {
        if (Config.RunBackupBeforeDataActions)
            ServiceContext.BackupService.RunBackup(BackupType.Automatic);
    }

    private void HandleTaskSuccess()
    {
        StatusMessage = Language.DeleteRecordsSuccess;
        StatusColor = ImGuiColors.HealerGreen;
    }

    private void HandlePostTaskSuccess()
    {
        DeleteTask = null;
        ItemsToDeleteCount = 0;
        TotalItemsCount = 0;
        IsDirty = true;
    }

    /// <summary>
    /// Recomputes the assignment counts for the UnassignCategories action.
    /// ItemsToDeleteCount = assignments that will be removed (selected categories).
    /// TotalItemsCount    = total assignments across all categories (denominator).
    /// </summary>
    private void RefreshUnassignCounts()
    {
        ItemsToDeleteCount = _selectedCategoryIds
            .Sum(id => ServiceContext.PlayerCacheService.GetCategoryPlayersCount(id));
        TotalItemsCount = _unassignCategories
            .Sum(c => ServiceContext.PlayerCacheService.GetCategoryPlayersCount(c.Id));
        if (TotalItemsCount == 0) TotalItemsCount = ItemsToDeleteCount; // avoid divide-by-zero in message
    }

    private void DrawGlobalOptions()
    {
        var runBackupBeforeDataActions = Config.RunBackupBeforeDataActions;
        if (Helper.Checkbox(Language.RunBackupBeforeDataActions, ref runBackupBeforeDataActions))
        {
            Config.RunBackupBeforeDataActions = runBackupBeforeDataActions;
            ServiceContext.ConfigService.SaveConfig(Config);
            IsDirty = true;
        }

        ImGui.Spacing();

        var isDisabled = DeleteTask != null || ItemsToDeleteCount == 0 || !string.IsNullOrEmpty(StatusMessage);
        ImGui.BeginDisabled(isDisabled);

        if (ImGui.Button(Language.RunAction) && !isDisabled)
            ImGui.OpenPopup(Language.Confirmation);

        ImGui.EndDisabled();
        ImGui.SameLine();
    }

    private void DrawActionOptions()
    {
        switch ((DataActionType)SelectedActionIndex)
        {
            case DataActionType.DeletePlayers:
                if (IsDirty)
                {
                    IsDirty = false;
                    ItemsToDeleteCount = ServiceContext.PlayerCacheService.GetPlayersForDeletionCount();
                    TotalItemsCount = ServiceContext.PlayerCacheService.GetAllPlayersCount();
                }

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.KeepPlayers);
                using (ImRaii.PushIndent(10f))
                {
                    foreach (var property in Config.PlayerDataActionOptions.GetType().GetProperties())
                    {
                        var currentValue = (bool)(property.GetValue(Config.PlayerDataActionOptions) ?? true);
                        if (Helper.Checkbox(Utils.GetLoc(property.Name), ref currentValue))
                        {
                            property.SetValue(Config.PlayerDataActionOptions, currentValue);
                            ServiceContext.ConfigService.SaveConfig(Config);
                            IsDirty = true;
                            StatusMessage = string.Empty;
                        }
                    }
                }
                break;
            case DataActionType.DeletePlayerSettings:
                if (IsDirty)
                {
                    IsDirty = false;
                    ItemsToDeleteCount = ServiceContext.PlayerCacheService.GetPlayerConfigsForDeletionCount();
                    TotalItemsCount = ServiceContext.PlayerCacheService.GetPlayerConfigCount();
                }

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.KeepPlayerSettings);
                using (ImRaii.PushIndent(10f))
                {
                    foreach (var property in Config.PlayerSettingsDataActionOptions.GetType().GetProperties())
                    {
                        var currentValue = (bool)(property.GetValue(Config.PlayerSettingsDataActionOptions) ?? true);
                        if (Helper.Checkbox(Utils.GetLoc(property.Name), ref currentValue))
                        {
                            property.SetValue(Config.PlayerSettingsDataActionOptions, currentValue);
                            ServiceContext.ConfigService.SaveConfig(Config);
                            IsDirty = true;
                            StatusMessage = string.Empty;
                        }
                    }
                }
                break;
            case DataActionType.DeleteEncounters:
                if (IsDirty)
                {
                    IsDirty = false;
                    ItemsToDeleteCount = ServiceContext.EncounterService.GetEncountersForDeletionCount();
                    TotalItemsCount = EncounterService.GetEncountersCount();
                }

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.KeepEncounters);
                using (ImRaii.PushIndent(10f))
                {
                    foreach (var property in Config.EncounterDataActionOptions.GetType().GetProperties())
                    {
                        var currentValue = (bool)(property.GetValue(Config.EncounterDataActionOptions) ?? true);
                        if (Helper.Checkbox(Utils.GetLoc(property.Name), ref currentValue))
                        {
                            property.SetValue(Config.EncounterDataActionOptions, currentValue);
                            ServiceContext.ConfigService.SaveConfig(Config);
                            IsDirty = true;
                            StatusMessage = string.Empty;
                        }
                    }
                }
                break;
            case DataActionType.UnassignCategories:
                if (IsDirty)
                {
                    IsDirty = false;
                    _selectedCategoryIds.Clear();
                    _unassignCategories = ServiceContext.CategoryService.GetCategories();
                    ItemsToDeleteCount = 0;
                    TotalItemsCount = 0;
                    StatusMessage = string.Empty;
                }

                Helper.TextColored(ImGuiColors.DalamudViolet, "Select categories to remove all player assignments from:");
                ImGuiHelpers.ScaledDummy(2f);

                if (_unassignCategories.Count == 0)
                {
                    ImGui.TextUnformatted("No categories found.");
                }
                else
                {
                    using (ImRaii.PushIndent(10f))
                    {
                        foreach (var cat in _unassignCategories)
                        {
                            var isChecked = _selectedCategoryIds.Contains(cat.Id);
                            // Show "(dynamic)" suffix for social-list-synced categories.
                            var label = cat.SocialListId != 0
                                ? $"{cat.Name} (dynamic)###UnassignCat{cat.Id}"
                                : $"{cat.Name}###UnassignCat{cat.Id}";
                            if (ImGui.Checkbox(label, ref isChecked))
                            {
                                if (isChecked) _selectedCategoryIds.Add(cat.Id);
                                else           _selectedCategoryIds.Remove(cat.Id);
                                RefreshUnassignCounts();
                                StatusMessage = string.Empty;
                            }
                        }
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ImGui.Spacing();
        ImGui.Separator();
    }
}
