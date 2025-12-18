# План исправлений по итогам ручного тестирования

- [x] Ручное тестирование выполнено (найдены ошибки/недочёты ниже).

## P0 — блокирующие ошибки

- [x] Исправить падение при добавлении замера: `PG error 42883: operator does not exist: record ->> unknown`.
  - Лог (пример): `2025-12-16T18:29:30.9055870Z [ERROR] [ui] plant measurement insert error: ...`
  - [x] Исправление внесено в SQL-скрипты (заменено чтение `x->>'field'` на `x.value->>'field'`).
  - [x] Также исправлено: `PG error 42883: function jsonb_array_elements_with_ordinality(jsonb) does not exist` (заменено на `jsonb_array_elements(...) WITH ORDINALITY` в wrapper-процедуре).
  - [x] Применить обновлённые SQL-скрипты к БД заводов (ANPZ/KNPZ): `sql/*/04_function_sp_insert_measurement_batch.sql`.
  - [x] Повторить ручной сценарий в UI и убедиться, что замер добавляется без ошибок.
  - Где править (предположительно):
    - `sql/anpz/04_function_sp_insert_measurement_batch.sql` (обработка JSON точек)
    - `sql/krnpz/04_function_sp_insert_measurement_batch.sql` (обработка JSON точек)
    - `sql/anpz/05_procedure_wrapper.sql`, `sql/krnpz/05_procedure_wrapper.sql` (wrapper-процедура)
    - UI-обвязка/логирование: `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`

- [x] Исправить падение при вставке батча в central через FDW: `PG error 23502: null value in column "id" of relation "measurement_batches" violates not-null constraint`.
  - Возможные причины:
    - Central БД: таблица `public.measurement_batches` создана ранее без `identity/default` на `id` (тогда `ALTER TABLE ... ADD IDENTITY` лечит).
    - FDW (часто): на заводе foreign table `central_ft.measurement_batches` включает колонку `id`, и `postgres_fdw` отправляет `NULL` вместо remote default/identity.
  - [x] В репозитории добавлен upgrade-блок в `sql/central/01_tables.sql` (добавляет `GENERATED ... AS IDENTITY` на `id`, если default отсутствует).
  - [x] В репозитории исправлен FDW: `sql/anpz/02_fdw.sql` и `sql/krnpz/02_fdw.sql` больше не включают `id/created_at` в foreign table.
  - [x] Применить FDW-скрипт в БД завода (ANPZ/KNPZ): `sql/*/02_fdw.sql`.
  - [x] Проверить/починить `id` в central при необходимости: `sql/central/01_tables.sql` (upgrade-блок).

- [x] Исправить падение при обновлении замеров с пустым поиском: `42P08: could not determine data type of parameter $1`.
  - Лог (пример): `2025-12-18T17:38:46.0525660Z [ERROR] [ui] plant measurements refresh error: 42P08: could not determine data type of parameter $1`.
  - [x] В UI параметр `@q` передаётся типизированным (`NpgsqlDbType.Text`), чтобы Postgres понимал тип даже при `NULL`.
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `src/OilErp.Ui/ViewModels/CentralMeasurementsTabViewModel.cs`

## P1 — UX/UI и консистентность интерфейса

- [x] Подсветка/выделение выбранного трубопровода/строки при добавлении замера (должно быть ясно, что выбрано).
  - Сделано: добавлен фон для `ListBoxItem:selected` и привязка фона строки к контейнеру.
  - Где править: `src/OilErp.Ui/Views/MainWindow.axaml`
- [x] Привести действия к одному стилю: либо везде иконки, либо везде текст.
  - Сделано: кнопки `+` заменены на `Добавить`/`Добавить замер`, добавлен “primary” стиль для основного действия.
  - Где править: `src/OilErp.Ui/Views/MainWindow.axaml`, `src/OilErp.Ui/App.axaml`
- [x] Сделать кнопку “Добавить замер” более заметной (сейчас сливается с фоном).
  - Сделано: “primary” стиль + понятная подпись.
  - Где править: `src/OilErp.Ui/Views/MainWindow.axaml`, `src/OilErp.Ui/App.axaml`
- [x] Больше визуальных подсказок (hover/focus/disabled/progress), чтобы было понятно, что происходит.
  - Сделано: подсветка выделения/наведения в списках (ListBox), “primary” стиль для основных действий (+ hover/pressed), прогресс‑индикаторы на вкладках, стиль `Button:disabled`.
  - Где править: `src/OilErp.Ui/Views/MainWindow.axaml`, `src/OilErp.Ui/App.axaml`

## P1 — корректность ввода и CRUD

- [x] Валидация ввода при добавлении замера (числовая толщина, обязательные поля; запрет “любых символов” там, где ожидается число).
  - Сделано: толщина теперь вводится как текст + парсинг/валидация перед сохранением.
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementEditWindowViewModel.cs`, `src/OilErp.Ui/Views/PlantMeasurementEditWindow.axaml`
- [x] Довести CRUD по замерам: редактирование/удаление (сейчас нет или неочевидно).
  - Сделано: редактирование/удаление последнего замера + окно “История” для выбора и редактирования/удаления конкретного замера.
  - Где править (предположительно): `src/OilErp.Ui/Views/*Measurements*.axaml`, `src/OilErp.Ui/ViewModels/*Measurements*.cs`, при необходимости SQL-процедуры в `sql/anpz` и `sql/krnpz`.

## P2 — справочники/смысловые поля

- [x] Пересмотреть “Локация” и “Статус”: объяснить назначение или заменить на понятные предустановленные варианты; для заводского режима ставить статус `OK` по умолчанию.
  - Сделано: в окне редактирования добавлена подсказка по назначению полей; в заводском справочнике оборудования статус по умолчанию `OK` + выпадающий список предустановок; на экране замеров пустой статус отображается как `OK`; при автосоздании оборудования через вставку замера в SQL статус также `OK`.
  - Где править: `src/OilErp.Ui/ViewModels/PlantEquipmentTabViewModel.cs`, `src/OilErp.Ui/ViewModels/EquipmentEditWindowViewModel.cs`, `src/OilErp.Ui/Views/EquipmentEditWindow.axaml`, `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `sql/anpz/05_procedure_wrapper.sql`, `sql/krnpz/05_procedure_wrapper.sql`

## P2 — темы и нейминг

- [x] Убрать синий цвет в тёмной теме (заменить на белый/нейтральный).
  - Где править: `src/OilErp.Ui/ViewModels/ThemePalette.cs`, `src/OilErp.Ui/App.axaml`
- [x] Переименовать темы на “Тёмная” и “Светлая” (без лишних слов).
  - Где править: `src/OilErp.Ui/ViewModels/MainWindowViewModel.cs`, `src/OilErp.Ui/Views/MainWindow.axaml`
- [x] Привести названия профилей/БД в UI к “ANPZ” и “KNPZ” (капсом, как аббревиатуры).
  - Сделано: отображение профилей/заголовков/кода завода в UI теперь `ANPZ`/`KNPZ`; добавлен алиас `KRNPZ -> KNPZ` для совместимости; имя БД берётся из `select current_database()` и форматируется в `ANPZ`/`KNPZ`/`Central`.
  - Где править: `src/OilErp.Ui/Services/KernelGateway.cs`, `src/OilErp.Ui/ViewModels/MainWindowViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `src/OilErp.Ui/Views/ConnectWindow.axaml`, `src/OilErp.Ui/Services/MeasurementIngestionService.cs`, `src/OilErp.Ui/Services/StoragePortFactory.cs`

## P3 — работа с данными (импорт/экспорт) и удобство поиска

- [x] Экспорт замеров в `csv`/`xlsx` (Excel), а также `json` для дальнейшей работы.
  - Сделано:
    - Завод: экспорт из окна “История замеров” (по одному трубопроводу).
    - Central/Завод: отдельная кнопка “Импорт/Экспорт” на вкладке “Замеры” (central — все заводы сразу, завод — только текущий).
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementsTransferWindowViewModel.cs`, `src/OilErp.Ui/ViewModels/CentralMeasurementsTransferWindowViewModel.cs`, `src/OilErp.Ui/Services/UiFilePicker.cs`, `src/OilErp.Ui/Services/SimpleXlsxWriter.cs`, `src/OilErp.Ui/Views/MainWindow.axaml`
- [x] Импорт тех же форматов в UI (если нужен именно через UI, а не через CLI).
  - Сделано:
    - Завод: импорт CSV/JSON в окно “История замеров” и/или через кнопку “Импорт/Экспорт”.
    - Central: импорт CSV/JSON через кнопку “Импорт/Экспорт” (пишет в `public.measurement_batches`).
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementsTransferWindowViewModel.cs`, `src/OilErp.Ui/ViewModels/CentralMeasurementsTransferWindowViewModel.cs`, `src/OilErp.Ui/Services/UiFilePicker.cs`
- [x] Фильтрация списка замеров (дата/статус/локация/трубопровод).
  - Сделано: фильтр по коду/локации/статусу оборудования в заводской таблице; в “Истории” — фильтр по диапазону дат + поиск по label/note.
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`
- [x] Сортировка списка замеров (дата/статус/локация/трубопровод).
  - Сделано: сортировка оборудования (в т.ч. по дате последнего замера) + сортировка истории замеров (время/толщина/label).
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`, `src/OilErp.Ui/Views/PlantMeasurementHistoryWindow.axaml`
- [x] Группировка списка замеров (дата/статус/локация/трубопровод).
  - Сделано: группировка оборудования по статусу/локации/дате последнего замера/префиксу кода; в “Истории” — группировка по дням.
  - Где править: `src/OilErp.Ui/ViewModels/PlantMeasurementsTabViewModel.cs`, `src/OilErp.Ui/Views/MainWindow.axaml`, `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`
- [x] Поиск замеров по основным полям (дата/статус/локация/трубопровод) + общий поиск по UI во всех окнах и по всем базам.
  - Сделано: поисковые строки на ключевых вкладках (central/plant справочники и замеры) + поиск в “Истории”; Enter запускает поиск, при вводе есть авто‑обновление (debounce).
  - Где править: `src/OilErp.Ui/Views/MainWindow.axaml`, `src/OilErp.Ui/ViewModels/*TabViewModel.cs`, `src/OilErp.Ui/ViewModels/PlantMeasurementHistoryWindowViewModel.cs`

- [x] Central: аналитика должна включать трубопроводы со всех заводов, даже если они не заведены вручную в `assets_global`.
  - Сделано: аналитика строится по `public.measurement_batches` + `assets_global` + `analytics_cr` (с fallback через `public.fn_calc_cr`), чтобы новое оборудование появлялось сразу после первых замеров.
  - Важно: если в CENTRAL не видно замеры других заводов — проверьте, что в заводской БД `central_srv` указывает на ту же central БД (host/port/dbname); автосинхронизация FDW берёт `OILERP__DB__CONN` / `OIL_ERP_PG`.
  - Где править: `src/OilErp.Ui/ViewModels/AnalyticsPanelViewModel.cs`, `sql/*/02_fdw.sql`, `src/OilErp.Infrastructure/Util/DatabaseInventory.cs`
- [x] Central: вкладка “Замеры” должна показывать оборудование со всех заводов + сортировка/группировка как на заводах.
  - Сделано: загрузка оборудования через FULL JOIN `assets_global` и last_batch; добавлены сортировка/группировка и отображение завода; добавление замеров ограничено только `CENTRAL`.
  - Где править: `src/OilErp.Ui/ViewModels/CentralMeasurementsTabViewModel.cs`, `src/OilErp.Ui/Views/MainWindow.axaml`
