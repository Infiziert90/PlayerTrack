using PlayerTrack.Models.Structs;

namespace PlayerTrack.Models;

public class PlayerConfig
{
    public bool IsChanged;
    public int? PlayerId;
    public int? CategoryId;
    public PlayerConfigType PlayerConfigType;
    public ConfigValue<uint> PlayerListNameColor;
    public ConfigValue<char> PlayerListIcon;
    public ConfigValue<string> NameplateCustomTitle;
    public ConfigValue<bool> NameplateShowInOverworld;
    public ConfigValue<bool> NameplateShowInContent;
    public ConfigValue<bool> NameplateShowInHighEndContent;
    public ConfigValue<uint> NameplateColor;
    public ConfigValue<bool> NameplateUseColor;
    public ConfigValue<bool> NameplateUseColorIfDead;
    public ConfigValue<NameplateTitleType> NameplateTitleType;
    public ConfigValue<bool> AlertNameChange;
    public ConfigValue<bool> AlertWorldTransfer;
    public ConfigValue<bool> AlertProximity;
    public ConfigValue<long> AlertNameChangeFrequency;
    public ConfigValue<long> AlertWorldTransferFrequency;
    public ConfigValue<long> AlertProximityFrequency;
    public ConfigValue<bool> AlertFormatIncludeCategory;
    public ConfigValue<bool> AlertFormatIncludeCustomTitle;
    public ConfigValue<VisibilityType> VisibilityType;

    public PlayerConfig() { }

    public PlayerConfig(PlayerConfigType playerConfigType)
    {
        PlayerConfigType = playerConfigType;
        var inheritOverride = PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Inherit;
        PlayerListNameColor = new ConfigValue<uint>(inheritOverride, 1);
        PlayerListIcon = new ConfigValue<char>(inheritOverride, (char)61447);
        NameplateCustomTitle = new ConfigValue<string>(inheritOverride, string.Empty);
        NameplateShowInOverworld = new ConfigValue<bool>(inheritOverride, true);
        NameplateShowInContent = new ConfigValue<bool>(inheritOverride, true);
        NameplateShowInHighEndContent = new ConfigValue<bool>(inheritOverride, true);
        NameplateColor = new ConfigValue<uint>(inheritOverride, 1);
        NameplateUseColor = new ConfigValue<bool>(inheritOverride, false);
        NameplateUseColorIfDead = new ConfigValue<bool>(inheritOverride, false);
        NameplateTitleType = new ConfigValue<NameplateTitleType>(inheritOverride, Models.NameplateTitleType.NoChange);
        AlertNameChange = new ConfigValue<bool>(inheritOverride, true);
        AlertWorldTransfer = new ConfigValue<bool>(inheritOverride, true);
        AlertProximity = new ConfigValue<bool>(inheritOverride, false);
        AlertNameChangeFrequency = new ConfigValue<long>(inheritOverride, 300000);
        AlertWorldTransferFrequency = new ConfigValue<long>(inheritOverride, 300000);
        AlertProximityFrequency = new ConfigValue<long>(inheritOverride, 14400000);
        AlertFormatIncludeCategory = new ConfigValue<bool>(inheritOverride, true);
        AlertFormatIncludeCustomTitle = new ConfigValue<bool>(inheritOverride, true);
        VisibilityType = new ConfigValue<VisibilityType>(inheritOverride, Models.VisibilityType.None);
    }

    public int Id { get; set; }

    public long Created { get; set; }

    public long Updated { get; set; }
}
