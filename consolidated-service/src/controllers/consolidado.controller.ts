import { Controller, Get, NotFoundException, Param } from '@nestjs/common';
import { RedisService } from '../infrastructure/redis/redis.service';

@Controller('consolidado')
export class ConsolidadoController {
  constructor(private readonly redis: RedisService) {}

  @Get(':data')
  async getConsolidado(@Param('data') data: string) {
    const saldo = await this.redis.getSaldo(data);

    if (!saldo) {
      throw new NotFoundException('Consolidado não disponível para a data informada.');
    }

    return {
      data,
      saldo_final: saldo.saldo_final,
      total_creditos: saldo.total_creditos,
      total_debitos: saldo.total_debitos,
      updated_at: saldo.updated_at,
    };
  }
}
