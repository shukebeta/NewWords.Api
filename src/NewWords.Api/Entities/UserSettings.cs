using SqlSugar;

namespace NewWords.Api.Entities;

public class UserSettings
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    public long UserId { get; set; }
    public string SettingName { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long? DeletedAt { get; set; }
    public long? UpdatedAt { get; set; }
}
