# GharMitra API

Clean-architecture REST API for **household income and expense management**, built with **.NET 10**, **ADO.NET (`Microsoft.Data.SqlClient`)**, and **SQL Server**.

GharMitra (घर मित्र — House Friend) helps a family:

- Manage **families** and **family members**
- Organize money with **income** and **expense categories**
- Record **income** (salary, rent received, and other sources)
- Record **expenses** against a family member, category, payment method, and spending type

This is not a property-listing product. Properties, listings, and ratings are out of scope.

---

## Architecture

```
GharMitra.Api            Controllers, middleware, Program.cs
GharMitra.Application    Use-case interfaces and DTOs
GharMitra.Domain         Entities and domain rules
GharMitra.Infrastructure ADO.NET connection factory and service implementations
GharMitra.Shared         Shared utilities (placeholder layer)
```

---

## Getting started

Prerequisites: .NET 10 SDK, SQL Server LocalDB or Express.

1. Create the database objects once by running `Database/01_Schema.sql` (SSMS, or `sqlcmd -S "(localdb)\MSSQLLocalDB" -i Database/01_Schema.sql`). The script is idempotent and never alters existing tables.
2. Set the connection string below.
3. Run:

```bash
dotnet restore
dotnet run --project GharMitra.Api
```

The API connects through plain ADO.NET. It does not create, migrate or alter any database object.

Connection string (`appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=GharMitraDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Typical URLs:

- Swagger UI: app root in Development
- Health: `GET /health` or `GET /api/v1/health`

---

## API endpoints

Base path: `/api/v1`

### Families

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/family` | List families and members |
| GET | `/family/{id}` | Get one family |
| POST | `/family` | Create family |
| PUT | `/family/{id}` | Update family |
| DELETE | `/family/{id}` | Soft-deactivate family and members |
| GET | `/family/{familyId}/members` | List members |
| POST | `/family/{familyId}/members` | Add member |
| PUT | `/family/{familyId}/members/{memberId}` | Update member |
| DELETE | `/family/{familyId}/members/{memberId}` | Soft-deactivate member |

```json
POST /api/v1/family
{ "name": "Sharma Family" }

POST /api/v1/family/1/members
{ "name": "Ravi", "role": "Father", "isFamilyHead": true }
```

### Categories

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/category` | List categories |
| GET | `/category/{id}` | Get one category |
| POST | `/category` | Create category (`type`: 1 = Expense, 2 = Income) |
| PUT | `/category/{id}` | Update category |
| DELETE | `/category/{id}` | Soft-deactivate category |

```json
POST /api/v1/category
{ "name": "Groceries", "type": 1, "description": "Food and household", "displayOrder": 1 }
```

### Income

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/income` | List active income |
| GET | `/income/{id}` | Get one income |
| GET | `/income/category/{categoryId}` | List by income category |
| POST | `/income` | Create income |
| PUT | `/income/{id}` | Update income |
| DELETE | `/income/{id}` | Delete income |
| PATCH | `/income/{id}/deactivate` | Soft-deactivate |
| PATCH | `/income/{id}/activate` | Reactivate |

```json
POST /api/v1/income
{
  "sourceName": "Monthly salary",
  "amount": 75000,
  "incomeDate": "2026-10-01",
  "categoryId": "00000000-0000-0000-0000-000000000000"
}
```

### Expenses

An expense must belong to an **active family**, an **active member of that family**, and an **active expense category**.

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/expense` | List expenses |
| GET | `/expense/{id}` | Get one expense |
| GET | `/expense/category/{categoryId}` | List by expense category |
| GET | `/expense/category/{categoryId}/total` | Sum by category |
| POST | `/expense` | Create expense |
| PUT | `/expense/{id}` | Update expense |
| DELETE | `/expense/{id}` | Delete expense |

```json
POST /api/v1/expense
{
  "familyId": 1,
  "familyMemberId": 1,
  "categoryId": "00000000-0000-0000-0000-000000000000",
  "amount": 1250.50,
  "expenseDate": "2026-10-11",
  "expenseType": "Need",
  "paymentMethod": "UPI",
  "spendingType": "Monthly",
  "isEssential": true,
  "isRecurring": false,
  "notes": "Weekly groceries"
}
```

---

## Typical flow

1. Create a family, then add members.
2. Create income and expense categories.
3. Record income against an income category.
4. Record expenses against a family member and an expense category.

---

## Database changes

There are no EF migrations. To change the schema, add or edit a SQL script under `Database/` (for example `02_Budgets.sql`), make it idempotent (`IF OBJECT_ID(...) IS NULL`), and run it in SSMS.
