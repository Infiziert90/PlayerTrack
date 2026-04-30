using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using PlayerTrack.Data;

namespace PlayerTrack.Extensions;

/// <summary>
/// ObjectTable extensions.
/// </summary>
public static class ObjectTableExtension
{
    /// <summary>
    /// Retrieve all players.
    /// </summary>
    /// <param name="objectTable">Dalamud ObjectTable.</param>
    /// <returns>all players.</returns>
    public static IEnumerable<PlayerData> GetPlayers(this IObjectTable objectTable) =>
        objectTable.PlayerObjects
                   .OfType<IPlayerCharacter>()
                   .Select(pc => pc.ToPlayerData())
                   .Where(tp => tp.IsValid())
                   .ToList();

    /// <summary>
    /// Retrieve player by content id.
    /// </summary>
    /// <param name="objectTable">Dalamud ObjectTable.</param>
    /// <param name="contentId">content id.</param>
    /// <returns>player if exists.</returns>
    public static PlayerData? GetPlayerByContentId(this IObjectTable objectTable, ulong contentId) =>
        objectTable.OfType<IPlayerCharacter>().FirstOrDefault(playerCharacter => playerCharacter.GetContentId() == contentId)?.ToPlayerData();
}
