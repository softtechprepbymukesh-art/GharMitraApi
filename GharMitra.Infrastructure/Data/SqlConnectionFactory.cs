using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Data;

/// <summary>
/// Opens a pooled ADO.NET connection to SQL Server using the configured connection string.
/// The database and its tables must already exist (see Database/01_Schema.sql).
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "Connection string is required.",
                nameof(connectionString));

        _connectionString = connectionString;
    }

    public async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
