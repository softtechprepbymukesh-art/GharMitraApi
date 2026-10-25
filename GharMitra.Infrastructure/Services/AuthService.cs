using System.Data;
using GharMitra.Application.Auth.DTOs;
using GharMitra.Application.Auth.Interfaces;
using GharMitra.Application.Common;
using GharMitra.Infrastructure.Data;
using GharMitra.Infrastructure.Security;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    /// <summary>A Family Head is only ever created by registering a new family.</summary>
    private static readonly string[] InvitableRoles = ["Adult", "Child", "Viewer"];

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _tokenService;
    private readonly ICurrentUser _currentUser;

    public AuthService(
        ISqlConnectionFactory connectionFactory,
        IPasswordHasher passwordHasher,
        JwtTokenService tokenService,
        ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    public async Task<AuthResponse> RegisterFamilyAsync(
        RegisterFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormaliseEmail(request.Email);
        var familyName = Require(request.FamilyName, "Family name");
        var memberName = Require(request.MemberName, "Member name");
        var relation = Require(request.Relation, "Relation");
        ValidatePassword(request.Password);
        ValidateTimeZone(request.TimeZoneId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (await EmailExistsAsync(connection, email, null, cancellationToken))
            throw new InvalidOperationException("An account with this email already exists.");

        var roleId = await GetRoleIdAsync(connection, "FamilyHead", cancellationToken);
        var passwordHash = _passwordHasher.Hash(request.Password);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int familyId;
        await using (var command = connection.CreateCommand(
            """
            INSERT INTO Families (Name, IsActive, CreatedAt, TimeZoneId, CurrencyCode, FinancialYearStartMonth)
            OUTPUT INSERTED.Id
            VALUES (@Name, 1, @CreatedAt, @TimeZoneId, N'INR', 4)
            """, transaction)
            .With("@Name", SqlDbType.NVarChar, familyName, 150)
            .With("@CreatedAt", SqlDbType.DateTime2, now)
            .With("@TimeZoneId", SqlDbType.NVarChar, request.TimeZoneId, 64))
        {
            familyId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        int memberId;
        await using (var command = connection.CreateCommand(
            """
            INSERT INTO FamilyMembers (FamilyId, Name, Role, IsFamilyHead, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@FamilyId, @Name, @Role, 1, 1, @CreatedAt)
            """, transaction)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@Name", SqlDbType.NVarChar, memberName, 150)
            .With("@Role", SqlDbType.NVarChar, relation, 50)
            .With("@CreatedAt", SqlDbType.DateTime2, now))
        {
            memberId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await InsertUserAsync(
            connection, transaction, userId, familyId, memberId, roleId, email, passwordHash, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await IssueAsync(
            connection, userId, familyId, memberId, email, "FamilyHead", cancellationToken);
    }

    public async Task<AuthResponse> InviteMemberAsync(
        InviteMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (!_currentUser.Has(Permission.FamilyManage))
            throw new UnauthorizedAccessException("Only the Family Head can add members.");

        var email = NormaliseEmail(request.Email);
        var memberName = Require(request.MemberName, "Member name");
        var relation = Require(request.Relation, "Relation");
        ValidatePassword(request.Password);

        var roleCode = Require(request.RoleCode, "Role");

        if (!InvitableRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Role must be one of: {string.Join(", ", InvitableRoles)}.",
                nameof(request.RoleCode));
        }

        // The family comes from the token, never from the request.
        var familyId = _currentUser.FamilyId;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (await EmailExistsAsync(connection, email, null, cancellationToken))
            throw new InvalidOperationException("An account with this email already exists.");

        var roleId = await GetRoleIdAsync(connection, roleCode, cancellationToken);
        var passwordHash = _passwordHasher.Hash(request.Password);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int memberId;
        await using (var command = connection.CreateCommand(
            """
            INSERT INTO FamilyMembers (FamilyId, Name, Role, IsFamilyHead, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@FamilyId, @Name, @Role, 0, 1, @CreatedAt)
            """, transaction)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@Name", SqlDbType.NVarChar, memberName, 150)
            .With("@Role", SqlDbType.NVarChar, relation, 50)
            .With("@CreatedAt", SqlDbType.DateTime2, now))
        {
            memberId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await InsertUserAsync(
            connection, transaction, userId, familyId, memberId, roleId, email, passwordHash, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await IssueAsync(
            connection, userId, familyId, memberId, email, roleCode, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormaliseEmail(request.Email);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        Guid userId = Guid.Empty;
        int familyId = 0, memberId = 0;
        string storedHash = string.Empty, roleCode = string.Empty;
        var found = false;

        await using (var command = connection.CreateCommand(
            """
            SELECT u.Id, u.FamilyId, u.FamilyMemberId, u.PasswordHash, r.Code
            FROM Users u
            INNER JOIN Roles r ON r.Id = u.RoleId
            INNER JOIN FamilyMembers m ON m.Id = u.FamilyMemberId
            WHERE u.Email = @Email AND u.IsActive = 1 AND m.IsActive = 1
            """)
            .With("@Email", SqlDbType.NVarChar, email, 256))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                found = true;
                userId = reader.GetGuid(0);
                familyId = reader.GetInt32(1);
                memberId = reader.GetInt32(2);
                storedHash = reader.GetString(3);
                roleCode = reader.GetString(4);
            }
        }

        // Verify even when the account is missing, so response time does not
        // reveal which emails are registered.
        var dummyHash = "PBKDF2$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var passwordOk = _passwordHasher.Verify(request.Password, found ? storedHash : dummyHash);

        if (!found || !passwordOk)
            throw new UnauthorizedAccessException("Invalid email or password.");

        await using (var command = connection
            .CreateCommand("UPDATE Users SET LastLoginAt = @Now WHERE Id = @Id")
            .With("@Now", SqlDbType.DateTime2, DateTime.UtcNow)
            .With("@Id", SqlDbType.UniqueIdentifier, userId))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await IssueAsync(connection, userId, familyId, memberId, email, roleCode, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Refresh token is required.");

        var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        Guid tokenId = Guid.Empty, userId = Guid.Empty;
        int familyId = 0, memberId = 0;
        string email = string.Empty, roleCode = string.Empty;
        var found = false;

        await using (var command = connection.CreateCommand(
            """
            SELECT t.Id, u.Id, u.FamilyId, u.FamilyMemberId, u.Email, r.Code
            FROM RefreshTokens t
            INNER JOIN Users u ON u.Id = t.UserId
            INNER JOIN Roles r ON r.Id = u.RoleId
            WHERE t.TokenHash = @TokenHash
              AND t.RevokedAt IS NULL
              AND t.ExpiresAt > @Now
              AND u.IsActive = 1
            """)
            .With("@TokenHash", SqlDbType.NVarChar, tokenHash, 200)
            .With("@Now", SqlDbType.DateTime2, DateTime.UtcNow))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                found = true;
                tokenId = reader.GetGuid(0);
                userId = reader.GetGuid(1);
                familyId = reader.GetInt32(2);
                memberId = reader.GetInt32(3);
                email = reader.GetString(4);
                roleCode = reader.GetString(5);
            }
        }

        if (!found)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        // Rotate: the presented token is retired as the replacement is issued.
        await using (var command = connection
            .CreateCommand("UPDATE RefreshTokens SET RevokedAt = @Now WHERE Id = @Id")
            .With("@Now", SqlDbType.DateTime2, DateTime.UtcNow)
            .With("@Id", SqlDbType.UniqueIdentifier, tokenId))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await IssueAsync(connection, userId, familyId, memberId, email, roleCode, cancellationToken);
    }

    public async Task LogoutAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "UPDATE RefreshTokens SET RevokedAt = @Now WHERE TokenHash = @TokenHash AND RevokedAt IS NULL")
            .With("@Now", SqlDbType.DateTime2, DateTime.UtcNow)
            .With("@TokenHash", SqlDbType.NVarChar, tokenHash, 200);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueAsync(
        SqlConnection connection,
        Guid userId,
        int familyId,
        int memberId,
        string email,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var permissions = await GetPermissionsAsync(connection, roleCode, cancellationToken);

        var access = _tokenService.CreateAccessToken(
            userId, familyId, memberId, email, roleCode, permissions);

        var refreshToken = JwtTokenService.CreateRefreshToken();

        await using (var command = connection.CreateCommand(
            """
            INSERT INTO RefreshTokens (Id, UserId, TokenHash, ExpiresAt, CreatedAt)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @CreatedAt)
            """)
            .With("@Id", SqlDbType.UniqueIdentifier, Guid.NewGuid())
            .With("@UserId", SqlDbType.UniqueIdentifier, userId)
            .With("@TokenHash", SqlDbType.NVarChar, JwtTokenService.HashRefreshToken(refreshToken), 200)
            .With("@ExpiresAt", SqlDbType.DateTime2, _tokenService.RefreshTokenExpiry())
            .With("@CreatedAt", SqlDbType.DateTime2, DateTime.UtcNow))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new AuthResponse(
            access.AccessToken,
            refreshToken,
            access.ExpiresAtUtc,
            userId,
            familyId,
            memberId,
            email,
            roleCode,
            permissions);
    }

    private static async Task InsertUserAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid userId,
        int familyId,
        int memberId,
        int roleId,
        string email,
        string passwordHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            """
            INSERT INTO Users (Id, FamilyId, FamilyMemberId, RoleId, Email, PasswordHash, IsActive, CreatedAt)
            VALUES (@Id, @FamilyId, @MemberId, @RoleId, @Email, @PasswordHash, 1, @CreatedAt)
            """, transaction)
            .With("@Id", SqlDbType.UniqueIdentifier, userId)
            .With("@FamilyId", SqlDbType.Int, familyId)
            .With("@MemberId", SqlDbType.Int, memberId)
            .With("@RoleId", SqlDbType.Int, roleId)
            .With("@Email", SqlDbType.NVarChar, email, 256)
            .With("@PasswordHash", SqlDbType.NVarChar, passwordHash, 500)
            .With("@CreatedAt", SqlDbType.DateTime2, now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> GetRoleIdAsync(
        SqlConnection connection,
        string roleCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT Id FROM Roles WHERE Code = @Code")
            .With("@Code", SqlDbType.NVarChar, roleCode, 50);

        var id = await command.ExecuteScalarAsync(cancellationToken);

        if (id is null)
            throw new InvalidOperationException($"Role '{roleCode}' is not configured. Run Database/04_Auth.sql.");

        return (int)id;
    }

    private static async Task<IReadOnlyList<string>> GetPermissionsAsync(
        SqlConnection connection,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var permissions = new List<string>();

        await using var command = connection.CreateCommand(
            """
            SELECT p.Code
            FROM Permissions p
            INNER JOIN RolePermissions rp ON rp.PermissionId = p.Id
            INNER JOIN Roles r ON r.Id = rp.RoleId
            WHERE r.Code = @Code
            ORDER BY p.Code
            """)
            .With("@Code", SqlDbType.NVarChar, roleCode, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            permissions.Add(reader.GetString(0));

        return permissions;
    }

    private static async Task<bool> EmailExistsAsync(
        SqlConnection connection,
        string email,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT 1 FROM Users WHERE Email = @Email", transaction)
            .With("@Email", SqlDbType.NVarChar, email, 256);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static string NormaliseEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string Require(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} is required.");

        return value.Trim();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
    }

    private static void ValidateTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("Time zone is required.", nameof(timeZoneId));

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"'{timeZoneId}' is not a known time zone. Use an IANA id such as Asia/Kolkata.",
                nameof(timeZoneId));
        }
    }
}
