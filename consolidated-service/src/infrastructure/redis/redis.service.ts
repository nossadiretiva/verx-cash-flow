import { Injectable, OnModuleDestroy, OnModuleInit, Logger } from '@nestjs/common';
import Redis from 'ioredis';

@Injectable()
export class RedisService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(RedisService.name);
  private client: Redis;

  onModuleInit() {
    this.client = new Redis(process.env.REDIS_URL ?? 'redis://localhost:6379', {
      maxRetriesPerRequest: 3,
      lazyConnect: true,
    });
    this.client.on('error', (err) => this.logger.error('Redis error', err));
    this.logger.log('Redis client inicializado.');
  }

  async onModuleDestroy() {
    await this.client.quit();
  }

  async setSaldo(data: string, saldo: SaldoDiario): Promise<void> {
    const ttlSeconds = Number(process.env.REDIS_SALDO_TTL_DAYS ?? 7) * 86400;
    await this.client.hset(`saldo:${data}`, {
      saldo_final: saldo.saldo_final.toFixed(2),
      total_creditos: saldo.total_creditos.toFixed(2),
      total_debitos: saldo.total_debitos.toFixed(2),
      updated_at: saldo.updated_at,
    });
    await this.client.expire(`saldo:${data}`, ttlSeconds);
  }

  async getSaldo(data: string): Promise<SaldoDiario | null> {
    const raw = await this.client.hgetall(`saldo:${data}`);
    if (!raw || Object.keys(raw).length === 0) return null;
    return {
      saldo_final: parseFloat(raw.saldo_final),
      total_creditos: parseFloat(raw.total_creditos),
      total_debitos: parseFloat(raw.total_debitos),
      updated_at: raw.updated_at,
    };
  }

  // Idempotência: retorna true se o event_id já foi processado
  async markEventProcessed(eventId: string): Promise<boolean> {
    const ttl = 48 * 3600;
    const result = await this.client.set(`processed_event:${eventId}`, '1', 'EX', ttl, 'NX');
    return result === null; // null = já existia = duplicata
  }

  async ping(): Promise<string> {
    return this.client.ping();
  }
}

export interface SaldoDiario {
  saldo_final: number;
  total_creditos: number;
  total_debitos: number;
  updated_at: string;
}
