# OilErp — запуск и проверка

## Коротко о проекте
OilErp — это учебная ERP‑система для учета активов и измерений коррозии. Центральная БД агрегирует данные и строит аналитику, заводские БД хранят локальные измерения и отправляют батчи в central через FDW.

## Требования
- PostgreSQL (актуальная версия).
- .NET 8 SDK.

## Быстрый старт без psql‑настроек
Ниже описан вариант, похожий на «ручной» сценарий через любой GUI‑клиент (pgAdmin, DBeaver, DataGrip).

### 1) Создание баз
Создайте три базы данных рядом с PostgreSQL:
- `central`
- `anpz`
- `krnpz`

Это можно сделать через GUI (Create Database) или одним SQL‑скриптом в любой клиентской консоли:
```sql
CREATE DATABASE central;
CREATE DATABASE anpz;
CREATE DATABASE krnpz;
```

### 2) Применение SQL (важен порядок)
**Central:**
1. `sql/central/01_tables.sql`
2. `sql/central/02_functions_core.sql`
3. `sql/central/03_procedures.sql`

**ANPZ / KRNPZ:**
1. `sql/anpz/01_tables.sql` (или `sql/krnpz/01_tables.sql`)
2. `sql/anpz/02_fdw.sql`
3. `sql/anpz/03_trigger_measurements_ai.sql`
4. `sql/anpz/04_function_sp_insert_measurement_batch.sql`
5. `sql/anpz/05_procedure_wrapper.sql`

Нагрузочный скрипт (опционально): `sql/central/99_mass_test_events.sql`.

> Примечание: `DatabaseBootstrapper` может попробовать создать базы и автоматически применить SQL (если есть права). Если автоприменение не сработало — следуйте порядку выше вручную.

### 3) Настройка строк подключения
Проект читает строки окружения. Минимальный набор:
```bash
export OILERP__DB__CONN="Host=localhost;Username=postgres;Password=postgres;Database=central"
export OILERP__DB__CONN_ANPZ="Host=localhost;Username=postgres;Password=postgres;Database=anpz"
export OILERP__DB__CONN_KRNPZ="Host=localhost;Username=postgres;Password=postgres;Database=krnpz"
export OILERP__DB__TIMEOUT_SEC=30
```
UI также понимает `OIL_ERP_PG`/`OIL_ERP_PG_TIMEOUT` как алиасы.

## Запуск UI
```bash
dotnet run --project src/OilErp.Ui
```
Перед запуском UI автоматически выполняются смоук‑тесты. Если они не пройдены — UI не стартует, подробности выводятся в консоль и в лог `%APPDATA%/OilErp/logs`.

## Как работать в UI
### Central (главная БД)
- **Оборудование**: добавить/изменить/удалить активы в `assets_global` (редактировать можно только central‑активы).
- **Политики риска**: управление `risk_policies`.
- **Замеры (Central)**: таблица последних замеров по активам; можно добавить замер напрямую в central.
- **Импорт/Экспорт**: CSV/JSON/XLSX для `measurement_batches`.
- **История замеров**: просмотр и экспорт истории по активу.
- **Аналитика**: таблица CR и уровней риска, группировка по заводам, выбор политики.

### Заводы (ANPZ / KNPZ)
- **Оборудование завода**: локальный справочник `assets_local`.
- **Замеры**: добавление замеров (заводская процедура), редактирование/удаление последнего замера, отправка батча в central через FDW.
- **Импорт/Экспорт**: CSV/JSON/XLSX по локальным измерениям.
- **История**: просмотр и правка истории по активу, с синхронизацией в central.

> Центральная БД — главный агрегатор. Заводы пишут батчи в central через FDW, а central триггером обновляет аналитику.

## Что проверяют смоук‑тесты
Смоук‑suite запускается перед UI и проверяет:
- **Подключение**: открытие соединения, health‑запросы, транзакции.
- **Загрузка/аналитика**: посев тестовых батчей и сверка CR/риска.
- **Функции**: корректность `fn_calc_cr`, `fn_eval_risk`, `fn_asset_summary_json`.
- **FDW и профили**: наличие объектов схемы, доступность `central_ft.measurement_batches`.
- **E2E‑сценарии**: завод → FDW → central → `analytics_cr`.

Если тесты не проходят, UI не запускается; причина отображается в консоли и логах.

## Логи и диагностика
- Логи: `%APPDATA%/OilErp/logs/app-*.log`.
- При ошибке bootstrap может положить гайд `OilErp_Database_Guide.md` на рабочий стол.
