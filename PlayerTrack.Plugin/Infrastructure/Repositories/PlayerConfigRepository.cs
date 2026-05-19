using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AutoMapper;
using Newtonsoft.Json;
using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;

namespace PlayerTrack.Infrastructure;

public class PlayerConfigRepository : BaseRepository
{
    public PlayerConfigRepository(IDbConnection connection, IMapper mapper) : base(connection, mapper) { }

    public List<ConfigValue<char>> GetDistinctIcons()
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.GetDistinctIcons()");
        try
        {
            const string sql = "SELECT DISTINCT player_list_icon FROM player_config WHERE player_list_icon IS NOT NULL";
            var iconJsonStrings = Connection.Query<string>(sql).ToList();

            return iconJsonStrings.Select(JsonConvert.DeserializeObject<ConfigValue<char>>).ToList();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to fetch distinct icons from player_config.");
            return new List<ConfigValue<char>>();
        }
    }

    public int? GetIdByPlayerId(int playerId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.GetIdByPlayerId(): {playerId}");
        try
        {
            const string sql = "SELECT id FROM player_config WHERE player_id = @player_id";
            var id = Connection.QueryFirstOrDefault<int?>(sql, new { player_id = playerId });

            return id;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to get player config id by player id {playerId}.");
            return null;
        }
    }

    public int CreatePlayerConfig(PlayerConfig config)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.CreatePlayerConfig(): {config.PlayerConfigType}");
        try
        {
            var configDTO = Mapper.Map<PlayerConfigDTO>(config);
            SetCreateTimestamp(configDTO);

            const string insertSql = @"
                INSERT INTO player_config
                (
                    player_config_type,
                    player_list_name_color,
                    player_list_icon,
                    nameplate_custom_title,
                    nameplate_show_in_overworld,
                    nameplate_show_in_content,
                    nameplate_show_in_high_end_content,
                    nameplate_color,
                    nameplate_use_color,
                    nameplate_use_color_if_dead,
                    nameplate_title_type,
                    alert_name_change,
                    alert_world_transfer,
                    alert_proximity,
                    alert_name_change_frequency,
                    alert_world_transfer_frequency,
                    alert_proximity_frequency,
                    alert_format_include_category,
                    alert_format_include_custom_title,
                    visibility_type,
                    updated,
                    created,
                    player_id,
                    category_id
                )
                VALUES
                (
                    @player_config_type,
                    @player_list_name_color,
                    @player_list_icon,
                    @nameplate_custom_title,
                    @nameplate_show_in_overworld,
                    @nameplate_show_in_content,
                    @nameplate_show_in_high_end_content,
                    @nameplate_color,
                    @nameplate_use_color,
                    @nameplate_use_color_if_dead,
                    @nameplate_title_type,
                    @alert_name_change,
                    @alert_world_transfer,
                    @alert_proximity,
                    @alert_name_change_frequency,
                    @alert_world_transfer_frequency,
                    @alert_proximity_frequency,
                    @alert_format_include_category,
                    @alert_format_include_custom_title,
                    @visibility_type,
                    @updated,
                    @created,
                    @player_id,
                    @category_id
                )
                RETURNING id";

            var newId = Connection.ExecuteScalar<int>(insertSql, configDTO);

            return newId;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to create player config.", config);
            return 0;
        }
    }

    public void UpdatePlayerConfig(PlayerConfig config)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.UpdatePlayerConfig(): {config.PlayerConfigType}");
        try
        {
            var configDTO = Mapper.Map<PlayerConfigDTO>(config);
            SetUpdateTimestamp(configDTO);
            const string sql = @"
            UPDATE player_config
            SET
                player_config_type = @player_config_type,
                player_list_name_color = @player_list_name_color,
                player_list_icon = @player_list_icon,
                nameplate_custom_title = @nameplate_custom_title,
                nameplate_show_in_overworld = @nameplate_show_in_overworld,
                nameplate_show_in_content = @nameplate_show_in_content,
                nameplate_show_in_high_end_content = @nameplate_show_in_high_end_content,
                nameplate_color = @nameplate_color,
                nameplate_use_color = @nameplate_use_color,
                nameplate_use_color_if_dead = @nameplate_use_color_if_dead,
                nameplate_title_type = @nameplate_title_type,
                alert_name_change = @alert_name_change,
                alert_world_transfer = @alert_world_transfer,
                alert_proximity = @alert_proximity,
                alert_name_change_frequency = @alert_name_change_frequency,
                alert_world_transfer_frequency = @alert_world_transfer_frequency,
                alert_proximity_frequency = @alert_proximity_frequency,
                alert_format_include_category = @alert_format_include_category,
                alert_format_include_custom_title = @alert_format_include_custom_title,
                visibility_type = @visibility_type,
                updated = @updated,
                player_id = @player_id,
                category_id = @category_id
            WHERE id = @id";
            Connection.Execute(sql, configDTO);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to update player config.", config);
        }
    }

    public PlayerConfig? GetPlayerConfigByCategoryId(int categoryId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.GetPlayerConfigByCategoryId(): {categoryId}");
        try
        {
            const string sql = "SELECT * FROM player_config WHERE category_id = @category_id";
            var configDTO = Connection.QuerySingleOrDefault<PlayerConfigDTO>(sql, new { category_id = categoryId });

            return Mapper.Map<PlayerConfig>(configDTO);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to get player config by category id {categoryId}.");
            return null;
        }
    }

    public void DeletePlayerConfigByCategoryId(int categoryId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.DeletePlayerConfigByCategoryId(): {categoryId}");
        try
        {
            const string sql = "DELETE FROM player_config WHERE category_id = @category_id";
            Connection.Execute(sql, new { category_id = categoryId });
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to delete player config by category id {categoryId}.");
        }
    }

    public PlayerConfig? GetDefaultPlayerConfig()
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.GetDefaultPlayerConfig()");
        try
        {
            const string sql = "SELECT * FROM player_config WHERE player_id IS NULL AND category_id IS NULL;";
            var configDTO = Connection.QuerySingleOrDefault<PlayerConfigDTO>(sql);

            return Mapper.Map<PlayerConfig>(configDTO);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to get default player config.");
            return null;
        }
    }

    public void DeletePlayerConfigByPlayerId(int playerId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.DeletePlayerConfigByPlayerId(): {playerId}");
        try
        {
            const string sql = "DELETE FROM player_config WHERE player_id = @playerId";
            Connection.Execute(sql, new { playerId });
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to delete player config by player id {playerId}.");
        }
    }

    public void DeletePlayerConfigs(List<int> configIds)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.DeletePlayerConfigs(): {string.Join(", ", configIds)}");
        try
        {
            const string sql = "DELETE FROM player_config WHERE id IN @configIds";
            Connection.Execute(sql, new { configIds });
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"Failed to delete player configs for player config ids {string.Join(", ", configIds)}.");
        }
    }

    public bool CreatePlayerConfigs(List<PlayerConfig> playerConfigs)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigRepository.CreatePlayerConfigs(): {playerConfigs.Count}");
        var uniqueCategoryIds = new HashSet<int?>();
        foreach (var config in playerConfigs)
        {
            if (config.CategoryId.HasValue && !uniqueCategoryIds.Add(config.CategoryId.Value))
            {
                Plugin.PluginLog.Error($"Duplicate CategoryId: {config.CategoryId.Value}");
                return false;
            }
        }

        using var transaction = Connection.BeginTransaction();
        try
        {
            const string insertSql = @"
                INSERT INTO player_config
                (
                    player_config_type,
                    player_list_name_color,
                    player_list_icon,
                    nameplate_custom_title,
                    nameplate_show_in_overworld,
                    nameplate_show_in_content,
                    nameplate_show_in_high_end_content,
                    nameplate_color,
                    nameplate_use_color,
                    nameplate_use_color_if_dead,
                    nameplate_title_type,
                    alert_name_change,
                    alert_world_transfer,
                    alert_proximity,
                    alert_name_change_frequency,
                    alert_world_transfer_frequency,
                    alert_proximity_frequency,
                    alert_format_include_category,
                    alert_format_include_custom_title,
                    visibility_type,
                    updated,
                    created,
                    player_id,
                    category_id
                )
                VALUES
                (
                    @player_config_type,
                    @player_list_name_color,
                    @player_list_icon,
                    @nameplate_custom_title,
                    @nameplate_show_in_overworld,
                    @nameplate_show_in_content,
                    @nameplate_show_in_high_end_content,
                    @nameplate_color,
                    @nameplate_use_color,
                    @nameplate_use_color_if_dead,
                    @nameplate_title_type,
                    @alert_name_change,
                    @alert_world_transfer,
                    @alert_proximity,
                    @alert_name_change_frequency,
                    @alert_world_transfer_frequency,
                    @alert_proximity_frequency,
                    @alert_format_include_category,
                    @alert_format_include_custom_title,
                    @visibility_type,
                    @updated,
                    @created,
                    @player_id,
                    @category_id
                )";

            var playerConfigDTOs = playerConfigs.Select(config => Mapper.Map<PlayerConfigDTO>(config)).ToList();
            foreach (var configDTO in playerConfigDTOs)
            {
                SetCreateTimestamp(configDTO);
            }

            Connection.Execute(insertSql, playerConfigDTOs, transaction);
            transaction.Commit();

            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to create player configs.");
            transaction.Rollback();

            return false;
        }
    }
}
