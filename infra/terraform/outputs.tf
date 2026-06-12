output "api_gateway_url" {
  description = "URL pública do API Gateway"
  value       = module.api_gateway.api_url
}

output "alb_dns_name" {
  description = "DNS do Application Load Balancer (uso interno)"
  value       = module.ecs.alb_dns_name
}

output "ecr_entry_service_url" {
  description = "URL do repositório ECR do Entry Service"
  value       = module.ecr.entry_service_repository_url
}

output "ecr_consolidated_service_url" {
  description = "URL do repositório ECR do Consolidated Service"
  value       = module.ecr.consolidated_service_repository_url
}

output "rds_endpoint" {
  description = "Endpoint do RDS PostgreSQL (sem porta)"
  value       = module.rds.endpoint
  sensitive   = true
}

output "redis_endpoint" {
  description = "Endpoint do ElastiCache Redis"
  value       = module.elasticache.endpoint
  sensitive   = true
}

output "sqs_queue_url" {
  description = "URL da fila SQS principal"
  value       = module.sqs.queue_url
}

output "sqs_dlq_url" {
  description = "URL da DLQ SQS"
  value       = module.sqs.dlq_url
}

output "vpc_id" {
  description = "ID da VPC criada"
  value       = module.vpc.vpc_id
}
