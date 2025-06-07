````markdown
# 🔗 URL Shortening Service

A high-performance, reliable URL shortener built in ASP.NET Core with PostgreSQL, Redis, and RabbitMQ.

---

## 🚀 Features

- ✅ Shorten long URLs via REST API
- ✅ Redirect short codes (with fast cache layer)
- ✅ Track click counts asynchronously via RabbitMQ
- ✅ Metrics middleware for observability
- ✅ Resilience patterns (retry, circuit breaker)
- ✅ Health checks for DB, Redis, RabbitMQ
- ✅ Performance-tested (P95 < 20ms)

---

## 🏗️ Tech Stack

| Layer         | Technology                      |
| ------------- | ------------------------------- |
| Web Framework | ASP.NET Core 8                  |
| DB Access     | NHibernate + PostgreSQL         |
| Cache         | Redis                           |
| Queue         | RabbitMQ                        |
| Metrics       | Custom Middleware               |
| Retry/Faults  | Microsoft.Extensions.Resilience |
| Tests         | xUnit + Benchmark Tool          |

---
### 📊 Database Schema (PostgreSQL)

The application uses a single core table to persist short URL mappings and track clicks:

#### 🗂️ `short_urls`

| Column         | Type        | Description                           |
| -------------- | ----------- | ------------------------------------- |
| `code`         | `text`      | Primary key – the unique short code   |
| `original_url` | `text`      | The original long URL                 |
| `click_count`  | `integer`   | Total number of clicks (write-behind) |
| `created_at`   | `timestamp` | Timestamp of creation (UTC)           |

#### 🧾 SQL Definition

```sql
CREATE TABLE short_urls (
    code TEXT PRIMARY KEY,
    original_url TEXT NOT NULL,
    click_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL
);
```

#### 🧠 Design Notes

* **Primary key** on `code` ensures uniqueness and fast lookups.
* `click_count` is updated **asynchronously** by the RabbitMQ consumer.
* `created_at` supports future features like expiration or reporting.
---

If you'd like, I can also prepare:

* A `schema.sql` file in the repo
* A PlantUML-based ER diagram for documentation
* A `seed.sql` to pre-populate the database with test data

Would you like me to add those as well?


## ⚙️ Prerequisites

Before running the project locally, make sure the following dependencies are installed and configured:

### ✅ Required Software

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (default connection: `localhost:5432`)
- [Redis](https://redis.io/download/) (default connection: `localhost:6379`)
- [RabbitMQ](https://www.rabbitmq.com/download.html) (default connection: `localhost:5672`)

You can run Redis & RabbitMQ via Docker:

```bash
docker run -d --name redis -p 6379:6379 redis
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

---

### ⚙️ Configuration File

Make sure to create or update `appsettings.json` in the root directory with the following:

```json
{
  "ConnectionStrings": {
    "Postgres": "Server=localhost;Port=5432;Database=url_shortener;User Id=postgres;Password=yourpassword;"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "QueueName": "click-events"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## 📦 Running the Project

```bash
dotnet restore
dotnet build -c Release
dotnet run --project UrlShortener.Application
```

Visit Swagger UI at:
📎 [http://localhost:7083/swagger](http://localhost:7083/swagger)

---

## 📈 Metrics

- ✅ Built-in metrics middleware exposes:

  - Request counts
  - Error rates
  - Cache hits/misses

- 🛠️ Future: expose via `/metrics` endpoint (Prometheus format)

---

## 🩺 Health Checks

Health checks are available at:
📎 [http://localhost:7083/health](http://localhost:7083/health)

Includes:

- ✅ PostgreSQL
- ✅ Redis
- ✅ RabbitMQ

---

## 🧪 Testing

Run performance test (1000 requests, parallel):

```bash
cd UrlShortenerBenchmark
dotnet run -- http://localhost:7083/{code} 1000
```

Run unit tests:

```bash
dotnet test
```

---

## 🧩 Architecture Diagram

### 🔗 View Online (via PlantUML)

You can view the architecture diagram rendered live here:
👉 [View Architecture Diagram](https://www.plantuml.com/plantuml/png/TLBDKjim4BxhAROvjE40lVVG0LmWcS4qmVHKUb3aRQA9BBchDPaCmxjtxMg8VwOtQRyVxUhRoyYQkAcS-i0xh11gfwrNSMzzhKbNO49L1u-U2puw14B3scyTzYYrDdkznJ51fJhCCcV500fRuWnU5S3FQmg7YFGBT8HqLcyznTLg2VVwY0Jpgs9ryN3p15aWFElafvvWzeDS5ZnJS0vfrjgThXDcWVHY3NQqqtL8oqA9T-YHD0Vg2n8mP1UiEVPPNM4mojB_9XsP6iCDfVbBpNgZew2h47bBMAplE_ctww7_my9kaBncXFcom1XjyBVQSA3ZMITuM8ZWJuEDU3tgpSPolkw0V3rqUiJIHZ79jDbXwlYVHOiCMxwMmvP2uqj8p3ZvUCdKxDVjm_BXiQcGdlry8TDWEN1Fw33UegdhG0mV8G_USY3hjvCNScIo9kQsB1qUDE5iY2zGEzvbEpJxE6ljFy6j2mULzWdEpx_sbW6-mEBfQGLtiagVNqqeyUtPhxuNSekxG8nUzSYYxM_8wTWXtO-9uHtoZKYEL_7epRprF1jH9L3XtMhdDZ8xS6WBynUh9RW9DssO2D828dWyGEixfkPhOHKe0NKvmCqBUlvVcqM_JQL4A-XxC4BeUxoNtxjVUaDO2PPuMMY4D_wjRl7RMGEbyqgYbb1w4Qj9f71nCGffwHd9OCsnoqlrKnPsszEubYQTblErkaHVDfNx2m00)
(Rendered via PlantUML Online Server)

---

### 🖼️ Local Diagram (Optional)

If you cloned this repository and generated a PNG locally (e.g. `Docs/architecture.png`), you can view it below:

```markdown
![Architecture Diagram](docs/architecture.png)
```

To generate the diagram locally, run:

```bash
plantuml architecture.puml -o docs/
```
---

## 🙌 Credits

Crafted by Idan
Feedback and PRs welcome!
````
