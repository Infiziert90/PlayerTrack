using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using PlayerTrack.Data;

namespace PlayerTrack.Extensions;

/// <summary>
/// PlayerCharacter extensions.
/// </summary>
public static class PlayerCharacterExtension
{
    /// <summary>
    /// Convert IPlayerCharacter to PlayerData.
    /// </summary>
    /// <param name="character">IPlayerCharacter.</param>
    /// <returns>PlayerData.</returns>
    public static PlayerData ToPlayerData(this IPlayerCharacter character) =>
        new ()
        {
            GameObjectId = character.GameObjectId,
            EntityId = character.EntityId,
            ContentId = character.GetContentId(),
            Name = character.Name.TextValue,
            HomeWorld = character.HomeWorld.RowId,
            ClassJob = character.ClassJob.RowId,
            Level = character.Level,
            Customize = character.Customize.ToArray(),
            CompanyTag = character.CompanyTag.TextValue,
            IsLocalPlayer = false,
            IsDead = character.IsDead,
        };

    /// <summary>
    /// Get content id.
    /// </summary>
    /// <param name="character">IPlayerCharacter.</param>
    /// <returns>content id.</returns>
    public static unsafe ulong GetContentId(this IPlayerCharacter character) =>
        ((Character*)character.Address)->ContentId;
}
