using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Data;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;
using PlayerTrack.Windows.ViewModels;

namespace PlayerTrack.Windows.Main.Components;

public class PlayerEncounterComponent : ViewComponent
{
    private const float SameLineOffset1 = 70f;
    private const float SameLineOffset2 = 160f;
    private const float SameLineOffset3 = 200f;
    private const float SameLineOffset4 = 230f;
    private readonly IMainPresenter Presenter;

    private string _search = string.Empty;
    private int _groupIndex;
    private static readonly string[] GroupNames = { "All", "Housing", "Overworld", "Content", "High-End" };

    // Substrings that identify residential / housing districts in FFXIV.
    // Match is case-insensitive and substring-based against the encounter
    // location name (which is the ContentName when available, otherwise the
    // PlaceName of the territory).
    private static readonly string[] HousingTokens =
    {
        "Mist",
        "The Lavender Beds",
        "Lavender Beds",
        "The Goblet",
        "Goblet",
        "Shirogane",
        "Empyreum",
        "Private Cottage",
        "Private House",
        "Private Mansion",
        "Private Chambers",
        "Topmast Apartment",
        "Lily Hills Apartment",
        "Sultana's Breath Apartment",
        "Kobai Goten Apartment",
        "Ingleside Apartment",
    };

    public PlayerEncounterComponent(IMainPresenter presenter)
    {
        Presenter = presenter;
    }

    public override void Draw()
    {
        var player = Presenter.GetSelectedPlayer();
        if (player == null)
            return;

        using var child = ImRaii.Child("###PlayerSummary_Encounter", new Vector2(-1, 0), false);
        if (!child.Success)
            return;

        DrawFilterBar();
        ImGuiHelpers.ScaledDummy(4f);

        var filtered = FilterEncounters(player.Encounters);

        if (filtered.Count == 0)
        {
            ImGui.TextUnformatted(Language.NoEncountersMessage);
            return;
        }

        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Time);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Duration);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset2);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Job);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset3);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Level);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset4);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Location);
        foreach (var enc in filtered)
        {
            ImGui.TextUnformatted(enc.Time);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
            ImGui.TextUnformatted(enc.Duration);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset2);
            ImGui.TextUnformatted(enc.Job);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset3);
            ImGui.TextUnformatted(enc.Level);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset4);
            ImGui.TextUnformatted(enc.Location);
        }
    }

    private void DrawFilterBar()
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var groupWidth = 130f * ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var searchWidth = availWidth - groupWidth - spacing;
        if (searchWidth < 80f * ImGuiHelpers.GlobalScale)
            searchWidth = 80f * ImGuiHelpers.GlobalScale;

        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputTextWithHint("###EncSearch", "Search location...", ref _search, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(groupWidth);
        ImGui.Combo("###EncGroup", ref _groupIndex, GroupNames, GroupNames.Length);
    }

    private List<PlayerEncounterView> FilterEncounters(List<PlayerEncounterView> encounters)
    {
        var query = encounters.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var needle = _search.Trim();
            query = query.Where(e =>
                !string.IsNullOrEmpty(e.Location) &&
                e.Location.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        switch (_groupIndex)
        {
            case 1: // Housing
                query = query.Where(e => IsHousing(e.Location));
                break;
            case 2: // Overworld
                query = query.Where(e => e.LocationType == LocationType.Overworld && !IsHousing(e.Location));
                break;
            case 3: // Content (non-high-end)
                query = query.Where(e => e.LocationType == LocationType.Content);
                break;
            case 4: // High-End
                query = query.Where(e => e.LocationType == LocationType.HighEndContent);
                break;
        }

        return query.ToList();
    }

    private static bool IsHousing(string location)
    {
        if (string.IsNullOrEmpty(location)) return false;
        foreach (var token in HousingTokens)
        {
            if (location.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
