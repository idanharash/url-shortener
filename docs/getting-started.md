# Getting started

The complete guide: clone → configure → run → verify. Everything runs in Docker; the .NET SDK is only needed for development and tests.
For the short version, see the [Quick start](../ReadMe.md#quick-start) in the README.

## 1. Prerequisites

| Tool | Version | Needed for | Check |
|---|---|---|---|
| [Git](https://git-scm.com/) | any | cloning | `git --version` |
| [Docker](https://docs.docker.com/get-docker/) with Compose v2 (Docker Desktop on Windows/macOS) | Compose 2.x | running the system | `docker compose version` |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0.100 or later (pinned in `global.json`) | building, tests, running outside Docker | `dotnet --version` |
| curl | any (built into Windows 10+, macOS, Linux) | trying the API | `curl --version` |

These ports must be free on your machine: **5000** (API), **5432** (PostgreSQL), **6379** (Redis), **5672** / **15672** (RabbitMQ) and **16686** / **4317** / **4318** (Jaeger). A locally installed PostgreSQL or Redis is the usual conflict.

## 2. Clone and configure

```bash
git clone https://github.com/idanharash/url-shortener.git
cd url-shortener
cp .env.example .env          # Windows cmd: copy .env.example .env
```

**Before the first start, open `.env` and set both passwords.** They are empty on purpose, so no password is ever committed. Choose your own values (`.env` is git-ignored, so they stay on your machine):

```dotenv
POSTGRES_PASSWORD=<choose-a-password>
RABBITMQ_PASSWORD=<choose-another-password>
```

That is the only required configuration. PostgreSQL and RabbitMQ are created with these values on the first start, and the API receives the same values automatically, so you never type them anywhere else when running in Docker. If you skip this step, Compose stops with `required variable POSTGRES_PASSWORD is missing a value`.

All variables in `.env`:

| Variable | Required | Default | Used for |
|---|---|---|---|
| `POSTGRES_PASSWORD` | **yes** | – | PostgreSQL superuser password; Compose refuses to start without it |
| `RABBITMQ_PASSWORD` | **yes** | – | RabbitMQ user password; Compose refuses to start without it |
| `POSTGRES_USER` | no | `postgres` | PostgreSQL user |
| `POSTGRES_DB` | no | `url_shortener` | Database name |
| `RABBITMQ_USERNAME` | no | `urlshortener` | RabbitMQ user |
| `ASPNETCORE_ENVIRONMENT` | no | `Development` in `.env.example` (`Production` if unset) | `Development` enables Swagger and prints trace spans to the API log |

`.env` is git-ignored. Compose passes these values to the containers as environment variables (see the `environment` sections in `docker-compose.yml`); every other setting comes from [`SharedConfig/appsettings.json`](../SharedConfig/appsettings.json) (see [Configuration](../ReadMe.md#configuration)). No other configuration is needed for Docker.

> PostgreSQL stores its password in the `pgdata` volume when the volume is first created. If you change `POSTGRES_PASSWORD` later, also reset the volume (`docker compose down -v`, which deletes the data).

## 3. Build and run

```bash
docker compose up -d --build
```

This one command uses `docker-compose.yml` plus `docker-compose.override.yml` (Compose merges them automatically) and:

1. starts **PostgreSQL**, **Redis**, **RabbitMQ** and **Jaeger**;
2. waits until PostgreSQL, Redis and RabbitMQ pass their health checks;
3. runs the **database migrations** once in the `migrate` container (FluentMigrator), which then exits;
4. starts the **API** (`web`) only after the migrations succeeded, on http://localhost:5000.

The first run downloads images and the .NET SDK image and builds the solution, so it takes a few minutes; later runs are fast.

**Migrations** need no separate step: they run on every `docker compose up` and only apply what's new (FluentMigrator records applied migrations in a `VersionInfo` table). To run them on their own: `docker compose run --rm migrate`.

## 4. Verify that everything is running

**Containers.** `migrate` should have exited with code 0; the others should be running (`db`, `redis` and `rabbitmq` also show `healthy`):

```bash
docker compose ps -a
docker compose logs migrate       # first run: applies InitialCreate; later runs: nothing new to apply
docker compose logs -f web        # API log; Ctrl+C to stop following
```

**API and its dependencies.** The health endpoint checks PostgreSQL, Redis and RabbitMQ:

```bash
curl http://localhost:5000/health
```

```json
{"status":"Healthy","checks":{"postgres":{"status":"Healthy","description":null},"redis":{"status":"Healthy","description":null},"rabbitmq":{"status":"Healthy","description":null}}}
```

`Degraded` means Redis or RabbitMQ is unreachable (redirects still work); `Unhealthy` (HTTP 503) means PostgreSQL is unreachable. `http://localhost:5000/health/live` only reports that the API process is up.

**Each dependency directly:**

| Service | Command | Expected |
|---|---|---|
| PostgreSQL | `docker compose exec db psql -U postgres -d url_shortener -c "\dt"` | Tables `short_urls` and `VersionInfo` |
| Redis | `docker compose exec redis redis-cli ping` | `PONG` |
| RabbitMQ | `docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers` | `click-events` with 1 consumer, and `click-events.dead-letter` |

(If you changed `POSTGRES_USER` or `POSTGRES_DB` in `.env`, use those values in the `psql` command.)

**Web UIs:**

| What | URL |
|---|---|
| Swagger (only when `ASPNETCORE_ENVIRONMENT=Development`) | http://localhost:5000/swagger |
| RabbitMQ management (log in with `RABBITMQ_USERNAME` / `RABBITMQ_PASSWORD`) | http://localhost:15672 |
| Jaeger traces (service `UrlShortener`) | http://localhost:16686 |
| Prometheus metrics | http://localhost:5000/metrics |

## 5. Try it

**Shorten a URL:**

```bash
curl -X POST http://localhost:5000/api/shorten -H "Content-Type: application/json" -d '{"url":"https://example.com"}'
# {"code":"aZ3kQ","shortUrl":"http://localhost:5000/aZ3kQ"}
```

On Windows `cmd`, quote the body like this: `-d "{\"url\":\"https://example.com\"}"`. In PowerShell, call `curl.exe` (plain `curl` is an alias for `Invoke-WebRequest` there).

**Follow the short link** (use the `code` you got back):

```bash
curl -i http://localhost:5000/aZ3kQ
# HTTP/1.1 302 Found
# Location: https://example.com
```

Opening `http://localhost:5000/<code>` in a browser redirects to the original site.

**Check the click count:**

```bash
curl http://localhost:5000/api/stats/aZ3kQ
# {"createdAt":"…","clickCount":1}
```

Clicks are counted asynchronously through RabbitMQ, so the count can take a moment to update.

**What failure looks like:**

| Request | Result |
|---|---|
| `{"url":"javascript:alert(1)"}` or `{"url":"not-a-url"}` | 400 with a validation message |
| `GET /doesnotexist` or `GET /api/stats/zzzzz` | 404 |
| More than 20 `POST /api/shorten` per minute from one IP | 429 with a `Retry-After` header |

**With Swagger:** open http://localhost:5000/swagger, expand `POST /api/shorten`, click *Try it out*, send `{ "url": "https://example.com" }`, then call `GET /api/stats/{code}` with the returned code. Test the redirect itself with curl or the browser: Swagger UI follows the 302 to the external site, which the browser usually blocks (CORS), so it shows an error even though the redirect works.

## 6. Stop, restart and reset

| Goal | Command |
|---|---|
| Stop everything, **keep data** | `docker compose down` |
| Start again (no rebuild) | `docker compose up -d` |
| Rebuild after code changes | `docker compose up -d --build` |
| Restart only the API | `docker compose restart web` |
| Stop and **delete all data** (database volume) | `docker compose down -v` |

Only PostgreSQL keeps data between restarts (volume `pgdata`). Redis and RabbitMQ have no volumes: `docker compose down` empties the cache, which refills from PostgreSQL, and drops click events still waiting in the queue.

## Running the API outside Docker (development)

Run the dependencies in Docker and the API with `dotnet run`, for debugging:

```bash
# 1. Dependencies only (PostgreSQL, Redis, RabbitMQ, Jaeger); waits until they are healthy
docker compose up -d --wait db redis rabbitmq jaeger          # same as: bash start-dependencies.sh

# 2. Passwords for the app, the same values as in .env (stored with dotnet user-secrets, outside the repo;
#    the API and the migrations project share them)
dotnet user-secrets set "Postgres:Password" "<POSTGRES_PASSWORD>" --project UrlShortener.Application
dotnet user-secrets set "RabbitMQ:Password" "<RABBITMQ_PASSWORD>" --project UrlShortener.Application

# 3. Migrations, then the API on http://localhost:5000 (Development: Swagger enabled)
dotnet run --project UrlShortener.Migrations
dotnet run --project UrlShortener.Application
```

Stop the Docker `web` container first if it is running (`docker compose stop web`); both use port 5000. The other settings in `SharedConfig/appsettings.json` already point to `localhost` and the default user names from `.env.example`; if you changed `POSTGRES_USER`, `POSTGRES_DB` or `RABBITMQ_USERNAME`, set `Postgres:Username`, `Postgres:Database` or `RabbitMQ:Username` the same way.

**Tests:** `dotnet test UrlShortener.UnitTests` needs only the SDK; `dotnet test` also runs the integration tests, which start their own PostgreSQL, Redis and RabbitMQ containers and need Docker running (they don't use or touch the Compose stack).

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `error during connect … dockerDesktopLinuxEngine: The system cannot find the file specified` (Windows), or `Cannot connect to the Docker daemon` | Docker isn't running. Start Docker Desktop and wait for "Engine running"; `docker version` must show a **Server** section. |
| `required variable POSTGRES_PASSWORD is missing a value` (or `RABBITMQ_PASSWORD`) | `.env` is missing or the password is empty: see [step 2](#2-clone-and-configure). |
| `Bind for 0.0.0.0:5432 failed: port is already allocated` (or another port) | Another program uses the port; stop it (often a local PostgreSQL or Redis). |
| `container url-shortener-db-1 is unhealthy`; `docker compose logs db` shows `database files are incompatible with server` | An existing `pgdata` volume was created by a different PostgreSQL version (e.g. by an older version of this project). Reset it: `docker compose down -v` (deletes the local data). |
| `migrate` exits with an error, `web` doesn't start | `docker compose logs migrate`. A password error after changing `POSTGRES_PASSWORD` means the old volume still has the old one: `docker compose down -v`. |
| API exits at startup with `Missing or invalid configuration: …` | A required setting is empty; the message names it. Outside Docker this is usually a missing `dotnet user-secrets` value. |
| API log shows `PRECONDITION_FAILED … inequivalent arg 'x-dead-letter-exchange'` | A RabbitMQ container from an older version of this project still has the old queue: `docker compose up -d --force-recreate rabbitmq`. |
| `/health` reports `Degraded` | Redis or RabbitMQ is down; the description says which. Redirects still work. Check `docker compose ps`. |
