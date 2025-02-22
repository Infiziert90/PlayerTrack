﻿using FluentDapperLite.Extension;
using FluentMigrator;

namespace PlayerTrack.Repositories.Migrations;

[Migration(20231120015050)]
public class M002_LocalPlayer : FluentMigrator.Migration
{
    public override void Up()
    {
        CreateLocalPlayerTable();
        CreateSocialListsTable();
        CreateSocialListMembersTable();
        UpdatePlayersTable();
        UpdateCategoriesTable();
    }

    public override void Down()
    {
        Delete.Table("local_players");
        Delete.Table("social_lists");
        Delete.Table("social_list_members");

        Delete.Column("content_id").FromTable("players");
        Delete.Index("idx_players_content_id").OnTable("players");

        Delete.Column("social_list_id").FromTable("categories");
    }

    private void CreateLocalPlayerTable()
    {
        Create.Table("local_players")
            .WithIdColumn()
            .WithTimeStampColumns()
            .WithColumn("content_id").AsUInt64("content_id").Unique().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("world_id").AsUInt32("world_id").NotNullable()
            .WithColumn("key").AsString().NotNullable().Unique()
            .WithColumn("customize").AsBinary().Nullable();
    }

    private void CreateSocialListsTable()
    {
        Create.Table("social_lists")
            .WithIdColumn()
            .WithTimeStampColumns()
            .WithColumn("content_id").AsUInt64("content_id").NotNullable()
            .WithColumn("list_type").AsUInt32("list_type").NotNullable()
            .WithColumn("list_number").AsUInt16("list_number").NotNullable()
            .WithColumn("data_center_id").AsUInt32("data_center_id").NotNullable()
            .WithColumn("page_count").AsUInt16("page_count").NotNullable()
            .WithColumn("add_players").AsBoolean().NotNullable()
            .WithColumn("sync_with_category").AsBoolean().NotNullable()
            .WithColumn("default_category_id").AsInt32().NotNullable()
            .WithColumn("page_last_updated").AsString().NotNullable();

        Create.Index("idx_social_lists_content_id").OnTable("social_lists").OnColumn("content_id").Ascending();
        Create.Index("idx_social_lists_list_type").OnTable("social_lists").OnColumn("list_type").Ascending();
        Create.Index("idx_social_lists_list_number").OnTable("social_lists").OnColumn("list_number").Ascending();
        Create.Index("idx_social_lists_data_center_id").OnTable("social_lists").OnColumn("data_center_id").Ascending();
    }

    private void CreateSocialListMembersTable()
    {
        Create.Table("social_list_members")
            .WithIdColumn()
            .WithTimeStampColumns()
            .WithColumn("content_id").AsUInt64("content_id").NotNullable()
            .WithColumn("key").AsString().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("world_id").AsUInt32("world_id").NotNullable()
            .WithColumn("page_number").AsUInt16("page_number").NotNullable()
            .WithColumn("social_list_id").AsInt32().NotNullable().ForeignKey("fk_social_list_members_social_lists", "social_lists", "id");

        Create.Index("idx_social_list_members_content_id").OnTable("social_list_members").OnColumn("content_id").Ascending();
        Create.Index("idx_social_list_members_key").OnTable("social_list_members").OnColumn("key").Ascending();
    }

    private void UpdatePlayersTable()
    {
        Alter.Table("players")
            .AddColumn("content_id").AsUInt64("content_id").NotNullable().SetExistingRowsTo(0);

        Create.Index("idx_players_content_id").OnTable("players").OnColumn("content_id").Ascending();
    }

    private void UpdateCategoriesTable()
    {
        Alter.Table("categories")
            .AddColumn("social_list_id").AsInt32().NotNullable().WithDefaultValue(0).ForeignKey("fk_categories_social_lists", "social_lists", "id");
    }
}
