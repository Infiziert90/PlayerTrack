namespace PlayerTrack.Windows.ViewModels;

/// <summary>A single plate bio history entry as presented in the player summary panel.</summary>
public class PlayerBioView
{
    /// <summary>The bio text as it appeared on the adventurer plate.</summary>
    public string Bio { get; set; } = string.Empty;

    /// <summary>Human-readable relative timestamp (e.g. "3 days ago").</summary>
    public string When { get; set; } = string.Empty;
}
