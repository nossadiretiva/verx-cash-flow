variable "aws_region" {
  description = "Região AWS onde os recursos serão criados"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Nome do projeto, usado como prefixo nos recursos"
  type        = string
  default     = "verx-cash-flow"
}

variable "environment" {
  description = "Ambiente de deploy (production, staging)"
  type        = string
  default     = "production"

  validation {
    condition     = contains(["production", "staging"], var.environment)
    error_message = "Ambiente deve ser 'production' ou 'staging'."
  }
}

# ── Rede ──────────────────────────────────────────────────────────────────────

variable "vpc_cidr" {
  description = "CIDR block da VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "Lista de AZs a utilizar (mínimo 2)"
  type        = list(string)
  default     = ["us-east-1a", "us-east-1b"]
}

# ── Banco de dados ────────────────────────────────────────────────────────────

variable "db_instance_class" {
  description = "Classe da instância RDS"
  type        = string
  default     = "db.t3.micro"
}

variable "db_name" {
  description = "Nome do banco de dados"
  type        = string
  default     = "cashflow"
}

variable "db_username" {
  description = "Usuário master do RDS"
  type        = string
  default     = "cashflow"
  sensitive   = true
}

variable "db_multi_az" {
  description = "Habilitar Multi-AZ no RDS"
  type        = bool
  default     = false
}

# ── Cache ─────────────────────────────────────────────────────────────────────

variable "redis_node_type" {
  description = "Tipo do nó ElastiCache"
  type        = string
  default     = "cache.t3.micro"
}

# ── ECS ───────────────────────────────────────────────────────────────────────

variable "entry_service_image" {
  description = "URI completa da imagem Docker do Entry Service (ECR)"
  type        = string
  default     = ""
}

variable "consolidated_service_image" {
  description = "URI completa da imagem Docker do Consolidated Service (ECR)"
  type        = string
  default     = ""
}

variable "entry_service_desired_count" {
  description = "Número desejado de tasks do Entry Service"
  type        = number
  default     = 2
}

variable "consolidated_service_desired_count" {
  description = "Número desejado de tasks do Consolidated Service"
  type        = number
  default     = 2
}

variable "entry_service_cpu" {
  description = "CPU units para o Entry Service (1024 = 1 vCPU)"
  type        = number
  default     = 512
}

variable "entry_service_memory" {
  description = "Memória em MiB para o Entry Service"
  type        = number
  default     = 1024
}

variable "consolidated_service_cpu" {
  description = "CPU units para o Consolidated Service"
  type        = number
  default     = 256
}

variable "consolidated_service_memory" {
  description = "Memória em MiB para o Consolidated Service"
  type        = number
  default     = 512
}

# ── Autenticação ──────────────────────────────────────────────────────────────

variable "jwt_issuer" {
  description = "Issuer do JWT (URL do realm Keycloak)"
  type        = string
}

variable "jwt_audience" {
  description = "Audience esperado nos tokens JWT"
  type        = string
  default     = "cashflow-api"
}

variable "jwt_jwks_uri" {
  description = "URL do JWKS endpoint do servidor de autorização"
  type        = string
}

# ── Observabilidade ───────────────────────────────────────────────────────────

variable "otlp_endpoint" {
  description = "Endpoint OTLP para envio de traces (ex: Jaeger em ECS)"
  type        = string
  default     = ""
}
