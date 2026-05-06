namespace PlayerTrack.Models;

public class PlayerBio
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string Bio { get; set; } = string.Empty;
    public long Created { get; set; }
    public long Updated { get; set; }
}
