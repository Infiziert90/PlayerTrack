using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using PlayerTrack.Domain;

namespace PlayerTrack.Extensions;

/// <summary>
/// Dalamud IChatGui extensions.
/// </summary>
public static class ChatGuiExtensions
{
    /// <summary>
    /// Print message with plugin name to the configured chat channel
    /// (defaults to <see cref="XivChatType.Notice"/>).
    /// </summary>
    /// <param name="value">chat gui service.</param>
    /// <param name="payloads">list of payloads.</param>
    public static void PluginPrintNotice(this IChatGui value, IEnumerable<Payload> payloads)
    {
        var config = ServiceContext.ConfigService.GetConfig();
        var type = config.UseCustomChatChannel
            ? config.CustomChatChannel
            : XivChatType.Notice;

        value.Print(new XivChatEntry
        {
            Message = BuildSeString(Plugin.PluginInterface.InternalName, payloads),
            Type = type,
        });
    }

    private static SeString BuildSeString(string? pluginName, IEnumerable<Payload> payloads)
    {
        var builder = new SeStringBuilder();
        builder.AddUiForeground(548);
        builder.AddText($"[{pluginName}] ");
        builder.Append(payloads);
        builder.AddUiForegroundOff();

        return builder.BuiltString;
    }
}
