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
  role       = aws_iam_role.ecs_execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Security group for Fargate tasks.
resource "aws_security_group" "app_sg" {
  name        = "app-sg"
  description = "Security group for Fargate tasks"
  vpc_id      = aws_vpc.main.id
  
  # Allow inbound HTTP traffic from ALB for Server.
  ingress {
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]  # ALB will handle security. Adjust for production.
  }
  
  # Allow inbound traffic from ALB for Prometheus.
  ingress {
    from_port   = 9090
    to_port     = 9090
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  # Allow inbound traffic from ALB for Grafana.
  ingress {
    from_port   = 3000
    to_port     = 3000
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Allow all outbound traffic (needed for ECR pulls).
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# Application load balancer (ALB) definition.
resource "aws_lb" "server" {
  name               = "app-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.app_sg.id]
  subnets            = [aws_subnet.public_a.id, aws_subnet.public_b.id]  # Remains in public subnets.
  
  tags = {
    Name = "app-alb"
  }
}

# Target Groups
resource "aws_lb_target_group" "server" {
  name        = "server-tg"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"  # Fargate uses IP targets
  
  health_check {
    path = "/health"  # App health endpoint.
  }
}

resource "aws_lb_target_group" "prometheus" {
  name        = "prometheus-tg"
  port        = 9090
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"
  
  health_check {
    path = "/"  # Prometheus default endpoint.
  }
}

resource "aws_lb_target_group" "grafana" {
  name        = "grafana-tg"
  port        = 3000
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"
  
  health_check {
    path = "/api/health"  # Grafana health endpoint.
  }
}

# ALB listener.
resource "aws_lb_listener" "server" {
  load_balancer_arn = aws_lb.app.arn
  port              = 8080
  protocol          = "HTTP"
  
  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.app.arn
  }
}

# Listener rules for Prometheus.
resource "aws_lb_listener_rule" "prometheus" {
  listener_arn = aws_lb_listener.app.arn
  priority     = 100
  
  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.prometheus.arn
  }
  
  condition {
    path_pattern {
      values = ["/prometheus/*"]
    }
  }
}

# Listener rules for Grafana.
resource "aws_lb_listener_rule" "grafana" {
  listener_arn = aws_lb_listener.app.arn
  priority     = 200
  
  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.grafana.arn
  }
  
  condition {
    path_pattern {
      values = ["/grafana/*"]
    }
  }
}

# ECS Task Definition for server.
resource "aws_ecs_task_definition" "server" {
  family                   = "server-task"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.ecs_execution_role.arn
  
  container_definitions = jsonencode([{
    name  = "server-container"
    image = var.server_image  # Docker image.
    portMappings = [{
      containerPort = 8080
      hostPort      = 8080
    }]
    environment = [
      {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }
    ]
  }])
}

# ECS Task Definition for Prometheus.
resource "aws_ecs_task_definition" "prometheus" {
  family                   = "prometheus-task"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.ecs_execution_role.arn
  
  container_definitions = jsonencode([{
    name  = "prometheus-container"
    image = "prom/prometheus:latest"
    portMappings = [{
      containerPort = 9090
      hostPort      = 9090
    }]
  }])
}

# ECS Task Definition for Grafana.
resource "aws_ecs_task_definition" "grafana" {
  family                   = "prometheus-task"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.ecs_execution_role.arn
  
  container_definitions = jsonencode([{
    name  = "grafana-container"
    image = "grafana/grafana:latest"
    portMappings = [{
      containerPort = 3000
      hostPort      = 3000
    }]
    environment = [
      {
        name  = "GF_SECURITY_ADMIN_PASSWORD"
        value = "admin"
      },
      {
        name  = "GF_USERS_ALLOW_SIGN_UP"
        value = "false"
      }
    ]
  }])
}

# ECS Services.
resource "aws_ecs_service" "server" {
  name            = "server-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.app.arn
  launch_type     = "FARGATE"
  desired_count   = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.app.arn
    container_name   = "server-container"
    container_port  = 8080
  }
  
  network_configuration {
    subnets         = [aws_subnet.private_a.id, aws_subnet.private_b.id]  # Use private subnets
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = false  # Turn off public IP
  }
}

resource "aws_ecs_service" "prometheus" {
  name            = "prometheus-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.prometheus.arn
  launch_type     = "FARGATE"
  desired_count   = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.prometheus.arn
    container_name   = "prometheus-container"
    container_port   = 9090
  }
  
  network_configuration {
    subnets         = [aws_subnet.private_a.id, aws_subnet.private_b.id]
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = false
  }
}

resource "aws_ecs_service" "grafana" {
  name            = "grafana-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.grafana.arn
  launch_type     = "FARGATE"
  desired_count   = 1

  load_balancer {
    target_group_arn = aws_lb_target_group.grafana.arn
    container_name   = "grafana-container"
    container_port   = 3000
  }
}