/* GharMitra - 05 Wire the lookups, seed system categories
   Fixes Docs/GAP_ANALYSIS.md H4 (lookup tables populated but not referenced)
   and M4 (no sub-category support), and supplies the Stage 0 category seed.

   What changes on Expenses:
     PaymentMethod  nvarchar -> PaymentMethodId  FK to PaymentMethods
     SpendingType   nvarchar -> SpendingTypeId   FK to SpendingTypes
     ExpenseType    nvarchar -> SubCategoryId    FK to Categories (sub-category)
                             +  NeedOrWant       1 = Need, 2 = Want   (requirement 6)

   The three text columns are backfilled, then dropped. Back up first if the
   database holds anything you care about.

   Run order: 01 -> 02 -> 03 -> 04 -> 05
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE GharMitraDb;
GO

/* =============================================================
   1. System categories (FamilyId NULL = shared by every family)

   Seeding these once globally means a new family starts with a usable
   list without copying 25 rows per tenant. A family adds its own on top.
   ============================================================= */
INSERT INTO dbo.Categories (Id, Name, Description, Type, IsActive, DisplayOrder, FamilyId, IsSystem, CreatedAt)
SELECT NEWID(), v.Name, NULL, 1, 1, v.Ord, NULL, 1, SYSUTCDATETIME()
FROM (VALUES
    (N'Grocery', 10), (N'Household', 20), (N'Utilities', 30), (N'Loan & EMI', 40),
    (N'Medical', 50), (N'Education', 60), (N'Travel', 70), (N'Transport', 80),
    (N'Shopping', 90), (N'Entertainment', 100), (N'Personal', 110),
    (N'Family Function', 120), (N'Insurance', 130), (N'Maintenance', 140),
    (N'Investment', 150), (N'Savings', 160), (N'Support to Family', 170),
    (N'Emergency', 180), (N'Miscellaneous', 999)
) AS v(Name, Ord)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Categories c
    WHERE c.FamilyId IS NULL AND c.Type = 1 AND c.Name = v.Name);
GO

INSERT INTO dbo.Categories (Id, Name, Description, Type, IsActive, DisplayOrder, FamilyId, IsSystem, CreatedAt)
SELECT NEWID(), v.Name, NULL, 2, 1, v.Ord, NULL, 1, SYSUTCDATETIME()
FROM (VALUES
    (N'Salary', 10), (N'Business', 20), (N'Freelancing', 30), (N'Rental Income', 40),
    (N'Interest', 50), (N'Bonus', 60), (N'Other Income', 999)
) AS v(Name, Ord)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Categories c
    WHERE c.FamilyId IS NULL AND c.Type = 2 AND c.Name = v.Name);
GO

/* =============================================================
   2. New columns on Expenses
   ============================================================= */
IF COL_LENGTH(N'dbo.Expenses', N'PaymentMethodId') IS NULL
    ALTER TABLE dbo.Expenses ADD PaymentMethodId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'dbo.Expenses', N'SpendingTypeId') IS NULL
    ALTER TABLE dbo.Expenses ADD SpendingTypeId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'dbo.Expenses', N'SubCategoryId') IS NULL
    ALTER TABLE dbo.Expenses ADD SubCategoryId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'dbo.Expenses', N'NeedOrWant') IS NULL
    ALTER TABLE dbo.Expenses ADD NeedOrWant TINYINT NOT NULL
        CONSTRAINT DF_Expenses_NeedOrWant DEFAULT 1;   -- 1 Need, 2 Want
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Expenses_NeedOrWant')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT CK_Expenses_NeedOrWant
        CHECK (NeedOrWant IN (1, 2));
GO

/* =============================================================
   3. Backfill from the free-text columns, matching on name
   ============================================================= */
IF COL_LENGTH(N'dbo.Expenses', N'PaymentMethod') IS NOT NULL
    EXEC sp_executesql N'
        UPDATE e SET PaymentMethodId = p.Id
        FROM dbo.Expenses e
        JOIN dbo.PaymentMethods p ON p.MethodName = e.PaymentMethod
        WHERE e.PaymentMethodId IS NULL;';
GO

IF COL_LENGTH(N'dbo.Expenses', N'SpendingType') IS NOT NULL
    EXEC sp_executesql N'
        UPDATE e SET SpendingTypeId = s.Id
        FROM dbo.Expenses e
        JOIN dbo.SpendingTypes s ON s.TypeName = e.SpendingType
        WHERE e.SpendingTypeId IS NULL;';
GO

/* ExpenseType held category-like values (Household, Medical, Travel...).
   They map onto the system categories seeded above. */
IF COL_LENGTH(N'dbo.Expenses', N'ExpenseType') IS NOT NULL
    EXEC sp_executesql N'
        UPDATE e SET SubCategoryId = c.Id
        FROM dbo.Expenses e
        JOIN dbo.Categories c ON c.Name = e.ExpenseType AND c.FamilyId IS NULL AND c.Type = 1
        WHERE e.SubCategoryId IS NULL;';
GO

/* Anything that did not match falls back to a sane default rather than
   blocking the migration. */
IF EXISTS (SELECT 1 FROM dbo.Expenses WHERE PaymentMethodId IS NULL)
    UPDATE dbo.Expenses
    SET PaymentMethodId = (SELECT TOP 1 Id FROM dbo.PaymentMethods WHERE MethodName = N'Cash')
    WHERE PaymentMethodId IS NULL;
GO
IF EXISTS (SELECT 1 FROM dbo.Expenses WHERE SpendingTypeId IS NULL)
    UPDATE dbo.Expenses
    SET SpendingTypeId = (SELECT TOP 1 Id FROM dbo.SpendingTypes WHERE TypeName = N'Planned')
    WHERE SpendingTypeId IS NULL;
GO

/* =============================================================
   4. Constraints and indexes
   ============================================================= */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_PaymentMethods')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_PaymentMethods
        FOREIGN KEY (PaymentMethodId) REFERENCES dbo.PaymentMethods (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_SpendingTypes')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_SpendingTypes
        FOREIGN KEY (SpendingTypeId) REFERENCES dbo.SpendingTypes (Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expenses_Categories_SubCategoryId')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_Categories_SubCategoryId
        FOREIGN KEY (SubCategoryId) REFERENCES dbo.Categories (Id);
GO

/* Required once every row carries a value. */
IF NOT EXISTS (SELECT 1 FROM dbo.Expenses WHERE PaymentMethodId IS NULL)
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Expenses')
               AND name = N'PaymentMethodId' AND is_nullable = 1)
    ALTER TABLE dbo.Expenses ALTER COLUMN PaymentMethodId UNIQUEIDENTIFIER NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Expenses WHERE SpendingTypeId IS NULL)
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Expenses')
               AND name = N'SpendingTypeId' AND is_nullable = 1)
    ALTER TABLE dbo.Expenses ALTER COLUMN SpendingTypeId UNIQUEIDENTIFIER NOT NULL;
GO

/* =============================================================
   5. Drop the free-text columns they replace
   ============================================================= */
IF COL_LENGTH(N'dbo.Expenses', N'PaymentMethod') IS NOT NULL
    ALTER TABLE dbo.Expenses DROP COLUMN PaymentMethod;
GO
IF COL_LENGTH(N'dbo.Expenses', N'SpendingType') IS NOT NULL
    ALTER TABLE dbo.Expenses DROP COLUMN SpendingType;
GO
IF COL_LENGTH(N'dbo.Expenses', N'ExpenseType') IS NOT NULL
    ALTER TABLE dbo.Expenses DROP COLUMN ExpenseType;
GO

/* ExpenseTypes is now redundant: its values live in Categories as system
   sub-categories. It is left in place rather than dropped, so nothing you
   built against it breaks. Drop it when you are satisfied:
       DROP TABLE dbo.ExpenseTypes;
*/

PRINT N'05_WireLookups.sql completed.';
GO
