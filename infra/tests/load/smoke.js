/**
 * Smoke test — execução rápida para CI
 * 5 RPS por 30 s em cada serviço; falha rápido se algo estiver quebrado.
 *
 * Execução (requer stack local rodando):
 *   k6 run infra/tests/load/smoke.js
 */

import http from 'k6/http';
import { check, group } from 'k6';
import { getToken } from './shared/auth.js';

const BASE_ENTRY        = __ENV.BASE_ENTRY        || 'http://localhost:8080';
const BASE_CONSOLIDATED = __ENV.BASE_CONSOLIDATED  || 'http://localhost:8081';
const TOKEN_URL         = __ENV.TOKEN_URL          || 'http://localhost:8180/realms/cashflow/protocol/openid-connect/token';
const CLIENT_ID         = __ENV.CLIENT_ID          || 'cashflow-client';
const CLIENT_SECRET     = __ENV.CLIENT_SECRET      || 'secret';

export const options = {
  scenarios: {
    smoke: {
      executor: 'constant-arrival-rate',
      rate: 5,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 10,
      maxVUs: 20,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    http_req_failed:   ['rate<0.05'],
  },
};

export function setup() {
  return { token: getToken(TOKEN_URL, CLIENT_ID, CLIENT_SECRET) };
}

const TODAY = new Date().toISOString().split('T')[0];

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}` };

  group('entry-service health', () => {
    const res = http.get(`${BASE_ENTRY}/health`, { tags: { name: 'GET /health entry' } });
    check(res, { 'entry health 200': (r) => r.status === 200 });
  });

  group('consolidated-service health', () => {
    const res = http.get(`${BASE_CONSOLIDATED}/health`, { tags: { name: 'GET /health consolidated' } });
    check(res, { 'consolidated health 200': (r) => r.status === 200 });
  });

  group('POST /lancamentos', () => {
    const res = http.post(
      `${BASE_ENTRY}/lancamentos`,
      JSON.stringify({ tipo: 'CREDITO', valor: 10.0, descricao: 'smoke test', data: TODAY }),
      { headers: { ...headers, 'Content-Type': 'application/json' }, tags: { name: 'POST /lancamentos smoke' } },
    );
    check(res, { 'lancamento 201': (r) => r.status === 201 });
  });

  group('GET /consolidado/:data', () => {
    const res = http.get(
      `${BASE_CONSOLIDATED}/consolidado/${TODAY}`,
      { headers, tags: { name: 'GET /consolidado smoke' } },
    );
    check(res, { 'consolidado 200 ou 404': (r) => r.status === 200 || r.status === 404 });
  });
}
