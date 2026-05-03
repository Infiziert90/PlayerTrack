using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PlayerTrack.Domain;

namespace PlayerTrack.API;

/// <inheritdoc cref="IPlayerTrackAPI" />
[SuppressMessage("Performance", "CA1854:Prefer the \'IDictionary.TryGetValue(TKey, out TValue)\' method")]
public class PlayerTrackAPI : IPlayerTrackAPI
{
    private readonly bool Initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTrackAPI"/> class.
    /// </summary>
    public PlayerTrackAPI()
    {
        Initialized = true;
    }

    /// <inheritdoc />
    public int APIVersion => 1;

    /// <inheritdoc />
    public string GetPlayerCurrentNameWorld(string name, uint worldId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerTrackAPI.GetPlayerCurrentNameWorld({name}, {worldId})");
        CheckInitialized();

        var player = ServiceContext.PlayerDataService.GetPlayer(name, worldId);
        if (player == null)
        {
            Plugin.PluginLog.Warning("Player not found");
            return $"{name} {worldId}";
        }

        return $"{player.Name} {player.WorldId}";
    }

    /// <inheritdoc />
    public string GetPlayerNotes(string name, uint worldId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerTrackAPI.GetPlayerNotes({name}, {worldId})");
        CheckInitialized();
        var player = ServiceContext.PlayerDataService.GetPlayer(name, worldId);
        if (player == null)
        {
            Plugin.PluginLog.Warning("Player not found");
            return string.Empty;
        }

        return player.Notes;
    }

    /// <inheritdoc />
    public ((string, uint), (string, uint)[])[] GetAllPlayerNameWorldHistories()
    {
        Plugin.PluginLog.Verbose($"Entering PlayerTrackAPI.GetAllPlayerNameWorldHistories()");
        CheckInitialized();

        var playerHistories = PlayerChangeService.GetAllPlayerNameWorldHistories();
        if (playerHistories == null)
        {
            Plugin.PluginLog.Warning("No player name/world history found.");
            return [];
        }

        Dictionary<int, List<(string, uint)>> combinedResults = [];
        foreach(var playerHistory in playerHistories)
        {
            if (!combinedResults.ContainsKey(playerHistory.PlayerId))
                combinedResults.Add(playerHistory.PlayerId, []);

            combinedResults[playerHistory.PlayerId].Add((playerHistory.PlayerName, playerHistory.WorldId));
        }

        var playerObjList = ServiceContext.PlayerDataService.GetAllPlayers().Where(x => combinedResults.ContainsKey(x.Id)).ToList();
        List<((string, uint), (string, uint)[])> toReturn = [];
        foreach(var result in combinedResults)
        {
            var player = playerObjList.FirstOrDefault(player => player.Id == result.Key);
            if(player != null)
                toReturn.Add(((player.Name, player.WorldId), result.Value.ToArray()));
        }

        return [.. toReturn];
    }

    /// <inheritdoc />
    public bool AssignCategory(string name, uint worldId, uint categoryId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerTrackAPI.AssignCategory({name}, {worldId}, {categoryId})");
        CheckInitialized();

        var player = ServiceContext.PlayerDataService.GetPlayer(name, worldId);
        if (player == null)
        {
            Plugin.PluginLog.Warning($"AssignCategory: Player not found, name: {name}, worldId: {worldId}");
            return false;
        }

        PlayerCategoryService.AssignCategoryToPlayerSync(player.Id, (int)categoryId);
        return true;
    }

    private void CheckInitialized()
    {
        Plugin.PluginLog.Verbose("Entering PlayerTrackAPI.CheckInitialized()");
        if (Initialized)
            return;

        const string msg = "API is not initialized.";
        Plugin.PluginLog.Warning(msg);
        throw new InvalidOperationException(msg);
    }
}
