using System.Security.Cryptography;
using System.Text;
namespace Api.Framework.Helper;

public static class TextEncryptionHelper
{
    private static Aes GetCryptoProvider(string keyStr)
    {
        using var sha256 = SHA256.Create();
        var key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyStr));
        var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        return aes;
    }

    public static string Encrypt(string? plainString, string keyStr)
    {
        var data = Encoding.UTF8.GetBytes(plainString ?? string.Empty);
        using var aes = GetCryptoProvider(keyStr);
        var iv = aes.IV;
        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);
        var result = new byte[iv.Length + encryptedData.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encryptedData, 0, result, iv.Length, encryptedData.Length);
        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string encryptedString, string keyStr)
    {
        var data = Convert.FromBase64String(encryptedString);
        using var aes = GetCryptoProvider(keyStr);
        var iv = new byte[aes.BlockSize / 8];
        var encryptedData = new byte[data.Length - iv.Length];
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(data, iv.Length, encryptedData, 0, encryptedData.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decryptedData = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        return Encoding.UTF8.GetString(decryptedData);
    }
}
