# AWS provider configuration

# Configure the AWS provider.

provider "aws" {
  region = var.aws_region  # Using variable from variables.tf for region flexibility.
  
  # AWS credentials are expected to be configured via AWS CLI
  # or environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY).
}

# Ensure we're using a compatible Terraform version.

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"  # Require a recent but stable version.
    }
  }
  required_version = ">= 1.3.0"
}