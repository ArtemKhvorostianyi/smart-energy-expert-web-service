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

## Deploy (Sliplane: API у контейнері, Ivy локально)

Обхід **`ivy deploy` + Sliplane** (якщо Ivy падає на `Failed to parse server description`): API деплоїш як звичайний Docker-сервіс у Sliplane, клієнт запускаєш **`ivy run`** на машині й направляєш на прод-API.

### 1. PostgreSQL

У Sliplane (або зовні) підніми Postgres, зніми рядок підключення Npgsql.

### 2. Образ API

З кореня репозиторію:

```bash
docker build -t hydro-api:local -f server/SmartEnergyExpert.Api/Dockerfile .
```

Перевір локально (підстав свій рядок БД):

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=…;Port=5432;Database=…;Username=…;Password=…" \
  -e Jwt__Key="мінімум-32-символи-випадкового-секрету" \
  hydro-api:local
```

API слухає **8080** (`ASPNETCORE_URLS` у Dockerfile).

### 3. Сервіс у Sliplane (UI)

1. Зайди в [Sliplane](https://sliplane.io/docs) → свій **сервер** → **New service** (або аналог для деплою контейнера).
2. Варіанти образа:
   - **Registry**: запуш `hydro-api:local` у GHCR / Docker Hub і вкажи образ у сервісі; або  
   - **Build from Git**: репозиторій, **Dockerfile path** `server/SmartEnergyExpert.Api/Dockerfile`, **build context** — **корінь репозиторію** (`.` / default). Якщо в UI окреме поле «context», вкажи `/` або корінь, не лише `server/...`.
3. У сервісі вистав **публічний порт** на контейнерний **8080** (або зміни `ASPNETCORE_URLS` і порт у Sliplane узгоджено).
4. **Environment variables** для контейнера API:

   | Змінна | Призначення |
   | -------- | ------------- |
   | `ConnectionStrings__DefaultConnection` | Npgsql до Postgres |
   | `Jwt__Key` | Довгий випадковий секрет (не `dev-only-…`) |
   | `Jwt__Issuer` / `Jwt__Audience` | За потреби; інакше дефолти з `Program.cs` |

5. Після старту перевір у браузері `https://<твій-api-хост>/` (або health, якщо додаси endpoint). Логін клієнта — той самий Basic/JWT, що й у сидів API (див. `DatabaseInitializer` / локальний пароль).

### 4. Локальний Ivy → прод API

У проєкті клієнта вже є **`client/appsettings.json`** (локальний `http://localhost:5109/`). Для прод-URL зручніше **user secrets** (не потрапляють у git):

```bash
cd client
dotnet user-secrets set "BackendApi:BaseUrl" "https://<твій-api-хост>/" --project SmartEnergyExpert.Client.csproj
dotnet user-secrets set "BackendApi:Email" "<email з сидів або твій>" --project SmartEnergyExpert.Client.csproj
dotnet user-secrets set "BackendApi:Password" "<пароль>" --project SmartEnergyExpert.Client.csproj
ivy auth add --provider Basic
ivy run --browse
```

Альтернатива — змінні середовища перед `ivy run` (подвійне підкреслення `__`):

`BackendApi__BaseUrl`, `BackendApi__Email`, `BackendApi__Password`.

Шаблон без секретів у репо: **`client/appsettings.Production.json.example`** → скопіюй у `appsettings.Production.json` (файл у `.gitignore`), якщо так зручніше ніж secrets.

Документація Sliplane: [docs.sliplane.io](https://docs.sliplane.io). Про збій Ivy `ivy deploy` + Sliplane — issue з логом `-v` у підтримку Ivy.

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
