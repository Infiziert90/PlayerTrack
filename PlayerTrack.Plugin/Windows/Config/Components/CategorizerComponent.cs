using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;

namespace PlayerTrack.Windows.Config.Components;

/// <summary>
/// Config panel that manages two families of auto-categorizer rules:
///
///   Tab 1 "Plate Bio Rules"   -- keyword rules matched against adventurer
///                                plate bio text (PlateWatcher).
///   Tab 2 "Encounter Rules"   -- zone-time rules that fire when an encounter
///                                ends and a player's in-zone duration meets
///                                the configured threshold (EncounterWatcher).
/// </summary>
public class CategorizerComponent : ConfigViewComponent
{
    // ----------------------------------------------------------------
    // Plate bio rule staging fields
    // ----------------------------------------------------------------

    private string _newKeyword         = string.Empty;
    private string _newPrimaryToken    = string.Empty;
    private string _newSecondaryToken  = string.Empty;
    private bool   _newCaseSensitive;
    private int    _newModeIndex; // index into ModeNames
    private int    _newCategoryIndex;

    // UI-level mode names (combine MatchMode + WholeWord for ergonomics).
    private static readonly string[] ModeNames = { "Substring", "Whole Word", "Regex", "Shorthand" };

    private static int RuleToModeIndex(CategoryRule rule) => rule.MatchMode switch
    {
        RuleMatchMode.Regex     => 2,
        RuleMatchMode.Shorthand => 3,
        _                       => rule.WholeWord ? 1 : 0,
    };

    private static void ApplyModeIndex(CategoryRule rule, int index)
    {
        switch (index)
        {
            case 1: rule.MatchMode = RuleMatchMode.Substring; rule.WholeWord = true;  break;
            case 2: rule.MatchMode = RuleMatchMode.Regex;     rule.WholeWord = false; break;
            case 3: rule.MatchMode = RuleMatchMode.Shorthand; rule.WholeWord = false; break;
            default: rule.MatchMode = RuleMatchMode.Substring; rule.WholeWord = false; break;
        }
    }

    // ----------------------------------------------------------------
    // Encounter rule staging fields
    // ----------------------------------------------------------------

    private int    _newEncMinutes    = 5;
    private int    _newEncCatIndex;

    // Zone picker state
    private string                                        _zoneSearch          = string.Empty;
    private List<(uint Id, string Place, string Content)>? _zoneCache;
    private int                                           _pickingForRule      = int.MinValue; // index into EncounterRules, or -1 for new-rule row
    private (uint Id, string Place, string Content)       _newEncZone          = default;

    // ----------------------------------------------------------------
    // Draw
    // ----------------------------------------------------------------

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("###CategorizerTabs");
        if (!tabs.Success) return;

        using (var tab = ImRaii.TabItem("Plate Bio Rules"))
        {
            if (tab.Success)
                DrawPlateBioTab();
        }

        using (var tab = ImRaii.TabItem("Encounter Rules"))
        {
            if (tab.Success)
                DrawEncounterTab();
        }

        using (var tab = ImRaii.TabItem("Auto Scrape"))
        {
            if (tab.Success)
                DrawAutoScrapeTab();
        }
    }

    // ================================================================
    // TAB 1 -- Plate bio rules (keyword categorizer)
    // ================================================================

    private void DrawPlateBioTab()
    {
        // Exclude dynamic social-list categories (FL, FC, etc.) -- they are
        // managed by the social list sync system and must not be assigned via
        // keyword rules.
        var categories = ServiceContext.CategoryService.GetCategories(includeDynamic: false);
        if (categories.Count == 0)
        {
            ImGui.TextUnformatted(
                "No categories defined. Create at least one category in the Categories tab first.");
            return;
        }

        var categoryNames = categories.Select(c => c.Name).ToArray();

        DrawDebugToggle();
        ImGuiHelpers.ScaledDummy(6f);
        DrawBioRuleTable(categories, categoryNames);
        ImGuiHelpers.ScaledDummy(6f);
        DrawBioAddRow(categories, categoryNames);
    }

    private void DrawDebugToggle()
    {
        var debug = Config.CategorizerDebugLogging;
        if (ImGui.Checkbox("Enable debug logging (PlateWatcher / EncounterWatcher)", ref debug))
        {
            Config.CategorizerDebugLogging = debug;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Writes verbose categorizer output to /xllog.");
    }

    private void DrawBioRuleTable(IReadOnlyList<Category> categories, string[] categoryNames)
    {
        if (Config.CategorizerRules.Count == 0)
        {
            ImGui.TextUnformatted("No rules yet. Use the form below to add one.");
            return;
        }

        using var table = ImRaii.Table(
            "##CategorizerRules",
            6,
            ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY,
            ImGuiHelpers.ScaledVector2(0f, 200f));
        if (!table.Success) return;

        ImGui.TableSetupColumn("On",       ImGuiTableColumnFlags.WidthFixed,   24f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Mode",     ImGuiTableColumnFlags.WidthFixed,  110f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Match",    ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Case",     ImGuiTableColumnFlags.WidthFixed,   40f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed,  170f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("",         ImGuiTableColumnFlags.WidthFixed,   22f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        int? removeAt = null;

        for (var i = 0; i < Config.CategorizerRules.Count; i++)
        {
            var rule = Config.CategorizerRules[i];
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var enabled = rule.Enabled;
            if (ImGui.Checkbox($"###BEn{i}", ref enabled))
            {
                rule.Enabled = enabled;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            ImGui.TableNextColumn();
            var modeIdx = RuleToModeIndex(rule);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo($"###BMode{i}", ref modeIdx, ModeNames, ModeNames.Length))
            {
                ApplyModeIndex(rule, modeIdx);
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            ImGui.TableNextColumn();
            if (rule.MatchMode == RuleMatchMode.Shorthand)
            {
                var avail = ImGui.GetContentRegionAvail().X;
                var lfWidth = ImGui.CalcTextSize("lf").X + (8f * ImGuiHelpers.GlobalScale);
                var halfWidth = (avail - lfWidth) * 0.5f;
                var prim = rule.PrimaryToken;
                ImGui.SetNextItemWidth(halfWidth);
                if (ImGui.InputTextWithHint($"###BPrim{i}", "Primary", ref prim, 32))
                {
                    rule.PrimaryToken = prim;
                    ServiceContext.ConfigService.SaveConfig(Config);
                }
                ImGui.SameLine();
                ImGui.TextUnformatted("lf");
                ImGui.SameLine();
                var sec = rule.SecondaryToken;
                ImGui.SetNextItemWidth(halfWidth);
                if (ImGui.InputTextWithHint($"###BSec{i}", "Secondary", ref sec, 32))
                {
                    rule.SecondaryToken = sec;
                    ServiceContext.ConfigService.SaveConfig(Config);
                }
            }
            else
            {
                var kw = rule.Keyword;
                ImGui.SetNextItemWidth(-1f);
                var hint = rule.MatchMode == RuleMatchMode.Regex ? "Regex pattern..." : "Keyword...";
                if (ImGui.InputTextWithHint($"###BKw{i}", hint, ref kw, 100))
                {
                    rule.Keyword = kw;
                    ServiceContext.ConfigService.SaveConfig(Config);
                }
            }

            ImGui.TableNextColumn();
            var cs = rule.CaseSensitive;
            if (ImGui.Checkbox($"###BCs{i}", ref cs))
            {
                rule.CaseSensitive = cs;
                ServiceContext.ConfigService.SaveConfig(Config);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Case sensitive");

            ImGui.TableNextColumn();
            var catIdx = categories.ToList().FindIndex(c => c.Id == (int)rule.CategoryId);
            if (catIdx < 0) catIdx = 0;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo($"###BCat{i}", ref catIdx, categoryNames, categoryNames.Length))
            {
                rule.CategoryId          = (uint)categories[catIdx].Id;
                rule.CategoryDisplayName = categories[catIdx].Name;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(FontAwesomeIcon.Trash.ToIconString());
            if (ImGui.IsItemClicked()) removeAt = i;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove rule");
        }

        if (removeAt.HasValue)
        {
            Config.CategorizerRules.RemoveAt(removeAt.Value);
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    private void DrawBioAddRow(IReadOnlyList<Category> categories, string[] categoryNames)
    {
        ImGui.TextUnformatted("Add rule:");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.SetNextItemWidth(110f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("###NewMode", ref _newModeIndex, ModeNames, ModeNames.Length);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Match mode");

        ImGui.SameLine();
        if (_newModeIndex == 3) // Shorthand
        {
            ImGui.SetNextItemWidth(90f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###NewPrim", "Primary", ref _newPrimaryToken, 32);
            ImGui.SameLine();
            ImGui.TextUnformatted("lf");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###NewSec", "Secondary", ref _newSecondaryToken, 32);
        }
        else
        {
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            var hint = _newModeIndex == 2 ? "Regex pattern..." : "Keyword...";
            ImGui.InputTextWithHint("###NewKw", hint, ref _newKeyword, 100);
        }

        ImGui.SameLine();
        ImGui.Checkbox("Case##New", ref _newCaseSensitive);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Case sensitive");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("###NewCat", ref _newCategoryIndex, categoryNames, categoryNames.Length);

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(FontAwesomeIcon.Plus.ToIconString());

        var hasMatchInput = _newModeIndex == 3
            ? !string.IsNullOrWhiteSpace(_newPrimaryToken)
            : !string.IsNullOrWhiteSpace(_newKeyword);

        if (ImGui.IsItemClicked()
            && hasMatchInput
            && _newCategoryIndex < categories.Count)
        {
            var cat = categories[_newCategoryIndex];
            var rule = new CategoryRule
            {
                CaseSensitive       = _newCaseSensitive,
                CategoryId          = (uint)cat.Id,
                CategoryDisplayName = cat.Name,
                Enabled             = true,
            };
            ApplyModeIndex(rule, _newModeIndex);

            if (_newModeIndex == 3)
            {
                rule.PrimaryToken   = _newPrimaryToken.Trim();
                rule.SecondaryToken = _newSecondaryToken.Trim();
            }
            else
            {
                rule.Keyword = _newKeyword.Trim();
            }

            Config.CategorizerRules.Add(rule);
            _newKeyword         = string.Empty;
            _newPrimaryToken    = string.Empty;
            _newSecondaryToken  = string.Empty;
            _newCaseSensitive   = false;
            _newModeIndex       = 0;
            _newCategoryIndex   = 0;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add rule");
    }

    // ================================================================
    // TAB 2 -- Encounter rules (zone-time categorizer)
    // ================================================================

    private void DrawEncounterTab()
    {
        // Exclude dynamic social-list categories -- same reason as DrawPlateBioTab.
        var categories = ServiceContext.CategoryService.GetCategories(includeDynamic: false);
        if (categories.Count == 0)
        {
            ImGui.TextUnformatted(
                "No categories defined. Create at least one category in the Categories tab first.");
            return;
        }

        var categoryNames = categories.Select(c => c.Name).ToArray();

        DrawGlobalMinimum();
        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        DrawEncounterRuleTable(categories, categoryNames);
        ImGuiHelpers.ScaledDummy(6f);
        DrawEncounterAddRow(categories, categoryNames);
        DrawZonePickerPopup();

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFAAAAAA);
        ImGui.TextWrapped(
            "Note: Encounter time data is only recorded when 'Add Encounters' is enabled " +
            "for the relevant zone type in the Locations tab. Overworld encounter tracking " +
            "is disabled by default.");
        ImGui.PopStyleColor();
    }

    private void DrawGlobalMinimum()
    {
        ImGui.TextUnformatted("Global exclusion: skip encounters shorter than");
        ImGui.SameLine();
        var minSec = Config.EncounterRuleMinEncounterSeconds;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("seconds##GlobalMin", ref minSec, 10))
        {
            if (minSec < 0) minSec = 0;
            Config.EncounterRuleMinEncounterSeconds = minSec;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Encounters shorter than this (for you, the local player) are ignored " +
                "entirely -- no per-rule checks are performed. Set to 0 to disable.");
    }

    private void DrawEncounterRuleTable(IReadOnlyList<Category> categories, string[] categoryNames)
    {
        if (Config.EncounterRules.Count == 0)
        {
            ImGui.TextUnformatted("No rules yet. Use the form below to add one.");
            return;
        }

        using var table = ImRaii.Table(
            "##EncounterRules",
            6,
            ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY,
            ImGuiHelpers.ScaledVector2(0f, 200f));
        if (!table.Success) return;

        ImGui.TableSetupColumn("On",       ImGuiTableColumnFlags.WidthFixed,   24f  * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Zone",     ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("ID",       ImGuiTableColumnFlags.WidthFixed,   48f  * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Min (m)",  ImGuiTableColumnFlags.WidthFixed,   68f  * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed,  170f  * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("",         ImGuiTableColumnFlags.WidthFixed,   22f  * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        int? removeAt = null;

        for (var i = 0; i < Config.EncounterRules.Count; i++)
        {
            var rule = Config.EncounterRules[i];
            ImGui.TableNextRow();

            // Enabled
            ImGui.TableNextColumn();
            var enabled = rule.Enabled;
            if (ImGui.Checkbox($"###EEn{i}", ref enabled))
            {
                rule.Enabled = enabled;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            // Zone name (clickable to re-pick)
            ImGui.TableNextColumn();
            var displayName = string.IsNullOrWhiteSpace(rule.ZoneDisplayName)
                ? $"Zone {rule.TerritoryTypeId}"
                : rule.ZoneDisplayName;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Button($"{displayName}###EZone{i}"))
            {
                _pickingForRule = i;
                _zoneSearch     = string.Empty;
                ImGui.OpenPopup("###ZonePicker");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to change zone");

            // Territory type ID (read-only display)
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(rule.TerritoryTypeId.ToString());

            // Min duration in minutes (stored as seconds)
            ImGui.TableNextColumn();
            var mins = rule.MinDurationSeconds / 60;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputInt($"###EMin{i}", ref mins, 1))
            {
                if (mins < 1) mins = 1;
                rule.MinDurationSeconds = mins * 60;
                ServiceContext.ConfigService.SaveConfig(Config);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minimum in-zone time (minutes)");

            // Category
            ImGui.TableNextColumn();
            var catIdx = categories.ToList().FindIndex(c => c.Id == (int)rule.CategoryId);
            if (catIdx < 0) catIdx = 0;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo($"###ECat{i}", ref catIdx, categoryNames, categoryNames.Length))
            {
                rule.CategoryId          = (uint)categories[catIdx].Id;
                rule.CategoryDisplayName = categories[catIdx].Name;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            // Delete
            ImGui.TableNextColumn();
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(FontAwesomeIcon.Trash.ToIconString());
            if (ImGui.IsItemClicked()) removeAt = i;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove rule");
        }

        if (removeAt.HasValue)
        {
            Config.EncounterRules.RemoveAt(removeAt.Value);
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    private void DrawEncounterAddRow(IReadOnlyList<Category> categories, string[] categoryNames)
    {
        ImGui.TextUnformatted("Add rule:");
        ImGuiHelpers.ScaledDummy(2f);

        // Zone picker button for new rule
        var newZoneLabel = _newEncZone.Id == 0
            ? "Pick Zone..."
            : $"{_newEncZone.Place} [{_newEncZone.Id}]";
        if (ImGui.Button(newZoneLabel + "###NewEZone"))
        {
            _pickingForRule = -1;
            _zoneSearch     = string.Empty;
            ImGui.OpenPopup("###ZonePicker");
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Select territory type");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(60f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("min##NewEMin", ref _newEncMinutes, 1);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minimum in-zone time (minutes)");
        if (_newEncMinutes < 1) _newEncMinutes = 1;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("###NewECat", ref _newEncCatIndex, categoryNames, categoryNames.Length);

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(FontAwesomeIcon.Plus.ToIconString());

        if (ImGui.IsItemClicked()
            && _newEncZone.Id != 0
            && _newEncCatIndex < categories.Count)
        {
            var cat = categories[_newEncCatIndex];
            Config.EncounterRules.Add(new EncounterRule
            {
                TerritoryTypeId     = _newEncZone.Id,
                ZoneDisplayName     = _newEncZone.Place,
                MinDurationSeconds  = _newEncMinutes * 60,
                CategoryId          = (uint)cat.Id,
                CategoryDisplayName = cat.Name,
                Enabled             = true,
            });
            _newEncZone      = default;
            _newEncMinutes   = 5;
            _newEncCatIndex  = 0;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add rule");
    }

    // ================================================================
    // TAB 3 -- Auto Scrape (background bio collection)
    // ================================================================

    private void DrawAutoScrapeTab()
    {
        ImGuiHelpers.ScaledDummy(4f);

        var enabled = Config.AutoScrapeEnabled;
        if (ImGui.Checkbox("Enable automatic plate bio scraping", ref enabled))
        {
            Config.AutoScrapeEnabled = enabled;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "When enabled, PlayerTrack silently opens each player's Adventurer Plate\n" +
                "in the background as they enter the zone, reads the bio, then closes the\n" +
                "window.  No external plugins are required.");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(8f);

        using var disabled = Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!Config.AutoScrapeEnabled);

        // Interval
        ImGui.TextUnformatted("Minimum seconds between plate openings:");
        ImGui.SameLine();
        var interval = Config.AutoScrapeIntervalSeconds;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##ScrapeInterval", ref interval, 1))
        {
            if (interval < 1) interval = 1;
            Config.AutoScrapeIntervalSeconds = interval;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Lower values collect bios more quickly but risk server-side rate limiting.\n" +
                "8 seconds is a safe default.");

        ImGuiHelpers.ScaledDummy(4f);

        // Stale threshold
        ImGui.TextUnformatted("Re-scrape a player's bio after (days):");
        ImGui.SameLine();
        var staleDays = Config.AutoScrapeStaleAfterDays;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##ScrapeStaleDays", ref staleDays, 1))
        {
            if (staleDays < 0) staleDays = 0;
            Config.AutoScrapeStaleAfterDays = staleDays;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "A player's bio is re-scraped only when the stored entry is older than\n" +
                "this many days.  Set to 0 to always scrape on every zone entry.");

        ImGuiHelpers.ScaledDummy(12f);
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFAAAAAA);
        ImGui.TextWrapped(
            "Note: Adventurer Plates are only available for players who have created one.\n" +
            "Players without a plate will show an empty bio and no entry will be stored.\n" +
            "The CharaCard window is opened and hidden automatically -- you will not see it\n" +
            "flash on screen under normal circumstances.");
        ImGui.PopStyleColor();
    }

    // ----------------------------------------------------------------
    // Zone picker popup (shared between table rows and the add-rule row)
    // ----------------------------------------------------------------

    private void DrawZonePickerPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(540f, 400f), ImGuiCond.Appearing);
        using var popup = ImRaii.Popup("###ZonePicker");
        if (!popup.Success) return;

        ImGui.TextUnformatted("Search territory type:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("###ZoneSearch", "Zone name or ID...", ref _zoneSearch, 128);

        ImGuiHelpers.ScaledDummy(4f);

        var zones = GetZones();
        var filter = _zoneSearch.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? zones
            : zones.Where(z =>
                z.Place.Contains(filter, System.StringComparison.OrdinalIgnoreCase) ||
                z.Content.Contains(filter, System.StringComparison.OrdinalIgnoreCase) ||
                z.Id.ToString().Contains(filter)).ToList();

        using var table = ImRaii.Table(
            "##ZonePickerTable",
            3,
            ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg,
            ImGuiHelpers.ScaledVector2(-1f, -1f));
        if (!table.Success) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("ID",      ImGuiTableColumnFlags.WidthFixed,  50f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Zone",    ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var z in filtered)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(z.Id.ToString());
            ImGui.TableNextColumn();

            var selected = false;
            if (ImGui.Selectable(
                    $"{z.Place}###ZP{z.Id}",
                    ref selected,
                    ImGuiSelectableFlags.SpanAllColumns))
            {
                ApplyZonePick(z);
                ImGui.CloseCurrentPopup();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(z.Content);
        }
    }

    private void ApplyZonePick((uint Id, string Place, string Content) z)
    {
        if (_pickingForRule == -1)
        {
            // New-rule row
            _newEncZone = z;
        }
        else if (_pickingForRule >= 0 && _pickingForRule < Config.EncounterRules.Count)
        {
            // Existing rule
            var rule = Config.EncounterRules[_pickingForRule];
            rule.TerritoryTypeId = z.Id;
            rule.ZoneDisplayName = z.Place;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        _pickingForRule = int.MinValue;
    }

    // ----------------------------------------------------------------
    // Territory type data (lazy, cached for session lifetime)
    // ----------------------------------------------------------------

    private List<(uint Id, string Place, string Content)> GetZones()
    {
        if (_zoneCache != null) return _zoneCache;

        _zoneCache = new List<(uint, string, string)>();

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        if (sheet == null) return _zoneCache;

        foreach (var row in sheet)
        {
            var placeName = row.PlaceName.Value.Name.ToString();
            if (string.IsNullOrWhiteSpace(placeName)) continue;

            var contentName = string.Empty;
            try
            {
                contentName = row.ContentFinderCondition.Value.Name.ToString();
            }
            catch { /* ContentFinderCondition may not be valid for all rows */ }

            _zoneCache.Add((row.RowId, placeName, contentName));
        }

        _zoneCache.Sort((a, b) =>
            System.StringComparer.OrdinalIgnoreCase.Compare(a.Place, b.Place));

        return _zoneCache;
    }
}
