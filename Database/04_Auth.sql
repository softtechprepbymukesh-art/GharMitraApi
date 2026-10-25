/* GharMitra - 04 Authentication and role-based access control
   Fixes CRITICAL findings C1 (no authentication) and C6 (no RBAC).

   Design notes
     - Users is the LOGIN identity. FamilyMembers is the PERSON.
       A child or an elderly parent can be a family member with no login,
       so the two are deliberately separate tables.
     - Email is the login key and is unique across all families.
     - One role per user. Permissions hang off the role, so adding a
       capability never requires a schema change.

   Run order: 01 -> 02 -> 03 -> 04 -> 05
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE GharMitraDb;
GO

/* =============================================================
   Roles
   ============================================================= */
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
CREATE TABLE dbo.Roles (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Code        NVARCHAR(50)      NOT NULL,
    Name        NVARCHAR(100)     NOT NULL,
    Description NVARCHAR(300)     NULL,
    CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Roles_Code')
    CREATE UNIQUE INDEX UX_Roles_Code ON dbo.Roles (Code);
GO

/* =============================================================
   Permissions
   ============================================================= */
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
CREATE TABLE dbo.Permissions (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Code        NVARCHAR(100)     NOT NULL,
    Description NVARCHAR(300)     NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Permissions_Code')
    CREATE UNIQUE INDEX UX_Permissions_Code ON dbo.Permissions (Code);
GO

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
CREATE TABLE dbo.RolePermissions (
    RoleId       INT NOT NULL,
    PermissionId INT NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles       FOREIGN KEY (RoleId)       REFERENCES dbo.Roles (Id),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id)
);
GO

/* =============================================================
   Users
   ============================================================= */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    FamilyId       INT              NOT NULL,
    FamilyMemberId INT              NOT NULL,
    RoleId         INT              NOT NULL,
    Email          NVARCHAR(256)    NOT NULL,
    PasswordHash   NVARCHAR(500)    NOT NULL,   -- PBKDF2$iterations$salt$hash
    IsActive       BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    LastLoginAt    DATETIME2        NULL,
    CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2        NULL,
    CONSTRAINT FK_Users_Families      FOREIGN KEY (FamilyId) REFERENCES dbo.Families (Id),
    CONSTRAINT FK_Users_Roles         FOREIGN KEY (RoleId)   REFERENCES dbo.Roles (Id),
    CONSTRAINT FK_Users_FamilyMembers FOREIGN KEY (FamilyMemberId, FamilyId)
        REFERENCES dbo.FamilyMembers (Id, FamilyId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Users_Email')
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users (Email);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Users_FamilyMemberId')
    CREATE UNIQUE INDEX UX_Users_FamilyMemberId ON dbo.Users (FamilyMemberId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_FamilyId')
    CREATE INDEX IX_Users_FamilyId ON dbo.Users (FamilyId);
GO

/* =============================================================
   Refresh tokens - only the hash is stored, never the token itself
   ============================================================= */
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
CREATE TABLE dbo.RefreshTokens (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId     UNIQUEIDENTIFIER NOT NULL,
    TokenHash  NVARCHAR(200)    NOT NULL,
    ExpiresAt  DATETIME2        NOT NULL,
    RevokedAt  DATETIME2        NULL,
    CreatedAt  DATETIME2        NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshTokens_TokenHash')
    CREATE INDEX IX_RefreshTokens_TokenHash ON dbo.RefreshTokens (TokenHash);
GO

/* =============================================================
   Seed roles
   ============================================================= */
INSERT INTO dbo.Roles (Code, Name, Description)
SELECT v.Code, v.Name, v.Descr
FROM (VALUES
    (N'FamilyHead', N'Family Head', N'Full financial picture of the whole family'),
    (N'Adult',      N'Adult Member', N'Own finances plus whatever the family shares'),
    (N'Child',      N'Child Member', N'Own pocket money and own expenses only'),
    (N'Viewer',     N'Viewer',       N'Read-only access to shared summaries')
) AS v(Code, Name, Descr)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Code = v.Code);
GO

/* =============================================================
   Seed permissions
   ============================================================= */
INSERT INTO dbo.Permissions (Code, Description)
SELECT v.Code, v.Descr
FROM (VALUES
    (N'family.manage',        N'Create and manage family members'),
    (N'family.view',          N'View family profile'),
    (N'expense.record.own',   N'Record own expenses'),
    (N'expense.view.own',     N'View own expenses'),
    (N'expense.view.family',  N'View every member expense'),
    (N'income.record.own',    N'Record own income'),
    (N'income.view.own',      N'View own income'),
    (N'income.view.family',   N'View every member income'),
    (N'account.manage',       N'Create and manage accounts'),
    (N'account.view.family',  N'View all family accounts'),
    (N'category.manage',      N'Create and manage family categories'),
    (N'budget.manage',        N'Create and manage budgets'),
    (N'budget.view.family',   N'View family budgets'),
    (N'goal.manage',          N'Create and manage goals'),
    (N'goal.view.family',     N'View family goals'),
    (N'loan.manage',          N'Create and manage loans'),
    (N'loan.view',            N'View loan details and outstanding'),
    (N'investment.manage',    N'Create and manage investments'),
    (N'reports.view.own',     N'View own reports'),
    (N'reports.view.family',  N'View consolidated family reports')
) AS v(Code, Descr)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = v.Code);
GO

/* =============================================================
   Map roles to permissions

   Child deliberately receives ONLY own-expense permissions, satisfying
   mandatory rule 9: a child must never reach family financial data.
   ============================================================= */
;WITH Mapping(RoleCode, PermissionCode) AS (
    SELECT N'FamilyHead', Code FROM dbo.Permissions          -- the Head gets everything
    UNION ALL SELECT N'Adult', v.Code FROM (VALUES
        (N'family.view'), (N'expense.record.own'), (N'expense.view.own'),
        (N'income.record.own'), (N'income.view.own'), (N'goal.manage'),
        (N'reports.view.own'), (N'budget.view.family'), (N'loan.view')
    ) AS v(Code)
    UNION ALL SELECT N'Child', v.Code FROM (VALUES
        (N'expense.record.own'), (N'expense.view.own')
    ) AS v(Code)
    UNION ALL SELECT N'Viewer', v.Code FROM (VALUES
        (N'family.view'), (N'reports.view.own')
    ) AS v(Code)
)
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Mapping m
JOIN dbo.Roles r       ON r.Code = m.RoleCode
JOIN dbo.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions rp
    WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

PRINT N'04_Auth.sql completed.';
GO
