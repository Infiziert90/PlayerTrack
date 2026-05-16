using PlayerTrack.Data;

namespace PlayerTrack.Windows.ViewModels;

public class PlayerEncounterView
{
    public int Id { get; set; }

    public string Time { get; init; } = null!;

    public string Duration { get; init; } = null!;

    public string Job { get; init; } = null!;

    public string Level { get; init; } = null!;

    public string Location { get; init; } = null!;

    public LocationType LocationType { get; init; } = LocationType.None;
}
