# ECS cluster and service configuration.

# Create an ECS cluster.
resource "aws_ecs_cluster" "main" {
  name = "app-cluster"
  
  tags = {
    Name = "app-cluster"
  }
}

# IAM role for ECS task execution.
resource "aws_iam_role" "ecs_execution_role" {
  name = "ecs_execution_role"
  
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

# Attach policy to execution role.
resource "aws_iam_role_policy_attachment" "ecs_execution_policy" {
  role = aws_iam_role.ecs_execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Security group for ALB.
resource "aws_security_group" "alb_sg" {
  name = "alb-sg"
  description = "Security group for ALB"
  vpc_id = aws_vpc.main.id

  # Incoming traffic on port 8080 for server.
  ingress {
    from_port = 8080
    to_port = 8080
    protocol = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # Restrict in production (e.g., to VPC CIDR or specific IPs).
  }

  # Incoming traffic on port 9090 for Prometheus.
  ingress {
    from_port = 9090
    to_port = 9090
    protocol = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # Restrict in production.
  }

  # Incoming traffic on port 3000 for Grafana.
  ingress {
    from_port = 3000
    to_port = 3000
    protocol = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # Restrict in production.
  }

  egress {
    from_port = 0
    to_port = 0
    protocol = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "alb-sg"
  }
}

# Security group for ECS tasks.
resource "aws_security_group" "app_sg" {
  name = "app-sg"
  description = "Security group for ECS tasks"
  vpc_id = aws_vpc.main.id

  # Allow inbound traffic from ALB on port 8080 (server).
  ingress {
    from_port = 8080
    to_port = 8080
    protocol = "tcp"
    security_groups = [aws_security_group.alb_sg.id]
  }

  # Allow inbound traffic from tasks in the same security group on port 8080 (Prometheus to server app).
  ingress {
    from_port = 8080
    to_port = 8080
    protocol = "tcp"
    self = true
  }

  # Allow inbound traffic from ALB on port 9090 (Prometheus).
  ingress {
    from_port = 9090
    to_port = 9090
    protocol = "tcp"
    security_groups = [aws_security_group.alb_sg.id]
  }

  # Allow inbound traffic from tasks in the same security group on port 9090 (Grafana to Prometheus).
  ingress {
    from_port = 9090
    to_port = 9090
    protocol = "tcp"
    self = true
  }

  # Allow inbound traffic from ALB on port 3000 (Grafana).
  ingress {
    from_port = 3000
    to_port = 3000
    protocol = "tcp"
    security_groups = [aws_security_group.alb_sg.id]
  }

  egress {
    from_port = 0
    to_port = 0
    protocol = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "app-sg"
  }
}

# Application Load Balancer.
resource "aws_lb" "app" {
  name = "app-alb"
  internal = false
  load_balancer_type = "application"
  security_groups = [aws_security_group.alb_sg.id]
  subnets = [aws_subnet.public_a.id, aws_subnet.public_b.id]
  tags = {
    Name = "app-alb"
  }
}

# Listener for server container (port 8080).
resource "aws_lb_listener" "server" {
  load_balancer_arn = aws_lb.app.arn
  port = 8080
  protocol = "HTTP"

  default_action {
    type = "forward"
    target_group_arn = aws_lb_target_group.server.arn
  }
}

# Listener for Prometheus container (port 9090).
resource "aws_lb_listener" "prometheus" {
  load_balancer_arn = aws_lb.app.arn
  port = 9090
  protocol = "HTTP"

  default_action {
    type = "forward"
    target_group_arn = aws_lb_target_group.prometheus.arn
  }
}

# Listener for Grafana container (port 3000).
resource "aws_lb_listener" "grafana" {
  load_balancer_arn = aws_lb.app.arn
  port = 3000
  protocol = "HTTP"

  default_action {
    type = "forward"
    target_group_arn = aws_lb_target_group.grafana.arn
  }
}

# Target groups.
resource "aws_lb_target_group" "server" {
  name = "server-tg"
  port = 8080
  protocol = "HTTP"
  vpc_id = aws_vpc.main.id
  target_type = "ip"
  health_check {
    path = "/health"
    interval = 30
    timeout = 5
    healthy_threshold = 3
    unhealthy_threshold = 3
  }
  tags = {
    Name = "server-tg"
  }
}

resource "aws_lb_target_group" "prometheus" {
  name = "prometheus-tg"
  port = 9090
  protocol = "HTTP"
  vpc_id = aws_vpc.main.id
  target_type = "ip"
  health_check {
    path = "/-/healthy"
    interval = 30
    timeout = 5
    healthy_threshold = 3
    unhealthy_threshold = 3
  }
  tags = {
    Name = "prometheus-tg"
  }
}

resource "aws_lb_target_group" "grafana" {
  name = "grafana-tg"
  port = 3000
  protocol = "HTTP"
  vpc_id = aws_vpc.main.id
  target_type = "ip"
  health_check {
    path = "/api/health"
    interval = 30
    timeout = 5
    healthy_threshold = 3
    unhealthy_threshold = 3
  }
  tags = {
    Name = "grafana-tg"
  }
}

# ECS Task Definition for Server.
resource "aws_ecs_task_definition" "server" {
  family = "server-task"
  network_mode = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu = "256"
  memory = "512"
  execution_role_arn = aws_iam_role.ecs_execution_role.arn

  container_definitions = jsonencode([{
    name = "server-container"
    image = var.server_image
    portMappings = [{
      containerPort = 8080
    }]
    environment = [
      {
        name = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }
    ]
  }])
}

# ECS Task Definition for Prometheus.
resource "aws_ecs_task_definition" "prometheus" {
  family = "prometheus-task"
  network_mode = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu = "256"
  memory = "512"
  execution_role_arn = aws_iam_role.ecs_execution_role.arn

  container_definitions = jsonencode([{
    name = "prometheus-container"
    image = var.prometheus_image
    portMappings = [{
      containerPort = 9090
    }]
  }])
}

# ECS Task Definition for Grafana.
resource "aws_ecs_task_definition" "grafana" {
  family = "grafana-task"
  network_mode = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu = "256"
  memory = "512"
  execution_role_arn = aws_iam_role.ecs_execution_role.arn

  container_definitions = jsonencode([{
    name = "grafana-container"
    image = var.grafana_image
    portMappings = [{
      containerPort = 3000
    }]
    environment = [
      {
        name = "GF_SECURITY_ADMIN_PASSWORD"
        value = "admin"
      },
      {
        name = "GF_USERS_ALLOW_SIGN_UP"
        value = "false"
      }
    ]
  }])
}


# Private DNS namespace for service discovery.
resource "aws_service_discovery_private_dns_namespace" "app" {
  name = "local"
  description = "Private DNS namespace for ECS service discovery"
  vpc = aws_vpc.main.id
}

# Service discovery service for the web app.
resource "aws_service_discovery_service" "server" {
  name = "server-container"

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.app.id
    dns_records {
      ttl = 60
      type = "A"
    }
    routing_policy = "MULTIVALUE"
  }

  health_check_custom_config {
    failure_threshold = 1
  }
}

# Service discovery service for Prometheus.
resource "aws_service_discovery_service" "prometheus" {
  name = "prometheus-container"

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.app.id
    dns_records {
      ttl = 60
      type = "A"
    }
    routing_policy = "MULTIVALUE"
  }

  health_check_custom_config {
    failure_threshold = 1
  }
}



# ECS service for server with service discovery.
resource "aws_ecs_service" "server" {
  name = "server-service"
  cluster = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.server.arn
  launch_type = "FARGATE"
  desired_count = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.server.arn
    container_name = "server-container"
    container_port = 8080
  }

  network_configuration {
    subnets = [aws_subnet.private_a.id, aws_subnet.private_b.id]
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = false
  }

  # Enable service discovery.
  service_registries {
    registry_arn = aws_service_discovery_service.server.arn
  }

  depends_on = [aws_lb_listener.server, aws_service_discovery_service.server]
}

# ECS service for Prometheus with service discovery.
resource "aws_ecs_service" "prometheus" {
  name = "prometheus-service"
  cluster = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.prometheus.arn
  launch_type = "FARGATE"
  desired_count = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.prometheus.arn
    container_name = "prometheus-container"
    container_port = 9090
  }

  network_configuration {
    subnets = [aws_subnet.private_a.id, aws_subnet.private_b.id]
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = false
  }

  # Enable service discovery.
  service_registries {
    registry_arn = aws_service_discovery_service.prometheus.arn
  }

  depends_on = [aws_lb_listener.prometheus, aws_service_discovery_service.prometheus]
}

resource "aws_ecs_service" "grafana" {
  name = "grafana-service"
  cluster = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.grafana.arn
  launch_type = "FARGATE"
  desired_count = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.grafana.arn
    container_name = "grafana-container"
    container_port = 3000
  }

  network_configuration {
    subnets = [aws_subnet.private_a.id, aws_subnet.private_b.id]
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = false
  }

  # Enable service discovery.
  service_registries {
    registry_arn = aws_service_discovery_service.grafana.arn
  }

  depends_on = [aws_lb_listener.grafana, aws_service_discovery_service.grafana]
}
