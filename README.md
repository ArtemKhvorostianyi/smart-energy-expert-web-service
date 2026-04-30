# Hydroacoustic Expert Web Service

Local web service for comparing hydroacoustic modeling results with field experiment measurements.

## Technology

- Backend: ASP.NET Core Web API
- UI: Ivy (single local client)
- Database: PostgreSQL + Entity Framework Core
- Auth: JWT on backend, Ivy Basic Auth in client

## Core domain

- `Dataset` (simulation or field source)
- `AcousticSample` (timestamped hydroacoustic point)
- `ComparisonRun` (model-vs-field execution with metrics)
- `DifferencePoint` (significant mismatches)
- `Recommendation` (rule-based explanation and action)

## Localhost setup

- API: `http://localhost:5109`
- PostgreSQL: `localhost:5432`, database `hydroacoustic_expert`
- Ivy client calls backend via `BackendApi:BaseUrl` (default `http://localhost:5109/`)

## Run locally

```bash
dotnet build SmartEnergyExpert.slnx
dotnet run --project server/SmartEnergyExpert.Api
```

In another terminal:

```bash
cd client
ivy auth add --provider Basic
ivy run --browse
```

## API flow

- `GET /api/datasets`
- `POST /api/comparisons`
- `GET /api/differences/{comparisonRunId}`
- `GET /api/recommendations/{comparisonRunId}`

## Seed data

On first start the API runs migrations and seeds:

- default users/roles;
- synthetic simulation dataset;
- synthetic field dataset;
- paired acoustic samples for immediate comparison testing.
