using System.Data;
using GharMitra.Application.Common;
using GharMitra.Application.Incomes.DTOs;
using GharMitra.Application.Incomes.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class IncomeService : IIncomeService
{
    private const string SelectSql =
        """
        SELECT i.Id, i.SourceName, i.Amount, i.IncomeDate, i.CategoryId, c.Name,
               i.IsActive, i.CreatedAt, i.UpdatedAt
        FROM Incomes i
        INNER JOIN Categories c ON c.Id = i.CategoryId
        """;

    private const string OrderBySql = "ORDER BY i.IncomeDate DESC, i.CreatedAt DESC";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public IncomeService(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        _currentUser = currentUser
            ?? throw new ArgumentNullException(nameof(currentUser));
    }

    /// <summary>
    /// Income is the most privacy-sensitive record in the system: §5 says one
    /// member must not see another's salary. Only income.view.family lifts the
    /// scope to the whole family.
    /// </summary>
    private TenantScope ReadScope()
    {
        // A caller with neither permission has no business on this endpoint at
        // all. A Child holds only expense permissions, so income is closed to
        // them outright rather than merely returning an empty list.
        if (!_currentUser.Has(Permission.IncomeViewOwn) &&
            !_currentUser.Has(Permission.IncomeViewFamily))
        {
            throw new UnauthorizedAccessException("You are not allowed to view income.");
        }

        return TenantScope.For(_currentUser, Permission.IncomeViewFamily);
    }

    public async Task<IReadOnlyList<IncomeDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"{SelectSql} WHERE {scope.Predicate("i")} AND i.IsActive = 1 {OrderBySql}");
        scope.AddParameters(command);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<IncomeDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, id, scope, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"""
             {SelectSql}
             WHERE {scope.Predicate("i")} AND i.CategoryId = @CategoryId AND i.IsActive = 1
             {OrderBySql}
             """)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, categoryId);
        scope.AddParameters(command);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<IncomeDto> CreateAsync(
        CreateIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        if (!_currentUser.Has(Permission.IncomeRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to record income.");

        var familyId = _currentUser.FamilyId;
        var memberId = _currentUser.FamilyMemberId;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureIncomeCategoryAsync(connection, familyId, request.CategoryId, cancellationToken);

        // The domain entity enforces the income business rules and assigns Id / CreatedAt.
        var income = new Income(
            request.SourceName,
            request.Amount,
            request.IncomeDate,
            request.CategoryId);

        await using (var command = connection.CreateCommand(
            """
            INSERT INTO Incomes
                (Id, SourceName, Amount, IncomeDate, CategoryId, FamilyId, FamilyMemberId,
                 IsActive, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @SourceName, @Amount, @IncomeDate, @CategoryId, @FamilyId, @FamilyMemberId,
                 @IsActive, @CreatedAt, NULL)
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, income.Id)
            .With("@SourceName", SqlDbType.NVarChar, income.SourceName, 256)
            .With("@Amount", SqlDbType.Decimal, income.Amount)
            .With("@IncomeDate", SqlDbType.DateTime2, income.IncomeDate)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, income.CategoryId)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@FamilyMemberId", SqlDbType.Int, memberId)
            .With("@IsActive", SqlDbType.Bit, income.IsActive)
            .With("@CreatedAt", SqlDbType.DateTime2, income.CreatedAt))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Reload with category
        return (await GetByIdAsync(
            connection, income.Id, TenantScope.Own(_currentUser), cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateRequest(request);

        if (!_currentUser.Has(Permission.IncomeRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to change income.");

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureIncomeCategoryAsync(
            connection, _currentUser.FamilyId, request.CategoryId, cancellationToken);

        // The domain entity enforces the income business rules on the new values.
        var income = new Income(
            request.SourceName,
            request.Amount,
            request.IncomeDate,
            request.CategoryId);

        await using var command = connection.CreateCommand(
            $"""
             UPDATE Incomes
             SET SourceName = @SourceName,
                 Amount = @Amount,
                 IncomeDate = @IncomeDate,
                 CategoryId = @CategoryId,
                 UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND {scope.Predicate(string.Empty)}
             """)
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@SourceName", SqlDbType.NVarChar, income.SourceName, 256)
            .With("@Amount", SqlDbType.Decimal, income.Amount)
            .With("@IncomeDate", SqlDbType.DateTime2, income.IncomeDate)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, income.CategoryId)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);
        scope.AddParameters(command);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.Has(Permission.IncomeRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to delete income.");

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"DELETE FROM Incomes WHERE Id = @Id AND {scope.Predicate(string.Empty)}")
            .With("@Id", SqlDbType.UniqueIdentifier, id);
        scope.AddParameters(command);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => SetActiveAsync(id, false, cancellationToken);

    public Task<bool> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => SetActiveAsync(id, true, cancellationToken);

    private async Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.Has(Permission.IncomeRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to change income.");

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"""
             UPDATE Incomes SET IsActive = @IsActive, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND {scope.Predicate(string.Empty)}
             """)
            .With("@Id", SqlDbType.UniqueIdentifier, id)
            .With("@IsActive", SqlDbType.Bit, isActive)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);
        scope.AddParameters(command);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<IncomeDto?> GetByIdAsync(
        SqlConnection connection,
        Guid id,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand($"{SelectSql} WHERE i.Id = @Id AND {scope.Predicate("i")}")
            .With("@Id", SqlDbType.UniqueIdentifier, id);
        scope.AddParameters(command);

        var items = await ReadAllAsync(command, cancellationToken);
        return items.Count == 0 ? null : items[0];
    }

    private static async Task EnsureIncomeCategoryAsync(
        SqlConnection connection,
        int familyId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            """
            SELECT 1 FROM Categories
            WHERE Id = @Id AND Type = @Type AND IsActive = 1
              AND (FamilyId IS NULL OR FamilyId = @FamilyId)
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, categoryId)
            .With("@Type", SqlDbType.Int, (int)CategoryType.Income)
            .With("@FamilyId", SqlDbType.Int, familyId);

        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            throw new ArgumentException(
                $"Income category with ID {categoryId} was not found.");
        }
    }

    private static async Task<IReadOnlyList<IncomeDto>> ReadAllAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<IncomeDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new IncomeDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetDateTime(3),
                reader.GetGuid(4),
                reader.GetNullableString(5),
                reader.GetBoolean(6),
                reader.GetDateTime(7),
                reader.GetNullableDateTime(8)));
        }

        return items;
    }

    private static void ValidateCreateRequest(CreateIncomeRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Validate(request.SourceName, request.Amount, request.IncomeDate, request.CategoryId);
    }

    private static void ValidateUpdateRequest(UpdateIncomeRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Validate(request.SourceName, request.Amount, request.IncomeDate, request.CategoryId);
    }

    private static void Validate(
        string sourceName, decimal amount, DateTime incomeDate, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name is required.", nameof(sourceName));

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        // Local-date tolerant: see Docs/GAP_ANALYSIS.md H7.
        if (incomeDate.Date > DateTime.UtcNow.AddHours(14).Date)
            throw new ArgumentException("Income date cannot be in the future.", nameof(incomeDate));

        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(categoryId));
    }
}
