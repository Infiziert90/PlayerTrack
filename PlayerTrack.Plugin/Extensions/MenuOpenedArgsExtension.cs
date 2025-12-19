using Dalamud.Game.Gui.ContextMenu;

namespace PlayerTrack.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IMenuOpenedArgs"/>.
/// </summary>
public static class MenuOpenedArgsExtensions
{
    /// <summary>
    /// Determines whether the context menu is a valid player menu.
    /// </summary>
    /// <param name="menuOpenedArgs">The menu opened arguments.</param>
    /// <param name="includeSelf">if set to <see langword="true" />, include the local player in the check.</param>
    /// <returns><see langword="true" /> if the context menu is a valid player menu; otherwise, <see langword="false" />.</returns>
    public static bool IsValidPlayerMenu(this IMenuOpenedArgs menuOpenedArgs, bool includeSelf = false)
    {
        if (!Plugin.PluginInterface.UiBuilder.ShouldModifyUi || menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
            return false;

        switch (menuOpenedArgs.AddonName)
        {
            case null:
            case "LookingForGroup":
            case "PartyMemberList":
            case "FriendList":
            case "FreeCompany":
            case "SocialList":
            case "ContactList":
            case "ChatLog":
            case "_PartyList":
            case "LinkShell":
            case "CrossWorldLinkshell":
            case "ContentMemberList":
            case "BeginnerChatList":
                if (menuTargetDefault.TargetName != string.Empty && Sheets.IsValidWorld(menuTargetDefault.TargetHomeWorld.RowId))
                {
                    if (!includeSelf && Plugin.PlayerState.IsLoaded)
                    {
                        var name = Plugin.PlayerState.CharacterName;
                        var worldId = Plugin.PlayerState.HomeWorld.RowId;
                        if (menuTargetDefault.TargetName == name && menuTargetDefault.TargetHomeWorld.RowId == worldId)
                        {
                            Plugin.PluginLog.Verbose("ContextMenu: Self context menu.");
                            return false;
                        }
                    }

                    return true;
                }

                return false;
        }

        return false;
    }
}
