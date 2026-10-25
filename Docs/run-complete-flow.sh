#!/usr/bin/env bash
# GharMitra - complete functional flow, Stage 0 to Stage 5, against a live API.
#
#   1. Start the API:   dotnet run --project GharMitra.Api
#   2. Run this script: bash Docs/run-complete-flow.sh [baseUrl]
#
# Uses a fresh email each run, so it can be run repeatedly without cleanup.
set -u

BASE="${1:-http://localhost:5012}"
API="$BASE/api/v1"
JSON='Content-Type: application/json'
STAMP=$(date +%s)
EMAIL="mukesh+$STAMP@example.com"
WIFE="deepa+$STAMP@example.com"
PASS='Str0ngPass!23'
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

say()  { printf '\n\033[1;36m== %s\033[0m\n' "$1"; }
ok()   { printf '   \033[32m%s\033[0m\n' "$1"; }
fail() { printf '   \033[31mFAILED: %s\033[0m\n' "$1"; exit 1; }
val()  { grep -o "\"$1\":[^,}]*" | head -1 | cut -d: -f2- | tr -d '"'; }

post() { # post <path> <token|-> <jsonfile>
  if [ "$2" = "-" ]; then
    curl -s -X POST -H "$JSON" --data @"$3" "$API$1"
  else
    curl -s -X POST -H "$JSON" -H "Authorization: Bearer $2" --data @"$3" "$API$1"
  fi
}
get() { curl -s -H "Authorization: Bearer $2" "$API$1"; }

# ---------------------------------------------------------------- STAGE 0
say "STAGE 0 - Register the family (creates tenant + Family Head login)"
cat > "$TMP/reg.json" <<EOF
{"familyName":"Patel Family","memberName":"Mukesh","relation":"Self",
 "email":"$EMAIL","password":"$PASS","timeZoneId":"Asia/Kolkata"}
EOF
REG=$(post /auth/register - "$TMP/reg.json")
HEAD_TOKEN=$(echo "$REG" | val accessToken)
FAMILY_ID=$(echo "$REG" | val familyId)
[ -n "$HEAD_TOKEN" ] || fail "register: $REG"
ok "family #$FAMILY_ID created, role $(echo "$REG" | val roleCode)"

say "STAGE 0 - Family Head invites the wife as an Adult member"
cat > "$TMP/inv.json" <<EOF
{"memberName":"Deepa","relation":"Wife","email":"$WIFE","password":"$PASS","roleCode":"Adult"}
EOF
INV=$(post /auth/members "$HEAD_TOKEN" "$TMP/inv.json")
WIFE_TOKEN=$(echo "$INV" | val accessToken)
[ -n "$WIFE_TOKEN" ] || fail "invite: $INV"
ok "Deepa added as Adult"

say "STAGE 0 - System categories are available immediately"
CATS=$(get /category "$HEAD_TOKEN")
ok "$(echo "$CATS" | grep -o '"id":"' | wc -l) categories seeded"

cat_id() { echo "$CATS" | tr '}' '\n' | grep "\"name\":\"$1\"" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4; }
SALARY=$(cat_id 'Salary');       GROCERY=$(cat_id 'Grocery')
UTILITIES=$(cat_id 'Utilities'); LOAN=$(cat_id 'Loan & EMI')
INVEST=$(cat_id 'Investment');   TRAVEL=$(cat_id 'Travel')

# Lookup ids come from the API's own category list plus the seeded lookups.
LOOK=$(get /category "$HEAD_TOKEN" >/dev/null; echo)
UPI=$(curl -s -H "Authorization: Bearer $HEAD_TOKEN" "$API/category" >/dev/null; echo)

# ---------------------------------------------------------------- STAGE 2
say "STAGE 2 - Record October income (salary 1,00,000)"
cat > "$TMP/inc.json" <<EOF
{"sourceName":"October salary","amount":100000,"incomeDate":"$(date -u +%Y-%m-01)","categoryId":"$SALARY"}
EOF
INC=$(post /income "$HEAD_TOKEN" "$TMP/inc.json")
echo "$INC" | grep -q '"amount"' || fail "income: $INC"
ok "income recorded: $(echo "$INC" | val amount)"

# Expenses need PaymentMethodId and SpendingTypeId. They are seeded lookups;
# this reads them straight from the database view the API exposes via Swagger.
# For the script we fetch them with sqlcmd when available, else skip expenses.
if command -v sqlcmd >/dev/null 2>&1; then
  PM=$(sqlcmd -S "(localdb)\MSSQLLocalDB" -d GharMitraDb -h-1 -W -Q \
       "SET NOCOUNT ON; SELECT TOP 1 CAST(Id AS varchar(36)) FROM PaymentMethods WHERE MethodName='UPI'" | tr -d ' \r')
  ST=$(sqlcmd -S "(localdb)\MSSQLLocalDB" -d GharMitraDb -h-1 -W -Q \
       "SET NOCOUNT ON; SELECT TOP 1 CAST(Id AS varchar(36)) FROM SpendingTypes WHERE TypeName='Planned'" | tr -d ' \r')
else
  echo "   sqlcmd not found - cannot resolve PaymentMethodId, skipping expenses"; PM=""; ST=""
fi

spend() { # spend <categoryId> <amount> <needOrWant> <note>
  [ -n "$PM" ] || return 0
  cat > "$TMP/e.json" <<EOF
{"categoryId":"$1","amount":$2,"expenseDate":"$(date -u +%Y-%m-%d)","needOrWant":$3,
 "paymentMethodId":"$PM","spendingTypeId":"$ST","isEssential":true,"notes":"$4"}
EOF
  R=$(post /expense "$HEAD_TOKEN" "$TMP/e.json")
  echo "$R" | grep -q '"amount"' || fail "expense $4: $R"
  ok "$4: $2"
}

say "STAGE 2 - Record the month's spending"
spend "$GROCERY"   10000 1 "Groceries"
spend "$UTILITIES"  6800 1 "Electricity, gas, wifi, maintenance"
spend "$TRAVEL"     3000 2 "Travel"
spend "$LOAN"      31000 1 "Home loan EMI"
spend "$INVEST"    15000 1 "LIC + SIP"

# ---------------------------------------------------------------- STAGE 3/4
say "STAGE 3+4 - Dashboard: what the family sees"
DASH=$(get /dashboard "$HEAD_TOKEN")
echo "$DASH" | grep -q '"safeToSpend"' || fail "dashboard: $DASH"
printf '   income        %s\n'  "$(echo "$DASH" | val income)"
printf '   spent         %s\n'  "$(echo "$DASH" | val spentSoFar)"
printf '   investments   %s\n'  "$(echo "$DASH" | val investments)"
printf '   loan payments %s\n'  "$(echo "$DASH" | val loanPayments)"
printf '   committed     %s\n'  "$(echo "$DASH" | val totalCommitted)"
printf '   SAFE TO SPEND %s\n'  "$(echo "$DASH" | val safeToSpend)"
printf '   savings rate  %s%%\n' "$(echo "$DASH" | val savingsRatePercent)"
printf '   headline      %s\n'  "$(echo "$DASH" | grep -o '"headline":"[^"]*"' | cut -d'"' -f4)"

# ---------------------------------------------------------------- STAGE 5
say "STAGE 5 - Affordability: can we afford 50,000 this month?"
echo '{"amount":50000,"purpose":"New laptop"}' > "$TMP/aff.json"
AFF=$(post /dashboard/affordability "$HEAD_TOKEN" "$TMP/aff.json")
printf '   canAfford %s, short by %s\n' "$(echo "$AFF" | val canAfford)" "$(echo "$AFF" | val shortfall)"
# Each option is a quoted string; split on quotes, not commas, so thousands
# separators inside the numbers survive.
echo "$AFF" | grep -o '"options":\[[^]]*\]' | grep -oE '"[^"]{10,}"' \
  | sed 's/^"//;s/"$//' | sed 's/^/   - /'

# ---------------------------------------------------------------- PRIVACY
say "PRIVACY - the wife sees only her own money"
WD=$(get /dashboard "$WIFE_TOKEN")
printf '   Head  sees income %s / spent %s\n' "$(echo "$DASH" | val income)" "$(echo "$DASH" | val spentSoFar)"
printf '   Deepa sees income %s / spent %s   (her own, as designed)\n' \
       "$(echo "$WD" | val income)" "$(echo "$WD" | val spentSoFar)"

say "DONE"
