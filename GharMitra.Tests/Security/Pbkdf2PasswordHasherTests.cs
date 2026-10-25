using GharMitra.Infrastructure.Security;

namespace GharMitra.Tests.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = _hasher.Hash("correct horse battery");

        Assert.True(_hasher.Verify("correct horse battery", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = _hasher.Hash("correct horse battery");

        Assert.False(_hasher.Verify("Correct horse battery", hash));
        Assert.False(_hasher.Verify("", hash));
    }

    [Fact]
    public void Hash_IsSaltedPerCall()
    {
        // Two users with the same password must not share a hash, or one crack
        // reveals both.
        var a = _hasher.Hash("same-password");
        var b = _hasher.Hash("same-password");

        Assert.NotEqual(a, b);
        Assert.True(_hasher.Verify("same-password", a));
        Assert.True(_hasher.Verify("same-password", b));
    }

    [Fact]
    public void Hash_CarriesItsOwnParameters()
    {
        var parts = _hasher.Hash("whatever").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 100_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_BlankPassword_Throws(string password)
        => Assert.Throws<ArgumentException>(() => _hasher.Hash(password));

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$notanumber$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2$1000$!!!notbase64!!!$aGFzaA==")]
    [InlineData("SCRYPT$1000$c2FsdA==$aGFzaA==")]
    public void Verify_MalformedStoredHash_ReturnsFalseInsteadOfThrowing(string stored)
        => Assert.False(_hasher.Verify("anything", stored));
}

public class JwtOptionsTests
{
    [Fact]
    public void Validate_MissingKey_ExplainsHowToSetIt()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new JwtOptions { SigningKey = "" }.Validate());

        Assert.Contains("user-secrets", ex.Message);
    }

    [Fact]
    public void Validate_ShortKey_IsRejected()
        => Assert.Throws<InvalidOperationException>(
            () => new JwtOptions { SigningKey = "too-short" }.Validate());

    [Fact]
    public void Validate_AdequateKey_Passes()
        => new JwtOptions { SigningKey = new string('k', 32) }.Validate();
}
