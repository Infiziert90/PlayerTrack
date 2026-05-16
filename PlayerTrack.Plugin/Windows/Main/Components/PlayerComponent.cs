using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Domain;
using PlayerTrack.Models;
using PlayerTrack.Resource;
using PlayerTrack.Windows.Components;
using PlayerTrack.Windows.Main.Presenters;

namespace PlayerTrack.Windows.Main.Components;

public class PlayerComponent
{
    private readonly IMainPresenter Presenter;

    private readonly PlayerSummaryComponent PlayerSummaryComponent;
    private readonly PlayerEncounterComponent PlayerEncounterComponent;
    private readonly PlayerHistoryComponent PlayerHistoryComponent;
    private readonly PlayerActionComponent PlayerActionComponent;

    public PlayerComponent(IMainPresenter presenter)
    {
        Presenter = presenter;

        PlayerSummaryComponent = new PlayerSummaryComponent(presenter);
        PlayerEncounterComponent = new PlayerEncounterComponent(presenter);
        PlayerHistoryComponent = new PlayerHistoryComponent(presenter);
        PlayerActionComponent = new PlayerActionComponent(presenter);
    }

    public void Draw()
    {
        var player = Presenter.GetSelectedPlayer();
        var isLoadingPlayer = Presenter.IsPlayerLoading();
        if (player == null)
            return;

        using var child = ImRaii.Child("###ConfigMenuOption_Player", new Vector2(-1, 0), false);
        if (!child.Success)
            return;

        using var disabled = ImRaii.Disabled(isLoadingPlayer);

        using var tabBar = ImRaii.TabBar("###PlayerSummary_TabBar", ImGuiTabBarFlags.None);
        if (!tabBar.Success)
            return;

        using (var tabItem = ImRaii.TabItem(Language.Players))
        {
            if (tabItem.Success)
                PlayerSummaryComponent.Draw();
        }
        using (var tabItem = ImRaii.TabItem(Language.Encounters))
        {
            if (tabItem.Success)
                PlayerEncounterComponent.Draw();
        }

        using (var tabItem = ImRaii.TabItem("Data"))
        {
            if (tabItem.Success)
                PlayerHistoryComponent.Draw();
        }

        using (var tabItem = ImRaii.TabItem(Language.Settings))
        {
            if (tabItem.Success)
            {
                using var indent = ImRaii.PushIndent(6f);
                ImGuiHelpers.ScaledDummy(3f);

                using var innerTabBar = ImRaii.TabBar("###PlayerConfigTabBar", ImGuiTabBarFlags.None);
                if (innerTabBar.Success)
                {
                    player.PlayerConfig.PlayerConfigType = PlayerConfigType.Player;
                    player.PlayerConfig = PlayerConfigComponent.DrawPlayerConfigTabs(player);
                    if (player.PlayerConfig.IsChanged)
                    {
                        player.PlayerConfig.IsChanged = false;
                        PlayerConfigService.UpdateConfig(player.Id, player.PlayerConfig);
                    }
                }
            }
        }

        using (var tabItem = ImRaii.TabItem(Language.Actions))
        {
            if (tabItem.Success)
                PlayerActionComponent.Draw();
        }
    }
}
