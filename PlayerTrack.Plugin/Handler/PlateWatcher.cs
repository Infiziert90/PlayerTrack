using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PlayerTrack.Domain;

namespace PlayerTrack.Handler;

/// <summary>
/// Watches the CharaCard addon (Adventurer Plate) and assigns PlayerTrack
/// categories based on keyword rules matched against the plate bio/comment text.
///
/// CharaCard in Dawntrail (7.x) is component-based and populated asynchronously
/// from a server response.  PostSetup/PostRefresh fire before network data arrives,
/// so a "pending" flag is armed at that point and PostUpdate polls until the name
/// node becomes non-empty.
///
/// Node IDs identified via diagnostic dump on 2026-05-03:
///   Name:  component 5, text node 5
///   World: component 6, text node 3  ("WorldName [DatacenterName]")
///   Bio:   component 12, text node 3
/// </summary>
public static class PlateWatcher
{
    // ----------------------------------------------------------------
    // Addon / node constants
    // ----------------------------------------------------------------

    private const string AddonName = "CharaCard";

    private const uint NodeIdNameComponent  = 5;
    private const uint NodeIdNameText       = 5;
    private const uint NodeIdWorldComponent = 6;
    private const uint NodeIdWorldText      = 3;
    private const uint NodeIdBioComponent   = 12;
    private const uint NodeIdBioText        = 3;

    // ----------------------------------------------------------------
    // Per-plate state
    // ----------------------------------------------------------------

    private static bool   _pendingProcessing;
    private static string _lastProcessedRawName  = string.Empty;
    private static bool   _diagnosticDumpDone;

    // ----------------------------------------------------------------
    // Events
    // ----------------------------------------------------------------

    /// <summary>
    /// Raised at the end of the main processing path in OnCharaCardUpdate
    /// (i.e. after a newly-seen plate has been read, regardless of whether a
    /// bio or rule match was found).  BioScraper subscribes to this event so
    /// it knows when to hide the CharaCard window it auto-opened.
    /// </summary>
    public static event Action? OnPlateProcessed;

    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    public static void Start()
    {
        Plugin.PluginLog.Verbose("Entering PlateWatcher.Start()");
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup,   AddonName, OnCharaCardOpen);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnCharaCardOpen);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate,  AddonName, OnCharaCardUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnCharaCardClose);
        Plugin.ChatGuiHandler.ChatMessage += OnSearchCommentChatMessage;
        Plugin.PluginLog.Information("[PlateWatcher] Registered for addon 'CharaCard' and IChatGui.ChatMessage.");
    }

    public static void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering PlateWatcher.Dispose()");
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup,   AddonName, OnCharaCardOpen);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, OnCharaCardOpen);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate,  AddonName, OnCharaCardUpdate);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, OnCharaCardClose);
        Plugin.ChatGuiHandler.ChatMessage -= OnSearchCommentChatMessage;
    }

    // ----------------------------------------------------------------
    // SimpleTweaks "Print Search Comment" chat integration
    // ----------------------------------------------------------------

    /// <summary>
    /// Listens for the "Search Info from &lt;Player&gt;" chat output produced by the
    /// SimpleTweaks "Print Search Comment" tweak and runs categorizer rules
    /// against the bio text it contains.
    ///
    /// Expected SeString payload layout:
    ///   TextPayload   -- contains "Search Info from" somewhere in its text
    ///   PlayerPayload -- the linked player (carries PlayerName and World)
    ///   TextPayload(s) -- the plate bio content
    ///
    /// With debug logging enabled every payload is written to /xllog so the
    /// exact layout can be verified against live output.
    /// </summary>
    private static void OnSearchCommentChatMessage(IHandleableChatMessage chatMessage)
    {
        try
        {
            var config = ServiceContext.ConfigService.GetConfig();

            // Diagnostic: dump every payload so the real layout can be confirmed.
            if (config.CategorizerDebugLogging)
            {
                Plugin.PluginLog.Debug($"[PlateWatcher/Chat] type={chatMessage.LogKind} payload count={chatMessage.Message.Payloads.Count}");
                for (var pi = 0; pi < chatMessage.Message.Payloads.Count; pi++)
                    Plugin.PluginLog.Debug(
                        $"  [{pi}] {chatMessage.Message.Payloads[pi].GetType().Name}: {chatMessage.Message.Payloads[pi]}");
            }

            // Phase 1: locate the "Search Info from" text payload.
            bool   foundPrefix  = false;
            bool   afterPlayer  = false;
            string playerName   = string.Empty;
            uint   worldId      = 0;
            var    bioParts     = new StringBuilder();

            foreach (var payload in chatMessage.Message.Payloads)
            {
                if (!foundPrefix)
                {
                    if (payload is TextPayload tp &&
                        tp.Text != null &&
                        tp.Text.Contains("Search Info from", StringComparison.OrdinalIgnoreCase))
                    {
                        foundPrefix = true;
                    }
                    continue;
                }

                // Phase 2: capture the PlayerPayload that follows the prefix.
                if (!afterPlayer)
                {
                    if (payload is PlayerPayload pp)
                    {
                        playerName  = pp.PlayerName;
                        worldId     = pp.World.RowId;
                        afterPlayer = true;
                    }
                    continue;
                }

                // Phase 3: accumulate all text payloads after the player link as bio.
                if (payload is TextPayload bioTp && bioTp.Text != null)
                    bioParts.Append(bioTp.Text);
            }

            if (!foundPrefix || !afterPlayer) return;

            string bio = bioParts.ToString().Trim();

            // SimpleTweaks prepends "PlayerName>" to its chat output.
            // Strip that prefix so the stored bio matches the raw plate content.
            var namePrefix = playerName + ">";
            if (bio.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                bio = bio[namePrefix.Length..].Trim();

            Plugin.PluginLog.Debug(
                $"[PlateWatcher/Chat] Detected: player=\"{playerName}\" " +
                $"worldId={worldId} bio=\"{bio}\"");

            if (string.IsNullOrWhiteSpace(bio))
            {
                Plugin.PluginLog.Debug(
                    $"[PlateWatcher/Chat] Bio is empty for {playerName}; no rules evaluated.");
                return;
            }

            // Persist bio snapshot regardless of whether a categorizer rule matches.
            var player = ServiceContext.PlayerDataService.GetPlayer(playerName, worldId);
            if (player != null)
                PlayerBioService.UpdateBioIfChanged(player.Id, bio);

            if (config.CategorizerRules.Count == 0)
            {
                Plugin.PluginLog.Debug("[PlateWatcher/Chat] No categorizer rules configured.");
                return;
            }

            bool anyEnabled = false;
            foreach (var rule in config.CategorizerRules)
            {
                if (!rule.Enabled || string.IsNullOrEmpty(rule.Keyword)) continue;
                anyEnabled = true;

                if (!Matches(bio, rule)) continue;

                Plugin.PluginLog.Information(
                    $"[PlateWatcher/Chat] Rule matched: keyword=\"{rule.Keyword}\" " +
                    $"categoryId={rule.CategoryId} player=\"{playerName}\"@worldId={worldId}");

                if (player == null)
                {
                    Plugin.PluginLog.Warning(
                        $"[PlateWatcher/Chat] Player \"{playerName}\"@worldId={worldId} " +
                        "not found in PlayerTrack cache; skipping.");
                    return;
                }

                PlayerCategoryService.AssignCategoryToPlayerSync(player.Id, (int)rule.CategoryId);
                return;
            }

            if (config.CategorizerDebugLogging)
            {
                if (!anyEnabled)
                    Plugin.PluginLog.Debug("[PlateWatcher/Chat] All categorizer rules are disabled.");
                else
                    Plugin.PluginLog.Debug(
                        $"[PlateWatcher/Chat] No rules matched bio for \"{playerName}\".");
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[PlateWatcher/Chat] Unhandled exception in OnSearchCommentChatMessage.");
        }
    }

    // ----------------------------------------------------------------
    // Arm / disarm
    // ----------------------------------------------------------------

    private static void OnCharaCardOpen(AddonEvent eventType, AddonArgs args)
    {
        _pendingProcessing = true;
        Plugin.PluginLog.Debug($"[PlateWatcher] {eventType} -- plate armed for processing.");
    }

    private static void OnCharaCardClose(AddonEvent eventType, AddonArgs args)
    {
        var wasProcessing     = _pendingProcessing;
        _pendingProcessing    = false;
        _lastProcessedRawName = string.Empty;

        // If a plate was pending when the addon closed (e.g. the player left the
        // zone before the server response arrived), fire OnPlateProcessed so that
        // BioScraper can release _isProcessing and continue with the queue.
        if (wasProcessing)
        {
            Plugin.PluginLog.Debug("[PlateWatcher] CharaCard closed while pending -- firing OnPlateProcessed.");
            OnPlateProcessed?.Invoke();
        }
    }

    // ----------------------------------------------------------------
    // PostUpdate: poll until data is loaded, then process
    // ----------------------------------------------------------------

    private static unsafe void OnCharaCardUpdate(AddonEvent eventType, AddonArgs args)
    {
        if (!_pendingProcessing) return;

        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null) return;

            bool idsConfigured = NodeIdNameComponent  != 0 && NodeIdNameText  != 0
                              && NodeIdWorldComponent != 0 && NodeIdWorldText  != 0
                              && NodeIdBioComponent   != 0 && NodeIdBioText    != 0;

            if (!idsConfigured)
            {
                // Diagnostic mode: dump all non-empty text nodes once data has loaded.
                if (_diagnosticDumpDone) { _pendingProcessing = false; return; }

                var nonEmpty = CollectNonEmptyTextNodes(addon);
                if (nonEmpty.Count == 0) return; // still loading

                _diagnosticDumpDone = true;
                _pendingProcessing  = false;
                Plugin.PluginLog.Warning("[PlateWatcher] === DIAGNOSTIC: non-empty text nodes ===");
                foreach (var (compId, textId, content) in nonEmpty)
                    Plugin.PluginLog.Warning($"  [COMP id={compId}] -> [TEXT id={textId}] \"{content}\"");
                return;
            }

            // Wait until the name node is populated.
            string playerName = ReadTextNodeInComponent(addon, NodeIdNameComponent, NodeIdNameText);
            if (string.IsNullOrWhiteSpace(playerName)) return;

            // Avoid double-processing the same plate (PostRefresh fires multiple times).
            if (playerName == _lastProcessedRawName) { _pendingProcessing = false; return; }
            _lastProcessedRawName = playerName;
            _pendingProcessing    = false;

            // Everything from here to the end of the outer try runs inside its own
            // try/finally so that OnPlateProcessed is guaranteed to fire exactly once
            // for every newly-processed plate, regardless of which branch exits first.
            try
            {
            // World node is "WorldName [DatacenterName]" -- strip the datacenter suffix.
            string rawWorld  = ReadTextNodeInComponent(addon, NodeIdWorldComponent, NodeIdWorldText);
            string worldName = StripDatacenter(rawWorld);
            uint   worldId   = ResolveWorldId(worldName);

            var config = ServiceContext.ConfigService.GetConfig();

            if (config.CategorizerDebugLogging)
                Plugin.PluginLog.Debug(
                    $"[PlateWatcher] Plate loaded: name=\"{playerName}\" " +
                    $"world=\"{worldName}\" worldId={worldId}");

            if (worldId == 0)
            {
                Plugin.PluginLog.Warning(
                    $"[PlateWatcher] Could not resolve world from \"{rawWorld}\"; skipping.");
                return;
            }

            string bio = ReadTextNodeInComponent(addon, NodeIdBioComponent, NodeIdBioText);

            if (config.CategorizerDebugLogging)
                Plugin.PluginLog.Debug($"[PlateWatcher] bio: \"{bio}\"");

            if (string.IsNullOrWhiteSpace(bio))
            {
                Plugin.PluginLog.Debug(
                    $"[PlateWatcher] Bio is empty for {playerName}; no rules evaluated.");
                return;
            }

            // Persist bio snapshot regardless of whether a categorizer rule matches.
            var player = ServiceContext.PlayerDataService.GetPlayer(playerName, worldId);
            if (player != null)
                PlayerBioService.UpdateBioIfChanged(player.Id, bio);

            if (config.CategorizerRules.Count == 0)
            {
                Plugin.PluginLog.Debug("[PlateWatcher] No categorizer rules configured.");
                return;
            }

            bool anyEnabled = false;
            foreach (var rule in config.CategorizerRules)
            {
                if (!rule.Enabled || string.IsNullOrEmpty(rule.Keyword)) continue;
                anyEnabled = true;

                if (!Matches(bio, rule)) continue;

                Plugin.PluginLog.Information(
                    $"[PlateWatcher] Rule matched: keyword=\"{rule.Keyword}\" " +
                    $"categoryId={rule.CategoryId} player=\"{playerName}\"@worldId={worldId}");

                if (player == null)
                {
                    Plugin.PluginLog.Warning(
                        $"[PlateWatcher] Player \"{playerName}\"@worldId={worldId} " +
                        "not found in PlayerTrack cache; skipping.");
                    return;
                }

                PlayerCategoryService.AssignCategoryToPlayerSync(player.Id, (int)rule.CategoryId);
                return;
            }

            if (!anyEnabled)
                Plugin.PluginLog.Debug("[PlateWatcher] All categorizer rules are disabled.");
            else
                Plugin.PluginLog.Debug(
                    $"[PlateWatcher] No rules matched bio for \"{playerName}\".");
            } // end inner try
            finally
            {
                OnPlateProcessed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "[PlateWatcher] Unhandled exception in OnCharaCardUpdate.");
            _pendingProcessing = false;
        }
    }

    // ----------------------------------------------------------------
    // Node traversal helpers
    // ----------------------------------------------------------------

    private static unsafe List<(uint CompId, uint TextId, string Content)>
        CollectNonEmptyTextNodes(AtkUnitBase* addon)
    {
        var results = new List<(uint, uint, string)>();
        if (addon == null) return results;

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (uint)node->Type < 1000) continue;

            var compNode = (AtkComponentNode*)node;
            if (compNode->Component == null) continue;

            uint compId  = node->NodeId;
            var children = compNode->Component->UldManager;

            for (var ci = 0; ci < children.NodeListCount; ci++)
            {
                var child = children.NodeList[ci];
                if (child == null || child->Type != NodeType.Text) continue;

                string content = ((AtkTextNode*)child)->NodeText.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(content))
                    results.Add((compId, child->NodeId, content));
            }
        }

        return results;
    }

    private static unsafe string ReadTextNodeInComponent(
        AtkUnitBase* addon, uint componentNodeId, uint textNodeId)
    {
        if (addon == null) return string.Empty;

        var rootNode = addon->GetNodeById(componentNodeId);
        if (rootNode == null || (uint)rootNode->Type < 1000) return string.Empty;

        var compNode = (AtkComponentNode*)rootNode;
        if (compNode->Component == null) return string.Empty;

        var children = compNode->Component->UldManager;
        for (var i = 0; i < children.NodeListCount; i++)
        {
            var child = children.NodeList[i];
            if (child == null || child->NodeId != textNodeId) continue;
            if (child->Type != NodeType.Text) continue;
            return ((AtkTextNode*)child)->NodeText.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    // ----------------------------------------------------------------
    // Keyword matching
    // ----------------------------------------------------------------

    private static bool Matches(string bio, Models.CategoryRule rule)
    {
        switch (rule.MatchMode)
        {
            case Models.RuleMatchMode.Regex:
                return MatchesRegex(bio, rule);
            case Models.RuleMatchMode.Shorthand:
                return MatchesShorthand(bio, rule);
        }

        if (rule.WholeWord)
        {
            var opts = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            // \b doesn't work for tokens ending in non-word chars like "F+", so use
            // explicit shorthand-token boundaries that treat '+' as part of the token.
            var pattern = @"(?<![A-Za-z0-9+])" + Regex.Escape(rule.Keyword) + @"(?![A-Za-z0-9+])";
            return Regex.IsMatch(bio, pattern, opts);
        }

        var cmp = rule.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return bio.Contains(rule.Keyword, cmp);
    }

    private static bool MatchesRegex(string bio, Models.CategoryRule rule)
    {
        if (string.IsNullOrEmpty(rule.Keyword)) return false;
        try
        {
            var opts = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(bio, rule.Keyword, opts);
        }
        catch (ArgumentException ex)
        {
            Plugin.PluginLog.Warning(ex, $"[PlateWatcher] Invalid regex in rule: \"{rule.Keyword}\"");
            return false;
        }
    }

    /// <summary>
    /// Shorthand-token matcher. Splits the bio on " lf " into primary / secondary
    /// segments, tokenizes each on '/', and tests rule.PrimaryToken (and optionally
    /// rule.SecondaryToken) against the parsed tokens using a satisfier set derived
    /// from the full rule list. Base tokens (e.g. "F") are satisfied by themselves
    /// OR their "+" extended form ("F+") when that extended form exists in any rule;
    /// extended tokens are satisfied strictly by themselves.
    /// </summary>
    private static bool MatchesShorthand(string bio, Models.CategoryRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.PrimaryToken)) return false;
        if (!TrySplitLf(bio, out var leftRaw, out var rightRaw)) return false;

        var leftTokens  = TokenizeShorthand(leftRaw);
        var rightTokens = TokenizeShorthand(rightRaw);

        var tokenUniverse = BuildTokenUniverse();
        var satisfiers    = BuildSatisfiers(tokenUniverse);

        if (!IsSatisfiedIn(rule.PrimaryToken, leftTokens, satisfiers)) return false;

        if (!string.IsNullOrWhiteSpace(rule.SecondaryToken) &&
            !IsSatisfiedIn(rule.SecondaryToken, rightTokens, satisfiers))
            return false;

        return true;
    }

    private static bool TrySplitLf(string input, out string left, out string right)
    {
        left = right = string.Empty;
        if (string.IsNullOrEmpty(input)) return false;
        var match = Regex.Match(input, @"\s+lf\s+", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        left  = input[..match.Index];
        right = input[(match.Index + match.Length)..];
        return true;
    }

    private static HashSet<string> TokenizeShorthand(string segment)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(segment)) return tokens;
        foreach (var raw in segment.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            tokens.Add(raw.ToUpperInvariant());
        return tokens;
    }

    private static HashSet<string> BuildTokenUniverse()
    {
        var universe = new HashSet<string>(StringComparer.Ordinal);
        var rules = ServiceContext.ConfigService.GetConfig().CategorizerRules;
        foreach (var r in rules)
        {
            if (r.MatchMode != Models.RuleMatchMode.Shorthand) continue;
            if (!string.IsNullOrWhiteSpace(r.PrimaryToken))   universe.Add(r.PrimaryToken.Trim().ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(r.SecondaryToken)) universe.Add(r.SecondaryToken.Trim().ToUpperInvariant());
        }
        return universe;
    }

    private static Dictionary<string, HashSet<string>> BuildSatisfiers(HashSet<string> tokenUniverse)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in tokenUniverse)
        {
            if (t.EndsWith("+", StringComparison.Ordinal))
            {
                map[t] = new HashSet<string>(StringComparer.Ordinal) { t };
            }
            else
            {
                var set = new HashSet<string>(StringComparer.Ordinal) { t };
                var extended = t + "+";
                if (tokenUniverse.Contains(extended)) set.Add(extended);
                map[t] = set;
            }
        }
        return map;
    }

    private static bool IsSatisfiedIn(string ruleToken, HashSet<string> parsedTokens, Dictionary<string, HashSet<string>> satisfiers)
    {
        var key = ruleToken.Trim().ToUpperInvariant();
        if (!satisfiers.TryGetValue(key, out var set))
        {
            // Rule references a token not in the universe (shouldn't happen since we
            // built the universe from rules, but be defensive).
            set = key.EndsWith("+", StringComparison.Ordinal)
                ? new HashSet<string>(StringComparer.Ordinal) { key }
                : new HashSet<string>(StringComparer.Ordinal) { key, key + "+" };
        }

        foreach (var s in set)
            if (parsedTokens.Contains(s)) return true;
        return false;
    }

    // ----------------------------------------------------------------
    // World helpers
    // ----------------------------------------------------------------

    /// <summary>Strips " [DatacenterName]" from the world node text.</summary>
    private static string StripDatacenter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        int bracket = raw.IndexOf('[');
        return (bracket > 0 ? raw[..bracket] : raw).Trim();
    }

    private static uint ResolveWorldId(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return 0;

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (sheet == null) return 0;

        foreach (var row in sheet)
        {
            if (row.Name.ToString().Equals(worldName, StringComparison.OrdinalIgnoreCase))
                return row.RowId;
        }

        Plugin.PluginLog.Warning($"[PlateWatcher] World \"{worldName}\" not found in Lumina sheet.");
        return 0;
    }
}
