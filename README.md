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

## Deploy (Sliplane через Ivy)

Тести й контейнер **не** стартують самі: образ збираєш локально або в CI/Sliplane build.

1. **Sliplane**: обліковий запис, [API key](https://sliplane.io/docs) у дашборді. За потреби — окремий сервер або створення через Ivy.
2. **PostgreSQL**: окремий сервіс/база в Sliplane (або зовнішня), рядок підключення у форматі Npgsql.
3. **Збірка образа API** (контекст — каталог проєкту API):

   ```bash
   docker build -t hydro-api -f server/SmartEnergyExpert.Api/Dockerfile server/SmartEnergyExpert.Api
   ```

4. **Ivy**: з кореня репозиторію виконай `ivy deploy`, обери **Sliplane**, вкажи API key; якщо Ivy питає Dockerfile — шлях до `server/SmartEnergyExpert.Api/Dockerfile`, build context — `server/SmartEnergyExpert.Api`.
5. **Порт**: у образі за замовчуванням **8080** (`ASPNETCORE_URLS`). У Sliplane вистав маршрутизацію на цей порт (або зміни змінні в сервісі).
6. **Змінні середовища** (обов’язково в проді):

   | Змінна | Призначення |
   |--------|-------------|
   | `ConnectionStrings__DefaultConnection` | Npgsql до PostgreSQL |
   | `Jwt__Key` | Довгий випадковий секрет (не `dev-only-…`) |
   | `Jwt__Issuer` / `Jwt__Audience` | За потреби; інакше дефолти з `Program.cs` |

7. **Ivy-клієнт**: `ivy run` зазвичай локально; у `appsettings` / конфіг клієнта вкажи `BackendApi:BaseUrl` на HTTPS URL сервісу Sliplane після деплою.

Деталі провайдера: [Sliplane Documentation](https://sliplane.io/docs).

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

## Presentation Script (What to Say)

Use this short script during your demo:

1. **Problem statement**
   - "This system compares hydroacoustic simulation output with field experiment measurements."
   - "The goal is to detect significant mismatches and explain why they may happen."

2. **Data setup**
   - "I select one simulation dataset and one field dataset."
   - "I can save this setup as a preset and reuse it for repeated experiments."

3. **Comparison run**
   - "Now I run model-vs-field comparison."
   - "The backend computes MAE, RMSE, MRE, P95, and counts significant points."

4. **Interpretation**
   - "Low metrics mean good model adequacy."
   - "Higher metrics and severity distribution indicate where model tuning is required."

5. **Top differences and recommendations**
   - "The system shows top mismatched points with timestamps and severity."
   - "It provides recommendation codes and suggested actions for diagnostics."

6. **Charts**
   - "Bar chart summarizes global quality metrics."
   - "Line chart shows relative/absolute error behavior."
   - "Pie chart shows severity distribution across top differences."

7. **Conclusion**
   - "This gives a practical decision-support workflow for validating hydroacoustic models against field data."
