using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dapper.Contrib.Extensions;
using PlayerTrack.Models.Structs;
using PlayerTrack.Resource;

namespace PlayerTrack.Models;

using System.Linq;

public class Player : IComparable<Player>, IEquatable<Player>
{
    public int Id { get; set; }

    public long Created { get; set; }

    public long Updated { get; set; }

    public long LastAlertSent { get; set; }

    public long LastNameChangeAlertSent { get; set; }

    public long LastWorldChangeAlertSent { get; set; }

    public long FirstSeen { get; set; }

    public long LastSeen { get; set; }

    public byte[]? Customize { get; set; }

    public int SeenCount { get; set; }

    public LodestoneStatus LodestoneStatus { get; set; } = LodestoneStatus.Unverified;

    public KeyValuePair<FreeCompanyState, string> FreeCompany { get; set; } = new(FreeCompanyState.Unknown, string.Empty);

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public uint LodestoneId { get; set; }

    public long LodestoneVerifiedOn { get; set; }

    public uint EntityId { get; set; }

    public uint WorldId { get; set; }

    public uint LastTerritoryType { get; set; }

    public ulong ContentId { get; set; }

    [Write(false)]
    public int PrimaryCategoryId { get; set; }

    [Write(false)]
    public bool IsCurrent { get; set; }

    [Write(false)]
    public bool IsRecent { get; set; }

    [Write(false)]
    public int OpenPlayerEncounterId { get; set; }

    [Write(false)]
    public string[] PreviousNames { get; set; } = [];

    [Write(false)]
    public string[] PreviousWorlds { get; set; } = [];

    [Write(false)]
    public PlayerConfig PlayerConfig { get; set; } = new(PlayerConfigType.Player);

    [Write(false)]
    public List<Tag> AssignedTags { get; set; } = [];

    [Write(false)]
    public List<Category> AssignedCategories { get; set; } = [];

    [Write(false)]
    public Vector4 PlayerListNameColor { get; set; } = ImGuiColors.DalamudWhite;

    [Write(false)]
    public string PlayerListIconString { get; set; } = FontAwesomeIcon.User.ToIconString();

    public override int GetHashCode() => Key.GetHashCode();

    public int CompareTo(Player? other)
    {
        if (other is null)
            return -1;

        return string.Compare(Name, other.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    public bool Equals(Player? other)
    {
        if (other is null)
            return false;

        return GetHashCode() == other.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Player player)
            return false;

        return GetHashCode() == player.GetHashCode();
    }

    public List<PlayerConfig> GetCategoryPlayerConfigs() => AssignedCategories.OrderBy(cat => cat.Rank).Select(cat => cat.PlayerConfig).ToList();

    public string WorldName()
    {
        return Sheets.GetWorldNameById(WorldId);
    }

    public string DataCenterName()
    {
        return Sheets.GetDataCenterNameByWorldId(WorldId);
    }

    public string FullyQualifiedName()
    {
        return $"{Name}@{WorldName()} (#{Id})";
    }

    public string RaceName()
    {
        var customizeArr = Customize;
        if (customizeArr is { Length: > 0 })
        {
            var customize = CharaCustomizeData.MapCustomizeData(customizeArr);
            var gender = customize.Gender;
            return gender switch
            {
                0 => Sheets.Races[customize.Race].MasculineName,
                1 => Sheets.Races[customize.Race].FeminineName,
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    public string GenderName()
    {
        var customizeArr = Customize;
        if (customizeArr is { Length: > 0 })
        {
            var customize = CharaCustomizeData.MapCustomizeData(customizeArr);
            var gender = customize.Gender;
            return gender switch
            {
                0 => Language.Male,
                1 => Language.Female,
                _ => string.Empty
            };
        }

        return string.Empty;
    }


    public void Merge(Player player)
    {
        // combine
        SeenCount += player.SeenCount;
        Notes = string.IsNullOrEmpty(Notes)
            ? player.Notes
            : (string.IsNullOrEmpty(player.Notes)
                ? Notes
                : Notes + " | " + player.Notes);

        // true if either is true
        IsCurrent = player.IsCurrent || IsCurrent;
        IsRecent = player.IsRecent || IsRecent;

        // use from latest player if set
        if (player.LastSeen > LastSeen)
        {
            Name = player.Name;
            WorldId = player.WorldId;
            Key = string.Concat(Name.Replace(' ', '_').ToUpperInvariant(), "_", WorldId);
            Customize = player.Customize?.Length > 0 ? player.Customize : Customize;
            LastTerritoryType = player.LastTerritoryType != 0 ? player.LastTerritoryType : LastTerritoryType;
            FreeCompany = player.FreeCompany.Key != FreeCompanyState.Unknown ? player.FreeCompany : FreeCompany;
            EntityId = player.EntityId != 0 ? player.EntityId : EntityId;
        }

        // use the most recent timestamp
        LastAlertSent = Math.Max(LastAlertSent, player.LastAlertSent);
        LastNameChangeAlertSent = Math.Max(LastNameChangeAlertSent, player.LastNameChangeAlertSent);
        LastWorldChangeAlertSent = Math.Max(LastWorldChangeAlertSent, player.LastWorldChangeAlertSent);
        LastSeen = Math.Max(LastSeen, player.LastSeen);

        // use the oldest timestamp
        Created = Math.Min(Created, player.Created);

        // use first seen if set
        if (FirstSeen == 0)
            FirstSeen = player.FirstSeen;
        else if (player.FirstSeen != 0)
            FirstSeen = Math.Min(FirstSeen, player.FirstSeen);

        // set fields if not set
        if (ContentId == 0)
        {
            ContentId = player.ContentId;
        }
        if (LodestoneStatus != LodestoneStatus.Verified)
        {
            LodestoneId = player.LodestoneId;
            LodestoneStatus = player.LodestoneStatus;
            LodestoneVerifiedOn = player.LodestoneVerifiedOn;
        }
    }
}
