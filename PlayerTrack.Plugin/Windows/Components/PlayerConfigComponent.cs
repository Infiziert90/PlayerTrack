using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;
using PlayerTrack.Resource;
using PlayerTrack.Windows.ViewModels;

namespace PlayerTrack.Windows.Components;

public static class PlayerConfigComponent
{
    private static char[] EnabledIconCodes = null!;
    private static string[] EnabledIconNames = null!;

    public static string[] InheritOrOverride { get; } =
    {
        InheritOverride.Inherit.ToString(),
        InheritOverride.Override.ToString(),
    };

    public static PlayerConfig DrawPlayerConfigTabs(PlayerView player)
    {
        PrepareSettings();

        var playerConfigSet = new PlayerConfigSet
        {
            PlayerConfigType = PlayerConfigType.Player,
            CurrentPlayerConfig = player.PlayerConfig,
            CategoryPlayerConfigs = [],
        };

        foreach (var category in player.AssignedCategories)
            playerConfigSet.CategoryPlayerConfigs.Add(category.PlayerConfig);

        return DrawConfigTabs(playerConfigSet);
    }

    public static PlayerConfig DrawCategoryConfigTabs(Category category)
    {
        PrepareSettings();
        var playerConfigSet = new PlayerConfigSet
        {
            PlayerConfigType = PlayerConfigType.Category,
            CurrentPlayerConfig = category.PlayerConfig,
        };

        return DrawConfigTabs(playerConfigSet);
    }

    public static PlayerConfig DrawDefaultConfigTabs()
    {
        PrepareSettings();
        var playerConfigSet = new PlayerConfigSet
        {
            PlayerConfigType = PlayerConfigType.Default,
            CurrentPlayerConfig = ServiceContext.ConfigService.GetConfig().PlayerConfig,
        };

        return DrawConfigTabs(playerConfigSet);
    }

    private static void PrepareSettings()
    {
        var enabledIcons = ServiceContext.ConfigService.GetConfig().Icons;
        EnabledIconCodes = enabledIcons.Select(icon => icon.ToIconChar()).ToArray();
        EnabledIconNames = enabledIcons.Select(icon => icon.ToString()).ToArray();
    }

    private static PlayerConfig DrawConfigTabs(PlayerConfigSet playerConfigSet)
    {
        using (var tabItem = ImRaii.TabItem(Language.Display))
        {
            if (tabItem.Success)
            {
                ImGuiHelpers.ScaledDummy(2f);

                DrawColorPicker(Language.NameColor, playerConfigSet, pc => pc.PlayerListNameColor,
                                ref playerConfigSet.CurrentPlayerConfig.PlayerListNameColor,
                                ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawIconPicker(Language.Icon, playerConfigSet, pc => pc.PlayerListIcon,
                               ref playerConfigSet.CurrentPlayerConfig.PlayerListIcon,
                               ref playerConfigSet.CurrentPlayerConfig.IsChanged);
            }
        }

        using (var tabItem = ImRaii.TabItem(Language.Nameplate))
        {
            if (tabItem.Success)
            {
                ImGuiHelpers.ScaledDummy(1f);

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.NameplateColors);
                DrawCheckbox(Language.NameplateUseColor, playerConfigSet, pc => pc.NameplateUseColor,
                             ref playerConfigSet.CurrentPlayerConfig.NameplateUseColor,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawCheckbox(Language.NameplateUseColorIfDead, playerConfigSet, pc => pc.NameplateUseColorIfDead,
                             ref playerConfigSet.CurrentPlayerConfig.NameplateUseColorIfDead,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);

                if (playerConfigSet.CurrentPlayerConfig.NameplateUseColor.Value)
                {
                    DrawColorPicker(Language.NameplateColor, playerConfigSet, pc => pc.NameplateColor,
                                    ref playerConfigSet.CurrentPlayerConfig.NameplateColor,
                                    ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                }

                ImGuiHelpers.ScaledDummy(3f);

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.NameplateTitle);
                DrawCombo(Language.NameplateTitle, playerConfigSet, pc => pc.NameplateTitleType,
                          ref playerConfigSet.CurrentPlayerConfig.NameplateTitleType,
                          ref playerConfigSet.CurrentPlayerConfig.IsChanged, true);
                if (playerConfigSet.CurrentPlayerConfig.NameplateTitleType.Value == NameplateTitleType.CustomTitle)
                {
                    DrawTextConfig(Language.CustomTitle, playerConfigSet, pc => pc.NameplateCustomTitle,
                                   ref playerConfigSet.CurrentPlayerConfig.NameplateCustomTitle,
                                   ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                }

                ImGuiHelpers.ScaledDummy(3f);

                Helper.TextColored(ImGuiColors.DalamudViolet, Language.NameplateConditions);
                DrawCheckbox(Language.ShowInOverworld, playerConfigSet, pc => pc.NameplateShowInOverworld,
                             ref playerConfigSet.CurrentPlayerConfig.NameplateShowInOverworld,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawCheckbox(Language.ShowInContent, playerConfigSet, pc => pc.NameplateShowInContent,
                             ref playerConfigSet.CurrentPlayerConfig.NameplateShowInContent,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawCheckbox(Language.ShowInHighEndContent, playerConfigSet, pc => pc.NameplateShowInHighEndContent,
                             ref playerConfigSet.CurrentPlayerConfig.NameplateShowInHighEndContent,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);

                ImGuiHelpers.ScaledDummy(3f);
            }
        }

        using (var tabItem = ImRaii.TabItem(Language.Alerts))
        {
            if (tabItem.Success)
            {
                ImGuiHelpers.ScaledDummy(2f);

                DrawCheckbox(Language.NameChangeAlert, playerConfigSet, pc => pc.AlertNameChange,
                             ref playerConfigSet.CurrentPlayerConfig.AlertNameChange,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawMinutesInput("Name Change Cooldown (min)", playerConfigSet, pc => pc.AlertNameChangeFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.AlertNameChangeFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawCheckbox(Language.WorldTransferAlert, playerConfigSet, pc => pc.AlertWorldTransfer,
                             ref playerConfigSet.CurrentPlayerConfig.AlertWorldTransfer,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawMinutesInput("World Transfer Cooldown (min)", playerConfigSet, pc => pc.AlertWorldTransferFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.AlertWorldTransferFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawCheckbox(Language.ProximityAlert, playerConfigSet, pc => pc.AlertProximity,
                             ref playerConfigSet.CurrentPlayerConfig.AlertProximity,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
                DrawMinutesInput("Proximity Cooldown (min)", playerConfigSet, pc => pc.AlertProximityFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.AlertProximityFrequency,
                             ref playerConfigSet.CurrentPlayerConfig.IsChanged);
            }
        }

        if (playerConfigSet.PlayerConfigType != PlayerConfigType.Default)
        {
            using (var tabItem = ImRaii.TabItem(Language.Integrations))
            {
                if (tabItem.Success)
                {
                    ImGuiHelpers.ScaledDummy(2f);

                    DrawCombo(Language.VisibilityType, playerConfigSet, pc => pc.VisibilityType,
                              ref playerConfigSet.CurrentPlayerConfig.VisibilityType,
                              ref playerConfigSet.CurrentPlayerConfig.IsChanged, false);
                }
            }
        }

        return playerConfigSet.CurrentPlayerConfig;
    }

    private static void DrawCheckbox<T>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector,
        ref ConfigValue<bool> config,
        ref bool isChanged)
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        if (extractedProperty.PropertyValue is not bool boolValue)
            return;

        DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
        DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);

        if (Helper.Checkbox(key, ref boolValue))
        {
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
            config.Value = boolValue;
            isChanged = true;
        }
    }

    private static void DrawMinutesInput<T>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector,
        ref ConfigValue<long> config,
        ref bool isChanged)
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        if (extractedProperty.PropertyValue is not long longValue)
            return;

        DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
        DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);

        var minutes = (int)(longValue / 60000L);
        ImGui.SetNextItemWidth(Helper.CalcScaledComboWidth(120f));
        if (ImGui.InputInt(key, ref minutes))
        {
            if (minutes < 0)
                minutes = 0;
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
            config.Value = (long)minutes * 60000L;
            isChanged = true;
        }
    }

    private static void DrawTextConfig<T>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector,
        ref ConfigValue<string> config,
        ref bool isChanged)
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        if (extractedProperty.PropertyValue is not string stringValue)
            return;

        DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
        DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);

        ImGui.SetNextItemWidth(Helper.CalcScaledComboWidth(150f));
        if (ImGui.InputText(key, ref stringValue, 150))
        {
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
            config.Value = stringValue;
            isChanged = true;
        }
    }

    private static void DrawColorPicker<T>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector,
        ref ConfigValue<uint> config,
        ref bool isChanged)
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        if (extractedProperty.PropertyValue is not uint uintValue)
            return;

        DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
        DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);

        var color = Sheets.GetUiColorAsVector4(uintValue);
        if (Helper.SimpleUiColorPicker(key, uintValue, ref color))
        {
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
            config.Value = Sheets.FindClosestUiColor(color).Id;
            isChanged = true;
        }
    }

    private static void DrawIconPicker<T>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector,
        ref ConfigValue<char> config,
        ref bool isChanged)
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        if (extractedProperty.PropertyValue is not char charValue)
            return;

        DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
        DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);

        if (Helper.IconPicker(key, ref charValue, EnabledIconCodes, EnabledIconNames))
        {
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
            config.Value = charValue;
            isChanged = true;
        }
    }

    private static void DrawCombo<TEnum>(
        string key,
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<TEnum>> propertySelector,
        ref ConfigValue<TEnum> config,
        ref bool isChanged,
        bool isAvailableForDefault) where TEnum : Enum
    {
        var extractedProperty = PlayerConfigService.ExtractProperty(playerConfigSet, propertySelector);
        object displayValue = config.InheritOverride == InheritOverride.Inherit ? extractedProperty.PropertyValue : config.Value;
        if (!isAvailableForDefault && extractedProperty.PlayerConfigType == PlayerConfigType.Default)
            extractedProperty.PlayerConfigType = PlayerConfigType.Category;

        if (displayValue is not TEnum enumValue)
        {
            Plugin.PluginLog.Error($"Failed to cast {displayValue} to {typeof(TEnum)}");
            return;
        }

        if (isAvailableForDefault || !isAvailableForDefault && playerConfigSet.PlayerConfigType == PlayerConfigType.Player)
        {
            DrawSourceIndicator(playerConfigSet.PlayerConfigType, extractedProperty.PlayerConfigType, extractedProperty.CategoryId);
            DrawInheritOverrideCombo(key, playerConfigSet.PlayerConfigType, ref config.InheritOverride, ref isChanged);
        }

        var enumNames = Enum.GetNames(typeof(TEnum));
        var selectedIndex = Array.IndexOf(enumNames, enumValue.ToString());

        ImGui.SetNextItemWidth(Helper.CalcScaledComboWidth(150f));
        if (Helper.Combo(key, ref selectedIndex, enumNames, 150, false))
        {
            var selectedEnum = (TEnum)Enum.Parse(typeof(TEnum), enumNames[selectedIndex]);
            config.Value = selectedEnum;
            isChanged = true;
            config.InheritOverride = playerConfigSet.PlayerConfigType == PlayerConfigType.Default ? InheritOverride.None : InheritOverride.Override;
        }
    }

    private static void DrawSourceIndicator(PlayerConfigType currentPlayerConfigType, PlayerConfigType sourcePlayerConfigType, int categoryId)
    {
        if (currentPlayerConfigType == PlayerConfigType.Default)
            return;

        const float iconOffset = 2.0f;
        if (currentPlayerConfigType == sourcePlayerConfigType)
        {
            using (ImRaii.Group())
            {
                var currentPosY = ImGui.GetCursorPosY();

                var pos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new Vector2(pos.X + (19f * ImGuiHelpers.GlobalScale), pos.Y + (iconOffset * ImGuiHelpers.GlobalScale)));
                using (ImRaii.PushFont(UiBuilder.IconFont))
                    Helper.TextColored(Vector4.Zero, FontAwesomeIcon.None.ToIconString());

                ImGui.SetCursorPosY(currentPosY);
            }

            return;
        }

        switch (sourcePlayerConfigType)
        {
            case PlayerConfigType.Category:
                var category = ServiceContext.CategoryService.GetCategoryById(categoryId);
                var categoryName = category == null ? string.Empty : category.Name;
                DrawSourceIndicatorIcon(string.Format(Language.CategorySpecificSetting, categoryName), FontAwesomeIcon.FolderOpen, iconOffset);
                break;
            case PlayerConfigType.Default:
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 3f * ImGuiHelpers.GlobalScale);
                DrawSourceIndicatorIcon(Language.DefaultSetting, FontAwesomeIcon.GlobeAmericas, iconOffset);
                break;
            case PlayerConfigType.Player:
            default:
                throw new ArgumentOutOfRangeException(nameof(sourcePlayerConfigType), sourcePlayerConfigType, null);
        }
    }

    private static void DrawSourceIndicatorIcon(string text, FontAwesomeIcon icon, float offset)
    {
        using (ImRaii.Group())
        {
            var currentPosY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (offset * ImGuiHelpers.GlobalScale));
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(icon.ToIconString());

            ImGui.SetCursorPosY(currentPosY);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);

        ImGui.SameLine();
    }

    private static void DrawInheritOverrideCombo(string key, PlayerConfigType playerConfigType, ref InheritOverride option, ref bool isChanged)
    {
        if (playerConfigType == PlayerConfigType.Default)
            return;

        var optionIndex = Array.IndexOf(InheritOrOverride, option.ToString());
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Helper.CalcScaledComboWidth(90f));
        if (Helper.Combo($"###{key}_InheritOverrideCombo", ref optionIndex, InheritOrOverride, 100, false, false))
        {
            option = Enum.Parse<InheritOverride>(InheritOrOverride[optionIndex]);
            isChanged = true;
        }

        ImGui.SameLine();
    }
}
