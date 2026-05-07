using System;
using System.Linq;
using Dalamud.Plugin.Ipc.Exceptions;

namespace PlayerTrack.API;

/// <summary>
/// Provides a safe, optional integration with the XIVInstantMessenger plugin.
///
/// XIVInstantMessenger (internal name "Messenger") exposes Dalamud IPC actions
/// via EzIPC under the "Messenger" namespace.  This wrapper handles all failure
/// modes (XIM not installed, IPC gate not yet registered) without surfacing
/// exceptions to the caller.
///
/// IPC contract (verified against XIVInstantMessenger source):
///   Key:        "Messenger.OpenMessenger"
///   Signature:  Action&lt;string, bool&gt;
///   Arg 1:      "Name@World"  (e.g. "Firstname Lastname@Balmung")
///   Arg 2:      setFocus -- pass true to bring the window to the foreground
/// </summary>
public static class XIVInstantMessengerProvider
{
    // ----------------------------------------------------------------
    // IPC identifiers (verified against XIVInstantMessenger source)
    // ----------------------------------------------------------------

    private const string IpcOpenMessenger = "Messenger.OpenMessenger";

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /// <summary>
    /// Returns true if XIVInstantMessenger is currently loaded.
    /// Uses the InstalledPlugins list for a cheap, non-IPC availability check.
    /// </summary>
    public static bool IsAvailable()
    {
        return Plugin.PluginInterface.InstalledPlugins
                     .Any(p => p.InternalName == "Messenger" && p.IsLoaded);
    }

    /// <summary>
    /// Opens the XIVInstantMessenger chat window for the specified player.
    /// If XIM is not installed or the IPC call fails for any reason, the
    /// error is logged and the method returns silently.
    /// </summary>
    /// <param name="playerName">
    /// The player's character name (e.g. "Firstname Lastname").
    /// </param>
    /// <param name="worldName">
    /// The player's home world name (e.g. "Balmung").
    /// </param>
    public static void TryOpenMessenger(string playerName, string worldName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(worldName))
            return;

        try
        {
            // XIM expects "Name@World" as a single string, plus a setFocus bool.
            var nameWithWorld = $"{playerName}@{worldName}";
            Plugin.PluginInterface
                  .GetIpcSubscriber<string, bool, object>(IpcOpenMessenger)
                  .InvokeAction(nameWithWorld, true);
        }
        catch (IpcNotReadyError)
        {
            Plugin.PluginLog.Debug("[XIM] XIVInstantMessenger IPC gate is not ready; skipping OpenMessenger.");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Warning(ex,
                $"[XIM] Failed to open messenger for \"{playerName}@{worldName}\".");
        }
    }
}
