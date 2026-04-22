# Smart Energy Expert Web Service

Web service for expert evaluation of experiment results to support decision-making in energy cyber-physical systems.

## Goal

Build a scalable and secure web service that:

- accepts experimental data;
- performs multi-criteria evaluation;
- determines risk level;
- generates decision recommendations.

## Planned Stack

- Backend: ASP.NET Core Web API
- Frontend: Ivy Framework
- Database: PostgreSQL
- ORM: Entity Framework Core
- Auth: JWT + ASP.NET Identity

## User Roles

- Admin
- Expert
- Operator

## Input Data

- experiment parameters;
- measured values;
- criterion weights;
- allowed limits;
- metadata (date, author, experiment type).

## Output Data

- integral score;
- risk category;
- recommendation;
- evaluation history;
- report.

## Repository Structure (Plan)

```text
/client
/server
/docs
```

## Backend Bootstrap

Current backend status:

- `ASP.NET Core Web API` project in `server/SmartEnergyExpert.Api`;
- configured `Entity Framework Core` + `PostgreSQL provider`;
- core entities, `DbContext`, DTOs, and controllers: `Auth`, `Experiments`, `Evaluations`;
- baseline `evaluation service` for score/risk/recommendation calculation;
- database bootstrap with initial role/user seed and `ExperimentParameters` API module.

Run:

```bash
dotnet build SmartEnergyExpert.slnx
dotnet run --project server/SmartEnergyExpert.Api
```

Sample API flow:

- `POST /api/experiments`
- `POST /api/experiments/{experimentId}/parameters`
- `POST /api/experiments/{experimentId}/evaluation`

## Ivy UI Bootstrap

Current frontend status:

- Ivy project in `client/`;
- base apps: `Dashboard`, `Experiments`, `Evaluations`;
- initial UX structure aligned with Admin/Expert/Operator roles and evaluation workflow.

Run UI:

```bash
cd client
ivy run --browse
```

## Status

Current phase: repository bootstrap and baseline architecture are completed.
