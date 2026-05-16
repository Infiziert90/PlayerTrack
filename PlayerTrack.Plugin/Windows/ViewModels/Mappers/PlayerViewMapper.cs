using System;
using System.Collections.Generic;
using System.Linq;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Helpers;

namespace PlayerTrack.Windows.ViewModels.Mappers;

public static class PlayerViewMapper
{
    private const char MaleSymbol = '\u2642';
    private const char FemaleSymbol = '\u2640';
    private static string Na = string.Empty;

    public static PlayerView MapPlayer(Player player)
    {
        Na = Language.NotAvailable;
        var playerView = new PlayerView
        {
            Id = player.Id,
            Name = player.Name,
            PrimaryCategoryId = player.PrimaryCategoryId,
            PlayerConfig = player.PlayerConfig,
            HomeWorld = GetHomeWorld(player.WorldId),
            DataCenter = GetDataCenter(player.WorldId),
            FreeCompany = GetFreeCompany(player.FreeCompany),
            LodestoneId = player.LodestoneId,
            Appearance = GetAppearance(player.Customize),
            FirstSeen = player.SeenCount != 0 && player.FirstSeen != 0 ? player.FirstSeen.ToTimeSpan() : Na,
            LastSeen = player.SeenCount != 0 && player.LastSeen != 0 ? player.LastSeen.ToTimeSpan() : Na,
            LastLocation = GetLastLocation(player.LastTerritoryType),
            SeenCount = player.SeenCount != 0 ? $"{player.SeenCount}x" : Na,
            Notes = player.Notes,
            PreviousNames = PlayerChangeService.GetPreviousNames(player.Id, player.Name),
            PreviousWorlds = PlayerChangeService.GetPreviousWorlds(player.Id, GetHomeWorld(player.WorldId))
        };

        AddTags(player.AssignedTags, playerView);
        AddCategories(player.AssignedCategories, playerView);
        AddEncounters(player.Id, playerView);
        AddPlayerHistory(player.Id, playerView);
        AddBioHistory(player.Id, playerView);

        return playerView;
    }

    public static string GetLastLocation(uint lastTerritoryType)
    {
        var locationName = lastTerritoryType != 0 ? Sheets.Locations[lastTerritoryType].GetName() : null;
        return string.IsNullOrEmpty(locationName) ? Na : locationName;
    }

    private static string GetHomeWorld(uint worldId)
    {
        var worldName = Sheets.GetWorldNameById(worldId);
        return !string.IsNullOrEmpty(worldName) ? worldName : Na;
    }

    private static string GetDataCenter(uint worldId)
    {
        if (worldId == 0) return Na;
        var dc = Sheets.GetDataCenterNameByWorldId(worldId);
        return !string.IsNullOrEmpty(dc) ? dc : Na;
    }

    private static string GetFreeCompany(KeyValuePair<FreeCompanyState, string> freeCompany)
    {
        switch (freeCompany.Key)
        {
            case FreeCompanyState.InFC:
                return freeCompany.Value;
            case FreeCompanyState.NotInFC:
                return Language.None;
            case FreeCompanyState.Unknown:
            default:
                return Na;
        }
    }

    private static string GetAppearance(byte[]? customizeArr)
    {
        if (customizeArr is { Length: > 0 })
        {
            var customize = CharaCustomizeData.MapCustomizeData(customizeArr);
            return customize.Gender switch
            {
                0 => $"{Sheets.Races[customize.Race].MasculineName} {MaleSymbol}",
                1 => $"{Sheets.Races[customize.Race].FeminineName} {FemaleSymbol}",
                _ => Na,
            };
        }

        return Na;
    }

    private static void AddTags(IReadOnlyCollection<Tag> assignedTags, PlayerView playerView)
    {
        var tags = ServiceContext.TagService.GetAllTags();
        if (tags.Count == 0)
            return;

        foreach (var tag in tags)
        {
            if (assignedTags.Any(t => t.Id == tag.Id))
                playerView.AssignedTags.Add(tag);
            else
                playerView.UnassignedTags.Add(tag);
        }
    }

    private static void AddCategories(IReadOnlyCollection<Category> assignedCategories, PlayerView playerView)
    {
        var cats = ServiceContext.CategoryService.GetCategories();
        if (cats.Count == 0)
            return;

        foreach (var cat in cats)
        {
            if (assignedCategories.Any(c => c.Id == cat.Id))
                playerView.AssignedCategories.Add(cat);
            else if (cat.SocialListId == 0)
                playerView.UnassignedCategories.Add(cat);
        }
    }

    private static void AddEncounters(int playerId, PlayerView playerView)
    {
        playerView.Encounters = [];
        playerView.TotalEncounterTime   = Na;
        playerView.LongestEncounterTime = Na;

        var pEncs = PlayerEncounterService.GetPlayerEncountersByPlayer(playerId);
        if (pEncs == null || pEncs.Count == 0)
            return;

        var now        = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long totalMs   = 0L;
        long longestMs = 0L;

        foreach (var pEnc in pEncs)
        {
            var enc = EncounterService.GetEncounter(pEnc.EncounterId);
            if (enc == null)
                continue;

            var durationMs = pEnc.Ended == 0
                ? now - pEnc.Created
                : pEnc.Ended - pEnc.Created;

            if (durationMs < 0) durationMs = 0;

            totalMs   += durationMs;
            if (durationMs > longestMs)
                longestMs = durationMs;

            playerView.Encounters.Add(new PlayerEncounterView
            {
                Id           = pEnc.Id,
                Time         = pEnc.Created.ToTimeSpan(),
                Duration     = durationMs.ToDuration(),
                Job          = Sheets.ClassJobs[pEnc.JobId].Code,
                Level        = pEnc.JobLvl.ToString(),
                Location     = GetLastLocation(enc.TerritoryTypeId),
                LocationType = Sheets.Locations.TryGetValue(enc.TerritoryTypeId, out var loc)
                    ? loc.LocationType
                    : Data.LocationType.None,
            });
        }

        if (totalMs   > 0) playerView.TotalEncounterTime   = totalMs.ToDuration();
        if (longestMs > 0) playerView.LongestEncounterTime = longestMs.ToDuration();
    }

    private static void AddBioHistory(int playerId, PlayerView playerView)
    {
        playerView.BioHistory = [];
        var bios = PlayerBioService.GetBioHistory(playerId);
        foreach (var bio in bios)
        {
            playerView.BioHistory.Add(new PlayerBioView
            {
                Bio  = bio.Bio,
                When = bio.Created.ToTimeSpan(),
            });
        }
    }

    private static void AddPlayerHistory(int playerId, PlayerView playerView)
    {
        var nameWorldHistories = PlayerChangeService.GetPlayerNameWorldHistory(playerId);
        if (nameWorldHistories.Count == 0)
        {
            playerView.PlayerNameWorldHistories = [];
        }
        else
        {
            foreach (var nameWorldHistory in nameWorldHistories)
            {
                var playerHistory = new PlayerNameWorldHistoryView { Time = nameWorldHistory.Created.ToTimeSpan(), };

                if (nameWorldHistory.IsMigrated)
                {
                    if (nameWorldHistory.WorldId == 0)
                        playerHistory.NameWorld = $"{nameWorldHistory.PlayerName}@{Language.NotAvailable}";
                    else if (string.IsNullOrEmpty(nameWorldHistory.PlayerName))
                        playerHistory.NameWorld = $"{Language.NotAvailable}@{GetHomeWorld(nameWorldHistory.WorldId)}";
                }
                else
                {
                    playerHistory.NameWorld = $"{nameWorldHistory.PlayerName}@{GetHomeWorld(nameWorldHistory.WorldId)}";
                }

                playerView.PlayerNameWorldHistories.Add(playerHistory);
            }
        }

        var customizedHistories = PlayerChangeService.GetPlayerCustomizeHistory(playerId);
        if (customizedHistories.Count == 0)
        {
            playerView.PlayerCustomizeHistories = [];
        }
        else
        {
            foreach (var customizedHistory in customizedHistories)
            {
                var playerHistory = new PlayerCustomizeHistoryView
                {
                    Time = customizedHistory.Created.ToTimeSpan(),
                    Appearance = GetAppearance(customizedHistory.Customize)
                };

                if (playerView.PlayerCustomizeHistories.Count > 0)
                {
                    var lastAppearance = playerView.PlayerCustomizeHistories.Last();
                    if (lastAppearance.Appearance == playerHistory.Appearance)
                        continue;
                }

                playerView.PlayerCustomizeHistories.Add(playerHistory);
            }
        }
    }
}
