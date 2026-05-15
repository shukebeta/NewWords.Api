using Api.Framework;
using Api.Framework.Extensions;
using Api.Framework.Result;
using NewWords.Api.Models.DTOs;
using NewWords.Api.Entities;
using NewWords.Api.Mappers;
using NewWords.Api.Services.interfaces;
using Microsoft.AspNetCore.Mvc;
using Api.Framework.Helper;
using LLM;
using LLM.Models;
using Microsoft.AspNetCore.Authorization;

namespace NewWords.Api.Controllers;

[Authorize]
public class SettingsController(
    ICurrentUser currentUser,
    IRepositoryBase<UserSettings> userSettingsRepository,
    IConfigurationService configurationService)
    : BaseController
{
    [HttpGet]
    public async Task<ApiResult<List<UserSettingsDto>>> GetAll()
    {
        var userId = currentUser.Id;
        if (userId == 0) return new FailedResult<List<UserSettingsDto>>(null, "User not authenticated or ID not found.");

        var settings = await userSettingsRepository.GetListAsync(s => s.UserId.Equals(userId));

        if (settings.Count < DefaultValues.Settings.Count)
        {
            var userSettingDict = new Dictionary<string, string>(DefaultValues.SettingsDictionary);
            foreach (var setting in settings)
            {
                if (userSettingDict.ContainsKey(setting.SettingName))
                {
                    userSettingDict[setting.SettingName] = setting.SettingValue;
                }
            }

            settings = userSettingDict.Select(kvp => new UserSettings
            {
                UserId = userId,
                SettingName = kvp.Key,
                SettingValue = kvp.Value
            }).ToList();
        }

        // Keep response mapping explicit so DTO shape changes fail visibly in code review.
        var settingsDtos = settings.Select(UserSettingsMappings.ToDto).ToList();
        return new SuccessfulResult<List<UserSettingsDto>>(settingsDtos);
    }

    [HttpPost]
    public async Task<ApiResult<bool>> Upsert(UserSettingsDto settingsDto)
    {
        var userId = currentUser.Id;
        if (userId == 0) return new FailedResult<bool>(false, "User not authenticated or ID not found.");

        if (string.IsNullOrWhiteSpace(settingsDto.SettingName) ||
            !DefaultValues.SettingsDictionary.ContainsKey(settingsDto.SettingName))
        {
            throw CustomExceptionHelper.New(settingsDto, (int)NewWords.Api.Enums.EventId._00106_UnknownSettingName, NewWords.Api.Enums.EventId._00106_UnknownSettingName.Description(settingsDto.SettingName));
        }

        var now = DateTime.UtcNow.ToUnixTimeSeconds();
        var existingSetting = await userSettingsRepository.GetFirstOrDefaultAsync(
            s => s.UserId == userId && s.SettingName == settingsDto.SettingName);

        bool result;
        if (existingSetting != null)
        {
            if (settingsDto.SettingValue == existingSetting.SettingValue)
            {
                return new SuccessfulResult<bool>(true);
            }
            existingSetting.SettingValue = settingsDto.SettingValue;
            existingSetting.UpdatedAt = now;
            result = await userSettingsRepository.UpdateAsync(existingSetting);
        }
        else
        {
            // Create the persistence model explicitly so API fields stay separate from server-owned fields.
            // If UserSettingsDto gains new writeable fields later, review this initializer instead of relying on implicit mapping.
            var newSetting = new UserSettings
            {
                UserId = userId,
                SettingName = settingsDto.SettingName,
                SettingValue = settingsDto.SettingValue,
                CreatedAt = now,
                UpdatedAt = now
            };
            result = await userSettingsRepository.InsertAsync(newSetting);
        }

        return result ? new SuccessfulResult<bool>(true) : new FailedResult<bool>(false, "0 rows affected");
    }

    [HttpGet]
    [AllowAnonymous]
    public ApiResult<List<Language>> Languages()
    {
        return new SuccessfulResult<List<Language>>(configurationService.SupportedLanguages);
    }
}
