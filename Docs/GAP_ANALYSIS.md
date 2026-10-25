# GharMitra — Architecture & Business Gap Analysis

**Reviewed:** 25 Oct 2026 · **Role:** Solution Architect / Business Analyst
**Business objective under test:**

> *How much money can a family safely spend today without breaking any bill, loan, budget or goal?*

**Verdict in one line:** the layering is sound, but the system cannot answer the objective
and cannot safely hold real family data. Both are fixable cheaply — there are 7 rows of
real data in the database today.

---

## 1. Current state (verified against the live database, not the source tree)

11 tables exist. The source script `Database/01_Schema.sql` contains only 5 of them.

| Table | Rows | FamilyId | FK constraints | Code behind it |
|-------|------|----------|----------------|----------------|
| Families | 1 | n/a | — | Yes |
| FamilyMembers | 1 | Yes | Yes | Yes |
| Categories | 2 | **No** | — | Yes |
| Incomes | 1 | **No** | Yes (Category) | Yes |
| Expenses | 1 | Yes | Yes (3) | Yes |
| Accounts | 0 | Yes | **None** | **No** |
| PaymentMethods | 5 | **No** | **None** | **No** |
| ExpenseTypes | 6 | **No** | **None** | **No** |
| IncomeSources | 6 | **No** | **None** | **No** |
| SpendingTypes | 3 | **No** | **None** | **No** |
| Relations | 8 | **No** | **None** | **No** |

Six tables were added directly in SSMS. They have no entities, no services, no API, no
foreign keys, and they are not in source control.

---

## 2. Scores

| Dimension | Score | Basis |
|-----------|-------|-------|
| **Final Architecture** | **45 / 100** | Clean layering, correct dependency direction, DTOs at boundary, 39 passing tests. No auth pipeline, no calculation layer, no read models, 6 orphan tables |
| **Database Design** | **32 / 100** | Tenant key missing on 7 of 11 tables, no FKs on 6 tables, a persisted calculated value, lookups not wired, no expense-date index, schema drift |
| **Security** | **10 / 100** | No authentication of any kind. Tenant id taken from the request body. No query is tenant-filtered. Credit: SQL is fully parameterised |
| **Scalability** | **35 / 100** | Pooled ADO.NET and row-level tenancy shape are right. No pagination, missing composite indexes, in-memory joins, no caching |
| **Acceptance Criteria Coverage** | **12 %** | ~4.6 of 37 flow steps implemented |
| **Mandatory Rule Compliance** | **0 of 10** | 8 fail, 2 not yet applicable |

### Mandatory rules scorecard

| # | Rule | Status | Evidence |
|---|------|--------|----------|
| 1 | FamilyId in every business table | **FAIL** | Missing on Categories, Incomes and all 5 lookups |
| 2 | FamilyId never from request body | **FAIL** | `CreateExpenseRequest.FamilyId`, `UpdateExpenseRequest.FamilyId` |
| 3 | FamilyId from authenticated context | **FAIL** | No authentication exists |
| 4 | Every query filtered by FamilyId | **FAIL** | `ExpenseService.GetAllAsync` has no WHERE clause |
| 5 | Role filtering in service layer | **FAIL** | No roles exist |
| 6 | Calculated values not persisted | **FAIL** | `Accounts.CurrentBalance` |
| 7 | Dashboard / Reports dynamic | n/a | Not built — keep it this way |
| 8 | AI depends on historical data | n/a | Not built |
| 9 | Child users never access family data | **FAIL** | No users, no roles |
| 10 | Security not dependent on frontend | **FAIL** | Backend has no security layer at all |

---

## 3. Critical issues

### C1 — No authentication exists anywhere

**Issue.** No `AddAuthentication`, no JWT, no `[Authorize]`, no claims. `Program.cs` line 103
calls `app.UseAuthorization()` with no authentication scheme registered, which is a silent
no-op that reads like security in a code review.
**Business impact.** Every endpoint is anonymous and public. Any caller reaches every
family's finances. Stages 0–5 all depend on knowing who is asking.
**Fix.** JWT bearer auth. `Users`, `Roles`, `Permissions`, `RolePermissions`, `RefreshTokens`.
Resolve `FamilyId`, `FamilyMemberId` and `Role` into claims at login.
**DB change.** 5 new tables. **API change.** Auth controller; `[Authorize]` globally with
explicit `[AllowAnonymous]` on login/register/health. **Priority: CRITICAL**

### C2 — FamilyId is accepted from the request body

**Issue.** `CreateExpenseRequest.FamilyId` and `UpdateExpenseRequest.FamilyId` are client-supplied.
**Business impact.** Classic horizontal privilege escalation (IDOR). A caller writes an
expense into any family by changing one integer, and after C1 is fixed this would still
let an authenticated user attack other tenants.
**Fix.** Delete `FamilyId` from every request DTO. Read it from `ICurrentUser` built from
JWT claims and inject that into the service.
**DB change.** None. **API change.** DTO and controller signature changes. **Priority: CRITICAL**

### C3 — Incomes has no FamilyId and no FamilyMemberId

**Issue.** The income table is not tenant-scoped and does not record whose income it is.
**Business impact.** Family A sees Family B's salary. Privacy requirement §5 ("Wife's
personal income not visible to others") cannot be implemented. Safe-to-spend cannot be
computed per family.
**Fix.** Add both columns, FK to Families and FamilyMembers, make them NOT NULL.
**DB change.** `ALTER TABLE Incomes ADD FamilyId INT NOT NULL, FamilyMemberId INT NOT NULL` +
2 FKs + index. **API change.** Derive both from the authenticated user. **Priority: CRITICAL**

### C4 — No query is filtered by tenant

**Issue.** `ExpenseService.GetAllAsync` issues `SELECT ... FROM Expenses` with no WHERE.
`GetByCategoryIdAsync` filters on category only. Same pattern in Income and Category services.
**Business impact.** Requirement §22 ("Family A must never access Family B") is violated by
the data layer itself, independently of the API.
**Fix.** `FamilyId` becomes the first predicate of every query, passed from `ICurrentUser`,
never from a parameter the caller controls.
**DB change.** None. **API change.** Service method signatures take the resolved tenant.
**Priority: CRITICAL**

### C5 — Category totals are summed across all families

**Issue.** `GetTotalExpensesByCategoryAsync` runs
`SELECT SUM(Amount) FROM Expenses WHERE CategoryId = @CategoryId` — no family predicate.
**Business impact.** The one aggregate the system has is financially wrong. A family's
"total grocery spend" silently includes every other family's groceries. Every Stage 3
calculation built on this pattern inherits the bug.
**Fix.** Add `AND FamilyId = @FamilyId`. Add a regression test asserting two families'
totals stay separate.
**DB change.** Composite index `(FamilyId, CategoryId)`. **API change.** None once C2 lands.
**Priority: CRITICAL**

### C6 — No role-based access control

**Issue.** `FamilyMembers.Role` is free text with no meaning to the system. Nothing
distinguishes a Family Head from a child.
**Business impact.** Rule 9 ("child users must never access family financial data") cannot
be enforced. The Family Head vs member split in §4/§5 does not exist.
**Fix.** Roles and permissions tables; resolve the caller's role in the service and build
the filter from it — family-wide rows for the Head, own rows for a member.
**DB change.** Part of C1. **API change.** Permission checks in services, not controllers.
**Priority: CRITICAL**

### C7 — Accounts.CurrentBalance persists a calculated value

**Issue.** Balance is stored, violating mandatory rule 6, and there is no ledger to rebuild
it from.
**Business impact.** Two sources of truth. Any failed or concurrent write makes the balance
drift from the transactions, and a wrong balance makes safe-to-spend wrong — the one number
the product exists to produce.
**Fix.** Replace with `OpeningBalance` (a fact, set once). Derive current balance as
`OpeningBalance + income − expenses` for that account. Materialise later only if profiling
demands it, and only with a ledger behind it.
**DB change.** Rename/replace the column. **API change.** Balance moves to a computed
read model. **Priority: CRITICAL**

---

## 4. High issues

### H1 — Categories has no FamilyId
Every family shares one global category list; nobody can add a custom category, and renaming
one changes it for all tenants. **Fix:** nullable `FamilyId` — NULL means a system default
visible to all, a value means that family's own category. Seed defaults per family at
registration (Stage 0 step 2). **DB:** add column + index. **Priority: HIGH**

### H2 — The six new tables have no foreign keys
`Accounts.FamilyId` is not constrained to `Families`. Orphan and cross-tenant rows are
possible at the database level. **Fix:** add FKs for every relationship. **Priority: HIGH**

### H3 — Schema drift: the database and source control disagree
Six tables and 28 seed rows exist only in your LocalDB instance. A fresh clone, a new
machine or a teammate gets a broken application. **Fix:** add `02_Lookups.sql` with the
tables plus idempotent seed inserts; make SSMS-first changes a rule you never repeat.
**Priority: HIGH**

### H4 — Lookup tables are populated but not wired to anything
`Expenses.PaymentMethod` is still `nvarchar`, `FamilyMembers.Role` is still `nvarchar`,
while `PaymentMethods` and `Relations` sit unused. "UPI", "upi" and "Upi" remain three
distinct values in every future report. **Fix:** replace the text columns with
`PaymentMethodId` / `RelationId` FKs. Do this now — 1 expense row to migrate.
**DB:** add FK columns, backfill, drop text columns. **API:** DTOs take ids.
**Priority: HIGH**

### H5 — No index supports any date-range query
`Expenses` has no index on `ExpenseDate`. Every monthly and yearly report — the core of
Stage 4 — will table-scan. **Fix:** `CREATE INDEX IX_Expenses_Family_Date ON Expenses
(FamilyId, ExpenseDate) INCLUDE (Amount, CategoryId)`. This is the single most important
index in the system. Mirror it on Incomes. **Priority: HIGH**

### H6 — CORS allows any origin
`AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`. Once auth exists this becomes a real
attack surface. **Fix:** configured origin allow-list per environment. **Priority: HIGH**

### H7 — UTC date validation blocks Indian users for five and a half hours a day
`if (request.ExpenseDate > DateTime.UtcNow) throw` — at 00:30 IST on 25 Oct, `UtcNow` is
24 Oct 19:00, so an expense dated 25 Oct is rejected as "in the future". **Between
midnight and 05:30 IST no user can record today's expense.** The §8 daily reminder has the
same flaw: "yesterday" is computed in UTC, so it fires against the wrong day.
**Fix.** Store `Families.TimeZone` (IANA, e.g. `Asia/Kolkata`). Validate and bucket dates
in family-local time. **DB:** add column. **API:** convert at the service boundary.
**Priority: HIGH**

### H8 — Expenses and Incomes do not link to an Account
Money moves without saying which account it came from, so §11 ("distinguish cash balance,
savings, investments") and safe-to-spend cannot be computed. **Fix:** add nullable
`AccountId` FK to both, required once accounts exist. **Priority: HIGH**

---

## 5. Medium and low issues

| # | Issue | Impact | Fix | Priority |
|---|-------|--------|-----|----------|
| M1 | No pagination on any list endpoint | `GET /expense` returns every row the family ever created | Page size + continuation; cap at 100 | MEDIUM |
| M2 | Mixed primary key types (Guid vs int identity) | Inconsistent joins, confusing for new modules | Pick one rule for all new tables — int identity for high-volume ledgers, Guid elsewhere — and document it | MEDIUM |
| M3 | No audit columns | Cannot answer "who changed this expense" — needed once several members share a family | Add `CreatedBy`, `UpdatedBy` to every business table | MEDIUM |
| M4 | No sub-category support | §6 requires Category → Sub-category (Grocery → Household) | `Categories.ParentCategoryId` self-FK | MEDIUM |
| M5 | No constraint enforcing one Family Head | Service enforces it; a second write path can create two heads | Filtered unique index on `(FamilyId) WHERE IsFamilyHead = 1` | MEDIUM |
| M6 | `Families` lacks TimeZone, Currency, FinancialYearStartMonth | Reminders, reports and the Indian financial year (Apr–Mar) all need them | Add three columns | MEDIUM |
| M7 | Error messages leak parameter names | `"...not found. (Parameter 'categoryId')"` reaches the client | Global exception middleware returning ProblemDetails; log detail server-side | MEDIUM |
| M8 | `UseAuthorization()` with no authentication scheme | Reads as secure in review; does nothing | Remove until C1 lands, then configure properly | MEDIUM |
| L1 | Lookups re-queried on every call | Read-heavy, write-rare data hitting SQL repeatedly | `IMemoryCache` with eviction on write | LOW |
| L2 | No rate limiting | Credential stuffing once login exists | ASP.NET rate limiter on auth endpoints | LOW |
| L3 | `FamilyService.GetAllAsync` joins in memory | Fine at 1 family, bad at 50,000 | Single joined query | LOW |

---

## 6. Table verdicts

### A. Missing tables — 19 still required

| Group | Tables |
|-------|--------|
| Auth / RBAC (Phase 1) | Users, Roles, Permissions, RolePermissions, RefreshTokens |
| Expense (Phase 2) | RecurringExpenses, ExpenseEntryLog |
| Budget (Phase 4) | Budgets, BudgetCategories |
| Loan (Phase 5) | Loans, LoanPayments, LoanScenarios |
| Goals (Phase 6) | Goals, GoalContributions |
| Investment (Phase 7) | Investments, InvestmentTransactions |
| Planning (Phase 16) | PlannedExpenses |
| Notifications (Phase 9) | Notifications, NotificationPreferences |

11 existing + 19 = **30 tables** at full scope.

### B. Unnecessary tables

Nothing existing should be dropped, but two are over-normalised:

- **ExpenseTypes** (Need / Want) and **SpendingTypes** (Daily / Monthly / Yearly) are closed
  sets that will never change. A table, an FK and a join buy nothing over a C# enum stored
  as `int`. **PaymentMethods**, **IncomeSources** and **Relations** are open sets families
  will extend — those are correctly tables.
- *Rule of thumb:* closed set → enum; open set → table.

**Never create these tables:** Reports, Dashboard, SafeToSpend, FinancialHealthScore,
BudgetUtilization, SavingsRate. All are Stage 3 derivations (mandatory rules 6 and 7).

### C. Missing columns

| Table | Add |
|-------|-----|
| Incomes | FamilyId, FamilyMemberId, AccountId, CreatedBy, UpdatedBy |
| Categories | FamilyId (nullable), ParentCategoryId, IsSystem |
| Expenses | AccountId, PaymentMethodId, ExpenseTypeId→enum, SpendingTypeId→enum, RecurringExpenseId, LoanId, CreatedBy, UpdatedBy |
| Families | TimeZone, CurrencyCode, FinancialYearStartMonth |
| FamilyMembers | UserId, RelationId, DateOfBirth |
| Accounts | OpeningBalance (replacing CurrentBalance) |

### D. Wrong relationships

1. `Expenses.FamilyMemberId` and `Expenses.FamilyId` are independent FKs — the database
   permits a member from another family. Enforced in the service only. Fix with a composite
   FK to `FamilyMembers (Id, FamilyId)`.
2. `Incomes → Categories` crosses tenants because neither side has a family key.
3. `Expenses → Categories` does not enforce `Type = Expense` at the database level.
4. No `Expense → Account` or `Income → Account` relationship at all.
5. `Accounts.FamilyId` has no FK.

### E–H

- **E Security risks:** C1, C2, C4, C5, C6, H6, M7, M8. One genuine strength: all SQL is
  parameterised, so there is no injection exposure.
- **F Multi-tenant issues:** C3, C4, C5, H1, H2, plus the 5 unscoped lookup tables.
- **G Performance:** H5, M1, L1, L3.
- **H Acceptance violations:** the mandatory-rules table in section 2.

---

## 7. Missing modules

Authentication · Authorisation / RBAC · Account management · Recurring expenses ·
Daily reminder workflow · Budgets · Loans and EMI tracking · Goals · Investments ·
Planned future expenses · **Calculation engine (Stage 3)** · Dashboard read model ·
Reports · Notifications · AI insight layer

The calculation engine is the one nobody plans for. It is not CRUD and it is where the
business objective actually lives.

---

## 8. Refactoring recommendations, in order

1. **Reconcile the database with source control.** Write `02_Lookups.sql` for the six SSMS
   tables plus seed data. Nothing else starts until the schema is reproducible.
2. **Fix tenancy while there are 7 rows.** Add `FamilyId` to Incomes and Categories, add the
   missing FKs, add `(FamilyId, ExpenseDate)` indexes. Drop and recreate rather than migrate.
3. **Build auth.** Users, roles, permissions, JWT, refresh tokens.
4. **Introduce `ICurrentUser`** (FamilyId, FamilyMemberId, Role) resolved from claims and
   injected into every service. Delete `FamilyId` from all request DTOs.
5. **Make tenancy unavoidable, not remembered.** Give every service a base helper that
   builds the `WHERE FamilyId = @FamilyId` predicate, so a developer has to work to omit it.
   Add a test asserting two families never see each other's rows.
6. **Wire the lookups** — FK columns replacing free text; enums for the two closed sets.
7. **Add the calculation layer** as pure functions over query results in Application —
   no tables, fully unit-testable without a database.
8. **Then build modules** in dependency order: Accounts → Recurring → Budgets → Loans →
   Goals → Investments → Dashboard → Reports → Notifications → AI.

---

## 9. Production readiness

**NOT READY. Do not deploy with real family data.**

| Gate | Status |
|------|--------|
| Authentication | Absent |
| Tenant isolation | Absent — any caller reads any family |
| Authorisation / RBAC | Absent |
| Financial correctness | Failing — aggregates cross tenants |
| Audit trail | Absent |
| Reproducible schema | Failing — 6 tables outside source control |
| Transport security | Partial — HTTPS redirect outside Development only |
| Rate limiting / abuse protection | Absent |
| Error handling | Partial — internals leak to clients |
| Observability | Console logging only |
| Backup / recovery | Not defined |
| Automated tests | Present — 39 unit tests, no integration or security tests |

**Earliest safe pilot** (your own family, single tenant, not published): after items 1–5 of
section 8.
**Public multi-family release:** after the full Phase 1–4 set plus integration tests, audit
trail and a defined backup policy.

---

## 10. What is genuinely good

Worth protecting during the refactor:

- Layer boundaries are correct and the dependency direction is clean.
- Domain entities (`Income`, `Category`) enforce their own invariants in constructors.
- DTOs at the API boundary; entities never leak.
- All SQL is parameterised — no injection surface.
- Soft deletes on master data.
- 39 unit tests that run without a database, with a connection factory that fails any test
  touching SQL. That is a better test design than most projects at this stage.

The problem is not the code that exists. It is that the security and calculation layers
the business objective depends on have not been written yet — and the current data volume
makes this the cheapest moment in the project's life to fix it.
