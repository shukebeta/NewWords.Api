using System.Security.Claims;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int Id => string.IsNullOrEmpty(GetClaimValue(ClaimTypes.NameIdentifier)) ? 0 : int.Parse(GetClaimValue(ClaimTypes.NameIdentifier));
    public string Username => GetClaimValue(ClaimTypes.Name);
    public string Email => GetClaimValue(ClaimTypes.Email);

    private string GetClaimValue(string claimType)
    {
        return httpContextAccessor.HttpContext?.User.FindFirstValue(claimType) ?? string.Empty;
    }
}
