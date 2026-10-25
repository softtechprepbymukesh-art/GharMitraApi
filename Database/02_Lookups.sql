/* GharMitra - 02 Lookups and Accounts
   Brings the tables that were created directly in SSMS under source control.
   Idempotent and non-destructive: existing tables and rows are left untouched.

   Run order: 01_Schema.sql -> 02_Lookups.sql -> 03_Tenancy.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE GharMitraDb;
GO

/* ---------------------------------------------------------------
   Accounts - cash / bank / wallet holders for a family.
   OpeningBalance is a FACT set once. The current balance is DERIVED
   (opening + income - expenses) and must never be stored. See
   Docs/GAP_ANALYSIS.md issue C7.
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    FamilyId       INT              NOT NULL,
    AccountName    NVARCHAR(100)    NOT NULL,
    AccountType    NVARCHAR(50)     NOT NULL,   -- Cash | Bank | Wallet
    OpeningBalance DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Accounts_OpeningBalance DEFAULT 0,
    IsActive       BIT              NOT NULL CONSTRAINT DF_Accounts_IsActive DEFAULT 1,
    CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2        NULL
);
GO

/* Existing SSMS table stored a mutable CurrentBalance. Rename it to the fact it
   should always have been. Safe: the table holds no rows. */
IF COL_LENGTH(N'dbo.Accounts', N'CurrentBalance') IS NOT NULL
   AND COL_LENGTH(N'dbo.Accounts', N'OpeningBalance') IS NULL
    EXEC sp_rename N'dbo.Accounts.CurrentBalance', N'OpeningBalance', N'COLUMN';
GO

/* ---------------------------------------------------------------
   Open-set lookups: families extend these over time.
   FamilyId NULL = system default available to every family.
   FamilyId set  = that family's own custom entry.
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PaymentMethods', N'U') IS NULL
CREATE TABLE dbo.PaymentMethods (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentMethods PRIMARY KEY,
    MethodName NVARCHAR(100)    NOT NULL,
    IsActive   BIT              NOT NULL CONSTRAINT DF_PaymentMethods_IsActive DEFAULT 1,
    CreatedAt  DATETIME2        NOT NULL CONSTRAINT DF_PaymentMethods_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2        NULL
);
GO

IF OBJECT_ID(N'dbo.IncomeSources', N'U') IS NULL
CREATE TABLE dbo.IncomeSources (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IncomeSources PRIMARY KEY,
    SourceName NVARCHAR(100)    NOT NULL,
    IsActive   BIT              NOT NULL CONSTRAINT DF_IncomeSources_IsActive DEFAULT 1,
    CreatedAt  DATETIME2        NOT NULL CONSTRAINT DF_IncomeSources_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2        NULL
);
GO

IF OBJECT_ID(N'dbo.Relations', N'U') IS NULL
CREATE TABLE dbo.Relations (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Relations PRIMARY KEY,
    RelationName NVARCHAR(100)    NOT NULL,
    IsActive     BIT              NOT NULL CONSTRAINT DF_Relations_IsActive DEFAULT 1,
    CreatedAt    DATETIME2        NOT NULL CONSTRAINT DF_Relations_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2        NULL
);
GO

/* ---------------------------------------------------------------
   ExpenseTypes / SpendingTypes.
   Kept for now so nothing breaks, but see Docs/GAP_ANALYSIS.md:
     - ExpenseTypes (Household, Medical, Travel...) DUPLICATES Categories.
       It should become Categories.ParentCategoryId sub-categories.
     - SpendingTypes (Emergency, Planned, Unplanned) is a closed set of 3
       and belongs in a C# enum, not a table.
   Both are scheduled for removal in 04_WireLookups.sql.
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ExpenseTypes', N'U') IS NULL
CREATE TABLE dbo.ExpenseTypes (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExpenseTypes PRIMARY KEY,
    TypeName  NVARCHAR(100)    NOT NULL,
    IsActive  BIT              NOT NULL CONSTRAINT DF_ExpenseTypes_IsActive DEFAULT 1,
    CreatedAt DATETIME2        NOT NULL CONSTRAINT DF_ExpenseTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2        NULL
);
GO

IF OBJECT_ID(N'dbo.SpendingTypes', N'U') IS NULL
CREATE TABLE dbo.SpendingTypes (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SpendingTypes PRIMARY KEY,
    TypeName  NVARCHAR(100)    NOT NULL,
    IsActive  BIT              NOT NULL CONSTRAINT DF_SpendingTypes_IsActive DEFAULT 1,
    CreatedAt DATETIME2        NOT NULL CONSTRAINT DF_SpendingTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2        NULL
);
GO

/* ---------------------------------------------------------------
   Seed data - matches what already exists in the SSMS database.
   Re-running adds only what is missing.
   --------------------------------------------------------------- */
INSERT INTO dbo.PaymentMethods (Id, MethodName)
SELECT NEWID(), v.Name
FROM (VALUES (N'Cash'), (N'UPI'), (N'Debit Card'), (N'Credit Card'), (N'Net Banking')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods x WHERE x.MethodName = v.Name);
GO

INSERT INTO dbo.IncomeSources (Id, SourceName)
SELECT NEWID(), v.Name
FROM (VALUES (N'Salary'), (N'Business'), (N'Freelancing'),
             (N'Rental Income'), (N'Interest'), (N'Bonus')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.IncomeSources x WHERE x.SourceName = v.Name);
GO

INSERT INTO dbo.Relations (Id, RelationName)
SELECT NEWID(), v.Name
FROM (VALUES (N'Self'), (N'Father'), (N'Mother'), (N'Wife'),
             (N'Son'), (N'Daughter'), (N'Brother'), (N'Sister')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Relations x WHERE x.RelationName = v.Name);
GO

INSERT INTO dbo.ExpenseTypes (Id, TypeName)
SELECT NEWID(), v.Name
FROM (VALUES (N'Household'), (N'Personal'), (N'Medical'),
             (N'Education'), (N'Travel'), (N'Entertainment')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpenseTypes x WHERE x.TypeName = v.Name);
GO

INSERT INTO dbo.SpendingTypes (Id, TypeName)
SELECT NEWID(), v.Name
FROM (VALUES (N'Planned'), (N'Unplanned'), (N'Emergency')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SpendingTypes x WHERE x.TypeName = v.Name);
GO

/* Uniqueness on the natural key, so seeding can never duplicate. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PaymentMethods_MethodName')
    CREATE UNIQUE INDEX UX_PaymentMethods_MethodName ON dbo.PaymentMethods (MethodName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_IncomeSources_SourceName')
    CREATE UNIQUE INDEX UX_IncomeSources_SourceName ON dbo.IncomeSources (SourceName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Relations_RelationName')
    CREATE UNIQUE INDEX UX_Relations_RelationName ON dbo.Relations (RelationName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ExpenseTypes_TypeName')
    CREATE UNIQUE INDEX UX_ExpenseTypes_TypeName ON dbo.ExpenseTypes (TypeName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SpendingTypes_TypeName')
    CREATE UNIQUE INDEX UX_SpendingTypes_TypeName ON dbo.SpendingTypes (TypeName);
GO
