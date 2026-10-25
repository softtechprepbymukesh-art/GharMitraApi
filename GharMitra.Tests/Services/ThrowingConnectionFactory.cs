using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Tests.Services;

/// <summary>
/// Fails the test if a service opens a connection. Used to prove that input validation
/// and guard clauses reject bad requests before any database work happens.
/// </summary>
internal sealed class ThrowingConnectionFactory : ISqlConnectionFactory
{
    public Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        => throw new Xunit.Sdk.XunitException("Database must not be touched for this input.");
}
