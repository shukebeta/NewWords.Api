namespace Api.Framework.Models;

public class JwtConfig
{
    public string SymmetricSecurityKey { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;
}