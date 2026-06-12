import { BalanceAggregatorService, LancamentoCriadoEvent } from '../../src/domain/balance-aggregator.service';
import { RedisService } from '../../src/infrastructure/redis/redis.service';

const makeRedis = (saldoAtual: ReturnType<RedisService['getSaldo']> extends Promise<infer T> ? T : never = null) => ({
  getSaldo: jest.fn().mockResolvedValue(saldoAtual),
  setSaldo: jest.fn().mockResolvedValue(undefined),
  markEventProcessed: jest.fn().mockResolvedValue(false),
  ping: jest.fn().mockResolvedValue('PONG'),
} as unknown as RedisService);

const makeEvent = (tipo: 'CREDITO' | 'DEBITO', valor: number, data = '2024-01-15'): LancamentoCriadoEvent => ({
  event_id: 'evt-001',
  event_type: 'LancamentoCriado',
  occurred_at: new Date().toISOString(),
  data: { lancamento_id: 'lanc-001', tipo, valor, data },
});

describe('BalanceAggregatorService', () => {
  it('credito em dia sem saldo anterior: saldo_final = valor', async () => {
    const redis = makeRedis(null);
    const svc = new BalanceAggregatorService(redis);

    await svc.aggregate(makeEvent('CREDITO', 100));

    expect(redis.setSaldo).toHaveBeenCalledWith('2024-01-15', expect.objectContaining({
      saldo_final: 100,
      total_creditos: 100,
      total_debitos: 0,
    }));
  });

  it('debito em dia sem saldo anterior: saldo_final = -valor', async () => {
    const redis = makeRedis(null);
    const svc = new BalanceAggregatorService(redis);

    await svc.aggregate(makeEvent('DEBITO', 50));

    expect(redis.setSaldo).toHaveBeenCalledWith('2024-01-15', expect.objectContaining({
      saldo_final: -50,
      total_creditos: 0,
      total_debitos: 50,
    }));
  });

  it('credito acumulado sobre saldo existente', async () => {
    const redis = makeRedis({ saldo_final: 200, total_creditos: 200, total_debitos: 0, updated_at: '' });
    const svc = new BalanceAggregatorService(redis);

    await svc.aggregate(makeEvent('CREDITO', 100));

    expect(redis.setSaldo).toHaveBeenCalledWith('2024-01-15', expect.objectContaining({
      saldo_final: 300,
      total_creditos: 300,
    }));
  });

  it('debito reduz saldo existente', async () => {
    const redis = makeRedis({ saldo_final: 500, total_creditos: 500, total_debitos: 0, updated_at: '' });
    const svc = new BalanceAggregatorService(redis);

    await svc.aggregate(makeEvent('DEBITO', 150));

    expect(redis.setSaldo).toHaveBeenCalledWith('2024-01-15', expect.objectContaining({
      saldo_final: 350,
      total_debitos: 150,
    }));
  });

  it('preserva data original do evento ao gravar saldo', async () => {
    const redis = makeRedis(null);
    const svc = new BalanceAggregatorService(redis);

    await svc.aggregate(makeEvent('CREDITO', 10, '2024-03-20'));

    expect(redis.setSaldo).toHaveBeenCalledWith('2024-03-20', expect.any(Object));
  });
});
