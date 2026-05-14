using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using PlayerTrack.Consumers;
using PlayerTrack.Domain.Common;
using PlayerTrack.Models;
using PlayerTrack.Models.Integration;

namespace PlayerTrack.Domain;

public class VisibilityService
{
    private const string Reason = "PlayerTrack";

    public bool IsVisibilityAvailable;
    private readonly VisibilityConsumer VisibilityConsumer;
    private int IsSyncing;

    // Keys of players PlayerTrack has placed into a visibility list.
    // Used to short-circuit framework-thread dispatch for players with VisibilityType.None
    // that we have never tracked, avoiding stutter when many players update simultaneously.
    private readonly HashSet<string> _trackedKeys = [];

    public VisibilityService()
    {
        Plugin.PluginLog.Verbose("Entering VisibilityService.VisibilityService()");
        VisibilityConsumer = new VisibilityConsumer();
    }

    public void Initialize()
    {
        Plugin.PluginLog.Verbose("Entering VisibilityService.Initialize()");
        if (ServiceContext.ConfigService.GetConfig().SyncWithVisibility)
        {
            IsVisibilityAvailable = VisibilityConsumer.IsAvailable();
            Plugin.PluginLog.Verbose($"VisibilityService.VisibilityService() - IsVisibilityAvailable: {IsVisibilityAvailable}");
            if (IsVisibilityAvailable)
                SyncWithVisibility();
        }

        ServiceContext.PlayerDataService.PlayerUpdated += SyncWithVisibility;
        PlayerConfigService.CategoryUpdated += SyncWithVisibility;
    }

    public void Dispose()
    {
        ServiceContext.PlayerDataService.PlayerUpdated -= SyncWithVisibility;
        PlayerConfigService.CategoryUpdated -= SyncWithVisibility;
    }

    private void SyncWithVisibility(int categoryId)
    {
        if (Interlocked.CompareExchange(ref IsSyncing, 1, 0) == 1)
        {
            Plugin.PluginLog.Warning($"VisibilityService.SyncWithVisibility() - Already syncing");
            return;
        }

        Task.Run(() =>
        {
            var category = ServiceContext.CategoryService.GetCategory(categoryId);
            if (category == null)
            {
                Plugin.PluginLog.Warning($"VisibilityService.SyncWithVisibility() - Category not found: {categoryId}");
                Interlocked.Exchange(ref IsSyncing, 0);
                return;
            }

            foreach (var player in ServiceContext.PlayerCacheService.GetCategoryPlayers(categoryId))
                SyncWithVisibility(player);

            Interlocked.Exchange(ref IsSyncing, 0);
        });
    }

    public void SyncWithVisibility(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering VisibilityService.SyncWithVisibility(): {player.Name}");
        if (!IsVisibilityAvailable)
        {
            Plugin.PluginLog.Verbose("VisibilityService.SyncWithVisibility() - Visibility not available");
            Interlocked.Exchange(ref IsSyncing, 0);
            return;
        }

        // Fast pre-check: if the player has no visibility type set and has never been tracked
        // by PlayerTrack, there is no IPC work to do. Skip the framework-thread dispatch to
        // avoid queuing dozens of callbacks simultaneously during zone transitions.
        var visibilityTypeFast = PlayerConfigService.GetVisibilityType(player);
        if (visibilityTypeFast == VisibilityType.None && !_trackedKeys.Contains(player.Key))
        {
            Plugin.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - skipped (None, not tracked)");
            return;
        }

        // Visibility IPC internally accesses ObjectTable which requires the framework thread.
        if (!Plugin.GameFramework.IsInFrameworkUpdateThread)
        {
            Plugin.GameFramework.RunOnFrameworkThread(() => SyncWithVisibility(player));
            return;
        }

        try
        {
            var voidedEntries = GetVisibilityPlayers(VisibilityType.Voidlist);
            var whitelistedEntries = GetVisibilityPlayers(VisibilityType.Whitelist);
            var visibilityType = PlayerConfigService.GetVisibilityType(player);
            Plugin.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType}");

            switch (visibilityType)
            {
                case VisibilityType.None:
                    Plugin.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Removing from visibility");
                    if (voidedEntries.ContainsKey(player.Key))
                        VisibilityConsumer.RemoveFromVoidList(player.Name, player.WorldId);

                    if (whitelistedEntries.ContainsKey(player.Key))
                        VisibilityConsumer.RemoveFromWhiteList(player.Name, player.WorldId);

                    _trackedKeys.Remove(player.Key);
                    break;
                case VisibilityType.Voidlist:
                    Plugin.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Adding to void list");
                    if (!voidedEntries.ContainsKey(player.Key))
                        VisibilityConsumer.AddToVoidList(player.Name, player.WorldId, Reason);

                    _trackedKeys.Add(player.Key);
                    break;
                case VisibilityType.Whitelist:
                    Plugin.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Adding to white list");
                    if (!whitelistedEntries.ContainsKey(player.Key))
                        VisibilityConsumer.AddToWhiteList(player.Name, player.WorldId, Reason);

                    _trackedKeys.Add(player.Key);
                    break;
                default:
                    Plugin.PluginLog.Warning($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Unhandled");
                    break;
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to sync with visibility for player {player.Name}.");
        }
    }

    public void SyncWithVisibility()
    {
        if (!IsVisibilityAvailable)
        {
            Plugin.PluginLog.Verbose("VisibilityService.SyncWithVisibility() - Visibility not available");
            return;
        }

        if (!Plugin.GameFramework.IsInFrameworkUpdateThread)
        {
            Plugin.GameFramework.RunOnFrameworkThread(SyncWithVisibility);
            return;
        }

        Plugin.PluginLog.Verbose("Entering VisibilityService.SyncWithVisibility()");
        try
        {
            var players = ServiceContext.PlayerDataService.GetAllPlayers().ToList();
            var voidedPlayers = players.Where(p => PlayerConfigService.GetVisibilityType(p) == VisibilityType.Voidlist).ToDictionary(p => p.Key, p => p);
            var whitelistedPlayers = players.Where(p => PlayerConfigService.GetVisibilityType(p) == VisibilityType.Whitelist).ToDictionary(p => p.Key, p => p);

            // remove players from void list
            var voidList = GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidList)
                if (!voidedPlayers.ContainsKey(key) && IsSyncedEntry(value.Reason))
                    VisibilityConsumer.RemoveFromVoidList(value.Name, value.HomeWorldId);

            // remove players from white list
            var whiteList = GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whiteList)
                if (!whitelistedPlayers.ContainsKey(key) && IsSyncedEntry(value.Reason))
                    VisibilityConsumer.RemoveFromWhiteList(value.Name, value.HomeWorldId);

            // add players to void list
            voidList = GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidedPlayers)
            {
                if (!voidList.ContainsKey(key))
                    VisibilityConsumer.AddToVoidList(value.Name, value.WorldId, Reason);

                _trackedKeys.Add(key);
            }

            // add players to white list
            whiteList = GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whitelistedPlayers)
            {
                if (!whiteList.ContainsKey(key))
                    VisibilityConsumer.AddToWhiteList(value.Name, value.WorldId, Reason);

                _trackedKeys.Add(key);
            }

            // add void list entries to ptrack
            voidList = GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidList)
            {
                if (players.All(p => p.Key != key))
                {
                    PlayerProcessService.CreateNewPlayer(value.Name, value.HomeWorldId);
                    var player = ServiceContext.PlayerDataService.GetPlayer(value.Name, value.HomeWorldId);
                    if (player == null)
                    {
                        Plugin.PluginLog.Warning($"Failed to create voided player from visibility, key: {key}");
                        continue;
                    }

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Voidlist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
                else
                {
                    var player = players.First(p => p.Key == key);
                    var categoryVisibilityType = PlayerConfigService.GetVisibilityType(player);
                    if (categoryVisibilityType != VisibilityType.None)
                        continue;

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Voidlist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
            }

            // add white list entries to ptrack
            whiteList = GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whiteList)
            {
                if (players.All(p => p.Key != key))
                {
                    PlayerProcessService.CreateNewPlayer(value.Name, value.HomeWorldId);
                    var player = ServiceContext.PlayerDataService.GetPlayer(value.Name, value.HomeWorldId);
                    if (player == null)
                    {
                        Plugin.PluginLog.Warning($"Failed to create whitelisted player from visibility, key: {key}");
                        continue;
                    }

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Whitelist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
                else
                {
                    var player = players.First(p => p.Key == key);
                    var categoryVisibilityType = PlayerConfigService.GetVisibilityType(player);
                    if (categoryVisibilityType != VisibilityType.None)
                        continue;

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Whitelist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to sync with visibility.");
        }
    }

    private static bool IsSyncedEntry(string reason) => reason.Equals(Reason, StringComparison.OrdinalIgnoreCase);

    p