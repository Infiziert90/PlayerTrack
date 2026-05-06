using System;

namespace PlayerTrack.Models;

/// <summary>
/// A single zone-time-to-category mapping rule.  When a player's single-session
/// encounter duration in the specified territory type meets or exceeds
/// <see cref="MinDurationSeconds"/>, the configured category is assigned.
/// </summary>
[Serializable]
public sealed class EncounterRule
{
    /// <summary>FFXIV territory type ID of the zone to watch.</summary>
    public uint TerritoryTypeId { get; set; } = 0;

    /// <summary>Human-readable zone name cached for the UI (not authoritative).</summary>
    public string ZoneDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Minimum single-session encounter duration in seconds.
    /// The player's encounter in this zone must last at least this long
    /// for the rule to fire.
    /// </summary>
    public int MinDurationSeconds { get; set; } = 300;

    /// <summary>The PlayerTrack category ID to assign when the rule matches.</summary>
    public uint CategoryId { get; set; } = 0;

    /// <summary>Human-readable category name cached for the UI (not authoritative).</summary>
    public string CategoryDisplayName { get; set; } = string.Empty;

    /// <summary>When false, this rule is skipped without being deleted.</summary>
    public bool Enabled { get; set; } = true;

    public override string ToString() =>
        $"EncounterRule[{(Enabled ? "ON" : "OFF")} | " +
        $"zone={TerritoryTypeId} minDuration={MinDurationSeconds}s => categoryId={CategoryId}]";
}
