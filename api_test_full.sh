#!/usr/bin/env bash
# =============================================================================
#  microlabb — FULL API TEST SUITE
#  Usage: chmod +x api_test_full.sh && ./api_test_full.sh
#  Env overrides:
#    BASE=http://localhost:5000   (default: Gateway)
#    DIRECT=1                     (bypass gateway, hit services directly)
# =============================================================================

# НЕ используем set -e, чтобы тесты продолжались при ошибках
set -uo pipefail

# --------------- config -------------------------------------------------------
GW="${BASE:-http://localhost:5000}"
IDENTITY="${IDENTITY_BASE:-http://localhost:7001}"
SHOPS="${SHOPS_BASE:-http://localhost:7003}"
PURCHASES="${PURCHASES_BASE:-http://localhost:7002}"

if [[ "${DIRECT:-0}" == "1" ]]; then
  GW_ID="$IDENTITY"
  GW_SH="$SHOPS"
  GW_PU="$PURCHASES"
else
  GW_ID="$GW"
  GW_SH="$GW"
  GW_PU="$GW"
fi

SUFFIX="$(date +%s)_$$"
USER1="user_a_${SUFFIX}"
USER2="user_b_${SUFFIX}"
PASS="Pass123!"

PASS_TOTAL=0
FAIL_TOTAL=0
SKIP_TOTAL=0

TOKEN1=""
TOKEN2=""
USER1_ID=""
TXN_ID=""
SECOND_TXN_ID=""

# --------------- helpers -------------------------------------------------------
c_green=$(tput setaf 2 2>/dev/null || echo "")
c_red=$(tput setaf 1 2>/dev/null || echo "")
c_yellow=$(tput setaf 3 2>/dev/null || echo "")
c_cyan=$(tput setaf 6 2>/dev/null || echo "")
c_bold=$(tput bold 2>/dev/null || echo "")
c_reset=$(tput sgr0 2>/dev/null || echo "")

section() { echo ""; echo "${c_bold}${c_cyan}══════════════════════════════════════════════${c_reset}"; echo "${c_bold}${c_cyan}  $1${c_reset}"; echo "${c_bold}${c_cyan}══════════════════════════════════════════════${c_reset}"; }

pass() { echo "  ${c_green}✔ PASS${c_reset}  $1"; ((PASS_TOTAL++)) || true; }
fail() { echo "  ${c_red}✘ FAIL${c_reset}  $1"; ((FAIL_TOTAL++)) || true; }
skip() { echo "  ${c_yellow}⚠ SKIP${c_reset}  $1"; ((SKIP_TOTAL++)) || true; }
info() { echo "  ${c_yellow}ℹ${c_reset}      $1"; }

# $1=url $2=method $3=body $4=token  → sets RESP and HTTP_CODE
req() {
  local url="$1" method="$2" body="${3:-}" token="${4:-}"
  local args=(-s --max-time 10 -o /tmp/mt_body -w "%{http_code}" -X "$method" -H "Content-Type: application/json")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$body"  ]] && args+=(-d "$body")
  HTTP_CODE=$(curl "${args[@]}" "$url" 2>/dev/null || echo "000")
  RESP=$(cat /tmp/mt_body 2>/dev/null || echo "")
}

jq_val() { echo "$RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); print($1)" 2>/dev/null || echo ""; }

assert_code() {
  local label="$1" want="$2"
  if [[ "$HTTP_CODE" == "$want" ]]; then pass "$label (HTTP $HTTP_CODE)";
  else fail "$label — expected HTTP $want, got $HTTP_CODE | body: ${RESP:0:200}"; fi
}

assert_field() {
  local label="$1" expr="$2" want="$3"
  local got; got=$(jq_val "$expr")
  if [[ "$got" == "$want" ]]; then pass "$label (value='$got')";
  else fail "$label — expected '$want', got '$got' | body: ${RESP:0:200}"; fi
}

assert_nonempty() {
  local label="$1" expr="$2"
  local got; got=$(jq_val "$expr")
  if [[ -n "$got" && "$got" != "None" && "$got" != "null" ]]; then pass "$label";
  else fail "$label — value is empty/null | body: ${RESP:0:200}"; fi
}

assert_contains() {
  local label="$1" needle="$2"
  if echo "$RESP" | grep -qi "$needle"; then pass "$label";
  else fail "$label — response does not contain '$needle' | body: ${RESP:0:200}"; fi
}

assert_not_contains() {
  local label="$1" needle="$2"
  if ! echo "$RESP" | grep -q "$needle"; then pass "$label";
  else fail "$label — response should NOT contain '$needle' | body: ${RESP:0:200}"; fi
}

# =============================================================================
section "0. PREREQUISITES — services reachable"
# =============================================================================

ALL_OK=1
for label_url in "Gateway:$GW" "Identity:$IDENTITY" "Shops:$SHOPS" "Purchases:$PURCHASES"; do
  label="${label_url%%:*}"
  url="${label_url#*:}"
  CODE=$(curl -s --max-time 5 -o /dev/null -w "%{http_code}" "$url/" 2>/dev/null || echo "000")
  if [[ "$CODE" != "000" ]]; then
    pass "$label is reachable (HTTP $CODE)"
  else
    fail "$label is NOT reachable at $url — is docker compose up?"
    ALL_OK=0
  fi
done

if [[ "$ALL_OK" == "0" ]]; then
  echo ""
  echo "  ${c_red}${c_bold}Сервисы недоступны. Запусти: cd src && docker compose up -d${c_reset}"
  echo "  ${c_red}${c_bold}Или используй DIRECT=1 для прямого обращения к сервисам.${c_reset}"
  echo ""
  exit 1
fi

# =============================================================================
section "1. IDENTITY — /api/account/register"
# =============================================================================

info "Registering user1=$USER1"
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
assert_code "Register user1" 200
assert_field "Register user1 → succeeded=true" "d['result']['succeeded']" "True"

info "Registering user2=$USER2"
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"$USER2\",\"password\":\"$PASS\"}"
assert_code "Register user2" 200

# Duplicate
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
assert_code "Register duplicate → 400" 400
assert_field "Duplicate → succeeded=false" "d['succeeded']" "False"

# Missing password
req "$GW_ID/api/account/register" POST "{\"username\":\"x\"}"
assert_code "Register missing password → 400" 400

# Missing username
req "$GW_ID/api/account/register" POST "{\"password\":\"$PASS\"}"
assert_code "Register missing username → 400" 400

# Empty body
req "$GW_ID/api/account/register" POST "{}"
assert_code "Register empty body → 400" 400

# Weak password
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"weakpw_${SUFFIX}\",\"password\":\"password\"}"
assert_code "Register weak password → 400" 400

# =============================================================================
section "2. IDENTITY — /api/account/login"
# =============================================================================

req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
assert_code "Login user1 → 200" 200
assert_field "Login succeeded=true" "d['succeeded']" "True"
assert_nonempty "Login returns token" "d['result']['token']"
assert_nonempty "Login returns id" "d['result']['id']"
TOKEN1=$(jq_val "d['result']['token']")
USER1_ID=$(jq_val "d['result']['id']")
info "TOKEN1=${TOKEN1:0:40}..."
info "USER1_ID=$USER1_ID"

req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER2\",\"password\":\"$PASS\"}"
assert_code "Login user2 → 200" 200
TOKEN2=$(jq_val "d['result']['token']")
info "TOKEN2=${TOKEN2:0:40}..."

# Wrong password
req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"WrongPass!99\"}"
assert_code "Login wrong password → 400" 400

# Non-existent user
req "$GW_ID/api/account/login" POST \
  "{\"username\":\"ghost_${SUFFIX}\",\"password\":\"$PASS\"}"
assert_code "Login non-existent user → 400" 400

# Empty credentials
req "$GW_ID/api/account/login" POST "{}"
assert_code "Login empty body → 400" 400

# Missing password only
req "$GW_ID/api/account/login" POST "{\"username\":\"$USER1\"}"
assert_code "Login missing password → 400" 400

# =============================================================================
section "3. IDENTITY — /api/account/user"
# =============================================================================

req "$GW_ID/api/account/user" GET "" "$TOKEN1"
assert_code "GetUser with valid token → 200" 200
assert_field "GetUser returns correct id" "d['result']['id']" "$USER1_ID"
assert_field "GetUser succeeded=true" "d['succeeded']" "True"

# No token
req "$GW_ID/api/account/user" GET
assert_code "GetUser without token → 401" 401
assert_field "GetUser 401 succeeded=false" "d['succeeded']" "False"

# Garbage token
req "$GW_ID/api/account/user" GET "" "not.a.real.token"
assert_code "GetUser garbage token → 401" 401

# Expired/tampered token
req "$GW_ID/api/account/user" GET "" \
  "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6ImZha2UiLCJuYmYiOjE2MDAwMDAwMDAsImV4cCI6MTYwMDAwMDAwMSwiaWF0IjoxNjAwMDAwMDAwfQ.invalidsignature"
assert_code "GetUser expired/bad token → 401" 401

# Token belongs to user2 — should NOT return user1 id
req "$GW_ID/api/account/user" GET "" "$TOKEN2"
assert_code "GetUser user2 token → 200" 200
assert_not_contains "GetUser user2 token does NOT return user1 id" "\"id\":\"$USER1_ID\""

# =============================================================================
section "4. SHOPS — /api/shops (public reads)"
# =============================================================================

req "$GW_SH/api/shops" GET
assert_code "GetAllShops → 200" 200
assert_field "GetAllShops succeeded=true" "d['succeeded']" "True"
SHOP_COUNT=$(jq_val "len(d['result'])")
info "Total shops in DB: $SHOP_COUNT"
if [[ "$SHOP_COUNT" -ge 1 ]]; then pass "GetAllShops returns at least one shop";
else fail "GetAllShops returned 0 shops — seed data missing"; fi

req "$GW_SH/api/shops/1" GET
assert_code "GetProducts(shop=1) → 200" 200
assert_field "GetProducts succeeded=true" "d['succeeded']" "True"
PROD_COUNT=$(jq_val "len(d['result'])")
info "Products in shop 1: $PROD_COUNT"
if [[ "$PROD_COUNT" -ge 1 ]]; then pass "Shop 1 has at least one product";
else fail "Shop 1 has 0 products — seed data missing"; fi

PROD1_ID=$(jq_val "d['result'][0]['productId']")
PROD1_NAME=$(jq_val "d['result'][0]['name']")
PROD1_CATEGORY=$(jq_val "d['result'][0]['category']")
info "First product: id=$PROD1_ID name='$PROD1_NAME' category='$PROD1_CATEGORY'"

# Non-existent shop
req "$GW_SH/api/shops/999999" GET
assert_code "GetProducts non-existent shop → 404" 404
assert_field "GetProducts 404 succeeded=false" "d['succeeded']" "False"

# shopId=0
req "$GW_SH/api/shops/0" GET
if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "400" ]]; then
  pass "GetProducts(shopId=0) → $HTTP_CODE (not 200)"
else
  fail "GetProducts(shopId=0) → $HTTP_CODE (expected 404 or 400)"
fi

req "$GW_SH/api/shops/2" GET
assert_code "GetProducts(shop=2) → 200" 200

req "$GW_SH/api/shops" GET "" "$TOKEN1"
assert_code "GetAllShops with token also works → 200" 200

# =============================================================================
section "5. SHOPS — /api/shops/{id}/find_by_category"
# =============================================================================

req "$GW_SH/api/shops/1/find_by_category" POST \
  "{\"categoryName\":\"$PROD1_CATEGORY\"}"
assert_code "FindByCategory known category → 200" 200
assert_field "FindByCategory succeeded=true" "d['succeeded']" "True"
CAT_COUNT=$(jq_val "len(d['result'])")
if [[ "$CAT_COUNT" -ge 1 ]]; then pass "FindByCategory returns results for '$PROD1_CATEGORY'";
else fail "FindByCategory returned 0 results for existing category '$PROD1_CATEGORY'"; fi

# Non-existent category → empty list
req "$GW_SH/api/shops/1/find_by_category" POST \
  "{\"categoryName\":\"__no_such_category_xyz__\"}"
assert_code "FindByCategory non-existent category → 200" 200
EMPTY=$(jq_val "d['result']")
if [[ "$EMPTY" == "[]" ]]; then pass "FindByCategory non-existent → empty list";
else fail "FindByCategory non-existent → expected [], got $EMPTY"; fi

# Empty body
req "$GW_SH/api/shops/1/find_by_category" POST "{}"
assert_code "FindByCategory empty body → 200" 200

# Non-existent shop
req "$GW_SH/api/shops/999999/find_by_category" POST \
  "{\"categoryName\":\"одежда\"}"
assert_code "FindByCategory non-existent shop → 404" 404

# =============================================================================
section "6. SHOPS — /api/shops/{id}/order (requires auth)"
# =============================================================================

# No token → 401
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]"
assert_code "Order without token → 401" 401

# Valid order
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN1"
assert_code "Order valid (1 unit) → 200" 200
assert_field "Order succeeded=true" "d['succeeded']" "True"
ORDER_COUNT=$(jq_val "len(d['result'])")
if [[ "$ORDER_COUNT" -ge 1 ]]; then pass "Order returns purchased products";
else fail "Order returned empty product list"; fi

# productId=0 → 400
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":0,\"count\":1}]" "$TOKEN1"
assert_code "Order productId=0 → 400" 400

# count=0 → 400
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":0}]" "$TOKEN1"
assert_code "Order count=0 → 400" 400

# negative count → 400 or 404
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":-1}]" "$TOKEN1"
if [[ "$HTTP_CODE" == "400" || "$HTTP_CODE" == "404" ]]; then
  pass "Order negative count → $HTTP_CODE (not 200)"
else
  fail "Order negative count → $HTTP_CODE (expected 400 or 404)"
fi

# Too many products (>10) → 400
BIG_ORDER="["
for i in $(seq 1 11); do BIG_ORDER+="{\"productId\":$PROD1_ID,\"count\":1},"; done
BIG_ORDER="${BIG_ORDER%,}]"
req "$GW_SH/api/shops/1/order" POST "$BIG_ORDER" "$TOKEN1"
assert_code "Order >10 products → 400" 400

# Empty products list → 400
req "$GW_SH/api/shops/1/order" POST "[]" "$TOKEN1"
assert_code "Order empty list → 400" 400

# Non-existent productId
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":999999,\"count\":1}]" "$TOKEN1"
assert_code "Order non-existent productId → 400" 400

# Non-existent shop
req "$GW_SH/api/shops/999999/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN1"
assert_code "Order non-existent shop → 400" 400

# Order with token2
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN2"
assert_code "Order with token2 → 200" 200

# =============================================================================
section "7. PURCHASES — /api/purchases (read, auth required)"
# =============================================================================

req "$GW_PU/api/purchases" GET
assert_code "GetAllHistory without token → 401" 401
assert_field "GetAllHistory 401 succeeded=false" "d['succeeded']" "False"

req "$GW_PU/api/purchases" GET "" "$TOKEN1"
assert_code "GetAllHistory user1 → 200" 200
assert_field "GetAllHistory succeeded=true" "d['succeeded']" "True"

req "$GW_PU/api/purchases" GET "" "garbage"
assert_code "GetAllHistory garbage token → 401" 401

req "$GW_PU/api/purchases" GET "" "$TOKEN2"
assert_code "GetAllHistory user2 → 200" 200

# =============================================================================
section "8. PURCHASES — POST /api/purchases/add"
# =============================================================================

# No token → 401
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Трусы","productId":1,"cost":100,"count":1,"category":"одежда"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}'
assert_code "AddTransaction without token → 401" 401

# Valid add
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Трусы","productId":1,"cost":100,"count":1,"category":"одежда"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction valid → 200" 200
assert_field "AddTransaction succeeded=true" "d['succeeded']" "True"

# Add second transaction
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Штаны","productId":2,"cost":50,"count":2,"category":"одежда"}],"transactionType":1,"date":"2024-02-15T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction second (user1) → 200" 200

# Add for user2
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Шлепанцы","productId":4,"cost":122,"count":1,"category":"обувь"}],"transactionType":0,"date":"2024-03-01T00:00:00","isShopCreate":false}' \
  "$TOKEN2"
assert_code "AddTransaction valid (user2) → 200" 200

# IsShopCreate=true → 400
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Трусы","productId":1,"cost":100,"count":1,"category":"одежда"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":true}' \
  "$TOKEN1"
assert_code "AddTransaction IsShopCreate=true → 400" 400

# Null products → 400
req "$GW_PU/api/purchases/add" POST \
  '{"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction null products → 400" 400

# Empty body → 400
req "$GW_PU/api/purchases/add" POST "{}" "$TOKEN1"
assert_code "AddTransaction empty body → 400" 400

# =============================================================================
section "9. PURCHASES — GET /api/purchases (list after add)"
# =============================================================================

req "$GW_PU/api/purchases" GET "" "$TOKEN1"
assert_code "GetAllHistory after adds → 200" 200
TXN_COUNT=$(jq_val "len(d['result'])")
info "user1 transactions: $TXN_COUNT"
if [[ "$TXN_COUNT" -ge 2 ]]; then pass "user1 has at least 2 transactions after adds";
else fail "user1 has $TXN_COUNT transactions — expected ≥2"; fi

TXN_ID=$(jq_val "d['result'][0]['id']")
SECOND_TXN_ID=$(jq_val "d['result'][1]['id'] if len(d['result']) > 1 else 0")
info "TXN_ID=$TXN_ID  SECOND_TXN_ID=$SECOND_TXN_ID"

# User isolation
req "$GW_PU/api/purchases" GET "" "$TOKEN2"
assert_code "GetAllHistory user2 → 200" 200
if [[ -n "$TXN_ID" && "$TXN_ID" != "" ]]; then
  if echo "$RESP" | grep -q "\"id\":$TXN_ID,"; then
    fail "User isolation BROKEN — user2 can see user1 transaction id=$TXN_ID"
  else
    pass "User isolation OK — user2 cannot see user1 transactions"
  fi
fi

# =============================================================================
section "10. PURCHASES — GET /api/purchases/{id}"
# =============================================================================

if [[ -z "$TXN_ID" || "$TXN_ID" == "None" || "$TXN_ID" == "0" ]]; then
  skip "GetById — no transaction id available (add failed above)"
else
  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN1"
  assert_code "GetById(id=$TXN_ID) → 200" 200
  assert_field "GetById succeeded=true" "d['succeeded']" "True"
  assert_field "GetById returns correct id" "d['result']['id']" "$TXN_ID"

  req "$GW_PU/api/purchases/$TXN_ID" GET
  assert_code "GetById without token → 401" 401

  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN2"
  if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "403" ]]; then
    pass "GetById cross-user → $HTTP_CODE (not 200)"
  else
    fail "GetById cross-user → $HTTP_CODE (expected 403 or 404, user isolation broken)"
  fi

  req "$GW_PU/api/purchases/999999" GET "" "$TOKEN1"
  assert_code "GetById non-existent → 404" 404
fi

# =============================================================================
section "11. PURCHASES — PUT /api/purchases/update"
# =============================================================================

req "$GW_PU/api/purchases/update" PUT \
  "{\"id\":1,\"transactionType\":1}"
assert_code "UpdateTransaction without token → 401" 401

req "$GW_PU/api/purchases/update" PUT \
  "{\"id\":0,\"transactionType\":1}" "$TOKEN1"
assert_code "UpdateTransaction id=0 → 400" 400

req "$GW_PU/api/purchases/update" PUT \
  "{\"id\":1,\"transactionType\":99}" "$TOKEN1"
assert_code "UpdateTransaction transactionType=99 → 400" 400

req "$GW_PU/api/purchases/update" PUT \
  "{\"id\":1,\"transactionType\":-1}" "$TOKEN1"
assert_code "UpdateTransaction type=-1 → 400" 400

if [[ -n "$TXN_ID" && "$TXN_ID" != "None" && "$TXN_ID" != "0" ]]; then
  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$TXN_ID,\"transactionType\":0}" "$TOKEN1"
  assert_code "UpdateTransaction valid (type=0) → 200" 200
  assert_field "UpdateTransaction succeeded=true" "d['succeeded']" "True"

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$TXN_ID,\"transactionType\":1}" "$TOKEN1"
  assert_code "UpdateTransaction valid (type=1) → 200" 200

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$TXN_ID,\"transactionType\":0}" "$TOKEN2"
  if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "403" ]]; then
    pass "UpdateTransaction cross-user → $HTTP_CODE (not 200)"
  else
    fail "UpdateTransaction cross-user → $HTTP_CODE (expected 403 or 404)"
  fi

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":999999,\"transactionType\":0}" "$TOKEN1"
  assert_code "UpdateTransaction non-existent id → 404" 404
else
  skip "Update valid/cross-user/404 tests — no TXN_ID from add step"
fi

# =============================================================================
section "12. PURCHASES — IsShopCreate transaction immutability"
# =============================================================================
skip "IsShopCreate immutability (needs Shop→Purchases MassTransit flow to create txn)"

# =============================================================================
section "13. CROSS-SERVICE — Shop order creates Purchase transaction"
# =============================================================================

req "$GW_PU/api/purchases" GET "" "$TOKEN1"
BEFORE_COUNT=$(jq_val "len(d['result'])")
info "Transactions before shop order: $BEFORE_COUNT"

req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN1"
assert_code "Cross-service: Shop order → 200" 200

info "Waiting 3s for MassTransit message delivery..."
sleep 3

req "$GW_PU/api/purchases" GET "" "$TOKEN1"
AFTER_COUNT=$(jq_val "len(d['result'])")
info "Transactions after shop order: $AFTER_COUNT"
if [[ "$AFTER_COUNT" -gt "$BEFORE_COUNT" ]]; then
  pass "Cross-service: Shop order created Purchase transaction ($BEFORE_COUNT → $AFTER_COUNT)"
else
  fail "Cross-service: No new Purchase transaction after Shop order (MassTransit consumer not working?)"
fi

SHOP_TXN=$(jq_val "next((x for x in d['result'] if x.get('isShopCreate')==True), None)")
if [[ -n "$SHOP_TXN" && "$SHOP_TXN" != "None" ]]; then
  pass "Cross-service: IsShopCreate=true transaction exists in Purchases"
else
  fail "Cross-service: No IsShopCreate=true transaction found"
fi

# =============================================================================
section "14. GATEWAY — routing and headers"
# =============================================================================

req "$GW/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
assert_code "Gateway → Identity login → 200" 200

req "$GW/api/shops" GET
assert_code "Gateway → Shops GetAll → 200" 200

req "$GW/api/purchases" GET "" "$TOKEN1"
assert_code "Gateway → Purchases GetAll → 200" 200

req "$GW/api/nonexistent_route_xyz" GET
assert_code "Gateway unknown route → 404" 404

# =============================================================================
section "15. RESPONSE ENVELOPE — shape validation"
# =============================================================================

req "$GW_ID/api/account/user" GET "" "$TOKEN1"
assert_contains "Envelope has 'succeeded' field" '"succeeded"'
assert_contains "Envelope has 'code' field" '"code"'
assert_contains "Envelope has 'result' field" '"result"'
assert_contains "Envelope has 'errors' field" '"errors"'

CODE_FIELD=$(jq_val "d['code']")
if [[ "$CODE_FIELD" == "200" ]]; then pass "Envelope code=200 on success";
else fail "Envelope code=$CODE_FIELD (expected 200)"; fi

req "$GW_ID/api/account/login" POST "{\"username\":\"x\",\"password\":\"y\"}"
ERR_COUNT=$(jq_val "len(d['errors'])")
if [[ "$ERR_COUNT" -ge 1 ]]; then pass "Envelope errors[] non-empty on failure";
else fail "Envelope errors[] is empty on failure"; fi

req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
ERR_ON_OK=$(jq_val "len(d['errors'])")
if [[ "$ERR_ON_OK" == "0" ]]; then pass "Envelope errors[] is empty on success";
else fail "Envelope errors[] has $ERR_ON_OK entries on success"; fi

# =============================================================================
section "16. SUMMARY"
# =============================================================================

TOTAL=$((PASS_TOTAL + FAIL_TOTAL + SKIP_TOTAL))
echo ""
echo "  Итого: $TOTAL тестов"
echo "  ${c_green}✔ PASS: $PASS_TOTAL${c_reset}"
echo "  ${c_red}✘ FAIL: $FAIL_TOTAL${c_reset}"
echo "  ${c_yellow}⚠ SKIP: $SKIP_TOTAL${c_reset}"
echo ""
if [[ "$FAIL_TOTAL" -eq 0 ]]; then
  echo "  ${c_green}${c_bold}Все тесты прошли!${c_reset}"
else
  echo "  ${c_red}${c_bold}Есть провалившиеся тесты — смотри лог выше!${c_reset}"
  exit 1
fi
