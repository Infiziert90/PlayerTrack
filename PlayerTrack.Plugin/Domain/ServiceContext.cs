using System;

namespace PlayerTrack.Domain;

public static class ServiceContext
{
    public static BackupService BackupService { get; set; } = null!;
    public static CategoryService CategoryService { get; set; } = null!;
    public static ConfigService ConfigService { get; set; } = null!;
    public static EncounterService EncounterService { get; set; } = null!;
    public static LodestoneService LodestoneService { get; set; } = null!;
    public static PlayerDataService PlayerDataService { get; set; } = null!;
    public static PlayerConfigService PlayerConfigService { get; set; } = null!;
    public static PlayerNameplateService PlayerNameplateService { get; set; } = null!;
    public static PlayerCategoryService PlayerCategoryService { get; set; } = null!;
    public static PlayerTagService PlayerTagService { get; set; } = null!;
    public static PlayerChangeService PlayerChangeService { get; set; } = null!;
    public static PlayerEncounterService PlayerEncounterService { get; set; } = null!;
    public static PlayerAlertService PlayerAlertService { get; set; } = null!;
    public static PlayerProcessService PlayerProcessService { get; set; } = null!;
    public static PlayerCacheService PlayerCacheService { get; set; } = null!;
    public static TagService TagService { get; set; } = null!;
    public static VisibilityService VisibilityService { get; set; } = null!;
    public static LocalPlayerService LocalPlayerService { get; set; } = null!;
    public static SocialListService SocialListService { get; set; } = null!;
    public static PlayerBioService PlayerBioService { get; set; } = null!;

    public static void Initialize()
    {
        Plugin.PluginLog.Verbose("Entering ServiceContext.Initialize()");
        ConfigService = new ConfigService();
        BackupService = new BackupService();
        CategoryService = new CategoryService();
        TagService = new TagService();
        EncounterService = new EncounterService();
        PlayerNameplateService = new PlayerNameplateService();
        PlayerCategoryService = new PlayerCategoryService();
        PlayerTagService = new PlayerTagService();
        PlayerChangeService = new PlayerChangeService();
        PlayerCacheService = new PlayerCacheService();
        PlayerDataService = new PlayerDataService();
        PlayerEncounterService = new PlayerEncounterService();
        PlayerAlertService = new PlayerAlertService();
        PlayerProcessService = new PlayerProcessService();
        PlayerConfigService = new PlayerConfigService();
        LodestoneService = new LodestoneService();
        VisibilityService = new VisibilityService();
        LocalPlayerService = new LocalPlayerService();
        SocialListService = new SocialListService();
        PlayerBioService = new PlayerBioService();
    }

    public static void Dispose()
    {
        Plugin.PluginLog.Verbose("Entering ServiceContext.Dispose()");
        try
        {
            PlayerCacheService.Dispose();
            PlayerProcessService.Dispose();
            EncounterService.Dispose();
            VisibilityService.Dispose();
            PlayerAlertService.Dispose();
        }
        catch (Exception)
        {
            Plugin.PluginLog.Warning("Failed to dispose services.");
        }
    }
}
