#!/bin/bash
# Valida que a stack completa está saudável após docker compose up
set -e

BASE_ENTRY="http://localhost:8080"
BASE_CONSOL="http://localhost:8081"
TOKEN_URL="http://localhost:8180/realms/cashflow/protocol/openid-connect/token"
MAX_WAIT=60

wait_for() {
  local url=$1
  local label=$2
  local elapsed=0
  echo "Aguardando $label..."
  until curl -sf "$url" > /dev/null 2>&1; do
    sleep 2; elapsed=$((elapsed + 2))
    if [ $elapsed -ge $MAX_WAIT ]; then echo "TIMEOUT: $label não respondeu em ${MAX_WAIT}s"; exit 1; fi
  done
  echo "OK: $label"
}

wait_for "$BASE_ENTRY/health"   "Entry Service"
wait_for "$BASE_CONSOL/health"  "Consolidated Service"

# Obter token
TOKEN=$(curl -sf -X POST "$TOKEN_URL" \
<<<<<<< HEAD
  -d "grant_type=client_credentials&client_id=cashflow-client&client_secret=cashflow-secret&scope=cashflow:write cashflow:read" \
=======
  -d "grant_type=client_credentials&client_id=cashflow-client&client_secret=cashflow-secret" \
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
  | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
  echo "AVISO: Keycloak não disponível — pulando testes autenticados"
  TOKEN="dummy"
fi

TODAY=$(date +%Y-%m-%d)

# POST /lancamentos
echo ""
echo "==> Testando POST /lancamentos..."
RESPONSE=$(curl -sf -X POST "$BASE_ENTRY/lancamentos" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"tipo\":\"CREDITO\",\"valor\":250.00,\"data\":\"$TODAY\",\"descricao\":\"Teste E2E\"}")
echo "Resposta: $RESPONSE"
LANCAMENTO_ID=$(echo "$RESPONSE" | grep -o '"id":"[^"]*' | cut -d'"' -f4)
echo "Lançamento criado: $LANCAMENTO_ID"

# Aguarda propagação via Outbox → SQS → Consolidated
echo ""
echo "==> Aguardando propagação assíncrona (máx 30s)..."
elapsed=0
until curl -sf "$BASE_CONSOL/consolidado/$TODAY" -H "Authorization: Bearer $TOKEN" > /dev/null 2>&1; do
  sleep 2; elapsed=$((elapsed + 2))
  if [ $elapsed -ge 30 ]; then echo "TIMEOUT: Consolidado não disponível após 30s"; exit 1; fi
done

# GET /consolidado/:data
echo ""
echo "==> Testando GET /consolidado/$TODAY..."
SALDO=$(curl -sf "$BASE_CONSOL/consolidado/$TODAY" -H "Authorization: Bearer $TOKEN")
echo "Saldo: $SALDO"

echo ""
echo "==> Stack E2E validada com sucesso!"
