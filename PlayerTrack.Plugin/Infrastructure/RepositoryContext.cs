using AutoMapper;
using Dalamud.Utility;
using Dapper;
using FluentDapperLite.Extension;
using FluentDapperLite.Maintenance;
using Microsoft.Extensions.Logging.Abstractions;
using PlayerTrack.Resource;
using System;
using System.Data;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PlayerTrack.Infrastructure;

public static class RepositoryContext
{
    public static BackupRepository BackupRepository { get; set; } = null!;
    public static CategoryRepository CategoryRepository { get; set; } = null!;
    public static EncounterRepository EncounterRepository { get; set; } = null!;
    public static PlayerRepository PlayerRepository { get; set; } = null!;
    public static ConfigRepository ConfigRepository { get; set; } = null!;
    public static TagRepository TagRepository { get; set; } = null!;
    public static PlayerEncounterRepository PlayerEncounterRepository { get; set; } = null!;
    public static PlayerNameWorldHistoryRepository PlayerNameWorldHistoryRepository { get; set; } = null!;
    public static PlayerCustomizeHistoryRepository PlayerCustomizeHistoryRepository { get; set; } = null!;
    public static PlayerCategoryRepository PlayerCategoryRepository { get; set; } = null!;
    public static PlayerTagRepository PlayerTagRepository { get; set; } = null!;
    public static PlayerConfigRepository PlayerConfigRepository { get; set; } = null!;
    public static ArchiveRecordRepository ArchiveRecordRepository { get; set; } = null!;
    public static LocalPlayerRepository LocalPlayerRepository { get; set; } = null!;
    public static SocialListRepository SocialListRepository { get; set; } = null!;
    public static SocialListMemberRepository SocialListMemberRepository { get; set; } = null!;
    private static IDbConnection Database { get; set; } = null!;
    private static IMapper Mapper { get; set; } = null!;

    public static void Initialize(string path)
    {
        Database = SQLiteDbConnectionBuilder.Build(path);
        Mapper = CreateMapper();
        BackupRepository = new BackupRepository(Database, Mapper);
        CategoryRepository = new CategoryRepository(Database, Mapper);
        EncounterRepository = new EncounterRepository(Database, Mapper);
        PlayerRepository = new PlayerRepository(Database, Mapper);
        ConfigRepository = new ConfigRepository(Database, Mapper);
        PlayerEncounterRepository = new PlayerEncounterRepository(Database, Mapper);
        TagRepository = new TagRepository(Database, Mapper);
        PlayerNameWorldHistoryRepository = new PlayerNameWorldHistoryRepository(Database, Mapper);
        PlayerCustomizeHistoryRepository = new PlayerCustomizeHistoryRepository(Database, Mapper);
        PlayerCategoryRepository = new PlayerCategoryRepository(Database, Mapper);
        PlayerTagRepository = new PlayerTagRepository(Database, Mapper);
        PlayerConfigRepository = new PlayerConfigRepository(Database, Mapper);
        ArchiveRecordRepository = new ArchiveRecordRepository(Database, Mapper);
        LocalPlayerRepository = new LocalPlayerRepository(Database, Mapper);
        SocialListRepository = new SocialListRepository(Database, Mapper);
        SocialListMemberRepository = new SocialListMemberRepository(Database, Mapper);
        RunWinePragmas();
        RunMaintenanceChecks();
    }

    public static void Dispose()
    {
        try
        {
            Database.Dispose();
        }
        catch (Exception)
        {
            Plugin.PluginLog.Warning("Failed to dispose RepositoryContext");
        }
    }

    public static void RunMaintenanceChecks(bool forceCheck = false)
    {
        Plugin.PluginLog.Verbose("Entering RepositoryContext.RunMaintenance()");
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var config = ConfigRepository.GetPluginConfig();

        if (config == null)
            return;

        const long weekInMillis = 604800000; // 7 days in milliseconds
        if (currentTime - config.MaintenanceLastRunOn >= weekInMillis || forceCheck)
        {
            try
            {
                Plugin.PluginLog.Verbose($"It's been a week since the last maintenance. Current time: {currentTime}, Last run: {config.MaintenanceLastRunOn}");
                SQLiteDbMaintenance.Reindex(Database);
                SQLiteDbMaintenance.Vacuum(Database);
                SQLiteDbMaintenance.Analyze(Database);
                SQLiteDbMaintenance.Optimize(Database);
                config.MaintenanceLastRunOn = currentTime;
                ConfigRepository.UpdatePluginConfig(config);
            }
            catch
            {
                // If the database fails to load, we will want to dispose of it if it is loaded,
                // we also do a null check just in case the Database object is null.
                // This is so we can delete/alter/repair the database file while the game is running.
                Database?.Dispose();
                throw;
            }
        }
        else
        {
            Plugin.PluginLog.Verbose("It hasn't been a week since the last maintenance, skipping");
        }

        Plugin.PluginLog.Verbose("Exiting RepositoryContext.RunMaintenance()");
    }

    public static string ExecuteSqlQuery(string sql)
    {
        Plugin.PluginLog.Debug($"Executing SQL query: {sql}.");
        try
        {
            if (!sql.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase) && !sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return Language.SQLExecutorRestriction;

            var results = Database.Query<dynamic>(sql).AsList();
            var jsonResults = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            return jsonResults;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to execute query: {sql}.", sql);
            return ex.Message;
        }
    }

    private static void RunWinePragmas()
    {
        if (!Util.IsWine())
            return;

        Plugin.PluginLog.Info("Wine detected, running Wine specific pragmas.");
        using var cmd = Database.CreateCommand();
        cmd.CommandText = "PRAGMA cache_size = 32768;";
        cmd.ExecuteNonQuery();
    }

    private static IMapper CreateMapper()
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BackupMappingProfile>();
            cfg.AddProfile<CategoryMappingProfile>();
            cfg.AddProfile<EncounterMappingProfile>();
            cfg.AddProfile<PlayerCustomizeHistoryMappingProfile>();
            cfg.AddProfile<PlayerEncounterMappingProfile>();
            cfg.AddProfile<PlayerMappingProfile>();
            cfg.AddProfile<PlayerNameWorldHistoryMappingProfile>();
            cfg.AddProfile<PlayerConfigMappingProfile>();
            cfg.AddProfile<PlayerTagMappingProfile>();
            cfg.AddProfile<PlayerCategoryMappingProfile>();
            cfg.AddProfile<TagMappingProfile>();
            cfg.AddProfile<ArchiveRecordMappingProfile>();
            cfg.AddProfile<LocalPlayerMappingProfile>();
            cfg.AddProfile<SocialListMappingProfile>();
            cfg.AddProfile<SocialListMemberMappingProfile>();
        });

        return mapperConfig.CreateMapper();
    }
}
