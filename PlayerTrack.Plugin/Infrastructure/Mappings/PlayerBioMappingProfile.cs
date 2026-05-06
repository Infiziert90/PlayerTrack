using AutoMapper;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class PlayerBioMappingProfile : Profile
{
    public PlayerBioMappingProfile()
    {
        CreateMap<PlayerBio, PlayerBioDTO>()
            .ForMember(dest => dest.id,        opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.created,   opt => opt.MapFrom(src => src.Created))
            .ForMember(dest => dest.updated,   opt => opt.MapFrom(src => src.Updated))
            .ForMember(dest => dest.player_id, opt => opt.MapFrom(src => src.PlayerId))
            .ForMember(dest => dest.bio,       opt => opt.MapFrom(src => src.Bio));

        CreateMap<PlayerBioDTO, PlayerBio>()
            .ForMember(dest => dest.Id,       opt => opt.MapFrom(src => src.id))
            .ForMember(dest => dest.Created,  opt => opt.MapFrom(src => src.created))
            .ForMember(dest => dest.Updated,  opt => opt.MapFrom(src => src.updated))
            .ForMember(dest => dest.PlayerId, opt => opt.MapFrom(src => src.player_id))
            .ForMember(dest => dest.Bio,      opt => opt.MapFrom(src => src.bio));
    }
}
