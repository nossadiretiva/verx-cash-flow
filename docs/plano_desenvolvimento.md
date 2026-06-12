# Plano de Desenvolvimento — Sistema de Controle de Fluxo de Caixa

> Planejamento faseado para implementação da solução arquitetural descrita no [DAS-FluxoCaixa](documento_arquitetura_solucao_DAS.md).

---

## Visão Geral

| Atributo | Valor |
|---|---|
| **Duração total estimada** | 10 semanas |
| **Número de sprints** | 8 |
| **Metodologia** | Iterativo por domínio (Core Domain primeiro) |
| **Repositório** | Monorepo — `verx-cash-flow/` |

---

## Premissas e Restrições

- O **Entry Service** (Core Domain) deve estar funcional e testado antes de iniciar o Consolidated Service.
- O ambiente local usa **LocalStack** para simular SQS — nenhuma conta AWS é necessária nas Sprints 0–6.
- Cada Sprint tem uma **Definition of Done** explícita; a Sprint seguinte só inicia quando todos os critérios forem atendidos.
- Testes de integração rodam contra serviços reais via docker-compose, nunca contra mocks de banco ou broker.
- IaC (Terraform) é desenvolvido em paralelo a partir da Sprint 5, sem bloquear o desenvolvimento dos serviços.

---

## Dependências Técnicas

```
Sprint 0 — Fundação
    └── Sprint 1 — Entry Service: Domain + API
            └── Sprint 2 — Entry Service: Outbox + SQS
                    └── Sprint 3 — Consolidated Service: Core
                            └── Sprint 4 — Integração E2E
                                    ├── Sprint 5 — Segurança
                                    ├── Sprint 6 — Observabilidade
                                    └── Sprint 7 — IaC + CI/CD
                                                    └── Sprint 8 — Carga + Hardening
```

---

## Sprint 0 — Fundação do Projeto (1 semana)

**Objetivo:** monorepo configurado, ambiente local funcional, pipeline CI básico rodando.

### Tarefas

| # | Tarefa | Serviço | Entregável |
|---|--------|---------|-----------|
| 0.1 | Inicializar estrutura de diretórios do monorepo | Infra | Pastas conforme layout do DAS |
| 0.2 | Criar `docker-compose.yml` com PostgreSQL 16, Redis 7 e LocalStack | Infra | `docker compose up` sobe a stack completa |
| 0.3 | Configurar `.editorconfig`, `.gitignore` e `commitlint` | Infra | Padrões de código aplicados |
| 0.4 | Criar pipeline CI base no GitHub Actions | CI/CD | Workflow de build e lint por serviço |
| 0.5 | Documentar variáveis de ambiente no `README.md` | Docs | Tabela de env vars com valores padrão para dev |

### Definition of Done
- [ ] `docker compose up` sobe sem erros: PostgreSQL, Redis e LocalStack disponíveis
- [ ] Pipeline CI executa no push para `main` e `develop`
- [ ] Estrutura de diretórios commitada conforme layout do DAS

---

## Sprint 1 — Entry Service: Domain + API (2 semanas)

**Objetivo:** endpoint `POST /lancamentos` funcional com persistência ACID no PostgreSQL.

### Stack
- .NET 8 — ASP.NET Minimal API
- Entity Framework Core 8 com migrations
- MediatR 12 (CQRS interno)
- FluentValidation

### Tarefas

| # | Tarefa | Camada | Entregável |
|---|--------|--------|-----------|
| 1.1 | Scaffold do projeto .NET 8 (`entry-service/`) | Infra | Projeto compilando |
| 1.2 | Modelar entidade `Lancamento` (tipo, valor, data, id, createdAt) | Domain | Entidade + Value Objects (`TipoLancamento`, `Valor`) |
| 1.3 | Criar `CreateLancamentoCommand` + Handler (MediatR) | Application | Handler com validação via FluentValidation |
| 1.4 | Configurar EF Core + migrations: tabelas `lancamentos` e `outbox_events` | Infrastructure | Migration aplicável via `dotnet ef database update` |
| 1.5 | Implementar `POST /lancamentos` (Minimal API) | API | Endpoint retorna `HTTP 201` com `{ id, timestamp }` |
| 1.6 | Testes unitários: Domain + Application | Tests | Cobertura ≥ 80% nas camadas de domínio e aplicação |
| 1.7 | Testes de integração: `POST /lancamentos` → assertar linha em `lancamentos` | Tests | Teste usando PostgreSQL real do docker-compose |

### Esquema de banco (Migration 001)

```sql
CREATE TABLE lancamentos (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tipo        VARCHAR(7) NOT NULL CHECK (tipo IN ('CREDITO', 'DEBITO')),
    valor       NUMERIC(15,2) NOT NULL CHECK (valor > 0),
    descricao   TEXT,
    data        DATE NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE outbox_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type      VARCHAR(100) NOT NULL,
    payload         JSONB NOT NULL,
    status          VARCHAR(10) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING','PUBLISHED','FAILED')),
    lancamento_id   UUID NOT NULL REFERENCES lancamentos(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    published_at    TIMESTAMPTZ
);

CREATE INDEX idx_outbox_events_status ON outbox_events(status) WHERE status = 'PENDING';
```

### Definition of Done
- [ ] `POST /lancamentos` retorna `201` com payload válido
- [ ] Transação atômica: se o INSERT em `outbox_events` falhar, `lancamentos` também faz rollback
- [ ] Cobertura de testes ≥ 80% (Domain + Application)
- [ ] Testes de integração passando contra PostgreSQL real

---

## Sprint 2 — Entry Service: Transactional Outbox Worker (1 semana)

**Objetivo:** eventos `LancamentoCriado` publicados no SQS de forma confiável, sem dual-write.

### Stack
- `BackgroundService` (.NET Hosted Service)
- AWS SDK for .NET (`AWSSDK.SQS`)
- LocalStack para SQS em desenvolvimento

### Tarefas

| # | Tarefa | Entregável |
|---|--------|-----------|
| 2.1 | Implementar `OutboxWorker` como `BackgroundService` | Worker que roda a cada 5s em loop |
| 2.2 | Lógica: `SELECT PENDING → PUBLISH SQS → UPDATE PUBLISHED` | Ciclo atômico com controle de erros |
| 2.3 | Configurar cliente SQS com suporte a LocalStack (env `SQS_ENDPOINT_URL`) | Zero alteração de código entre dev e prod |
| 2.4 | Retry com backoff exponencial (Polly) no PUBLISH | Até 3 retentativas antes de marcar `FAILED` |
| 2.5 | Testes de integração: ciclo completo `POST → DB → Outbox → SQS` | Mensagem visível na fila LocalStack após POST |
| 2.6 | Dockerfile do Entry Service | Imagem `entry-service:local` buildável |

### Payload do evento `LancamentoCriado`

```json
{
  "event_id": "uuid-v4",
  "event_type": "LancamentoCriado",
  "occurred_at": "ISO-8601",
  "data": {
    "lancamento_id": "uuid-v4",
    "tipo": "CREDITO|DEBITO",
    "valor": 150.00,
    "data": "YYYY-MM-DD"
  }
}
```

### Definition of Done
- [ ] Mensagem `LancamentoCriado` aparece na fila SQS após `POST /lancamentos`
- [ ] Worker não publica duplicatas se o processo for reiniciado (status `PUBLISHED` impede reprocessamento)
- [ ] Dockerfile compila e roda localmente
- [ ] Testes de integração do ciclo E2E passando

---

## Sprint 3 — Consolidated Service: Core (2 semanas)

**Objetivo:** serviço Node.js/NestJS consumindo eventos SQS, calculando saldos e servindo relatório via Redis.

### Stack
- Node.js 20 LTS + NestJS 10
- TypeScript 5
- `@aws-sdk/client-sqs` (SQS consumer)
- `ioredis` (Redis client)
- Jest (testes)

### Tarefas

| # | Tarefa | Camada | Entregável |
|---|--------|--------|-----------|
| 3.1 | Scaffold do projeto NestJS (`consolidated-service/`) | Infra | Projeto compilando com TypeScript |
| 3.2 | Módulo `SqsConsumerModule`: polling de mensagens SQS | Infrastructure | Consumidor funcional contra LocalStack |
| 3.3 | Lógica de idempotência: verificar `event_id` no Redis antes de processar | Infrastructure | SET com NX no Redis — duplicatas ignoradas |
| 3.4 | `BalanceAggregatorService`: agregar saldo do dia | Domain | Cálculo: `total_creditos - total_debitos = saldo` |
| 3.5 | Persistir saldo: `SET saldo:{YYYY-MM-DD}` no Redis com TTL de 7 dias | Infrastructure | Hash Redis com `saldo`, `total_creditos`, `total_debitos` |
| 3.6 | `DlqHandlerService`: encaminhar para DLQ após N falhas | Infrastructure | Eventos problemáticos isolados sem bloquear pipeline |
| 3.7 | `GET /consolidado/:data` — Controller NestJS | API | Retorna saldo do Redis ou `404` se não calculado |
| 3.8 | Testes unitários: BalanceAggregatorService | Tests | Casos: crédito, débito, dia sem lançamentos |
| 3.9 | Testes de integração: consume SQS → assertar Redis | Tests | Valor correto no Redis após consumo |
| 3.10 | Dockerfile do Consolidated Service | Infra | Imagem `consolidated-service:local` buildável |

### Estrutura da chave Redis

```
saldo:{YYYY-MM-DD}  →  Hash {
    saldo_final:      "1500.00"
    total_creditos:   "2000.00"
    total_debitos:    "500.00"
    updated_at:       "ISO-8601"
}

processed_event:{event_id}  →  String "1"  (TTL: 48h — idempotência)
```

### Contrato da API de Relatório

```
GET /consolidado/2024-01-15

HTTP 200
{
  "data": "2024-01-15",
  "saldo_final": 1500.00,
  "total_creditos": 2000.00,
  "total_debitos": 500.00,
  "updated_at": "2024-01-15T18:30:00Z"
}

HTTP 404  →  { "message": "Consolidado não disponível para a data informada" }
```

### Definition of Done
- [ ] `GET /consolidado/:data` retorna `200` após processamento do evento correspondente
- [ ] Idempotência: reenvio da mesma mensagem SQS não altera o saldo
- [ ] DLQ recebe mensagem após 3 falhas de processamento
- [ ] Cobertura de testes ≥ 80%
- [ ] Dockerfile compila e roda localmente

---

## Sprint 4 — Integração End-to-End (1 semana)

**Objetivo:** stack completa rodando localmente com testes E2E automatizados.

### Tarefas

| # | Tarefa | Entregável |
|---|--------|-----------|
| 4.1 | Finalizar `docker-compose.yml` com todos os serviços | `docker compose up` sobe stack completa em < 30s |
| 4.2 | Script de seed: criar filas SQS no LocalStack automaticamente | `init-localstack.sh` executado no startup |
| 4.3 | Health check endpoints em ambos os serviços (`GET /health`) | `200 OK` com status dos recursos (DB, Redis, SQS) |
| 4.4 | Testes E2E: `POST /lancamentos` → polling → `GET /consolidado/:data` | Teste automatizado valida o fluxo completo |
| 4.5 | Validar isolamento RNF-001: derrubar Consolidated e verificar que Entry continua respondendo | Teste de caos manual documentado |
| 4.6 | Documentar execução local no `README.md` | Seção "Como executar localmente" completa |

### Fluxo E2E Automatizado

```
1. POST /lancamentos  { tipo: CREDITO, valor: 100.00, data: hoje }
2. Assert: HTTP 201 + id retornado
3. Aguardar propagação (polling até 10s)
4. GET /consolidado/{hoje}
5. Assert: HTTP 200 + saldo_final = 100.00
```

### Definition of Done
- [ ] `docker compose up` sobe sem erros; todos os serviços passam no health check
- [ ] Teste E2E passa de ponta a ponta em ambiente limpo
- [ ] Entry Service continua respondendo `201` quando Consolidated está offline
- [ ] Tempo de boot da stack completa < 60 segundos

---

## Sprint 5 — Segurança (1 semana)

**Objetivo:** autenticação OAuth2/JWT em todos os endpoints e TLS configurado para dev.

### Tarefas

| # | Tarefa | Serviço | Entregável |
|---|--------|---------|-----------|
| 5.1 | Middleware JWT (RS256): validar `Authorization: Bearer` no Entry Service | Entry | Requisições sem token retornam `401` |
| 5.2 | Validação de scope: `scope:write` obrigatório no `POST /lancamentos` | Entry | Tokens com scope inválido retornam `403` |
| 5.3 | Guard JWT no Consolidated Service (NestJS `JwtAuthGuard`) | Consolidated | `GET /consolidado/:data` exige `scope:read` |
| 5.4 | Adicionar servidor OAuth2 local (Keycloak ou `keycloak-mock`) ao docker-compose | Infra | Token válido obtido via `client_credentials` |
| 5.5 | Testes: fluxos autenticado, sem token e scope errado | Ambos | Testes automatizados para os 3 cenários |
| 5.6 | Documentar fluxo de obtenção de token no `README.md` | Docs | Exemplo `curl` completo |

### Definition of Done
- [ ] Requisição sem token → `401 Unauthorized`
- [ ] Token com scope incorreto → `403 Forbidden`
- [ ] Token válido → fluxo normal
- [ ] Testes de segurança passando nos dois serviços

---

## Sprint 6 — Observabilidade (1 semana)

**Objetivo:** três pilares de observabilidade instrumentados e visíveis localmente.

### Stack
- OpenTelemetry SDK (.NET + Node.js)
- Jaeger (traces locais via docker-compose)
- Prometheus + Grafana (métricas locais via docker-compose)
- Logs JSON estruturados (`Serilog` no .NET, `pino` no NestJS)

### Tarefas

| # | Tarefa | Serviço | Entregável |
|---|--------|---------|-----------|
| 6.1 | Instrumentar Entry Service com OpenTelemetry (traces) | Entry | Trace visível no Jaeger: HTTP → DB → SQS |
| 6.2 | Instrumentar Consolidated Service com OpenTelemetry (traces) | Consolidated | Trace visível: SQS → Redis |
| 6.3 | Logs JSON estruturados com `correlation_id` e `lancamento_id` | Ambos | Logs parseáveis e correlacionáveis |
| 6.4 | Métricas customizadas: `lancamentos_criados_total`, `consolidado_latency_ms`, `redis_cache_hits_total` | Ambos | Métricas visíveis no Prometheus |
| 6.5 | Dashboard Grafana básico: taxa de lançamentos, latência P95, hit rate Redis | Infra | Dashboard importável como JSON |
| 6.6 | Adicionar Jaeger, Prometheus e Grafana ao docker-compose | Infra | `docker compose up` expõe UIs localmente |

### Definition of Done
- [ ] Trace de `POST /lancamentos` visível no Jaeger com todos os spans
- [ ] Logs incluem `correlation_id` correlacionando request → evento → consolidação
- [ ] Dashboard Grafana exibe métricas em tempo real durante testes manuais

---

## Sprint 7 — IaC + CI/CD (1 semana)

**Objetivo:** infraestrutura AWS provisionável via Terraform; deploy automatizado pelo pipeline.

### Stack
- Terraform >= 1.7 (módulos: VPC, ECS, RDS, ElastiCache, SQS, API GW)
- GitHub Actions (CI/CD)
- Amazon ECR (registry de imagens)

### Tarefas

| # | Tarefa | Entregável |
|---|--------|-----------|
| 7.1 | Módulo Terraform: VPC com subnets públicas e privadas | VPC isolada com NAT Gateway |
| 7.2 | Módulo Terraform: Amazon RDS for PostgreSQL (db.t3.micro, Multi-AZ opcional) | RDS provisionado na subnet privada |
| 7.3 | Módulo Terraform: Amazon ElastiCache Redis (cache.t3.micro) | Redis na subnet privada, TLS habilitado |
| 7.4 | Módulo Terraform: Amazon SQS (fila principal + DLQ) | Filas com política de retenção e DLQ configurada |
| 7.5 | Módulo Terraform: Amazon ECS Fargate — Entry + Consolidated Services | Task definitions com auto-scaling |
| 7.6 | Módulo Terraform: AWS API Gateway REST + authorizer JWT | Endpoints roteados para os serviços |
| 7.7 | Pipeline CI: build → lint → test → push ECR → deploy ECS (main) | Deploy automático ao merge em `main` |
| 7.8 | Pipeline CI: build → lint → test (pull requests) | PR bloqueado se qualquer etapa falhar |

### Estrutura Terraform

```
infra/terraform/
├── main.tf
├── variables.tf
├── outputs.tf
└── modules/
    ├── vpc/
    ├── rds/
    ├── elasticache/
    ├── sqs/
    ├── ecs/
    └── api-gateway/
```

### Definition of Done
- [ ] `terraform apply` provisiona stack completa na AWS sem erros manuais
- [ ] Deploy automático funciona via `git push origin main`
- [ ] PRs são bloqueados quando testes falham

---

## Sprint 8 — Testes de Carga + Hardening (1 semana)

**Objetivo:** validar RNFs quantitativos sob carga realista e corrigir gargalos.

### Stack
- k6 (testes de carga)
- Chaos engineering manual (parar containers)

### Tarefas

| # | Tarefa | Meta | Entregável |
|---|--------|------|-----------|
| 8.1 | Script k6: rampa de 0 → 50 RPS em `GET /consolidado/:data` | ≤ 5% de falhas, P95 ≤ 50ms | Relatório HTML do teste |
| 8.2 | Script k6: carga sustentada de `POST /lancamentos` (20 RPS) | P95 ≤ 200ms | Relatório HTML do teste |
| 8.3 | Teste de resiliência RNF-001: derrubar Consolidated durante carga no Entry | Entry: 0% de degradação | Evidência de isolamento (screenshot/log) |
| 8.4 | Ajuste de connection pool (PostgreSQL) e Redis pipeline se necessário | Latência dentro das metas | PR com ajuste documentado |
| 8.5 | Security review: OWASP Top 10 nos endpoints expostos | Sem findings críticos | Checklist preenchido |
| 8.6 | Atualizar `README.md` com resultados dos testes de carga | Seção "Resultados de Performance" | Tabela com métricas reais |

### Definition of Done
- [ ] `GET /consolidado/:data` suporta 50 RPS com ≤ 5% de falhas
- [ ] `POST /lancamentos` atinge P95 ≤ 200ms sob 20 RPS
- [ ] Derrubar o Consolidated Service não causa erros no Entry Service
- [ ] OWASP checklist sem findings de severidade alta ou crítica

---

## Cronograma Resumido

```
Semana  1 : Sprint 0 — Fundação
Semana  2 : Sprint 1 — Entry Service: Domain + API          ┐
Semana  3 : Sprint 1 — Entry Service: Domain + API          ┘
Semana  4 : Sprint 2 — Entry Service: Outbox Worker
Semana  5 : Sprint 3 — Consolidated Service: Core           ┐
Semana  6 : Sprint 3 — Consolidated Service: Core           ┘
Semana  7 : Sprint 4 — Integração E2E
Semana  8 : Sprint 5 — Segurança
Semana  9 : Sprint 6 — Observabilidade
             Sprint 7 — IaC + CI/CD (paralelo, equipe infra)
Semana 10 : Sprint 8 — Carga + Hardening
```

---

## Checklist de Ferramentas Necessárias

### Desenvolvimento Local
- [ ] Docker Desktop ≥ 24
- [ ] .NET SDK 8.0
- [ ] Node.js 20 LTS + pnpm 9
- [ ] k6 (testes de carga)

### Cloud / Deploy
- [ ] AWS CLI v2 configurado
- [ ] Terraform CLI ≥ 1.7
- [ ] AWS ECR — repositórios criados manualmente ou via `terraform apply` inicial

### IDE / Ferramentas
- [ ] VS Code ou Rider para .NET
- [ ] VS Code com extensão ESLint + Prettier para NestJS

---

## Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Drift de configuração entre LocalStack e AWS real | Média | Alto | Testes de integração também rodam contra AWS em staging |
| Latência do Outbox Worker exceder SLA de eventual consistency | Baixa | Médio | Configurar intervalo de polling dinâmico; alertar se backlog > 100 |
| Redis cache miss em cold start | Média | Médio | Pre-warm do cache após deploy via script de replay de eventos |
| Duplicação de mensagens SQS em cenário at-least-once | Alta | Baixo | Idempotência por `event_id` já prevista na Sprint 3 |
