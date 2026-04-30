using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using PlayerTrack.Data;

namespace PlayerTrack;

public static class Sheets
{
    public static readonly ReadOnlyDictionary<uint, WorldData> Worlds;
    public static readonly ReadOnlyDictionary<uint, DCData> DataCenters;
    public static readonly ReadOnlyDictionary<uint, RaceData> Races;
    public static readonly ReadOnlyDictionary<uint, UiColorData> UiColor;
    public static readonly ReadOnlyDictionary<uint, ClassJobData> ClassJobs;
    public static readonly ReadOnlyDictionary<ushort, LocationData> Locations;

    public static readonly ExcelSheet<Race> RaceSheet;
    public static readonly ExcelSheet<Addon> AddonSheet;
    public static readonly ExcelSheet<Tribe> TribeSheet;
    public static readonly ExcelSheet<World> WorldSheet;
    public static readonly ExcelSheet<UIColor> UiColorSheet;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet;
    public static readonly ExcelSheet<TerritoryType> TerritoryTypeSheet;
    public static readonly ExcelSheet<WorldDCGroupType> WorldDcGroupTypeSheet;
    public static readonly ExcelSheet<ContentFinderCondition> ContentFinderSheet;

    static Sheets()
    {
        RaceSheet = Plugin.DataManager.GetExcelSheet<Race>();
        AddonSheet = Plugin.DataManager.GetExcelSheet<Addon>();
        TribeSheet = Plugin.DataManager.GetExcelSheet<Tribe>();
        WorldSheet = Plugin.DataManager.GetExcelSheet<World>();
        UiColorSheet = Plugin.DataManager.GetExcelSheet<UIColor>();
        ClassJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        TerritoryTypeSheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        WorldDcGroupTypeSheet = Plugin.DataManager.GetExcelSheet<WorldDCGroupType>();
        ContentFinderSheet = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();

        Worlds = new(LoadWorlds());
        DataCenters = new(LoadDataCenters());
        Races = new(LoadRaces());
        UiColor = new(LoadUiColors());
        Locations = new(LoadLocations());
        ClassJobs = new(LoadClassJobs());
    }

    /// <summary>
    /// Validates if the world id is a valid world.
    /// </summary>
    /// <param name="worldId">world id.</param>
    /// <returns>indicator whether world is valid.</returns>
    public static bool IsValidWorld(uint worldId) => Worlds.ContainsKey(worldId);

    /// <summary>
    /// Get indicator whether world is a test data center.
    /// </summary>
    /// <param name="worldId">world id.</param>
    /// <returns>indicator whether world is a test data center.</returns>
    public static bool IsTestDc(uint worldId)
    {
        var world = WorldSheet.GetRowOrDefault(worldId);
        if (world == null)
            return false;

        var region = WorldDcGroupTypeSheet.GetRowOrDefault(world.Value.DataCenter.RowId)?.Region.RowId ?? 0;
        return region == 7;
    }

    /// <summary>
    /// Gets the world id by name.
    /// </summary>
    /// <param name="worldName">world name.</param>
    /// <returns>world id.</returns>
    public static uint GetWorldIdByName(string worldName)
    {
        foreach (var world in Worlds)
            if (world.Value.Name.Equals(worldName, StringComparison.OrdinalIgnoreCase))
                return world.Key;

        return 0;
    }

    /// <summary>
    /// Gets the data center name by world id.
    /// </summary>
    /// <param name="id">world id.</param>
    /// <returns>data center name.</returns>
    public static string GetDataCenterNameByWorldId(uint id)
    {
        if (Worlds.TryGetValue(id, out var world))
        {
            return DataCenters.TryGetValue(world.DataCenterId, out var dataCenter) ? dataCenter.Name : "Etheirys";
        }

        return "Etheirys";
    }

    /// <summary>
    /// Gets the world name by id.
    /// </summary>
    /// <param name="id">world id.</param>
    /// <returns>world name.</returns>
    public static string GetWorldNameById(uint id) => Worlds.TryGetValue(id, out var world) ? world.Name : "Etheirys";

    private static Dictionary<uint, WorldData> LoadWorlds()
    {
        var luminaWorlds = WorldSheet.Where(x => x.DataCenter.ValueNullable != null &&
                                         (x.DataCenter.ValueNullable?.Region.RowId ?? 0) != 0 &&
                                         !string.IsNullOrWhiteSpace(x.DataCenter.ValueNullable?.Name.ExtractText()) &&
                                         !string.IsNullOrWhiteSpace(x.Name.ExtractText()) &&
                                         !string.IsNullOrWhiteSpace(x.InternalName.ExtractText()) &&
                                         !x.Name.ExtractText().Contains('-') &&
                                         !x.Name.ExtractText().Contains('_'))
                             .Where(x => x.DataCenter.Value.Region.RowId != 5 ||
                                         (x.RowId > 1000 && x.RowId != 1200 &&
                                          IsCJKString(x.Name.ExtractText())));

        return luminaWorlds.ToDictionary(
            luminaWorld => luminaWorld.RowId,
            luminaWorld => new WorldData { Id = luminaWorld.RowId, Name = Utils.Sanitize(luminaWorld.Name), DataCenterId = luminaWorld.DataCenter.RowId });
    }

    /// <summary>
    /// Checks if a string contains CJK characters.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the string contains CJK characters.</returns>
    private static bool IsCJKString(string text) => text.All(IsCJKCharacter);

    /// <summary>
    /// Checks if a character is a CJK character.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>True if the character is a CJK character.</returns>
    private static bool IsCJKCharacter(char c) => (c >= 0x4E00 && c <= 0x9FFF);

    /// <summary>
    /// Finds the closest color from the UIColors to the input color by foreground.
    /// </summary>
    /// <param name="inputColor">The input color to find the closest match for.</param>
    /// <param name="usedColorIds">Used colors to exclude.</param>
    /// <returns>The closest matching color from the UIColor foregrounds.</returns>
    public static UiColorData FindClosestUiColor(Vector4 inputColor, HashSet<uint>? usedColorIds = null)
    {
        UiColorData? closestColor = null;
        var minDistance = float.MaxValue;

        foreach (var colorPair in UiColor)
        {
            if (usedColorIds != null && usedColorIds.Contains(colorPair.Key))
            {
                continue;
            }

            var color = GetUiColorAsVector4(colorPair.Key);
            var distance = Utils.ColorDistance(inputColor, color);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = colorPair.Value;
            }
        }

        return closestColor ?? UiColor.First().Value;
    }

    /// <summary>
    /// Gets the UI color as a vector4.
    /// </summary>
    /// <param name="colorId">color id.</param>
    /// <returns>color as vector4.</returns>
    public static Vector4 GetUiColorAsVector4(uint colorId)
    {
        var uiColor = UiColor.TryGetValue(colorId, out var color) ? color : new UiColorData();
        return Utils.UiColorToVector4(uiColor.Foreground);
    }

    /// <summary>
    /// Returns a Vector4 representing a legible font color that contrasts with the given background color.
    /// </summary>
    /// <param name="backgroundColor">
    /// The background color as a Vector4 (in RGBA format) against which the font color needs to be legible.
    /// </param>
    /// <returns>
    /// A Vector4 representing a legible font color (either black or white) that contrasts with the provided
    /// background color.
    /// </returns>
    public static Vector4 GetLegibleFontColor(Vector4 backgroundColor)
    {
        var luminance = 0.299f * backgroundColor.X + 0.587f * backgroundColor.Y + 0.114f * backgroundColor.Z;

        if (luminance > 0.5f)
            return new Vector4(0, 0, 0, 1);

        return new Vector4(1, 1, 1, 1);
    }

    /// <summary>
    /// Gets a random UI color.
    /// </summary>
    /// <returns>A ToadUIColor object representing a random UI color.</returns>
    public static UiColorData GetRandomUiColor()
    {
        var randomIndex = new Random().Next(0, UiColor.Count);
        var randomColorId = UiColor.Keys.ElementAt(randomIndex);
        return UiColor[randomColorId];
    }

    private static Dictionary<uint, DCData> LoadDataCenters()
    {
        var luminaDataCenters = WorldDcGroupTypeSheet.Where(dataCenter => !dataCenter.Name.IsEmpty && dataCenter.Region.RowId != 0 && dataCenter.Region.RowId != 7);

        return luminaDataCenters.ToDictionary(
            luminaDataCenter => luminaDataCenter.RowId,
            luminaDataCenter => new DCData() { Id = luminaDataCenter.RowId, Name = Utils.Sanitize(luminaDataCenter.Name) });
    }

    private static Dictionary<uint, ClassJobData> LoadClassJobs()
    {
        return ClassJobSheet.ToDictionary(
            luminaClassJob => luminaClassJob.RowId,
            luminaClassJob => new ClassJobData
            {
                Id = luminaClassJob.RowId,
                Name = Utils.Sanitize(luminaClassJob.Name),
                Code = Utils.Sanitize(luminaClassJob.Abbreviation),
            });
    }

    private static Dictionary<uint, RaceData> LoadRaces()
    {
        return RaceSheet.ToDictionary(
            luminaRace => luminaRace.RowId,
            luminaRace => new RaceData
            {
                Id = luminaRace.RowId,
                MasculineName = Utils.Sanitize(luminaRace.Masculine),
                FeminineName = Utils.Sanitize(luminaRace.Feminine),
            });
    }

    private static Dictionary<uint, TribeData> LoadTribes()
    {
        return TribeSheet.ToDictionary(
            luminaTribe => luminaTribe.RowId,
            luminaTribe => new TribeData
            {
                Id = luminaTribe.RowId,
                MasculineName = Utils.Sanitize(luminaTribe.Masculine),
                FeminineName = Utils.Sanitize(luminaTribe.Feminine),
            });
    }

    private static Dictionary<uint, UiColorData> LoadUiColors()
    {
        return UiColorSheet.ToDictionary(
            uiColor => uiColor.RowId,
            uiColor => new UiColorData { Id = uiColor.RowId, Foreground = uiColor.Dark, Glow = uiColor.Light });
    }

    private static Dictionary<ushort, LocationData> LoadLocations()
    {
        return TerritoryTypeSheet.ToDictionary(
            territoryTypeSheetItem => (ushort)territoryTypeSheetItem.RowId,
            territoryTypeSheetItem =>
            {
                var location = new LocationData { TerritoryId = (ushort)territoryTypeSheetItem.RowId };
                location.TerritoryName = TerritoryTypeSheet.GetRowOrDefault(location.TerritoryId)?.PlaceName.Value.Name.ExtractText() ?? string.Empty;
                var cfcFound = ContentFinderSheet.TryGetFirst(c => c.TerritoryType.RowId == location.TerritoryId, out var cfc);
                if (cfcFound && cfc.RowId != 0)
                {
                    location.ContentId = cfc.RowId;
                    location.ContentName = Plugin.PluginInterface.Sanitizer.Sanitize(cfc.Name.ExtractText());
                    location.LocationType = cfc.HighEndDuty ? LocationType.HighEndContent : LocationType.Content;
                }
                else
                {
                    location.LocationType = LocationType.Overworld;
                }

                location.TerritoryName = Plugin.PluginInterface.Sanitizer.Sanitize(location.TerritoryName);
                return location;
            });
    }
}
