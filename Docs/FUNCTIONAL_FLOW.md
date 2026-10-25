# GharMitra — Functional Flow (start to last process)

How a family actually uses the system, from the day they sign up to the day the AI
advises them. Build order follows this flow.

---

## The one question the whole system answers

```
How much money can this family safely spend today,
without breaking a bill, a loan, a budget or a goal?
```

Every module exists to make that number correct.

---

## Actors

| Actor | Can see | Typical actions |
|-------|---------|-----------------|
| Family Head | Whole family picture | Budgets, goals, loans, approvals, family reports |
| Adult member (Wife / Father / Mother) | Own data + what is shared | Record own income and expenses, own budget, own goals |
| Child member | Own data only | Record own expenses, see own pocket money |
| Viewer | Read-only subset | See shared summaries only |

Rule applied on **every** read: filter by the caller's `FamilyId` first, then let the
role decide family-level vs own-only rows. Never trust React for this.

---

## STAGE 0 — Onboarding (once per family)

```
1. Register
   -> create Family (the tenant)
   -> create first User (login)
   -> create FamilyMember for that user, IsFamilyHead = true
   -> assign Role = FamilyHead

2. System seeds default Categories for the family
   Expense: Grocery, Utilities, Loan, Medical, Education, Travel, Shopping, ...
   Income:  Salary, Rent received, Interest, Other

3. Family Head adds other members (Father, Mother, Wife, Son...)
   -> optionally invites each one as a User with a Role

4. Create Accounts and opening balances
   Cash 5,000 | Bank 80,000 | Wallet 2,000
```

Output: an empty but correctly scoped family workspace.

---

## STAGE 1 — Financial baseline (once, then edited rarely)

The family tells the system what is already true about their money.

```
5.  Income sources      Salary 1,00,000 on the 1st
6.  Recurring expenses  EMI 31,000 | Maintenance 3,000 | Electricity 2,000
                        Wi-Fi 1,000 | Gas 800 | LIC 5,000 | SIP 10,000
7.  Loans               Home loan: principal, rate, EMI, tenure, start date
8.  Investments         Existing MF / LIC / FD / gold with current value
9.  Budgets             Grocery 10,000 | Travel 3,000 | Shopping 4,000 ...
10. Goals               Vacation 1,20,000 by Dec 2027, saved 30,000
11. Planned expenses    Property tax 12,000 due in 6 months
                        School fees 25,000 due in 3 months
```

Output: the system now knows every rupee that is already committed.

---

## STAGE 2 — Daily and monthly loop (runs forever)

This is the only stage the family touches most days.

```
DAILY
  12. Member opens app -> "Add expense"
      amount, category, payment method, need/want, essential?, notes
      -> saved against that member, that family, that account
  13. If no expense recorded by 9 PM -> reminder
      "You have not recorded yesterday's expenses"
      -> member confirms "nothing spent" -> reminder stops for that day

MONTHLY
  14. Salary credited        -> record Income
  15. Due recurring items    -> system shows the list on the due date
                                member taps Confirm -> becomes a real Expense
  16. EMI paid               -> one Expense + one LoanPayment row
                                split into interest and principal automatically
  17. Extra principal paid   -> LoanPayment flagged as prepayment
  18. Goal contribution      -> GoalContribution row, goal progress moves
  19. Investment instalment  -> InvestmentTransaction row
```

Output: a complete, categorised, member-tagged transaction history.

---

## STAGE 3 — Calculation (system, continuous — stores nothing)

Everything here is derived from Stage 1 and 2. Do **not** create tables for it.

```
Income
  - Actual expenses this month
  - Investment contributions
  - Loan payments
  - Reserved for planned future expenses   (planned amount / months remaining)
  - Goal contributions
  ------------------------------------
  = SAFE TO SPEND
```

Also computed:

| Figure | Formula |
|--------|---------|
| Savings rate | (Income - Expense) / Income |
| Budget utilisation | Spent in category / Budget for category |
| Goal monthly need | (Target - Saved) / months remaining |
| Goal achievable? | Goal monthly need <= Safe to spend |
| Loan outstanding | Principal - sum of principal paid |
| Projected closure | Amortisation using current EMI + prepayment pattern |
| Financial health | Savings rate, budget discipline, EMI-to-income ratio, emergency months |

---

## STAGE 4 — Output to the family

```
20. Dashboard     In | Out | Left | Coming up | Warnings
21. Reports       Daily / Monthly / Yearly, category-wise, member-wise (if allowed)
22. Alerts        Budget 80% used | Bill due in 3 days | Goal behind schedule
23. AI insights   "Grocery is up 15% vs your 6-month average"
```

---

## STAGE 5 — Decide and plan (closes the loop)

```
24. "Can I afford 50,000 this month?"
      -> compare against Safe to Spend -> yes / no + what it would break
25. "Should I prepay 50,000 on the home loan?"
      -> Scenario A (EMI only) vs B (+20,000) vs C (+50,000)
      -> shows closure date, interest saved, tenure cut
26. Adjust budget / goal / prepayment plan
      -> back to STAGE 1, numbers recalculate
```

---

## Worked example — one real month

Income 1,00,000.

| Item | Amount |
|------|--------|
| Fixed bills (maintenance, electricity, Wi-Fi, gas) | 6,800 |
| Variable (grocery 10,000, travel 3,000, father 5,000, personal 5,000) | 23,000 |
| Home loan EMI (interest 22,000 + principal 9,000) | 31,000 |
| Investments (LIC 5,000 + SIP 10,000) | 15,000 |
| Reserved for property tax (12,000 / 6 months) | 2,000 |
| Vacation goal (90,000 remaining / 14 months) | 6,429 |
| **Total committed** | **84,229** |
| **Safe to spend** | **15,771** |

Family asks: *"Can I afford a 50,000 purchase this month?"*

System answers **No** — and explains:

- Safe to spend is 15,771, short by 34,229
- Delay 4 months and it is affordable with no change
- Or pause the vacation goal for 5 months — vacation slips to May 2028
- Or use the emergency fund — but that drops cover from 4 months to 2.4

That explanation is the product. An expense tracker only shows the 50,000 after it is spent.

---

## Module dependency order (what blocks what)

```
Auth | Family | User | Role | Permission     <- nothing works correctly without this
            |
            v
      Category --+--> Income ---+
                 +--> Expense --+--> Account balance
                       |
                       +--> Budget            (needs Expense + Category)
                       +--> Recurring expense (needs Expense)
                       +--> Planned expense   (needs Expense)
                       |
        Loan ----------+
        Investment ----+
        Goal ----------+
                       |
                       v
            Dashboard | Reports       (needs everything above)
                       |
                       v
               Notifications          (needs Budget + Recurring + Goal)
                       |
                       v
                      AI              (needs all of it + 6-12 months of history)
```

**AI is last for a reason beyond priority:** it cannot say "grocery is up 15%" until
there are several months of grocery history to compare against. Shipping it early
produces generic advice, which the requirement explicitly rejects.

---

## Privacy applied per stage

| Stage | Family Head sees | Member sees |
|-------|------------------|-------------|
| Expense list | All members | Own rows only |
| Income | Family total + per member | Own income only |
| Budget | Family + category budgets | Own personal budget |
| Goal | Family goals + own | Own goals + family goals marked shared |
| Loan | Full detail | Only that the EMI exists, not the balance, unless shared |
| Reports | Consolidated | Own slice |
| Account balance | All accounts | Own wallet only |

Enforce in the service layer: resolve the caller to their FamilyId and Role, then build
the filter from that. The API must never accept a FamilyId from the request body.
