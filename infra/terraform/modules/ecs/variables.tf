variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "public_subnet_ids" {
  type = list(string)
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "entry_service_image" {
  type = string
}

variable "consolidated_service_image" {
  type = string
}

variable "entry_service_desired_count" {
  type    = number
  default = 2
}

variable "consolidated_service_desired_count" {
  type    = number
  default = 2
}

variable "entry_service_cpu" {
  type    = number
  default = 512
}

variable "entry_service_memory" {
  type    = number
  default = 1024
}

variable "consolidated_service_cpu" {
  type    = number
  default = 256
}

variable "consolidated_service_memory" {
  type    = number
  default = 512
}

variable "db_host" {
  type      = string
  sensitive = true
}

variable "db_port" {
  type    = number
  default = 5432
}

variable "db_name" {
  type = string
}

variable "db_username" {
  type      = string
  sensitive = true
}

variable "db_password_ssm_path" {
  description = "Caminho no SSM Parameter Store para a senha do RDS"
  type        = string
}

variable "redis_host" {
  type      = string
  sensitive = true
}

variable "redis_port" {
  type    = number
  default = 6379
}

variable "sqs_queue_url" {
  type = string
}

variable "sqs_dlq_url" {
  type = string
}

variable "sqs_queue_arn" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "jwt_jwks_uri" {
  type = string
}

variable "jwt_audience" {
  type = string
}

variable "jwt_issuer" {
  type = string
}

variable "otlp_endpoint" {
  type    = string
  default = ""
}
