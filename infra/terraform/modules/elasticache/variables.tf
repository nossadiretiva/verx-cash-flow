variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "subnet_ids" {
  description = "Subnets privadas para o ElastiCache"
  type        = list(string)
}

variable "allowed_security_group_ids" {
  description = "SGs com permissão de acesso ao Redis"
  type        = list(string)
}

variable "node_type" {
  type    = string
  default = "cache.t3.micro"
}
