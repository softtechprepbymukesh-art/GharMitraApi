using System.Data;
using GharMitra.Application.Categories.DTOs;
using GharMitra.Application.Categories.Interfaces;
using GharMitra.Application.Common;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class CategoryService : ICategoryService
{
    private const string SelectColumns =
        "Id, Name, Description, Type, IsActive, DisplayOrder, CreatedAt, UpdatedAt";

    /// <summary>
    /// A family sees the shared system defaults (FamilyId IS NULL) plus the
    /// categories it created itself. It can never see another family's.
    /// </summary>
    private const string VisibilitySql = "(FamilyId IS NULL OR FamilyId = @FamilyId)";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public CategoryService(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    private int FamilyId
    {
        get
        {
            if (!_currentUser.IsAuthenticated)
                throw new UnauthorizedAccessException("Authentication is required.");

            return _currentUser.FamilyId;
        }
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand(
                $"SELECT {SelectColumns} FROM Categories WHERE {VisibilitySql} " +
                "ORDER BY Type, DisplayOrder, Name")
            .With("@FamilyId", SqlDbType.Int, FamilyId);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"SELECT {SelectColumns} FROM Categories WHERE Id = @Id AND {VisibilitySql}")
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@FamilyId", SqlDbType.Int, FamilyId);

        var items = await ReadAllAsync(command, cancellationToken);
        return items.Count == 0 ? null : items[0];
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        var familyId = FamilyId;
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (await NameExistsAsync(connection, familyId, name, request.Type, null, cancellationToken))
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
                (Id, Name, Description, Type, IsActive, DisplayOrder, FamilyId, IsSystem,
                 CreatedAt, UpdatedAt)
            VALUES
                (@Id, @Name, @Description, @Type, @IsActive, @DisplayOrder, @FamilyId, 0,
                 @CreatedAt, NULL)
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, category.Id)
            .With("@Name", SqlDbType.NVarChar, category.Name, 100)
            .With("@Description", SqlDbType.NVarChar, category.Description, 500)
            .With("@Type", SqlDbType.Int, (int)category.Type)
            .With("@IsActive", SqlDbType.Bit, category.IsActive)
            .With("@DisplayOrder", SqlDbType.Int, category.DisplayOrder)
            .With("@FamilyId", SqlDbType.Int, familyId)
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
        RequireManagePermission();

        var familyId = FamilyId;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        // Only the family's own categories are editable. A system default is
        // shared by every family and must not be changed by one of them.
        if (!await OwnCategoryExistsAsync(connection, familyId, id, cancellationToken))
            return false;

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        if (await NameExistsAsync(connection, familyId, name, request.Type, id, cancellationToken))
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
            WHERE Id = @Id AND FamilyId = @FamilyId
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@Name", SqlDbType.NVarChar, name, 100)
            .With("@Description", SqlDbType.NVarChar, request.Description?.Trim(), 500)
            .With("@Type", SqlDbType.Int, (int)request.Type)
            .With("@DisplayOrder", SqlDbType.Int, request.DisplayOrder)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        // Soft delete: category database se physically delete nahi hogi.
        await using var command = connection.CreateCommand(
            "UPDATE Categories SET IsActive = 0, UpdatedAt = @UpdatedAt " +
            "WHERE Id = @Id AND FamilyId = @FamilyId")
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@FamilyId", SqlDbType.Int, FamilyId)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private void RequireManagePermission()
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (!_currentUser.Has(Permission.CategoryManage))
            throw new UnauthorizedAccessException("You are not allowed to manage categories.");
    }

    private static async Task<bool> OwnCategoryExistsAsync(
        SqlConnection connection,
        int familyId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT 1 FROM Categories WHERE Id = @Id AND FamilyId = @FamilyId")
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@FamilyId", SqlDbType.Int, familyId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> NameExistsAsync(
        SqlConnection connection,
        int familyId,
        string name,
        CategoryType type,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            """
            SELECT 1 FROM Categories
            WHERE Name = @Name AND Type = @Type
              AND (FamilyId IS NULL OR FamilyId = @FamilyId)
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """)
            .With("@Name", SqlDbType.NVarChar, name, 100)
            .With("@Type", SqlDbType.Int, (int)type)
            .With("@FamilyId", SqlDbType.Int, familyId)
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
