import http from 'k6/http';

/**
 * Obtém token OAuth2 via Client Credentials flow.
 * Chamado em setup() para reutilizar o token durante toda a execução.
 */
export function getToken(tokenUrl, clientId, clientSecret) {
  const response = http.post(
    tokenUrl,
    { grant_type: 'client_credentials', client_id: clientId, client_secret: clientSecret },
    { tags: { name: 'POST token' } },
  );

  if (response.status !== 200) {
    throw new Error(`Falha ao obter token OAuth2: HTTP ${response.status} — ${response.body}`);
  }

  return response.json('access_token');
}
