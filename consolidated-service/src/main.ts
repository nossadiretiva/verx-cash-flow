// telemetry DEVE ser o primeiro import — inicializa o SDK antes do NestJS
import './telemetry';
import { NestFactory } from '@nestjs/core';
import { Logger } from 'nestjs-pino';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, { bufferLogs: true });
  app.useLogger(app.get(Logger));

  const port = process.env.PORT ?? 8081;
  await app.listen(port);
}

bootstrap();
