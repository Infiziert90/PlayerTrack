using FluentMigrator;

namespace PlayerTrack.Repositories.Migrations;

[Migration(20260519120000)]
public class M008_AlertFrequencies : FluentMigrator.Migration
{
    public override void Up()
    {
        Alter.Table("players")
            .AddColumn("last_name_change_alert_sent").AsInt64().NotNullable().WithDefaultValue(0)
            .AddColumn("last_world_change_alert_sent").AsInt64().NotNullable().WithDefaultValue(0);

        Alter.Table("player_config")
            .AddColumn("alert_name_change_frequency").AsString().NotNullable().WithDefaultValue(string.Empty)
            .AddColumn("alert_world_transfer_frequency").AsString().NotNullable().WithDefaultValue(string.Empty)
            .AddColumn("alert_proximity_frequency").AsString().NotNullable().WithDefaultValue(string.Empty);

        // Backfill JSON defaults. InheritOverride: None=0 (default-level config), Inherit=1 (per-player/category).
        // Default frequencies (ms): name change 5m, world transfer 5m, proximity 4h.
        Execute.Sql(@"
            UPDATE player_config SET
                alert_name_change_frequency = CASE WHEN player_config_type = 0
                    THEN '{""InheritOverride"":0,""Value"":300000}'
                    ELSE '{""InheritOverride"":1,""Value"":0}' END,
                alert_world_transfer_frequency = CASE WHEN player_config_type = 0
                    THEN '{""InheritOverride"":0,""Value"":300000}'
                    ELSE '{""InheritOverride"":1,""Value"":0}' END,
                alert_proximity_frequency = CASE WHEN player_config_type = 0
                    THEN '{""InheritOverride"":0,""Value"":14400000}'
                    ELSE '{""InheritOverride"":1,""Value"":0}' END;
        ");
    }

    public override void Down()
    {
    }
}
