using System;
using System.Collections.Concurrent;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using PlayerTrack.Domain;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Handler;

/// <summary>
/// Automatically fetches Adventurer Plate bios for players that enter the zone
/// without requiring DailyRoutines or any other external plugin.
///
/// Design overview
/// ---------------
/// 1. OnCurrentPlayerAdded fires whenever PlayerTrack registers a new or
///    returning player in the current zone.
/// 2. The handler checks the database for a recent bio entry.  If the most
///    recent entry is missing or older than AutoScrapeStaleAfterDays, the
///    player is pushed onto a work queue.
/// 3. OnFrameworkUpdate (game main thread) dequeues one entry per interval,
///    verifies the player's game object is still present, then opens the
///    CharaCard addon via AgentCharaCard.Instance()->OpenCharaCard().
/// 4. PlateWatcher's existing OnCharaCardUpdate handler reads the bio nodes
///    as normal and calls PlayerBioService.UpdateBioIfChanged.  When it
///    finishes it fires PlateWatcher.OnPlateProcessed.
/// 5. BioScraper receives OnPlateProcessed, hides the CharaCard window (only
///    if BioScraper was the one that opened it), and marks itself ready for
///    the next queue entry.
/// </summary>
public static class BioScraper
{
    // ----------------------------------------------------------------
    // Defaults (used when config values are out of range)
    // ----------------------------------------------------------------

    private const int FallbackIntervalSeconds = 8;

    // ----------------------------------------------------------------
    // Work queue and processing state
    // ----------------------------------------------------------------

    private static readonly ConcurrentQueue<(int PlayerId, uint EntityId)> Queue = new();

    /// <summary>True while BioScraper is waiting for PlateWatcher to finish
    /// reading the plate it opened.  Used to prevent double-opens and to
    /// gate the CharaCard hide on the correct caller.</summary>
    private static volatile bool _isProcessing;

    /// <summary>Timestamp (ms) of the last OpenCharaCard call.</summary>
    private static long _lastOpenedMs;

    private static bool _started;

    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    public static void Start()
    {
        Plugin.PluginLog.Verbose("Entering BioScraper.Start()");
        ServiceContext.PlayerProcessService.OnCurrentPlayerAdded += OnPlayerAdded;
        PlateWatcher.OnPlateProcessed += OnPlateProcessed;
        Plugin.GameFramework.Update += OnFrameworkUpdate;
        _started = true;
        Plugin.PluginLog.Information("[BioScraper] Started.");
    }

    public static void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering BioScraper.Dispose()");
        if (!_started) return;
        ServiceContext.PlayerProcessService.OnCurrentPlayerAdded -= OnPlayerAdded;
        PlateWatcher.OnPlateProcessed -= OnPlateProcessed;
        Plugin.GameFramework.Update -= OnFrameworkUpdate;
        _started = false;
        Plugin.PluginLog.Information("[BioScraper] Disposed.");
    }

    // ----------------------------------------------------------------
    // Step 1 -- enqueue players that need a fresh bio
    // ----------------------------------------------------------------

    /// <summary>
    /// Called from a thread-pool thread by PlayerProcessService.
    /// ConcurrentQueue and the read-only DB call are both thread-safe.
    /// </summary>
    private static void OnPlayerAdded(Player player)
    {
        try
        {
            var config = ServiceContext.ConfigService.GetConfig();
            if (!config.AutoScrapeEnabled)
                return;

            // Local player: EntityId 0 means we have no game-object handle.
            if (player.Id == 0 || player.EntityId == 0)
                return;

            // Skip if the bio on record is still fresh.
            var latest = RepositoryContext.PlayerBioRepository.GetLatestByPlayerId(player.Id);
            if (latest != null && config.AutoScrapeStaleAfterDays > 0)
            {
                var ageMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - latest.Created;
                var staleMs = (long)config.AutoScrapeStaleAfterDays * 86_400_000L;
                if (ageMs < staleMs)
                {
                    Plugin.PluginLog.Verbose(
                        $"[BioScraper] Skipping player id={player.Id} -- bio is fresh " +
                        $"({ageMs / 86_400_000.0:F1} days old, threshold={config.AutoScrapeStaleAfterDays}d).");
                    return;
                }
            }

            Queue.Enqueue((player.Id, player.EntityId));
            Plugin.PluginLog.Debug(
                $"[BioScraper] Queued player id={player.Id} entityId={player.EntityId} " +
                $"name=\"{player.Name}\".");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[BioScraper] Unhandled exception in OnPlayerAdded.");
        }
    }

    // ----------------------------------------------------------------
    // Step 2 -- throttled game-thread processor
    // ----------------------------------------------------------------

    private static unsafe void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            // Guard: another open is already in flight.
            if (_isProcessing) return;

            // Guard: nothing to do.
            if (Queue.IsEmpty) return;

            var config = ServiceContext.ConfigService.GetConfig();
            if (!config.AutoScrapeEnabled) return;

            // Throttle: enforce minimum interval between openings.
            var intervalMs = (long)Math.Max(1, config.AutoScrapeIntervalSeconds > 0
                ? config.AutoScrapeIntervalSeconds
                : FallbackIntervalSeconds) * 1_000L;

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (nowMs - _lastOpenedMs < intervalMs) return;

            if (!Queue.TryDequeue(out var entry)) return;

            var (playerId, entityId) = entry;

            // Verify the player's game object is still in the zone.
            var obj = Plugin.ObjectCollection.FirstOrDefault(i => i.EntityId == entityId);
            if (obj == null)
            {
                Plugin.PluginLog.Debug(
                    $"[BioScraper] Player id={playerId} entityId={entityId} " +
                    "has left the zone; discarding.");
                return;
            }

            Plugin.PluginLog.Debug(
                $"[BioScraper] Opening CharaCard for player id={playerId} entityId={entityId}.");

            _isProcessing = true;
            _lastOpenedMs = nowMs;

            AgentCharaCard.Instance()->OpenCharaCard((GameObject*)obj.Address);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[BioScraper] Unhandled exception in OnFrameworkUpdate.");
            _isProcessing = false;
        }
    }

    // ----------------------------------------------------------------
    // Step 3 -- hide window after PlateWatcher has finished reading
    // ----------------------------------------------------------------

    private static unsafe void OnPlateProcessed()
    {
        // If BioScraper was not the caller, leave the window alone.
        if (!_isProcessing) return;

        try
        {
            var config = ServiceContext.ConfigService.GetConfig();
            if (!config.AutoScrapeEnabled) return;

            var agent = AgentCharaCard.Instance();
            if (agent != null)
                agent->AgentInterface.Hide();

            Plugin.PluginLog.Debug("[BioScraper] CharaCard hidden after auto-scrape.");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[BioScraper] Unhandled exception in OnPlateProcessed.");
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
