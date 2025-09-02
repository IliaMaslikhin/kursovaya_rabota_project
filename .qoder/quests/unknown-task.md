# Oil ERP Asset Registry System - C# Application Design

## Overview

The Oil ERP Asset Registry System is a comprehensive .NET 8-based enterprise application designed for oil company operations, focusing on asset integrity tracking and management across multiple industrial plants. The system provides centralized analytics and risk monitoring while supporting distributed data collection at plant-level databases.

### Core Objectives
- Centralized management of asset integrity data across geographically dispersed plants
- Real-time integration between local plant operations and central analytics
- Tracking of defects, work orders, and risk policies with full auditability
- Data consistency using PostgreSQL Foreign Data Wrappers (FDW)

### Key Features
- Global asset registry with lifecycle tracking
- Plant-specific defect and measurement data management  
- Risk policy enforcement and incident reporting
- Cross-platform desktop UI for operators and engineers
- Automated testing and console-based operational workflows

## Architecture

### System Architecture Overview

```mermaid
graph TB
    subgraph "Presentation Layer"
        UI[OilErp.Ui - Avalonia Desktop]
        APP[OilErp.App - Console Interface]
    end
    
    subgraph "Business Layer"
        DOMAIN[OilErp.Domain - Business Logic]
    end
    
    subgraph "Data Access Layer"
        DATA[OilErp.Data - Repository Pattern]
    end
    
    subgraph "Database Layer"
        CENTRAL[(Central PostgreSQL)]
        PLANT1[(Plant DB 1)]
        PLANT2[(Plant DB 2)]
        PLANTN[(Plant DB N)]
    end
    
    subgraph "Testing Layer"
        TESTS[OilErp.Tests - Unit & Integration]
    end
    
    UI --> DOMAIN
    APP --> DOMAIN
    DOMAIN --> DATA
    DATA --> CENTRAL
    DATA --> PLANT1
    DATA --> PLANT2
    DATA --> PLANTN
    TESTS --> DOMAIN
    TESTS --> DATA
    
    CENTRAL -.->|FDW| PLANT1
    CENTRAL -.->|FDW| PLANT2
    CENTRAL -.->|FDW| PLANTN
```

### Project Structure

| Project | Responsibility | Key Components |
|---------|---------------|----------------|
| **OilErp.Domain** | Business Logic & Models | Entities, Value Objects, Domain Services, Business Rules |
| **OilErp.Data** | Data Access | Repositories, Database Context, Dapper Integration |
| **OilErp.App** | Console Application | CLI Commands, Data Import/Export, System Operations |
| **OilErp.Ui** | Desktop Interface | Avalonia Views, ViewModels, User Controls |
| **OilErp.Tests** | Testing | Unit Tests, Integration Tests, Test Fixtures |

### Technology Stack

- **Framework**: .NET 8
- **Language**: C# 12
- **Database**: PostgreSQL 14+ with Foreign Data Wrappers
- **Data Access**: Dapper (lightweight ORM)
- **UI Framework**: Avalonia (cross-platform desktop)
- **Testing**: xUnit
- **Logging**: Serilog
- **Architecture Pattern**: Layered Architecture with Repository Pattern

## Domain Model Design

### Core Domain Entities

```mermaid
erDiagram
    ASSET ||--o{ SEGMENT : contains
    ASSET ||--o{ DEFECT : has
    ASSET ||--o{ WORK_ORDER : requires
    SEGMENT ||--o{ MEASUREMENT_POINT : includes
    MEASUREMENT_POINT ||--o{ READING : records
    MATERIAL ||--o{ SEGMENT : composes
    COATING ||--o{ SEGMENT : protects
    FLUID ||--o{ ASSET : processes
    RISK_POLICY ||--o{ ASSET : governs
    
    ASSET {
        string Id PK
        string TagNumber
        string Description
        string PlantCode
        string AssetType
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    SEGMENT {
        uuid Id PK
        string AssetId FK
        string SegmentName
        decimal LengthM
        string MaterialCode FK
        string CoatingCode FK
        DateTime CreatedAt
    }
    
    MEASUREMENT_POINT {
        uuid Id PK
        uuid SegmentId FK
        string PointName
        decimal DistanceFromStart
        string MeasurementType
        DateTime CreatedAt
    }
    
    READING {
        uuid Id PK
        uuid PointId FK
        decimal Value
        string Unit
        DateTime MeasuredAt
        string OperatorId
        DateTime CreatedAt
    }
    
    DEFECT {
        uuid Id PK
        string AssetId FK
        string DefectType
        string Severity
        string Description
        DateTime DiscoveredAt
        DateTime CreatedAt
    }
    
    WORK_ORDER {
        uuid Id PK
        string AssetId FK
        string WoNumber
        string WorkType
        string Status
        Date ScheduledDate
        DateTime CreatedAt
    }
```

### Domain Services Architecture

```mermaid
graph LR
    subgraph "Domain Services"
        AS[Asset Service]
        RS[Risk Assessment Service]
        MS[Measurement Service]
        WS[Work Order Service]
        SS[Sync Service]
    end
    
    subgraph "Value Objects"
        MP[Measurement Point]
        DP[Defect Priority]
        AP[Asset Properties]
        RP[Risk Parameters]
    end
    
    subgraph "Aggregates"
        AA[Asset Aggregate]
        MA[Measurement Aggregate]
        WA[Work Order Aggregate]
    end
    
    AS --> AA
    RS --> AA
    MS --> MA
    WS --> WA
    SS --> AA
    SS --> MA
    SS --> WA
    
    AA --> MP
    AA --> AP
    MA --> MP
    WA --> DP
    RS --> RP
```

## Data Layer Architecture

### Repository Pattern Implementation

```mermaid
graph TB
    subgraph "Repository Interfaces"
        IAR[IAssetRepository]
        IMR[IMeasurementRepository]
        IWR[IWorkOrderRepository]
        IDR[IDefectRepository]
        IUR[IUnitOfWork]
    end
    
    subgraph "Repository Implementations"
        AR[AssetRepository]
        MR[MeasurementRepository]
        WR[WorkOrderRepository]
        DR[DefectRepository]
        UW[UnitOfWork]
    end
    
    subgraph "Database Connections"
        CDC[Central DB Connection]
        PDC[Plant DB Connection]
    end
    
    IAR -.-> AR
    IMR -.-> MR
    IWR -.-> WR
    IDR -.-> DR
    IUR -.-> UW
    
    AR --> CDC
    AR --> PDC
    MR --> PDC
    WR --> PDC
    DR --> PDC
    UW --> CDC
    UW --> PDC
```

### Database Schema Integration

#### Central Database Tables
- `catalogs.materials` - Material definitions and properties
- `catalogs.coatings` - Coating types and specifications  
- `catalogs.fluids` - Fluid properties and corrosivity data
- `assets.global_assets` - Master asset registry
- `risk.policies` - Risk assessment policies and thresholds
- `analytics.*` - Aggregated analytics and reporting data
- `incidents.*` - Incident tracking and management
- `sync.*` - Synchronization metadata and status

#### Plant Database Tables
- `local_assets.assets` - Local asset instances
- `local_assets.segments` - Asset segments and components
- `measurements.points` - Measurement point definitions
- `measurements.readings` - Actual measurement data
- `maintenance.defects` - Identified defects and issues
- `maintenance.work_orders` - Maintenance work orders
- `events.local_events` - Event sourcing for synchronization

## Application Layer Design

### Console Application (OilErp.App)

```mermaid
graph LR
    subgraph "CLI Commands"
        IC[Import Command]
        EC[Export Command]
        SC[Sync Command]
        RC[Report Command]
        MC[Maintenance Command]
    end
    
    subgraph "Command Handlers"
        ICH[Import Handler]
        ECH[Export Handler]
        SCH[Sync Handler]
        RCH[Report Handler]
        MCH[Maintenance Handler]
    end
    
    subgraph "Services"
        DS[Data Service]
        SS[Sync Service]
        RS[Report Service]
    end
    
    IC --> ICH
    EC --> ECH
    SC --> SCH
    RC --> RCH
    MC --> MCH
    
    ICH --> DS
    ECH --> DS
    SCH --> SS
    RCH --> RS
    MCH --> DS
```

#### Command Structure
| Command | Purpose | Parameters |
|---------|---------|------------|
| `import` | Import asset data from CSV/Excel | `--file`, `--type`, `--plant` |
| `export` | Export data for reporting | `--format`, `--date-range`, `--plant` |
| `sync` | Synchronize plant data to central | `--plant`, `--force` |
| `report` | Generate analytics reports | `--type`, `--output`, `--date-range` |
| `maintain` | Run maintenance operations | `--operation`, `--target` |

### Desktop Application (OilErp.Ui)

```mermaid
graph TB
    subgraph "Views"
        MV[Main View]
        AV[Asset Management View]
        MeV[Measurement View]
        DV[Defect Tracking View]
        WV[Work Order View]
        RV[Reports View]
    end
    
    subgraph "ViewModels"
        MVM[Main ViewModel]
        AVM[Asset ViewModel]
        MeVM[Measurement ViewModel]
        DVM[Defect ViewModel]
        WVM[Work Order ViewModel]
        RVM[Reports ViewModel]
    end
    
    subgraph "Services"
        AS[Asset Service]
        MS[Measurement Service]
        DS[Defect Service]
        WS[Work Order Service]
        RS[Report Service]
    end
    
    MV --> MVM
    AV --> AVM
    MeV --> MeVM
    DV --> DVM
    WV --> WVM
    RV --> RVM
    
    MVM --> AS
    AVM --> AS
    MeVM --> MS
    DVM --> DS
    WVM --> WS
    RVM --> RS
```

#### UI Components Architecture
- **Navigation**: Tree-based navigation with plant and asset hierarchy
- **Data Grids**: Sortable, filterable grids for asset lists and measurements
- **Forms**: Asset creation/editing with validation
- **Charts**: Real-time measurement visualization and trend analysis
- **Reports**: Printable reports with export capabilities

## Data Flow Architecture

### Measurement Data Flow

```mermaid
sequenceDiagram
    participant Operator
    participant PlantUI as Plant UI
    participant PlantDB as Plant Database
    participant SyncService as Sync Service
    participant CentralDB as Central Database
    participant Analytics as Analytics Engine
    
    Operator->>PlantUI: Enter Measurement
    PlantUI->>PlantDB: Store in measurements.readings
    PlantDB->>PlantDB: Create sync event
    
    Note over SyncService: Periodic sync process
    SyncService->>PlantDB: Query pending events
    PlantDB-->>SyncService: Return events
    SyncService->>CentralDB: Push aggregated data
    
    CentralDB->>Analytics: Trigger analytics update
    Analytics->>CentralDB: Store calculated metrics
```

### Risk Assessment Flow

```mermaid
flowchart TD
    A[New Measurement] --> B{Exceeds Threshold?}
    B -->|Yes| C[Create Risk Event]
    B -->|No| D[Store Normal Reading]
    C --> E[Evaluate Risk Policy]
    E --> F{Critical Risk?}
    F -->|Yes| G[Generate Incident]
    F -->|No| H[Log Risk Event]
    G --> I[Create Work Order]
    H --> J[Update Asset Status]
    D --> J
    I --> J
```

## Business Logic Layer

### Asset Management

#### Asset Lifecycle States
- **Planned** - Asset designed but not yet installed
- **Active** - Asset in operation and collecting data
- **Maintenance** - Asset temporarily out of service
- **Retired** - Asset permanently decommissioned

#### Asset Operations
- **Create Asset**: Validate asset data and register in global registry
- **Update Asset**: Modify asset properties with audit trail
- **Add Segment**: Define physical segments with materials and coatings
- **Record Measurement**: Capture measurement data with validation
- **Report Defect**: Log defects with severity assessment
- **Schedule Maintenance**: Create work orders based on defects or schedules

### Risk Management

#### Risk Assessment Rules
- **Threshold Monitoring**: Compare measurements against policy thresholds
- **Trend Analysis**: Identify deteriorating conditions over time
- **Correlation Analysis**: Detect patterns across multiple assets
- **Predictive Maintenance**: Schedule maintenance based on risk scores

#### Risk Policy Engine
- **Policy Definition**: Configure thresholds and response actions
- **Policy Evaluation**: Apply policies to incoming measurements
- **Escalation Rules**: Define escalation paths for different risk levels
- **Compliance Tracking**: Monitor adherence to risk management procedures

## Integration Architecture

### Plant-to-Central Synchronization

```mermaid
graph LR
    subgraph "Plant Database"
        PE[Plant Events]
        PA[Plant Assets]
        PM[Plant Measurements]
    end
    
    subgraph "Sync Engine"
        ES[Event Scanner]
        DT[Data Transformer]
        VS[Validation Service]
        CS[Conflict Resolver]
    end
    
    subgraph "Central Database"
        CA[Central Assets]
        CM[Central Measurements]
        SA[Sync Audit]
    end
    
    PE --> ES
    ES --> DT
    DT --> VS
    VS --> CS
    CS --> CA
    CS --> CM
    CS --> SA
    
    PA -.-> DT
    PM -.-> DT
```

### Event-Driven Architecture

#### Event Types
- **AssetCreated**: New asset registered
- **MeasurementRecorded**: New measurement data
- **DefectDetected**: Defect identified
- **WorkOrderCreated**: Maintenance scheduled
- **RiskThresholdExceeded**: Risk policy violation
- **SyncCompleted**: Data synchronization finished

#### Event Processing
- **Event Store**: Persist all events for audit and replay
- **Event Handlers**: Process events asynchronously
- **Event Projection**: Build read models from events
- **Event Replay**: Reconstruct system state from events

## Testing Strategy

### Unit Testing Architecture

```mermaid
graph TB
    subgraph "Domain Tests"
        DT[Domain Entity Tests]
        VST[Value Object Tests]
        DST[Domain Service Tests]
        BRT[Business Rule Tests]
    end
    
    subgraph "Data Layer Tests"
        RT[Repository Tests]
        DIT[Database Integration Tests]
        MT[Migration Tests]
    end
    
    subgraph "Application Tests"
        CT[Command Tests]
        ST[Service Tests]
        IT[Integration Tests]
    end
    
    subgraph "UI Tests"
        VMT[ViewModel Tests]
        UIT[UI Integration Tests]
        E2ET[End-to-End Tests]
    end
    
    subgraph "Test Infrastructure"
        TF[Test Fixtures]
        TD[Test Data Builders]
        TDB[Test Database]
    end
    
    DT --> TF
    RT --> TDB
    CT --> TD
    VMT --> TF
```

### Test Categories

#### Unit Tests
- **Domain Entity Tests**: Validate business logic and invariants
- **Repository Tests**: Verify data access logic with mocked databases
- **Service Tests**: Test business services with mocked dependencies
- **ViewModel Tests**: Validate UI logic and data binding

#### Integration Tests
- **Database Integration**: Test repository implementations with real database
- **API Integration**: Verify external service integrations
- **Sync Integration**: Test plant-to-central synchronization
- **End-to-End**: Complete user workflow testing

### Test Data Management

#### Test Database Strategy
- **In-Memory Database**: Fast unit tests with SQLite in-memory
- **Docker PostgreSQL**: Integration tests with containerized database
- **Test Data Builders**: Fluent builders for creating test data
- **Database Migrations**: Separate test migration scripts

#### Mock Strategy
- **Repository Mocks**: Mock data layer for business logic tests
- **Service Mocks**: Mock external dependencies
- **Time Mocks**: Control time for temporal testing
- **Event Mocks**: Mock event publishing for unit tests

## Deployment and Infrastructure

### Database Deployment

#### Central Database Setup
- Install PostgreSQL 14+ with FDW extensions
- Run central schema migrations from `sql/central/`
- Configure FDW connections to plant databases
- Set up backup and replication strategies

#### Plant Database Setup
- Install PostgreSQL at each plant location
- Run plant schema migrations from `sql/plants/`
- Configure local backup strategies
- Establish secure connections to central database

### Application Deployment

#### Desktop Application Distribution
- Build cross-platform binaries for Windows, Linux, macOS
- Package with installer for easy deployment
- Include database connection configuration
- Provide offline operation capabilities

#### Console Application Deployment
- Deploy as scheduled service for automated operations
- Configure command-line interfaces for operators
- Set up logging and monitoring
- Implement error handling and recovery

### Security Architecture

#### Database Security
- Role-based access control for different user types
- Encrypted connections between plants and central
- Audit logging for all data modifications
- Backup encryption and secure storage

#### Application Security
- User authentication and authorization
- Secure configuration management
- Input validation and sanitization
- Error handling without information disclosure

## Performance Considerations

### Database Optimization

#### Query Performance
- Proper indexing strategy for frequent queries
- Partitioning for large measurement tables
- Query optimization for cross-database joins via FDW
- Connection pooling for high-throughput scenarios

#### Data Archival
- Archive old measurement data to separate tables
- Implement data retention policies
- Compress archived data for storage efficiency
- Maintain query performance with proper indexing

### Application Performance

#### UI Responsiveness
- Asynchronous data loading with progress indicators
- Virtual scrolling for large data grids
- Lazy loading of detailed data
- Caching frequently accessed data

#### Sync Performance
- Batch processing for large data sets
- Delta synchronization to minimize data transfer
- Compression for network efficiency
- Retry logic with exponential backoff