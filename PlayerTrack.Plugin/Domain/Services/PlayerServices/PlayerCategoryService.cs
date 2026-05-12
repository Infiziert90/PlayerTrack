using System.Collections.Generic;
using System.Linq;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using System.Threading.Tasks;

namespace PlayerTrack.Domain;

public class PlayerCategoryService
{
    public static void AssignCategoriesToPlayers(IEnumerable<Player> players, int[] categoryIds) => Task.Run(() =>
    {
        Plugin.PluginLog.Verbose("Entering PlayerCategoryService.AssignCategoriesToPlayers()");

        var categories = new List<Category>();
        foreach (var categoryId in categoryIds)
        {
            var category = ServiceContext.CategoryService.GetCategoryById(categoryId);
            if (category == null)
            {
                Plugin.PluginLog.Warning($"AssignCategoriesToPlayers: Category not found, categoryId: {categoryId}");
                continue;
            }

            categories.Add(category);
        }

        if (categories.Count == 0)
        {
            Plugin.PluginLog.Warning("AssignCategoriesToPlayers: No categories found");
            return;
        }

        var categoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
        foreach (var player in players)
        {
            var assignedCategories = player.AssignedCategories;
            foreach (var category in categories)
            {
                if (assignedCategories.Any(c => c.Id == category.Id))
                {
                    Plugin.PluginLog.Verbose($"Category already assigned to player, playerId: {player.Id}, categoryId: {category.Id}");
                    continue;
                }

                assignedCategories.Add(category);
                SetPrimaryCategoryId(player, categoryRanks);
                ServiceContext.PlayerDataService.UpdatePlayer(player);
                RepositoryContext.PlayerCategoryRepository.CreatePlayerCategory(player.Id, category.Id);
            }
        }

        ServiceContext.PlayerDataService.RecalculatePlayerRankings();
    });

    public static void AssignCategoryToPlayer(int playerId, int categoryId) =>
        Task.Run(() =>
        {
            AssignCategoryToPlayerSync(playerId, categoryId);
        });

    public static void AssignCategoryToPlayerSync(int playerId, int categoryId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerCategoryService.AssignCategoryToPlayer(): {playerId}, {categoryId}");
        var category = ServiceContext.CategoryService.GetCategoryById(categoryId);
        if (category == null)
        {
            Plugin.PluginLog.Warning($"Category not found, categoryId: {categoryId}");
            return;
        }

        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            Plugin.PluginLog.Warning($"Player not found, playerId: {playerId}");
            return;
        }

        var assignedCategories = player.AssignedCategories;
        if (assignedCategories.Any(c => c.Id == categoryId))
        {
            Plugin.PluginLog.Verbose($"Category already assigned to player, playerId: {playerId}, categoryId: {categoryId}");
            return;
        }

        assignedCategories.Add(category);
        var categoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
        SetPrimaryCategoryId(player, categoryRanks);
        ServiceContext.PlayerDataService.UpdatePlayer(player);
        RepositoryContext.PlayerCategoryRepository.CreatePlayerCategory(playerId, categoryId);
        ServiceContext.PlayerDataService.RecalculatePlayerRankings();
    }

    public static void UnassignCategoriesFromPlayer(int playerId) => Task.Run(() =>
    {
        Plugin.PluginLog.Verbose($"Entering PlayerCategoryService.UnassignCategoriesFromPlayer(): {playerId}");
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            Plugin.PluginLog.Warning($"Player not found, playerId: {playerId}");
            return;
        }

        var assignedCategories = player.AssignedCategories;
        if (assignedCategories.Count == 0)
        {
            Plugin.PluginLog.Warning($"No categories assigned to player, playerId: {playerId}");
            return;
        }

        player.AssignedCategories = [];
        player.PrimaryCategoryId = 0;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
        RepositoryContext.PlayerCategoryRepository.DeletePlayerCategoryByPlayerId(playerId);
        ServiceContext.PlayerDataService.RecalculatePlayerRankings();
    });

    public static void UnassignCategoryFromPlayer(int playerId, int categoryId) => Task.Run(() =>
    {
        Plugin.PluginLog.Verbose($"Entering PlayerCategoryService.UnassignCategoryFromPlayer(): {playerId}, {categoryId}");
        var category = ServiceContext.CategoryService.GetCategoryById(categoryId);
        if (category == null)
        {
            Plugin.PluginLog.Warning($"Category not found, categoryId: {categoryId}");
            return;
        }

        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            Plugin.PluginLog.Warning($"Player not found, playerId: {playerId}");
            return;
        }

        var assignedCategories = player.AssignedCategories;
        if (assignedCategories.All(c => c.Id != categoryId))
        {
            Plugin.PluginLog.Warning($"Category not assigned to player, playerId: {playerId}, categoryId: {categoryId}");
            return;
        }

        assignedCategories.RemoveAt(assignedCategories.FindIndex(c => c.Id == categoryId));
        var categoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
        SetPrimaryCategoryId(player, categoryRanks);
        ServiceContext.PlayerDataService.UpdatePlayer(player);
        RepositoryContext.PlayerCategoryRepository.DeletePlayerCategory(playerId, categoryId);
        ServiceContext.PlayerDataService.RecalculatePlayerRankings();
    });

    /// <summary>
    /// Removes every player assignment for <paramref name="categoryId"/> from
    /// both the database and the in-memory cache.  The category record itself is
    /// not deleted.
    /// </summary>
    public static void UnassignCategoryFromAllPlayers(int categoryId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerCategoryService.UnassignCategoryFromAllPlayers(): {categoryId}");
        RepositoryContext.PlayerCategoryRepository.DeletePlayerCategoryByCategoryId(categoryId);
        ServiceContext.PlayerDataService.ClearCategoryFromPlayers(categoryId);
    }

    public static void DeletePlayerCategoryByCategoryId(int categoryId) => RepositoryContext.PlayerCategoryRepository.DeletePlayerCategoryByCategoryId(categoryId);

    public static void DeletePlayerCategoryByPlayerId(int playerId) => RepositoryContext.PlayerCategoryRepository.DeletePlayerCategoryByPlayerId(playerId);

    public static void SetPrimaryCategoryId(Player player, Dictionary<int, int> categoryRanks) => player.PrimaryCategoryId = GetPrimaryCategoryId(player, categoryRanks);

    private static int GetPrimaryCategoryId(Player player, IReadOnlyDictionary<int, int> categoryRanks)
    {
        var primaryCategoryId = 0;
        var highestRank = int.MaxValue;

        foreach (var category in player.AssignedCategories)
        {
            if (!categoryRanks.TryGetValue(category.Id, out var rank)) continue;
            if (rank >= highestRank) continue;
            highestRank = rank;
            primaryCategoryId = category.Id;
        }

        return primaryCategoryId;
    }
}
