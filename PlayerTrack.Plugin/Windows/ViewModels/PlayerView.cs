using System.Collections.Generic;
using PlayerTrack.Models;

namespace PlayerTrack.Windows.ViewModels;

public class PlayerView
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string HomeWorld { get; set; } = null!;

    public string FreeCompany { get; set; } = null!;

    public uint LodestoneId { get; set; }

    public string FirstSeen { get; set; } = null!;

    public string LastSeen { get; set; } = null!;

    public string LastLocation { get; set; } = null!;

    public string SeenCount { get; set; } = null!;

    public string Appearance { get; set; } = null!;

    public string TotalEncounterTime { get; set; } = null!;

    public string LongestEncounterTime { get; set; } = null!;

    public string PreviousNames { get; set; } = null!;

    public string PreviousWorlds { get; set; } = null!;

    public List<Tag> AssignedTags { get; set; } = [];

    public List<Tag> UnassignedTags { get; set; } = [];

    public int PrimaryCategoryId { get; set; }

    public List<Category> AssignedCategories { get; set; } = [];

    public List<Category> UnassignedCategories { get; set; } = [];

    public string Notes { get; set; } = null!;

    public PlayerConfig PlayerConfig { get; set; } = null!;

    public List<PlayerEncounterView> Encounters { get; set; } = [];

    public List<PlayerNameWorldHistoryView> PlayerNameWorldHistories { get; set; } = [];

    public List<PlayerCustomizeHistoryView> PlayerCustomizeHistories { get; set; } = [];

    /// <summary>
    /// Plate bio history for this player, newest entry first.
    /// Populated by <see cref="PlayerTrack.Windows.ViewModels.Mappers.PlayerViewMapper"/>.
    /// </summary>
    public List<PlayerBioView> BioHistory { get; set; } = [];
}
