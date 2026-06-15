import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import {
<<<<<<< HEAD
  lancamentosProcessadosTotal,
  lancamentosDuplicadosTotal,
  lancamentosFalhasTotal,
} from '../../metrics';
import {
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
  SQSClient,
  ReceiveMessageCommand,
  DeleteMessageCommand,
  SendMessageCommand,
  MessageSystemAttributeName,
} from '@aws-sdk/client-sqs';
import { BalanceAggregatorService, LancamentoCriadoEvent } from '../../domain/balance-aggregator.service';
import { RedisService } from '../redis/redis.service';

@Injectable()
export class SqsConsumerService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(SqsConsumerService.name);
  private readonly sqsClient: SQSClient;
  private readonly queueUrl: string;
  private readonly dlqUrl: string;
  private running = false;

  constructor(
    private readonly aggregator: BalanceAggregatorService,
    private readonly redis: RedisService,
  ) {
    this.sqsClient = new SQSClient({
      region: process.env.AWS_REGION ?? 'us-east-1',
      endpoint: process.env.SQS_ENDPOINT_URL,
      credentials: {
        accessKeyId: process.env.AWS_ACCESS_KEY_ID ?? 'test',
        secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY ?? 'test',
      },
    });
    this.queueUrl = process.env.SQS_QUEUE_URL ?? '';
    this.dlqUrl = process.env.SQS_DLQ_URL ?? '';
  }

  onModuleInit() {
    this.running = true;
    void this.poll();
    this.logger.log('SQS Consumer iniciado.');
  }

  onModuleDestroy() {
    this.running = false;
  }

  private async poll(): Promise<void> {
    while (this.running) {
      try {
        await this.receiveAndProcess();
      } catch (err) {
        this.logger.error('Erro no loop de polling SQS', err);
        await this.sleep(5000);
      }
    }
  }

  private async receiveAndProcess(): Promise<void> {
    const response = await this.sqsClient.send(
      new ReceiveMessageCommand({
        QueueUrl: this.queueUrl,
        MaxNumberOfMessages: 10,
        WaitTimeSeconds: 20,
        MessageSystemAttributeNames: [MessageSystemAttributeName.ApproximateReceiveCount],
      }),
    );

    const messages = response.Messages ?? [];
    for (const message of messages) {
      await this.processMessage(message);
    }
  }

  private async processMessage(message: { Body?: string; ReceiptHandle?: string; Attributes?: Record<string, string> }): Promise<void> {
    const receiptHandle = message.ReceiptHandle!;
    const receiveCount = parseInt(message.Attributes?.['ApproximateReceiveCount'] ?? '1', 10);

    try {
      const event: LancamentoCriadoEvent = JSON.parse(message.Body ?? '{}');

      const isDuplicate = await this.redis.markEventProcessed(event.event_id);
      if (isDuplicate) {
<<<<<<< HEAD
        this.logger.warn({ event_id: event.event_id }, 'Evento duplicado ignorado');
        lancamentosDuplicadosTotal.inc();
=======
        this.logger.warn(`Evento duplicado ignorado: ${event.event_id}`);
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        await this.deleteMessage(receiptHandle);
        return;
      }

      await this.aggregator.aggregate(event);
<<<<<<< HEAD
      lancamentosProcessadosTotal.inc({ tipo: event.data.tipo });
      this.logger.log(`Evento processado: event_id=${event.event_id} tipo=${event.data.tipo}`);
      await this.deleteMessage(receiptHandle);
    } catch (err) {
      lancamentosFalhasTotal.inc();
      this.logger.error({ receiveCount, err }, 'Falha ao processar mensagem SQS');

=======
      await this.deleteMessage(receiptHandle);
    } catch (err) {
      this.logger.error(`Falha ao processar mensagem. ReceiveCount=${receiveCount}`, err);

      // Envia para DLQ após 3 falhas (SQS faz isso automaticamente via redrive,
      // mas tratamos explicitamente erros de parse/validação)
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
      if (receiveCount >= 3) {
        await this.sendToDlq(message.Body ?? '');
        await this.deleteMessage(receiptHandle);
      }
<<<<<<< HEAD
=======
      // Se < 3, deixa no queue para retry automático do SQS (visibility timeout)
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
    }
  }

  private async deleteMessage(receiptHandle: string): Promise<void> {
    await this.sqsClient.send(
      new DeleteMessageCommand({ QueueUrl: this.queueUrl, ReceiptHandle: receiptHandle }),
    );
  }

  private async sendToDlq(body: string): Promise<void> {
    if (!this.dlqUrl) return;
    await this.sqsClient.send(
      new SendMessageCommand({ QueueUrl: this.dlqUrl, MessageBody: body }),
    );
    this.logger.warn('Mensagem encaminhada para DLQ.');
  }

  private sleep(ms: number) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
