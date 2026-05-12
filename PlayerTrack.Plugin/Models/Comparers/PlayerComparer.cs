using System;
using System.Collections.Generic;

namespace PlayerTrack.Models.Comparers;

public class PlayerComparer : IComparer<Player>
{
    public readonly Dictionary<int, int> CategoryRanks;
    public readonly int DefaultRank;
    public readonly PlayerSortType SortType;

    // Precomputed per-player sort values used for time- and count-based sorts.
    // Key = player ID, Value = milliseconds (for time sorts) or count (unused --
    // SeenCount is already on the Player object).
    public readonly Dictionary<int, long> SortValues;

    // Convenience constructor -- preserves the original default behaviour.
    public PlayerComparer(Dictionary<int, int> categoryRanks, int defaultRank)
        : this(categoryRanks, defaultRank, PlayerSortType.ByCategory, new Dictionary<int, long>()) { }

    public PlayerComparer(
        Dictionary<int, int> categoryRanks,
        int defaultRank,
        PlayerSortType sortType,
        Dictionary<int, long> sortValues)
    {
        CategoryRanks = categoryRanks;
        CategoryRanks.TryAdd(0, defaultRank);
        DefaultRank  = defaultRank;
        SortType     = sortType;
        SortValues   = sortValues;
    }

    public int Compare(Player? x, Player? y)
    {
        try
        {
            if (ReferenceEquals(x, y))   return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;

            int primaryCmp;
            int secondaryCmp;

            switch (SortType)
            {
                // ---------------------------------------------------------------
                // ByCategory: original behaviour -- category rank is primary.
                // ---------------------------------------------------------------
                case PlayerSortType.ByCategory:
                {
                    var xRank = CategoryRanks.GetValueOrDefault(x.PrimaryCategoryId, DefaultRank);
                    var yRank = CategoryRanks.GetValueOrDefault(y.PrimaryCategoryId, DefaultRank);
                    primaryCmp = xRank.CompareTo(yRank);
                    if (primaryCmp != 0) return primaryCmp;

                    secondaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (secondaryCmp != 0) return secondaryCmp;
                    break;
                }

                // ---------------------------------------------------------------
                // ByName: alphabetical, then world, then created for uniqueness.
                // ---------------------------------------------------------------
                case PlayerSortType.ByName:
                {
                    primaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (primaryCmp != 0) return primaryCmp;
                    break;
                }

                // ---------------------------------------------------------------
                // ByLastSeen: most recent first.
                // ---------------------------------------------------------------
                case PlayerSortType.ByLastSeen:
                {
                    primaryCmp = y.LastSeen.CompareTo(x.LastSeen);
                    if (primaryCmp != 0) return primaryCmp;
                    secondaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (secondaryCmp != 0) return secondaryCmp;
                    break;
                }

                // ---------------------------------------------------------------
                // ByFirstSeen: oldest first.
                // ---------------------------------------------------------------
                case PlayerSortType.ByFirstSeen:
                {
                    // Players with no first-seen data sort to the bottom.
                    if (x.FirstSeen == 0 && y.FirstSeen == 0) break;
                    if (x.FirstSeen == 0) return 1;
                    if (y.FirstSeen == 0) return -1;

                    primaryCmp = x.FirstSeen.CompareTo(y.FirstSeen);
                    if (primaryCmp != 0) return primaryCmp;
                    secondaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (secondaryCmp != 0) return secondaryCmp;
                    break;
                }

                // ---------------------------------------------------------------
                // BySeenCount: highest encounter count first.
                // ---------------------------------------------------------------
                case PlayerSortType.BySeenCount:
                {
                    primaryCmp = y.SeenCount.CompareTo(x.SeenCount);
                    if (primaryCmp != 0) return primaryCmp;
                    secondaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (secondaryCmp != 0) return secondaryCmp;
                    break;
                }

                // ---------------------------------------------------------------
                // ByTotalEncounterTime / ByZoneEncounterTime:
                // SortValues holds precomputed millisecond totals keyed by player ID.
                // Players without data sort to the bottom (0 ms).
                // ---------------------------------------------------------------
                case PlayerSortType.ByTotalEncounterTime:
                case PlayerSortType.ByZoneEncounterTime:
                {
                    var xMs = SortValues.GetValueOrDefault(x.Id, 0L);
                    var yMs = SortValues.GetValueOrDefault(y.Id, 0L);
                    primaryCmp = yMs.CompareTo(xMs); // descending
                    if (primaryCmp != 0) return primaryCmp;
                    secondaryCmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    if (secondaryCmp != 0) return secondaryCmp;
                    break;
                }

                default:
                    break;
            }

            // Final tiebreakers: world then creation timestamp.
            // name+world is a unique key, so Created is only reached for
            // players that share both (not possible in practice).
            var worldCmp = x.WorldId.CompareTo(y.WorldId);
            if (worldCmp != 0) return worldCmp;

            return x.Created.CompareTo(y.Created);
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
