using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Data;

/// <summary>
/// Creates opened SQL Server connections. Services never build connections themselves.
/// </summary>
public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
