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
  description = "Subnets privadas para o RDS"
  type        = list(string)
}

variable "allowed_security_group_ids" {
  description = "SGs com permissão de acesso ao RDS"
  type        = list(string)
}

variable "instance_class" {
  type    = string
  default = "db.t3.micro"
}

variable "db_name" {
  type = string
}

variable "db_username" {
  type      = string
  sensitive = true
}

variable "multi_az" {
  type    = bool
  default = false
}
