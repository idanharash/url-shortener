#!/bin/bash
set -e

echo "🛠 Running database migrations, then starting the web app..."
docker compose up -d --build web

echo "🌍 Web app is live at: http://localhost:5000"
