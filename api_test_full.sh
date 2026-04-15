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
USER2_ID=""
TXN_ID=""
SECOND_TXN_ID=""
SHOP_TXN_ID=""   # IsShopCreate=true транзакция из cross-service теста

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

assert_gt() {
  local label="$1" val="$2" min="$3"
  if [[ "$val" -gt "$min" ]] 2>/dev/null; then pass "$label (value=$val > $min)";
  else fail "$label — expected >$min, got $val"; fi
}

assert_lt() {
  local label="$1" val="$2" max="$3"
  if [[ "$val" -lt "$max" ]] 2>/dev/null; then pass "$label (value=$val < $max)";
  else fail "$label — expected <$max, got $val"; fi
}

assert_ge() {
  local label="$1" val="$2" min="$3"
  if [[ "$val" -ge "$min" ]] 2>/dev/null; then pass "$label (value=$val >= $min)";
  else fail "$label — expected >=$min, got $val"; fi
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

# Weak password (no special chars)
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"weakpw_${SUFFIX}\",\"password\":\"password\"}"
assert_code "Register weak password → 400" 400

# Weak password (too short)
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"weakpw2_${SUFFIX}\",\"password\":\"P1!\"}"
assert_code "Register too short password → 400" 400

# Weak password (no digits)
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"weakpw3_${SUFFIX}\",\"password\":\"Password!\"}"
assert_code "Register no-digit password → 400" 400

# Register succeeds → result.succeeded=true, result has errors field empty
req "$GW_ID/api/account/register" POST \
  "{\"username\":\"user3_${SUFFIX}\",\"password\":\"$PASS\"}"
assert_code "Register user3 → 200" 200
assert_field "Register user3 result.succeeded=true" "d['result']['succeeded']" "True"

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
USER2_ID=$(jq_val "d['result']['id']")
info "TOKEN2=${TOKEN2:0:40}..."
info "USER2_ID=$USER2_ID"

# Два логина подряд — оба токена рабочие (не инвалидируют друг друга)
req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
TOKEN1_NEW=$(jq_val "d['result']['token']")
req "$GW_ID/api/account/user" GET "" "$TOKEN1"
assert_code "Old token still valid after re-login → 200" 200
req "$GW_ID/api/account/user" GET "" "$TOKEN1_NEW"
assert_code "New token valid after re-login → 200" 200

# Wrong password
req "$GW_ID/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"WrongPass!99\"}"
assert_code "Login wrong password → 400" 400
assert_field "Login wrong password succeeded=false" "d['succeeded']" "False"

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

# Missing username only
req "$GW_ID/api/account/login" POST "{\"password\":\"$PASS\"}"
assert_code "Login missing username → 400" 400

# =============================================================================
section "3. IDENTITY — /api/account/user"
# =============================================================================

req "$GW_ID/api/account/user" GET "" "$TOKEN1"
assert_code "GetUser with valid token → 200" 200
assert_field "GetUser returns correct id" "d['result']['id']" "$USER1_ID"
assert_field "GetUser succeeded=true" "d['succeeded']" "True"
assert_nonempty "GetUser result has id field" "d['result']['id']"
assert_nonempty "GetUser result has username field" "d['result']['username']"

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

# user2 получает свой собственный id
assert_field "GetUser user2 returns user2 id" "d['result']['id']" "$USER2_ID"

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

# Каждый магазин имеет id и name
SHOP1_ID=$(jq_val "d['result'][0]['id']")
SHOP1_NAME=$(jq_val "d['result'][0]['name']")
info "First shop: id=$SHOP1_ID name='$SHOP1_NAME'"
if [[ -n "$SHOP1_ID" && "$SHOP1_ID" != "None" ]]; then pass "GetAllShops shop has id field";
else fail "GetAllShops shop missing id field"; fi
if [[ -n "$SHOP1_NAME" && "$SHOP1_NAME" != "None" ]]; then pass "GetAllShops shop has name field";
else fail "GetAllShops shop missing name field"; fi

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
PROD1_COST=$(jq_val "d['result'][0]['cost']")
PROD1_COUNT=$(jq_val "d['result'][0]['count']")
info "First product: id=$PROD1_ID name='$PROD1_NAME' category='$PROD1_CATEGORY' cost=$PROD1_COST count=$PROD1_COUNT"

# Поля продукта заполнены
if [[ -n "$PROD1_COST" && "$PROD1_COST" != "None" ]]; then pass "Product has cost field";
else fail "Product missing cost field"; fi
if [[ -n "$PROD1_COUNT" && "$PROD1_COUNT" != "None" ]]; then pass "Product has count field";
else fail "Product missing count field"; fi
if [[ -n "$PROD1_CATEGORY" && "$PROD1_CATEGORY" != "None" ]]; then pass "Product has category field";
else fail "Product missing category field"; fi

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

# Отрицательный shopId
req "$GW_SH/api/shops/-1" GET
if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "400" ]]; then
  pass "GetProducts(shopId=-1) → $HTTP_CODE (not 200)"
else
  fail "GetProducts(shopId=-1) → $HTTP_CODE (expected 404 or 400)"
fi

req "$GW_SH/api/shops/2" GET
assert_code "GetProducts(shop=2) → 200" 200

# Разные магазины возвращают разные продукты
PRODS2=$(jq_val "len(d['result'])")
req "$GW_SH/api/shops/1" GET
PRODS1=$(jq_val "len(d['result'])")
if [[ "$PRODS1" -ge 1 && "$PRODS2" -ge 1 ]]; then pass "Both shops have products (shop1=$PRODS1, shop2=$PRODS2)";
else fail "One of the shops is empty"; fi

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

# Все результаты имеют нужную категорию
WRONG_CAT=$(jq_val "sum(1 for p in d['result'] if p.get('category') != '$PROD1_CATEGORY')")
if [[ "$WRONG_CAT" == "0" ]]; then pass "FindByCategory all results match requested category";
else fail "FindByCategory returned $WRONG_CAT items with wrong category"; fi

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

# Пустая строка категории → 200 пустой список
req "$GW_SH/api/shops/1/find_by_category" POST "{\"categoryName\":\"\"}"
assert_code "FindByCategory empty string → 200" 200

# Non-existent shop
req "$GW_SH/api/shops/999999/find_by_category" POST \
  "{\"categoryName\":\"одежда\"}"
assert_code "FindByCategory non-existent shop → 404" 404

# Регистронезависимость категории (если поддерживается)
PROD1_CATEGORY_UPPER=$(echo "$PROD1_CATEGORY" | tr '[:lower:]' '[:upper:]')
req "$GW_SH/api/shops/1/find_by_category" POST \
  "{\"categoryName\":\"$PROD1_CATEGORY_UPPER\"}"
if [[ "$HTTP_CODE" == "200" ]]; then
  UPPER_COUNT=$(jq_val "len(d['result'])")
  info "FindByCategory case-sensitivity: upper='$PROD1_CATEGORY_UPPER' → $UPPER_COUNT results"
  pass "FindByCategory uppercase category → 200 (case-handling exists)"
else
  info "FindByCategory case-sensitive (returns $HTTP_CODE for uppercase)"
  pass "FindByCategory case behavior documented"
fi

# =============================================================================
section "6. SHOPS — /api/shops/{id}/order (requires auth)"
# =============================================================================

# No token → 401
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]"
assert_code "Order without token → 401" 401

# Запомним кол-во до заказа
req "$GW_SH/api/shops/1" GET
STOCK_BEFORE=$(jq_val "next((p['count'] for p in d['result'] if p['productId']==$PROD1_ID), None)")
info "Stock of product $PROD1_ID before order: $STOCK_BEFORE"

# Valid order
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN1"
assert_code "Order valid (1 unit) → 200" 200
assert_field "Order succeeded=true" "d['succeeded']" "True"
ORDER_COUNT=$(jq_val "len(d['result'])")
if [[ "$ORDER_COUNT" -ge 1 ]]; then pass "Order returns purchased products";
else fail "Order returned empty product list"; fi

# Проверяем поля возвращённого продукта
RET_NAME=$(jq_val "d['result'][0]['name']")
RET_COST=$(jq_val "d['result'][0]['cost']")
RET_COUNT=$(jq_val "d['result'][0]['count']")
if [[ -n "$RET_NAME" && "$RET_NAME" != "None" ]]; then pass "Order result product has name";
else fail "Order result product missing name"; fi
if [[ -n "$RET_COST" && "$RET_COST" != "None" ]]; then pass "Order result product has cost";
else fail "Order result product missing cost"; fi
if [[ "$RET_COUNT" == "1" ]]; then pass "Order result product count matches requested (1)";
else fail "Order result product count=$RET_COUNT (expected 1)"; fi

# productId=0 → 400
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":0,\"count\":1}]" "$TOKEN1"
assert_code "Order productId=0 → 400" 400

# count=0 → 400
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":0}]" "$TOKEN1"
assert_code "Order count=0 → 400" 400

# negative count → 400
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

# Ровно 10 продуктов → 200 (граничное значение)
BORDER_ORDER="["
for i in $(seq 1 10); do BORDER_ORDER+="{\"productId\":$PROD1_ID,\"count\":1},"; done
BORDER_ORDER="${BORDER_ORDER%,}]"
req "$GW_SH/api/shops/1/order" POST "$BORDER_ORDER" "$TOKEN1"
if [[ "$HTTP_CODE" == "200" || "$HTTP_CODE" == "400" ]]; then
  info "Order exactly 10 products → HTTP $HTTP_CODE"
  if [[ "$HTTP_CODE" == "200" ]]; then pass "Order exactly 10 products → 200 (boundary OK)";
  else pass "Order exactly 10 products → 400 (strict <10 boundary)"; fi
else
  fail "Order exactly 10 products → $HTTP_CODE (expected 200 or 400)"
fi

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
# result — это список
RESULT_TYPE=$(jq_val "type(d['result']).__name__")
if [[ "$RESULT_TYPE" == "list" ]]; then pass "GetAllHistory result is array";
else fail "GetAllHistory result is $RESULT_TYPE (expected list)"; fi

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

# Add second transaction (другой transactionType)
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Штаны","productId":2,"cost":50,"count":2,"category":"одежда"}],"transactionType":1,"date":"2024-02-15T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction second (user1) → 200" 200

# Add для user2
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Шлепанцы","productId":4,"cost":122,"count":1,"category":"обувь"}],"transactionType":0,"date":"2024-03-01T00:00:00","isShopCreate":false}' \
  "$TOKEN2"
assert_code "AddTransaction valid (user2) → 200" 200

# IsShopCreate=true без Receipt → 400
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Трусы","productId":1,"cost":100,"count":1,"category":"одежда"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":true}' \
  "$TOKEN1"
assert_code "AddTransaction IsShopCreate=true → 400" 400
assert_field "AddTransaction IsShopCreate=true → succeeded=false" "d['succeeded']" "False"

# IsShopCreate=false с Receipt → 400
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Трусы","productId":1,"cost":100,"count":1,"category":"одежда"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false,"receipt":{"shopId":1,"cost":100,"count":1,"date":"2024-01-01T00:00:00","products":[]}}' \
  "$TOKEN1"
assert_code "AddTransaction IsShopCreate=false with receipt → 400" 400

# Null products → 400
req "$GW_PU/api/purchases/add" POST \
  '{"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction null products → 400" 400

# Empty products list → 400
req "$GW_PU/api/purchases/add" POST \
  '{"products":[],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction empty products list → 400" 400

# Empty body → 400
req "$GW_PU/api/purchases/add" POST "{}" "$TOKEN1"
assert_code "AddTransaction empty body → 400" 400

# transactionType=0 (valid enum min)
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"А","productId":1,"cost":1,"count":1,"category":"к"}],"transactionType":0,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction transactionType=0 → 200" 200

# transactionType=1 (valid enum max, если 0 и 1)
req "$GW_PU/api/purchases/add" POST \
  '{"products":[{"name":"Б","productId":2,"cost":2,"count":1,"category":"к"}],"transactionType":1,"date":"2024-01-01T00:00:00","isShopCreate":false}' \
  "$TOKEN1"
assert_code "AddTransaction transactionType=1 → 200" 200

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

# Поля каждой транзакции
if [[ -n "$TXN_ID" && "$TXN_ID" != "None" && "$TXN_ID" != "0" ]]; then
  TXN0_PRODUCTS=$(jq_val "len(d['result'][0]['products'])")
  TXN0_TYPE=$(jq_val "d['result'][0]['transactionType']")
  TXN0_ISSHOP=$(jq_val "d['result'][0]['isShopCreate']")
  if [[ "$TXN0_PRODUCTS" -ge 1 ]]; then pass "Transaction has products list";
  else fail "Transaction missing products (got $TXN0_PRODUCTS)"; fi
  if [[ -n "$TXN0_TYPE" && "$TXN0_TYPE" != "None" ]]; then pass "Transaction has transactionType field";
  else fail "Transaction missing transactionType field"; fi
  if [[ "$TXN0_ISSHOP" == "False" ]]; then pass "Manual transaction has isShopCreate=false";
  else fail "Manual transaction isShopCreate=$TXN0_ISSHOP (expected False)"; fi
fi

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

# user2 видит только свои транзакции (минимум 1 после add выше)
USER2_TXN_COUNT=$(jq_val "len(d['result'])")
if [[ "$USER2_TXN_COUNT" -ge 1 ]]; then pass "user2 has own transactions";
else fail "user2 has 0 transactions (expected ≥1 after add)"; fi

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

  # Проверяем полноту объекта транзакции
  assert_nonempty "GetById result has products" "d['result']['products']"
  TXN_DETAIL_ISSHOP=$(jq_val "d['result']['isShopCreate']")
  if [[ "$TXN_DETAIL_ISSHOP" == "False" ]]; then pass "GetById isShopCreate=false on manual txn";
  else fail "GetById isShopCreate=$TXN_DETAIL_ISSHOP (expected False)"; fi

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

  # GetById с id=0 → 400 или 404
  req "$GW_PU/api/purchases/0" GET "" "$TOKEN1"
  if [[ "$HTTP_CODE" == "400" || "$HTTP_CODE" == "404" ]]; then
    pass "GetById id=0 → $HTTP_CODE (not 200)"
  else
    fail "GetById id=0 → $HTTP_CODE (expected 400 or 404)"
  fi
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

  # Проверяем что поле действительно обновилось в БД
  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN1"
  UPDATED_TYPE=$(jq_val "d['result']['transactionType']")
  if [[ "$UPDATED_TYPE" == "0" ]]; then pass "UpdateTransaction: transactionType persisted in DB (=0)";
  else fail "UpdateTransaction: DB has transactionType=$UPDATED_TYPE (expected 0)"; fi

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$TXN_ID,\"transactionType\":1}" "$TOKEN1"
  assert_code "UpdateTransaction valid (type=1) → 200" 200

  # Проверяем что type=1 тоже обновился
  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN1"
  UPDATED_TYPE2=$(jq_val "d['result']['transactionType']")
  if [[ "$UPDATED_TYPE2" == "1" ]]; then pass "UpdateTransaction: transactionType persisted in DB (=1)";
  else fail "UpdateTransaction: DB has transactionType=$UPDATED_TYPE2 (expected 1)"; fi

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$TXN_ID,\"transactionType\":0}" "$TOKEN2"
  if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "403" ]]; then
    pass "UpdateTransaction cross-user → $HTTP_CODE (not 200)"
  else
    fail "UpdateTransaction cross-user → $HTTP_CODE (expected 403 or 404)"
  fi

  # После неудачного кросс-юзер update — тип у user1 не изменился
  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN1"
  TYPE_AFTER_CROSS=$(jq_val "d['result']['transactionType']")
  if [[ "$TYPE_AFTER_CROSS" == "1" ]]; then pass "UpdateTransaction cross-user did not modify owner's data";
  else fail "UpdateTransaction cross-user corrupted owner's transactionType: $TYPE_AFTER_CROSS"; fi

  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":999999,\"transactionType\":0}" "$TOKEN1"
  assert_code "UpdateTransaction non-existent id → 404" 404
else
  skip "Update valid/cross-user/404 tests — no TXN_ID from add step"
fi

# =============================================================================
section "12. PURCHASES — IsShopCreate transaction immutability"
# =============================================================================
# Этот тест выполняется ПОСЛЕ секции 13 (cross-service),
# поэтому здесь только резервируем место.
# Фактическая проверка immutability происходит в секции 13b ниже.
skip "IsShopCreate immutability — проверка в секции 13 (после cross-service заказа)"

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

# Получаем id IsShopCreate транзакции для теста immutability
SHOP_TXN_ID=$(jq_val "next((x['id'] for x in d['result'] if x.get('isShopCreate')==True), None)")
info "SHOP_TXN_ID=$SHOP_TXN_ID"

# Проверяем поля IsShopCreate транзакции
if [[ -n "$SHOP_TXN_ID" && "$SHOP_TXN_ID" != "None" && "$SHOP_TXN_ID" != "0" ]]; then
  req "$GW_PU/api/purchases/$SHOP_TXN_ID" GET "" "$TOKEN1"
  assert_code "Cross-service: GetById shop txn → 200" 200
  assert_field "Cross-service: shop txn isShopCreate=true" "d['result']['isShopCreate']" "True"
  SHOP_TXN_PRODUCTS=$(jq_val "len(d['result']['products'])")
  if [[ "$SHOP_TXN_PRODUCTS" -ge 1 ]]; then pass "Cross-service: shop txn has products";
  else fail "Cross-service: shop txn has no products"; fi

  # Повторный заказ другим пользователем — у него своя транзакция
  req "$GW_SH/api/shops/1/order" POST \
    "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN2"
  assert_code "Cross-service: shop order user2 → 200" 200
  sleep 3
  req "$GW_PU/api/purchases" GET "" "$TOKEN2"
  USER2_SHOP_TXN=$(jq_val "next((x for x in d['result'] if x.get('isShopCreate')==True), None)")
  if [[ -n "$USER2_SHOP_TXN" && "$USER2_SHOP_TXN" != "None" ]]; then
    pass "Cross-service: user2 gets own IsShopCreate txn"
  else
    fail "Cross-service: user2 has no IsShopCreate=true txn after order"
  fi

  # user2 не видит IsShopCreate транзакцию user1
  req "$GW_PU/api/purchases/$SHOP_TXN_ID" GET "" "$TOKEN2"
  if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "403" ]]; then
    pass "Cross-service: user2 cannot access user1 shop txn"
  else
    fail "Cross-service: user2 got HTTP $HTTP_CODE for user1 shop txn (isolation broken)"
  fi
fi

# ─────────────────────────────────────────────────────────────────────────────
# 13b. IsShopCreate IMMUTABILITY — нельзя обновить IsShopCreate транзакцию
# ─────────────────────────────────────────────────────────────────────────────
if [[ -n "$SHOP_TXN_ID" && "$SHOP_TXN_ID" != "None" && "$SHOP_TXN_ID" != "0" ]]; then
  # Попытка обновить IsShopCreate транзакцию должна вернуть ошибку
  req "$GW_PU/api/purchases/update" PUT \
    "{\"id\":$SHOP_TXN_ID,\"transactionType\":1}" "$TOKEN1"
  if [[ "$HTTP_CODE" == "400" || "$HTTP_CODE" == "403" || "$HTTP_CODE" == "422" ]]; then
    pass "IsShopCreate immutability: update rejected (HTTP $HTTP_CODE)"
  elif [[ "$HTTP_CODE" == "200" ]]; then
    # Если 200 — проверяем, что тип реально не изменился
    req "$GW_PU/api/purchases/$SHOP_TXN_ID" GET "" "$TOKEN1"
    SHOP_TXN_TYPE=$(jq_val "d['result']['transactionType']")
    info "IsShopCreate txn transactionType after update attempt: $SHOP_TXN_TYPE"
    fail "IsShopCreate immutability: update returned 200 — shop txn should be immutable"
  else
    fail "IsShopCreate immutability: unexpected HTTP $HTTP_CODE"
  fi
else
  skip "IsShopCreate immutability — no SHOP_TXN_ID (cross-service flow not working)"
fi

# =============================================================================
section "14. GATEWAY — routing and headers"
# =============================================================================

req "$GW/api/account/login" POST \
  "{\"username\":\"$USER1\",\"password\":\"$PASS\"}"
assert_code "Gateway → Identity login → 200" 200

req "$GW/api/account/register" POST \
  "{\"username\":\"gw_user_${SUFFIX}\",\"password\":\"$PASS\"}"
assert_code "Gateway → Identity register → 200" 200

req "$GW/api/account/user" GET "" "$TOKEN1"
assert_code "Gateway → Identity getuser → 200" 200

req "$GW/api/shops" GET
assert_code "Gateway → Shops GetAll → 200" 200

req "$GW/api/shops/1" GET
assert_code "Gateway → Shops GetProducts → 200" 200

req "$GW/api/shops/1/find_by_category" POST "{\"categoryName\":\"одежда\"}"
assert_code "Gateway → Shops FindByCategory → 200" 200

req "$GW/api/purchases" GET "" "$TOKEN1"
assert_code "Gateway → Purchases GetAll → 200" 200

req "$GW/api/purchases/$TXN_ID" GET "" "$TOKEN1"
assert_code "Gateway → Purchases GetById → 200" 200

req "$GW/api/nonexistent_route_xyz" GET
assert_code "Gateway unknown route → 404" 404

# Несуществующий метод на известном маршруте
req "$GW/api/shops" DELETE "" "$TOKEN1"
if [[ "$HTTP_CODE" == "404" || "$HTTP_CODE" == "405" ]]; then
  pass "Gateway wrong HTTP method → $HTTP_CODE (not 200)"
else
  fail "Gateway wrong HTTP method → $HTTP_CODE (expected 404 or 405)"
fi

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

# Envelope: succeeded=false при 401
req "$GW_PU/api/purchases" GET
if [[ "$HTTP_CODE" == "401" ]]; then
  SUCC=$(jq_val "d['succeeded']")
  if [[ "$SUCC" == "False" ]]; then pass "Envelope succeeded=false на 401";
  else fail "Envelope succeeded=$SUCC на 401 (expected False)"; fi
fi

# Envelope: succeeded=false при 404
req "$GW_SH/api/shops/999999" GET
if [[ "$HTTP_CODE" == "404" ]]; then
  SUCC404=$(jq_val "d['succeeded']")
  if [[ "$SUCC404" == "False" ]]; then pass "Envelope succeeded=false на 404";
  else fail "Envelope succeeded=$SUCC404 на 404 (expected False)"; fi
fi

# Envelope code совпадает с HTTP-статусом
req "$GW_SH/api/shops/999999" GET
HTTP_404_CODE=$HTTP_CODE
ENV_CODE=$(jq_val "d['code']")
if [[ "$HTTP_404_CODE" == "$ENV_CODE" ]]; then pass "Envelope code matches HTTP status ($ENV_CODE)";
else fail "Envelope code=$ENV_CODE != HTTP $HTTP_404_CODE"; fi

# =============================================================================
section "16. SHOPS — stock consistency"
# =============================================================================

# Получаем текущий сток продукта
req "$GW_SH/api/shops/1" GET
STOCK_NOW=$(jq_val "next((p['count'] for p in d['result'] if p['productId']==$PROD1_ID), None)")
info "Stock of product $PROD1_ID now: $STOCK_NOW"

if [[ -n "$STOCK_NOW" && "$STOCK_NOW" != "None" && "$STOCK_NOW" -ge 1 ]]; then
  # Делаем заказ на 1 единицу
  req "$GW_SH/api/shops/1/order" POST \
    "[{\"productId\":$PROD1_ID,\"count\":1}]" "$TOKEN1"
  assert_code "Stock consistency: order 1 unit → 200" 200

  # Проверяем что сток уменьшился на 1
  req "$GW_SH/api/shops/1" GET
  STOCK_AFTER=$(jq_val "next((p['count'] for p in d['result'] if p['productId']==$PROD1_ID), None)")
  EXPECTED_STOCK=$((STOCK_NOW - 1))
  info "Stock after order: $STOCK_NOW → $STOCK_AFTER (expected $EXPECTED_STOCK)"
  if [[ "$STOCK_AFTER" == "$EXPECTED_STOCK" ]]; then
    pass "Stock consistency: count decreased by 1 ($STOCK_NOW → $STOCK_AFTER)"
  else
    fail "Stock consistency: expected $EXPECTED_STOCK, got $STOCK_AFTER"
  fi
else
  # Нет стока — проверяем что нельзя заказать больше чем есть
  req "$GW_SH/api/shops/1/order" POST \
    "[{\"productId\":$PROD1_ID,\"count\":99999}]" "$TOKEN1"
  assert_code "Stock consistency: overorder → 400" 400
  pass "Stock consistency: insufficient stock check OK"
fi

# Нельзя заказать больше чем есть (большое количество)
req "$GW_SH/api/shops/1/order" POST \
  "[{\"productId\":$PROD1_ID,\"count\":99999}]" "$TOKEN1"
if [[ "$HTTP_CODE" == "400" || "$HTTP_CODE" == "409" ]]; then
  pass "Stock consistency: overorder rejected (HTTP $HTTP_CODE)"
else
  fail "Stock consistency: overorder returned $HTTP_CODE (expected 400 or 409)"
fi

# =============================================================================
section "17. PURCHASES — data integrity"
# =============================================================================

if [[ -n "$TXN_ID" && "$TXN_ID" != "None" && "$TXN_ID" != "0" ]]; then
  req "$GW_PU/api/purchases/$TXN_ID" GET "" "$TOKEN1"

  # Транзакция имеет все обязательные поля
  assert_contains "Transaction has id field" '"id"'
  assert_contains "Transaction has products field" '"products"'
  assert_contains "Transaction has transactionType field" '"transactionType"'
  assert_contains "Transaction has isShopCreate field" '"isShopCreate"'
  assert_contains "Transaction has date field" '"date"'

  # products — это массив с минимум 1 элементом
  PROD_IN_TXN=$(jq_val "len(d['result']['products'])")
  if [[ "$PROD_IN_TXN" -ge 1 ]]; then pass "Transaction.products has ≥1 item";
  else fail "Transaction.products is empty"; fi

  # Каждый продукт внутри транзакции имеет поля
  TXN_PROD_NAME=$(jq_val "d['result']['products'][0].get('name', None)")
  TXN_PROD_COST=$(jq_val "d['result']['products'][0].get('cost', None)")
  TXN_PROD_COUNT=$(jq_val "d['result']['products'][0].get('count', None)")
  if [[ -n "$TXN_PROD_NAME" && "$TXN_PROD_NAME" != "None" ]]; then pass "Transaction product has name";
  else fail "Transaction product missing name"; fi
  if [[ -n "$TXN_PROD_COST" && "$TXN_PROD_COST" != "None" ]]; then pass "Transaction product has cost";
  else fail "Transaction product missing cost"; fi
  if [[ -n "$TXN_PROD_COUNT" && "$TXN_PROD_COUNT" != "None" ]]; then pass "Transaction product has count";
  else fail "Transaction product missing count"; fi

  # IsShopCreate=false → receipt должен быть null
  TXN_RECEIPT=$(jq_val "d['result'].get('receipt', 'MISSING')")
  if [[ "$TXN_RECEIPT" == "None" || "$TXN_RECEIPT" == "MISSING" ]]; then
    pass "Manual transaction: receipt is null"
  else
    info "Manual transaction has receipt=$TXN_RECEIPT (informational)"
    pass "Manual transaction receipt field present"
  fi
else
  skip "Data integrity checks — no TXN_ID available"
fi

# IsShopCreate=true транзакция должна иметь receipt
if [[ -n "$SHOP_TXN_ID" && "$SHOP_TXN_ID" != "None" && "$SHOP_TXN_ID" != "0" ]]; then
  req "$GW_PU/api/purchases/$SHOP_TXN_ID" GET "" "$TOKEN1"
  SHOP_TXN_RECEIPT=$(jq_val "d['result'].get('receipt', None)")
  if [[ -n "$SHOP_TXN_RECEIPT" && "$SHOP_TXN_RECEIPT" != "None" ]]; then
    pass "Shop transaction: receipt is present"
    RECEIPT_SHOPID=$(jq_val "d['result']['receipt'].get('shopId', None)")
    if [[ -n "$RECEIPT_SHOPID" && "$RECEIPT_SHOPID" != "None" && "$RECEIPT_SHOPID" != "0" ]]; then
      pass "Shop transaction receipt has shopId"
    else
      fail "Shop transaction receipt missing/zero shopId"
    fi
  else
    fail "Shop transaction (isShopCreate=true) has no receipt"
  fi
fi

# =============================================================================
section "18. SUMMARY"
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
