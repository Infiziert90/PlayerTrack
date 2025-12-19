using Dalamud.Plugin.Services;
using PlayerTrack.Data;

namespace PlayerTrack.Extensions;

/// <summary>
/// Dalamud ClientStateHandler extensions.
/// </summary>
public static class ClientStateExtension
{
    /// <summary>
    /// Validate if actor is valid player character.
    /// </summary>
    /// <param name="value">actor.</param>
    /// <returns>Indicator if player character is valid.</returns>
    public static LocalPlayerData? GetLocalPlayer(this IClientState value)
    {
        if (!Plugin.PlayerState.IsLoaded || Plugin.ObjectTable.LocalPlayer == null)
            return null;

        var localPlayer = new LocalPlayerData
        {
            Name = Plugin.PlayerState.CharacterName,
            HomeWorld = Plugin.PlayerState.HomeWorld.RowId,
            ContentId = Plugin.PlayerState.ContentId,
            Customize = Plugin.ObjectTable.LocalPlayer.Customize,
        };

        return localPlayer.IsValid() ? localPlayer : null;
    }
}
