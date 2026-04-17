using Microsoft.Extensions.Configuration;
using ScribAi.Api.Security;

namespace ScribAi.Api.Tests;

public class SecretsProtectorTests
{
    private static SecretsProtector Build(string? b64Key = null)
    {
        var key = b64Key ?? Convert.ToBase64String(new byte[32]);
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SCRIBAI_SECRETS_KEY"] = key })
            .Build();
        return new SecretsProtector(cfg);
    }

    [Fact]
    public void Roundtrip_preserves_value()
    {
        var p = Build();
        var cipher = p.Encrypt("super-secret-api-key");
        var plain = p.Decrypt(cipher);
        Assert.Equal("super-secret-api-key", plain);
    }

    [Fact]
    public void Different_invocations_produce_different_ciphertexts()
    {
        var p = Build();
        var a = p.Encrypt("same");
        var b = p.Encrypt("same");
        Assert.NotEqual(Convert.ToBase64String(a), Convert.ToBase64String(b));
    }

    [Fact]
    public void Missing_key_throws()
    {
        var cfg = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => new SecretsProtector(cfg));
    }

    [Fact]
    public void Wrong_length_key_throws()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SCRIBAI_SECRETS_KEY"] = Convert.ToBase64String(new byte[16]) })
            .Build();
        Assert.Throws<InvalidOperationException>(() => new SecretsProtector(cfg));
    }

    [Fact]
    public void Tampered_ciphertext_fails_decrypt()
    {
        var p = Build();
        var cipher = p.Encrypt("x");
        cipher[cipher.Length - 1] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() => p.Decrypt(cipher));
    }
}
