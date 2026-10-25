using System.Data;
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

    public FamilyService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<FamilyDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var families = new List<(int Id, string Name, bool IsActive, DateTime CreatedAt)>();
        await using (var command = connection.CreateCommand($"{FamilySelectSql} ORDER BY Name"))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                families.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetDateTime(3)));
            }
        }

        IReadOnlyList<FamilyMemberDto> members;
        await using (var command = connection.CreateCommand($"{MemberSelectSql} {MemberOrderBySql}"))
        {
            members = await ReadMembersAsync(command, cancellationToken);
        }

        return families
            .Select(f => new FamilyDto(
                f.Id,
                f.Name,
                f.IsActive,
                f.CreatedAt,
                members.Where(m => m.FamilyId == f.Id).ToList()))
            .ToList();
    }

    public async Task<FamilyDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, id, cancellationToken);
    }

    public async Task<FamilyDto> CreateAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);
        var createdAt = DateTime.UtcNow;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            """
            INSERT INTO Families (Name, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, 1, @CreatedAt)
            """)
            .With("@Name", SqlDbType.NVarChar, name, 150)
            .With("@CreatedAt", SqlDbType.DateTime2, createdAt);

        var id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        return new FamilyDto(id, name, true, createdAt, new List<FamilyMemberDto>());
    }

    public async Task<bool> UpdateAsync(
        int id,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (!await FamilyExistsAsync(connection, id, cancellationToken))
            return false;

        var name = ValidateName(request.Name);

        await using var command = connection.CreateCommand(
            "UPDATE Families SET Name = @Name, IsActive = @IsActive WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, id)
            .With("@Name", SqlDbType.NVarChar, name, 150)
            .With("@IsActive", SqlDbType.Bit, request.IsActive);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
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
        if (familyId <= 0)
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
        if (familyId <= 0 || memberId <= 0)
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
        if (familyId <= 0 || memberId <= 0)
            return false;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "UPDATE FamilyMembers SET IsActive = 0, IsFamilyHead = 0 WHERE Id = @Id AND FamilyId = @FamilyId")
            .With("@Id", SqlDbType.Int, memberId)
            .With("@FamilyId", SqlDbType.Int, familyId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<FamilyDto?> GetByIdAsync(
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

    private static async Task<bool> FamilyExistsAsync(
        SqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT 1 FROM Families WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, id);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task EnsureActiveFamilyAsync(
        SqlConnection connection,
        int familyId,
        CancellationToken cancellationToken)
    {
        if (familyId <= 0)
            throw new ArgumentException("Family ID is required.");

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
