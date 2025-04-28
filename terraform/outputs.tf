# Output values.

# Output useful information after deployment.

output "ecs_cluster_name" {
  description = "Name of the ECS cluster"
  value       = aws_ecs_cluster.main.name
}

output "ecs_service_names" {
  description = "Name of the ECS service"
  value = {
    server = aws_ecs_service.server.name
    prometheus = aws_ecs_service.prometheus.name
    grafana = aws_ecs_service.grafana.name
  }
}

output "alb_dns_name" {
  description = "DNS name of the load balancer"
  value       = aws_lb.app.dns_name
}