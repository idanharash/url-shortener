#!/bin/bash
set -e

echo "🚀 Starting PostgreSQL, Redis, RabbitMQ and Jaeger (waits for health checks)..."
docker compose up -d --wait db redis rabbitmq jaeger
echo "✅ Dependencies ready."
