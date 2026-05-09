# Hydroacoustic Client (Ivy)

Single localhost UI for running model-vs-field hydroacoustic comparison.

## Run locally

```bash
ivy auth add --provider Basic
ivy run --browse
```

## Included apps

- `Dashboard` with local workflow hints.
- `Hydroacoustic Comparison` for selecting simulation and field datasets.
- Result blocks for metrics, top differences, and recommendations.

## Backend assumptions

- За замовчуванням API: `http://localhost:5109/` (`client/appsettings.json`).
- Прод API: див. **Deploy (Sliplane: API у контейнері, Ivy локально)** у кореневому `README.md` — `dotnet user-secrets` для `BackendApi:BaseUrl` / Email / Password або змінні `BackendApi__*`.
- Логін після сидів (як у `appsettings.json`): `admin@smartenergy.local` / `Admin123!` (інші ролі — у `DatabaseInitializer`).
