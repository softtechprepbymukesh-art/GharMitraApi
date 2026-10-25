using System.Data;
using GharMitra.Application.Categories.DTOs;
using GharMitra.Application.Categories.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class CategoryService : ICategoryService
{
    private const string SelectColumns =
        "Id, Name, Description, Type, IsActive, DisplayOrder, CreatedAt, UpdatedAt";

    private readonly ISqlConnectionFactory _connectionFactory;

    public CategoryService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"SELECT {SelectColumns} FROM Categories ORDER BY Type, DisplayOrder, Name");

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"SELECT {SelectColumns} FROM Categories WHERE Id = @Id")
            .With("@Id", SqlDbType.UniqueIdentifier, id);

        var items = await ReadAllAsync(command, cancellationToken);
        return items.Count == 0 ? null : items[0];
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (await NameExistsAsync(connection, name, request.Type, null, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Category '{name}' already exists for {request.Type}.");
        }

        var category = new Category(
            name,
            request.Type,
            request.Description?.Trim(),
            request.DisplayOrder);

        await using var command = connection.CreateCommand(
            """
            INSERT INTO Categories
                (Id, Name, Description, Type, IsActive, DisplayOrder, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @Name, @Description, @Type, @IsActive, @DisplayOrder, @CreatedAt, NULL)
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, category.Id)
            .With("@Name", SqlDbType.NVarChar, category.Name, 100)
            .With("@Description", SqlDbType.NVarChar, category.Description, 500)
            .With("@Type", SqlDbType.Int, (int)category.Type)
            .With("@IsActive", SqlDbType.Bit, category.IsActive)
            .With("@DisplayOrder", SqlDbType.Int, category.DisplayOrder)
            .With("@CreatedAt", SqlDbType.DateTime2, category.CreatedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.Type,
            category.IsActive,
            category.DisplayOrder,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (!await ExistsAsync(connection, id, cancellationToken))
        {
            return false;
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        if (await NameExistsAsync(connection, name, request.Type, id, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Category '{name}' already exists for {request.Type}.");
        }

        await using var command = connection.CreateCommand(
            """
            UPDATE Categories
            SET Name = @Name,
                Description = @Description,
                Type = @Type,
                DisplayOrder = @DisplayOrder,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@Name", SqlDbType.NVarChar, name, 100)
            .With("@Description", SqlDbType.NVarChar, request.Description?.Trim(), 500)
            .With("@Type", SqlDbType.Int, (int)request.Type)
            .With("@DisplayOrder", SqlDbType.Int, request.DisplayOrder)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        // Soft delete: category database se physically delete nahi hogi.
        await using var command = connection.CreateCommand(
            "UPDATE Categories SET IsActive = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id")
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<bool> ExistsAsync(
        SqlConnection connection,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT 1 FROM Categories WHERE Id = @Id")
            .With("@Id", SqlDbType.UniqueIdentifier, id);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> NameExistsAsync(
        SqlConnection connection,
        string name,
        CategoryType type,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            """
            SELECT 1 FROM Categories
            WHERE Name = @Name AND Type = @Type AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """)
            .With("@Name", SqlDbType.NVarChar, name, 100)
            .With("@Type", SqlDbType.Int, (int)type)
            .With("@ExcludeId", SqlDbType.UniqueIdentifier, excludeId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<IReadOnlyList<CategoryDto>> ReadAllAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<CategoryDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CategoryDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetNullableString(2),
                (CategoryType)reader.GetInt32(3),
                reader.GetBoolean(4),
                reader.GetInt32(5),
                reader.GetDateTime(6),
                reader.GetNullableDateTime(7)));
        }

        return items;
    }
}
