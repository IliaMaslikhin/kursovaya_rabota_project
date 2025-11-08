# Запуск

## Конфигурация подключения к БД
CLI и тестовые сценарии читают настройки из переменных окружения или из `appsettings.Development.json`.

Переменные окружения (предпочтительно):

```bash
export OILERP__DB__CONN="Host=localhost;Username=postgres;Password=postgres;Database=central"
export OILERP__DB__TIMEOUT_SEC=30
```

Альтернатива через файл `appsettings.Development.json` (рядом с `src/OilErp.Tests.Runner`):

```json
{
  "OILERP": {
    "DB": {
      "CONN": "Host=localhost;Username=postgres;Password=postgres;Database=central",
      "TIMEOUT_SEC": 30
    }
  }
}
```

## Первый запуск

1. Примените SQL-скрипты из каталога `sql/` для центральной БД и заводов (см. `sql/README.md`).
2. Установите переменные окружения как выше.
3. Запустите команду добавления актива:

```bash
dotnet run --project src/OilErp.Tests.Runner -- add-asset --id A-001 --name "Pipe #1" --plant ANPZ
```

Примеры других команд:

```bash
# Загрузка измерений из JSON/CSV
dotnet run --project src/OilErp.Tests.Runner -- add-measurements-anpz --file ./points.json

# Просмотр очереди событий
dotnet run --project src/OilErp.Tests.Runner -- events-peek --limit 10

# Аналитика по активу
dotnet run --project src/OilErp.Tests.Runner -- summary --asset A-001
```

