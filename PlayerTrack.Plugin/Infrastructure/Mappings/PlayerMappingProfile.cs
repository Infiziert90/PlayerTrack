using System.Collections.Generic;
using AutoMapper;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class PlayerMappingProfile : Profile
{
    public PlayerMappingProfile()
    {
        CreateMap<Player, PlayerDTO>()
            .ForMember(dest => dest.id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.created, opt => opt.MapFrom(src => src.Created))
            .ForMember(dest => dest.updated, opt => opt.MapFrom(src => src.Updated))
            .ForMember(dest => dest.last_alert_sent, opt => opt.MapFrom(src => src.LastAlertSent))
            .ForMember(dest => dest.last_name_change_alert_sent, opt => opt.MapFrom(src => src.LastNameChangeAlertSent))
            .ForMember(dest => dest.last_world_change_alert_sent, opt => opt.MapFrom(src => src.LastWorldChangeAlertSent))
            .ForMember(dest => dest.first_seen, opt => opt.MapFrom(src => src.FirstSeen))
            .ForMember(dest => dest.last_seen, opt => opt.MapFrom(src => src.LastSeen))
            .ForMember(dest => dest.customize, opt => opt.MapFrom(src => src.Customize))
            .ForMember(dest => dest.seen_count, opt => opt.MapFrom(src => src.SeenCount))
            .ForMember(dest => dest.lodestone_status, opt => opt.MapFrom(src => (int)src.LodestoneStatus))
            .ForMember(dest => dest.lodestone_verified_on, opt => opt.MapFrom(src => src.LodestoneVerifiedOn))
            .ForMember(dest => dest.free_company_state, opt => opt.MapFrom(src => (int)src.FreeCompany.Key))
            .ForMember(dest => dest.free_company_tag, opt => opt.MapFrom(src => src.FreeCompany.Value))
            .ForMember(dest => dest.key, opt => opt.MapFrom(src => src.Key))
            .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.notes, opt => opt.MapFrom(src => src.Notes))
            .ForMember(dest => dest.lodestone_id, opt => opt.MapFrom(src => src.LodestoneId))
            .ForMember(dest => dest.entity_id, opt => opt.MapFrom(src => src.EntityId))
            .ForMember(dest => dest.world_id, opt => opt.MapFrom(src => src.WorldId))
            .ForMember(dest => dest.last_territory_type, opt => opt.MapFrom(src => src.LastTerritoryType))
            .ForMember(dest => dest.content_id, opt => opt.MapFrom(src => src.ContentId));

        CreateMap<PlayerDTO, Player>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.id))
            .ForMember(dest => dest.Created, opt => opt.MapFrom(src => src.created))
            .ForMember(dest => dest.Updated, opt => opt.MapFrom(src => src.updated))
            .ForMember(dest => dest.LastAlertSent, opt => opt.MapFrom(src => src.last_alert_sent))
            .ForMember(dest => dest.LastNameChangeAlertSent, opt => opt.MapFrom(src => src.last_name_change_alert_sent))
            .ForMember(dest => dest.LastWorldChangeAlertSent, opt => opt.MapFrom(src => src.last_world_change_alert_sent))
            .ForMember(dest => dest.FirstSeen, opt => opt.MapFrom(src => src.first_seen))
            .ForMember(dest => dest.LastSeen, opt => opt.MapFrom(src => src.last_seen))
            .ForMember(dest => dest.Customize, opt => opt.MapFrom(src => src.customize))
            .ForMember(dest => dest.SeenCount, opt => opt.MapFrom(src => src.seen_count))
            .ForMember(dest => dest.LodestoneStatus, opt => opt.MapFrom(src => (LodestoneStatus)src.lodestone_status))
            .ForMember(dest => dest.LodestoneVerifiedOn, opt => opt.MapFrom(src => src.lodestone_verified_on))
            .ForMember(dest => dest.FreeCompany, opt => opt.MapFrom(src => new KeyValuePair<FreeCompanyState, string>(src.free_company_state, src.free_company_tag)))
            .ForMember(dest => dest.Key, opt => opt.MapFrom(src => src.key))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.name))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.notes))
            .ForMember(dest => dest.LodestoneId, opt => opt.MapFrom(src => src.lodestone_id))
            .ForMember(dest => dest.EntityId, opt => opt.MapFrom(src => src.entity_id))
            .ForMember(dest => dest.WorldId, opt => opt.MapFrom(src => src.world_id))
            .ForMember(dest => dest.LastTerritoryType, opt => opt.MapFrom(src => src.last_territory_type))
            .ForMember(dest => dest.ContentId, opt => opt.MapFrom(src => src.content_id));
    }
}
