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

## 🔐 Security Considerations

This project includes basic security measures to prevent abuse:

- ✅ **Rate Limiting:** Enforced via Redis for `POST /api/shorten`, using a fixed-window strategy (20 requests per IP per minute).
- ✅ **Environment-based config:** No hardcoded secrets in source code.
- ❌ Authentication is not implemented (by design – open public demo).

> 📌 Redirect and stats endpoints are not rate-limited to ensure high availability and user experience.

---

## 🐳 Run with Docker (Recommended)

The project includes a full Docker setup with:

- PostgreSQL, Redis, RabbitMQ
- FluentMigrator runner
- Health-check based startup script

### 📦 Start everything (including migrations)

```bash
bash start-dependencies.sh   # Start PostgreSQL, Redis, RabbitMQ, Jaeger
bash start-app.sh            # Run migrations and start the web server
```

Once started:

- Web App: [http://localhost:5000](http://localhost:5000)
- Swagger: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- Health: [http://localhost:5000/health](http://localhost:5000/health)
- RabbitMQ UI: [http://localhost:15672](http://localhost:15672)  
  _(user/pass from `.env`)_
- PgAdmin (optional): [http://localhost:5050](http://localhost:5050)

### 📬 Example Usage

```bash
curl -X POST http://localhost:5000/api/shorten \
     -H "Content-Type: application/json" \
     -d '{ "url": "https://www.google.com" }'

# Output:
# { "code": "abc123", "shortUrl": "http://localhost:5000/abc123" }
```

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

## ⚡ Performance Results (Benchmark)

| Scenario                 | Avg Latency | P95   | Notes                |
| ------------------------ | ----------- | ----- | -------------------- |
| GET /{code} (cache hit)  | 3 ms        | 6 ms  | Redis only           |
| GET /{code} (cache miss) | 18 ms       | 29 ms | Includes DB fallback |
| POST /api/shorten        | 11 ms       | 20 ms | Includes cache + DB  |

## 📈 Observability & Monitoring

### Metrics (Prometheus-ready)

- Total requests per endpoint/method
- Latency per path (including P95)
- Cache hit/miss tracking

### Tracing (Jaeger-ready)

- Full request path: Controller → Cache → DB → Queue
- Distributed trace IDs across spans
- Error tagging and structured logging

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

👉 [View Live Diagram (PlantUML)](https://www.plantuml.com/plantuml/png/PLJ1RjD04BtxAuOS895e8mGI4WVKrasQGD8wSH07uB2n1sSLUzVChfj65V_EUBF5TdpRp7lpPiRp9bV62h9LaGUlx0uBX38NKWLcBrKfI1IGwABK2fMDxJ5784oiK5cEBKefaht8y9mG-3WCmboo0CGonG4y1m0_RaWV3D8l40RyE_ZRohYEvh0GHoGONiVHvVra0w4c7BdqfYSOVACyrCgIpdEaGItN_EmnOwH7cQ2ZDJ-xj52dU8SAILXDxGGFTDcn5TQqMIxG6MMkVfCmbgh4LkJdJuKLMs4OZRIn6M6yMhZqsvkk_lrCRf6Ki0Z3UOp6IAsc8h6QZYENS3_ZX3yLLgor5jkjjCkL0vQhhj8QKsbO91J93dt79mc7T5fUoZJDyI1ykpsPjR2ua8MqKcKDzwSQJkKJfl1TgbGVe4I2UUHhl6ISxVc67ndfIgNWUpAzakoUPB-gH-JLcgf0uaepRIocdJaysYqGLsMfoS9hM6ebhIPkkLTXXYAtkt27oTuCkb14ka2xmyfmKPEGAD70c4Ca4tEgsr2wgG-RHGIJfxgyQ_cXHBN17Ela8pXFnxTVJnerjxwBBm8p91Yx08IrZHEhJtVYC3VT6_0NDStr8V3t0nSNdzlV6Jx1RBE987g8Dp0MfHoRNTsA2jeaJcfVZ1VhnXkjkmbBFISwHUwY1JExVL3Dqo7EConnXtR2ASThSrJ9i5OPdL7P_yoRYZpVyWB1wZemV9I2VTxZ-eFa17-l20eFKEkUKvShPiMeKd1cWiaZAXi477P2tZZCQSCluQQnkKe82yOyBsJZsjw7S7y1zHYQ_3iI6VBMMXOCsfQ3oykwG2_cBxCNvjCE6jFrLnKxc_vtALTsle2NasqReBEWs1B3pPJZshwINEzTzpRUTeBTB-1GbclemxWKoho5fR-Rm36OwatDP1V9rb7ePM7uuVsxZwFW2bLQ5Va_)

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
