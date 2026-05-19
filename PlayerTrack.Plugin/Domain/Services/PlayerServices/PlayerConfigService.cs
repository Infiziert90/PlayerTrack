using System;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;
using System.Collections.Generic;

namespace PlayerTrack.Domain;

public class PlayerConfigService
{
    public static Action<int>? CategoryUpdated;

    public static ExtractedProperty<T> ExtractProperty<T>(PlayerConfigSet playerConfigSet, Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        return playerConfigSet.PlayerConfigType switch
        {
            PlayerConfigType.Default => new ExtractedProperty<T>
            {
                PlayerConfigType = PlayerConfigType.Default,
                PropertyValue = ExtractDefaultProperty(GetDefaultConfig(), propertySelector).PropertyValue,
            },
            PlayerConfigType.Category => ExtractCategoryProperty(GetDefaultConfig(), playerConfigSet.CurrentPlayerConfig, propertySelector),
            PlayerConfigType.Player => ExtractPlayerProperty(GetDefaultConfig(), playerConfigSet.CurrentPlayerConfig, playerConfigSet.CategoryPlayerConfigs, propertySelector),
            _ => throw new ArgumentOutOfRangeException(nameof(playerConfigSet.PlayerConfigType), "Invalid player configuration type.")
        };
    }

    public static VisibilityType GetVisibilityType(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetVisibilityType(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.VisibilityType).PropertyValue;
    }

    public static uint GetNameColor(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.PlayerListNameColor).PropertyValue;
    }

    public static char GetIcon(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetIcon(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.PlayerListIcon).PropertyValue;
    }

    public static bool GetIsProximityAlertEnabled(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetIsProximityAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertProximity).PropertyValue;
    }

    public static bool GetIsWorldTransferAlertEnabled(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetIsWorldTransferAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertWorldTransfer).PropertyValue;
    }

    public static bool GetIsNameChangeAlertEnabled(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetIsNameChangeAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertNameChange).PropertyValue;
    }

    public static long GetProximityAlertFrequency(Player player) =>
        ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertProximityFrequency).PropertyValue;

    public static long GetWorldTransferAlertFrequency(Player player) =>
        ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertWorldTransferFrequency).PropertyValue;

    public static long GetNameChangeAlertFrequency(Player player) =>
        ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertNameChangeFrequency).PropertyValue;

    public static bool GetNameplateShowInOverworld(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateShowInOverworld(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInOverworld).PropertyValue;
    }

    public static bool GetNameplateShowInContent(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateShowInContent(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInContent).PropertyValue;
    }

    public static bool GetNameplateShowInHighEndContent(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateShowInHighEndContent(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInHighEndContent).PropertyValue;
    }

    public static bool GetNameplateUseColor(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateColorShowColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateUseColor).PropertyValue;
    }

    public static uint GetNameplateColor(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateColor).PropertyValue;
    }

    public static bool GetNameplateUseColorIfDead(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateUseColorIfDead(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateUseColorIfDead).PropertyValue;
    }

    public static NameplateTitleType GetNameplateTitleType(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateTitleType(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateTitleType).PropertyValue;
    }

    public static string GetNameplateCustomTitle(Player player)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetNameplateCustomTitle(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateCustomTitle).PropertyValue;
    }

    public static uint GetCategoryColor(Category category)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.GetCategoryColor(): {category.Name}");
        return ExtractCategoryProperty(
            GetDefaultConfig(),
            category.PlayerConfig,
            x => x.PlayerListNameColor).PropertyValue;
    }

    public static void UpdateCategoryConfig(int categoryId, PlayerConfig config)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.UpdateConfig(): {categoryId}, {config.Id}");
        var category = ServiceContext.CategoryService.GetCategory(categoryId);
        if (category == null)
            return;

        if (category.PlayerConfig.Id == 0)
        {
            category.PlayerConfig.CategoryId = category.Id;
            config.Id = RepositoryContext.PlayerConfigRepository.CreatePlayerConfig(config);
        }
        else
        {
            config.Id = category.PlayerConfig.Id;
            RepositoryContext.PlayerConfigRepository.UpdatePlayerConfig(config);
        }

        category.PlayerConfig = config;
        ServiceContext.CategoryService.UpdateCategory(category);
        CategoryUpdated?.Invoke(category.Id);

    }

    public static void DeletePlayerConfig(int playerId) => RepositoryContext.PlayerConfigRepository.DeletePlayerConfigByPlayerId(playerId);

    public static void UpdateConfig(int playerId, PlayerConfig config)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.UpdateConfig(): {playerId}, {config.Id}");
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
            return;

        if (player.PlayerConfig.Id == 0)
        {
            config.PlayerId = playerId;
            RepositoryContext.PlayerConfigRepository.CreatePlayerConfig(config);
            config.Id = RepositoryContext.PlayerConfigRepository.GetIdByPlayerId(playerId) ?? 0;
        }
        else
        {
            config.Id = player.PlayerConfig.Id;
            RepositoryContext.PlayerConfigRepository.UpdatePlayerConfig(config);
        }

        player.PlayerConfig = config;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    public static void DeletePlayerConfigByCategoryId(int categoryId) => RepositoryContext.PlayerConfigRepository.DeletePlayerConfigByCategoryId(categoryId);

    public static void ResetPlayerConfig(int playerId)
    {
        Plugin.PluginLog.Verbose($"Entering PlayerConfigService.ResetPlayerConfig(): {playerId}");
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
            return;

        player.PlayerConfig = new PlayerConfig(PlayerConfigType.Player) { PlayerId = playerId, };
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    private static ExtractedProperty<T> ExtractPlayerProperty<T>(
        PlayerConfig defaultPlayerConfig,
        PlayerConfig playerPlayerConfig,
        List<PlayerConfig> categoryPlayerConfigs,
        Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        var playerProperty = propertySelector(playerPlayerConfig);
        if (playerProperty.InheritOverride == InheritOverride.Override)
            return new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Player, PropertyValue = playerProperty.Value };

        foreach (var categoryPlayerConfig in categoryPlayerConfigs)
        {
            var categoryProperty = propertySelector(categoryPlayerConfig);
            if (categoryProperty.InheritOverride == InheritOverride.Override)
                return new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Category, PropertyValue = categoryProperty.Value, CategoryId = categoryPlayerConfig.CategoryId ?? 0 };
        }

        return ExtractDefaultProperty(defaultPlayerConfig, propertySelector);
    }

    private static ExtractedProperty<T> ExtractCategoryProperty<T>(
        PlayerConfig defaultPlayerConfig,
        PlayerConfig categoryPlayerConfig,
        Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        var categoryProperty = propertySelector(categoryPlayerConfig);
        return categoryProperty.InheritOverride == InheritOverride.Override ? new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Category, PropertyValue = categoryProperty.Value, CategoryId = categoryPlayerConfig.CategoryId ?? 0 } : ExtractDefaultProperty(defaultPlayerConfig, propertySelector);
    }

    private static ExtractedProperty<T> ExtractDefaultProperty<T>(PlayerConfig defaultPlayerConfig, Func<PlayerConfig, ConfigValue<T>> propertySelector) =>
        new() { PlayerConfigType = PlayerConfigType.Default, PropertyValue = propertySelector(defaultPlayerConfig).Value };

    private static PlayerConfig GetDefaultConfig() => ServiceContext.ConfigService.GetConfig().PlayerConfig;
}
