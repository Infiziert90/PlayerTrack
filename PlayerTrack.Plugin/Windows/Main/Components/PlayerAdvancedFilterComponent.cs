using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;

namespace PlayerTrack.Windows.Main.Components;

/// <summary>
/// Collapsible panel rendered below the main player-list controls that exposes
/// advanced, SQL-like sort and filter options.  Sort state is session-only
/// (not persisted across logins).
/// </summary>
public class PlayerAdvancedFilterComponent : ViewComponent
{
    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------

    // Zone list is rebuilt at most once per this many milliseconds.
    private const long ZoneCacheTtlMs = 60_000L;

    // Display names for each PlayerSortType value, in enum declaration order.
    private static readonly string[] SortTypeLabels =
    {
        "Category (default)",
        "Name (A-Z)",
        "Last Seen",
        "First Seen",
        "Encounter Count",
        "Total Encounter Time",
        "Zone Encounter Time",
    };

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    private readonly IMainPresenter Presenter;

    // Currently selected sort type index (mirrors PlayerSortType ordinal).
    private int _sortTypeIndex;

    // Zone list: (territoryTypeId, displayName) pairs sorted alphabetically.
    private List<(uint Id, string Name)> _zones = new();
    private string[] _zoneNames = Array.Empty<string>();
    private long _zoneCacheBuiltAt;

    // Index into _zones for the currently selected zone (0 = first zone auto-selected).
    private int _selectedZoneIndex;

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public PlayerAdvancedFilterComponent(IMainPresenter presenter)
    {
        Presenter = presenter;

        // Reflect any sort type that may have been set before this component
        // was instantiated (e.g. if the window is closed and reopened mid-session).
        _sortTypeIndex = (int)ServiceContext.PlayerCacheService.GetSortType();
    }

    // ------------------------------------------------------------------
    // Draw
    // ------------------------------------------------------------------

    public override void Draw()
    {
        var sortType = (PlayerSortType)_sortTypeIndex;

        // Refresh zone list if needed and the zone sort is active.
        if (sortType == PlayerSortType.ByZoneEncounterTime)
            EnsureZonesLoaded();

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("###AdvSort_Type", SortTypeLabels[_sortTypeIndex]))
        {
            for (var i = 0; i < SortTypeLabels.Length; i++)
            {
                var isSelected = _sortTypeIndex == i;
                if (ImGui.Selectable(SortTypeLabels[i], isSelected))
                {
                    _sortTypeIndex = i;
                    ApplySortType((PlayerSortType)i);
                }
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        // Zone selector -- only shown when "Zone Encounter Time" is active.
        if (sortType == PlayerSortType.ByZoneEncounterTime)
            DrawZoneSelector();
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private void DrawZoneSelector()
    {
        if (_zones.Count == 0)
        {
            ImGui.TextDisabled("No zone encounter data found.");
            return;
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("###AdvSort_Zone",
            _selectedZoneIndex < _zones.Count ? _zones[_selectedZoneIndex].Name : "---"))
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                var isSelected = _selectedZoneIndex == i;
                if (ImGui.Selectable(_zones[i].Name, isSelected))
                {
                    _selectedZoneIndex = i;
                    ApplySortType(PlayerSortType.ByZoneEncounterTime);
                }
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void EnsureZonesLoaded()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (_zones.Count > 0 && now - _zoneCacheBuiltAt < ZoneCacheTtlMs)
            return;

        RebuildZoneList();
    }

    private void RebuildZoneList()
    {
        var ids = RepositoryContext.EncounterRepository.GetDistinctTerritoryTypeIds();

        var pairs = new List<(uint Id, string Name)>(ids.Count);
        foreach (var id in ids)
        {
            if (!Sheets.Locations.TryGetValue(id, out var loc))
                continue;

            var name = loc.GetName();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Zone #{id}";

            pairs.Add((id, name));
        }

        // Sort alphabetically by display name.
        pairs.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Preserve the previously selected zone ID if it still exists in the new list.
        var previousZoneId = _zones.Count > 0 && _selectedZoneIndex < _zones.Count
            ? _zones[_selectedZoneIndex].Id
            : 0u;

        _zones = pairs;
        _zoneNames = pairs.Select(p => p.Name).ToArray();
        _zoneCacheBuiltAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Restore selection or default to the first entry.
        _selectedZoneIndex = 0;
        if (previousZoneId != 0)
        {
            var idx = _zones.FindIndex(z => z.Id == previousZoneId);
            if (idx >= 0)
                _selectedZoneIndex = idx;
        }

        // Auto-apply the zone so the list updates immediately on first load.
        if (_zones.Count > 0)
            ApplySortType(PlayerSortType.ByZoneEncounterTime);
    }

    private void ApplySortType(PlayerSortType sortType)
    {
        uint zoneId = 0;
        if (sortType == PlayerSortType.ByZoneEncounterTime && _zones.Count > 0)
            zoneId = _zones[_selectedZoneIndex].Id;

        ServiceContext.PlayerCacheService.SetSortType(sortType, zoneId);
        Presenter.ClearCache();
    }
}
