
using NewWords.Api.Entities;

namespace NewWords.Api.Models;

public class UserSession
{
    public string Token { get; init; } = string.Empty;
    public long UserId { get; set; }
    public string Email { get; set; }
    public string NativeLanguage { get; set; }
    public string CurrentLearningLanguage { get; set; }

    public UserSession From(User user)
    {
        UserId = user.UserId;
        Email = user.Email;
        NativeLanguage = user.NativeLanguage;
        CurrentLearningLanguage = user.CurrentLearningLanguage;
        return this;
    }
}
