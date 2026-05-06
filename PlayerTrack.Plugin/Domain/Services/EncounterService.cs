using System;
using System.Collections.Generic;
using System.Linq;
using PlayerTrack.Data;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Domain;

public class EncounterService
{
    private const long NinetyDaysInMilliseconds = 7776000000;
    private const int MaxBatchSize = 500;

    public Encounter? CurrentEncounter { get; private set;  }
    public Encounter? CurrentEncounterSnapshot { get; private set;  }

    /// <summary>
    /// Fired after an encounter ends and all player encounter timestamps have
    /// been committed to the database.  Subscribers (e.g. EncounterWatcher)
    /// receive the closed <see cref="Encounter"/> and may safely query
    /// player encounter durations via the repository.
    /// </summary>
    public static event Action<Encounter>? EncounterEnded;

    public static void UpdateEncounter(Encounter encounter) =>
        RepositoryContext.EncounterRepository.UpdateEncounter(encounter);

    public static void EnsureNoOpenEncounters()
    {
        Plugin.PluginLog.Verbose("Entering EncounterService.EnsureNoOpenEncounters()");
        var encounters = RepositoryContext.EncounterRepository.GetAllOpenEncounters();
        if (encounters == null || encounters.Count == 0)
        {
            Plugin.PluginLog.Verbose("No open encounters found.");
            return;
        }

        foreach (var encounter in encounters)
        {
            Plugin.PluginLog.Verbose($"Ending encounter: {encounter.Id}");
            encounter.Ended = encounter.Updated;
            UpdateEncounter(encounter);
            PlayerEncounterService.EndPlayerEncounters(encounter.Id);
        }
    }

    public static Encounter? GetEncounter(int id) =>
        RepositoryContext.EncounterRepository.GetEncounter(id);

    public static void CreateEncounter(Encounter encounter) =>
        RepositoryContext.EncounterRepository.CreateEncounter(encounter);

    public static int GetEncountersCount() =>
        RepositoryContext.EncounterRepository.GetAllEncounters()?.Count ?? 0;

    public int GetEncountersForDeletionCount() =>
        GetEncountersForDeletion().Count;

    public void Dispose() =>
        EndCurrentEncounter();

    public Encounter? GetCurrentEncounter()
    {
        CurrentEncounterSnapshot = CurrentEncounter;
        return CurrentEncounterSnapshot;
    }

    public void StartCurrentEncounter(LocationData location)
    {
        Plugin.PluginLog.Verbose($"Entering EncounterService.StartCurrentEncounter(): {location.TerritoryId}");
        var loc = Sheets.Locations[location.TerritoryId];

        CreateEncounter(new Encounter { TerritoryTypeId = location.TerritoryId });
        CurrentEncounter = RepositoryContext.EncounterRepository.GetOpenEncounter();
        if (CurrentEncounter == null)
        {
            Plugin.PluginLog.Warning("Failed to start encounter.");
            return;
        }

        CurrentEncounter.CategoryId = CategoryService.GetDefaultCategory(loc);
        CurrentEncounter.SaveEncounter = ShouldSaveEncounter(loc);
        CurrentEncounter.SavePlayers = ShouldSavePlayers(loc);
    }

    public void EndCurrentEncounter()
    {
        Plugin.PluginLog.Verbose("Entering EncounterService.EndCurrentEncounter()");
        if (CurrentEncounter == null)
        {
            if (Plugin.ClientStateHandler.IsLoggedIn)
                Plugin.PluginLog.Warning("Failed to end encounter.");

            return;
        }

        CurrentEncounter.Ended = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UpdateEncounter(CurrentEncounter);
        PlayerEncounterService.EndPlayerEncounters(CurrentEncounter.Id);

        // Capture and clear before firing so subscribers see a null CurrentEncounter
        // (consistent with the post-encounter state) but still have the closed record.
        var ended = CurrentEncounter;
        CurrentEncounter = null;
        EncounterEnded?.Invoke(ended);
    }

    public void DeleteEncounters()
    {
        var encounters = GetEncountersForDeletion();
        var encounterIds = encounters.Select(e => e.Id).ToList();

        for (var i = 0; i < encounterIds.Count; i += MaxBatchSize)
        {
            var currentBatch = encounterIds.Skip(i).Take(MaxBatchSize).ToList();
            RepositoryContext.EncounterRepository.DeleteEncountersWithRelations(currentBatch);
        }

        RepositoryContext.RunMaintenanceChecks(true);
    }

    private static bool ShouldSaveEncounter(LocationData loc)
    {
        Plugin.PluginLog.Verbose($"Entering EncounterService.ShouldSaveEncounter(): {loc.LocationType}");
        var config = ServiceContext.ConfigService.GetConfig().GetTrackingLocationConfig(loc.LocationType);
        return config.AddEncounters;
    }

    private static bool ShouldSavePlayers(LocationData loc)
    {
        Plugin.PluginLog.Verbose($"Entering EncounterService.ShouldSavePlayers(): {loc.LocationType}");
        var config = ServiceContext.ConfigService.GetConfig().GetTrackingLocationConfig(loc.LocationType);
        return config.AddPlayers;
    }

    private List<Encounter> GetEncountersForDeletion()
    {
        var allEncounters = RepositoryContext.EncounterRepository.GetAllEncounters();
        if (allEncounters == null)
            return [];

        var currentTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var options = ServiceContext.ConfigService.GetConfig().EncounterDataActionOptions;
        var encountersForDeletion = new List<Encounter>();

        foreach (var encounter in allEncounters)
        {
            var location = Sheets.Locations[encounter.TerritoryTypeId];

            var shouldDelete =
                !(options.KeepEncountersInOverworld && location.LocationType == LocationType.Overworld) &&
                !(options.KeepEncountersInNormalContent && location.LocationType == LocationType.Content) &&
                !(options.KeepEncountersInHighEndContent && location.LocationType == LocationType.HighEndContent) &&
                !(options.KeepEncountersFromLast90Days && currentTimeUnix - encounter.Created <= NinetyDaysInMilliseconds);

            if (shouldDelete)
                encountersForDeletion.Add(encounter);
        }

        if (CurrentEncounter != null && encountersForDeletion.Any(encounter => encounter.Id == CurrentEncounter?.Id))
            encountersForDeletion.Remove(CurrentEncounter);

        return encountersForDeletion;
    }
}
