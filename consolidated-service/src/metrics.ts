import { Registry, Counter, Histogram, collectDefaultMetrics } from 'prom-client';

export const metricsRegistry = new Registry();

collectDefaultMetrics({ register: metricsRegistry, prefix: 'consolidated_' });

export const lancamentosProcessadosTotal = new Counter({
  name: 'consolidated_lancamentos_processados_total',
  help: 'Total de eventos LancamentoCriado processados com sucesso',
  labelNames: ['tipo'] as const,
  registers: [metricsRegistry],
});

export const lancamentosDuplicadosTotal = new Counter({
  name: 'consolidated_lancamentos_duplicados_total',
  help: 'Total de eventos ignorados por idempotência (event_id já processado)',
  registers: [metricsRegistry],
});

export const lancamentosFalhasTotal = new Counter({
  name: 'consolidated_lancamentos_falhas_total',
  help: 'Total de falhas ao processar eventos SQS',
  registers: [metricsRegistry],
});

export const redisCacheHitsTotal = new Counter({
  name: 'consolidated_redis_cache_hits_total',
  help: 'Total de consultas ao Redis que retornaram saldo existente',
  registers: [metricsRegistry],
});

export const redisCacheMissesTotal = new Counter({
  name: 'consolidated_redis_cache_misses_total',
  help: 'Total de consultas ao Redis sem saldo (cache miss)',
  registers: [metricsRegistry],
});

export const consolidadoLatency = new Histogram({
  name: 'consolidated_consolidado_request_duration_ms',
  help: 'Latência das requisições GET /consolidado/:data em ms',
  buckets: [5, 10, 25, 50, 100, 250],
  registers: [metricsRegistry],
});
