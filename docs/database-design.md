# Database Design

## Schema Overview

The system uses PostgreSQL with a distributed database architecture.

## Central Database Schemas

### Catalogs Schema
- `materials`: Material specifications and properties
- `coatings`: Coating types and characteristics  
- `fluids`: Process fluid definitions
- `corrosion_mechanisms`: Types of corrosion processes

### Assets Schema
- `global_assets`: Master asset registry
- `asset_hierarchy`: Asset parent-child relationships

### Risk Schema
- `policies`: Risk assessment policies
- `matrix`: Risk level calculation rules
- `thresholds`: Critical value definitions

### Analytics Schema
- `corrosion_rates`: Historical corrosion calculations
- `remaining_life`: Asset life predictions
- `trends`: Statistical trend analysis

### Incidents Schema
- `incidents_global`: Company-wide incident tracking
- `incident_assets`: Asset involvement in incidents

### Sync Schema
- `outbox`: Events to be synchronized
- `inbox`: Received events from plants

## Plant Database Schemas

### Local_Assets Schema
- `assets`: Plant-specific asset instances
- `segments`: Asset segment definitions
- `measurement_points`: Data collection points

### Measurements Schema
- `readings`: Sensor and manual measurements
- `calibrations`: Equipment calibration records

### Maintenance Schema
- `defects`: Identified asset defects
- `work_orders`: Maintenance work tracking
- `inspections`: Scheduled inspection records

### Events Schema
- `local_events`: Plant-generated events for sync

## Foreign Data Wrappers

Plant databases access central catalogs via PostgreSQL FDW:

```sql
CREATE SERVER central_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host 'central-host', dbname 'central', port '5432');

IMPORT FOREIGN SCHEMA catalogs
  FROM SERVER central_server
  INTO public;
```

## Data Types

### Value Objects
- `AssetId`: Strongly-typed asset identifier
- `PlantCode`: Enumerated plant codes (ANPZ, KRNPZ, SNPZ)
- `MeasurementValue`: Measurement with unit and precision

### Enumerations
- `RiskLevel`: GREEN, YELLOW, ORANGE, RED
- `DefectSeverity`: LOW, MEDIUM, HIGH, CRITICAL
- `WorkOrderStatus`: OPEN, IN_PROGRESS, COMPLETED, CANCELLED