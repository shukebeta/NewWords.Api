
using NewWords.Api.Entities;

namespace NewWords.Api.Models;

public class UserSession
{
    public string Token { get; init; } = string.Empty;
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NativeLanguage { get; set; } = string.Empty;
    public string CurrentLearningLanguage { get; set; } = string.Empty;

    public UserSession From(User user)
    {
        UserId = user.Id;
        Email = user.Email;
        NativeLanguage = user.NativeLanguage;
        CurrentLearningLanguage = user.CurrentLearningLanguage;
        return this;
    }
}
