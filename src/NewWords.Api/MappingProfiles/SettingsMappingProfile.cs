using AutoMapper;
using NewWords.Api.Entities;
using NewWords.Api.Models.DTOs;

namespace NewWords.Api.MappingProfiles;

public class SettingsMappingProfile : Profile
{
    public SettingsMappingProfile()
    {
        CreateMap<UserSettings, UserSettingsDto>().ReverseMap();
    }
}