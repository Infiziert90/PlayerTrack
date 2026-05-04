using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Utility;
using PlayerTrack.Data;

namespace PlayerTrack.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IMenuItemClickedArgs"/>.
/// </summary>
public static class MenuItemClickedArgsExtension
{
    /// <summary>
    /// Gets the player from the menu item clicked arguments.
    /// </summary>
    /// <param name="menuOpenedArgs">The menu item clicked arguments.</param>
    /// <returns>The player or <see langword="null" /> if the player is invalid.</returns>
    public static PlayerData? GetPlayer(this IMenuItemClickedArgs menuOpenedArgs)
    {
        if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            Plugin.PluginLog.Warning("ContextMenu: Invalid target");
            return null;
        }

        var playerName = menuTargetDefault.TargetName;
        var worldId = menuTargetDefault.TargetHomeWorld.RowId;
        var contentId = menuTargetDefault.TargetContentId;
        var objectId = menuTargetDefault.TargetObjectId;

        // Name and world are the minimum required to identify a player.
        // contentId / objectId are 0 / 0xE0000000 for players who are not
        // currently in the local zone (e.g. right-click on a chat name).
        // The OpenPlayerTrack handler already handles the isCurrent=false
        // path when contentId is absent, so we do not require them here.
        if (playerName.IsValidCharacterName() && Sheets.IsValidWorld(worldId))
        {
            return new PlayerData
            {
                Name = playerName,
                HomeWorld = worldId,
                ContentId = contentId,
                GameObjectId = objectId,
                CompanyTag = string.Empty,
            };
        }

        Plugin.PluginLog.Warning($"ContextMenu: Invalid player {playerName} {worldId} {contentId} {objectId}");
        return null;
    }
}
