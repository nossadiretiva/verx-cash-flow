output "endpoint" {
  value     = aws_db_instance.main.address
  sensitive = true
}

output "port" {
  value = aws_db_instance.main.port
}

output "db_name" {
  value = aws_db_instance.main.db_name
}

output "security_group_id" {
  value = aws_security_group.rds.id
}

output "password_ssm_path" {
  description = "Caminho no SSM Parameter Store onde a senha está armazenada"
  value       = aws_ssm_parameter.db_password.name
}
