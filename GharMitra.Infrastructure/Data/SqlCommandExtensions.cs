using System.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Data;

internal static class SqlCommandExtensions
{
    private const int CommandTimeoutSeconds = 30;

    public static SqlCommand CreateCommand(
        this SqlConnection connection,
        string sql,
        SqlTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Transaction = transaction;
        return command;
    }

    public static SqlCommand With(
        this SqlCommand command,
        string name,
        SqlDbType type,
        object? value,
        int size = 0)
    {
        var parameter = size > 0
            ? command.Parameters.Add(name, type, size)
            : command.Parameters.Add(name, type);

        if (type == SqlDbType.Decimal)
        {
            parameter.Precision = 18;
            parameter.Scale = 2;
        }

        parameter.Value = value ?? DBNull.Value;
        return command;
    }

    public static string? GetNullableString(this SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static DateTime? GetNullableDateTime(this SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
