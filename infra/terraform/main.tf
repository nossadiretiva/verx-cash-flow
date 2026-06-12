module "vpc" {
  source = "./modules/vpc"

  project_name       = var.project_name
  environment        = var.environment
  vpc_cidr           = var.vpc_cidr
  availability_zones = var.availability_zones
}

module "ecr" {
  source = "./modules/ecr"

  project_name = var.project_name
  environment  = var.environment
}

module "sqs" {
  source = "./modules/sqs"

  project_name = var.project_name
  environment  = var.environment
  queue_name   = "cashflow-lancamentos"
}

module "rds" {
  source = "./modules/rds"

  project_name               = var.project_name
  environment                = var.environment
  vpc_id                     = module.vpc.vpc_id
  subnet_ids                 = module.vpc.private_subnet_ids
  allowed_security_group_ids = [module.ecs.ecs_security_group_id]
  instance_class             = var.db_instance_class
  db_name                    = var.db_name
  db_username                = var.db_username
  multi_az                   = var.db_multi_az
}

module "elasticache" {
  source = "./modules/elasticache"

  project_name               = var.project_name
  environment                = var.environment
  vpc_id                     = module.vpc.vpc_id
  subnet_ids                 = module.vpc.private_subnet_ids
  allowed_security_group_ids = [module.ecs.ecs_security_group_id]
  node_type                  = var.redis_node_type
}

module "ecs" {
  source = "./modules/ecs"

  project_name    = var.project_name
  environment     = var.environment
  vpc_id          = module.vpc.vpc_id
  public_subnet_ids  = module.vpc.public_subnet_ids
  private_subnet_ids = module.vpc.private_subnet_ids
  aws_region      = var.aws_region

  entry_service_image        = var.entry_service_image
  consolidated_service_image = var.consolidated_service_image

  entry_service_desired_count        = var.entry_service_desired_count
  consolidated_service_desired_count = var.consolidated_service_desired_count
  entry_service_cpu                  = var.entry_service_cpu
  entry_service_memory               = var.entry_service_memory
  consolidated_service_cpu           = var.consolidated_service_cpu
  consolidated_service_memory        = var.consolidated_service_memory

  db_host              = module.rds.endpoint
  db_port              = module.rds.port
  db_name              = module.rds.db_name
  db_username          = var.db_username
  db_password_ssm_path = module.rds.password_ssm_path

  redis_host = module.elasticache.endpoint
  redis_port = module.elasticache.port

  sqs_queue_url = module.sqs.queue_url
  sqs_dlq_url   = module.sqs.dlq_url
  sqs_queue_arn = module.sqs.queue_arn

  jwt_jwks_uri = var.jwt_jwks_uri
  jwt_audience = var.jwt_audience
  jwt_issuer   = var.jwt_issuer

  otlp_endpoint = var.otlp_endpoint
}

module "api_gateway" {
  source = "./modules/api-gateway"

  project_name       = var.project_name
  environment        = var.environment
  vpc_id             = module.vpc.vpc_id
  private_subnet_ids = module.vpc.private_subnet_ids
  alb_listener_arn   = module.ecs.alb_listener_arn
  jwt_issuer         = var.jwt_issuer
  jwt_audience       = var.jwt_audience
}
