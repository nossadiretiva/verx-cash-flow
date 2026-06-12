output "cluster_name" {
  value = aws_ecs_cluster.main.name
}

output "cluster_arn" {
  value = aws_ecs_cluster.main.arn
}

output "alb_dns_name" {
  value = aws_lb.main.dns_name
}

output "alb_arn" {
  value = aws_lb.main.arn
}

output "alb_listener_arn" {
  value = aws_lb_listener.http.arn
}

output "entry_service_target_group_arn" {
  value = aws_lb_target_group.entry_service.arn
}

output "consolidated_service_target_group_arn" {
  value = aws_lb_target_group.consolidated_service.arn
}

output "ecs_security_group_id" {
  value = aws_security_group.ecs_tasks.id
}

output "task_role_arn" {
  value = aws_iam_role.ecs_task.arn
}
