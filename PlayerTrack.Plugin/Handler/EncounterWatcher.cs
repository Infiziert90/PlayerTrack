using System;
using System.Linq;
using PlayerTrack.Domain;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Handler;

/// <summary>
/// Evaluates <see cref="EncounterRule"/> entries when an encounter ends and
/// assigns PlayerTrack categories to players whose single-session encounter
/// duration in the specified zone meets or exceeds the rule threshold.
///
/// Evaluation is intentionally lazy:
///   1. If no rules exist for the territory type, the handler returns immediately
///      with no database work.
///   2. If the encounter itself was shorter than the global minimum
///      (<see cref="PlayerTrack.Models.PluginConfig.EncounterRuleMinEncounterSeconds"/>),
///      the handler returns without fetching player encounters.
///   3. Only when both guards pass does the handler perform the single
///      GetAllByEncounterId query and iterate over players.
/// </summary>
public static class EncounterWatcher
{
    public static void Start()
    {
        Plugin.PluginLog.Verbose("Entering EncounterWatcher.Start()");
        EncounterService.EncounterEnded += OnEncounterEnded;
        Plugin.PluginLog.Information("[EncounterWatcher] Registered for encounter-end events.");
    }

    public static void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering EncounterWatcher.Dispose()");
        EncounterService.EncounterEnded -= OnEncounterEnded;
    }

    private static void OnEncounterEnded(Encounter enc)
    {
        try
        {
            var config = ServiceContext.ConfigService.GetConfig();

            // Guard 1: any enabled rules targeting this territory type?
            var rules = config.EncounterRules
                .Where(r => r.Enabled && r.TerritoryTypeId == enc.TerritoryTypeId && r.CategoryId != 0)
                .ToList();
            if (rules.Count == 0) return;

            // Guard 2: global minimum encounter duration.
            var encDurationMs = enc.Ended - enc.Created;
            var globalMinMs   = (long)config.EncounterRuleMinEncounterSeconds * 1000L;
            if (config.EncounterRuleMinEncounterSeconds > 0 && encDurationMs < globalMinMs)
            {
                Plugin.PluginLog.Debug(
                    $"[EncounterWatcher] Zone {enc.TerritoryTypeId}: encounter {enc.Id} " +
                    $"lasted {encDurationMs / 1000}s < global min {config.EncounterRuleMinEncounterSeconds}s; skipping.");
                return;
            }

            // Fetch player encounters (timestamps already committed by EndPlayerEncounters).
            var playerEncounters = RepositoryContext.PlayerEncounterRepository.GetAllByEncounterId(enc.Id);
            if (playerEncounters == null || playerEncounters.Count == 0) return;

            Plugin.PluginLog.Debug(
                $"[EncounterWatcher] Zone {enc.TerritoryTypeId}: evaluating {rules.Count} rule(s) " +
                $"against {playerEncounters.Count} player encounter(s) " +
                $"(zone duration={encDurationMs / 1000}s).");

            foreach (var pe in playerEncounters)
            {
                if (pe.Ended == 0 || pe.Created == 0) continue;
                var peDurationMs = pe.Ended - pe.Created;

                foreach (var rule in rules)
                {
                    if (peDurationMs < (long)rule.MinDurationSeconds * 1000L) continue;

                    var player = ServiceContext.PlayerDataService.GetPlayer(pe.PlayerId);
                    if (player == null)
                    {
                        Plugin.PluginLog.Warning(
                            $"[EncounterWatcher] Player id={pe.PlayerId} not in cache; skipping.");
                        continue;
                    }

                    Plugin.PluginLog.Information(
                        $"[EncounterWatcher] Rule matched: zone={enc.TerritoryTypeId} " +
                        $"player=\"{player.Name}\" " +
                        $"duration={peDurationMs / 1000}s >= {rule.MinDurationSeconds}s " +
                        $"=> categoryId={rule.CategoryId}");

                    PlayerCategoryService.AssignCategoryToPlayerSync(player.Id, (int)rule.CategoryId);
                    break; // First matching rule wins per player.
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[EncounterWatcher] Unhandled exception in OnEncounterEnded.");
        }
    }
}
