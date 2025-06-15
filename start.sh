#!/bin/bash
set -e

echo "🚀 Starting core services (db, redis, rabbitmq)..."
docker-compose up -d db redis rabbitmq

echo "⏳ Waiting for PostgreSQL to be ready..."
until docker exec url-shortener-db-1 pg_isready -U idanh > /dev/null 2>&1; do
  echo "Waiting for PostgreSQL..."
  sleep 2
done
echo "✅ PostgreSQL is ready!"

echo "⏳ Waiting for Redis to be ready..."
until docker exec url-shortener-redis-1 redis-cli ping | grep -q PONG; do
  echo "Waiting for Redis..."
  sleep 2
done
echo "✅ Redis is ready!"

echo "⏳ Waiting for RabbitMQ to be ready..."
until curl -s http://localhost:15672 > /dev/null 2>&1; do
  echo "Waiting for RabbitMQ..."
  sleep 2
done
echo "✅ RabbitMQ is ready!"

echo "📦 Building migrations image..."
docker-compose build migrate

echo "🛠 Running database migrations..."
docker-compose run --rm migrate

echo "🚀 Starting web app..."
docker-compose up -d web

echo "🌍 All systems go! Visit: http://localhost:5000"