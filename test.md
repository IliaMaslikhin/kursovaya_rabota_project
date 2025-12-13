# Проверки после последних изменений

1) **Применить SQL-скрипты**  
   - central: `sql/central/01_tables.sql`, `02_functions_core.sql`, `03_procedures.sql` (новые CHECK/NOTIFY, очистка чужих типов).  
   - anpz/krnpz: `04_function_sp_insert_measurement_batch.sql`, `05_procedure_wrapper.sql` (валидация порядка дат/толщин, prev/last по истории, транзакции).  
   - Убедиться, что `events_inbox.event_type` не null и только `HC_MEASUREMENT_BATCH`.

2) **Smoke UI** (ручной)  
   - Подключение через MainWindow (env `OILERP__DB__CONN[_ANPZ|_KRNPZ]`).  
   - Вкладка “Очередь”: `Обновить`, `Requeue`, `Cleanup` (с тайм-аутом), отображение `events_ingest` ленты.  
   - Вкладка “Диагностика”: выбор каналов из списка и ввод кастомного; проверить авто-переподключение (убить сеть/PG, потом восстановить).  
   - Базовые операции вкладок “Внесение данных” и “Аналитика” (ingest + refresh).

3) **Tests.Runner**  
   - Собрать `src/OilErp.Tests.Runner` (restore может потребовать доступ в сеть).  
   - Запустить без аргументов, убедиться, что сценарий `Plant_Events_Reach_Analytics` проходит при наличии заводских строк; при отсутствии — skipped.  
   - Опционально включить нагрузочный тест: `OILERP__TESTS__ENABLE_LOAD=1 [OILERP__TESTS__LOAD_EVENTS=...] dotnet run --project src/OilErp.Tests.Runner`.

4) **Infrastructure**  
   - Проверить логирование: `OILERP__LOG__TO_CONSOLE=0`/`OILERP__LOG__TO_FILE=0`/`OILERP__LOG__DIR=...` — логи пишутся/не пишутся по настройкам.  
   - Проверить отключение pg_proc-кэша: `OILERP__DB__DISABLE_PROC_CACHE=1` (должен логироваться факт отключения, вызовы работают).

5) **LISTEN авто-reconnect**  
   - В любом клиенте подписаться через Diagnostics или Tests.Runner `watch --channel events_ingest`.  
   - Перезапустить Postgres или оборвать сеть; после восстановления слушатель должен автоматически переподписаться и получать NOTIFY.
