using FluentAssertions;
using NewWords.Api.Entities;
using NewWords.Api.Mappers;
using Xunit;

namespace NewWords.Api.Tests.Services;

public class UserSettingsMappingsTests
{
    [Fact]
    public void ToDto_ShouldMapApiFieldsExplicitly()
    {
        var entity = new UserSettings
        {
            UserId = 42,
            SettingName = "targetLanguage",
            SettingValue = "ja",
            CreatedAt = 100,
            UpdatedAt = 200
        };

        var dto = UserSettingsMappings.ToDto(entity);

        dto.SettingName.Should().Be("targetLanguage");
        dto.SettingValue.Should().Be("ja");
    }
}
