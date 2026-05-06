using FluentDapperLite.Extension;
using FluentMigrator;

namespace PlayerTrack.Repositories.Migrations;

[Migration(20260506120000)]
public class M007_PlayerBios : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("player_bios")
            .WithIdColumn()
            .WithTimeStampColumns()
            .WithColumn("player_id").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("bio").AsString(int.MaxValue).NotNullable().WithDefaultValue(string.Empty);

        Create.Index("idx_player_bios_player_id")
            .OnTable("player_bios")
            .OnColumn("player_id")
            .Ascending();
    }

    public override void Down()
    {
        Delete.Table("player_bios");
    }
}
