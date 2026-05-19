using FluentDapperLite.Repository;

namespace PlayerTrack.Infrastructure;

using Models;

public class PlayerConfigDTO : DTO
{
    public PlayerConfigType player_config_type { get; set; }

    public string player_list_name_color { get; set; } = string.Empty;

    public string player_list_icon { get; set; } = string.Empty;

    public string nameplate_custom_title { get; set; } = string.Empty;

    public string nameplate_show_in_overworld { get; set; } = string.Empty;

    public string nameplate_show_in_content { get; set; } = string.Empty;

    public string nameplate_show_in_high_end_content { get; set; } = string.Empty;

    public string nameplate_color { get; set; } = string.Empty;

    public string nameplate_use_color { get; set; } = string.Empty;

    public string nameplate_use_color_if_dead { get; set; } = string.Empty;

    public string nameplate_title_type { get; set; } = string.Empty;

    public string alert_name_change { get; set; } = string.Empty;

    public string alert_world_transfer { get; set; } = string.Empty;

    public string alert_proximity { get; set; } = string.Empty;

    public string alert_name_change_frequency { get; set; } = string.Empty;

    public string alert_world_transfer_frequency { get; set; } = string.Empty;

    public string alert_proximity_frequency { get; set; } = string.Empty;

    public string alert_format_include_category { get; set; } = string.Empty;

    public string alert_format_include_custom_title { get; set; } = string.Empty;

    public string visibility_type { get; set; } = string.Empty;

    public int? player_id { get; set; }

    public int? category_id { get; set; }
}
