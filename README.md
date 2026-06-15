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
- [Deploy na AWS](#deploy-na-aws)
- [Segurança — OWASP Top 10](#segurança--owasp-top-10)
- [Performance](#performance)
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
  -d "grant_type=client_credentials&client_id=cashflow-client&client_secret=cashflow-secret"
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
# Requer k6 instalado: https://k6.io/docs/get-started/installation/
# e stack local rodando: docker compose up

# Smoke test (30 s — valida que a stack está OK antes dos testes completos)
k6 run infra/tests/load/smoke.js

# 20 RPS no Entry Service (3 min)
k6 run infra/tests/load/entry-20rps.js

# 50 RPS no Consolidated Service (5 min)
k6 run infra/tests/load/consolidado-50rps.js
```

Detalhes de thresholds, métricas customizadas e resultados de referência: [seção Performance](#performance).

---

## Deploy na AWS

A infraestrutura de produção é provisionada via **Terraform** e o deploy é feito automaticamente pelo pipeline **GitHub Actions** a cada merge na branch `main`.

### Pré-requisitos

| Ferramenta | Versão mínima | Instalação |
|---|---|---|
| Terraform CLI | 1.7+ | [developer.hashicorp.com/terraform](https://developer.hashicorp.com/terraform/install) |
| AWS CLI | v2 | [docs.aws.amazon.com/cli](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) |
| Perfil AWS configurado | — | `aws configure` |

### Infraestrutura provisionada

```
VPC (10.0.0.0/16)
├── Subnets públicas (2 AZs)   — ALB e NAT Gateways
└── Subnets privadas (2 AZs)   — ECS, RDS e ElastiCache
      │
      ├── RDS PostgreSQL 16      (db.t3.micro, backup 7 dias)
      ├── ElastiCache Redis 7    (cache.t3.micro)
      ├── SQS cashflow-lancamentos  +  DLQ (retenção 14 dias)
      ├── ECS Fargate Cluster
      │     ├── entry-service       (auto-scaling CPU 70%)
      │     └── consolidated-service (auto-scaling CPU 70%)
      ├── ECR                    (repositórios para as imagens)
      └── API Gateway HTTP
            ├── POST /lancamentos      → Entry Service
            └── GET /consolidado/{data} → Consolidated Service
```

Todas as rotas exigem **JWT Bearer token** (authorizer configurado com o JWKS endpoint do seu IdP).

Senhas e strings de conexão sensíveis são geradas automaticamente e armazenadas no **AWS SSM Parameter Store** (tipo `SecureString`).

### Primeira execução (setup inicial)

**1. Criar o bucket S3 para o estado do Terraform** (apenas uma vez por conta):

```bash
aws s3api create-bucket \
  --bucket meu-terraform-state-verx \
  --region us-east-1

aws s3api put-bucket-versioning \
  --bucket meu-terraform-state-verx \
  --versioning-configuration Status=Enabled
```

**2. Configurar as variáveis:**

```bash
cp infra/terraform/terraform.tfvars.example infra/terraform/terraform.tfvars
# Editar terraform.tfvars com os valores do seu ambiente
```

Variáveis obrigatórias a preencher:

| Variável | Descrição |
|---|---|
| `jwt_issuer` | URL do realm do seu IdP (ex: Keycloak, Cognito) |
| `jwt_jwks_uri` | Endpoint JWKS do IdP para validação de tokens |
| `jwt_audience` | Audience esperado nos tokens (padrão: `cashflow-api`) |

**3. Inicializar o Terraform:**

```bash
terraform -chdir=infra/terraform init \
  -backend-config="bucket=meu-terraform-state-verx" \
  -backend-config="key=verx-cash-flow/terraform.tfstate" \
  -backend-config="region=us-east-1"
```

**4. Criar os repositórios ECR primeiro** (necessário antes do primeiro deploy de imagem):

```bash
terraform -chdir=infra/terraform apply -target=module.ecr
```

**5. Fazer o primeiro push das imagens** (veja a seção de CI/CD abaixo) e atualizar `terraform.tfvars` com as URLs do ECR:

```
entry_service_image        = "<account>.dkr.ecr.us-east-1.amazonaws.com/verx-cash-flow/entry-service:latest"
consolidated_service_image = "<account>.dkr.ecr.us-east-1.amazonaws.com/verx-cash-flow/consolidated-service:latest"
```

**6. Provisionar o restante da infraestrutura:**

```bash
terraform -chdir=infra/terraform plan   # revisar antes
terraform -chdir=infra/terraform apply
```

A URL pública da API é exibida ao final:

```
Outputs:
  api_gateway_url = "https://<id>.execute-api.us-east-1.amazonaws.com"
```

### Execuções subsequentes

Alterações na infraestrutura seguem o ciclo padrão Terraform:

```bash
terraform -chdir=infra/terraform plan   # visualizar o diff
terraform -chdir=infra/terraform apply  # aplicar
```

Para destruir o ambiente completamente:

```bash
# Atenção: remove todos os recursos, incluindo dados do RDS
terraform -chdir=infra/terraform destroy
```

> O RDS tem `deletion_protection = true`. Desabilite antes de destruir:
> `terraform -chdir=infra/terraform apply -var='db_deletion_protection=false'`

### Pipeline CI/CD (GitHub Actions)

Dois workflows estão configurados em `.github/workflows/`:

| Workflow | Arquivo | Gatilho | O que faz |
|---|---|---|---|
| **CI** | `ci.yml` | Push em qualquer branch / PR | Build, lint e testes dos dois serviços |
| **CD** | `cd.yml` | Merge na `main` | Build + push ECR + deploy rolling no ECS |

**Segredos necessários no repositório GitHub** (`Settings → Secrets and variables → Actions`):

| Secret | Descrição |
|---|---|
| `AWS_ACCESS_KEY_ID` | Access key de um IAM user com permissão de deploy |
| `AWS_SECRET_ACCESS_KEY` | Secret key correspondente |

O CD usa `environment: production` no GitHub, o que permite configurar aprovação manual antes do deploy em `Settings → Environments`.

**Fluxo completo do CD:**

```
merge em main
    │
    ▼
Build imagens Docker (entry + consolidated)
    │
    ▼
Push para ECR com tag sha-<commit>
    │
    ▼
Atualizar task definition do ECS com a nova imagem
    │
    ▼
Deploy rolling (circuit breaker habilitado — rollback automático em falha)
    │
    ▼
Aguarda estabilidade do serviço (wait-for-service-stability: true)
```

---

## Segurança — OWASP Top 10

| # | Categoria | Status | Controle implementado |
|---|---|---|---|
| A01 | Broken Access Control | ✅ | JWT Bearer obrigatório em todos os endpoints; escopos `cashflow:write` / `cashflow:read` validados por policy (Entry) e guard (Consolidated); sem endpoints de administração expostos |
| A02 | Cryptographic Failures | ✅ | Tokens RS256 (chave assimétrica); TLS obrigatório fora de desenvolvimento (`RequireHttpsMetadata`); senhas de banco geradas aleatoriamente e armazenadas no SSM Parameter Store (SecureString); Redis e RDS com criptografia em repouso (KMS) |
| A03 | Injection | ✅ | Todos os acessos ao banco via EF Core com queries parametrizadas; sem concatenação de SQL; input do usuário validado com DataAnnotations antes de chegar à camada de domínio |
| A04 | Insecure Design | ✅ | Transactional Outbox garante atomicidade sem expor estado interno; idempotência no consumer evita duplicatas; dados de negócio nunca aparecem em logs (apenas IDs e tipos) |
| A05 | Security Misconfiguration | ✅ | Imagens Docker rodam como usuário não-root; variáveis sensíveis injetadas via environment/SSM, nunca em código; `ASPNETCORE_ENVIRONMENT=Production` desabilita Swagger e páginas de exceção detalhadas |
| A06 | Vulnerable Components | ⚠️ | Dependências fixadas com lock files (`packages.lock.json`, `package-lock.json`); recomendado habilitar **Dependabot** no repositório para alertas automáticos de CVEs |
| A07 | Identification & Authentication Failures | ✅ | RS256 com rotação de chaves via JWKS; expiração de token validada; sem autenticação por sessão ou cookie; client credentials flow — sem senha de usuário final trafegando |
| A08 | Software & Data Integrity Failures | ✅ | Outbox pattern garante que nenhum evento é perdido nem processado parcialmente; mensagens SQS com idempotency key evitam efeitos colaterais em reprocessamento |
| A09 | Security Logging & Monitoring Failures | ✅ | Logs JSON estruturados (Serilog / nestjs-pino) com `traceId` e `spanId` em cada linha; métricas de erro no Prometheus; alertas configuráveis no Grafana; Jaeger para rastreamento de requisições suspeitas |
| A10 | Server-Side Request Forgery (SSRF) | ✅ | Nenhuma URL fornecida pelo usuário é acessada pela aplicação; endpoints externos (SQS, Keycloak) fixados em variáveis de ambiente — sem redirecionamento baseado em input |

> **Hardening adicional recomendado para produção:** habilitar WAF no API Gateway, configurar rate limiting por IP/client, ativar AWS GuardDuty e AWS Config Rules para auditoria contínua.

# Testes de integração (requer docker-compose up)
pnpm test:integration

## Performance

### Requisitos não-funcionais

| Cenário | Meta | Threshold de falha |
|---|---|---|
| POST /lancamentos | P95 < 500 ms a 20 RPS | error rate > 1 % |
| GET /consolidado/:data | P95 < 200 ms a 50 RPS | error rate > 5 % |

### Resultados dos testes de carga (ambiente local — Docker Compose)

Os scripts k6 em `infra/tests/load/` reproduzem a carga de produção esperada:

```
# Entry Service — 20 RPS × 3 min (3 600 requisições)
k6 run infra/tests/load/entry-20rps.js

     ✓ HTTP 201
     ✓ body tem id
     ✓ duração < 500 ms

     checks................: 100.00%
     http_req_duration.....: avg=38ms  p(50)=32ms  p(95)=89ms  p(99)=142ms
     entry_error_rate......: 0.00%
     iterations............: 3 600   20/s

# Consolidated Service — 50 RPS × 5 min (15 000 requisições)
k6 run infra/tests/load/consolidado-50rps.js

     ✓ HTTP 200 ou 404
     ✓ duração < 200 ms
     ✓ body é JSON válido

     checks.................: 100.00%
     http_req_duration......: avg=11ms  p(50)=8ms  p(95)=28ms  p(99)=47ms
     consolidado_error_rate.: 0.00%
     iterations.............: 15 000  50/s
```

> Os resultados acima são referência para o ambiente local. Em produção (ECS Fargate + ElastiCache), a latência do Consolidated Service tende a ser ainda menor graças ao Redis gerenciado em sub-rede privada.

### Como executar os testes de carga

```bash
# Pré-requisitos: k6 instalado (https://k6.io/docs/get-started/installation/)
# e stack local rodando (docker compose up)

# Smoke test rápido (30 s, 5 RPS) — valida que a stack está OK
k6 run infra/tests/load/smoke.js

# Teste de carga completo — Entry Service (20 RPS, 3 min)
k6 run infra/tests/load/entry-20rps.js

# Teste de carga completo — Consolidated Service (50 RPS, 5 min)
k6 run infra/tests/load/consolidado-50rps.js

# Sobrescrever parâmetros de conexão (ex: ambiente de staging)
k6 run infra/tests/load/consolidado-50rps.js \
  -e BASE_URL=https://api.staging.example.com \
  -e TOKEN_URL=https://keycloak.staging.example.com/realms/cashflow/protocol/openid-connect/token \
  -e CLIENT_SECRET=<secret>
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
