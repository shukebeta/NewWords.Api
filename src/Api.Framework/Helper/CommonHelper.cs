using System.Security.Cryptography;
using System.Text;

namespace Api.Framework.Helper;

/// <summary>
/// Common Util functions collection
/// </summary>
public static class CommonHelper
{
    public static string CalculateSha256Hash(string input)
    {
        using SHA256 sha256 = SHA256.Create();
        // Convert the input string to a byte array and compute the hash
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        // Convert the byte array to a hexadecimal string
        StringBuilder sb = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    } 
 
    public static string CalculateMd5Hash(string input)
    {
        // Use input string to calculate MD5 hash
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        // Convert the byte array to hexadecimal string
        StringBuilder sb = new StringBuilder();
        foreach (var t in hashBytes)
        {
            sb.Append(t.ToString("X2"));
        }

        return sb.ToString().ToLower();
    }

}