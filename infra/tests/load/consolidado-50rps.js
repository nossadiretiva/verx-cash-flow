/**
 * Teste de carga — Consolidated Service
 * Alvo: 50 RPS em GET /consolidado/:data, duração 5 min
 * Thresholds: P95 < 200 ms, P99 < 500 ms, error rate < 5 %
 *
 * Execução:
 *   k6 run infra/tests/load/consolidado-50rps.js
 *
 * Variáveis de ambiente opcionais:
 *   BASE_URL       URL base do Consolidated Service (padrão: http://localhost:8081)
 *   TOKEN_URL      Endpoint de token Keycloak       (padrão: http://localhost:8180/...)
 *   CLIENT_ID      client_id OAuth2                 (padrão: cashflow-client)
 *   CLIENT_SECRET  client_secret OAuth2             (padrão: secret)
 */

import http from 'k6/http';
import { check, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { getToken } from './shared/auth.js';

// ── Métricas customizadas ────────────────────────────────────────────────────
const errorRate          = new Rate('consolidado_error_rate');
const consolidadoDur     = new Trend('consolidado_duration_ms', true);
const cacheHits          = new Counter('consolidado_cache_hits');
const cacheMisses        = new Counter('consolidado_cache_misses');
const requestsTotal      = new Counter('consolidado_requests_total');

// ── Configuração ─────────────────────────────────────────────────────────────
const BASE_URL      = __ENV.BASE_URL       || 'http://localhost:8081';
const TOKEN_URL     = __ENV.TOKEN_URL      || 'http://localhost:8180/realms/cashflow/protocol/openid-connect/token';
const CLIENT_ID     = __ENV.CLIENT_ID      || 'cashflow-client';
const CLIENT_SECRET = __ENV.CLIENT_SECRET  || 'secret';

export const options = {
  scenarios: {
    constant_rps: {
      executor: 'constant-arrival-rate',
      rate: 50,            // 50 iterações por segundo
      timeUnit: '1s',
      duration: '5m',
      preAllocatedVUs: 60,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_duration:        ['p(95)<200', 'p(99)<500'],
    consolidado_error_rate:   ['rate<0.05'],   // < 5 % de erros
    http_req_failed:          ['rate<0.05'],
  },
};

// ── Setup: obter token uma vez ────────────────────────────────────────────────
export function setup() {
  return { token: getToken(TOKEN_URL, CLIENT_ID, CLIENT_SECRET) };
}

// ── Pool de datas dos últimos 7 dias ─────────────────────────────────────────
function buildDatePool() {
  const dates = [];
  for (let i = 0; i < 7; i++) {
    const d = new Date();
    d.setDate(d.getDate() - i);
    dates.push(d.toISOString().split('T')[0]);
  }
  return dates;
}

const DATE_POOL = buildDatePool();

// ── Iteração principal ────────────────────────────────────────────────────────
export default function (data) {
  const targetDate = DATE_POOL[Math.floor(Math.random() * DATE_POOL.length)];
  const headers    = { Authorization: `Bearer ${data.token}` };

  group('GET /consolidado/:data', () => {
    const res = http.get(
      `${BASE_URL}/consolidado/${targetDate}`,
      { headers, tags: { name: 'GET /consolidado/:data' } },
    );

    // 200 (cache hit) e 404 (sem lançamentos nessa data) são respostas válidas
    const isValidStatus = res.status === 200 || res.status === 404;

    const ok = check(res, {
      'HTTP 200 ou 404':    () => isValidStatus,
      'duração < 200 ms':   (r) => r.timings.duration < 200,
      'body é JSON válido': (r) => {
        try { r.json(); return true; } catch { return false; }
      },
    });

    errorRate.add(!ok);
    consolidadoDur.add(res.timings.duration);
    requestsTotal.add(1);

    if (res.status === 200) cacheHits.add(1);
    else cacheMisses.add(1);
  });
}

// ── Teardown ──────────────────────────────────────────────────────────────────
export function teardown(data) {
  console.log('Teste de carga Consolidated Service finalizado.');
}
