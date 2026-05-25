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

### Автентифікація (Basic Auth)

- Розділ **«Вхід»** — логін, реєстрація профілю (PostgreSQL), гостьовий режим.
- **Гість:** `guest` / `explore` (див. `Guest:Password` у `appsettings.json`) — перегляд і порівняння без імпорту, симуляцій і PDF.
- **Повний доступ** після реєстрації та входу email/паролем.

Для production змініть у user-secrets або env:

- `BasicAuth:HashSecret`, `BasicAuth:JwtSecret` (base64, ≥32 байт)
- `Guest:Password`

У застосунку: Панель, керування датасетами, середовищна симуляція, гідроакустичне порівняння тощо.

## 4. Docker (лише API + Postgres, legacy)

Якщо потрібен старий API:

```bash
cd deploy/department
docker compose up -d --build
curl -sf "http://localhost:18080/health"
```

Для **чистого Ivy** достатньо сервісу `postgres` у Compose і локального `ivy run`.
