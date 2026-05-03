using System;

using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

// ReSharper disable InconsistentNaming
namespace PlayerTrack.API;

/// <summary>
/// IPC for PlayerTrack plugin.
/// </summary>
public class PlayerTrackProvider
{
    /// <summary>
    /// API Version.
    /// </summary>
    public const string LabelProviderApiVersion = "PlayerTrack.APIVersion";

    /// <summary>
    /// GetPlayerCurrentNameWorld.
    /// </summary>
    public const string LabelProviderGetPlayerCurrentNameWorld = "PlayerTrack.GetPlayerCurrentNameWorld";

    /// <summary>
    /// GetPlayerNotes.
    /// </summary>
    public const string LabelProviderGetPlayerNotes = "PlayerTrack.GetPlayerNotes";

    /// <summary>
    /// GetAllPlayerNameWorldHistories.
    /// </summary>
    public const string LabelProviderGetAllPlayerNameWorldHistories = "PlayerTrack.GetAllPlayerNameWorldHistories";

    /// <summary>
    /// AssignCategory.
    /// </summary>
    public const string LabelProviderAssignCategory = "PlayerTrack.AssignCategory";

    /// <summary>
    /// API.
    /// </summary>
    public readonly IPlayerTrackAPI API;

    /// <summary>
    /// ProviderAPIVersion.
    /// </summary>
    public readonly ICallGateProvider<int>? ProviderAPIVersion;

    /// <summary>
    /// GetPlayerCurrentNameWorld.
    /// </summary>
    public readonly ICallGateProvider<string, uint, string>? ProviderGetPlayerCurrentNameWorld;

    /// <summary>
    /// GetPlayerNotes.
    /// </summary>
    public readonly ICallGateProvider<string, uint, string>? ProviderGetPlayerNotes;

    /// <summary>
    /// ProviderGetAllPlayerNameWorldHistories.
    /// </summary>
    public readonly ICallGateProvider<((string, uint), (string, uint)[])[]>? ProviderGetAllPlayerNameWorldHistories;

    /// <summary>
    /// ProviderAssignCategory.
    /// </summary>
    public readonly ICallGateProvider<string, uint, uint, bool>? ProviderAssignCategory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTrackProvider"/> class.
    /// </summary>
    /// <param name="pluginInterface">plugin interface.</param>
    /// <param name="api">plugin api.</param>
    public PlayerTrackProvider(IDalamudPluginInterface pluginInterface, IPlayerTrackAPI api)
    {
        Plugin.PluginLog.Verbose("Entering PlayerTrackProvider");
        API = api;

        try
        {
            ProviderAPIVersion = pluginInterface.GetIpcProvider<int>(LabelProviderApiVersion);
            ProviderAPIVersion.RegisterFunc(() => api.APIVersion);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Error registering IPC provider for {LabelProviderApiVersion}:\n{ex}");
        }

        try
        {
            ProviderGetPlayerCurrentNameWorld = pluginInterface.GetIpcProvider<string, uint, string>(LabelProviderGetPlayerCurrentNameWorld);
            ProviderGetPlayerCurrentNameWorld.RegisterFunc(api.GetPlayerCurrentNameWorld);
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error($"Error registering IPC provider for {LabelProviderGetPlayerCurrentNameWorld}:\n{e}");
        }

        try
        {
            ProviderGetPlayerNotes = pluginInterface.GetIpcProvider<string, uint, string>(LabelProviderGetPlayerNotes);
            ProviderGetPlayerNotes.RegisterFunc(api.GetPlayerNotes);
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error($"Error registering IPC provider for {LabelProviderGetPlayerNotes}:\n{e}");
        }

        try
        {
            ProviderGetAllPlayerNameWorldHistories = pluginInterface.GetIpcProvider<((string, uint), (string, uint)[])[]>(LabelProviderGetAllPlayerNameWorldHistories);
            ProviderGetAllPlayerNameWorldHistories.RegisterFunc(api.GetAllPlayerNameWorldHistories);
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error($"Error registering IPC provider for {LabelProviderGetAllPlayerNameWorldHistories}:\n{e}");
        }

        try
        {
            ProviderAssignCategory = pluginInterface.GetIpcProvider<string, uint, uint, bool>(LabelProviderAssignCategory);
            ProviderAssignCategory.RegisterFunc(api.AssignCategory);
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error($"Error registering IPC provider for {LabelProviderAssignCategory}:\n{e}");
        }
    }

    /// <summary>
    /// Dispose IPC.
    /// </summary>
    public void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering PlayerTrackProvider.Dispose");
        ProviderAPIVersion?.UnregisterFunc();
        ProviderGetPlayerCurrentNameWorld?.UnregisterFunc();
        ProviderGetPlayerNotes?.UnregisterFunc();
        ProviderGetAllPlayerNameWorldHistories?.UnregisterFunc();
        ProviderAssignCategory?.UnregisterFunc();
    }
}
