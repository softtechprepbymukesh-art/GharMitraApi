/* GharMitra - SQL Server schema (run once, safe to re-run).
   The application connects with plain ADO.NET and never creates or alters objects. */
IF DB_ID(N'GharMitraDb') IS NULL
    CREATE DATABASE GharMitraDb;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE GharMitraDb;
GO

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
CREATE TABLE dbo.Categories (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name         NVARCHAR(100)    NOT NULL,
    Description  NVARCHAR(500)    NULL,
    Type         INT              NOT NULL,          -- 1 = Expense, 2 = Income
    IsActive     BIT              NOT NULL,
    DisplayOrder INT              NOT NULL,
    CreatedAt    DATETIME2        NOT NULL,
    UpdatedAt    DATETIME2        NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Categories_Name')
    CREATE INDEX IX_Categories_Name ON dbo.Categories (Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Categories_Type_IsActive')
    CREATE INDEX IX_Categories_Type_IsActive ON dbo.Categories (Type, IsActive);
GO

IF OBJECT_ID(N'dbo.Families', N'U') IS NULL
CREATE TABLE dbo.Families (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Families PRIMARY KEY,
    Name      NVARCHAR(150)     NOT NULL,
    IsActive  BIT               NOT NULL,
    CreatedAt DATETIME2         NOT NULL
);
GO

IF OBJECT_ID(N'dbo.FamilyMembers', N'U') IS NULL
CREATE TABLE dbo.FamilyMembers (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FamilyMembers PRIMARY KEY,
    FamilyId     INT               NOT NULL,
    Name         NVARCHAR(150)     NOT NULL,
    Role         NVARCHAR(50)      NOT NULL,
    IsFamilyHead BIT               NOT NULL,
    IsActive     BIT               NOT NULL,
    CreatedAt    DATETIME2         NOT NULL,
    CONSTRAINT FK_FamilyMembers_Families_FamilyId FOREIGN KEY (FamilyId) REFERENCES dbo.Families (Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FamilyMembers_FamilyId')
    CREATE INDEX IX_FamilyMembers_FamilyId ON dbo.FamilyMembers (FamilyId);
GO

IF OBJECT_ID(N'dbo.Incomes', N'U') IS NULL
CREATE TABLE dbo.Incomes (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Incomes PRIMARY KEY,
    SourceName NVARCHAR(256)    NOT NULL,
    Amount     DECIMAL(18,2)    NOT NULL,
    IncomeDate DATETIME2        NOT NULL,
    CategoryId UNIQUEIDENTIFIER NOT NULL,
    IsActive   BIT              NOT NULL CONSTRAINT DF_Incomes_IsActive DEFAULT 1,
    CreatedAt  DATETIME2        NOT NULL CONSTRAINT DF_Incomes_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt  DATETIME2        NULL,
    CONSTRAINT FK_Incomes_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Income_CategoryId')
    CREATE INDEX IX_Income_CategoryId ON dbo.Incomes (CategoryId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Income_IsActive')
    CREATE INDEX IX_Income_IsActive ON dbo.Incomes (IsActive);
GO

IF OBJECT_ID(N'dbo.Expenses', N'U') IS NULL
CREATE TABLE dbo.Expenses (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY,
    FamilyId       INT               NOT NULL,
    FamilyMemberId INT               NOT NULL,
    CategoryId     UNIQUEIDENTIFIER  NOT NULL,
    Amount         DECIMAL(18,2)     NOT NULL,
    ExpenseDate    DATETIME2         NOT NULL,
    ExpenseType    NVARCHAR(50)      NOT NULL,
    PaymentMethod  NVARCHAR(50)      NOT NULL,
    SpendingType   NVARCHAR(50)      NOT NULL,
    IsEssential    BIT               NOT NULL,
    IsRecurring    BIT               NOT NULL,
    Notes          NVARCHAR(500)     NULL,
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_Expenses_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt      DATETIME2         NULL,
    CONSTRAINT FK_Expenses_Categories_CategoryId     FOREIGN KEY (CategoryId)     REFERENCES dbo.Categories (Id),
    CONSTRAINT FK_Expenses_Families_FamilyId         FOREIGN KEY (FamilyId)       REFERENCES dbo.Families (Id),
    CONSTRAINT FK_Expenses_FamilyMembers_MemberId    FOREIGN KEY (FamilyMemberId) REFERENCES dbo.FamilyMembers (Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expense_CategoryId')
    CREATE INDEX IX_Expense_CategoryId ON dbo.Expenses (CategoryId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expense_FamilyId')
    CREATE INDEX IX_Expense_FamilyId ON dbo.Expenses (FamilyId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Expense_FamilyMemberId')
    CREATE INDEX IX_Expense_FamilyMemberId ON dbo.Expenses (FamilyMemberId);
GO
