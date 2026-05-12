using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;
using System.Collections;
using PlayerTrack.Data;
using PlayerTrack.Domain.Common;
using PlayerTrack.Extensions;
using PlayerTrack.Models;

namespace PlayerTrack.Domain;

public class PlayerProcessService
{
    private volatile bool IsProcessing;
    private readonly ReaderWriterLockSlim Locker = new ();
    public event Action<Player>? OnPlayerSelected;
    public event Action<Player>? OnCurrentPlayerAdded;
    public event Action<Player>? OnCurrentPlayerRemoved;

    public void RegisterCurrentPlayer(Player player) => OnCurrentPlayerAdded?.Invoke(player);

    public void Dispose()
    {
        Plugin.GameFramework.Update -= ProcessCurrentPlayers;
        Locker.Dispose();
    }

    public void Start()
    {
        Plugin.GameFramework.Update += ProcessCurrentPlayers;
    }

    private void ProcessCurrentPlayers(IFramework framework)
    {
        Locker.EnterWriteLock();
        try
        {
            if (IsProcessing)
                return;

            IsProcessing = true;
        }
        finally
        {
            Locker.ExitWriteLock();
        }

        Plugin.GameFramework.RunOnFrameworkThread(() =>
        {

            if (Plugin.ConditionHandler.Any(ConditionFlag.DutyRecorderPlayback))
                return;

            var objectPlayers = Plugin.ObjectCollection.GetPlayers().ToList();
            Task.Run(() =>
            {
                try
                {
                    var currentPlayers = ServiceContext.PlayerCacheService.GetCurrentPlayers();
                    if (currentPlayers.Count > 0)
                    {
                        foreach (var currentPlayer in currentPlayers)
                        {
                            if (objectPlayers.All(p => p.ContentId != currentPlayer.ContentId))
                            {
                                Plugin.PluginLog.Verbose($"Removing current player: {currentPlayer.ContentId}, {currentPlayer.Name}@{currentPlayer.WorldId}");
                                RemoveCurrentPlayer(currentPlayer);
                            }
                        }
                    }

                    if (objectPlayers.Count > 0)
                    {
                        foreach (var objectPlayer in objectPlayers)
                        {
                            var currentPlayer = currentPlayers.FirstOrDefault(p => p.ContentId == objectPlayer.ContentId);
                            if (currentPlayer == null)
                            {
                                Plugin.PluginLog.Verbose($"Adding current player: {objectPlayer.ContentId}, {objectPlayer.Name}");
                                AddOrUpdatePlayer(objectPlayer);
                            }
                        }
                    }
                }
                finally
                {
                    Locker.EnterWriteLock();
                    try
                    {
                        IsProcessing = false;
                    }
                    finally
                    {
                        Locker.ExitWriteLock();
                    }
                }
            });
        });
    }

    public static void CreateNewPlayer(string name, uint worldId, ulong contentId = 0, bool isSeen = true)
    {
        var key = PlayerKeyBuilder.Build(name, worldId);
        var player = new Player
        {
            Key = key,
            Name = name,
            WorldId = worldId,
            ContentId = contentId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        if (isSeen)
            player.FirstSeen = player.Created;

        ServiceContext.PlayerDataService.AddPlayer(player);
    }

    public void SelectPlayer(string name, string worldName)
    {
        var worldId = Sheets.GetWorldIdByName(worldName);
        var player = ServiceContext.PlayerDataService.GetPlayer(name, worldId);
        if (player == null)
        {
            Plugin.PluginLog.Verbose("Player not found.");
            return;
        }

        OnPlayerSelected?.Invoke(player);
    }

    public void SelectPlayer(int playerId)
    {
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            Plugin.PluginLog.Verbose("Player not found.");
            return;
        }
        OnPlayerSelected?.Invoke(player);
    }

    public void AddOrUpdatePlayer(PlayerData toadPlayer, bool isCurrent = true, bool isUserRequest = false)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerProcessService.AddOrUpdatePlayer(): {toadPlayer.ContentId}, {toadPlayer.Name}, {toadPlayer.HomeWorld}, {isUserRequest}");
        var enc = ServiceContext.EncounterService.GetCurrentEncounter();
        var player = ServiceContext.PlayerDataService.GetPlayer(toadPlayer.ContentId, toadPlayer.Name, toadPlayer.HomeWorld);

        if (enc == null)
        {
            HandleContentIdUpdateOnly(player, toadPlayer);
            Plugin.PluginLog.Verbose("Encounter is missing.");

            // Context menu / user request: always open the panel regardless of encounter state.
            if (isUserRequest && player != null)
            {
                OnPlayerSelected?.Invoke(player);
            }
            // Bio scraper: notify for known players entering the zone even without an encounter,
            // so bios can be collected independently of encounter tracking.
            else if (isCurrent && !isUserRequest && player != null)
            {
                player.EntityId = toadPlayer.EntityId; // EntityId is session-specific; keep it current.
                OnCurrentPlayerAdded?.Invoke(player);
            }
            return;
        }

        if (!enc.SavePlayers && !isUserRequest)
        {
            HandleContentIdUpdateOnly(player, toadPlayer);
            Plugin.PluginLog.Verbose("Encounter is not set to save players.");

            // Bio scraper: notify for known players even when the encounter does not save players.
            if (isCurrent && player != null)
            {
                player.EntityId = toadPlayer.EntityId;
                OnCurrentPlayerAdded?.Invoke(player);
            }
            return;
        }

        var loc = PlayerEncounterService.GetEncounterLocation();
        if (player == null)
        {
            Plugin.PluginLog.Verbose("Player not found, creating new player.");
            var key = PlayerKeyBuilder.Build(toadPlayer.Name, toadPlayer.HomeWorld);
            CreateNewPlayer(toadPlayer, key, isCurrent, enc.CategoryId, loc);
            player = ServiceContext.PlayerDataService.GetPlayer(toadPlayer.ContentId, toadPlayer.Name, toadPlayer.HomeWorld);
            if (player != null)
            {
                if (isUserRequest)
                    OnPlayerSelected?.Invoke(player);
                else if (enc.SaveEncounter)
                    player.OpenPlayerEncounterId = PlayerEncounterService.CreatePlayerEncounter(toadPlayer, player);
            }
        }
        else if (isUserRequest)
        {
            Plugin.PluginLog.Verbose("Force load player, used for player search.");
            player = UpdateExistingPlayer(player, toadPlayer, isCurrent, loc);
            OnPlayerSelected?.Invoke(player);
        }
        else if (!player.IsCurrent)
        {
            Plugin.PluginLog.Verbose("Player found, updating existing player.");
            player = UpdateExistingPlayer(player, toadPlayer, isCurrent, loc);
            ServiceContext.PlayerAlertService.SendProximityAlert(player);
            if (enc.SaveEncounter)
                player.OpenPlayerEncounterId = PlayerEncounterService.CreatePlayerEncounter(toadPlayer, player);
        }
    }

    internal void RemoveCurrentPlayer(uint entityId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerProcessService.RemoveCurrentPlayer(): {entityId}");
        var player = ServiceContext.PlayerDataService.GetPlayer(entityId);
        if (player == null)
        {
            Plugin.PluginLog.Verbose("Player not found.");
            return;
        }

        if (!player.IsCurrent)
        {
            Plugin.PluginLog.Verbose("Player is not current.");
            return;
        }

        RemoveCurrentPlayer(player);
    }

    internal void RemoveCurrentPlayer(Player player)
    {
        PlayerEncounterService.EndPlayerEncounter(player, ServiceContext.EncounterService.GetCurrentEncounter());
        player.IsCurrent = false;
        player.LastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        player.OpenPlayerEncounterId = 0;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
        OnCurrentPlayerRemoved?.Invoke(player);
    }

    private static void HandleContentIdUpdateOnly(Player? player, PlayerData toadPlayer)
    {
        if (player is not { ContentId: 0 })
            return;

        player.ContentId = toadPlayer.ContentId;
        Plugin.PluginLog.Verbose($"Player content id updated: {player.Name}@{player.WorldId}");
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    private void CreateNewPlayer(PlayerData toadPlayer, string key, bool isCurrent, int categoryId, LocationData loc)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var player = new Player
        {
            Key = key,
            EntityId = toadPlayer.EntityId,
            ContentId = toadPlayer.ContentId,
            Name = toadPlayer.Name,
            WorldId = toadPlayer.HomeWorld,
            PrimaryCategoryId = categoryId,
            FreeCompany = PlayerFCHelper.CheckFreeCompany(toadPlayer.CompanyTag, loc.InContent()),
            Customize = toadPlayer.Customize,
            LastTerritoryType = (ushort)(isCurrent ? loc.TerritoryId : 0),
            Created = currentTime,
            FirstSeen = currentTime,
            IsCurrent = isCurrent,
            IsRecent = isCurrent,
            LastSeen = isCurrent ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0,
            SeenCount = isCurrent ? 1 : 0,
        };

        ServiceContext.PlayerDataService.AddPlayer(player);
        OnCurrentPlayerAdded?.Invoke(player);
    }

    private Player UpdateExistingPlayer(Player player, PlayerData toadPlayer, bool isCurrent, LocationData loc)
    {
        if (isCurrent)
        {
            if (player.Customize != null && !StructuralComparisons.StructuralEqualityComparer.Equals(player.Customize, toadPlayer.Customize))
                PlayerChangeService.AddCustomizeHistory(player.Id, player.Customize);

            if (player.Name != toadPlayer.Name || player.WorldId != toadPlayer.HomeWorld)
            {
                PlayerChangeService.AddNameWorldHistory(player.Id, player.Name, player.WorldId);
                ServiceContext.PlayerAlertService.SendPlayerNameWorldChangeAlert(player, player.Name, player.WorldId, toadPlayer.Name, toadPlayer.HomeWorld);
            }

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            player.Key = PlayerKeyBuilder.Build(toadPlayer.Name, toadPlayer.HomeWorld);
            player.Name = toadPlayer.Name;
            player.WorldId = toadPlayer.HomeWorld;
            player.Customize = toadPlayer.Customize;
            player.FreeCompany = PlayerFCHelper.CheckFreeCompany(toadPlayer.CompanyTag, player.FreeCompany, loc.InContent());
            player.EntityId = toadPlayer.EntityId;
            player.ContentId = toadPlayer.ContentId;
            player.SeenCount += 1;
            player.LastTerritoryType = loc.TerritoryId;
            player.LastSeen = currentTime;
            player.IsCurrent = true;
            player.IsRecent = true;
            if (player.FirstSeen == 0)
                player.FirstSeen = currentTime;

            ServiceContext.PlayerDataService.UpdatePlayer(player);
        }

        player = ServiceContext.PlayerDataService.GetPlayer(player.Id) ?? player;
        OnCurrentPlayerAdded?.Invoke(player);
        return player;
    }
}
