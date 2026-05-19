using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using AutoMapper;

using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class PlayerRepository : BaseRepository
{
    public PlayerRepository(IDbConnection connection, IMapper mapper) : base(connection, mapper) { }

    public IEnumerable<Player> GetAllPlayersWithRelations()
    {
        Plugin.PluginLog.Verbose("Entering PlayerRepository.GetAllPlayersWithRelations()");
        var players = new Dictionary<int, Player>();

        try
        {
            const string playerSql = "SELECT * FROM players";
            var playerDTOs = Connection.Query<PlayerDTO>(playerSql);
            foreach (var dto in playerDTOs)
            {
                var player = Mapper.Map<Player>(dto);
                player.AssignedTags = new List<Tag>();
                player.AssignedCategories = new List<Category>();
                player.PlayerConfig = new PlayerConfig(PlayerConfigType.Player)
                {
                    PlayerId = player.Id,
                };
                players.Add(player.Id, player);
            }

            var tagDict = Connection.Query<TagDTO>("SELECT * FROM tags")
                                         .ToDictionary<TagDTO, int, Tag>(t => t.id, t => Mapper.Map<Tag>(t));

            var categoryDict = Connection.Query<CategoryDTO>("SELECT * FROM categories")
                                              .ToDictionary<CategoryDTO, int, Category>(c => c.id, c => Mapper.Map<Category>(c));

            const string tagSql = "SELECT * FROM player_tags";
            var playerTagDTOs = Connection.Query<PlayerTagDTO>(tagSql);
            foreach (var dto in playerTagDTOs)
            {
                if (players.TryGetValue(dto.player_id, out var player))
                {
                    player.AssignedTags.Add(tagDict[dto.tag_id]);
                }
            }

            const string categoryConfigSql = "SELECT * FROM player_config WHERE category_id IS NOT NULL";
            var categoryConfigDTOs = Connection.Query<PlayerConfigDTO>(categoryConfigSql);
            var categoryConfigDict = categoryConfigDTOs
                .Where(dto => dto.category_id.HasValue)
                .ToDictionary(
                    dto => dto.category_id!.Value,
                    dto => Mapper.Map<PlayerConfig>(dto));

            const string categorySql = "SELECT * FROM player_categories";
            var playerCategoryDTOs = Connection.Query<PlayerCategoryDTO>(categorySql);
            foreach (var dto in playerCategoryDTOs)
            {
                if (players.TryGetValue(dto.player_id, out var player))
                {
                    var category = categoryDict[dto.category_id];
                    if (categoryConfigDict.TryGetValue(category.Id, out var config))
                    {
                        category.PlayerConfig = config;
                    }

                    player.AssignedCategories.Add(category);
                }
            }

            const string configSql = "SELECT * FROM player_config";
            var playerConfigDTOs = Connection.Query<PlayerConfigDTO>(configSql);
            foreach (var dto in playerConfigDTOs)
            {
                if (dto.player_id is null or 0)
                {
                    continue;
                }

                if (players.TryGetValue((int)dto.player_id, out var player))
                {
                    var config = Mapper.Map<PlayerConfig>(dto);
                    player.PlayerConfig = config;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to fetch all players with relations.");
            return Enumerable.Empty<Player>();
        }

        return players.Values;
    }

    public bool UpdatePlayer(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerRepository.UpdatePlayer(): {player.Id}");
        try
        {
            var playerDto = Mapper.Map<PlayerDTO>(player);
            SetUpdateTimestamp(playerDto);
            const string sql = @"
        UPDATE players
        SET
            key = @key,
            last_alert_sent = @last_alert_sent,
            last_name_change_alert_sent = @last_name_change_alert_sent,
            last_world_change_alert_sent = @last_world_change_alert_sent,
            first_seen = @first_seen,
            last_seen = @last_seen,
            customize = @customize,
            seen_count = @seen_count,
            lodestone_status = @lodestone_status,
            lodestone_verified_on = @lodestone_verified_on,
            free_company_state = @free_company_state,
            free_company_tag = @free_company_tag,
            name = @name,
            notes = @notes,
            lodestone_id = @lodestone_id,
            world_id = @world_id,
            last_territory_type = @last_territory_type,
            updated = @updated,
            content_id = @content_id,
            entity_id = @entity_id
        WHERE id = @id";
            Connection.Execute(sql, playerDto);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to update player with PlayerID {player.Id} ({player.Key}).");
            return false;
        }
    }

    public bool DeletePlayer(int playerId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerRepository.DeletePlayer(): {playerId}");
        try
        {
            const string sql = @"DELETE FROM players WHERE id = @player_id";
            Connection.Execute(sql, new { player_id = playerId });
            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to delete player with PlayerID {playerId}.");
            return false;
        }
    }

    public bool DeletePlayersWithRelations(List<int> playerIds)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerRepository.DeletePlayersWithRelations(): {string.Join(", ", playerIds)}");
        using var transaction = Connection.BeginTransaction();
        try
        {
            const string sqlFormat = "DELETE FROM {0} WHERE player_id IN @player_ids";

            Connection.Execute(string.Format(sqlFormat, "player_customize_histories"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "player_name_world_histories"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "player_categories"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "player_config"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "player_tags"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "lodestone_lookups"), new { player_ids = playerIds }, transaction);
            Connection.Execute(string.Format(sqlFormat, "player_encounters"), new { player_ids = playerIds }, transaction);

            const string deletePlayersSql = "DELETE FROM players WHERE id IN @player_ids";
            Connection.Execute(deletePlayersSql, new { player_ids = playerIds }, transaction);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Plugin.PluginLog.Error(ex, $"Failed to delete players and their relations for PlayerIDs {string.Join(", ", playerIds)}.");
            return false;
        }
    }

    public int CreatePlayer(Player player, ulong contentId, bool setTimestamps = true)
    {
        const string checkExistenceSql = "SELECT id FROM players WHERE key = @key AND content_id = @content_id";
        var existingId = Connection.ExecuteScalar<int?>(checkExistenceSql, new { key = player.Key, content_id = contentId });

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var playerDto = Mapper.Map<PlayerDTO>(player);
        if (setTimestamps)
        {
            SetCreateTimestamp(playerDto);
        }

        const string sql = @"
        INSERT INTO players (
            created,
            updated,
            last_alert_sent,
            last_name_change_alert_sent,
            last_world_change_alert_sent,
            first_seen,
            last_seen,
            customize,
            seen_count,
            lodestone_status,
            lodestone_verified_on,
            free_company_state,
            free_company_tag,
            key,
            name,
            notes,
            lodestone_id,
            world_id,
            last_territory_type,
            content_id,
            entity_id)
        VALUES (
            @created,
            @updated,
            @last_alert_sent,
            @last_name_change_alert_sent,
            @last_world_change_alert_sent,
            @first_seen,
            @last_seen,
            @customize,
            @seen_count,
            @lodestone_status,
            @lodestone_verified_on,
            @free_company_state,
            @free_company_tag,
            @key,
            @name,
            @notes,
            @lodestone_id,
            @world_id,
            @last_territory_type,
            @content_id,
            @entity_id)
        RETURNING id";

        var newId = Connection.ExecuteScalar<int>(sql, playerDto);
        return newId;
    }

    public Tuple<int, string> CreateExistingPlayer(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerRepository.CreatePlayer(): {player.Key}");
        using var transaction = Connection.BeginTransaction();
        try
        {
            const string checkExistenceSql = "SELECT id FROM players WHERE key = @key";
            var existingId = Connection.ExecuteScalar<int?>(checkExistenceSql, new { key = player.Key }, transaction);

            if (existingId.HasValue)
            {
                Plugin.PluginLog.Verbose($"CreatePlayer(): Player with Key {player.Key} already exists.");
                return new Tuple<int, string>(existingId.Value, string.Empty);
            }

            var playerDto = Mapper.Map<PlayerDTO>(player);

            const string sql = @"
            INSERT INTO players (
                id,
                created,
                updated,
                last_alert_sent,
                last_name_change_alert_sent,
                last_world_change_alert_sent,
                first_seen,
                last_seen,
                customize,
                seen_count,
                lodestone_status,
                lodestone_verified_on,
                free_company_state,
                free_company_tag,
                key,
                name,
                notes,
                lodestone_id,
                world_id,
                last_territory_type,
                content_id,
                entity_id)
            VALUES (
                @id,
                @created,
                @updated,
                @last_alert_sent,
                @last_name_change_alert_sent,
                @last_world_change_alert_sent,
                @first_seen,
                @last_seen,
                @customize,
                @seen_count,
                @lodestone_status,
                @lodestone_verified_on,
                @free_company_state,
                @free_company_tag,
                @key,
                @name,
                @notes,
                @lodestone_id,
                @world_id,
                @last_territory_type,
                @content_id,
                @entity_id)";

            Connection.Execute(sql, playerDto, transaction);

            var newId = Connection.ExecuteScalar<int>("SELECT last_insert_rowid()", transaction: transaction);

            transaction.Commit();
            return new Tuple<int, string>(newId, string.Empty);
        }
        catch (SQLiteException sqliteEx)
        {
            var playerDataJson = Newtonsoft.Json.JsonConvert.SerializeObject(player);
            var errorMsg = $"SQLite Error: {sqliteEx.ErrorCode} - {sqliteEx.Message} - {sqliteEx.InnerException} - {sqliteEx.StackTrace} - {playerDataJson}";
            Plugin.PluginLog.Error(errorMsg);
            transaction.Rollback();
            return new Tuple<int, string>(0, errorMsg);
        }
        catch (Exception ex)
        {
            var playerDataJson = Newtonsoft.Json.JsonConvert.SerializeObject(player);
            var errorMsg = $"General Error: {ex.Message} - {ex.InnerException} - {ex.StackTrace} - {playerDataJson}";
            Plugin.PluginLog.Error(ex, $"Failed to create new player with Key {player.Key}.", errorMsg);
            transaction.Rollback();
            return new Tuple<int, string>(0, errorMsg);
        }
    }
}
