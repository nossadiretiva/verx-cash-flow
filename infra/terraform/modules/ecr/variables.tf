variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "image_retention_count" {
  description = "Número de imagens tagged a manter por repositório"
  type        = number
  default     = 10
}
