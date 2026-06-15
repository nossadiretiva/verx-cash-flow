<<<<<<< HEAD
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
=======
import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, {
    logger: ['log', 'warn', 'error'],
  });

  const port = process.env.PORT ?? 8081;
  await app.listen(port);
  console.log(`Consolidated Service rodando na porta ${port}`);
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
}

bootstrap();
