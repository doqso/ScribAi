using ScribAi.Api.Auth;

namespace ScribAi.Api.Tests;

public class ApiKeyHasherTests
{
    [Fact]
    public void Generated_key_has_sk_prefix_and_is_hex()
    {
        var k = ApiKeyHasher.Generate();
        Assert.StartsWith("sk_", k);
        Assert.Equal(3 + 64, k.Length);
    }

    [Fact]
    public void Hash_is_stable_and_sha256_length()
    {
        var a = ApiKeyHasher.Hash("sk_test");
        var b = ApiKeyHasher.Hash("sk_test");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void Different_keys_produce_different_hashes()
    {
        Assert.NotEqual(ApiKeyHasher.Hash("a"), ApiKeyHasher.Hash("b"));
    }

    [Fact]
    public void Prefix_trims_to_10_chars_or_full_string_when_short()
    {
        Assert.Equal("sk_abcdefg", ApiKeyHasher.Prefix("sk_abcdefg1234567890"));
        Assert.Equal("short", ApiKeyHasher.Prefix("short"));
    }
}
