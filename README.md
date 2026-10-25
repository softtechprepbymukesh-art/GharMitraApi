# GharMitra

**GharMitra (घर मित्र — House Friend)** is a mobile-first family finance and financial-planning platform for middle-class households. It helps a family see where money comes in and goes out, control spending, plan goals, manage loans and investments, and make better financial decisions.

> Help a family understand its money, control its expenses, plan its future, achieve its goals, and reduce financial stress.

**Stack:** ASP.NET Core Web API (.NET 10) · ADO.NET (`Microsoft.Data.SqlClient`) · SQL Server · React.js (planned) · Swagger/OpenAPI

---

## 1. Current status

| Area | Status |
|------|--------|
| Category (income / expense) | Done |
| Income | Done |
| Family and family members (Family Head flag) | Done |
| Expense | Done |
| Authentication, roles, permissions | Next (Phase 1) |
| Multi-tenant isolation (`FamilyId` on every record) | Next (Phase 1/3) |
| Budgets, loans, goals, investments, reports, notifications, AI | Planned (see roadmap) |

**Data access:** the API talks to SQL Server directly through ADO.NET. There is **no EF Core, no migrations and no code-first model**. The app never creates or alters database objects; the schema lives in [Database/01_Schema.sql](Database/01_Schema.sql).

---

## 2. Architecture

Clean architecture with one-way dependencies:

```
GharMitra.Api             Controllers, Swagger, Program.cs (composition root)
      │
      ├── GharMitra.Application      Service interfaces + DTOs (no database code)
      │         │
      │         └── GharMitra.Domain       Entities and business rules (no dependencies)
      │
      └── GharMitra.Infrastructure   ADO.NET services (implements Application interfaces)
                │
                └── Application, Domain

GharMitra.Shared          Shared utilities (placeholder)
Database/                 SQL scripts (schema)
```

### How a request flows

```
HTTP request
  → Controller            (maps HTTP ⇄ DTO, picks status code)
  → I*Service             (interface in Application)
  → *Service              (Infrastructure: validate → SQL → map to DTO)
  → ISqlConnectionFactory (opens a pooled SqlConnection)
  → SQL Server
```

### Infrastructure data layer

| File | Purpose |
|------|---------|
| `Data/ISqlConnectionFactory.cs` | Abstraction used by every service to get an open connection |
| `Data/SqlConnectionFactory.cs` | Opens `SqlConnection` from the configured connection string (registered as a singleton) |
| `Data/SqlCommandExtensions.cs` | Small helpers: `CreateCommand`, fluent typed `.With("@Param", SqlDbType, value)`, nullable readers |
| `Services/*Service.cs` | One service per module; all SQL is parameterised |

### Adding a new module (the pattern to follow)

1. **Domain** – entity (`Domain/Entities/Xyz.cs`) with its business rules.
2. **Application** – `Xyz/DTOs/*` and `Xyz/Interfaces/IXyzService.cs`.
3. **Database** – add the table to `Database/01_Schema.sql` (or a new numbered script, e.g. `02_Budgets.sql`). Every financial table carries `FamilyId` (tenant key).
4. **Infrastructure** – `Services/XyzService.cs` using `ISqlConnectionFactory`.
5. **Api** – `Controllers/XyzController.cs`; register `AddScoped<IXyzService, XyzService>()` in `Program.cs`.

---

## 3. Getting started

Prerequisites: .NET 10 SDK, SQL Server (LocalDB, Express or full) and SSMS.

**Step 1 – create the database (once).** Open [Database/01_Schema.sql](Database/01_Schema.sql) in SSMS and execute it, or run:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -i Database/01_Schema.sql
```

The script is idempotent: it only creates what is missing and never drops or alters existing objects. If you already have a `GharMitraDb` created by the old migrations, its tables are identical, so nothing changes.

**Step 2 – set the connection string** in `GharMitra.Api/appsettings.json` (or override with the environment variable `ConnectionStrings__DefaultConnection`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=GharMitraDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

For SQL authentication use `Server=.;Database=GharMitraDb;User Id=...;Password=...;TrustServerCertificate=True;`. Keep real credentials out of git (use user-secrets or environment variables).

**Step 3 – run**

```bash
dotnet restore
dotnet build
dotnet run --project GharMitra.Api
```

On startup the log shows `Connected to SQL Server '...', database '...'` (or an error naming what to check). Swagger UI is served at the app root in Development; health check is `GET /health`.

Endpoints, request bodies and examples: [GharMitra.Api/README.md](GharMitra.Api/README.md).

### Troubleshooting database connectivity

| Symptom | Fix |
|---------|-----|
| `Cannot connect to the database` at startup | Check server name / instance, that SQL Server (or LocalDB: `sqllocaldb start MSSQLLocalDB`) is running |
| `Invalid object name 'Categories'` | Run `Database/01_Schema.sql` against the same database named in the connection string |
| Certificate / SSL error | Keep `TrustServerCertificate=True` for local development |
| Build error "file is locked" | Stop the running API (Visual Studio / `dotnet run`) before building |

---

## 4. Coding standards

- **Layering:** controllers contain no business or SQL logic; services contain no HTTP concerns; Domain references nothing.
- **Data access:** ADO.NET only, in Infrastructure. Always use parameters (`.With(...)`), never string-concatenate user input into SQL. Use `await using` for connections, commands and readers. Use a `SqlTransaction` when a change touches more than one table or row set.
- **Async everywhere** with `CancellationToken` passed down to every database call.
- **DTOs at the boundary:** never return domain entities from the API.
- **Validation:** request validation in the service (or domain entity); invalid input → `ArgumentException` (HTTP 400), duplicate names → `InvalidOperationException` (HTTP 409, categories), missing → 404.
- **Naming:** `PascalCase` for types and members, `_camelCase` for private fields, `Async` suffix on async methods, one class per file, namespaces match folders.
- **Soft delete** (`IsActive`) for master data (family, member, category); hard delete only where the module explicitly says so.
- **Money** is `decimal(18,2)`; dates are stored in UTC (`datetime2`).
- **Multi-tenancy:** every financial table has `FamilyId`; every query for family data must filter by it (enforced at service/API level, never only in the UI).
- **Secrets** never committed; configuration via `appsettings` + environment variables.

---

## 4a. Testing

`GharMitra.Tests` is an xUnit project (no database required):

```bash
dotnet test GharMitra.Tests
```

| Folder | Covers |
|--------|--------|
| `Domain/` | Business rules of `Income` and `Category` entities |
| `Services/` | Request validation and guard clauses in every service, using a `ThrowingConnectionFactory` that fails the test if the database is touched |
| `Data/` | `SqlConnectionFactory` argument checks and unreachable-server behaviour |

Add tests for each new module alongside its service.

---

## 5. Roadmap

| Phase | Scope |
|-------|-------|
| 1 Foundation | Authentication, family/tenant, users, roles (RBAC), permissions, category, income |
| 2 Expense management | Expense, recurring expense, payment methods, daily tracking, entry reminder |
| 3 Family finance | Members, personal wallets, family summary, privacy rules, family dashboard |
| 4 Budget and savings | Monthly and category budgets, savings, budget alerts |
| 5 Loans | Home loan, EMI, interest/principal, prepayment, scenario comparison, closure projection |
| 6 Goals | Monthly/yearly/custom goals, emergency fund, progress and planning |
| 7 Investments | LIC, mutual funds/SIP, FD/RD, gold/silver, others (manual tracking first) |
| 8 Reports and dashboard | Daily / monthly / yearly reports, charts, financial health dashboard |
| 9 Notifications | Expense, bill, EMI, goal and budget reminders (configurable, no spam) |
| 10 AI | Assistant, budget recommendations, what-if scenarios, trend analysis |
| 11 Family communication | Family chat with privacy-aware sharing |
| 12 Production | Security hardening, performance, cloud, mobile, monitoring, backup, Play Store |

### Key product rules (carried into every phase)

- **Privacy by design:** the Family Head sees the family-level picture; each member sees only their own personal data plus what is explicitly shared. Enforced in API and database queries, not only in React.
- **Data isolation:** Family A can never read Family B. Every API call validates identity, family, role, permission and resource ownership.
- **Clear separation** of actual data, calculations and AI recommendations; AI output is never presented as guaranteed financial advice.
- **Property module** is deferred; a single home is handled through Loan, Assets and Liabilities.

---

## 6. Repository layout

```
GharMitra/
├── Database/                    SQL scripts
├── GharMitra.Api/               Web API host
├── GharMitra.Application/       Interfaces and DTOs
├── GharMitra.Domain/            Entities and rules
├── GharMitra.Infrastructure/    ADO.NET data access and services
├── GharMitra.Shared/            Shared utilities
├── GharMitra.Tests/             xUnit tests
└── README.md
```
