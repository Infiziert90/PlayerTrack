using System.Collections.Generic;
using AutoMapper;
using Newtonsoft.Json;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;

namespace PlayerTrack.Infrastructure;

public class PlayerConfigMappingProfile : Profile
{
    public PlayerConfigMappingProfile()
    {
        CreateMap<PlayerConfig, PlayerConfigDTO>()
            .ForMember(dest => dest.id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.created, opt => opt.MapFrom(src => src.Created))
            .ForMember(dest => dest.updated, opt => opt.MapFrom(src => src.Updated))
            .ForMember(dest => dest.player_config_type, opt => opt.MapFrom(src => src.PlayerConfigType))
            .ForMember(dest => dest.player_id, opt => opt.MapFrom(src => src.PlayerId))
            .ForMember(dest => dest.category_id, opt => opt.MapFrom(src => src.CategoryId))
            .ForMember(dest => dest.player_list_name_color, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.PlayerListNameColor)))
            .ForMember(dest => dest.player_list_icon, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.PlayerListIcon)))
            .ForMember(dest => dest.nameplate_custom_title, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateCustomTitle)))
            .ForMember(dest => dest.nameplate_show_in_overworld, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateShowInOverworld)))
            .ForMember(dest => dest.nameplate_show_in_content, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateShowInContent)))
            .ForMember(dest => dest.nameplate_show_in_high_end_content, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateShowInHighEndContent)))
            .ForMember(dest => dest.nameplate_color, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateColor)))
            .ForMember(dest => dest.nameplate_use_color, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateUseColor)))
            .ForMember(dest => dest.nameplate_use_color_if_dead, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateUseColorIfDead)))
            .ForMember(dest => dest.nameplate_title_type, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.NameplateTitleType)))
            .ForMember(dest => dest.alert_name_change, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertNameChange)))
            .ForMember(dest => dest.alert_world_transfer, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertWorldTransfer)))
            .ForMember(dest => dest.alert_proximity, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertProximity)))
            .ForMember(dest => dest.alert_name_change_frequency, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertNameChangeFrequency)))
            .ForMember(dest => dest.alert_world_transfer_frequency, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertWorldTransferFrequency)))
            .ForMember(dest => dest.alert_proximity_frequency, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertProximityFrequency)))
            .ForMember(dest => dest.alert_format_include_category, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertFormatIncludeCategory)))
            .ForMember(dest => dest.alert_format_include_custom_title, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.AlertFormatIncludeCustomTitle)))
            .ForMember(dest => dest.visibility_type, opt => opt.MapFrom(src => JsonConvert.SerializeObject(src.VisibilityType)));

        CreateMap<PlayerConfigDTO, PlayerConfig>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.id))
            .ForMember(dest => dest.Created, opt => opt.MapFrom(src => src.created))
            .ForMember(dest => dest.Updated, opt => opt.MapFrom(src => src.updated))
            .ForMember(dest => dest.PlayerConfigType, opt => opt.MapFrom(src => src.player_config_type))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.id))
            .ForMember(dest => dest.PlayerId, opt => opt.MapFrom(src => src.player_id))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.category_id))
            .ForMember(dest => dest.PlayerListNameColor, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<uint>>(src.player_list_name_color)))
            .ForMember(dest => dest.PlayerListIcon, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<char>>(src.player_list_icon)))
            .ForMember(dest => dest.NameplateCustomTitle, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<string>>(src.nameplate_custom_title)))
            .ForMember(dest => dest.NameplateShowInOverworld, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.nameplate_show_in_overworld)))
            .ForMember(dest => dest.NameplateShowInContent, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.nameplate_show_in_content)))
            .ForMember(dest => dest.NameplateShowInHighEndContent, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.nameplate_show_in_high_end_content)))
            .ForMember(dest => dest.NameplateColor, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<uint>>(src.nameplate_color)))
            .ForMember(dest => dest.NameplateUseColor, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.nameplate_use_color)))
            .ForMember(dest => dest.NameplateUseColorIfDead, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.nameplate_use_color_if_dead)))
            .ForMember(dest => dest.NameplateTitleType, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<NameplateTitleType>>(src.nameplate_title_type)))
            .ForMember(dest => dest.AlertNameChange, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.alert_name_change)))
            .ForMember(dest => dest.AlertWorldTransfer, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.alert_world_transfer)))
            .ForMember(dest => dest.AlertProximity, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.alert_proximity)))
            .ForMember(dest => dest.AlertNameChangeFrequency, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<long>>(src.alert_name_change_frequency)))
            .ForMember(dest => dest.AlertWorldTransferFrequency, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<long>>(src.alert_world_transfer_frequency)))
            .ForMember(dest => dest.AlertProximityFrequency, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<long>>(src.alert_proximity_frequency)))
            .ForMember(dest => dest.AlertFormatIncludeCategory, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.alert_format_include_category)))
            .ForMember(dest => dest.AlertFormatIncludeCustomTitle, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<bool>>(src.alert_format_include_custom_title)))
            .ForMember(dest => dest.VisibilityType, opt => opt.MapFrom(src => JsonConvert.DeserializeObject<ConfigValue<VisibilityType>>(src.visibility_type)));
    }
}
