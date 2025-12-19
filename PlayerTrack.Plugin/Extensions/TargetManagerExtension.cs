using System;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PlayerTrack.Extensions;

/// <summary>
/// Target manager extension to provide additional functionality.
/// </summary>
public static class TargetManagerExtension
{
    /// <summary>
    /// Sets the target to the specified object ID. If the object ID is already targeted, it will clear the target.
    /// </summary>
    /// <param name="manager">this</param>
    /// <param name="objectId">The object ID to target</param>
    public static void SetTarget(this ITargetManager manager, uint objectId)
    {
        var obj = Plugin.ObjectCollection.SearchById(objectId);
        if (obj == null)
            return;

        if (manager.Target?.EntityId == obj.EntityId)
        {
            manager.Target = null;
            return;
        }

        manager.Target = obj;
    }

    /// <summary>
    /// Sets the focus target to the specified object ID. If the object ID is already focused, it will clear the focus
    /// target.
    /// </summary>
    /// <param name="manager">this</param>
    /// <param name="objectId">The object ID to set as focus target.</param>
    public static void SetFocusTarget(this ITargetManager manager, uint objectId)
    {
        var obj = Plugin.ObjectCollection.SearchById(objectId);
        if (obj == null)
            return;

        if (manager.FocusTarget?.EntityId == obj.EntityId)
        {
            manager.FocusTarget = null;
            return;
        }

        manager.FocusTarget = obj;
    }

    /// <summary>
    /// Opens the plate window for the specified object ID.
    /// </summary>
    /// <param name="manager">this</param>
    /// <param name="objectId">The object ID for which to open the plate window.</param>
    public static unsafe void OpenPlateWindow(this ITargetManager manager, uint objectId)
    {
        var obj = Plugin.ObjectCollection.FirstOrDefault(i => i.EntityId == objectId);
        if (obj == null)
            return;

        try
        {
            AgentCharaCard.Instance()->OpenCharaCard((GameObject*)obj.Address);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to open plate window.");
        }
    }

    /// <summary>
    /// Examines the specified object ID.
    /// </summary>
    /// <param name="manager">this</param>
    /// <param name="objectId">The object ID to examine.</param>
    public static unsafe void ExamineTarget(this ITargetManager manager, uint objectId)
    {
        var obj = Plugin.ObjectCollection.FirstOrDefault(i => i.EntityId == objectId);
        if (obj == null)
            return;

        try
        {
            AgentInspect.Instance()->ExamineCharacter(objectId);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "Failed to examine target.");
        }
    }
}
