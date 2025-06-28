#!/bin/bash
set -e

echo "📦 Building migrations..."
docker-compose build migrate

echo "🛠 Running database migrations..."
docker-compose run --rm migrate

echo "🚀 Starting web app..."
docker-compose up -d web

echo "🌍 Web app is live at: http://localhost:5000"