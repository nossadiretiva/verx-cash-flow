import { Injectable, Logger } from '@nestjs/common';
import { RedisService, SaldoDiario } from '../infrastructure/redis/redis.service';

export interface LancamentoCriadoEvent {
  event_id: string;
  event_type: string;
  occurred_at: string;
  data: {
    lancamento_id: string;
    tipo: 'CREDITO' | 'DEBITO';
    valor: number;
    data: string;
  };
}

@Injectable()
export class BalanceAggregatorService {
  private readonly logger = new Logger(BalanceAggregatorService.name);

  constructor(private readonly redis: RedisService) {}

  async aggregate(event: LancamentoCriadoEvent): Promise<void> {
    const { tipo, valor, data } = event.data;

    const current = (await this.redis.getSaldo(data)) ?? {
      saldo_final: 0,
      total_creditos: 0,
      total_debitos: 0,
      updated_at: new Date().toISOString(),
    };

    const updated: SaldoDiario =
      tipo === 'CREDITO'
        ? {
            total_creditos: current.total_creditos + valor,
            total_debitos: current.total_debitos,
            saldo_final: current.saldo_final + valor,
            updated_at: new Date().toISOString(),
          }
        : {
            total_creditos: current.total_creditos,
            total_debitos: current.total_debitos + valor,
            saldo_final: current.saldo_final - valor,
            updated_at: new Date().toISOString(),
          };

    await this.redis.setSaldo(data, updated);

    this.logger.log(
      `Saldo de ${data} atualizado. tipo=${tipo} valor=${valor} saldo_final=${updated.saldo_final}`,
    );
  }
}
