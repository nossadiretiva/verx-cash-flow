import { Controller, Get, NotFoundException, Param, UseGuards } from '@nestjs/common';
import { ReadGuard } from '../auth/jwt-auth.guard';
import { consolidadoLatency, redisCacheHitsTotal, redisCacheMissesTotal } from '../metrics';
import { RedisService } from '../infrastructure/redis/redis.service';

@Controller('consolidado')
export class ConsolidadoController {
  constructor(private readonly redis: RedisService) {}

  @UseGuards(ReadGuard)
  @Get(':data')
  async getConsolidado(@Param('data') data: string) {
    const end = consolidadoLatency.startTimer();
    const saldo = await this.redis.getSaldo(data);
    end();

    if (!saldo) {
      redisCacheMissesTotal.inc();
      throw new NotFoundException('Consolidado não disponível para a data informada.');
    }

    redisCacheHitsTotal.inc();
    return {
      data,
      saldo_final: saldo.saldo_final,
      total_creditos: saldo.total_creditos,
      total_debitos: saldo.total_debitos,
      updated_at: saldo.updated_at,
    };
  }
}
