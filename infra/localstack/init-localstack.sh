#!/bin/bash
set -e

echo "==> Criando filas SQS no LocalStack..."

awslocal sqs create-queue \
  --queue-name cashflow-lancamentos-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}'

DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/cashflow-lancamentos-dlq \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' --output text)

awslocal sqs create-queue \
  --queue-name cashflow-lancamentos \
  --attributes "{
    \"VisibilityTimeout\": \"30\",
    \"MessageRetentionPeriod\": \"86400\",
    \"RedrivePolicy\": \"{\\\"deadLetterTargetArn\\\":\\\"${DLQ_ARN}\\\",\\\"maxReceiveCount\\\":\\\"3\\\"}\"
  }"

echo "==> Filas criadas:"
awslocal sqs list-queues
echo "==> LocalStack SQS pronto."
