namespace PlayerTrack.Models;

public enum PlayerSortType
{
    /// <summary>Primary sort by category rank, secondary by name (default behaviour).</summary>
    ByCategory,

    /// <summary>Sort alphabetically by player name.</summary>
    ByName,

    /// <summary>Sort by most recently seen (descending).</summary>
    ByLastSeen,

    /// <summary>Sort by first time seen (ascending, oldest first).</summary>
    ByFirstSeen,

    /// <summary>Sort by total encounter count (descending).</summary>
    BySeenCount,

    /// <summary>Sort by total cumulative encounter time across all zones (descending).</summary>
    ByTotalEncounterTime,

    /// <summary>Sort by cumulative encounter time within a specific zone (descending).</summary>
    ByZoneEncounterTime,
}
