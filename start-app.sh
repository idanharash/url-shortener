#!/bin/bash
set -e

echo "🛠 Running database migrations, then starting the API and the Worker..."
docker compose up -d --build web worker

echo "🌍 Web app is live at: http://localhost:5000"
