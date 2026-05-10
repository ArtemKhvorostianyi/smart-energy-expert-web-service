# Hydroacoustic Expert Web Service

## 1. Призначення та склад

Програмний засіб для **порівняння результатів гідроакустичного моделювання з даними натурних вимірювань**: датасети («симуляція», «поле»), метрики, відмінності, рекомендації.

- **Сервер** — ASP.NET Core Web API, **PostgreSQL** + EF Core. **Без автентифікації на API** (хто має доступ до хоста — використовує API; ізолюйте мережею).
- **Клієнт** — Ivy-додаток у каталозі `client/`; звернення лише через **`BackendApi:BaseUrl`** у `client/appsettings.json` (або змінна оточення `BackendApi__BaseUrl`). Авторизація до API, user-secrets і Ivy-з’єднання під бекенд **не використовуються**.

## 2. Бекенд у Docker (`deploy/department`)

**Вимоги:** Docker Engine + Compose v2.

```bash
cd deploy/department
cp example.env .env
```

У `.env`: надійний `POSTGRES_PASSWORD`; за потреби `DEPARTMENT_HTTP_PORT` (типово **18080**), `DEPARTMENT_DB_PORT` (**15432**).

```bash
docker compose up -d --build
```

Перший старт — міграції та сиди (синтетика + bundled CSV).

```bash
curl -sf "http://localhost:18080/health"
```

Зупинка: `docker compose down` (з `-v` — видалиться volume Postgres).

Шаблон: **`deploy/department/example.env`**; робочий **`.env`** не комітують.

## 3. Клієнт Ivy

Інструмент (одноразово): `dotnet tool install -g Ivy.Console`.

У **`client/appsettings.json`** задайте URL API (`http://localhost:18080/` після Compose з порту за замовчуванням, або `http://localhost:5109/` якщо API запущено локально через `dotnet run` без Docker).

```bash
cd client
ivy run --browse
```

У застосунку: Панель, керування датасетами, середовищна симуляція, гідроакустичне порівняння тощо.
