using System.Data;
using GharMitra.Application.Expenses.DTOs;
using GharMitra.Application.Expenses.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class ExpenseService : IExpenseService
{
    private const string SelectSql =
        """
        SELECT e.Id, e.FamilyId, e.FamilyMemberId, e.CategoryId, e.Amount, e.ExpenseDate,
               e.ExpenseType, e.PaymentMethod, e.SpendingType, e.IsEssential, e.IsRecurring,
               e.Notes, e.CreatedAt, e.UpdatedAt, c.Name
        FROM Expenses e
        INNER JOIN Categories c ON c.Id = e.CategoryId
        """;

    private const string OrderBySql = "ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC";

    private readonly ISqlConnectionFactory _connectionFactory;

    public ExpenseService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    #region Read Operations

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand($"{SelectSql} {OrderBySql}");

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<ExpenseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return Array.Empty<ExpenseDto>();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"{SelectSql} WHERE e.CategoryId = @CategoryId {OrderBySql}")
            .With("@CategoryId", SqlDbType.UniqueIdentifier, categoryId);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<decimal> GetTotalExpensesByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return 0m;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand("SELECT COALESCE(SUM(Amount), 0) FROM Expenses WHERE CategoryId = @CategoryId")
            .With("@CategoryId", SqlDbType.UniqueIdentifier, categoryId);

        var total = await command.ExecuteScalarAsync(cancellationToken);
        return total is decimal value ? value : 0m;
    }

    #endregion

    #region Create

    public async Task<ExpenseDto> CreateAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureExpenseCategoryAsync(connection, request.CategoryId, cancellationToken);

        await EnsureFamilyAndMemberAsync(
            connection,
            request.FamilyId,
            request.FamilyMemberId,
            cancellationToken);

        await using var command = connection.CreateCommand(
            """
            INSERT INTO Expenses
                (FamilyId, FamilyMemberId, CategoryId, Amount, ExpenseDate, ExpenseType,
                 PaymentMethod, SpendingType, IsEssential, IsRecurring, Notes, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@FamilyId, @FamilyMemberId, @CategoryId, @Amount, @ExpenseDate, @ExpenseType,
                 @PaymentMethod, @SpendingType, @IsEssential, @IsRecurring, @Notes, @CreatedAt, NULL)
            """)
            .With("@FamilyId", SqlDbType.Int, request.FamilyId)
            .With("@FamilyMemberId", SqlDbType.Int, request.FamilyMemberId)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, request.CategoryId)
            .With("@Amount", SqlDbType.Decimal, request.Amount)
            .With("@ExpenseDate", SqlDbType.DateTime2, request.ExpenseDate)
            .With("@ExpenseType", SqlDbType.NVarChar, request.ExpenseType.Trim(), 50)
            .With("@PaymentMethod", SqlDbType.NVarChar, request.PaymentMethod.Trim(), 50)
            .With("@SpendingType", SqlDbType.NVarChar, request.SpendingType.Trim(), 50)
            .With("@IsEssential", SqlDbType.Bit, request.IsEssential)
            .With("@IsRecurring", SqlDbType.Bit, request.IsRecurring)
            .With("@Notes", SqlDbType.NVarChar, CleanNotes(request.Notes), 500)
            .With("@CreatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        var newId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        return (await GetByIdAsync(connection, newId, cancellationToken))!;
    }

    #endregion

    #region Update

    public async Task<bool> UpdateAsync(
        int id,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateRequest(request);

        if (id <= 0)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureExpenseCategoryAsync(connection, request.CategoryId, cancellationToken);

        await EnsureFamilyAndMemberAsync(
            connection,
            request.FamilyId,
            request.FamilyMemberId,
            cancellationToken);

        await using var command = connection.CreateCommand(
            """
            UPDATE Expenses
            SET FamilyId = @FamilyId,
                FamilyMemberId = @FamilyMemberId,
                CategoryId = @CategoryId,
                Amount = @Amount,
                ExpenseDate = @ExpenseDate,
                ExpenseType = @ExpenseType,
                PaymentMethod = @PaymentMethod,
                SpendingType = @SpendingType,
                IsEssential = @IsEssential,
                IsRecurring = @IsRecurring,
                Notes = @Notes,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """)
            .With("@Id", SqlDbType.Int, id)
            .With("@FamilyId", SqlDbType.Int, request.FamilyId)
            .With("@FamilyMemberId", SqlDbType.Int, request.FamilyMemberId)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, request.CategoryId)
            .With("@Amount", SqlDbType.Decimal, request.Amount)
            .With("@ExpenseDate", SqlDbType.DateTime2, request.ExpenseDate)
            .With("@ExpenseType", SqlDbType.NVarChar, request.ExpenseType.Trim(), 50)
            .With("@PaymentMethod", SqlDbType.NVarChar, request.PaymentMethod.Trim(), 50)
            .With("@SpendingType", SqlDbType.NVarChar, request.SpendingType.Trim(), 50)
            .With("@IsEssential", SqlDbType.Bit, request.IsEssential)
            .With("@IsRecurring", SqlDbType.Bit, request.IsRecurring)
            .With("@Notes", SqlDbType.NVarChar, CleanNotes(request.Notes), 500)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    #endregion

    #region Delete

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand("DELETE FROM Expenses WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    #endregion

    private static async Task<ExpenseDto?> GetByIdAsync(
        SqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand($"{SelectSql} WHERE e.Id = @Id")
            .With("@Id", SqlDbType.Int, id);

        var items = await ReadAllAsync(command, cancellationToken);
        return items.Count == 0 ? null : items[0];
    }

    private static async Task EnsureExpenseCategoryAsync(
        SqlConnection connection,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            "SELECT 1 FROM Categories WHERE Id = @Id AND Type = @Type AND IsActive = 1")
            .With("@Id", SqlDbType.UniqueIdentifier, categoryId)
            .With("@Type", SqlDbType.Int, (int)CategoryType.Expense);

        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            throw new ArgumentException(
                $"Expense category with ID {categoryId} was not found.",
                nameof(categoryId));
        }
    }

    private static async Task EnsureFamilyAndMemberAsync(
        SqlConnection connection,
        int familyId,
        int familyMemberId,
        CancellationToken cancellationToken)
    {
        bool? familyActive;
        await using (var command = connection
            .CreateCommand("SELECT IsActive FROM Families WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, familyId))
        {
            familyActive = await command.ExecuteScalarAsync(cancellationToken) as bool?;
        }

        if (familyActive != true)
        {
            throw new ArgumentException(
                $"Active family with ID {familyId} was not found.",
                nameof(familyId));
        }

        int memberFamilyId = 0;
        bool memberActive = false;
        bool memberFound = false;
        await using (var command = connection
            .CreateCommand("SELECT FamilyId, IsActive FROM FamilyMembers WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, familyMemberId))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                memberFound = true;
                memberFamilyId = reader.GetInt32(0);
                memberActive = reader.GetBoolean(1);
            }
        }

        if (!memberFound || !memberActive)
        {
            throw new ArgumentException(
                $"Active family member with ID {familyMemberId} was not found.",
                nameof(familyMemberId));
        }

        if (memberFamilyId != familyId)
        {
            throw new ArgumentException(
                "Family member does not belong to the specified family.");
        }
    }

    #region Mapping

    private static async Task<IReadOnlyList<ExpenseDto>> ReadAllAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<ExpenseDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ExpenseDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetGuid(3),
                reader.GetDecimal(4),
                reader.GetDateTime(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetBoolean(9),
                reader.GetBoolean(10),
                reader.GetNullableString(11),
                reader.GetDateTime(12),
                reader.GetNullableDateTime(13),
                reader.GetNullableString(14)));
        }

        return items;
    }

    private static string? CleanNotes(string? notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    #endregion

    #region Validation

    private static void ValidateCreateRequest(
        CreateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.FamilyId <= 0)
            throw new ArgumentException(
                "Family ID is required.");

        if (request.FamilyMemberId <= 0)
            throw new ArgumentException(
                "Family Member ID is required.");

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException(
                "Category ID is required.");

        if (request.Amount <= 0)
            throw new ArgumentException(
                "Amount must be greater than zero.");

        if (request.ExpenseDate > DateTime.UtcNow)
            throw new ArgumentException(
                "Expense date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(request.ExpenseType))
            throw new ArgumentException(
                "Expense type is required.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException(
                "Payment method is required.");

        if (string.IsNullOrWhiteSpace(request.SpendingType))
            throw new ArgumentException(
                "Spending type is required.");
    }

    private static void ValidateUpdateRequest(
        UpdateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.FamilyId <= 0)
            throw new ArgumentException(
                "Family ID is required.");

        if (request.FamilyMemberId <= 0)
            throw new ArgumentException(
                "Family Member ID is required.");

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException(
                "Category ID is required.");

        if (request.Amount <= 0)
            throw new ArgumentException(
                "Amount must be greater than zero.");

        if (request.ExpenseDate > DateTime.UtcNow)
            throw new ArgumentException(
                "Expense date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(request.ExpenseType))
            throw new ArgumentException(
                "Expense type is required.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException(
                "Payment method is required.");

        if (string.IsNullOrWhiteSpace(request.SpendingType))
            throw new ArgumentException(
                "Spending type is required.");
    }

    #endregion
}
