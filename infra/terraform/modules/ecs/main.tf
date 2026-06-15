locals {
  name_prefix = "${var.project_name}-${var.environment}"
}

# ── Cluster ───────────────────────────────────────────────────────────────────

resource "aws_ecs_cluster" "main" {
  name = local.name_prefix

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = { Name = local.name_prefix }
}

# ── CloudWatch Log Groups ─────────────────────────────────────────────────────

resource "aws_cloudwatch_log_group" "entry_service" {
  name              = "/ecs/${local.name_prefix}/entry-service"
  retention_in_days = 30
}

resource "aws_cloudwatch_log_group" "consolidated_service" {
  name              = "/ecs/${local.name_prefix}/consolidated-service"
  retention_in_days = 30
}

# ── IAM: Task Execution Role (pull ECR + SSM) ─────────────────────────────────

resource "aws_iam_role" "ecs_task_execution" {
  name = "${local.name_prefix}-ecs-task-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_task_execution_managed" {
  role       = aws_iam_role.ecs_task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "ecs_task_execution_ssm" {
  name = "${local.name_prefix}-ssm-read"
  role = aws_iam_role.ecs_task_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["ssm:GetParameters", "ssm:GetParameter"]
      Resource = "arn:aws:ssm:${var.aws_region}:*:parameter/${var.project_name}/${var.environment}/*"
    }]
  })
}

# ── IAM: Task Role (SQS + CloudWatch) ────────────────────────────────────────

resource "aws_iam_role" "ecs_task" {
  name = "${local.name_prefix}-ecs-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy" "ecs_task_sqs" {
  name = "${local.name_prefix}-sqs-access"
  role = aws_iam_role.ecs_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes",
        "sqs:GetQueueUrl",
        "sqs:ChangeMessageVisibility"
      ]
      Resource = [var.sqs_queue_arn]
    }]
  })
}

# ── Security Groups ───────────────────────────────────────────────────────────

resource "aws_security_group" "alb" {
  name        = "${local.name_prefix}-alb-sg"
  description = "Tráfego de entrada para o ALB"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${local.name_prefix}-alb-sg" }
}

resource "aws_security_group" "ecs_tasks" {
  name        = "${local.name_prefix}-ecs-tasks-sg"
  description = "Tráfego das tasks ECS"
  vpc_id      = var.vpc_id

  ingress {
    description     = "ALB → ECS"
    from_port       = 0
    to_port         = 65535
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${local.name_prefix}-ecs-tasks-sg" }
}

# ── Application Load Balancer ─────────────────────────────────────────────────

resource "aws_lb" "main" {
  name               = "${local.name_prefix}-alb"
  internal           = true
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.private_subnet_ids

  tags = { Name = "${local.name_prefix}-alb" }
}

resource "aws_lb_target_group" "entry_service" {
  name        = "${local.name_prefix}-entry-tg"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    path                = "/health"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
    timeout             = 5
    matcher             = "200"
  }

  tags = { Name = "${local.name_prefix}-entry-tg" }
}

resource "aws_lb_target_group" "consolidated_service" {
  name        = "${local.name_prefix}-consolidated-tg"
  port        = 8081
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    path                = "/health"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
    timeout             = 5
    matcher             = "200"
  }

  tags = { Name = "${local.name_prefix}-consolidated-tg" }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "fixed-response"
    fixed_response {
      content_type = "application/json"
      message_body = "{\"message\":\"Not Found\"}"
      status_code  = "404"
    }
  }
}

resource "aws_lb_listener_rule" "entry_service" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 10

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.entry_service.arn
  }

  condition {
    path_pattern { values = ["/lancamentos", "/lancamentos/*", "/health"] }
  }
}

resource "aws_lb_listener_rule" "consolidated_service" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 20

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.consolidated_service.arn
  }

  condition {
    path_pattern { values = ["/consolidado/*"] }
  }
}

# ── Task Definitions ──────────────────────────────────────────────────────────

resource "aws_ecs_task_definition" "entry_service" {
  family                   = "${local.name_prefix}-entry-service"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.entry_service_cpu
  memory                   = var.entry_service_memory
  execution_role_arn       = aws_iam_role.ecs_task_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([{
    name      = "entry-service"
    image     = var.entry_service_image
    essential = true

    portMappings = [{ containerPort = 8080, protocol = "tcp" }]

    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_URLS",        value = "http://+:8080" },
      { name = "Sqs__QueueUrl",          value = var.sqs_queue_url },
      { name = "Sqs__Region",            value = var.aws_region },
      { name = "Auth__JwksUri",          value = var.jwt_jwks_uri },
      { name = "Auth__Audience",         value = var.jwt_audience },
      { name = "Auth__Issuer",           value = var.jwt_issuer },
      { name = "Otlp__Endpoint",         value = var.otlp_endpoint }
    ]

    secrets = [
      {
        name      = "ConnectionStrings__Postgres"
        valueFrom = "/${var.project_name}/${var.environment}/db/connection_string"
      },
      {
        name      = "DB_PASSWORD"
        valueFrom = var.db_password_ssm_path
      }
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.entry_service.name
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "ecs"
      }
    }

    healthCheck = {
      command     = ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval    = 30
      timeout     = 5
      retries     = 3
      startPeriod = 60
    }
  }])
}

resource "aws_ecs_task_definition" "consolidated_service" {
  family                   = "${local.name_prefix}-consolidated-service"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.consolidated_service_cpu
  memory                   = var.consolidated_service_memory
  execution_role_arn       = aws_iam_role.ecs_task_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([{
    name      = "consolidated-service"
    image     = var.consolidated_service_image
    essential = true

    portMappings = [{ containerPort = 8081, protocol = "tcp" }]

    environment = [
      { name = "NODE_ENV",              value = "production" },
      { name = "PORT",                  value = "8081" },
      { name = "REDIS_URL",             value = "redis://${var.redis_host}:${var.redis_port}" },
      { name = "SQS_QUEUE_URL",         value = var.sqs_queue_url },
      { name = "SQS_DLQ_URL",           value = var.sqs_dlq_url },
      { name = "AWS_REGION",            value = var.aws_region },
      { name = "JWT_JWKS_URI",          value = var.jwt_jwks_uri },
      { name = "JWT_AUDIENCE",          value = var.jwt_audience },
      { name = "JWT_ISSUER",            value = var.jwt_issuer },
      { name = "OTLP_ENDPOINT",         value = var.otlp_endpoint },
      { name = "OTEL_SERVICE_NAME",     value = "consolidated-service" },
      { name = "REDIS_SALDO_TTL_DAYS",  value = "7" }
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.consolidated_service.name
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "ecs"
      }
    }

    healthCheck = {
      command     = ["CMD-SHELL", "curl -sf http://localhost:8081/health || exit 1"]
      interval    = 30
      timeout     = 5
      retries     = 3
      startPeriod = 60
    }
  }])
}

# ── ECS Services ──────────────────────────────────────────────────────────────

resource "aws_ecs_service" "entry_service" {
  name            = "${local.name_prefix}-entry-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.entry_service.arn
  desired_count   = var.entry_service_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.entry_service.arn
    container_name   = "entry-service"
    container_port   = 8080
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  lifecycle {
    ignore_changes = [desired_count, task_definition]
  }

  depends_on = [aws_lb_listener_rule.entry_service]
}

resource "aws_ecs_service" "consolidated_service" {
  name            = "${local.name_prefix}-consolidated-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.consolidated_service.arn
  desired_count   = var.consolidated_service_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.consolidated_service.arn
    container_name   = "consolidated-service"
    container_port   = 8081
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  lifecycle {
    ignore_changes = [desired_count, task_definition]
  }

  depends_on = [aws_lb_listener_rule.consolidated_service]
}

# ── Auto Scaling ──────────────────────────────────────────────────────────────

resource "aws_appautoscaling_target" "entry_service" {
  max_capacity       = 10
  min_capacity       = 1
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.entry_service.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "entry_service_cpu" {
  name               = "${local.name_prefix}-entry-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.entry_service.resource_id
  scalable_dimension = aws_appautoscaling_target.entry_service.scalable_dimension
  service_namespace  = aws_appautoscaling_target.entry_service.service_namespace

  target_tracking_scaling_policy_configuration {
    target_value       = 70
    scale_in_cooldown  = 300
    scale_out_cooldown = 60

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}

resource "aws_appautoscaling_target" "consolidated_service" {
  max_capacity       = 10
  min_capacity       = 1
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.consolidated_service.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "consolidated_service_cpu" {
  name               = "${local.name_prefix}-consolidated-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.consolidated_service.resource_id
  scalable_dimension = aws_appautoscaling_target.consolidated_service.scalable_dimension
  service_namespace  = aws_appautoscaling_target.consolidated_service.service_namespace

  target_tracking_scaling_policy_configuration {
    target_value       = 70
    scale_in_cooldown  = 300
    scale_out_cooldown = 60

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}
