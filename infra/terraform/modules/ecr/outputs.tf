output "entry_service_repository_url" {
  value = aws_ecr_repository.entry_service.repository_url
}

output "consolidated_service_repository_url" {
  value = aws_ecr_repository.consolidated_service.repository_url
}

output "registry_id" {
  value = aws_ecr_repository.entry_service.registry_id
}
