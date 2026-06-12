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
}

bootstrap();
