# Hydroacoustic Expert Web Service

## 1. Призначення та склад програмного засобу

Програмний засоб призначений для **порівняння результатів гідроакустичного моделювання з даними натурних вимірювань**: облік наборів даних («симуляція», «поле»), побудова висновків щодо відмінностей і рекомендацій для аналізу якості моделі.

Склад:

- **Серверна частина** — веб-API на ASP.NET Core (JSON, JWT), зберігання в **PostgreSQL** (Entity Framework Core, міграції).
- **Клієнтська частина** — настільний застосунок на платформі **Ivy** (підключення до API; автентифікація Basic у клієнті відповідає обліковим записам, що створюються при першому старті API).

Кореневий URL API у продакшені не є повноцінним веб-інтерфейсом; основна робота з даними виконується через клієнт Ivy після вказання адреси API.

## 2. Запуск на локальній машині за допомогою Docker

**Вимоги:** встановлені Docker Engine і плагін Docker Compose v2.

**Кроки** (з кореня клонованого репозиторію):

```bash
cd deploy/department
cp example.env .env
```

У файлі `.env` задайте надійні значення `POSTGRES_PASSWORD` та `JWT_KEY` (не менше ~32 символів для ключа). За потреби змініть **`DEPARTMENT_HTTP_PORT`** (за замовчуванням `18080`) та **`DEPARTMENT_DB_PORT`** (`15432`), якщо ці порти на хості зайняті.

```bash
docker compose up -d --build
```

Перший запуск контейнерів виконує міграції та сиди в БД. Порт API на хості береться з `.env` (`DEPARTMENT_HTTP_PORT`, типово **18080**).

**API (Docker: PostgreSQL + веб-API)** — після `docker compose up` перевірка:

```bash
curl -sf "http://localhost:18080/health"
```

Якщо змінено `DEPARTMENT_HTTP_PORT`, підставте його замість `18080`.

Зупинка стека:

```bash
cd deploy/department
docker compose down
```

(Повне видалення даних БД: `docker compose down -v`.)

**Клієнт Ivy** (окремий термінал; потрібні .NET SDK та встановлена утиліта `ivy`). Підключення до API на тій самій машині (порт як у `.env`).

Встановлення Ivy CLI (одноразово, глобально):

```bash
dotnet tool install -g Ivy.Console
```

За наявності старої версії: `dotnet tool update -g Ivy.Console`. Далі:

```bash
cd client
dotnet user-secrets set "BackendApi:BaseUrl" "http://localhost:18080/" --project SmartEnergyExpert.Client.csproj
dotnet user-secrets set "BackendApi:Email" "admin@smartenergy.local" --project SmartEnergyExpert.Client.csproj
dotnet user-secrets set "BackendApi:Password" "Admin123!" --project SmartEnergyExpert.Client.csproj
ivy auth add --provider Basic
ivy run --browse
```

Облікові дані користувачів після сидів — у файлі **`server/SmartEnergyExpert.Api/Services/DatabaseInitializer.cs`** (за потреби змініть `Email`/`Password` у `user-secrets`).

Через Compose та змінні: **`deploy/department/docker-compose.yml`**, **`deploy/department/example.env`**. Файл `.env` не комітують.
