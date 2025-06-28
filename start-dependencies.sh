#!/bin/bash
set -e

echo "🚀 Starting core services..."
docker-compose up -d db redis rabbitmq jaeger

echo "⏳ Waiting for PostgreSQL..."
until docker exec url-shortener-db-1 pg_isready -U postgres > /dev/null 2>&1; do
  sleep 2
done
echo "✅ PostgreSQL ready."

echo "⏳ Waiting for Redis..."
until docker exec url-shortener-redis-1 redis-cli ping | grep -q PONG; do
  sleep 2
done
echo "✅ Redis ready."

echo "⏳ Waiting for RabbitMQ..."
until curl -sf http://localhost:15672 > /dev/null; do
  sleep 2
done
echo "✅ RabbitMQ ready."