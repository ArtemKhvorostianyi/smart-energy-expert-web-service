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

- API URL: `http://localhost:5109/`
- Login credentials (seeded): `admin@smartenergy.local` / `Admin123!`
