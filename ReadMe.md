# 🔗 URL Shortening Service

A production-ready, high-performance URL shortener built in **ASP.NET Core 8**, using **PostgreSQL**, **Redis**, **RabbitMQ**, and **FluentMigrator**.  
Includes full observability, health checks, async processing, and Docker-based deployment.

---

## 🚀 Features

- ✅ Shorten long URLs via REST API
- ✅ Redirect short codes (with fast Redis cache)
- ✅ Track click counts asynchronously via RabbitMQ
- ✅ Database schema managed with FluentMigrator
- ✅ Metrics middleware for observability
- ✅ Resilience patterns (retry, circuit breaker)
- ✅ Health checks for DB, Redis, RabbitMQ
- ✅ Dockerized end-to-end (incl. migrations)
- ✅ Performance-tested (P95 < 20ms)

---

## 🏗️ Tech Stack

| Layer         | Technology                      |
| ------------- | ------------------------------- |
| Web Framework | ASP.NET Core 8                  |
| DB Access     | NHibernate + PostgreSQL         |
| Cache         | Redis                           |
| Queue         | RabbitMQ                        |
| Migrations    | FluentMigrator                  |
| Metrics       | Custom Middleware               |
| Resilience    | Microsoft.Extensions.Resilience |
| Observability | Health Checks, Logs             |
| Testing       | xUnit + Benchmarking Tool       |

---

## 🐳 Run with Docker (Recommended)

The project includes a full Docker setup with:

- PostgreSQL, Redis, RabbitMQ
- FluentMigrator runner
- Health-check based startup script

### 📦 Start everything (including migrations)

```bash
bash start.sh
```

Once started:

- Web App: [http://localhost:5000](http://localhost:5000)
- Swagger: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- Health: [http://localhost:5000/health](http://localhost:5000/health)
- RabbitMQ UI: [http://localhost:15672](http://localhost:15672)  
  _(user/pass from `.env`)_
- PgAdmin (optional): [http://localhost:5050](http://localhost:5050)

To shut down:

```bash
docker-compose down
```

---

## 📄 Database Schema

Managed by **FluentMigrator**  
See: [`InitialCreate.cs`](UrlShortener.Migrations/Migrations/InitialCreate.cs)

### 🗂️ Table: `short_urls`

| Column         | Type        | Description                          |
| -------------- | ----------- | ------------------------------------ |
| `code`         | `text`      | Primary key – the unique short code  |
| `original_url` | `text`      | The original long URL                |
| `click_count`  | `integer`   | Total number of clicks (async write) |
| `created_at`   | `timestamp` | Timestamp of creation (UTC)          |

#### SQL Definition

```sql
CREATE TABLE short_urls (
    code TEXT PRIMARY KEY,
    original_url TEXT NOT NULL,
    click_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL
);
```

---

## 🧪 Testing

Run unit tests:

```bash
dotnet test
```

Run performance test (e.g. 1000 concurrent GETs):

```bash
cd UrlShortenerBenchmark
dotnet run -- http://localhost:5000/{code} 1000
```

---

## 📈 Observability & Monitoring

- ✅ Custom metrics middleware (requests, cache hits, etc.)
- ✅ Health checks: `/health`
- 🧭 Tracing & structured logging ready to be plugged in
- 🛠️ `/metrics` endpoint can be added for Prometheus scraping

---

## 🧩 Architecture Overview

### 🎯 Components

- **API Service** – ASP.NET Core Web API
- **Redis** – Fast cache for GETs by short code
- **RabbitMQ** – Queue for async click tracking
- **Worker** – Background consumer updates DB with counts
- **Migrations** – Run via FluentMigrator on app start

### 🖼️ Diagram

You can view the architecture diagram here:

👉 [View Live Diagram (PlantUML)](https://www.plantuml.com/plantuml/png/TLBDKjim4BxhAROvjE40lVVG0LmWcS4qmVHKUb3aRQA9BBchDPaCmxjtxMg8VwOtQRyVxUhRoyYQkAcS-i0xh11gfwrNSMzzhKbNO49L1u-U2puw14B3scyTzYYrDdkznJ51fJhCCcV500fRuWnU5S3FQmg7YFGBT8HqLcyznTLg2VVwY0Jpgs9ryN3p15aWFElafvvWzeDS5ZnJS0vfrjgThXDcWVHY3NQqqtL8oqA9T-YHD0Vg2n8mP1UiEVPPNM4mojB_9XsP6iCDfVbBpNgZew2h47bBMAplE_ctww7_my9kaBncXFcom1XjyBVQSA3ZMITuM8ZWJuEDU3tgpSPolkw0V3rqUiJIHZ79jDbXwlYVHOiCMxwMmvP2uqj8p3ZvUCdKxDVjm_BXiQcGdlry8TDWEN1Fw33UegdhG0mV8G_USY3hjvCNScIo9kQsB1qUDE5iY2zGEzvbEpJxE6ljFy6j2mULzWdEpx_sbW6-mEBfQGLtiagVNqqeyUtPhxuNSekxG8nUzSYYxM_8wTWXtO-9uHtoZKYEL_7epRprF1jH9L3XtMhdDZ8xS6WBynUh9RW9DssO2D828dWyGEixfkPhOHKe0NKvmCqBUlvVcqM_JQL4A-XxC4BeUxoNtxjVUaDO2PPuMMY4D_wjRl7RMGEbyqgYbb1w4Qj9f71nCGffwHd9OCsnoqlrKnPsszEubYQTblErkaHVDfNx2m00)

---

## 🙌 Credits

Built with ❤️ by **Idan**  
Feel free to open issues or PRs!

---

## 📁 Repository Structure

```text
.
├── UrlShortener.Application      # ASP.NET Core Web App
├── UrlShortener.Domain           # Entities & interfaces
├── UrlShortener.Infrastructure   # NHibernate, Redis, RabbitMQ
├── UrlShortener.Migrations       # FluentMigrator project
├── UrlShortenerBenchmark         # Performance benchmarking tool
├── docker-compose.yml            # Base Docker config
├── docker-compose.override.yml   # Adds migration runner
├── Dockerfile                    # Web application image
├── Dockerfile.migrations         # Migrations runner
├── start.sh                      # Startup script with health checks
└── .env                          # Configuration for containers
```
