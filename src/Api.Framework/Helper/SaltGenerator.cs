using System.Security.Cryptography;

namespace Api.Framework.Helper;
public static class SaltGenerator
{
    private static byte[] GenerateSalt(int length)
    {
        // Create a byte array to hold the random salt
        byte[] salt = new byte[length];

        // Create an instance of RNGCryptoServiceProvider to generate the salt
        using var rngCsp = RandomNumberGenerator.Create();
        // Fill the byte array with random bytes
        rngCsp.GetBytes(salt);

        return salt;
    }

    public static string GenerateSaltString(int length)
    {
        // Generate the salt bytes
        byte[] saltBytes = GenerateSalt(length / 2);

        // Convert the byte array to a hexadecimal string
        return BitConverter.ToString(saltBytes).Replace("-", "");
    }

}
