using FluentDapperLite.Repository;
using PlayerTrack.Models;

// ReSharper disable InconsistentNaming
namespace PlayerTrack.Infrastructure;

public class PlayerDTO : DTO
{
    public long last_alert_sent { get; set; }

    public long last_name_change_alert_sent { get; set; }

    public long last_world_change_alert_sent { get; set; }

    public long last_seen { get; set; }
    
    public long first_seen { get; set; }

    public byte[]? customize { get; set; }

    public int seen_count { get; set; }

    public int lodestone_status { get; set; }

    public long lodestone_verified_on { get; set; }

    public FreeCompanyState free_company_state { get; set; } = FreeCompanyState.Unknown;

    public string free_company_tag { get; set; } = string.Empty;

    public string key { get; set; } = string.Empty;

    public string name { get; set; } = string.Empty;

    public string notes { get; set; } = string.Empty;

    public uint lodestone_id { get; set; }

    public uint entity_id { get; set; }

    public uint world_id { get; set; }

    public ushort last_territory_type { get; set; }
    
    public ulong content_id { get; set; }
}
