<div align="center">

# Sistema de Controle de Fluxo de Caixa

![Architecture](https://img.shields.io/badge/Arquitetura-Event--Driven%20Microservices-0A66C2?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![NestJS](https://img.shields.io/badge/NestJS-20-E0234E?style=for-the-badge&logo=nestjs&logoColor=white)
![AWS](https://img.shields.io/badge/Cloud-AWS-FF9900?style=for-the-badge&logo=amazonaws&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/DB-PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Cache-Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)

*Dois microsserviços orientados a eventos para registro transacional e consolidação analítica de fluxo de caixa diário*

</div>

---

## Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Pré-requisitos](#pré-requisitos)
- [Execução Local](#execução-local)
- [Variáveis de Ambiente](#variáveis-de-ambiente)
- [Endpoints da API](#endpoints-da-api)
- [Testes](#testes)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [Plano de Desenvolvimento](#plano-de-desenvolvimento)
- [Documentação de Arquitetura](#documentação-de-arquitetura)

---

## Visão Geral

Solução de microsserviços para registro e consolidação de lançamentos financeiros (débitos e créditos) com isolamento de falhas por domínio e suporte a picos de leitura de até **50 RPS** com perda máxima de **5%**.

| Serviço | Runtime | Responsabilidade |
|---|---|---|
| **Entry Service** | C# / .NET 8 | Registrar lançamentos com atomicidade ACID + Transactional Outbox |
| **Consolidated Service** | Node.js 20 / NestJS | Agregar saldos via eventos e servir relatório diário a partir do Redis |

---

## Arquitetura

```
Comerciante ──HTTPS──► API Gateway (OAuth2 · JWT · TLS)
                              │
              ┌───────────────┼───────────────────┐
              ▼                                   ▼
       Entry Service                    Consolidated Service
       C# / .NET 8                      Node.js / NestJS
       POST /lancamentos                GET /consolidado/:data
              │                                   │
              ▼                                   ▼
       PostgreSQL                              Redis
       lancamentos                         saldo:{YYYY-MM-DD}
       outbox_events
              │
              ▼  (Transactional Outbox Worker)
           Amazon SQS ──► Consolidated Service (async)
```

**Padrões aplicados:** CQRS · Transactional Outbox · Circuit Breaker · Idempotent Consumer

Documentação arquitetural completa: [DAS-FluxoCaixa](docs/documento_arquitetura_solucao_DAS.md)

---

## Pré-requisitos

| Ferramenta | Versão mínima | Uso |
|---|---|---|
| Docker Desktop | 24+ | Stack local completa |
| .NET SDK | 8.0 | Desenvolvimento do Entry Service |
| Node.js | 20 LTS | Desenvolvimento do Consolidated Service |
| pnpm | 9+ | Gerenciador de pacotes (Consolidated) |
| AWS CLI | v2 | Deploy na AWS (opcional para dev local) |
| Terraform | 1.7+ | Provisionamento de infra AWS (opcional para dev local) |

---

## Execução Local

### 1. Subir a stack completa

```bash
docker compose up
```

Inicializa todos os serviços com configurações de desenvolvimento pré-definidas:

| Serviço | Porta local | Observação |
|---|---|---|
| Entry Service | `8080` | API REST |
| Consolidated Service | `8081` | API REST |
| PostgreSQL | `5432` | Banco transacional |
| Redis | `6379` | Cache de saldos |
| LocalStack (SQS) | `4566` | Simulação do Amazon SQS |
| Jaeger UI | `16686` | Traces distribuídos |
| Grafana | `3000` | Métricas e dashboards |
| Prometheus | `9090` | Coleta de métricas |

### 2. Verificar saúde dos serviços

```bash
curl http://localhost:8080/health   # Entry Service
curl http://localhost:8081/health   # Consolidated Service
```

### 3. Obter token de acesso (OAuth2 Client Credentials)

```bash
curl -X POST http://localhost:8180/realms/cashflow/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=cashflow-client&client_secret=secret"
```

### 4. Registrar um lançamento

```bash
curl -X POST http://localhost:8080/lancamentos \
  -H "Authorization: Bearer {TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": "CREDITO",
    "valor": 150.00,
    "descricao": "Venda à vista",
    "data": "2024-01-15"
  }'
```

Resposta esperada:
```json
{
  "id": "uuid-v4",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 5. Consultar o consolidado

```bash
curl http://localhost:8081/consolidado/2024-01-15 \
  -H "Authorization: Bearer {TOKEN}"
```

Resposta esperada:
```json
{
  "data": "2024-01-15",
  "saldo_final": 150.00,
  "total_creditos": 150.00,
  "total_debitos": 0.00,
  "updated_at": "2024-01-15T10:30:05Z"
}
```

> O consolidado é atualizado de forma assíncrona. Aguarde alguns segundos após o lançamento para a propagação via SQS.

---

## Variáveis de Ambiente

### Entry Service

| Variável | Padrão (dev) | Descrição |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | Ambiente de execução |
| `ConnectionStrings__Postgres` | `Host=postgres;...` | String de conexão PostgreSQL |
| `SQS__QueueUrl` | `http://localstack:4566/...` | URL da fila SQS |
| `SQS__EndpointUrl` | `http://localstack:4566` | Endpoint customizado (LocalStack/AWS) |
| `Outbox__PollingIntervalSeconds` | `5` | Intervalo do worker de Outbox |
| `Auth__JwksUri` | `http://keycloak:8080/...` | URI das chaves públicas JWT |

### Consolidated Service

| Variável | Padrão (dev) | Descrição |
|---|---|---|
| `NODE_ENV` | `development` | Ambiente de execução |
| `REDIS_URL` | `redis://redis:6379` | Connection string Redis |
| `SQS_QUEUE_URL` | `http://localstack:4566/...` | URL da fila SQS |
| `SQS_ENDPOINT_URL` | `http://localstack:4566` | Endpoint customizado (LocalStack/AWS) |
| `SQS_MAX_MESSAGES` | `10` | Máximo de mensagens por polling |
| `SQS_VISIBILITY_TIMEOUT` | `30` | Timeout de visibilidade (segundos) |
| `JWT_JWKS_URI` | `http://keycloak:8080/...` | URI das chaves públicas JWT |
| `REDIS_SALDO_TTL_DAYS` | `7` | TTL das chaves de saldo no Redis |

---

## Endpoints da API

### Entry Service — `POST /lancamentos`

```
POST /lancamentos
Authorization: Bearer {token}  (scope:write obrigatório)

Body:
{
  "tipo":      "CREDITO" | "DEBITO"   // obrigatório
  "valor":     number (> 0)           // obrigatório
  "data":      "YYYY-MM-DD"           // obrigatório
  "descricao": string                 // opcional
}

201 Created  →  { "id": "uuid", "timestamp": "ISO-8601" }
400 Bad Request  →  { "errors": [...] }
401 Unauthorized
403 Forbidden (scope inválido)
```

### Consolidated Service — `GET /consolidado/:data`

```
GET /consolidado/2024-01-15
Authorization: Bearer {token}  (scope:read obrigatório)

200 OK  →  {
  "data":             "2024-01-15",
  "saldo_final":      1500.00,
  "total_creditos":   2000.00,
  "total_debitos":    500.00,
  "updated_at":       "ISO-8601"
}

404 Not Found  →  { "message": "Consolidado não disponível para a data informada" }
401 Unauthorized
```

### Health Check (ambos os serviços)

```
GET /health

200 OK  →  {
  "status": "ok",
  "dependencies": {
    "database": "ok",   // Entry Service
    "redis":    "ok",   // Consolidated Service
    "sqs":      "ok"
  }
}
```

---

## Testes

### Entry Service

```bash
cd entry-service

# Testes unitários
dotnet test tests/Unit/

# Testes de integração (requer docker-compose up)
dotnet test tests/Integration/

# Todos com cobertura
dotnet test --collect:"XPlat Code Coverage"
```

### Consolidated Service

```bash
cd consolidated-service

# Testes unitários
pnpm test

# Testes de integração (requer docker-compose up)
pnpm test:integration

# Cobertura
pnpm test:cov
```

### Testes End-to-End

```bash
# A partir da raiz do projeto (requer stack completa rodando)
pnpm e2e
```

### Testes de Carga

```bash
# 50 RPS no serviço de consolidado (requer k6 instalado)
k6 run infra/tests/load/consolidado-50rps.js

# 20 RPS no Entry Service
k6 run infra/tests/load/entry-20rps.js
```

---

## Estrutura do Repositório

```
verx-cash-flow/
├── entry-service/                    # Microsserviço de Controle de Lançamentos (C# / .NET 8)
│   ├── src/
│   │   ├── Controllers/              # ASP.NET Minimal API — endpoints REST
│   │   ├── Domain/                   # Entidades, Value Objects e Regras de Negócio
│   │   ├── Application/              # Use Cases e MediatR Command Handlers
│   │   └── Infrastructure/           # EF Core, PostgreSQL, Outbox Worker, Polly
│   ├── tests/
│   │   ├── Unit/
│   │   └── Integration/
│   └── Dockerfile
│
├── consolidated-service/             # Microsserviço do Consolidado Diário (Node.js / NestJS)
│   ├── src/
│   │   ├── controllers/              # NestJS REST Controllers
│   │   ├── domain/                   # Aggregators, Balance Calculator
│   │   ├── application/              # NestJS Services e Event Handlers
│   │   └── infrastructure/           # ioredis, SQS Consumer, DLQ Handler
│   ├── tests/
│   │   ├── unit/
│   │   └── integration/
│   └── Dockerfile
│
├── infra/
│   ├── terraform/                    # IaC: VPC, ECS, RDS, ElastiCache, SQS, API GW
│   │   └── modules/
│   ├── tests/load/                   # Scripts k6 para testes de carga
│   └── docker-compose.yml            # Stack local completa
│
├── docs/
│   ├── documento_arquitetura_solucao_DAS.md   # Arquitetura completa — Modelo C4
│   ├── plano_desenvolvimento.md               # Plano de desenvolvimento por sprints
│   └── desafio-arquiteto-solucoes.pdf         # Especificação original do desafio
│
└── README.md
```

---

## Plano de Desenvolvimento

O desenvolvimento está organizado em **8 sprints** cobrindo 10 semanas:

| Sprint | Conteúdo | Duração |
|---|---|---|
| 0 | Fundação: monorepo, docker-compose, CI skeleton | 1 semana |
| 1 | Entry Service: Domain + API (`POST /lancamentos`) | 2 semanas |
| 2 | Entry Service: Transactional Outbox Worker + SQS | 1 semana |
| 3 | Consolidated Service: Consumer + Redis + API | 2 semanas |
| 4 | Integração E2E + testes de caos (RNF-001) | 1 semana |
| 5 | Segurança: OAuth2 + JWT em todos os endpoints | 1 semana |
| 6 | Observabilidade: OpenTelemetry + Jaeger + Grafana | 1 semana |
| 7 | IaC Terraform + Pipeline CI/CD (GitHub Actions) | 1 semana |
| 8 | Testes de carga (50 RPS) + hardening final | 1 semana |

Detalhamento completo, Definition of Done por sprint e matriz de riscos: [docs/plano_desenvolvimento.md](docs/plano_desenvolvimento.md)

---

## Documentação de Arquitetura

A documentação arquitetural completa (C4 Model — Níveis 1 a 3, padrões, segurança, observabilidade e estimativa de custos AWS) está em:

**[docs/documento_arquitetura_solucao_DAS.md](docs/documento_arquitetura_solucao_DAS.md)**

---

<div align="center">

*Arquitetura projetada para isolar falhas por domínio, escalar de forma independente e garantir rastreabilidade total de cada centavo lançado.*

</div>
