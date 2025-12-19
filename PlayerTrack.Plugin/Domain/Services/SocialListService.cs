using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerTrack.Data;
using PlayerTrack.Domain.Common;
using PlayerTrack.Extensions;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Domain;

public class SocialListService
{
    public static void HandleMembersList(SocialListType listType, List<SocialListMemberData> toadMembers, ushort listNumber = 0, ushort page = 0, ushort pageCount = 0)
    {
        Plugin.PluginLog.Verbose($"Entering SocialListService.HandleMembersList: {listType}");

        // get content id
        var localPlayerContentId = Plugin.PlayerState.ContentId;
        if (localPlayerContentId == 0)
        {
            Plugin.PluginLog.Warning("HandleMembersList: LocalContentId is 0");
            return;
        }

        // get local player
        var localPlayer = Plugin.ClientStateHandler.GetLocalPlayer();
        if (localPlayer == null)
        {
            Plugin.PluginLog.Warning("HandleMembersList: LocalPlayer is null");
            return;
        }

        // kick off rest of processing async
        Task.Run(() => HandleMembersListInternal(listType, toadMembers, listNumber, page, pageCount, localPlayer, localPlayerContentId));
    }

    private static void HandleMembersListInternal(SocialListType listType, List<SocialListMemberData> toadMembers, ushort listNumber, ushort page, ushort pageCount, LocalPlayerData localPlayer, ulong localPlayerContentId)
    {
         // get data center
        var dataCenter = Sheets.Worlds.Values.FirstOrDefault(x => x.Id == localPlayer.HomeWorld)?.DataCenterId ?? 0;
        if (dataCenter == 0)
        {
            Plugin.PluginLog.Warning("HandleMembersList: DataCenter is 0");
            return;
        }

        // retrieve or create social list
        // use dc for cwls since you have a different set per dc
        Plugin.PluginLog.Verbose($"HandleMembersList: Retrieving social list for {localPlayerContentId} {listType} {listNumber}");
        var socialList = listType != SocialListType.CrossWorldLinkShell ?
            RepositoryContext.SocialListRepository.GetSocialList(localPlayerContentId, listType, listNumber) :
            RepositoryContext.SocialListRepository.GetSocialList(localPlayerContentId, listType, listNumber, dataCenter);

        // check if list already exists
        if (socialList == null)
        {
            // create new list
            socialList = new SocialList { ContentId = localPlayerContentId, ListType = listType, };

            // set list number for ls / cwls
            if (listType is SocialListType.LinkShell or SocialListType.CrossWorldLinkShell)
                socialList.ListNumber = listNumber;

            // set data center for cwls
            if (listType == SocialListType.CrossWorldLinkShell)
                socialList.DataCenterId = dataCenter;

            // set and verify id
            socialList.Id = RepositoryContext.SocialListRepository.CreateSocialList(socialList);
            if (socialList.Id == 0)
            {
                Plugin.PluginLog.Warning("HandleMembersList: Failed to create social list");
                return;
            }
        }

        // update page fields for FC
        else if (listType == SocialListType.FreeCompany)
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (socialList.PageLastUpdated.TryGetValue(page, out _))
                socialList.PageLastUpdated[page] = currentTime;
            else
                socialList.PageLastUpdated.TryAdd(page, currentTime);

            socialList.PageCount = pageCount;
            RepositoryContext.SocialListRepository.UpdateSocialList(socialList);
        }

        // get synced category
        var syncedCategory = ServiceContext.CategoryService.GetSyncedCategory(socialList.Id);

        // retrieve existing members
        Plugin.PluginLog.Verbose($"HandleMembersList: Retrieving existing members for {socialList.Id}");
        var existingMembers = RepositoryContext.SocialListMemberRepository.GetSocialListMembers(socialList.Id);

        // remove self
        toadMembers.RemoveAll(x => x.ContentId == localPlayerContentId);

        // loop through incoming list
        Plugin.PluginLog.Verbose($"HandleMembersList: Looping through {toadMembers.Count} members");
        foreach (var toadMember in toadMembers)
        {
            // check if new or existing
            var existingMember = existingMembers.FirstOrDefault(x => x.ContentId == toadMember.ContentId);
            var memberKey = PlayerKeyBuilder.Build(toadMember.Name, toadMember.HomeWorld);
            if (existingMember != null)
            {
                // update existing if name/world changed
                if (!existingMember.Key.Equals(memberKey))
                {
                    existingMember.Key = memberKey;
                    existingMember.Name = toadMember.Name;
                    existingMember.WorldId = toadMember.HomeWorld;
                    existingMember.PageNumber = page;
                }
            }
            else
            {
                // create new member
                var newMember = new SocialListMember
                {
                    SocialListId = socialList.Id,
                    ContentId = toadMember.ContentId,
                    Key = memberKey,
                    Name = toadMember.Name,
                    WorldId = toadMember.HomeWorld,
                    PageNumber = page,
                };

                RepositoryContext.SocialListMemberRepository.CreateSocialListMember(newMember);
            }
        }

        // get all current members for list
        Plugin.PluginLog.Verbose($"HandleMembersList: Retrieving all members for {socialList.Id}");
        var socialListMembers = RepositoryContext.SocialListMemberRepository.GetSocialListMembers(socialList.Id, page);

        // remove members that are no longer in list
        Plugin.PluginLog.Verbose($"HandleMembersList: Looping through {socialListMembers.Count} members");
        foreach (var socialListMember in socialListMembers)
        {
            var toadMember = toadMembers.FirstOrDefault(x => x.ContentId == socialListMember.ContentId);
            if (toadMember == null)
            {
                Plugin.PluginLog.Verbose($"HandleMembersList: Removing member {socialListMember.Name} from page {socialListMember.PageNumber}");
                RepositoryContext.SocialListMemberRepository.DeleteSocialListMember(socialListMember.Id);

                // remove from synced category
                if (syncedCategory != null)
                {
                    Plugin.PluginLog.Verbose($"HandleMembersList: Removing player {socialListMember.Name} from dynamic category {syncedCategory.Id}");
                    PlayerCategoryService.UnassignCategoryFromPlayer(socialListMember.Id, syncedCategory.Id);
                }
            }
        }

        // remove members from deleted pages for FC
        Plugin.PluginLog.Verbose($"HandleMembersList: Removing members from deleted pages");
        if (listType == SocialListType.FreeCompany)
        {
            var fcList = RepositoryContext.SocialListRepository.GetSocialList(localPlayerContentId, SocialListType.FreeCompany);
            if (fcList != null)
            {
                var fcMembers = RepositoryContext.SocialListMemberRepository.GetSocialListMembers(fcList.Id);
                foreach (var fcMember in fcMembers)
                {
                    if (fcMember.PageNumber > socialList.PageCount)
                    {
                        Plugin.PluginLog.Verbose($"HandleMembersList: Removing FC member {fcMember.Name} from page {fcMember.PageNumber}");
                        RepositoryContext.SocialListMemberRepository.DeleteSocialListMember(fcMember.Id);
                    }
                }
            }
        }

        // get fresh copy
        Plugin.PluginLog.Verbose($"HandleMembersList: Retrieving fresh copy of social list");
        socialList = listType != SocialListType.CrossWorldLinkShell ?
            RepositoryContext.SocialListRepository.GetSocialList(localPlayerContentId, listType, listNumber) :
            RepositoryContext.SocialListRepository.GetSocialList(localPlayerContentId, listType, listNumber, dataCenter);

        if (socialList == null)
        {
            Plugin.PluginLog.Warning("HandleMembersList: Failed to retrieve social list");
            return;
        }
        socialListMembers = RepositoryContext.SocialListMemberRepository.GetSocialListMembers(socialList.Id);

        // add / update players
        Plugin.PluginLog.Verbose($"HandleMembersList: Adding / updating players");
        var players = new List<Player>();
        foreach (var socialListMember in socialListMembers)
        {
            var player = ServiceContext.PlayerDataService.GetPlayer(socialListMember.ContentId, socialListMember.Name, socialListMember.WorldId);
            if (player == null)
            {
                if (socialList.AddPlayers)
                {
                    if (string.IsNullOrEmpty(socialListMember.Name)) continue;
                    PlayerProcessService.CreateNewPlayer(socialListMember.Name, socialListMember.WorldId, socialListMember.ContentId, false);
                    player = ServiceContext.PlayerDataService.GetPlayer(socialListMember.ContentId, socialListMember.Name, socialListMember.WorldId);
                }
            }
            else if (player.ContentId == 0)
            {
                player.ContentId = socialListMember.ContentId;
                ServiceContext.PlayerDataService.UpdatePlayer(player);
            }

            if (player != null)
                players.Add(player);

        }

        // setup category list
        var categoryIds = new List<int>();

        // sync with dynamic category
        Plugin.PluginLog.Verbose($"HandleMembersList: Syncing with dynamic category");
        if (socialList.SyncWithCategory)
        {
            if (syncedCategory == null)
            {
                ServiceContext.CategoryService.CreateCategory(GetCategoryName(localPlayerContentId, listType, listNumber), socialList.Id);
                syncedCategory = ServiceContext.CategoryService.GetSyncedCategory(socialList.Id);

                if (syncedCategory == null)
                {
                    Plugin.PluginLog.Warning("HandleMembersList: Failed to create synced category");
                    return;
                }
            }

            // Remove stale players
            var playersInCategory = ServiceContext.PlayerCacheService.GetCategoryPlayers(syncedCategory.Id);
            foreach (var player in playersInCategory)
            {
                if (!players.Contains(player))
                {
                    Plugin.PluginLog.Verbose($"HandleMembersList: Removing player {player.Name} from dynamic category {syncedCategory.Id}");
                    PlayerCategoryService.UnassignCategoryFromPlayer(player.Id, syncedCategory.Id);
                }
            }

            categoryIds.Add(syncedCategory.Id);
        }
        else if (syncedCategory != null)
        {
            ServiceContext.CategoryService.DeleteCategory(syncedCategory);
        }

        // add to default category if missing
        Plugin.PluginLog.Verbose($"HandleMembersList: Adding to default category");
        if (socialList.DefaultCategoryId != 0)
            categoryIds.Add(socialList.DefaultCategoryId);

        // assign categories
        if (categoryIds.Count > 0)
            PlayerCategoryService.AssignCategoriesToPlayers(players, categoryIds.ToArray());
    }

    public static List<SocialList> GetSocialLists(ulong contentId)
    {
        return RepositoryContext.SocialListRepository.GetSocialLists(contentId);
    }

    public static int AddOrUpdateSocialList(SocialList socialList)
    {
        if (socialList.Id == 0)
            socialList.Id = RepositoryContext.SocialListRepository.CreateSocialList(socialList);
        else
            RepositoryContext.SocialListRepository.UpdateSocialList(socialList);

        return socialList.Id;

    }

    public static string GetSocialListName(SocialListType socialListType, int listNumber = 0)
    {
        var socialListName = socialListType.ToLocalizedString();
        return listNumber == 0 ? socialListName : $"{socialListName} [{listNumber}]";
    }

    public static Category ResetCategoryName(Category category)
    {
        category.Name = GetCategoryName(category);
        ServiceContext.CategoryService.UpdateCategory(category);
        return category;
    }

    public static string GetCategoryName(Category category)
    {
        var socialList = RepositoryContext.SocialListRepository.GetSocialList(category.SocialListId);
        return socialList == null ? category.Name : GetCategoryName(socialList.ContentId, socialList.ListType, socialList.ListNumber);
    }

    public static string GetCategoryName(ulong contentId, SocialListType socialListType, int listNumber)
    {
        var localPlayer = LocalPlayerService.GetLocalPlayer(contentId);
        if (localPlayer == null)
            return string.Empty;

        var playerName = LocalPlayerService.GetLocalPlayerFullName(contentId);
        var socialListTypeAbbreviation = socialListType.ToAbrLocalizedString();

        switch (socialListType)
        {
            case SocialListType.None:
            case SocialListType.FriendList:
            case SocialListType.BlackList:
            case SocialListType.FreeCompany:
                return $"{socialListTypeAbbreviation} [{playerName}]";
            case SocialListType.LinkShell:
            case SocialListType.CrossWorldLinkShell:
                return $"{socialListTypeAbbreviation}#{listNumber} [{playerName}]";
            default:
                throw new ArgumentOutOfRangeException(nameof(socialListType), socialListType, null);
        }
    }

    public static void DeleteSocialLists(ulong localPlayerContentId)
    {
        var socialLists = RepositoryContext.SocialListRepository.GetSocialLists(localPlayerContentId);
        foreach (var socialList in socialLists)
        {
            var syncedCategory = ServiceContext.CategoryService.GetSyncedCategory(socialList.Id);
            if (syncedCategory != null)
                ServiceContext.CategoryService.DeleteCategory(syncedCategory);

            RepositoryContext.SocialListMemberRepository.DeleteSocialListMembers(socialList.Id);
            RepositoryContext.SocialListRepository.DeleteSocialList(socialList.Id);
        }
    }

    public static void DeleteSyncedCategory(int socialListId)
    {
        var syncedCategory = ServiceContext.CategoryService.GetSyncedCategory(socialListId);
        if (syncedCategory != null)
        {
            ServiceContext.CategoryService.DeleteCategory(syncedCategory);
        }
    }
}
