using FluentDapperLite.Repository;

namespace PlayerTrack.Infrastructure;

// ReSharper disable InconsistentNaming
public class PlayerBioDTO : DTO
{
    public int player_id { get; set; }

    public string bio { get; set; } = string.Empty;
}
