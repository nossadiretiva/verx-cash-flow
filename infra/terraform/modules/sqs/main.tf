locals {
  name_prefix = "${var.project_name}-${var.environment}"
}

resource "aws_sqs_queue" "dlq" {
  name                       = "${local.name_prefix}-${var.queue_name}-dlq"
  message_retention_seconds  = 1209600 # 14 dias
  visibility_timeout_seconds = var.visibility_timeout_seconds

  tags = { Name = "${local.name_prefix}-${var.queue_name}-dlq" }
}

resource "aws_sqs_queue" "main" {
  name                       = "${local.name_prefix}-${var.queue_name}"
  visibility_timeout_seconds = var.visibility_timeout_seconds
  message_retention_seconds  = var.message_retention_seconds

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = var.max_receive_count
  })

  tags = { Name = "${local.name_prefix}-${var.queue_name}" }
}

resource "aws_sqs_queue_redrive_allow_policy" "dlq" {
  queue_url = aws_sqs_queue.dlq.id

  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.main.arn]
  })
}
