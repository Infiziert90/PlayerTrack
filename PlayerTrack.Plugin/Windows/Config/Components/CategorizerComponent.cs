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
/// Config panel that lets the user manage keyword-to-category rules for the
/// built-in plate watcher (Adventurer Plate auto-categorizer).
/// </summary>
public class CategorizerComponent : ConfigViewComponent
{
    // Staging fields for the "add rule" row.
    private string _newKeyword      = string.Empty;
    private bool   _newCaseSensitive;
    private bool   _newWholeWord;
    private int    _newCategoryIndex;

    public override void Draw()
    {
        var categories = ServiceContext.CategoryService.GetCategories();

        if (categories.Count == 0)
        {
            ImGui.TextUnformatted(
                "No categories defined. Create at least one category in the Categories tab first.");
            return;
        }

        var categoryNames = categories.Select(c => c.Name).ToArray();

        DrawDebugToggle();
        ImGuiHelpers.ScaledDummy(6f);
        DrawRuleTable(categories, categoryNames);
        ImGuiHelpers.ScaledDummy(6f);
        DrawAddRow(categories, categoryNames);
    }

    // ----------------------------------------------------------------
    // Debug toggle
    // ----------------------------------------------------------------

    private void DrawDebugToggle()
    {
        var debug = Config.CategorizerDebugLogging;
        if (ImGui.Checkbox("Enable debug logging (PlateWatcher)", ref debug))
        {
            Config.CategorizerDebugLogging = debug;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Writes verbose plate-watcher output to /xllog.");
    }

    // ----------------------------------------------------------------
    // Rule table
    // ----------------------------------------------------------------

    private void DrawRuleTable(IReadOnlyList<Category> categories, string[] categoryNames)
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
        ImGui.TableSetupColumn("Keyword",  ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Case",     ImGuiTableColumnFlags.WidthFixed,   40f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Whole",    ImGuiTableColumnFlags.WidthFixed,   44f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed,  170f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("",         ImGuiTableColumnFlags.WidthFixed,   22f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        int? removeAt = null;

        for (var i = 0; i < Config.CategorizerRules.Count; i++)
        {
            var rule = Config.CategorizerRules[i];
            ImGui.TableNextRow();

            // -- Enabled --
            ImGui.TableNextColumn();
            var enabled = rule.Enabled;
            if (ImGui.Checkbox($"###En{i}", ref enabled))
            {
                rule.Enabled = enabled;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            // -- Keyword --
            ImGui.TableNextColumn();
            var kw = rule.Keyword;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText($"###Kw{i}", ref kw, 100))
            {
                rule.Keyword = kw;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            // -- Case sensitive --
            ImGui.TableNextColumn();
            var cs = rule.CaseSensitive;
            if (ImGui.Checkbox($"###Cs{i}", ref cs))
            {
                rule.CaseSensitive = cs;
                ServiceContext.ConfigService.SaveConfig(Config);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Case sensitive");

            // -- Whole word --
            ImGui.TableNextColumn();
            var ww = rule.WholeWord;
            if (ImGui.Checkbox($"###Ww{i}", ref ww))
            {
                rule.WholeWord = ww;
                ServiceContext.ConfigService.SaveConfig(Config);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Whole word only");

            // -- Category combo --
            ImGui.TableNextColumn();
            var catIdx = categories.ToList().FindIndex(c => c.Id == (int)rule.CategoryId);
            if (catIdx < 0) catIdx = 0;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo($"###Cat{i}", ref catIdx, categoryNames, categoryNames.Length))
            {
                rule.CategoryId          = (uint)categories[catIdx].Id;
                rule.CategoryDisplayName = categories[catIdx].Name;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            // -- Delete --
            ImGui.TableNextColumn();
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(FontAwesomeIcon.Trash.ToIconString());
            if (ImGui.IsItemClicked())
                removeAt = i;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove rule");
        }

        if (removeAt.HasValue)
        {
            Config.CategorizerRules.RemoveAt(removeAt.Value);
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    // ----------------------------------------------------------------
    // Add-rule row
    // ----------------------------------------------------------------

    private void DrawAddRow(IReadOnlyList<Category> categories, string[] categoryNames)
    {
        ImGui.TextUnformatted("Add rule:");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("###NewKw", "Keyword...", ref _newKeyword, 100);

        ImGui.SameLine();
        ImGui.Checkbox("Case##New", ref _newCaseSensitive);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Case sensitive");

        ImGui.SameLine();
        ImGui.Checkbox("Whole##New", ref _newWholeWord);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Whole word only");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("###NewCat", ref _newCategoryIndex, categoryNames, categoryNames.Length);

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(FontAwesomeIcon.Plus.ToIconString());

        if (ImGui.IsItemClicked()
            && !string.IsNullOrWhiteSpace(_newKeyword)
            && _newCategoryIndex < categories.Count)
        {
            var cat = categories[_newCategoryIndex];
            Config.CategorizerRules.Add(new CategoryRule
            {
                Keyword             = _newKeyword.Trim(),
                CaseSensitive       = _newCaseSensitive,
                WholeWord           = _newWholeWord,
                CategoryId          = (uint)cat.Id,
                CategoryDisplayName = cat.Name,
                Enabled             = true,
            });

            // Reset staging fields.
            _newKeyword       = string.Empty;
            _newCaseSensitive = false;
            _newWholeWord     = false;
            _newCategoryIndex = 0;

            ServiceContext.ConfigService.SaveConfig(Config);
        }

        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add rule");
    }
}
