# Hydroacoustic Expert Web Service

## 1. Призначення та склад програмного засобу

Програмний засоб призначений для **порівняння результатів гідроакустичного моделювання з даними натурних вимірювань**: облік наборів даних («симуляція», «поле»), побудова висновків щодо відмінностей і рекомендацій для аналізу якості моделі.

Склад:

- **Серверна частина** — веб-API на ASP.NET Core (JSON), зберігання в **PostgreSQL** (Entity Framework Core, міграції). **Автентифікація та перевірка ролей на API вимкнені** (доступ відкритий усім, хто досягає хоста — обмежуйте доступ мережею або розгортанням усередині довіреного периметра).
- **Клієнтська частина** — настільний застосунок на платформі **Ivy**; підключення до API лише через **`BackendApi:BaseUrl`** у `client/appsettings.json` (або змінна `BackendApi__BaseUrl`).

Кореневий URL API не є повноцінним веб-інтерфейсом; основна робота з даними — у клієнті Ivy.

## 2. Запуск на локальній машині за допомогою Docker

**Вимоги:** Docker Engine і Docker Compose v2.

**Кроки** (з кореня репозиторію):

```bash
cd deploy/department
cp example.env .env
```

У `.env` задайте надійний `POSTGRES_PASSWORD`. За потреби змініть **`DEPARTMENT_HTTP_PORT`** (типово **18080**) та **`DEPARTMENT_DB_PORT`** (`15432`).

```bash
docker compose up -d --build
```

Перший запуск виконує міграції та сиди в БД (синтетичні датасети та пакетний CSV). Порт API на хості — з `.env`.

**Перевірка API:**

```bash
curl -sf "http://localhost:18080/health"
```

(Підставте свій `DEPARTMENT_HTTP_PORT`.)

**Зупинка:**

```bash
cd deploy/department
docker compose down
```

(`docker compose down -v` — видалить volume з даними Postgres.)

**Клієнт Ivy** (окремий термінал; .NET SDK + `Ivy.Console`):

```bash
dotnet tool install -g Ivy.Console
cd client
# У client/appsettings.json вкажіть "BackendApi:BaseUrl" на API, напр. http://localhost:18080/
ivy run --browse
```

Файли деплою: **`deploy/department/docker-compose.yml`**, **`deploy/department/example.env`**. Файл `.env` не комітують.
