using System.Security.Cryptography;
using System.Text;

namespace ScribAi.Api.Auth;

public static class ApiKeyHasher
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "sk_" + Convert.ToHexStringLower(bytes);
    }

    public static string Hash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(bytes);
    }

    public static string Prefix(string key) => key.Length >= 10 ? key[..10] : key;
}
