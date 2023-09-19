﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.DrunkenToad.Core;
using Dalamud.DrunkenToad.Gui;
using Dalamud.DrunkenToad.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Loc.ImGui;
using ImGuiNET;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.UserInterface.Components;
using PlayerTrack.UserInterface.Main.Presenters;

namespace PlayerTrack.UserInterface.Main.Components;

using System;
using ViewModels;

public class PlayerSummaryComponent : ViewComponent
{
    private const float SectionSpace = 2.8f;
    private readonly IMainPresenter presenter;
    private readonly List<string> overrideLangCodes = new() { "fr", "it", "es" };
    private readonly float[] defaultPlayerOffsets = { 100f, 260f, 360f };
    private readonly float[] alternatePlayerOffsets = { 120f, 280f, 450f };
    private float[] currentOffsets = null!;
    private int selectedTagIndex;
    private int selectedCategoryIndex;
    private float assignedChildHeight;

    public PlayerSummaryComponent(IMainPresenter presenter) => this.presenter = presenter;

    public override void Draw()
    {
        var player = this.presenter.GetSelectedPlayer();
        if (player == null)
        {
            return;
        }

        this.currentOffsets = this.GetOffsets();
        ImGui.BeginChild("###PlayerSummaryPlayer", new Vector2(-1, 0), false);
        this.DrawInfoStatHeadings();
        this.DrawName(player);
        this.DrawFirstSeen(player);
        this.DrawHomeworld(player);
        this.DrawLastSeen(player);
        this.DrawFreeCompany(player);
        this.DrawLastLocation(player);
        this.DrawLodestone(player);
        this.DrawSeenCount(player);
        this.DrawAppearance(player);
        this.DrawCategoryTagHeadings();
        this.DrawCategoryTagAssignment(player);
        this.DrawCategoryTagAssignments(player);
        this.DrawNotes(player);
        ImGui.EndChild();
    }

    private void DrawNotes(PlayerView player)
    {
        LocGui.TextColored("Notes", ImGuiColors.DalamudViolet);
        var notes = player.Notes;
        if (ImGui.InputTextMultiline(
                "###Player_Summary_Notes_Text",
                ref notes,
                2000,
                new Vector2(
                    x: ImGui.GetWindowSize().X - 5f * ImGuiHelpers.GlobalScale,
                    y: -1 - 5f * ImGuiHelpers.GlobalScale)))
        {
            player.Notes = notes;
            ServiceContext.PlayerDataService.UpdatePlayer(player.Id);
        }
    }

    private void DrawCategoryTagAssignments(PlayerView player)
    {
        this.DrawAssignedCategories(player);
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        this.DrawAssignedTags(player);
    }

    private void DrawCategoryTagAssignment(PlayerView player)
    {
        this.CalculateAssignedChildHeight(player);
        this.DrawCategoryCombo(player);
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        this.DrawTagCombo(player);
        ImGuiHelpers.ScaledDummy(1f);
    }

    private void DrawCategoryTagHeadings()
    {
        ImGuiHelpers.ScaledDummy(SectionSpace);
        LocGui.TextColored("Categories", ImGuiColors.DalamudViolet);
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        LocGui.TextColored("Tags", ImGuiColors.DalamudViolet);
    }

    private void DrawAppearance(PlayerView player)
    {
        LocGui.Text("Appearance");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[0]);
        ImGui.Text(player.Appearance);
    }

    private void DrawSeenCount(PlayerView player)
    {
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        LocGui.Text("SeenCount");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[2]);
        LocGui.Text(player.SeenCount);
    }

    private void DrawLodestone(PlayerView player)
    {
        LocGui.Text("Lodestone");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[0]);
        if (player.LodestoneStatus != LodestoneStatus.Failed && player.LodestoneStatus != LodestoneStatus.Banned)
        {
            LocGui.TextColored(player.Lodestone, player.LodestoneColor);
            if (player.LodestoneStatus == LodestoneStatus.Verified)
            {
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (ImGui.IsItemClicked())
                {
                    PlayerLodestoneService.OpenLodestoneProfile(player.LodestoneId);
                }
            }
        }
        else
        {
            ImGui.BeginGroup();
            LocGui.TextColored(player.Lodestone, player.LodestoneColor);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(ImGuiColors.DPSRed, FontAwesomeIcon.Redo.ToIconString());
            ImGui.PopFont();
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (ImGui.IsItemClicked())
            {
                player.LodestoneStatus = LodestoneStatus.Unverified;
                player.Lodestone = ServiceContext.Localization.GetString(player.LodestoneStatus.ToString());
                player.LodestoneColor = ImGuiColors.DalamudWhite;
                PlayerLodestoneService.ResetLodestone(player.Id);
            }
        }
    }

    private void DrawLastLocation(PlayerView player)
    {
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        LocGui.Text("LastLocation");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[2]);
        LocGui.Text(player.LastLocation);
    }

    private void DrawFreeCompany(PlayerView player)
    {
        LocGui.Text("FreeCompany");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[0]);
        LocGui.Text(player.FreeCompany);
    }

    private void DrawLastSeen(PlayerView player)
    {
        LocGui.Text("LastSeen");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[2]);
        LocGui.Text(player.LastSeen);
    }

    private void DrawHomeworld(PlayerView player)
    {
        LocGui.Text("Homeworld");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[0]);
        if (!string.IsNullOrEmpty(player.PreviousWorlds))
        {
            ImGui.BeginGroup();
            LocGui.Text(player.HomeWorld);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            LocGui.TextColored(FontAwesomeIcon.InfoCircle.ToIconString(), ImGuiColors.DalamudYellow);
            ImGui.PopFont();
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(string.Format(ServiceContext.Localization.GetString("PreviouslyOn"), player.PreviousWorlds));
            }
        }
        else
        {
            LocGui.Text(player.HomeWorld);
        }

        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
    }

    private void DrawFirstSeen(PlayerView player)
    {
        LocGui.Text("FirstSeen");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[2]);
        ImGui.Text(player.FirstSeen);
    }

    private void DrawName(PlayerView player)
    {
        LocGui.Text("Name");
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[0]);
        if (!string.IsNullOrEmpty(player.PreviousNames))
        {
            ImGui.BeginGroup();
            LocGui.Text(player.Name);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            LocGui.TextColored(FontAwesomeIcon.InfoCircle.ToIconString(), ImGuiColors.DalamudYellow);
            ImGui.PopFont();
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(string.Format(ServiceContext.Localization.GetString("PreviouslyKnownAs"), player.PreviousNames));
            }
        }
        else
        {
            LocGui.Text(player.Name);
        }

        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
    }

    private void DrawInfoStatHeadings()
    {
        LocGui.TextColored("PlayerInfo", ImGuiColors.DalamudViolet);
        ImGuiHelpers.ScaledRelativeSameLine(this.currentOffsets[1]);
        LocGui.TextColored("PlayerStats", ImGuiColors.DalamudViolet);
    }

    private void CalculateAssignedChildHeight(PlayerView player)
    {
        var itemCount = Math.Max(player.AssignedCategories.Count, player.AssignedTags.Count);
        if (itemCount != 0)
        {
            var lines = (int)Math.Ceiling((double)itemCount / 3);
            this.assignedChildHeight = lines * ImGui.GetTextLineHeightWithSpacing();
        }
        else
        {
            this.assignedChildHeight = ImGuiHelpers.GlobalScale * 1f;
        }
    }

    private void DrawCategoryCombo(PlayerView player)
    {
        var categoryNames = player.UnassignedCategories.Select(category => category.Name).ToList();
        categoryNames.Insert(0, string.Empty);
        var disableCategoryBox = categoryNames.Count == 1;
        if (disableCategoryBox)
        {
            ImGui.BeginDisabled();
        }

        if (ToadGui.Combo("###AddPlayerCategory", ref this.selectedCategoryIndex, categoryNames, 160, false))
        {
            if (this.selectedCategoryIndex != 0)
            {
                var selectedCategory = ServiceContext.CategoryService.GetCategoryByName(categoryNames[this.selectedCategoryIndex]);
                if (selectedCategory != null && player.AssignedCategories.All(category => category.Id != selectedCategory.Id))
                {
                    PlayerCategoryService.AssignCategoryToPlayer(player.Id, selectedCategory.Id);
                    player.AssignedCategories.Add(selectedCategory);
                    player.UnassignedCategories.RemoveAt(this.selectedCategoryIndex - 1);
                    this.selectedCategoryIndex = 0;
                }
            }
        }

        if (disableCategoryBox)
        {
            ImGui.EndDisabled();
        }
    }

    private void DrawTagCombo(PlayerView player)
    {
        var tagNames = player.UnassignedTags.Select(tag => tag.Name).ToList();
        tagNames.Insert(0, string.Empty);
        var disableTagBox = tagNames.Count == 1;
        if (disableTagBox)
        {
            ImGui.BeginDisabled();
        }

        if (ToadGui.Combo("###AddPlayerTag", ref this.selectedTagIndex, tagNames, 160, false))
        {
            if (this.selectedTagIndex != 0)
            {
                var selectedTag = ServiceContext.TagService.GetTagByName(tagNames[this.selectedTagIndex]);
                if (selectedTag != null && player.AssignedTags.All(tag => tag.Id != selectedTag.Id))
                {
                    PlayerTagService.AssignTag(player.Id, selectedTag.Id);
                    player.AssignedTags.Add(selectedTag);
                    player.UnassignedTags.RemoveAt(this.selectedTagIndex - 1);
                    this.selectedTagIndex = 0;
                }
            }
        }

        if (disableTagBox)
        {
            ImGui.EndDisabled();
        }
    }

    private void DrawAssignedCategories(PlayerView player)
    {
        ImGui.BeginChild("AssignedCategories", new Vector2(0, this.assignedChildHeight), false);
        for (var i = 0; i < player.AssignedCategories.Count; i++)
        {
            var category = player.AssignedCategories[i];
            var colorUint = PlayerConfigService.GetCategoryColor(category);
            var color = DalamudContext.DataManager.GetUIColorAsVector4(colorUint);
            var fontColor = ImGuiUtil.GetLegibleFontColor(color);

            ImGui.PushStyleColor(ImGuiCol.Text, fontColor);
            ImGui.PushStyleColor(ImGuiCol.Button, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);

            if (ImGui.SmallButton($"{category.Name} x"))
            {
                PlayerCategoryService.UnassignCategoryFromPlayer(player.Id, category.Id);
                player.AssignedCategories.RemoveAll(assignedCategory => assignedCategory.Id == category.Id);
                player.UnassignedCategories.Add(category);
            }

            ImGui.PopStyleColor(4);

            if (ImGui.IsItemHovered())
            {
                LocGui.SetHoverTooltip("UnassignCategoryTooltip");
            }

            if ((i + 1) % 3 != 0 && i != player.AssignedCategories.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        ImGui.EndChild();
    }

    private void DrawAssignedTags(PlayerView player)
    {
        ImGui.BeginChild("AssignedTags", new Vector2(0, this.assignedChildHeight), false);
        for (var i = 0; i < player.AssignedTags.Count; i++)
        {
            var tag = player.AssignedTags[i];
            var color = DalamudContext.DataManager.GetUIColorAsVector4(tag.Color);
            var fontColor = ImGuiUtil.GetLegibleFontColor(color);
            ImGui.PushStyleColor(ImGuiCol.Text, fontColor);
            ImGui.PushStyleColor(ImGuiCol.Button, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);

            if (ImGui.SmallButton($"{tag.Name} x"))
            {
                PlayerTagService.RemoveTag(player.Id, tag.Id);
                player.AssignedTags.RemoveAll(assignedTag => assignedTag.Id == tag.Id);
                player.UnassignedTags.Add(tag);
            }

            ImGui.PopStyleColor(4);

            if ((i + 1) % 3 != 0 && i != player.AssignedTags.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        ImGui.EndChild();
    }

    private float[] GetOffsets()
    {
        if (this.overrideLangCodes.Contains(DalamudContext.PluginInterface.UiLanguage))
        {
            return this.alternatePlayerOffsets;
        }

        return this.defaultPlayerOffsets;
    }
}
