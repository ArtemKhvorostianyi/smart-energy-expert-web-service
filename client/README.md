# Hydroacoustic Client (Ivy)

UI для порівняння гідроакустичних моделей і польових даних.

## Запуск

У **`appsettings.json`** задайте **`BackendApi:BaseUrl`** (наприклад `http://localhost:5109/` або `http://localhost:18080/` після Docker Compose).

```bash
ivy run --browse
```

Авторизація до API **не використовується**; не потрібні user-secrets, Ivy connection BasicAuth для бекенда прибрано з репозиторію.

## Розділи

Панель, керування датасетами, середовищна симуляція, гідроакустичне порівняння тощо.
