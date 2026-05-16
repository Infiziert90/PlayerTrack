using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.API;
using PlayerTrack.Domain;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;
using PlayerTrack.Windows.ViewModels;

namespace PlayerTrack.Windows.Main.Components;

public class PlayerSummaryComponent : ViewComponent
{
    private const float SectionSpace = 2.8f;

    private readonly IMainPresenter Presenter;
    private float CategoryTagSplit;
    private int SelectedTagIndex;
    private int SelectedCategoryIndex;
    private float AssignedChildHeight;

    public PlayerSummaryComponent(IMainPresenter presenter)
    {
        Presenter = presenter;
    }

    private const string TotalTimeLabel  = "Total Time";
    private const string LongestEncLabel = "Longest Enc.";

    public override void Draw()
    {
        var player = Presenter.GetSelectedPlayer();
        if (player == null)
            return;

        using var child = ImRaii.Child("###PlayerSummaryPlayer", new Vector2(-1, 0), false);
        if (!child.Success)
            return;

        // Recompute the categories/tags split each frame so it tracks window resizes.
        CategoryTagSplit = ImGui.GetContentRegionAvail().X * 0.5f;

        DrawCenteredHeader(player);
        DrawCenteredStats(player);

        // Requested blank line between the name block and the rest.
        ImGuiHelpers.ScaledDummy(new Vector2(0, 10f));

        DrawCategoryTagHeadings();
        DrawCategoryTagAssignment(player);
        DrawCategoryTagAssignments(player);
        DrawNotes(player);
    }

    private static void DrawCenteredHeader(PlayerView player)
    {
        var name = string.IsNullOrEmpty(player.Name) ? "?" : player.Name;
        var world = string.IsNullOrEmpty(player.HomeWorld) ? "?" : player.HomeWorld;
        var display = $"{name}@{world}";

        var textSize = ImGui.CalcTextSize(display);
        var ximAvailable = XIVInstantMessengerProvider.IsAvailable();
        var iconBtnHeight = ImGui.GetFrameHeight();
        var infoIconWidth = HasHistoryTooltip(player) ? ImGui.CalcTextSize(FontAwesomeIcon.InfoCircle.ToIconString()).X + ImGui.GetStyle().ItemSpacing.X : 0f;

        // Estimate button widths so we can pre-center the whole row.
        var btnWidth = MeasureIconBtnWidth();
        var totalButtonsWidth = (ximAvailable ? btnWidth : 0f) + btnWidth;
        var totalWidth = textSize.X + infoIconWidth + ImGui.GetStyle().ItemSpacing.X + totalButtonsWidth;

        var avail = ImGui.GetContentRegionAvail().X;
        var startX = ImGui.GetCursorPosX() + Math.Max(0f, (avail - totalWidth) * 0.5f);

        ImGui.SetCursorPosX(startX);

        // Name@World text + optional info-icon (with combined tooltip for previous names/worlds).
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(display);
            if (HasHistoryTooltip(player))
            {
                ImGui.SameLine();
                using (ImRaii.PushFont(UiBuilder.IconFont))
                    Helper.TextColored(ImGuiColors.DalamudYellow, FontAwesomeIcon.InfoCircle.ToIconString());
            }
        }

        if (HasHistoryTooltip(player) && ImGui.IsItemHovered())
        {
            var tooltip = BuildHistoryTooltip(player);
            if (!string.IsNullOrEmpty(tooltip))
                ImGui.SetTooltip(tooltip);
        }

        if (ximAvailable)
        {
            ImGui.SameLine();
            DrawXimButton(player);
        }

        ImGui.SameLine();
        DrawPlayerSearchButton(player);
    }

    private static float MeasureIconBtnWidth()
    {
        var pad = ImGui.GetStyle().FramePadding.X * 2f;
        using var f = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.CalcTextSize(FontAwesomeIcon.Search.ToIconString()).X + pad;
    }

    private static bool HasHistoryTooltip(PlayerView player) =>
        !string.IsNullOrEmpty(player.PreviousNames) || !string.IsNullOrEmpty(player.PreviousWorlds);

    private static string BuildHistoryTooltip(PlayerView player)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(player.PreviousNames))
            parts.Add(string.Format(Language.PreviouslyKnownAs, player.PreviousNames));
        if (!string.IsNullOrEmpty(player.PreviousWorlds))
            parts.Add(string.Format(Language.PreviouslyOn, player.PreviousWorlds));
        return string.Join("\n", parts);
    }

    private static void DrawCenteredStats(PlayerView player)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var half = avail * 0.5f;
        var startX = ImGui.GetCursorPosX();

        var total = $"{TotalTimeLabel}: {player.TotalEncounterTime}";
        var longest = $"{LongestEncLabel}: {player.LongestEncounterTime}";

        var totalSize = ImGui.CalcTextSize(total);
        var longestSize = ImGui.CalcTextSize(longest);

        ImGui.SetCursorPosX(startX + Math.Max(0f, (half - totalSize.X) * 0.5f));
        ImGui.TextUnformatted(total);

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + half + Math.Max(0f, (half - longestSize.X) * 0.5f));
        ImGui.TextUnformatted(longest);
    }

    private void DrawNotes(PlayerView player)
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        var leftWidth = (availWidth - itemSpacing) * 0.5f;
        var areaHeight = -1f - (5f * ImGuiHelpers.GlobalScale);

        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Notes);
        ImGui.SameLine();
        ImGui.SetCursorPosX(leftWidth + itemSpacing);
        Helper.TextColored(ImGuiColors.DalamudViolet, "Plate Bio");

        using (ImRaii.Child("###PlayerNotes", new Vector2(leftWidth, areaHeight), false))
        {
            var flags = ImGuiInputTextFlags.AllowTabInput;
            if (Config.UseCtrlNewLine)
                flags |= ImGuiInputTextFlags.CtrlEnterForNewLine;

            var notes = player.Notes;
            if (ImGui.InputTextMultiline("###Player_Summary_Notes_Text", ref notes, 2000, new Vector2(-1, -1), flags))
            {
                player.Notes = notes;
                ServiceContext.PlayerDataService.UpdatePlayerNotes(player.Id, notes);
            }
        }

        ImGui.SameLine();

        using (ImRaii.Child("###PlayerBioHistory", new Vector2(-1, areaHeight), false))
        {
            DrawBioHistory(player);
        }
    }

    private static void DrawBioHistory(PlayerView player)
    {
        if (player.BioHistory.Count == 0)
        {
            ImGui.TextUnformatted("No plate bio recorded yet.");
            return;
        }

        for (var i = 0; i < player.BioHistory.Count; i++)
        {
            var entry = player.BioHistory[i];
            if (i > 0)
            {
                ImGuiHelpers.ScaledDummy(2f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(2f);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
                ImGui.TextUnformatted(entry.When);

            ImGui.TextWrapped(entry.Bio);
        }
    }

    private void DrawCategoryTagAssignments(PlayerView player)
    {
        DrawAssignedCategories(player);
        ImGui.SameLine();
        ImGui.SetCursorPosX(CategoryTagSplit);
        DrawAssignedTags(player);
    }

    private void DrawCategoryTagAssignment(PlayerView player)
    {
        CalculateAssignedChildHeight(player);
        DrawCategoryCombo(player);
        ImGui.SameLine();
        ImGui.SetCursorPosX(CategoryTagSplit);
        DrawTagCombo(player);
        ImGuiHelpers.ScaledDummy(1f);
    }

    private void DrawCategoryTagHeadings()
    {
        ImGuiHelpers.ScaledDummy(SectionSpace);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Categories);
        ImGui.SameLine();
        ImGui.SetCursorPosX(CategoryTagSplit);
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.Tags);
    }

    private static void DrawXimButton(PlayerView player)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.SmallButton(FontAwesomeIcon.CommentDots.ToIconString() + "###XimOpen"))
                XIVInstantMessengerProvider.TryOpenMessenger(player.Name, player.HomeWorld);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open in XIVInstantMessenger");
    }

    private static void DrawPlayerSearchButton(PlayerView player)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.SmallButton(FontAwesomeIcon.Search.ToIconString() + "###PlayerSearch"))
                OpenPlayerSearch(player);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Player Search (/search forename)");
    }

    private static void OpenPlayerSearch(PlayerView player)
    {
        try
        {
            var forename = (player.Name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(forename))
                return;
            Utils.SendGameChatCommand($"/search forename \"{forename}\"");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Warning(ex, "[PlayerSearch] Failed to submit /search command.");
        }
    }

    private void CalculateAssignedChildHeight(PlayerView player)
    {
        var itemCount = Math.Max(player.AssignedCategories.Count, player.AssignedTags.Count);
        if (itemCount != 0)
            AssignedChildHeight = (int)Math.Ceiling((double)itemCount / 3) * ImGui.GetTextLineHeightWithSpacing();
        else
            AssignedChildHeight = ImGuiHelpers.GlobalScale * 1f;
    }

    private void DrawCategoryCombo(PlayerView player)
    {
        var categoryNames = player.UnassignedCategories.Select(category => category.Name).Prepend(string.Empty).ToList();
        using var disabled = ImRaii.Disabled(categoryNames.Count == 1);

        if (Helper.Combo("###AddPlayerCategory", ref SelectedCategoryIndex, categoryNames, 160, false))
        {
            if (SelectedCategoryIndex != 0)
            {
                var selectedCategory = ServiceContext.CategoryService.GetCategoryByName(categoryNames[SelectedCategoryIndex]);
                if (selectedCategory != null && player.AssignedCategories.All(category => category.Id != selectedCategory.Id))
                {
                    PlayerCategoryService.AssignCategoryToPlayer(player.Id, selectedCategory.Id);
                    player.AssignedCategories.Add(selectedCategory);
                    player.UnassignedCategories.RemoveAt(SelectedCategoryIndex - 1);
                    SelectedCategoryIndex = 0;
                }
            }
        }
    }

    private void DrawTagCombo(PlayerView player)
    {
        var tagNames = player.UnassignedTags.Select(tag => tag.Name).Prepend(string.Empty).ToList();
        using var disabled = ImRaii.Disabled(tagNames.Count == 1);

        if (Helper.Combo("###AddPlayerTag", ref SelectedTagIndex, tagNames, 160, false))
        {
            if (SelectedTagIndex != 0)
            {
                var selectedTag = ServiceContext.TagService.GetTagByName(tagNames[SelectedTagIndex]);
                if (selectedTag != null && player.AssignedTags.All(tag => tag.Id != selectedTag.Id))
                {
                    PlayerTagService.AssignTag(player.Id, selectedTag.Id);
                    player.AssignedTags.Add(selectedTag);
                    player.UnassignedTags.RemoveAt(SelectedTagIndex - 1);
                    SelectedTagIndex = 0;
                }
            }
        }
    }

    private void DrawAssignedCategories(PlayerView player)
    {
        using var child = ImRaii.Child("AssignedCategories", new Vector2(CategoryTagSplit - ImGui.GetStyle().ItemSpacing.X, AssignedChildHeight), false);
        if (!child.Success)
            return;

        for (var i = 0; i < player.AssignedCategories.Count; i++)
        {
            var category = player.AssignedCategories[i];
            var colorUint = PlayerConfigService.GetCategoryColor(category);
            var color = Sheets.GetUiColorAsVector4(colorUint);
            var fontColor = Sheets.GetLegibleFontColor(color);

            using (ImRaii.PushColor(ImGuiCol.Text, fontColor).Push(ImGuiCol.Button, color)
                         .Push(ImGuiCol.ButtonActive, color).Push(ImGuiCol.ButtonHovered, color))
            {
                if (category.IsDynamicCategory())
                {
                    ImGui.SmallButton(category.Name);
                }
                else
                {
                    if (ImGui.SmallButton($"{category.Name} x"))
                    {
                        PlayerCategoryService.UnassignCategoryFromPlayer(player.Id, category.Id);
                        player.AssignedCategories.RemoveAll(assignedCategory => assignedCategory.Id == category.Id);
                        player.UnassignedCategories.Add(category);
                    }
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(category.IsDynamicCategory() ? Language.UnassignDynamicCategoryTooltip : Language.UnassignCategoryTooltip);

            if ((i + 1) % 3 != 0 && i != player.AssignedCategories.Count - 1)
                ImGui.SameLine();
        }
    }

    private void DrawAssignedTags(PlayerView player)
    {
        using var child = ImRaii.Child("AssignedTags", new Vector2(0, AssignedChildHeight), false);
        if (!child.Success)
            return;

        for (var i = 0; i < player.AssignedTags.Count; i++)
        {
            var tag = player.AssignedTags[i];
            var color = Sheets.GetUiColorAsVector4(tag.Color);
            var fontColor = Sheets.GetLegibleFontColor(color);

            using (ImRaii.PushColor(ImGuiCol.Text, fontColor).Push(ImGuiCol.Button, color)
                         .Push(ImGuiCol.ButtonActive, color).Push(ImGuiCol.ButtonHovered, color))
            {
                if (ImGui.SmallButton($"{tag.Name} x"))
                {
                    PlayerTagService.UnassignTagFromPlayer(player.Id, tag.Id);
                    player.AssignedTags.RemoveAll(assignedTag => assignedTag.Id == tag.Id);
                    player.UnassignedTags.Add(tag);
                }
            }

            if ((i + 1) % 3 != 0 && i != player.AssignedTags.Count - 1)
                ImGui.SameLine();
        }
    }
}
