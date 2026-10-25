/* GharMitra - 03 Tenancy, integrity and performance
   Fixes the CRITICAL and HIGH findings in Docs/GAP_ANALYSIS.md:
     C3  Incomes has no FamilyId / FamilyMemberId
     H1  Categories has no FamilyId
     H2  Accounts has no foreign key
     H5  No index supports any date-range query
     H7  No family time zone, so UTC date validation breaks Indian users
     H8  Expenses / Incomes do not link to an Account
     D1  Expense member is not constrained to the expense family
     M4  No sub-category support
     M5  Nothing enforces one Family Head per family
     M6  Families lacks TimeZone / Currency / FinancialYearStartMonth

   Idempotent and non-destructive. Existing rows are backfilled, never deleted.
   Run order: 01_Schema.sql -> 02_Lookups.sql -> 03_Tenancy.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE GharMitraDb;
GO

/* =============================================================
   1. Families - locale context (H7, M6)
   ============================================================= */
IF COL_LENGTH(N'dbo.Families', N'TimeZoneId') IS NULL
    ALTER TABLE dbo.Families ADD TimeZoneId NVARCHAR(64) NOT NULL
        CONSTRAINT DF_Families_TimeZoneId DEFAULT N'Asia/Kolkata';
GO
IF COL_LENGTH(N'dbo.Families', N'CurrencyCode') IS NULL
    ALTER TABLE dbo.Families ADD CurrencyCode NCHAR(3) NOT NULL
        CONSTRAINT DF_Families_CurrencyCode DEFAULT N'INR';
GO
IF COL_LENGTH(N'dbo.Families', N'FinancialYearStartMonth') IS NULL
    ALTER TABLE dbo.Families ADD FinancialYearStartMonth TINYINT NOT NULL
        CONSTRAINT DF_Families_FyStart DEFAULT 4;   -- April, Indian financial year
GO

/* =============================================================
   2. Accounts - missing foreign key (H2)
   ============================================================= */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Accounts_Families_FamilyId')
    ALTER TABLE dbo.Accounts ADD CONSTRAINT FK_Accounts_Families_FamilyId
        FOREIGN KEY (FamilyId) REFERENCES dbo.Families (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Accounts_FamilyId')
    CREATE INDEX IX_Accounts_FamilyId ON dbo.Accounts (FamilyId) WHERE IsActive = 1;
GO

/* =============================================================
   3. Categories - tenant key and sub-categories (H1, M4)
   FamilyId NULL  = system default category, visible to every family
   FamilyId value = that family's own category
   ============================================================= */
IF COL_LENGTH(N'dbo.Categories', N'FamilyId') IS NULL
    ALTER TABLE dbo.Categories ADD FamilyId INT NULL;
GO
IF COL_LENGTH(N'dbo.Categories', N'ParentCategoryId') IS NULL
    ALTER TABLE dbo.Categories ADD ParentCategoryId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'dbo.Categories', N'IsSystem') IS NULL
    ALTER TABLE dbo.Categories ADD IsSystem BIT NOT NULL
        CONSTRAINT DF_Categories_IsSystem DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Categories_Families_FamilyId')
    ALTER TABLE dbo.Categories ADD CONSTRAINT FK_Categories_Families_FamilyId
        FOREIGN KEY (FamilyId) REFERENCES dbo.Families (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Categories_Categories_ParentCategoryId')
    ALTER TABLE dbo.Categories ADD CONSTRAINT FK_Categories_Categories_ParentCategoryId
        FOREIGN KEY (ParentCategoryId) REFERENCES dbo.Categories (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Categories_Family_Type')
    CREATE INDEX IX_Categories_Family_Type ON dbo.Categories (FamilyId, Type, IsActive);
GO

/* A category name must be unique within its own scope, not globally. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Categories_Family_Type_Name')
    CREATE UNIQUE INDEX UX_Categories_Family_Type_Name
        ON dbo.Categories (FamilyId, Type, Name) WHERE FamilyId IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Categories_System_Type_Name')
    CREATE UNIQUE INDEX UX_Categories_System_Type_Name
        ON dbo.Categories (Type, Name) WHERE FamilyId IS NULL;
GO

/* =============================================================
   4. Incomes - tenant key, owner and account (C3, H8)
   ============================================================= */
IF COL_LENGTH(N'dbo.Incomes', N'FamilyId') IS NULL
    ALTER TABLE dbo.Incomes ADD FamilyId INT NULL;
GO
IF COL_LENGTH(N'dbo.Incomes', N'FamilyMemberId') IS NULL
    ALTER TABLE dbo.Incomes ADD FamilyMemberId INT NULL;
GO
IF COL_LENGTH(N'dbo.Incomes', N'AccountId') IS NULL
    ALTER TABLE dbo.Incomes ADD AccountId UNIQUEIDENTIFIER NULL;
GO

/* Backfill existing rows. Only safe when the database holds exactly one family,
   which is true for development data. Otherwise stop and let a human decide. */
IF EXISTS (SELECT 1 FROM dbo.Incomes WHERE FamilyId IS NULL)
BEGIN
    IF (SELECT COUNT(*) FROM dbo.Families) = 1
    BEGIN
        DECLARE @FamilyId INT = (SELECT TOP 1 Id FROM dbo.Families);
        DECLARE @MemberId INT = (
            SELECT TOP 1 Id FROM dbo.FamilyMembers
            WHERE FamilyId = @FamilyId
            ORDER BY IsFamilyHead DESC, Id);

        IF @MemberId IS NULL
            THROW 50001, N'Cannot backfill Incomes: the family has no members. Add a member first.', 1;

        UPDATE dbo.Incomes
        SET FamilyId = @FamilyId,
            FamilyMemberId = @MemberId
        WHERE FamilyId IS NULL;

        PRINT N'Backfilled Incomes with the single existing family.';
    END
    ELSE
        THROW 50002, N'Cannot backfill Incomes automatically: zero or multiple families exist. Assign Incomes.FamilyId manually, then re-run.', 1;
END
GO

/* Enforce NOT NULL only once every row has a value. */
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Incomes')
             AND name = N'FamilyId' AND is_nullable = 1)
   AND NOT EXISTS (SELECT 1 FROM dbo.Incomes WHERE FamilyId IS NULL)
BEGIN
    ALTER TABLE dbo.Incomes ALTER COLUMN FamilyId INT NOT NULL;
    ALTER TABLE dbo.Incomes ALTER COLUMN FamilyMemberId INT NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Incomes_Families_FamilyId')
    ALTER TABLE dbo.Incomes ADD CONSTRAINT FK_Incomes_Families_FamilyId
        FOREIGN KEY (FamilyId) REFERENCES dbo.Families (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Incomes_Accounts_AccountId')
    ALTER TABLE dbo.Incomes ADD CONSTRAINT FK_Incomes_Accounts_AccountId
        FOREIGN KEY (AccountId) REFERENCES dbo.Accounts (Id);
GO

/* =============================================================
   5. Expenses - account link and tenant-safe member reference (H8, D1)
   ============================================================= */
IF COL_LENGTH(N'dbo.Expenses', N'AccountId') IS NULL
    ALTER TABLE dbo.Expenses ADD AccountId UNIQUEIDENTIFIER NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_Accounts_AccountId')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_Accounts_AccountId
        FOREIGN KEY (AccountId) REFERENCES dbo.Accounts (Id);
GO

/* The database must not allow an expense whose member belongs to another family.
   Requires a unique key on (Id, FamilyId) to reference. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FamilyMembers_Id_FamilyId')
    ALTER TABLE dbo.FamilyMembers ADD CONSTRAINT UX_FamilyMembers_Id_FamilyId
        UNIQUE (Id, FamilyId);
GO
/* Replaced by the composite key below. The constraint carries different names
   depending on whether the database came from the old EF migrations or from
   01_Schema.sql, so drop whichever is present. */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_FamilyMembers_FamilyMemberId')
    ALTER TABLE dbo.Expenses DROP CONSTRAINT FK_Expenses_FamilyMembers_FamilyMemberId;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_FamilyMembers_MemberId')
    ALTER TABLE dbo.Expenses DROP CONSTRAINT FK_Expenses_FamilyMembers_MemberId;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_FamilyMembers_Member_Family')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_FamilyMembers_Member_Family
        FOREIGN KEY (FamilyMemberId, FamilyId) REFERENCES dbo.FamilyMembers (Id, FamilyId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Incomes_FamilyMembers_Member_Family')
   AND NOT EXISTS (SELECT 1 FROM dbo.Incomes WHERE FamilyMemberId IS NULL)
    ALTER TABLE dbo.Incomes ADD CONSTRAINT FK_Incomes_FamilyMembers_Member_Family
        FOREIGN KEY (FamilyMemberId, FamilyId) REFERENCES dbo.FamilyMembers (Id, FamilyId);
GO

/* =============================================================
   6. One Family Head per family (M5)
   ============================================================= */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FamilyMembers_OneHeadPerFamily')
    CREATE UNIQUE INDEX UX_FamilyMembers_OneHeadPerFamily
        ON dbo.FamilyMembers (FamilyId) WHERE IsFamilyHead = 1;
GO

/* =============================================================
   7. Indexes for every date-range query (H5)
   This is the most important index in the system: every monthly and yearly
   report, and every Safe-To-Spend calculation, is driven by it.
   ============================================================= */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expenses_Family_Date')
    CREATE INDEX IX_Expenses_Family_Date
        ON dbo.Expenses (FamilyId, ExpenseDate)
        INCLUDE (Amount, CategoryId, FamilyMemberId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expenses_Family_Member_Date')
    CREATE INDEX IX_Expenses_Family_Member_Date
        ON dbo.Expenses (FamilyId, FamilyMemberId, ExpenseDate)
        INCLUDE (Amount, CategoryId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incomes_Family_Date')
    CREATE INDEX IX_Incomes_Family_Date
        ON dbo.Incomes (FamilyId, IncomeDate)
        INCLUDE (Amount, CategoryId, FamilyMemberId);
GO

/* The old single-column indexes are now covered by the composites above. */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expenses_FamilyId')
    DROP INDEX IX_Expenses_FamilyId ON dbo.Expenses;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Income_IsActive_IncomeDate')
    DROP INDEX IX_Income_IsActive_IncomeDate ON dbo.Incomes;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Income_IncomeDate')
    DROP INDEX IX_Income_IncomeDate ON dbo.Incomes;
GO

PRINT N'03_Tenancy.sql completed.';
GO
