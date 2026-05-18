using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Resource;

namespace PlayerTrack.Windows.Config.Components;

public class IntegrationComponent : ConfigViewComponent
{
    private static readonly XivChatType[] ChatChannels =
    {
        XivChatType.Notice,
        XivChatType.Echo,
        XivChatType.Debug,
        XivChatType.Urgent,
        XivChatType.SystemMessage,
        XivChatType.SystemError,
        XivChatType.ErrorMessage,
        XivChatType.GatheringSystemMessage,
        XivChatType.Say,
        XivChatType.Yell,
        XivChatType.Shout,
        XivChatType.TellIncoming,
        XivChatType.TellOutgoing,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.NoviceNetwork,
        XivChatType.PvPTeam,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
    };

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("###Integration_TabBar", ImGuiTabBarFlags.None);
        if (!tabBar.Success)
            return;

        using (var tabItem = ImRaii.TabItem(Language.Lodestone))
        {
            if (tabItem.Success)
                DrawLodestoneTab();
        }

        using (var tabItem = ImRaii.TabItem(Language.Visibility))
        {
            if (tabItem.Success)
                DrawVisibilityTab();
        }

        using (var tabItem = ImRaii.TabItem(Language.Chat))
        {
            if (tabItem.Success)
                DrawChatTab();
        }
    }

    private void DrawLodestoneTab()
    {
        var lodestoneLocale = Config.LodestoneLocale;
        if (Helper.Combo(Language.LodestoneLocale, ref lodestoneLocale, 60))
        {
            Config.LodestoneLocale = lodestoneLocale;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    private void DrawVisibilityTab()
    {
        ImGuiHelpers.ScaledDummy(1f);
        var syncWithVisibility = Config.SyncWithVisibility;
        if (Helper.Checkbox(Language.SyncWithVisibility, ref syncWithVisibility))
        {
            Config.SyncWithVisibility = syncWithVisibility;
            ServiceContext.ConfigService.SaveConfig(Config);
        }
    }

    private void DrawChatTab()
    {
        ImGuiHelpers.ScaledDummy(1f);
        var useCustom = Config.UseCustomChatChannel;
        if (Helper.Checkbox(Language.UseCustomChatChannel, ref useCustom))
        {
            Config.UseCustomChatChannel = useCustom;
            ServiceContext.ConfigService.SaveConfig(Config);
        }

        if (!useCustom)
            return;

        var current = Config.CustomChatChannel;
        ImGuiHelpers.ScaledDummy(1f);
        ImGui.SetNextItemWidth(Helper.CalcScaledComboWidth(200));
        using var combo = ImRaii.Combo(Language.ChatChannel, current.ToString());
        if (!combo.Success)
            return;

        foreach (var channel in ChatChannels)
        {
            var isSelected = channel == current;
            if (ImGui.Selectable(channel.ToString(), isSelected))
            {
                Config.CustomChatChannel = channel;
                ServiceContext.ConfigService.SaveConfig(Config);
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }
    }
}
