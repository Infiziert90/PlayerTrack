using System;
using System.Collections.Generic;
using System.Data;
using AutoMapper;
using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class PlayerBioRepository : BaseRepository
{
    public PlayerBioRepository(IDbConnection connection, IMapper mapper) : base(connection, mapper) { }

    /// <summary>Returns the most recently recorded bio for a player, or null if none exists.</summary>
    public PlayerBio? GetLatestByPlayerId(int playerId)
    {
        try
        {
            const string sql = "SELECT * FROM player_bios WHERE player_id = @playerId ORDER BY created DESC LIMIT 1";
            var dto = Connection.QueryFirstOrDefault<PlayerBioDTO>(sql, new { playerId });
            return dto == null ? null : Mapper.Map<PlayerBio>(dto);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"[PlayerBioRepository] Failed to get latest bio for player {playerId}.");
            return null;
        }
    }

    /// <summary>Returns all recorded bios for a player, newest first.</summary>
    public List<PlayerBio> GetAllByPlayerId(int playerId)
    {
        try
        {
            const string sql = "SELECT * FROM player_bios WHERE player_id = @playerId ORDER BY created DESC";
            var dtos = Connection.Query<PlayerBioDTO>(sql, new { playerId }).AsList();
            return Mapper.Map<List<PlayerBio>>(dtos);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"[PlayerBioRepository] Failed to get bio history for player {playerId}.");
            return [];
        }
    }

    /// <summary>Inserts a new bio entry and returns its new row ID.</summary>
    public int CreatePlayerBio(PlayerBio bio)
    {
        try
        {
            var dto = Mapper.Map<PlayerBioDTO>(bio);
            SetInsertTimestamps(dto);
            const string sql = @"
                INSERT INTO player_bios (player_id, bio, created, updated)
                VALUES (@player_id, @bio, @created, @updated)
                RETURNING id";
            return Connection.ExecuteScalar<int>(sql, dto);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, $"[PlayerBioRepository] Failed to create bio for player {bio.PlayerId}.");
            return 0;
        }
    }
}
