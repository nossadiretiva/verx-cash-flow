/**
 * Teste de carga — Entry Service
 * Alvo: 20 RPS em POST /lancamentos, duração 3 min
 * Thresholds: P95 < 500 ms, error rate < 1 %
 *
 * Execução:
 *   k6 run infra/tests/load/entry-20rps.js
 *
 * Variáveis de ambiente opcionais:
 *   BASE_URL       URL base do Entry Service   (padrão: http://localhost:8080)
 *   TOKEN_URL      Endpoint de token Keycloak  (padrão: http://localhost:8180/...)
 *   CLIENT_ID      client_id OAuth2            (padrão: cashflow-client)
 *   CLIENT_SECRET  client_secret OAuth2        (padrão: secret)
 */

import http from 'k6/http';
import { check, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { getToken } from './shared/auth.js';

// ── Métricas customizadas ────────────────────────────────────────────────────
const errorRate        = new Rate('entry_error_rate');
const lancamentoDur    = new Trend('entry_lancamento_duration_ms', true);
const lancamentosTotal = new Counter('entry_lancamentos_total');

// ── Configuração ─────────────────────────────────────────────────────────────
const BASE_URL     = __ENV.BASE_URL      || 'http://localhost:8080';
const TOKEN_URL    = __ENV.TOKEN_URL     || 'http://localhost:8180/realms/cashflow/protocol/openid-connect/token';
const CLIENT_ID    = __ENV.CLIENT_ID     || 'cashflow-client';
const CLIENT_SECRET = __ENV.CLIENT_SECRET || 'secret';

export const options = {
  scenarios: {
    constant_rps: {
      executor: 'constant-arrival-rate',
      rate: 20,            // 20 iterações por segundo
      timeUnit: '1s',
      duration: '3m',
      preAllocatedVUs: 30,
      maxVUs: 60,
    },
  },
  thresholds: {
    http_req_duration:     ['p(95)<500', 'p(99)<1000'],
    entry_error_rate:      ['rate<0.01'],   // < 1 % de erros
    http_req_failed:       ['rate<0.01'],
  },
};

// ── Setup: obter token uma vez ────────────────────────────────────────────────
export function setup() {
  return { token: getToken(TOKEN_URL, CLIENT_ID, CLIENT_SECRET) };
}

// ── Dados de teste ────────────────────────────────────────────────────────────
const TIPOS = ['CREDITO', 'DEBITO'];

/** Gera uma data aleatória nos últimos 7 dias no formato YYYY-MM-DD */
function randomDate() {
  const d = new Date();
  d.setDate(d.getDate() - Math.floor(Math.random() * 7));
  return d.toISOString().split('T')[0];
}

// ── Iteração principal ────────────────────────────────────────────────────────
export default function (data) {
  const tipo  = TIPOS[Math.floor(Math.random() * TIPOS.length)];
  const valor = parseFloat((Math.random() * 999 + 1).toFixed(2));
  const headers = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${data.token}`,
  };

  group('POST /lancamentos', () => {
    const res = http.post(
      `${BASE_URL}/lancamentos`,
      JSON.stringify({ tipo, valor, descricao: `k6 load ${tipo}`, data: randomDate() }),
      { headers, tags: { name: 'POST /lancamentos' } },
    );

    const ok = check(res, {
      'HTTP 201':         (r) => r.status === 201,
      'body tem id':      (r) => r.json('id') !== undefined,
      'duração < 500 ms': (r) => r.timings.duration < 500,
    });

    errorRate.add(!ok);
    lancamentoDur.add(res.timings.duration);
    lancamentosTotal.add(1);
  });
}

// ── Teardown: sumário final ───────────────────────────────────────────────────
export function teardown(data) {
  console.log('Teste de carga Entry Service finalizado.');
  console.log(`Token utilizado (primeiros 20 chars): ${data.token.substring(0, 20)}...`);
}
