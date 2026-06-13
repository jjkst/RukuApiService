# RukuServiceApi

RESTful API backend to manage services, availability, scheduling, authentication, file uploads, and system monitoring.

## Table of Contents

- [Architecture](#architecture)
- [API Routes](#api-routes)
- [Running Locally](#running-locally)
- [Running with Docker](#running-with-docker)
- [Testing](#testing)
- [Database Management](#database-management)
- [Security](#security)
- [Monitoring & Health Checks](#monitoring--health-checks)

## Architecture

### Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8.0 |
| ORM | Entity Framework Core 8.0 + Pomelo MySQL |
| Database | MariaDB 10.11 (MySQL-compatible) |
| Auth | JWT Bearer with role-based policies |
| Validation | FluentValidation |
| Logging | Serilog (console + file, JSON structured) |
| Docs | Swagger/OpenAPI (dev only) |
| Reverse Proxy | Nginx (serves Angular static files + proxies API) |
| Containerization | Docker multi-stage builds + docker-compose |

### Project Structure

```
RukuApiServices/
├── RukuServiceApi/                     # Main API project
│   ├── Controllers/                    # API controllers
│   │   ├── BaseController.cs           # Generic CRUD (GetAll, GetById, Create, Update, Delete)
│   │   ├── AuthController.cs           # Login & registration
│   │   ├── ServicesController.cs       # Service management (authenticated users)
│   │   ├── AvailabilitiesController.cs # Availability CRUD + date/timeslot queries
│   │   ├── SchedulesController.cs      # Schedule CRUD (owner-filtered)
│   │   ├── UsersController.cs          # User management + role updates
│   │   ├── EmailController.cs          # Contact form + email settings
│   │   ├── UploadImageController.cs    # File uploads (10MB max)
│   │   └── MonitoringController.cs     # System info, metrics, logs
│   ├── Models/                         # Entities + DTOs
│   │   ├── User.cs, Service.cs, Availability.cs, Schedule.cs, Contact.cs
│   │   ├── ValidationModels.cs         # Request/response DTOs
│   │   ├── AuthorizationPolicies.cs    # Policy constants
│   │   ├── ErrorResponse.cs            # Consistent error format
│   │   └── JwtSettings.cs, EmailSettings.cs, FileUploadSettings.cs
│   ├── Services/                       # Business logic
│   │   ├── AuthService.cs              # JWT token generation & validation
│   │   ├── FileUploadService.cs        # File handling with security checks
│   │   └── DatabaseSeeder.cs           # Dev data seeding
│   ├── Middleware/                      # HTTP pipeline middleware
│   │   ├── SecurityHeadersMiddleware.cs
│   │   ├── RequestLoggingMiddleware.cs
│   │   ├── GlobalExceptionMiddleware.cs
│   │   └── ValidationMiddleware.cs
│   ├── HealthChecks/
│   │   └── CustomHealthChecks.cs       # DB, email, filesystem, memory checks
│   ├── Validators/
│   │   └── Validators.cs               # FluentValidation rules
│   ├── Context/
│   │   └── ApplicationDbContext.cs      # EF Core DbContext
│   └── Program.cs                       # App startup & configuration
├── RukuServiceApi.IntegrationTests/     # Integration tests (MSTest, hits live API)
├── RukuServiceApi.UnitTests/            # Unit tests (MSTest, mocked dependencies)
├── MigrateTool/                         # CLI migration tool
├── Dockerfile                           # Multi-stage: build -> migrate -> api
├── docker-compose.local.yml             # Local Docker test harness for API + MariaDB
├── env.template                         # Local env vars template
└── (see jk-portfolio-deploy for full-stack Docker Compose, nginx, mariadb config)
```

### Request Flow

```
Client Request
  → Nginx (reverse proxy, rate limiting: 10r/s with burst=20)
    → SecurityHeadersMiddleware (X-Frame-Options, HSTS, etc.)
      → RequestLoggingMiddleware (correlation IDs, duration tracking)
        → GlobalExceptionMiddleware (consistent ErrorResponse format)
          → ValidationMiddleware (FluentValidation)
            → Authentication (JWT Bearer)
              → Authorization (role-based policies)
                → Controller Action
                  → Entity Framework Core → MariaDB
```

### Authorization Roles

| Policy | Roles | Used By |
|--------|-------|---------|
| `AdminOnly` | Admin | Monitoring, email settings, user role management |
| `AdminOrOwner` | Admin, Owner | File uploads |
| `AuthenticatedUser` | Any authenticated user | Services CRUD, availabilities CRUD, schedules CRUD (owner-filtered), email send, user read |

## API Routes

> **Base URL:** `http://localhost:5002` (local development)
>
> All authenticated endpoints require a JWT token in the `Authorization` header:
> ```
> Authorization: Bearer <token>
> ```

### Pagination

All list endpoints (`GET /api/services`, `GET /api/availabilities`, `GET /api/schedules`, `GET /api/users`) accept optional pagination query parameters:

| Param | Default | Max | Notes |
|-------|---------|-----|-------|
| `skip` | `0`     | —   | Negative values are clamped to `0` |
| `take` | `100`   | `500` | Out-of-range values fall back to the default |

Results are ordered by `Id` ascending so pages are stable across requests.

```bash
curl "http://localhost:5002/api/services?skip=100&take=50" \
  -H "Authorization: Bearer <token>"
```

---

### Authentication (`/api/auth`) — Public

#### POST `/api/auth/login`

Login with email and uid to receive a JWT token.

```bash
curl -X POST http://localhost:5002/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@rukuit.com",
    "uid": "admin-uid-12345"
  }'
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "user": {
    "id": 1,
    "email": "admin@rukuit.com",
    "displayName": "Admin User",
    "role": "Admin",
    "emailVerified": true
  }
}
```

| Status | Meaning |
|--------|---------|
| 200 | Success — token + user returned |
| 400 | Email and UID are required |
| 401 | Invalid credentials (user not found or UID mismatch) |

#### POST `/api/auth/register`

Register a new user account.

```bash
curl -X POST http://localhost:5002/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@example.com",
    "uid": "firebase-uid-abc123",
    "displayName": "Jane Doe",
    "emailVerified": true,
    "provider": "Google"
  }'
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "user": {
    "id": 3,
    "email": "newuser@example.com",
    "displayName": "Jane Doe",
    "role": "Subscriber",
    "emailVerified": true
  }
}
```

| Status | Meaning |
|--------|---------|
| 200 | Success — new user created with Subscriber role |
| 409 | User already exists (duplicate email or UID) |

**Provider values:** `Google`, `Facebook`, `Apple`

---

### Services (`/api/services`) — Authenticated users

All service endpoints now require any authenticated user (`AuthenticatedUser`).

#### GET `/api/services`

```bash
curl http://localhost:5002/api/services \
  -H "Authorization: Bearer <token>"
```

**Response (200):** Array of service objects.

#### GET `/api/services/{id}`

```bash
curl http://localhost:5002/api/services/1 \
  -H "Authorization: Bearer <token>"
```

**Response (200):** Single service object. **404** if not found.

#### POST `/api/services/create`

Create a new service with validation.

```bash
curl -X POST http://localhost:5002/api/services/create \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "UI Automation Training",
    "fileName": "ui-auto.png",
    "description": "Comprehensive training on UI test automation frameworks and best practices.",
    "features": ["Selenium WebDriver", "Cypress", "Playwright"],
    "pricingPlans": [
      {
        "name": "Foundation",
        "initialSetupFee": "$200.00",
        "monthlySubscription": "$0",
        "features": ["2-day workshop", "Course materials"]
      },
      {
        "name": "Enterprise",
        "initialSetupFee": "$1500.00",
        "monthlySubscription": "$100.00",
        "features": ["On-site training", "Ongoing mentorship", "Custom curriculum"]
      }
    ]
  }'
```

**Response (201):** Created service object with assigned `id`.

| Status | Meaning | Error code |
|--------|---------|------------|
| 201 | Created | — |
| 409 | A service with the same `title` already exists | `DUPLICATE_SERVICE_TITLE` |
| 409 | A service with the same `description` already exists | `DUPLICATE_SERVICE_DESCRIPTION` |

**Validation rules:**
- `title`: required, 3–100 chars, alphanumeric/spaces/hyphens/underscores only
- `description`: required, 10–1000 chars
- `fileName`: optional, max 255 chars, alphanumeric/dots/hyphens/underscores only
- `features[]`: each max 200 chars
- `pricingPlans[].name`: required, 3–50 chars
- `pricingPlans[].initialSetupFee`: required, format `$123.00` or `123.00`
- `pricingPlans[].monthlySubscription`: required, same format

#### PUT `/api/services/update/{id}`

Update an existing service.

```bash
curl -X PUT http://localhost:5002/api/services/update/1 \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Build Your Website Updated",
    "fileName": "web-dev-v2.png",
    "description": "Updated full-stack web development service with modern frameworks.",
    "features": ["React", "Angular", "Vue"],
    "pricingPlans": [
      {
        "name": "Standard",
        "initialSetupFee": "$750.00",
        "monthlySubscription": "$75.00",
        "features": ["10 pages", "Priority support"]
      }
    ]
  }'
```

**Response (200):** Updated service object.

| Status | Meaning | Error code |
|--------|---------|------------|
| 200 | Updated | — |
| 404 | Service not found | — |
| 409 | Another service already has this `title` | `DUPLICATE_SERVICE_TITLE` |
| 409 | Another service already has this `description` | `DUPLICATE_SERVICE_DESCRIPTION` |

#### DELETE `/api/services/{id}`

```bash
curl -X DELETE http://localhost:5002/api/services/1 \
  -H "Authorization: Bearer <token>"
```

**Response:** 204 No Content. **404** if not found.

---

### Availabilities (`/api/availabilities`) — Public Read, Auth for Write

#### GET `/api/availabilities`

```bash
curl http://localhost:5002/api/availabilities
```

**Response (200):**
```json
[
  {
    "id": 1,
    "startDate": "2026-04-01T00:00:00",
    "endDate": "2026-04-30T00:00:00",
    "services": ["Build Your Website", "UI Automation Training"],
    "timeslots": ["09:00 AM - 10:00 AM", "10:00 AM - 11:00 AM", "02:00 PM - 03:00 PM"]
  }
]
```

#### GET `/api/availabilities/{id}`

```bash
curl http://localhost:5002/api/availabilities/1
```

**Response (200):** Single availability object. **404** if not found.

#### POST `/api/availabilities`

Create a new availability window. Dates must be in the future and cannot overlap existing availabilities.

```bash
curl -X POST http://localhost:5002/api/availabilities \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2026-05-01",
    "endDate": "2026-05-31",
    "services": ["Build Your Website", "Website Maintenance"],
    "timeslots": ["09:00 AM - 10:00 AM", "11:00 AM - 12:00 PM"]
  }'
```

**Response (201):** Created availability object with `id`.

| Status | Meaning |
|--------|---------|
| 201 | Created |
| 400 | StartDate must be in the future / EndDate must be after StartDate |
| 409 | Date range overlaps an existing availability |

#### PUT `/api/availabilities/{id}`

```bash
curl -X PUT http://localhost:5002/api/availabilities/1 \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2026-05-01",
    "endDate": "2026-06-15",
    "services": ["Build Your Website"],
    "timeslots": ["09:00 AM - 10:00 AM"]
  }'
```

**Response (200):** Updated availability. Same validation as POST.

#### DELETE `/api/availabilities/{id}`

```bash
curl -X DELETE http://localhost:5002/api/availabilities/1
```

**Response:** 204 No Content. **404** if not found.

#### GET `/api/availabilities/dates`

Get all unique dates with availability (today and future only).

```bash
curl http://localhost:5002/api/availabilities/dates
```

**Response (200):**
```json
["2026-04-01", "2026-04-02", "2026-04-03"]
```

#### GET `/api/availabilities/services?date={date}`

Get services available on a specific date.

```bash
curl "http://localhost:5002/api/availabilities/services?date=2026-04-15"
```

**Response (200):**
```json
["Build Your Website", "UI Automation Training"]
```

#### POST `/api/availabilities/timeslots`

Get available timeslots for a specific date and set of services.

```bash
curl -X POST http://localhost:5002/api/availabilities/timeslots \
  -H "Content-Type: application/json" \
  -d '{
    "date": "2026-04-15",
    "services": ["Build Your Website"]
  }'
```

**Response (200):**
```json
["09:00 AM - 10:00 AM", "10:00 AM - 11:00 AM", "02:00 PM - 03:00 PM"]
```

| Status | Meaning |
|--------|---------|
| 200 | Success |
| 404 | No availabilities for the given date or services |

---

### Schedules (`/api/schedules`) — Public

#### GET `/api/schedules`

```bash
curl http://localhost:5002/api/schedules
```

**Response (200):**
```json
[
  {
    "id": 1,
    "contactName": "John Doe",
    "selectedDate": "2026-04-15T00:00:00",
    "services": ["Build Your Website"],
    "timeslots": ["09:00 AM - 10:00 AM"],
    "note": "Interested in React-based solution",
    "uid": "firebase-uid-abc123"
  }
]
```

#### GET `/api/schedules/{id}`

```bash
curl http://localhost:5002/api/schedules/1
```

**Response (200):** Single schedule object. **404** if not found.

#### POST `/api/schedules`

Book a new appointment.

```bash
curl -X POST http://localhost:5002/api/schedules \
  -H "Content-Type: application/json" \
  -d '{
    "contactName": "Jane Smith",
    "selectedDate": "2026-04-20",
    "services": ["Build Your Website", "Website Maintenance"],
    "timeslots": ["10:00 AM - 11:00 AM"],
    "note": "Need e-commerce features",
    "uid": "firebase-uid-xyz789"
  }'
```

**Response (201):** Created schedule object with `id`.

#### PUT `/api/schedules/{id}`

```bash
curl -X PUT http://localhost:5002/api/schedules/1 \
  -H "Content-Type: application/json" \
  -d '{
    "contactName": "Jane Smith",
    "selectedDate": "2026-04-25",
    "services": ["Build Your Website"],
    "timeslots": ["02:00 PM - 03:00 PM"],
    "note": "Rescheduled to later date"
  }'
```

**Response (200):** Updated schedule object. **404** if not found.

#### DELETE `/api/schedules/{id}`

```bash
curl -X DELETE http://localhost:5002/api/schedules/1
```

**Response:** 204 No Content. **404** if not found.

---

### Users (`/api/users`) — Authenticated

All endpoints require a valid JWT token.

#### GET `/api/users`

```bash
curl http://localhost:5002/api/users \
  -H "Authorization: Bearer <token>"
```

**Response (200):**
```json
[
  {
    "id": 1,
    "displayName": "Admin User",
    "email": "admin@rukuit.com",
    "emailVerified": true,
    "uid": "admin-uid-12345",
    "role": "Admin",
    "provider": "Google"
  }
]
```

#### GET `/api/users/{id}`

```bash
curl http://localhost:5002/api/users/1 \
  -H "Authorization: Bearer <token>"
```

**Response (200):** Single user object. **404** if not found.

#### PUT `/api/users/{id}/role` — AdminOnly

Update a user's role. Only Admin users can perform this action.

```bash
curl -X PUT http://localhost:5002/api/users/2/role \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d '"Owner"'
```

**Response (200):** Updated user object with new role.

| Status | Meaning |
|--------|---------|
| 200 | Role updated |
| 400 | Invalid role value |
| 404 | User not found |

**Valid roles:** `Admin`, `Owner`, `Subscriber` (case-insensitive)

#### DELETE `/api/users/{id}`

```bash
curl -X DELETE http://localhost:5002/api/users/3 \
  -H "Authorization: Bearer <token>"
```

**Response:** 204 No Content. **404** if not found.

---

### Email (`/api/email`) — Authenticated

#### POST `/api/email/send`

Send a contact form email.

```bash
curl -X POST http://localhost:5002/api/email/send \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1234567890",
    "questions": "I am interested in your web development services. Can you provide a quote for an e-commerce site?"
  }'
```

**Response (200):**
```json
{ "message": "Email sent successfully" }
```

| Status | Meaning |
|--------|---------|
| 200 | Email sent |
| 400 | Failed to send (SMTP error) or validation failure |

**Validation rules:**
- `firstName`: required, 2–50 chars, letters/spaces only
- `lastName`: required, 2–50 chars, letters/spaces only
- `email`: required, valid email format
- `phoneNumber`: optional, international format (e.g. `+1234567890`)
- `questions`: required, 10–1000 chars

#### GET `/api/email/settings` — AdminOnly

Get current SMTP configuration (credentials excluded).

```bash
curl http://localhost:5002/api/email/settings \
  -H "Authorization: Bearer <admin-token>"
```

**Response (200):**
```json
{
  "smtpServer": "smtp.gmail.com",
  "smtpPort": 587,
  "enableSsl": true
}
```

---

### File Upload (`/api/uploadimage`) — Admin/Owner

#### POST `/api/uploadimage`

Upload an image or PDF file using multipart form data.

```bash
curl -X POST http://localhost:5002/api/uploadimage \
  -H "Authorization: Bearer <token>" \
  -F "file=@/path/to/image.jpg" \
  -F "folder=services"
```

**Response (200):**
```json
{
  "filePath": "uploads/services/1710345600_aB3xYz.jpg",
  "fileName": "1710345600_aB3xYz.jpg",
  "size": 245678
}
```

| Status | Meaning |
|--------|---------|
| 200 | File uploaded |
| 400 | Validation failed (size, type, suspicious filename) |

**Constraints:**
- Max file size: 10 MB
- Allowed extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.pdf`
- Allowed MIME types: `image/jpeg`, `image/png`, `image/gif`, `application/pdf`
- Filenames with path traversal (`..`, `/`, `\`) or script keywords are rejected

---

### Monitoring (`/api/monitoring`) — AdminOnly

#### GET `/api/monitoring/system-info`

```bash
curl http://localhost:5002/api/monitoring/system-info \
  -H "Authorization: Bearer <admin-token>"
```

**Response (200):**
```json
{
  "application": {
    "name": "RukuServiceApi",
    "version": "1.0.0",
    "startTime": "2026-03-13T08:00:00Z",
    "uptime": "04:30:15"
  },
  "system": {
    "machineName": "PROD-SERVER",
    "osVersion": "Unix 5.15.0",
    "processorCount": 4,
    "workingSet": 104857600,
    "privateMemorySize": 134217728,
    "virtualMemorySize": 268435456
  },
  "environment": {
    "environmentName": "Production",
    "frameworkVersion": ".NET 8.0.0",
    "is64BitProcess": true,
    "is64BitOperatingSystem": true
  },
  "timestamp": "2026-03-13T12:30:15Z"
}
```

#### GET `/api/monitoring/performance`

```bash
curl http://localhost:5002/api/monitoring/performance \
  -H "Authorization: Bearer <admin-token>"
```

**Response (200):**
```json
{
  "memory": {
    "workingSetMB": 100,
    "privateMemoryMB": 128,
    "virtualMemoryMB": 256,
    "gcMemoryMB": 45,
    "gen0Collections": 12,
    "gen1Collections": 5,
    "gen2Collections": 1
  },
  "cpu": {
    "totalProcessorTime": 15234.5,
    "userProcessorTime": 12000.3,
    "privilegedProcessorTime": 3234.2
  },
  "threads": {
    "threadCount": 24,
    "handleCount": 150
  },
  "timestamp": "2026-03-13T12:30:15Z"
}
```

#### GET `/api/monitoring/logs?count={n}`

Get the most recent log lines. Default count is 50.

```bash
curl "http://localhost:5002/api/monitoring/logs?count=20" \
  -H "Authorization: Bearer <admin-token>"
```

**Response (200):**
```json
{
  "logFile": "log-20260313.txt",
  "lineCount": 20,
  "logs": [
    "2026-03-13 12:30:00 [INF] Request started: GET /api/services ...",
    "2026-03-13 12:30:01 [INF] Request completed: GET /api/services - Status: 200 ..."
  ],
  "timestamp": "2026-03-13T12:30:15Z"
}
```

| Status | Meaning |
|--------|---------|
| 200 | Success |
| 404 | No log files found |

#### POST `/api/monitoring/gc`

Force garbage collection and report freed memory.

```bash
curl -X POST http://localhost:5002/api/monitoring/gc \
  -H "Authorization: Bearer <admin-token>"
```

**Response (200):**
```json
{
  "beforeMemoryMB": 150,
  "afterMemoryMB": 95,
  "freedMemoryMB": 55,
  "timestamp": "2026-03-13T12:30:15Z"
}
```

---

### Health Checks — No Auth Required

#### GET `/health`

Full health check report covering database, email, filesystem, memory, and EF context.

```bash
curl http://localhost:5002/health
```

**Response (200):**
```json
{
  "status": "Healthy",
  "results": {
    "database": { "status": "Healthy", "description": "Database is accessible" },
    "email_service": { "status": "Healthy", "description": "Email service is configured" },
    "file_system": { "status": "Healthy", "description": "File system is accessible" },
    "memory": { "status": "Healthy", "description": "Memory usage is normal: 95MB" },
    "db_context": { "status": "Healthy" }
  }
}
```

#### GET `/health/ready`

Readiness probe for orchestrators (Kubernetes, Docker health checks).

```bash
curl http://localhost:5002/health/ready
```

**Response:** 200 if ready, 503 if not.

#### GET `/health/live`

Liveness probe — always returns healthy if the process is running.

```bash
curl http://localhost:5002/health/live
```

**Response:** 200 if alive.

---

### Error Response Format

All errors return a consistent JSON structure:

```json
{
  "message": "Description of the error",
  "details": "Stack trace or additional info (development only)",
  "statusCode": 400,
  "correlationId": "0HN4ABCDEF:00000001",
  "timestamp": "2026-03-13T12:30:15Z",
  "path": "/api/services/create"
}
```

| Exception Type | HTTP Status |
|----------------|-------------|
| `UnauthorizedAccessException` | 401 |
| `ArgumentException` | 400 |
| `InvalidOperationException` | 400 |
| `FileNotFoundException` | 404 |
| `TimeoutException` | 408 |
| All others | 500 |

## Running Locally

### Prerequisites

- .NET 8.0 SDK
- MySQL 8.0+ or MariaDB 10.11+

### Setup

1. **Clone and restore**
   ```bash
   git clone https://github.com/jjkst/RukuServiceApi.git
   cd RukuServiceApi
   dotnet restore
   ```

2. **Configure environment variables**
   ```bash
   cp env.template .env.local
   ```
   Edit `.env.local` with your values:
   - `CONNECTIONSTRING` - MySQL/MariaDB connection string
   - `JWT_SECRET_KEY` - Minimum 32 characters
   - `SMTP_*` - Email server settings
   - `ADMIN_EMAIL`, `ADMIN_UID` - Admin user credentials (seeded on startup)
   - `ALLOWED_HOSTS` - Semicolon-separated (`;`), not commas

3. **Create the database**
   ```sql
   CREATE DATABASE RukuITServicesTest CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```

4. **Run migrations**
   ```bash
   ./migrate update Development
   # Or reset from scratch:
   ./migrate reset Development
   ```

5. **Run the API**
   ```bash
   dotnet run --project RukuServiceApi
   ```

6. **Access**
   - API: http://localhost:5002
   - Swagger UI: http://localhost:5002/swagger (dev only)
   - Health: http://localhost:5002/health

In development, the database is automatically seeded with an admin user (`admin@rukuit.com`), owner user, and sample services.

## Running with Docker

The project uses a multi-stage Dockerfile with three targets:
- **build** - Compiles the API and creates an EF Core migration bundle
- **migrate** - Runs pending migrations, then exits
- **api** - Lightweight Alpine-based runtime (~100MB)

### Full-Stack Deployment

For deploying the complete stack (API + Angular frontend + MariaDB + Nginx + SSL), see the **[jk-portfolio-deploy](https://github.com/jjkst/jk-portfolio-deploy)** project.

The deploy project provides:
- Docker Compose orchestration for all services
- Nginx reverse proxy with SSL/TLS via Let's Encrypt
- Deployment scripts for Digital Ocean
- Database backup automation

### Local Docker Test Harness

Use `docker-compose.local.yml` to run a local API + MariaDB test environment for integration verification:

```bash
docker compose -f docker-compose.local.yml up --build -d
# Execute integration tests
dotnet test RukuServiceApi.IntegrationTests
# Tear down local test environment
docker compose -f docker-compose.local.yml down -v
```

This local harness is intended for API validation only. For full deployment, continue using the existing `jk-portfolio-deploy` compose stack.

### Standalone Docker Run (API only)

To run just the API container (e.g., for development or testing):

```bash
docker build --target api -t ruku-service-api .
docker run -p 5000:80 \
  --env-file .env.local \
  -v "$(pwd)/RukuServiceApi/logs:/app/logs" \
  -v "$(pwd)/RukuServiceApi/uploads:/app/uploads" \
  ruku-service-api
```

## Testing

Two test projects using **MSTest**. Unit tests need no running server; integration tests hit the live API over HTTP.

### Unit Tests (`RukuServiceApi.UnitTests`)

Isolated — all dependencies mocked. Run them without any infrastructure:

```bash
dotnet test RukuServiceApi.UnitTests
```

| Area | Test Files |
|------|-----------|
| **Validators** | `CreateServiceRequestValidatorTests`, `UpdateServiceRequestValidatorTests`, `ContactRequestValidatorTests`, `PricingPlanValidatorTests`, `CreateUserRequestValidatorTests`, `UpdateUserRoleRequestValidatorTests` |
| **Services** | `AuthServiceTests` (JWT generation/validation), `FileUploadServiceTests` (type allowlist, size limits, path traversal) |
| **Middleware** | `SecurityHeadersMiddlewareTests`, `GlobalExceptionMiddlewareTests`, `ValidationMiddlewareTests` |
| **Health Checks** | `MemoryHealthCheckTests`, `EmailServiceHealthCheckTests` |

### Integration Tests (`RukuServiceApi.IntegrationTests`)

HTTP-level tests that call the live API at `http://localhost:5002`. Cover the full request pipeline — auth, routing, validation, database round-trips, and error responses.

| Test File | What It Covers |
|-----------|---------------|
| `AuthControllerTests` | Login (valid/invalid credentials), registration, duplicate user |
| `ServicesControllerTests` | CRUD, role-based access, duplicate detection, validation errors |
| `PublicServicesControllerTests` | Public read-only access, no-auth required |
| `AvailabilitiesControllerTests` | CRUD, date validation, overlap detection, timeslot queries |
| `SchedulesControllerTests` | Booking creation, update, delete |
| `UsersControllerTests` | User listing, role updates (Admin-only), delete |
| `EmailControllerTests` | Contact form send, settings endpoint |
| `UploadImageControllerTests` | File upload, type/size/path-traversal rejection |
| `MonitoringControllerTests` | System info, performance metrics, log retrieval, GC endpoint |
| `HealthCheckTests` | `/health`, `/health/ready`, `/health/live` |

#### Option A — Self-contained (recommended, requires Docker)

Spins up MariaDB and the API in Docker, runs all tests, then tears everything down:

```bash
./test-integration.sh
```

#### Option B — Against a locally running server

If you already have MariaDB and the API running (`dotnet run --project RukuServiceApi`):

```bash
dotnet test RukuServiceApi.IntegrationTests
```

### Integration Tests (`RukuServiceApi.IntegrationTests`)

HTTP-level tests that exercise every controller (Auth, Services, Availabilities, Schedules, Users, Email, UploadImage, Monitoring, Health Checks) against a running API.

**Test harness assumptions** (hardcoded in [`TestHelpers.cs`](RukuServiceApi.IntegrationTests/TestHelpers.cs)):

| Setting | Value | How to satisfy |
|---------|-------|----------------|
| Base URL | `http://localhost:5002` | API must listen on this port |
| Admin login | `admin@rukuit.com` / `admin-uid-12345` | Seeded automatically in `Development` |
| Owner login | `owner@rukuit.com` / `owner-uid-67890` | Seeded automatically in `Development` |

There are no test-side env vars to configure — the project just needs an API on `:5002` with the dev seed users present, which means **`ASPNETCORE_ENVIRONMENT=Development`** (see [`DatabaseSeeder.SeedDevUsersAsync`](RukuServiceApi/Services/DatabaseSeeder.cs)).

Tests run in parallel at the method level (per `MSTestSettings.cs`).

#### Option A — Run against a locally launched API

```bash
# 1. Start the API (Development env auto-seeds the dev users)
dotnet run --project RukuServiceApi &

# 2. Run the tests
dotnet test RukuServiceApi.IntegrationTests
```

#### Option B — Run against the local Docker harness

Uses [`docker-compose.local.yml`](docker-compose.local.yml) to bring up MariaDB + the API together. See the [Local Docker Test Harness](#local-docker-test-harness) section above for the full flow.

#### Useful flags

```bash
# Run a single test class
dotnet test RukuServiceApi.IntegrationTests \
  --filter "FullyQualifiedName~ServicesControllerTests"

# Run a single test method
dotnet test RukuServiceApi.IntegrationTests \
  --filter "FullyQualifiedName~ServicesControllerTests.GetServices_ReturnsOk"

# Run tests by category (if [TestCategory] attributes are used)
dotnet test RukuServiceApi.IntegrationTests --filter "TestCategory=Smoke"

# Re-run on file change (TDD loop) — keep the API running in another terminal
dotnet watch test --project RukuServiceApi.UnitTests

# Increase verbosity for failures
dotnet test RukuServiceApi.IntegrationTests --logger "console;verbosity=detailed"
```

### Run All Tests

```bash
dotnet test
```

> The solution-level `dotnet test` runs unit + integration tests together, so the API still needs to be reachable on `:5002` or the integration tests will fail.

## Database Management

### Migration Tool

The project includes a C# CLI migration tool (`MigrateTool`):

```bash
./migrate create AddNewFeature       # Create a new migration
./migrate update Development         # Apply migrations (Development)
./migrate update Production          # Apply migrations (Production)
./migrate reset Development          # Drop + reapply all migrations
./migrate status                     # Show migration status
./migrate script InitialCreate out.sql  # Generate SQL script
./migrate rollback PreviousMigration # Rollback to specific migration
./migrate remove                     # Remove last unapplied migration
./migrate drop Development           # Drop database
```

Or use dotnet CLI directly:
```bash
dotnet ef migrations add <Name> --project RukuServiceApi
dotnet ef database update --project RukuServiceApi
```

### Data Seeding

**Admin user (all environments):** On every startup, the app checks for an admin user configured via environment variables. If the user does not already exist, it is created automatically.

| Variable | Required | Description |
|----------|----------|-------------|
| `ADMIN_EMAIL` | Yes | Admin account email |
| `ADMIN_UID` | Yes | Firebase/auth UID |
| `ADMIN_DISPLAY_NAME` | No | Display name (defaults to "Administrator") |

If `ADMIN_EMAIL` or `ADMIN_UID` are not set, admin seeding is skipped with a warning.

**Dev test users (Development only):** In Development mode, additional test users are seeded if no users exist:
- Admin (`admin@rukuit.com` / `admin-uid-12345`)
- Owner (`owner@rukuit.com` / `owner-uid-67890`)

### Schema

| Table | Description |
|-------|-------------|
| Users | Accounts with roles (Admin, Owner, Subscriber) |
| Services | Service offerings with features and pricing plans |
| Availabilities | Date ranges with services and timeslots |
| Schedules | Customer appointment scheduling |

## Security

### Authentication & Authorization
- JWT Bearer tokens with configurable expiration
- Three roles: Admin, Owner, Subscriber
- Policy-based authorization on all protected endpoints

### Input Validation
- FluentValidation for all request DTOs
- File upload: type allowlist (.jpg, .jpeg, .png, .gif, .pdf), 10MB size limit

### Security Headers
Applied via middleware on every response:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Strict-Transport-Security` (HSTS)

### Environment Security
- All secrets via environment variables (never hardcoded)
- Swagger disabled in production
- CORS restricted to configured origins
- Nginx rate limiting: 10 requests/second with burst of 20

## Monitoring & Health Checks

### Health Check Components

| Check | Tag | What It Validates |
|-------|-----|------------------|
| `database` | - | MySQL connection and query |
| `email_service` | - | SMTP configuration |
| `file_system` | - | Upload directory accessibility |
| `memory` | - | Memory usage thresholds |
| `db_context` | - | EF Core DbContext |

### Logging

Serilog with structured JSON output:
- Console + rolling file sinks
- Request/response logging with correlation IDs
- Automatic log file rotation
- Log levels configurable per environment

### Production Deployment Checklist

See the **[jk-portfolio-deploy](https://github.com/jjkst/jk-portfolio-deploy)** project for the complete production deployment guide, including:

- Environment configuration
- Docker Compose orchestration
- SSL/TLS setup with Let's Encrypt
- Database backups
- Digital Ocean droplet setup
