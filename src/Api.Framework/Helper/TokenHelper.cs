using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Api.Framework.Helper;

public static class TokenHelper
{
    public static string JwtTokenGenerator(Claim[] claims, string jwtIssuer, string jwtKey, int expireInDays,
        int notBeforeInMinutes = 5)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: null,
            claims: claims,
            notBefore: DateTime.Now.AddMinutes(-notBeforeInMinutes), // in case user's computer time is inaccurate
            expires: DateTime.Now.AddDays(expireInDays),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static Claim[] ClaimsGenerator(long id, string username, string email)
    {
        return
        [
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, id.ToString()),
        ];
    }
}
