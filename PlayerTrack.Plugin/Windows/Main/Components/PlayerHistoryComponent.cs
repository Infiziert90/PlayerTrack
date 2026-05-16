using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;
using PlayerTrack.Windows.ViewModels;

namespace PlayerTrack.Windows.Main.Components;

/// <summary>
/// "Data" tab content. Surface stats are visible by default; deeper history
/// and per-job aggregation appear when the "Stats for Nerds" toggle is on.
/// The toggle state is persisted on <see cref="PluginConfig.ShowStatsForNerds"/>.
/// </summary>
public class PlayerHistoryComponent(IMainPresenter presenter) : ViewComponent
{
    private const float SameLineOffset1 = 120f;
    private const float SurfaceLabelOffset = 130f;

    public override void Draw()
    {
        var player = presenter.GetSelectedPlayer();
        if (player == null)
            return;

        using var child = ImRaii.Child("###PlayerSummary_Data", new Vector2(-1, 0), false);
        if (!child.Success)
            return;

        DrawSurfaceStats(player);

        ImGuiHelpers.ScaledDummy(8f);

        var nerds = Config.ShowStatsForNerds;
        if (ImGui.Checkbox("Stats for Nerds", ref nerds))
        {
            Config.ShowStatsForNerds = nerds;
            ServiceContext.ConfigService.SaveConfig(Config);
        }

        if (!nerds)
            return;

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawNerdStats(player);
    }

    private static void DrawSurfaceStats(PlayerView player)
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, "Overview");
        ImGuiHelpers.ScaledDummy(2f);

        DrawLabelValue("Appearance",   player.Appearance);
        DrawLabelValue("Home World",   player.HomeWorld);
        DrawLabelValue("Data Center",  player.DataCenter);
        DrawLabelValue("Free Company", player.FreeCompany);
        DrawLabelValue("Lodestone",    player.LodestoneId == 0 ? Language.NotAvailable : player.LodestoneId.ToString());
        DrawLabelValue("Total Time",   player.TotalEncounterTime);
        DrawLabelValue("Longest Enc.", player.LongestEncounterTime);
        DrawLabelValue("Last Location", player.LastLocation);
    }

    private static void DrawLabelValue(string label, string value)
    {
        ImGui.TextUnformatted(label);
        ImGuiHelpers.ScaledRelativeSameLine(SurfaceLabelOffset);
        ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? Language.NotAvailable : value);
    }

    private static void DrawNerdStats(PlayerView player)
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, "Activity");
        ImGuiHelpers.ScaledDummy(2f);
        DrawLabelValue(Language.FirstSeen,    player.FirstSeen);
        DrawLabelValue(Language.LastSeen,     player.LastSeen);
        DrawLabelValue(Language.SeenCount,    player.SeenCount);
        DrawLabelValue("Encounter Count",     player.Encounters.Count.ToString());

        ImGuiHelpers.ScaledDummy(8f);
        DrawJobBreakdown(player);

        ImGuiHelpers.ScaledDummy(8f);
        DrawNameWorldHistory(player);

        ImGuiHelpers.ScaledDummy(8f);
        DrawAppearanceHistory(player);
    }

    private static void DrawJobBreakdown(PlayerView player)
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, "Job Frequency");
        if (player.Encounters.Count == 0)
        {
            ImGui.TextUnformatted(Language.NoEncountersMessage);
            return;
        }

        var total = player.Encounters.Count;
        var groups = player.Encounters
            .Where(e => !string.IsNullOrEmpty(e.Job))
            .GroupBy(e => e.Job)
            .Select(g => new { Job = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        foreach (var g in groups)
        {
            var pct = total == 0 ? 0 : (int)System.Math.Round(g.Count * 100.0 / total);
            ImGui.TextUnformatted(g.Job);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
            ImGui.TextUnformatted($"{g.Count} ({pct}%)");
        }
    }

    private static void DrawNameWorldHistory(PlayerView player)
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Time);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.NameWorld);
        if (player.PlayerNameWorldHistories.Count == 0)
        {
            ImGui.TextUnformatted(Language.NoHistoryMessage);
            return;
        }

        foreach (var ph in player.PlayerNameWorldHistories)
        {
            ImGui.TextUnformatted(ph.Time);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
            ImGui.TextUnformatted(ph.NameWorld);
        }
    }

    private static void DrawAppearanceHistory(PlayerView player)
    {
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Time);
        ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Appearance);
        if (player.PlayerCustomizeHistories.Count == 0)
        {
            ImGui.TextUnformatted(Language.NoHistoryMessage);
            return;
        }

        foreach (var ph in player.PlayerCustomizeHistories)
        {
            ImGui.TextUnformatted(ph.Time);
            ImGuiHelpers.ScaledRelativeSameLine(SameLineOffset1);
            ImGui.TextUnformatted(ph.Appearance);
        }
    }
}
