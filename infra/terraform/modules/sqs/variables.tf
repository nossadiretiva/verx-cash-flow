variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "queue_name" {
  type    = string
  default = "cashflow-lancamentos"
}

variable "visibility_timeout_seconds" {
  type    = number
  default = 30
}

variable "message_retention_seconds" {
  type    = number
  default = 86400
}

variable "max_receive_count" {
  description = "Número de tentativas antes de enviar para DLQ"
  type        = number
  default     = 3
}
