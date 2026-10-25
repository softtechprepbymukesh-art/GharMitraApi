using System.Data;
using GharMitra.Application.Common;
using GharMitra.Application.Families.DTOs;
using GharMitra.Application.Families.Interfaces;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class FamilyService : IFamilyService
{
    private const string FamilySelectSql =
        "SELECT Id, Name, IsActive, CreatedAt FROM Families";

    private const string MemberSelectSql =
        "SELECT Id, FamilyId, Name, Role, IsFamilyHead, IsActive, CreatedAt FROM FamilyMembers";

    private const string MemberOrderBySql = "ORDER BY IsFamilyHead DESC, Name";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public FamilyService(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        _currentUser = currentUser
            ?? throw new ArgumentNullException(nameof(currentUser));
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

    /// <summary>
    /// Returns only the caller's own family. There is deliberately no way to
    /// list every family: that would be a cross-tenant read.
    /// </summary>
    public async Task<IReadOnlyList<FamilyDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var family = await GetByIdAsync(FamilyId, cancellationToken);

        return family is null ? Array.Empty<FamilyDto>() : [family];
    }

    public async Task<FamilyDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Any id other than the caller's own family is simply not found.
        if (id <= 0 || id != FamilyId)
            return null;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        return await LoadFamilyAsync(connection, id, cancellationToken);
    }

    /// <summary>
    /// Families are created by registration (Stage 0), which also creates the
    /// Family Head login. Creating a bare family through this API would produce
    /// a tenant nobody can sign in to.
    /// </summary>
    public Task<FamilyDto> CreateAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Use POST /api/v1/auth/register to create a family together with its Family Head.");

    public async Task<bool> UpdateAsync(
        int id,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        if (id <= 0 || id != FamilyId)
            return false;

        var name = ValidateName(request.Name);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "UPDATE Families SET Name = @Name, IsActive = @IsActive WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, id)
            .With("@Name", SqlDbType.NVarChar, name, 150)
            .With("@IsActive", SqlDbType.Bit, request.IsActive);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        if (id <= 0 || id != FamilyId)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int affected;
        await using (var command = connection.CreateCommand(
            "UPDATE Families SET IsActive = 0 WHERE Id = @Id", transaction)
            .With("@Id", SqlDbType.Int, id))
        {
            affected = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (affected == 0)
            return false;

        await using (var command = connection.CreateCommand(
            "UPDATE FamilyMembers SET IsActive = 0 WHERE FamilyId = @Id", transaction)
            .With("@Id", SqlDbType.Int, id))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        int familyId,
        CancellationToken cancellationToken = default)
    {
        if (familyId <= 0 || familyId != FamilyId)
            return Array.Empty<FamilyMemberDto>();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection
            .CreateCommand($"{MemberSelectSql} WHERE FamilyId = @FamilyId {MemberOrderBySql}")
            .With("@FamilyId", SqlDbType.Int, familyId);

        return await ReadMembersAsync(command, cancellationToken);
    }

    public async Task<FamilyMemberDto> AddMemberAsync(
        int familyId,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        if (familyId != FamilyId)
            throw new UnauthorizedAccessException("You can only add members to your own family.");

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureActiveFamilyAsync(connection, familyId, cancellationToken);

        var name = ValidateName(request.Name);
        var role = ValidateRole(request.Role);
        var createdAt = DateTime.UtcNow;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        if (request.IsFamilyHead)
        {
            await ClearFamilyHeadAsync(connection, transaction, familyId, null, cancellationToken);
        }

        int id;
        await using (var command = connection.CreateCommand(
            """
            INSERT INTO FamilyMembers (FamilyId, Name, Role, IsFamilyHead, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@FamilyId, @Name, @Role, @IsFamilyHead, 1, @CreatedAt)
            """, transaction)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@Name", SqlDbType.NVarChar, name, 150)
            .With("@Role", SqlDbType.NVarChar, role, 50)
            .With("@IsFamilyHead", SqlDbType.Bit, request.IsFamilyHead)
            .With("@CreatedAt", SqlDbType.DateTime2, createdAt))
        {
            id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);

        return new FamilyMemberDto(
            id,
            familyId,
            name,
            role,
            request.IsFamilyHead,
            true,
            createdAt);
    }

    public async Task<bool> UpdateMemberAsync(
        int familyId,
        int memberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        if (familyId <= 0 || memberId <= 0 || familyId != FamilyId)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        bool? currentIsHead;
        await using (var command = connection
            .CreateCommand("SELECT IsFamilyHead FROM FamilyMembers WHERE Id = @Id AND FamilyId = @FamilyId")
            .With("@Id", SqlDbType.Int, memberId)
            .With("@FamilyId", SqlDbType.Int, familyId))
        {
            currentIsHead = await command.ExecuteScalarAsync(cancellationToken) as bool?;
        }

        if (currentIsHead is null)
            return false;

        var name = ValidateName(request.Name);
        var role = ValidateRole(request.Role);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        if (request.IsFamilyHead && currentIsHead != true)
        {
            await ClearFamilyHeadAsync(connection, transaction, familyId, memberId, cancellationToken);
        }

        await using (var command = connection.CreateCommand(
            """
            UPDATE FamilyMembers
            SET Name = @Name, Role = @Role, IsActive = @IsActive, IsFamilyHead = @IsFamilyHead
            WHERE Id = @Id AND FamilyId = @FamilyId
            """, transaction)
            .With("@Id", SqlDbType.Int, memberId)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@Name", SqlDbType.NVarChar, name, 150)
            .With("@Role", SqlDbType.NVarChar, role, 50)
            .With("@IsActive", SqlDbType.Bit, request.IsActive)
            .With("@IsFamilyHead", SqlDbType.Bit, request.IsFamilyHead))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMemberAsync(
        int familyId,
        int memberId,
        CancellationToken cancellationToken = default)
    {
        RequireManagePermission();

        if (familyId <= 0 || memberId <= 0 || familyId != FamilyId)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "UPDATE FamilyMembers SET IsActive = 0, IsFamilyHead = 0 WHERE Id = @Id AND FamilyId = @FamilyId")
            .With("@Id", SqlDbType.Int, memberId)
            .With("@FamilyId", SqlDbType.Int, familyId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private void RequireManagePermission()
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (!_currentUser.Has(Permission.FamilyManage))
            throw new UnauthorizedAccessException("Only the Family Head can manage the family.");
    }

    private static async Task<FamilyDto?> LoadFamilyAsync(
        SqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        FamilyDto? family = null;
        await using (var command = connection
            .CreateCommand($"{FamilySelectSql} WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, id))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                family = new FamilyDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetDateTime(3),
                    new List<FamilyMemberDto>());
            }
        }

        if (family is null)
            return null;

        await using var membersCommand = connection
            .CreateCommand($"{MemberSelectSql} WHERE FamilyId = @FamilyId {MemberOrderBySql}")
            .With("@FamilyId", SqlDbType.Int, id);

        return family with { Members = await ReadMembersAsync(membersCommand, cancellationToken) };
    }

    private static async Task EnsureActiveFamilyAsync(
        SqlConnection connection,
        int familyId,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT IsActive FROM Families WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, familyId);

        var isActive = await command.ExecuteScalarAsync(cancellationToken) as bool?;

        if (isActive is null)
            throw new ArgumentException($"Family with ID {familyId} was not found.");

        if (isActive == false)
            throw new ArgumentException($"Family with ID {familyId} is inactive.");
    }

    private static async Task ClearFamilyHeadAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int familyId,
        int? excludeMemberId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            """
            UPDATE FamilyMembers
            SET IsFamilyHead = 0
            WHERE FamilyId = @FamilyId
              AND IsFamilyHead = 1
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, transaction)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@ExcludeId", SqlDbType.Int, excludeMemberId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<FamilyMemberDto>> ReadMembersAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var members = new List<FamilyMemberDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new FamilyMemberDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetDateTime(6)));
        }

        return members;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        return name.Trim();
    }

    private static string ValidateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required.");

        return role.Trim();
    }
}
