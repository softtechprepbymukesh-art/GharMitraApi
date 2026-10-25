using System.Data;
using GharMitra.Application.Common;
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
        SELECT e.Id, e.FamilyId, e.FamilyMemberId,
               e.CategoryId, c.Name,
               e.SubCategoryId, sc.Name,
               e.Amount, e.ExpenseDate, e.NeedOrWant,
               e.PaymentMethodId, pm.MethodName,
               e.SpendingTypeId, st.TypeName,
               e.IsEssential, e.IsRecurring, e.AccountId, e.Notes,
               e.CreatedAt, e.UpdatedAt
        FROM Expenses e
        INNER JOIN Categories c       ON c.Id  = e.CategoryId
        LEFT  JOIN Categories sc      ON sc.Id = e.SubCategoryId
        INNER JOIN PaymentMethods pm  ON pm.Id = e.PaymentMethodId
        INNER JOIN SpendingTypes st   ON st.Id = e.SpendingTypeId
        """;

    private const string OrderBySql = "ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public ExpenseService(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        _currentUser = currentUser
            ?? throw new ArgumentNullException(nameof(currentUser));
    }

    /// <summary>
    /// Family Head sees every member. Anyone else is narrowed to their own rows,
    /// which is how the §5 privacy rule is enforced in the data layer.
    /// </summary>
    private TenantScope ReadScope()
    {
        if (!_currentUser.Has(Permission.ExpenseViewOwn) &&
            !_currentUser.Has(Permission.ExpenseViewFamily))
        {
            throw new UnauthorizedAccessException("You are not allowed to view expenses.");
        }

        return TenantScope.For(_currentUser, Permission.ExpenseViewFamily);
    }

    #region Read Operations

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"{SelectSql} WHERE {scope.Predicate("e")} {OrderBySql}");
        scope.AddParameters(command);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<ExpenseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, id, scope, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return Array.Empty<ExpenseDto>();

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"{SelectSql} WHERE {scope.Predicate("e")} AND e.CategoryId = @CategoryId {OrderBySql}")
            .With("@CategoryId", SqlDbType.UniqueIdentifier, categoryId);
        scope.AddParameters(command);

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<decimal> GetTotalExpensesByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return 0m;

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"""
             SELECT COALESCE(SUM(Amount), 0) FROM Expenses
             WHERE {scope.Predicate(string.Empty)} AND CategoryId = @CategoryId
             """)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, categoryId);
        scope.AddParameters(command);

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

        if (!_currentUser.Has(Permission.ExpenseRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to record expenses.");

        // Identity comes from the token, never from the request body.
        var familyId = _currentUser.FamilyId;
        var memberId = _currentUser.FamilyMemberId;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureExpenseCategoryAsync(connection, familyId, request.CategoryId, cancellationToken);
        await EnsureSubCategoryAsync(connection, familyId, request.SubCategoryId, cancellationToken);
        await EnsureLookupAsync(connection, "PaymentMethods", request.PaymentMethodId, "Payment method", cancellationToken);
        await EnsureLookupAsync(connection, "SpendingTypes", request.SpendingTypeId, "Spending type", cancellationToken);
        await EnsureAccountAsync(connection, familyId, request.AccountId, cancellationToken);

        await using var command = connection.CreateCommand(
            """
            INSERT INTO Expenses
                (FamilyId, FamilyMemberId, CategoryId, SubCategoryId, Amount, ExpenseDate,
                 NeedOrWant, PaymentMethodId, SpendingTypeId, IsEssential, IsRecurring,
                 Notes, AccountId, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@FamilyId, @FamilyMemberId, @CategoryId, @SubCategoryId, @Amount, @ExpenseDate,
                 @NeedOrWant, @PaymentMethodId, @SpendingTypeId, @IsEssential, @IsRecurring,
                 @Notes, @AccountId, @CreatedAt, NULL)
            """)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@FamilyMemberId", SqlDbType.Int, memberId)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, request.CategoryId)
            .With("@SubCategoryId", SqlDbType.UniqueIdentifier, request.SubCategoryId)
            .With("@Amount", SqlDbType.Decimal, request.Amount)
            .With("@ExpenseDate", SqlDbType.DateTime2, request.ExpenseDate)
            .With("@NeedOrWant", SqlDbType.TinyInt, (byte)request.NeedOrWant)
            .With("@PaymentMethodId", SqlDbType.UniqueIdentifier, request.PaymentMethodId)
            .With("@SpendingTypeId", SqlDbType.UniqueIdentifier, request.SpendingTypeId)
            .With("@IsEssential", SqlDbType.Bit, request.IsEssential)
            .With("@IsRecurring", SqlDbType.Bit, request.IsRecurring)
            .With("@Notes", SqlDbType.NVarChar, CleanNotes(request.Notes), 500)
            .With("@AccountId", SqlDbType.UniqueIdentifier, request.AccountId)
            .With("@CreatedAt", SqlDbType.DateTime2, DateTime.UtcNow);

        var newId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        return (await GetByIdAsync(connection, newId, TenantScope.Own(_currentUser), cancellationToken))!;
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

        if (!_currentUser.Has(Permission.ExpenseRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to change expenses.");

        var familyId = _currentUser.FamilyId;

        // A member may only edit their own rows; the Family Head may edit any in the family.
        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureExpenseCategoryAsync(connection, familyId, request.CategoryId, cancellationToken);
        await EnsureSubCategoryAsync(connection, familyId, request.SubCategoryId, cancellationToken);
        await EnsureLookupAsync(connection, "PaymentMethods", request.PaymentMethodId, "Payment method", cancellationToken);
        await EnsureLookupAsync(connection, "SpendingTypes", request.SpendingTypeId, "Spending type", cancellationToken);
        await EnsureAccountAsync(connection, familyId, request.AccountId, cancellationToken);

        await using var command = connection.CreateCommand(
            $"""
             UPDATE Expenses
             SET CategoryId = @CategoryId,
                 SubCategoryId = @SubCategoryId,
                 Amount = @Amount,
                 ExpenseDate = @ExpenseDate,
                 NeedOrWant = @NeedOrWant,
                 PaymentMethodId = @PaymentMethodId,
                 SpendingTypeId = @SpendingTypeId,
                 IsEssential = @IsEssential,
                 IsRecurring = @IsRecurring,
                 Notes = @Notes,
                 AccountId = @AccountId,
                 UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND {scope.Predicate(string.Empty)}
             """)
            .With("@Id", SqlDbType.Int, id)
            .With("@CategoryId", SqlDbType.UniqueIdentifier, request.CategoryId)
            .With("@SubCategoryId", SqlDbType.UniqueIdentifier, request.SubCategoryId)
            .With("@Amount", SqlDbType.Decimal, request.Amount)
            .With("@ExpenseDate", SqlDbType.DateTime2, request.ExpenseDate)
            .With("@NeedOrWant", SqlDbType.TinyInt, (byte)request.NeedOrWant)
            .With("@PaymentMethodId", SqlDbType.UniqueIdentifier, request.PaymentMethodId)
            .With("@SpendingTypeId", SqlDbType.UniqueIdentifier, request.SpendingTypeId)
            .With("@IsEssential", SqlDbType.Bit, request.IsEssential)
            .With("@IsRecurring", SqlDbType.Bit, request.IsRecurring)
            .With("@Notes", SqlDbType.NVarChar, CleanNotes(request.Notes), 500)
            .With("@AccountId", SqlDbType.UniqueIdentifier, request.AccountId)
            .With("@UpdatedAt", SqlDbType.DateTime2, DateTime.UtcNow);
        scope.AddParameters(command);

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

        if (!_currentUser.Has(Permission.ExpenseRecordOwn))
            throw new UnauthorizedAccessException("You are not allowed to delete expenses.");

        var scope = ReadScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"DELETE FROM Expenses WHERE Id = @Id AND {scope.Predicate(string.Empty)}")
            .With("@Id", SqlDbType.Int, id);
        scope.AddParameters(command);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    #endregion

    private static async Task<ExpenseDto?> GetByIdAsync(
        SqlConnection connection,
        int id,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand($"{SelectSql} WHERE e.Id = @Id AND {scope.Predicate("e")}")
            .With("@Id", SqlDbType.Int, id);
        scope.AddParameters(command);

        var items = await ReadAllAsync(command, cancellationToken);
        return items.Count == 0 ? null : items[0];
    }

    /// <summary>
    /// The category must be an active expense category that is either a system
    /// default or belongs to this family. This is what stops one family pointing
    /// a row at another family's category.
    /// </summary>
    private static async Task EnsureExpenseCategoryAsync(
        SqlConnection connection,
        int familyId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await CategoryIsUsableAsync(connection, familyId, categoryId, cancellationToken))
        {
            throw new ArgumentException(
                $"Expense category with ID {categoryId} was not found.");
        }
    }

    private static async Task EnsureSubCategoryAsync(
        SqlConnection connection,
        int familyId,
        Guid? subCategoryId,
        CancellationToken cancellationToken)
    {
        if (subCategoryId is null)
            return;

        if (!await CategoryIsUsableAsync(connection, familyId, subCategoryId.Value, cancellationToken))
        {
            throw new ArgumentException(
                $"Expense sub-category with ID {subCategoryId} was not found.");
        }
    }

    private static async Task<bool> CategoryIsUsableAsync(
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
            .With("@Type", SqlDbType.Int, (int)CategoryType.Expense)
            .With("@FamilyId", SqlDbType.Int, familyId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task EnsureLookupAsync(
        SqlConnection connection,
        string table,
        Guid id,
        string label,
        CancellationToken cancellationToken)
    {
        // table is a compile-time constant from this class, never user input.
        await using var command = connection
            .CreateCommand($"SELECT 1 FROM {table} WHERE Id = @Id AND IsActive = 1")
            .With("@Id", SqlDbType.UniqueIdentifier, id);

        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new ArgumentException($"{label} with ID {id} was not found.");
    }

    private static async Task EnsureAccountAsync(
        SqlConnection connection,
        int familyId,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        if (accountId is null)
            return;

        await using var command = connection
            .CreateCommand("SELECT 1 FROM Accounts WHERE Id = @Id AND FamilyId = @FamilyId AND IsActive = 1")
            .With("@Id", SqlDbType.UniqueIdentifier, accountId.Value)
            .With("@FamilyId", SqlDbType.Int, familyId);

        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            throw new ArgumentException(
                $"Account with ID {accountId} was not found in this family.");
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
                reader.GetNullableString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetNullableString(6),
                reader.GetDecimal(7),
                reader.GetDateTime(8),
                (NeedOrWant)reader.GetByte(9),
                reader.GetGuid(10),
                reader.GetNullableString(11),
                reader.GetGuid(12),
                reader.GetNullableString(13),
                reader.GetBoolean(14),
                reader.GetBoolean(15),
                reader.IsDBNull(16) ? null : reader.GetGuid(16),
                reader.GetNullableString(17),
                reader.GetDateTime(18),
                reader.GetNullableDateTime(19)));
        }

        return items;
    }

    private static string? CleanNotes(string? notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    #endregion

    #region Validation

    private static void ValidateCreateRequest(CreateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        ValidateCommon(
            request.CategoryId, request.Amount, request.ExpenseDate,
            request.PaymentMethodId, request.SpendingTypeId, request.NeedOrWant);
    }

    private static void ValidateUpdateRequest(UpdateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        ValidateCommon(
            request.CategoryId, request.Amount, request.ExpenseDate,
            request.PaymentMethodId, request.SpendingTypeId, request.NeedOrWant);
    }

    private static void ValidateCommon(
        Guid categoryId,
        decimal amount,
        DateTime expenseDate,
        Guid paymentMethodId,
        Guid spendingTypeId,
        NeedOrWant needOrWant)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.");

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        // Compared against local-date end-of-day rather than UtcNow: an expense
        // recorded at 00:30 IST is "today" locally but still yesterday in UTC.
        // See Docs/GAP_ANALYSIS.md H7.
        if (expenseDate.Date > DateTime.UtcNow.AddHours(14).Date)
            throw new ArgumentException("Expense date cannot be in the future.");

        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException("Payment method is required.");

        if (spendingTypeId == Guid.Empty)
            throw new ArgumentException("Spending type is required.");

        if (needOrWant is not (NeedOrWant.Need or NeedOrWant.Want))
            throw new ArgumentException("Need or Want must be 1 (Need) or 2 (Want).");
    }

    #endregion
}
