using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using Dalamud.Interface;
using PlayerTrack.Data;
using PlayerTrack.Domain.Caches;
using PlayerTrack.Extensions;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using PlayerTrack.Models.Comparers;
using Timer = System.Timers.Timer;

namespace PlayerTrack.Domain;

public class PlayerCacheService
{
    private readonly ReaderWriterLockSlim SetLock = new(LockRecursionPolicy.NoRecursion);
    private readonly PlayerCache PlayerCache = new();
    private readonly PlayerCurrentCache PlayerCurrentCache = new();
    private readonly PlayerRecentCache PlayerRecentCache = new();
    private readonly PlayerCategoryCache PlayerCategoryCache = new();
    private readonly PlayerTagCache PlayerTagCache = new();
    private readonly Timer RecentPlayerTimer;
    private IComparer<Player> Comparer = null!;
    private List<Player> DbPlayers = null!;
    private Dictionary<int, int> DbCategoryRanks = null!;
    private const long NinetyDaysInMilliseconds = 7776000000;

    // Advanced sort state -- session-only, not persisted.
    private PlayerSortType _activeSortType = PlayerSortType.ByCategory;
    private uint _activeZoneId = 0;

    public event Action? OnCacheUpdated;

    public PlayerCacheService()
    {
        RecentPlayerTimer = new Timer(30000);
        RecentPlayerTimer.Elapsed += OnRecentPlayerTimerOnElapsed;
        RecentPlayerTimer.AutoReset = true;
        RecentPlayerTimer.Start();
    }

    public void Dispose()
    {
        RecentPlayerTimer.Elapsed -= OnRecentPlayerTimerOnElapsed;
        RecentPlayerTimer.Stop();
    }

    private void OnRecentPlayerTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var threshold = ServiceContext.ConfigService.GetConfig().RecentPlayersThreshold;
        var expiry = PlayerRecentCache.GetExpiry();

        foreach (var entry in expiry)
        {
            var expiryTime = entry.Value + threshold;
            if (expiryTime > currentTime)
                continue;

            if (!PlayerRecentCache.RemoveExpiry(entry.Key))
                continue;

            var player = PlayerCache.Get(entry.Key);
            if (player == null)
                continue;

            player.IsRecent = false;
            ServiceContext.PlayerDataService.UpdatePlayer(player);
        }
    }

    public void LoadPlayers()
    {
        SetLock.EnterWriteLock();
        try
        {
            var currentIds = PlayerCurrentCache.GetIds();
            var recentIds = PlayerRecentCache.GetIds();
            Initialize();
            CopyUnsavedProperties();
            FetchDataFromDatabase();
            PlayerCurrentCache.SetIds(currentIds);
            PlayerRecentCache.SetIds(recentIds);
            foreach (var player in DbPlayers)
            {
                player.IsCurrent = currentIds.Contains(player.Id);
                player.IsRecent = recentIds.Contains(player.Id);
                PopulateDerivedFields(player);
                AddPlayerToCaches(player);
            }
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    public void AddPlayer(Player playerToAdd)
    {
        SetLock.EnterWriteLock();
        try
        {
            AddPlayerToCaches(playerToAdd);
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    public void RemovePlayer(int playerIdToRemove)
    {
        SetLock.EnterWriteLock();
        try
        {
            var player = PlayerCache.Get(playerIdToRemove);
            if (player != null)
                RemovePlayerFromAllCaches(player);
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    public void UpdatePlayer(Player playerToUpdate)
    {
        SetLock.EnterWriteLock();
        try
        {
            var existingPlayer = PlayerCache.Get(playerToUpdate.Id);
            if (existingPlayer != null)
            {
                RemovePlayerFromAllCaches(existingPlayer);
                PopulateDerivedFields(playerToUpdate);
            }

            AddPlayerToCaches(playerToUpdate);
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    public Player? GetPlayer(int playerId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Get(playerId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public Player? GetPlayer(ulong contentId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.FindFirst(p => p.ContentId == contentId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetPlayers(string name, uint worldId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Get(p => p.Name == name && p.WorldId == worldId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public Player? GetPlayer(uint entityId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.FindFirst(p => p.EntityId == entityId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetPlayers()
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.GetAll();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetPlayers(Func<Player, bool> filter)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.GetAll().Where(filter).ToList();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public void AddCategory(int categoryIdToAdd)
    {
        SetLock.EnterWriteLock();
        try
        {
            PlayerCategoryCache.AddGroup(categoryIdToAdd);
        }
        finally
        {
            SetLock.ExitWriteLock();
        }
    }

    public void AddTag(int tagIdToAdd)
    {
        SetLock.EnterWriteLock();
        try
        {
            PlayerTagCache.AddGroup(tagIdToAdd);
        }
        finally
        {
            SetLock.ExitWriteLock();
        }
    }

    public void RemoveCategory(int categoryIdToRemove)
    {
        var updatedPlayers = new List<Player>();
        SetLock.EnterWriteLock();
        try
        {
            var categoryCache = PlayerCategoryCache.GetGroup(categoryIdToRemove);
            if (categoryCache != null)
            {
                updatedPlayers.AddRange(categoryCache.Values);
                foreach (var player in updatedPlayers)
                {
                    player.AssignedCategories.RemoveAll(c => c.Id == categoryIdToRemove);
                    PopulateDerivedFields(player);
                }

                PlayerCategoryCache.RemoveGroup(categoryIdToRemove);

                foreach (var player in updatedPlayers)
                {
                    RemovePlayerFromAllCaches(player);
                    AddPlayerToCaches(player);
                }

                InitializeComparer();
                ResortCache();
            }
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    // ------------------------------------------------------------------
    // Advanced sort API
    // ------------------------------------------------------------------

    public PlayerSortType GetSortType() => _activeSortType;

    public uint GetSortZoneId() => _activeZoneId;

    /// <summary>
    /// Changes the active sort criteria and immediately resorts all caches.
    /// Pass <paramref name="zoneId"/> = 0 (or omit) when the sort type is not
    /// <see cref="PlayerSortType.ByZoneEncounterTime"/>.
    /// </summary>
    public void SetSortType(PlayerSortType sortType, uint zoneId = 0)
    {
        _activeSortType = sortType;
        _activeZoneId   = zoneId;
        Resort();
    }

    // ------------------------------------------------------------------

    public void Resort()
    {
        SetLock.EnterWriteLock();
        try
        {
            InitializeComparer();
            ResortCache();
        }
        finally
        {
            SetLock.ExitWriteLock();
            OnCacheUpdated?.Invoke();
        }
    }

    public List<Player> GetAllPlayers(int start, int count)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Get(start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetAllPlayers(int start, int count, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Get(PlayerSearchService.GetSearchFilter(name, searchType), start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetAllPlayersCount()
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Count();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetAllPlayersCount(string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCache.Count(PlayerSearchService.GetSearchFilter(name, searchType));
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCurrentPlayers()
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCurrentCache.GetAll();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCurrentPlayers(int start, int count)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCurrentCache.Get(start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCurrentPlayers(int start, int count, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCurrentCache.Get(PlayerSearchService.GetSearchFilter(name, searchType), start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetCurrentPlayersCount()
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCurrentCache.Count();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetCurrentPlayersCount(string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCurrentCache.Count(PlayerSearchService.GetSearchFilter(name, searchType));
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetRecentPlayers(int start, int count)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerRecentCache.Get(start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetRecentPlayers(int start, int count, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerRecentCache.Get(PlayerSearchService.GetSearchFilter(name, searchType), start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetRecentPlayersCount()
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerRecentCache.Count();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetRecentPlayersCount(string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerRecentCache.Count(PlayerSearchService.GetSearchFilter(name, searchType));
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCategoryPlayers(int categoryId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCategoryCache.GetGroup(categoryId)?.Values.ToList() ?? new List<Player>();
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCategoryPlayers(int categoryId, int start, int count)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCategoryCache.Get(categoryId, start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetCategoryPlayers(int categoryId, int start, int count, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCategoryCache.Get(categoryId, PlayerSearchService.GetSearchFilter(name, searchType), start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetCategoryPlayersCount(int categoryId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCategoryCache.Count(categoryId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetCategoryPlayersCount(int categoryId, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerCategoryCache.Count(categoryId, PlayerSearchService.GetSearchFilter(name, searchType));
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetTagPlayers(int tagId, int start, int count)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerTagCache.Get(tagId, start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public List<Player> GetTagPlayers(int tagId, int start, int count, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerTagCache.Get(tagId, PlayerSearchService.GetSearchFilter(name, searchType), start, count);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetTagPlayersCount(int tagId)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerTagCache.Count(tagId);
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetTagPlayersCount(int tagId, string name, SearchType searchType)
    {
        SetLock.EnterReadLock();
        try
        {
            return PlayerTagCache.Count(tagId, PlayerSearchService.GetSearchFilter(name, searchType));
        }
        finally
        {
            SetLock.ExitReadLock();
        }
    }

    public int GetPlayerConfigCount()
    {
        return PlayerCache.Get(p => p.PlayerConfig.Id != 0).Count;
    }

    public int GetPlayersForDeletionCount()
    {
        return GetPlayersForDeletion().Count;
    }

    public int GetPlayerConfigsForDeletionCount()
    {
        return GetPlayerConfigsForDeletion().Count;
    }

    public void PopulateDerivedFields(Player player)
    {
        PlayerCategoryService.SetPrimaryCategoryId(player, DbCategoryRanks);
        var colorId = PlayerConfigService.GetNameColor(player);
        var color = Sheets.UiColor.TryGetValue(colorId, out var uiColor) ? uiColor : new UiColorData();
        player.PlayerListNameColor = color.Foreground.ToVector4();
        player.PlayerListIconString = ((FontAwesomeIcon)PlayerConfigService.GetIcon(player)).ToIconString();
    }

    private void ResortCache()
    {
        var filter = ServiceContext.ConfigService.GetConfig().PlayerListFilter;
        switch (filter)
        {
            case PlayerListFilter.CurrentPlayers:
                PlayerCurrentCache.Resort(Comparer);
                break;
            case PlayerListFilter.RecentPlayers:
                PlayerRecentCache.Resort(Comparer);
                break;
            case PlayerListFilter.AllPlayers:
                PlayerCache.Resort(Comparer);
                break;
            case PlayerListFilter.PlayersByCategory:
                PlayerCategoryCache.Resort(Comparer);
                break;
            case PlayerListFilter.PlayersByTag:
                PlayerTagCache.Resort(Comparer);
                break;
            default:
                Plugin.PluginLog.Warning($"Invalid player list filter: {filter}");
                break;
        }
    }

    private void RemovePlayerFromAllCaches(Player playerToRemove)
    {
        PlayerCache.Remove(playerToRemove);
        PlayerCurrentCache.Remove(playerToRemove);
        PlayerRecentCache.Remove(playerToRemove);
        PlayerCategoryCache.Remove(playerToRemove);
        PlayerTagCache.Remove(playerToRemove);
    }

    private void CopyUnsavedProperties()
    {
        PlayerCurrentCache.SaveIds();
        PlayerRecentCache.SaveIds();
    }

    private void FetchDataFromDatabase()
    {
        DbPlayers = RepositoryContext.PlayerRepository.GetAllPlayersWithRelations().ToList();
        DbCategoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
    }

    private void Initialize()
    {
        InitializeComparer();
        InitializeUnsavedProperties();
        PlayerCache.Initialize(Comparer);
        PlayerCurrentCache.Initialize(Comparer);
        PlayerRecentCache.Initialize(Comparer);
        PlayerCategoryCache.Initialize(Comparer);
        PlayerTagCache.Initialize(Comparer);
    }

    private void InitializeUnsavedProperties()
    {
        PlayerCurrentCache.ClearIds();
        PlayerRecentCache.ClearIds();
    }

    private void InitializeComparer()
    {
        var categoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
        var noCategoryPlacement = ServiceContext.ConfigService.GetConfig().NoCategoryPlacement;
        var noCategoryRank = 0;
        if (categoryRanks.Count != 0)
        {
            noCategoryRank = noCategoryPlacement switch
            {
                NoCategoryPlacement.Top    => categoryRanks.Values.Min() - 1,
                NoCategoryPlacement.Bottom => categoryRanks.Values.Max() + 1,
                _                          => 0
            };
        }

        // Precompute per-player sort values for the expensive time-based sorts.
        var sortValues = new Dictionary<int, long>();
        if (_activeSortType == PlayerSortType.ByTotalEncounterTime)
        {
            sortValues = RepositoryContext.PlayerEncounterRepository.GetEncounterTimeSumsByPlayer();
        }
        else if (_activeSortType == PlayerSortType.ByZoneEncounterTime && _activeZoneId != 0)
        {
            sortValues = RepositoryContext.PlayerEncounterRepository.GetEncounterTimeSumsByPlayerAndZone(_activeZoneId);
        }

        Comparer = new PlayerComparer(
            ServiceContext.CategoryService.GetCategoryRanks(),
            noCategoryRank,
            _activeSortType,
            sortValues);
    }

    private void AddPlayerToCaches(Player player)
    {
        PlayerCache.Add(player);
        PlayerCurrentCache.Add(player);
        PlayerRecentCache.Add(player);
        PlayerCategoryCache.Add(player);
        PlayerTagCache.Add(player);
    }

    private List<Player> GetPlayersForDeletion()
    {
        var playersWithEncounters = RepositoryContext.PlayerEncounterRepository.GetPlayersWithEncounters();
        var currentTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var options = ServiceContext.ConfigService.GetConfig().PlayerDataActionOptions;
        return PlayerCache.Get(p =>
            (!options.KeepPlayersWithNotes || string.IsNullOrEmpty(p.Notes)) &&
            (!options.KeepPlayersWithCategories || p.AssignedCategories.Count == 0) &&
            (!options.KeepPlayersWithAnySettings || p.PlayerConfig.Id == 0) &&
            (!options.KeepPlayersWithEncounters || !playersWithEncounters.Contains(p.Id)) &&
            (!options.KeepPlayersSeenInLast90Days || currentTimeUnix - p.LastSeen > NinetyDaysInMilliseconds) &&
            (!options.KeepPlayersVerifiedOnLodestone || p.LodestoneStatus != LodestoneStatus.Verified));
    }

    private List<PlayerConfig> GetPlayerConfigsForDeletion()
    {
        var playersWithEncounters = RepositoryContext.PlayerEncounterRepository.GetPlayersWithEncounters();
        var currentTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var options = ServiceContext.ConfigService.GetConfig().PlayerSettingsDataActionOptions;
        return PlayerCache.Get(p =>
            p.PlayerConfig.Id != 0 &&
            (!options.KeepSettingsForPlayersWithNotes || string.IsNullOrEmpty(p.Notes)) &&
            (!options.KeepSettingsForPlayersWithCategories || p.AssignedCategories.Count == 0) &&
            (!options.KeepSettingsForPlayersWithAnySettings || p.PlayerConfig.Id == 0) &&
            (!options.KeepSettingsForPlayersWithEncounters || !playersWithEncounters.Contains(p.Id)) &&
            (!options.KeepSettingsForPlayersSeenInLast90Days || currentTimeUnix - p.LastSeen > NinetyDaysInMilliseconds) &&
            (!options.KeepSettingsForPlayersVerifiedOnLodestone || p.LodestoneStatus != LodestoneStatus.Verified)).Select(p => p.PlayerConfig).ToList();
    }
}
