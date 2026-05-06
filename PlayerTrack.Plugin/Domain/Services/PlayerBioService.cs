using System;
using System.Collections.Generic;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Domain;

/// <summary>
/// Manages the per-player plate bio history.
/// Bio entries are written only when the bio text has changed since the last
/// recorded entry, preventing duplicate records for repeated plate views.
/// </summary>
public class PlayerBioService
{
    /// <summary>
    /// Records a new bio snapshot for <paramref name="playerId"/> only if the
    /// text differs from the most recently stored entry.  Empty bios are ignored.
    /// </summary>
    public static void UpdateBioIfChanged(int playerId, string bio)
    {
        if (string.IsNullOrWhiteSpace(bio)) return;

        try
        {
            var latest = RepositoryContext.PlayerBioRepository.GetLatestByPlayerId(playerId);
            if (latest != null && latest.Bio == bio)
                return; // Bio unchanged; skip.

            Plugin.PluginLog.Debug(
                $"[PlayerBioService] Recording new bio for player {playerId} " +
                $"(length={bio.Length}).");

            RepositoryContext.PlayerBioRepository.CreatePlayerBio(new PlayerBio
            {
                PlayerId = playerId,
                Bio      = bio,
            });
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"[PlayerBioService] Failed to update bio for player {playerId}.");
        }
    }

    /// <summary>Returns all recorded bios for a player, newest first.</summary>
    public static List<PlayerBio> GetBioHistory(int playerId)
    {
        try
        {
            return RepositoryContext.PlayerBioRepository.GetAllByPlayerId(playerId);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"[PlayerBioService] Failed to get bio history for player {playerId}.");
            return [];
        }
    }
}
