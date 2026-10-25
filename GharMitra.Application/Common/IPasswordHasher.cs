namespace GharMitra.Application.Common;

public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash: PBKDF2$iterations$salt$hash.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification against a stored hash.</summary>
    bool Verify(string password, string storedHash);
}
