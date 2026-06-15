import { Module } from '@nestjs/common';
<<<<<<< HEAD
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
=======
import { TerminusModule } from '@nestjs/terminus';
import { ConsolidadoController } from './controllers/consolidado.controller';
import { BalanceAggregatorService } from './domain/balance-aggregator.service';
import { RedisService } from './infrastructure/redis/redis.service';
import { SqsConsumerService } from './infrastructure/sqs/sqs-consumer.service';
import { HealthController } from './controllers/health.controller';

@Module({
  imports: [TerminusModule],
  controllers: [ConsolidadoController, HealthController],
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
  providers: [RedisService, BalanceAggregatorService, SqsConsumerService],
})
export class AppModule {}
