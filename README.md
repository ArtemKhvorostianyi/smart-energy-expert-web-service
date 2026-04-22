# Smart Energy Expert Web Service

Web-сервіс експертного оцінювання результатів експериментів для підтримки прийняття рішень у кібер-фізичних системах енергетичної галузі.

## Мета

Створити масштабований і безпечний web-сервіс, який:

- приймає експериментальні дані;
- виконує багатокритеріальне оцінювання;
- визначає рівень ризику;
- формує рекомендації для прийняття рішень.

## Запланований стек

- Backend: ASP.NET Core Web API
- Frontend: React
- Database: PostgreSQL
- ORM: Entity Framework Core
- Auth: JWT + ASP.NET Identity

## Ролі користувачів

- Admin
- Expert
- Operator

## Вхідні дані

- параметри експерименту;
- значення показників;
- ваги критеріїв;
- допустимі межі;
- службові дані (дата, автор, тип експерименту).

## Вихідні дані

- інтегральна оцінка;
- категорія ризику;
- рекомендація;
- історія оцінювань;
- звіт.

## Структура репозиторію (план)

```text
/client
/server
/docs
```

## Backend Bootstrap

Поточний стан backend:

- `ASP.NET Core Web API` проєкт у `server/SmartEnergyExpert.Api`;
- підключений `Entity Framework Core` + `PostgreSQL provider`;
- базові сутності, `DbContext`, DTO, контролери `Auth`, `Experiments`, `Evaluations`;
- доданий базовий `evaluation service` для розрахунку score/risk/recommendation.

Запуск:

```bash
dotnet build SmartEnergyExpert.slnx
dotnet run --project server/SmartEnergyExpert.Api
```

## Ivy UI Bootstrap

Поточний стан frontend:

- Ivy-проєкт у `client/`;
- базові застосунки: `Dashboard`, `Experiments`, `Evaluations`;
- стартова UX-структура під ролі Admin/Expert/Operator і сценарій оцінювання.

Запуск UI:

```bash
cd client
ivy run --browse
```

## Статус

Початковий етап: ініціалізація репозиторію та базової проєктної структури.
