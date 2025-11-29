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

## Автопроверка БД и первый запуск

- При старте CLI/смоук-харнесса и UI выполняется проверка профиля БД (central/anpz/krnpz) через `DatabaseBootstrapper`: при необходимости создаются сами базы `central/anpz/krnpz` на указанном хосте (тем же пользователем), затем недостающие объекты из `sql/` (idempotent). Оффлайн-режима нет: без соединения приложение завершается.
- Если проверка/создание не удалась, на рабочий стол сохраняется `OilErp_Database_Guide.md` с ошибкой и инструкцией (берётся из этого файла), в `%APPDATA%/OilErp/logs/app.log` пишутся детали.
- Фиксируется «код машины» (хеш окружения) и маркер первого запуска в `%APPDATA%/OilErp/first-run.machine`; тесты, помеченные `FirstRunOnly`, пропускаются при последующих запусках.
- UI берёт подключение из `OIL_ERP_PG` (или резервно `OILERP__DB__CONN`), показывает статус (профиль, код машины, путь до гайда). При ошибке подключения приложение не стартует (см. лог).
- Форма добавления замеров пишет напрямую в процедуру завода (ANPZ/KRNPZ) через существующие Core-сервисы; без БД выполнение прерывается с ошибкой. JSON снапшоты используются только как доп. данные при наличии соединения.
