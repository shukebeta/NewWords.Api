using NewWords.Api.Entities;
using NewWords.Api.Models.DTOs;

namespace NewWords.Api.Mappers;

internal static class UserSettingsMappings
{
    // Keep this mapping explicit instead of reintroducing a general-purpose mapper library.
    // The DTO is small, and writing the fields out makes schema changes visible during review.
    // If either UserSettings or UserSettingsDto gains a new API-facing field, update this method deliberately.
    internal static UserSettingsDto ToDto(UserSettings setting)
    {
        return new UserSettingsDto
        {
            SettingName = setting.SettingName,
            SettingValue = setting.SettingValue
        };
    }
}
