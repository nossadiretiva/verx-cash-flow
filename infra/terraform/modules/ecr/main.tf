locals {
  name_prefix = "${var.project_name}"
}

resource "aws_ecr_repository" "entry_service" {
  name                 = "${local.name_prefix}/entry-service"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = { Name = "${local.name_prefix}-entry-service" }
}

resource "aws_ecr_repository" "consolidated_service" {
  name                 = "${local.name_prefix}/consolidated-service"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = { Name = "${local.name_prefix}-consolidated-service" }
}

resource "aws_ecr_lifecycle_policy" "entry_service" {
  repository = aws_ecr_repository.entry_service.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Manter últimas ${var.image_retention_count} imagens tagged"
        selection = {
          tagStatus   = "tagged"
          tagPrefixList = ["v", "sha-"]
          countType   = "imageCountMoreThan"
          countNumber = var.image_retention_count
        }
        action = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Remover imagens untagged após 7 dias"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = 7
        }
        action = { type = "expire" }
      }
    ]
  })
}

resource "aws_ecr_lifecycle_policy" "consolidated_service" {
  repository = aws_ecr_repository.consolidated_service.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Manter últimas ${var.image_retention_count} imagens tagged"
        selection = {
          tagStatus     = "tagged"
          tagPrefixList = ["v", "sha-"]
          countType     = "imageCountMoreThan"
          countNumber   = var.image_retention_count
        }
        action = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Remover imagens untagged após 7 dias"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = 7
        }
        action = { type = "expire" }
      }
    ]
  })
}
