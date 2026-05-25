# Hydroacoustic Expert Web Service

## 1. Призначення та склад

Програмний засіб для **порівняння результатів гідроакустичного моделювання з даними натурних вимірювань**: датасети («симуляція», «поле»), метрики, відмінності, рекомендації.

- **Застосунок** — Ivy (`client/`): UI + бізнес-логіка + **PostgreSQL** через Entity Framework Core (`Connections/AppDb`).
- Окремий ASP.NET API (`server/`) **більше не потрібен** для роботи клієнта — залишено для зворотної сумісності / поступового видалення.

## 2. PostgreSQL (локально або Docker)

**Вимоги:** PostgreSQL 16 (локально або через Compose).

```bash
cd deploy/department
cp example.env .env
docker compose up -d postgres
```

У `client/appsettings.json` (або user-secrets / змінна `ConnectionStrings__DefaultConnection`) задайте рядок підключення до Postgres.

## 3. Клієнт Ivy

Інструмент (одноразово): `dotnet tool install -g Ivy.Console`.

```bash
cd client
ivy run --browse
```

При старті виконуються міграції EF та сиди (синтетика + bundled ARLUT CSV).

У застосунку: Панель, керування датасетами, середовищна симуляція, гідроакустичне порівняння тощо.

## 4. Docker (лише API + Postgres, legacy)

Якщо потрібен старий API:

```bash
cd deploy/department
docker compose up -d --build
curl -sf "http://localhost:18080/health"
```

Для **чистого Ivy** достатньо сервісу `postgres` у Compose і локального `ivy run`.
