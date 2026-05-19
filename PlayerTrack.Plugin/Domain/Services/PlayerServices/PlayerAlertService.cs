using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using PlayerTrack.Extensions;
using PlayerTrack.Models;
using PlayerTrack.Resource;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlayerTrack.Domain;

public class PlayerAlertService
{
    private static readonly Regex ProximityRegex = new(@"^(?<playerName>[A-Z][a-zA-Z'-]*\s[A-Z][a-zA-Z'-]*)(?<worldName>[A-Z][a-zA-Z]*)\s.*", RegexOptions.Compiled);
    private static readonly Regex NameWorldChangeRegex = new(@".*》\s*(?<playerName>[A-Z][a-zA-Z'-]*\s[A-Z][a-zA-Z'-]*)(?<worldName>[A-Z][a-zA-Z]*)$", RegexOptions.Compiled);

    private readonly DalamudLinkPayload OpenPlayerTrackChatLinkHandler = Plugin.ChatGuiHandler.AddChatLinkHandler(10001, OnChatLinkClick);

    public void SendPlayerNameWorldChangeAlert(
        Player player,
        string previousPlayerName, uint previousWorldId,
        string newPlayerName, uint newWorldId) =>
        Task.Run(() =>
    {
        Plugin.PluginLog.Verbose($"Entering PlayerAlertService.SendPlayerNameWorldChangeAlert(): {previousPlayerName}, {previousWorldId}");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nameAlertDesired = previousPlayerName != newPlayerName && IsNameChangeAlertEnabled(player);
        var worldAlertDesired = previousWorldId != newWorldId && IsWorldTransferAlertEnabled(player);

        var nameCooldown = PlayerConfigService.GetNameChangeAlertFrequency(player);
        var worldCooldown = PlayerConfigService.GetWorldTransferAlertFrequency(player);

        var shouldSendNameAlert = nameAlertDesired && (nameCooldown <= 0 || now - player.LastNameChangeAlertSent > nameCooldown);
        var shouldSendWorldAlert = worldAlertDesired && (worldCooldown <= 0 || now - player.LastWorldChangeAlertSent > worldCooldown);

        if (!shouldSendNameAlert && !shouldSendWorldAlert)
            return;

        if (shouldSendNameAlert)
            player.LastNameChangeAlertSent = now;
        if (shouldSendWorldAlert)
            player.LastWorldChangeAlertSent = now;
        UpdatePlayerNameWorldAlertTimestamps(player.Id, player.LastNameChangeAlertSent, player.LastWorldChangeAlertSent);

        Plugin.ChatGuiHandler.PluginPrintNotice([
            OpenPlayerTrackChatLinkHandler,
            new TextPayload(previousPlayerName),
            new IconPayload(BitmapFontIcon.CrossWorld),
            new TextPayload(Sheets.GetWorldNameById(previousWorldId)),
            new TextPayload(" 》 "),
            new TextPayload(newPlayerName),
            new IconPayload(BitmapFontIcon.CrossWorld),
            new TextPayload(Sheets.GetWorldNameById(newWorldId)),
            RawPayload.LinkTerminator
        ]);
    });

    public void SendProximityAlert(Player player) => Task.Run(() =>
    {
        var cooldown = PlayerConfigService.GetProximityAlertFrequency(player);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!IsProximityAlertEnabled(player) ||
            (cooldown > 0 && now - player.LastAlertSent <= cooldown) ||
            (cooldown > 0 && now - player.Created <= cooldown))
            return;

        player.LastAlertSent = now;
        UpdatePlayerAlert(player.Id, player.LastAlertSent);

        Plugin.ChatGuiHandler.PluginPrintNotice([
            OpenPlayerTrackChatLinkHandler,
            new TextPayload(player.Name),
            new IconPayload(BitmapFontIcon.CrossWorld),
            new TextPayload(Sheets.GetWorldNameById(player.WorldId)),
            new TextPayload($" {Language.ProximityAlertMessage}"),
            RawPayload.LinkTerminator
        ]);
    });

    public void Dispose()
    {
        Plugin.ChatGuiHandler.RemoveChatLinkHandler();
    }

    private static void OnChatLinkClick(uint id, SeString message)
    {
        Plugin.PluginLog.Verbose($"Entering ChatHandler.OnChatLinkClick(): {id}, {message}");
        var match = NameWorldChangeRegex.Match(message.TextValue);
        if (!match.Success)
            match = ProximityRegex.Match(message.TextValue);

        if (match.Success)
        {
            var name = match.Groups["playerName"].Value;
            var worldName = match.Groups["worldName"].Value;
            ServiceContext.PlayerProcessService.SelectPlayer(name, worldName);
        }
        else
        {
            Plugin.PluginLog.Verbose("Failed to parse chat link.");
        }
    }

    private static bool IsProximityAlertEnabled(Player player) => PlayerConfigService.GetIsProximityAlertEnabled(player);

    private static bool IsWorldTransferAlertEnabled(Player player) => PlayerConfigService.GetIsWorldTransferAlertEnabled(player);

    private static bool IsNameChangeAlertEnabled(Player player) => PlayerConfigService.GetIsNameChangeAlertEnabled(player);

    private static void UpdatePlayerAlert(int playerId, long playerLastAlertSent)
    {
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
            return;

        player.LastAlertSent = playerLastAlertSent;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    private static void UpdatePlayerNameWorldAlertTimestamps(int playerId, long lastNameChangeAlertSent, long lastWorldChangeAlertSent)
    {
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
            return;

        player.LastNameChangeAlertSent = lastNameChangeAlertSent;
        player.LastWorldChangeAlertSent = lastWorldChangeAlertSent;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }
}
