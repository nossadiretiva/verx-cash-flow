<<<<<<< HEAD
import { Controller, Get, NotFoundException, Param, UseGuards } from '@nestjs/common';
import { ReadGuard } from '../auth/jwt-auth.guard';
import { consolidadoLatency, redisCacheHitsTotal, redisCacheMissesTotal } from '../metrics';
=======
import { Controller, Get, NotFoundException, Param } from '@nestjs/common';
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
import { RedisService } from '../infrastructure/redis/redis.service';

@Controller('consolidado')
export class ConsolidadoController {
  constructor(private readonly redis: RedisService) {}

<<<<<<< HEAD
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
=======
  @Get(':data')
  async getConsolidado(@Param('data') data: string) {
    const saldo = await this.redis.getSaldo(data);

    if (!saldo) {
      throw new NotFoundException('Consolidado não disponível para a data informada.');
    }

>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
    return {
      data,
      saldo_final: saldo.saldo_final,
      total_creditos: saldo.total_creditos,
      total_debitos: saldo.total_debitos,
      updated_at: saldo.updated_at,
    };
  }
}
