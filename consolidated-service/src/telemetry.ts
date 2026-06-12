// Este arquivo DEVE ser o primeiro import em main.ts.
// O SDK lê OTEL_SERVICE_NAME automaticamente via OTEL_RESOURCE_ATTRIBUTES ou env var.
import { NodeSDK } from '@opentelemetry/sdk-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-grpc';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';

const otlpEndpoint = process.env.OTLP_ENDPOINT;

const sdk = new NodeSDK({
  ...(otlpEndpoint
    ? { traceExporter: new OTLPTraceExporter({ url: otlpEndpoint }) }
    : {}),
  instrumentations: [
    getNodeAutoInstrumentations({
      '@opentelemetry/instrumentation-fs': { enabled: false },
      // pino instrumentation conflita com pino-pretty em modo async worker
      '@opentelemetry/instrumentation-pino': { enabled: false },
    }),
  ],
});

sdk.start();

process.on('SIGTERM', () => {
  sdk.shutdown().finally(() => process.exit(0));
});
