
namespace Api.Framework.Helper;

public static class GravatarHelper
{
    public static string GetGravatarUrl(string email)
    {
        // Trim leading and trailing whitespace from
        // an email address and force all characters
        // to lower case
        string address = email.Trim().ToLower();

        // Create a SHA256 hash of the final string
        string hash = CommonHelper.CalculateSha256Hash(address);

        // Grab the actual image URL
        return $"https://www.gravatar.com/avatar/{hash}";
    }
}