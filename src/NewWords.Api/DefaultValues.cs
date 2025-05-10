using NewWords.Api.Models.DTOs;

namespace NewWords.Api;

public static class DefaultValues
{
    public static readonly List<UserSettingsDto> Settings =
    [
        new UserSettingsDto
        {
            SettingName = "defaultLearningLanguage",
            SettingValue = "en", // Example placeholder
        },
        new UserSettingsDto
        {
            SettingName = "dailyGoal",
            SettingValue = "10", // Example placeholder: 10 words a day
        }
    ];

    public static Dictionary<string, string> SettingsDictionary =>
        Settings.ToDictionary(s => s.SettingName, v => v.SettingValue);
}