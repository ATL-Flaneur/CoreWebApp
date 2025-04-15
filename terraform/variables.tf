# Variable definitions

# AWS region to deploy resources.
variable "aws_region" {
  description = "The AWS region to deploy resources into"
  type        = string
  default     = "us-east-1"
}

# Server docker image.
variable "server_image" {
  description = "Docker image to deploy (e.g., yourusername/yourapp:latest)"
  type        = string
  default     = "890742572437.dkr.ecr.us-east-1.amazonaws.com/server:latest"
}

# Grafana docker image.
variable "grafana_image" {
  description = "Docker image to deploy (e.g., yourusername/yourapp:latest)"
  type        = string
  default     = "890742572437.dkr.ecr.us-east-1.amazonaws.com/grafana:latest"
}


# Prometheus docker image.
variable "prometheus_image" {
  description = "Docker image to deploy (e.g., yourusername/yourapp:latest)"
  type        = string
  default     = "890742572437.dkr.ecr.us-east-1.amazonaws.com/prometheus:latest"
}
  

# Container port your app listens on.
variable "container_port" {
  description = "Port the ASP.NET app listens on inside the container"
  type        = number
  default     = 8080
}