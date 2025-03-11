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
  
  # Allow inbound HTTP traffic.
  ingress {
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  # Allow all outbound traffic.
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# ECS Task Definition.
resource "aws_ecs_task_definition" "app" {
  family                   = "app-task"
  network_mode             = "awsvpc"  # Required for Fargate.
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"     # 0.25 vCPU
  memory                   = "512"     # 512MB memory
  execution_role_arn       = aws_iam_role.ecs_execution_role.arn
  
  container_definitions = jsonencode([{
    name  = "app-container"
    image = var.docker_image  # Your Docker image.
    portMappings = [{
      containerPort = 8080
      hostPort      = 8080
    }]
  }])
}

# ECS Service.
resource "aws_ecs_service" "app" {
  name            = "app-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.app.arn
  launch_type     = "FARGATE"
  desired_count   = 1
  
  network_configuration {
    subnets         = [aws_subnet.public_a.id, aws_subnet.public_b.id]
    security_groups = [aws_security_group.app_sg.id]
    assign_public_ip = true  # Required for Fargate in public subnets.
  }
}