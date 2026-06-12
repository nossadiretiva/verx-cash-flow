import { Module } from '@nestjs/common';
import { TerminusModule } from '@nestjs/terminus';
import { ConsolidadoController } from './controllers/consolidado.controller';
import { BalanceAggregatorService } from './domain/balance-aggregator.service';
import { RedisService } from './infrastructure/redis/redis.service';
import { SqsConsumerService } from './infrastructure/sqs/sqs-consumer.service';
import { HealthController } from './controllers/health.controller';

@Module({
  imports: [TerminusModule],
  controllers: [ConsolidadoController, HealthController],
  providers: [RedisService, BalanceAggregatorService, SqsConsumerService],
})
export class AppModule {}
