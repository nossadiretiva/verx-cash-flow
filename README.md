<div align="center">

# Sistema de Controle de Fluxo de Caixa

**Documentação de Arquitetura de Solução — Modelo C4**

![Architecture](https://img.shields.io/badge/Arquitetura-Event--Driven%20Microservices-0A66C2?style=for-the-badge)
![C4 Model](https://img.shields.io/badge/Modelo-C4%20L1%20→%20L3-E87722?style=for-the-badge)
![AWS](https://img.shields.io/badge/Cloud-AWS-FF9900?style=for-the-badge&logo=amazonaws&logoColor=white)
![DDD](https://img.shields.io/badge/Design-Domain--Driven%20Design-6DB33F?style=for-the-badge)
![CQRS](https://img.shields.io/badge/Pattern-CQRS%20%2B%20Outbox-9B59B6?style=for-the-badge)

*Arquitetura de alta disponibilidade e alto desempenho para controle transacional e consolidação analítica de fluxo de caixa diário*

</div>

---

## Sumário

- [Contexto de Negócio](#contexto-de-negócio)
- [Requisitos](#requisitos)
- [Visão Arquitetural](#visão-arquitetural)
- [Modelo C4](#modelo-c4)
  - [Nível 1 — Diagrama de Contexto](#nível-1--diagrama-de-contexto)
  - [Nível 2 — Diagrama de Containers](#nível-2--diagrama-de-containers)
  - [Nível 3 — Diagrama de Componentes](#nível-3--diagrama-de-componentes)
- [Fluxo de Dados — Jornada do Lançamento](#fluxo-de-dados--jornada-do-lançamento)
- [Padrões Arquiteturais](#padrões-arquiteturais)
- [Arquitetura de Segurança](#arquitetura-de-segurança)
- [Observabilidade](#observabilidade)
- [Infraestrutura e Custos](#infraestrutura-e-estimativa-de-custos-aws)
- [Estrutura do Repositório](#estrutura-do-repositório)

---

## Contexto de Negócio

Um comerciante precisa registrar continuamente lançamentos financeiros (débitos e créditos) ao longo do dia e, ao final de cada período, consultar um **relatório consolidado com o saldo diário atualizado**.

O desafio central da solução é garantir que o volume transacional de registros **nunca seja comprometido** por instabilidades no serviço de consolidação — e que as consultas ao relatório consigam atender **picos de até 50 requisições por segundo** com tolerância máxima de 5% de perda. Isso impõe uma separação estrutural obrigatória entre os caminhos de escrita e leitura, resolvida por uma **arquitetura orientada a eventos com CQRS**.

---

## Requisitos

### Requisitos Funcionais

| ID | Requisito |
|----|-----------|
| **RF-001** | Prover serviço para realizar e registrar lançamentos (débitos e créditos) |
| **RF-002** | Prover serviço para calcular e expor o consolidado diário com saldo atualizado |

### Requisitos Não-Funcionais

| ID | Requisito | Meta Quantitativa |
|----|-----------|-------------------|
| **RNF-001** | **Isolamento de Falhas** — o Entry Service não pode ser afetado pela queda do Consolidated Service | Disponibilidade independente por domínio |
| **RNF-002** | **Desempenho sob Pico** — o serviço de consolidado suporta picos de carga | ≥ 50 RPS sustentados |
| **RNF-003** | **Tolerância a Perdas** — perda de requisições de consolidado em pico | ≤ 5% |
| **RNF-004** | **Segurança e Integridade** — toda transação financeira usa canais protegidos | OAuth2 + JWT + TLS obrigatório |

---

## Visão Arquitetural

A solução adota uma **Arquitetura de Microsserviços orientada a eventos (Event-Driven Architecture)** com dois serviços especializados: um para o caminho de **escrita (Command)** e outro para o caminho de **leitura/consulta (Query)** — realizando o padrão **CQRS** com separação física de stores.

### Mapa de Decisões Técnicas

| Componente | Tecnologia Adotada | Justificativa Arquitetural |
|---|---|---|
| **Entry Service** | C# / .NET 8 | Alto throughput transacional; ecossistema robusto para regras de negócio críticas e integração com EF Core + Outbox |
| **Consolidated Service** | Node.js 20 / NestJS | I/O assíncrono intenso sem bloqueio de thread; NestJS provê estrutura modular alinhada a DDD |
| **Banco Transacional** | Amazon RDS for PostgreSQL | Garantias ACID rigorosas; suporte nativo a transações que combinam `lancamentos` e `outbox_events` em um único commit |
| **Message Broker** | Amazon SQS | Desacoplamento temporal total — mensagens persistem no broker mesmo com Consolidated Service offline, satisfazendo RNF-001 |
| **Cache de Relatórios** | Amazon ElastiCache for Redis | Latência sub-milissegundo para leituras; saldo pré-calculado responde a 50+ RPS sem pressão no banco, eliminando a perda de RNF-003 |
| **API Gateway** | AWS API Gateway | Ponto único de entrada para autenticação, rate limiting e TLS termination, sem lógica de negócio nos microsserviços |

### Domínios Funcionais (DDD)

```
╔═══════════════════════════════════════════════════════════╗
║  Core Domain — Lançamentos                                ║
║  ▸ Registrar créditos e débitos com integridade ACID      ║
║  ▸ Garantir isolamento de falhas (RNF-001)                ║
╠═══════════════════════════════════════════════════════════╣
║  Supporting Domain — Consolidação                         ║
║  ▸ Agregar lançamentos e calcular o balanço diário        ║
║  ▸ Suportar 50 RPS com perda ≤ 5% (RNF-002/003)           ║
╚═══════════════════════════════════════════════════════════╝
```

---

### Nível 1 — Diagrama de Contexto

```mermaid
flowchart TB
    U1(["👤 Comerciante<br/>Registra lançamentos diários<br/>e consulta o saldo consolidado"])

    S1["🏦 Sistema de Controle de Fluxo de Caixa<br/>Registra lançamentos em tempo real<br/>e disponibiliza o saldo diário consolidado"]

    E1["🏛️ Sistemas Legados<br/>Integração externa opcional"]
    E2["📊 Consumidores de Relatórios<br/>Dashboards · BI · Auditoria"]

    U1 -->|"HTTPS/REST"| S1
    S1 -->|"HTTPS/REST"| E2
    E1 -->|"HTTPS/REST"| S1

    classDef person fill:#08427b,color:#fff,stroke:#052e56
    classDef system fill:#1168bd,color:#fff,stroke:#0b4884
    classDef external fill:#6b6b6b,color:#fff,stroke:#4a4a4a

    class U1 person
    class S1 system
    class E1,E2 external
```

**Atores e Sistemas Externos**

| Ator / Sistema | Tipo | Papel na Solução |
|---|---|---|
| **Comerciante** | Usuário Primário | Registra débitos/créditos ao longo do dia; consulta o saldo consolidado |
| **Sistemas Legados** | Sistema Externo | Fornece dados históricos para sincronização inicial (integração opcional) |
| **Consumidores de Relatórios** | Sistema Externo | Consome o consolidado para dashboards, BI, exportações e auditoria |

---

### Nível 2 — Diagrama de Containers

```mermaid
flowchart TB
    U1(["👤 Comerciante"])
    E1["🏛️ Sistemas Legados"]

    subgraph SYS["🏦  Sistema de Controle de Fluxo de Caixa"]
        GW["🔀 AWS API Gateway<br/>OAuth2 · JWT · Rate Limiting · TLS"]
        ES["⚙️ Entry Service<br/>C# / .NET 8 · ECS Fargate"]
        DB[("🗄️ Amazon RDS<br/>PostgreSQL<br/>lancamentos · outbox_events")]
        SQS["📨 Amazon SQS<br/>Message Broker<br/>Desacoplamento assíncrono"]
        CS["⚙️ Consolidated Service<br/>Node.js · NestJS · ECS Fargate"]
        RD[("⚡ Amazon ElastiCache<br/>Redis · Saldos pré-calculados")]
    end

    U1 -->|"HTTPS / TLS 1.3"| GW
    GW -->|"POST /lancamentos"| ES
    GW -->|"GET /consolidado/:data"| CS
    ES -->|"Transação ACID"| DB
    ES -->|"Publish LancamentoCriado"| SQS
    SQS -->|"Consume — async"| CS
    CS <-->|"Read / Write"| RD
    E1 -->|"Sync dados"| GW

    classDef person fill:#08427b,color:#fff,stroke:#052e56
    classDef container fill:#1168bd,color:#fff,stroke:#0b4884
    classDef database fill:#1168bd,color:#fff,stroke:#0b4884
    classDef broker fill:#e67e22,color:#fff,stroke:#ca6f1e
    classDef external fill:#6b6b6b,color:#fff,stroke:#4a4a4a

    class U1 person
    class GW,ES,CS container
    class DB,RD database
    class SQS broker
    class E1 external
```

**Papéis dos Containers**

| Container | Runtime | Responsabilidade | Por que esta escolha |
|---|---|---|---|
| **API Gateway** | AWS API Gateway | AuthN/AuthZ, rate limiting, TLS, roteamento | Desacopla preocupações de segurança dos serviços de negócio |
| **Entry Service** | C# / .NET 8 — Amazon ECS Fargate | Registrar lançamentos; garantir atomicidade via Outbox | Alto throughput; Polly para resiliência; EF Core para Outbox |
| **PostgreSQL** | Amazon RDS for PostgreSQL | Persistência ACID de lançamentos e eventos Outbox | Único banco suportando 2 tabelas em uma transação atômica |
| **Message Broker** | Amazon SQS | Desacoplamento temporal entre escrita e consolidação | Se o Consolidated cair, mensagens persistem — RNF-001 satisfeito |
| **Consolidated Service** | Node.js / NestJS — Amazon ECS Fargate | Agregar saldos; expor relatório via cache | Event loop não-bloqueante ideal para consumo assíncrono |
| **Redis** | Amazon ElastiCache for Redis | Cache de saldos pré-calculados — latência ≤ 1ms | Elimina a perda em pico de 50 RPS — RNF-002/003 satisfeitos |

---

### Nível 3 — Diagrama de Componentes

```mermaid
flowchart TB
    U1(["👤 Comerciante"])
    GW_EXT["🔀 AWS API Gateway<br/>OAuth2 / JWT"]
    SQS_EXT["📨 Amazon SQS"]
    LEG_EXT["🏛️ Sistemas Legados"]

    subgraph ENTRY["⚙️  Entry Service — C# / .NET 8"]
        CTRL["🌐 API Controller<br/>ASP.NET Minimal API"]
        BIZ["🧠 Business Logic Layer<br/>Domain Services · MediatR"]
        OUTBOX["📤 Transactional Outbox Worker<br/>Hosted Service · Exactly-once delivery"]
        LADAPTER["🔌 Legacy Adapter<br/>HTTP Client · Polly · Circuit Breaker"]
        DBENTRY[("🗄️ Amazon RDS<br/>lancamentos<br/>outbox_events")]
    end

    subgraph CONSOL["⚙️  Consolidated Service — Node.js / NestJS"]
        CONSUMER["📥 Event Consumer<br/>SQS Consumer Module<br/>Idempotência por event_id"]
        CALC["🧮 Balance Aggregator<br/>NestJS Domain Service"]
        DLQ["⚠️ DLQ Handler<br/>Dead Letter Queue"]
        RCTRL["🌐 Report API Controller<br/>NestJS · GET /consolidado/:data"]
        CACHE[("⚡ Amazon ElastiCache<br/>Redis · saldo:{YYYY-MM-DD}")]
    end

    U1 --> GW_EXT
    GW_EXT -->|"POST /lancamentos"| CTRL
    CTRL --> BIZ
    BIZ -->|"BEGIN TX / COMMIT"| DBENTRY
    OUTBOX -->|"SELECT pending"| DBENTRY
    OUTBOX -->|"PUBLISH"| SQS_EXT
    SQS_EXT -->|"CONSUME"| CONSUMER
    CONSUMER --> CALC
    CONSUMER -->|"N retries falhos"| DLQ
    CALC -->|"SET saldo:{data}"| CACHE
    GW_EXT -->|"GET /consolidado/:data"| RCTRL
    RCTRL -->|"GET saldo:{data}"| CACHE
    LEG_EXT --> LADAPTER
    LADAPTER --> BIZ

    classDef person fill:#08427b,color:#fff,stroke:#052e56
    classDef component fill:#1168bd,color:#fff,stroke:#0b4884
    classDef database fill:#1168bd,color:#fff,stroke:#0b4884
    classDef external fill:#6b6b6b,color:#fff,stroke:#4a4a4a
    classDef warning fill:#c0392b,color:#fff,stroke:#922b21

    class U1 person
    class CTRL,BIZ,OUTBOX,LADAPTER,CONSUMER,CALC,RCTRL component
    class DBENTRY,CACHE database
    class GW_EXT,SQS_EXT,LEG_EXT external
    class DLQ warning
```

---

## Fluxo de Dados — Jornada do Lançamento

```mermaid
sequenceDiagram
    autonumber
    actor Comerciante
    participant GW  as API Gateway
    participant ES  as Entry Service
    participant DB  as PostgreSQL
    participant OW  as Outbox Worker
    participant MQ  as Message Broker
    participant CS  as Consolidated Service
    participant RD  as Redis

    rect rgb(220, 240, 255)
        Note over Comerciante,DB: Caminho de Escrita — Entry Service (síncrono, ACID)
        Comerciante->>GW: POST /lancamentos  { tipo, valor, data }
        GW->>GW: Valida token JWT / OAuth2 scope:write
        GW->>ES: POST /lancamentos  (rede privada VPC)
        ES->>DB: BEGIN TRANSACTION
        ES->>DB: INSERT INTO lancamentos (tipo, valor, data, ...)
        ES->>DB: INSERT INTO outbox_events (event=LancamentoCriado, status=PENDING)
        ES->>DB: COMMIT
        ES-->>GW: HTTP 201 Created { id, timestamp }
        GW-->>Comerciante: HTTP 201 Created ✓
    end

    rect rgb(220, 255, 220)
        Note over OW,MQ: Transactional Outbox (assíncrono — desacoplado da request)
        OW->>DB: SELECT * FROM outbox_events WHERE status = 'PENDING'
        OW->>MQ: PUBLISH LancamentoCriado { lancamento_id, valor, data }
        OW->>DB: UPDATE outbox_events SET status = 'PUBLISHED'
    end

    rect rgb(255, 245, 220)
        Note over MQ,RD: Pipeline de Consolidação (event-driven — assíncrono)
        MQ-->>CS: CONSUME LancamentoCriado
        CS->>CS: Verifica idempotência — event_id já processado?
        CS->>CS: Agrega saldo do dia (soma créditos - débitos)
        CS->>RD: SET saldo:2024-01-15  { saldo, total_creditos, total_debitos }
    end

    rect rgb(245, 220, 255)
        Note over Comerciante,RD: Consulta de Relatório (síncrono — servido inteiramente do cache)
        Comerciante->>GW: GET /consolidado/2024-01-15
        GW->>GW: Valida token JWT / OAuth2 scope:read
        GW->>CS: GET /consolidado/2024-01-15  (VPC privada)
        CS->>RD: GET saldo:2024-01-15
        RD-->>CS: { saldo, creditos, debitos }  ← latência ~0.1ms
        CS-->>GW: HTTP 200 { data, saldo_final, total_creditos, total_debitos }
        GW-->>Comerciante: HTTP 200 ✓
    end
```

**Garantias do Fluxo**

| Etapa | Garantia | Mecanismo |
|---|---|---|
| Registro do lançamento | Atomicidade total | Transação única no PostgreSQL (INSERT lancamento + outbox_event) |
| Publicação do evento | Exactly-once delivery | Transactional Outbox Worker com idempotência no consumidor |
| Atualização do saldo | Idempotência | Consolidated Service verifica `event_id` antes de processar |
| Resposta ao relatório | Latência ≤ 1ms | Redis serve 100% das consultas — zero hit no banco analítico |
| Tolerância à falha do Consolidated | Nenhum impacto no Entry Service | Broker persiste mensagens até o Consolidated voltar (RNF-001) |

---

## Padrões Arquiteturais

### Transactional Outbox

Resolve o problema de *dual-write*: garantir que a persistência do lançamento e a publicação do evento no broker ocorram **atomicamente** — eliminando o risco de publicar um evento para um lançamento que falhou (phantom event) ou de perder um evento de um lançamento persistido (silent loss).

```
┌─────────────────────────────────────────────────────────┐
│  Transação PostgreSQL                                   │
│                                                         │
│  INSERT INTO lancamentos  (valor, tipo, data)           │
│  INSERT INTO outbox_events (payload, status=PENDING)    │
│  COMMIT  ←──── atomicidade ACID garantida               │
└─────────────────────────────────────────────────────────┘
                          │
         Background Worker (Hosted Service)
                          │
         SELECT outbox_events WHERE status = PENDING
                          │
         PUBLISH → Message Broker
                          │
         UPDATE outbox_events SET status = PUBLISHED
```

### CQRS — Command Query Responsibility Segregation

| Responsabilidade | Serviço | Store | Consistência |
|---|---|---|---|
| **Command** (escrita) | Entry Service | PostgreSQL | Forte — ACID |
| **Query** (leitura) | Consolidated Service | Redis | Eventual — atualizado por evento |

A separação física dos stores elimina contenção entre leitura e escrita e permite escalar cada dimensão de forma independente.

### Circuit Breaker & Retry

Aplicado no **Legacy Adapter** (integração com sistemas legados) via **Polly** (.NET):

- **Retry Policy**: 3 tentativas com backoff exponencial (1s → 2s → 4s)
- **Circuit Breaker**: abre após 5 falhas consecutivas em 30 segundos; half-open após 60 segundos
- **Dead Letter Queue**: eventos não consumidos após N tentativas são isolados para reprocessamento manual ou auditoria, sem bloquear o pipeline principal

### Idempotência no Consumidor

O Consolidated Service mantém um registro de `event_id` já processados. Reenvios automáticos do broker (cenário de retry ou at-least-once delivery) não duplicam o cálculo do saldo — a operação é ignorada silenciosamente.

---

## Arquitetura de Segurança

```
Internet
    │
    ▼  TLS 1.3 obrigatório
┌─────────────────────────────────────────┐
│  API Gateway (Edge Layer)               │
│  ├── TLS 1.3 termination                │
│  ├── OAuth2 Authorization (JWT RS256)   │
│  ├── Rate Limiting por cliente          │
│  └── WAF — proteção contra OWASP Top 10 │
└──────────────────┬──────────────────────┘
                   │  VPC Privada — sem acesso público direto
       ┌───────────▼────────────┐
       │  Microsserviços        │
       │  Entry + Consolidated  │  ← comunicação interna por rede privada
       └───────────┬────────────┘
                   │  TLS nas conexões
       ┌───────────▼────────────┐
       │  Data Layer            │
       │  PostgreSQL + Redis    │  ← criptografia em repouso (AES-256, AWS KMS)
       └────────────────────────┘
```

| Camada | Mecanismo | Padrão / Protocolo |
|---|---|---|
| **Transporte** | TLS 1.3 em toda comunicação | HTTPS, Amazon ElastiCache TLS, Amazon SQS (HTTPS nativo) |
| **Autenticação** | OAuth2 + JWT assinado RS256 | RFC 6749 / RFC 7519 |
| **Autorização** | Scopes por operação (`scope:write`, `scope:read`) | RBAC via claims no token |
| **Dados em repouso** | Criptografia AES-256 | AWS KMS — RDS + ElastiCache |
| **Rede** | VPC isolada com subnets privadas | AWS VPC / Security Groups |
| **Auditoria** | Log estruturado JSON de cada transação | Amazon CloudWatch Logs + AWS X-Ray traces |

---

## Observabilidade

A solução adota os **três pilares de observabilidade** via **OpenTelemetry**, com rastreabilidade ponta-a-ponta de cada lançamento — desde a requisição HTTP até a gravação do saldo no Redis.

```
                 OpenTelemetry Collector
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
   ┌────────────┐    ┌─────────────┐   ┌──────────────┐
   │  Métricas  │    │    Logs     │   │    Traces    │
   │ CloudWatch │    │ CloudWatch  │   │   AWS X-Ray  │
   │  Metrics + │    │    Logs     │   │              │
   │ Dashboards │    │ (JSON str.) │   │              │
   └────────────┘    └─────────────┘   └──────────────┘
```

| Pilar | Ferramenta | Sinais Monitorados |
|---|---|---|
| **Métricas** | Amazon CloudWatch Metrics + Dashboards | Taxa de lançamentos/s, latência P50/P95/P99, RPS do consolidado, hit rate do ElastiCache, fila do SQS |
| **Logs** | Amazon CloudWatch Logs (JSON estruturado) | Cada lançamento registrado, falhas de consumo, eventos na DLQ, erros de validação e autenticação |
| **Traces** | AWS X-Ray | Rastreamento distribuído: HTTP → Entry → Amazon SQS → Consolidated → ElastiCache |

**Alertas Críticos Recomendados**

| Alerta | Threshold | Impacto |
|---|---|---|
| Latência P95 do Entry Service | > 200ms | Degradação na experiência de registro |
| Taxa de erros no Consolidated | > 1% | Saldo consolidado pode ficar desatualizado |
| Fila do Amazon SQS | > 1.000 mensagens visíveis | Pipeline de consolidação atrasado |
| Cache miss rate do Redis | > 10% | Latência de leitura aumenta — risco de RNF-002/003 |
| Eventos na DLQ | > 0 | Indica falha não tratada no consumo — requer atenção imediata |

---

## Infraestrutura e Estimativa de Custos AWS

### Topologia AWS Target

```
                        ┌───────────────────────────────────────────┐
                        │              AWS Cloud                    │
                        │                                           │
  Comerciante ──HTTPS──►│  AWS API Gateway  (Edge / Auth / TLS)     │
                        │          │                                │
                        │    ┌─────▼────────────────────────┐       │
                        │    │     Amazon ECS Fargate       │       │
                        │    │  ┌──────────┐ ┌────────────┐ │       │
                        │    │  │  Entry   │ │Consolidated│ │       │
                        │    │  │ Service  │ │  Service   │ │       │
                        │    │  └────┬─────┘ └──────┬─────┘ │       │
                        │    └───────┼──────────────┼───────┘       │
                        │    ┌───────▼────┐   ┌─────▼──────┐        │
                        │    │ Amazon RDS │   │ ElastiCache│        │
                        │    │ PostgreSQL │   │   Redis    │        │
                        │    └────────────┘   └────────────┘        │
                        │                                           │
                        │    ┌──────────────────────────────────┐   │
                        │    │           Amazon SQS             │   │
                        │    └──────────────────────────────────┘   │
                        │                                           │
                        │    CloudWatch · X-Ray · KMS · VPC         │
                        └───────────────────────────────────────────┘
```

### Estimativa de Custos Mensais

| Serviço AWS | Dimensionamento | Custo/mês (USD) |
|---|---|---|
| **Amazon ECS Fargate** | 2 tasks × 2 serviços (0.5 vCPU / 1 GB RAM) — Alta Disponibilidade | ~$ 30,00 |
| **Amazon RDS PostgreSQL** | db.t3.micro — SSD 20 GB — Multi-AZ opcional | ~$ 20,00 |
| **Amazon SQS** | Mensageria serverless — pay-per-use (por mensagem) | ~$ 5,00 |
| **Amazon ElastiCache (Redis)** | cache.t3.micro — suporte a 50+ RPS | ~$ 15,00 |
| **AWS API Gateway** | REST API — até 1M req/mês no free tier | ~$ 3,50 |
| **CloudWatch + X-Ray + KMS** | Logs, métricas, traces e criptografia | ~$ 10,00 |
| **Total Estimado** | | **~$ 83,50 / mês** |

> Custos estimados para ambiente de produção com carga moderada. Auto-scaling do ECS Fargate e elasticidade do SQS absorvem picos sem custo fixo adicional significativo.

---

## Estrutura do Repositório

Organização **monorepo** recomendada para acomodar os dois microsserviços, infraestrutura e documentação em um único repositório versionado:

```
verx-cash-flow/
├── entry-service/                    # Microsserviço de Controle de Lançamentos
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
├── consolidated-service/             # Microsserviço do Consolidado Diário
│   ├── src/
│   │   ├── controllers/              # NestJS REST Controllers
│   │   ├── domain/                   # Aggregators, Balance Calculator
│   │   ├── application/              # NestJS Services e Event Handlers
│   │   └── infrastructure/           # ioredis, Amazon SQS consumer, DLQ Handler
│   ├── tests/
│   │   ├── unit/
│   │   └── integration/
│   └── Dockerfile
│
├── architecture-docs/                # Diagramas C4, ADRs e especificações
│   ├── adr/                          # Architecture Decision Records
│   ├── c4/                           # Fontes dos diagramas (PlantUML / Mermaid)
│   └── desafio-arquiteto-solucoes.pdf
│
├── infra/                            # Infrastructure as Code
│   ├── terraform/                    # AWS: ECS, RDS, ElastiCache, SQS, API GW
│   └── docker-compose.yml            # Stack local completa
│
└── README.md
```

**Execução local com um único comando:**

```bash
docker compose up
```

Inicializa Entry Service, Consolidated Service, PostgreSQL, Redis e Message Broker com configurações de desenvolvimento pré-definidas — pronto para validação funcional e testes integrados.

---

<div align="center">

*Arquitetura projetada para isolar falhas por domínio, escalar de forma independente e garantir rastreabilidade total de cada centavo lançado.*

</div>
