import { Module } from '@nestjs/common';
import { LoggerModule } from 'nestjs-pino';
import { TerminusModule } from '@nestjs/terminus';
import { AuthModule } from './auth/auth.module';
import { ConsolidadoController } from './controllers/consolidado.controller';
import { HealthController } from './controllers/health.controller';
import { MetricsController } from './controllers/metrics.controller';
import { BalanceAggregatorService } from './domain/balance-aggregator.service';
import { RedisService } from './infrastructure/redis/redis.service';
import { SqsConsumerService } from './infrastructure/sqs/sqs-consumer.service';

@Module({
  imports: [
    LoggerModule.forRoot({
      pinoHttp: {
        level: process.env.LOG_LEVEL ?? (process.env.NODE_ENV === 'production' ? 'info' : 'debug'),
        // JSON estruturado — use `... | pino-pretty` localmente se quiser output colorido
      },
    }),
    TerminusModule,
    AuthModule,
  ],
  controllers: [ConsolidadoController, HealthController, MetricsController],
  providers: [RedisService, BalanceAggregatorService, SqsConsumerService],
})
export class AppModule {}
