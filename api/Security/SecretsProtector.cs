using System.Security.Cryptography;
using System.Text;

namespace ScribAi.Api.Security;

public interface ISecretsProtector
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] encrypted);
}

public class SecretsProtector : ISecretsProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public SecretsProtector(IConfiguration cfg)
    {
        var b64 = cfg["SCRIBAI_SECRETS_KEY"]
            ?? throw new InvalidOperationException("SCRIBAI_SECRETS_KEY env var is required");

        byte[] key;
        try { key = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new InvalidOperationException("SCRIBAI_SECRETS_KEY must be base64"); }

        if (key.Length != 32)
            throw new InvalidOperationException($"SCRIBAI_SECRETS_KEY must decode to 32 bytes (got {key.Length})");

        _key = key;
    }

    public byte[] Encrypt(string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var result = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, result, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipher.Length, TagSize);
        return result;
    }

    public string Decrypt(byte[] encrypted)
    {
        if (encrypted is null || encrypted.Length < NonceSize + TagSize)
            throw new ArgumentException("Invalid ciphertext", nameof(encrypted));

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherLen = encrypted.Length - NonceSize - TagSize;
        var cipher = new byte[cipherLen];

        Buffer.BlockCopy(encrypted, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encrypted, NonceSize, cipher, 0, cipherLen);
        Buffer.BlockCopy(encrypted, NonceSize + cipherLen, tag, 0, TagSize);

        var plain = new byte[cipherLen];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
