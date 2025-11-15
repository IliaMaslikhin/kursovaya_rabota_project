# OilErp MCP7 UI Outline

## Guiding Principles
- **MCP7 layout**: seven core panels (Overview, Plants, Production, Storage, Logistics, Analytics, Settings) docked into a unified shell with responsive behavior.
- **Data source abstraction**: UI talks only to Core contracts via adapters so it remains testable without infrastructure dependencies.
- **Theming**: light/dark toggle based on Fluency theme, matching Avalonia defaults for rapid iteration.

## Must-Have Features
1. **Global shell** with navigation rail (MCP7 sections), action toolbar, status footer, and modal host.
2. **Plant overview dashboard** showing KPIs (through placeholder data now, later wired to Core DTOs) and quick filters.
3. **Storages & inventory panel** (MCP7 Storage) with tabular list, inline search, and detail drawer.
4. **Operations timeline** (MCP7 Production) listing recent operations with color-coded status chips.
5. **Analytics workspace** reserved for charting—backed by placeholder view that proves layout slots for charts.
6. **Settings & theme panel** to configure environment (plant, api endpoint, theme) persisted locally.
7. **Diagnostics console** (MCP7 Logistics) to show last sync time and command logs for operations.

## Iterative Path
- **Iteration 0 (current)**: scaffold Avalonia app, implement shell layout and placeholder views for all seven panels.
- **Iteration 1**: hook fake data providers from Core DTOs, add navigation state + view models.
- **Iteration 2**: integrate real operations via Infrastructure adapters, add command execution flows.
- **Iteration 3**: polish (theming, validation, responsive tweaks) and add tests using Avalonia.Headless.
