# System Architecture

## Overview

The Oil ERP Asset Registry system follows a distributed architecture pattern with a central hub managing global data and analytics, while individual plants handle local operations.

## Architecture Diagram

```mermaid
graph TB
    subgraph "Central Node"
        CentralDB[(Central PostgreSQL)]
        ConsoleApp[Console Application]
        AvaloniaUI[Avalonia Desktop UI]
    end
    
    subgraph "Plant ANPZ"
        ANPZDB[(ANPZ PostgreSQL)]
    end
    
    subgraph "Plant KRNPZ"
        KRNPZDB[(KRNPZ PostgreSQL)]
    end
    
    subgraph "Plant SNPZ"
        SNPZDB[(SNPZ PostgreSQL)]
    end
    
    ANPZDB -.->|FDW Read Catalogs| CentralDB
    KRNPZDB -.->|FDW Read Catalogs| CentralDB
    SNPZDB -.->|FDW Read Catalogs| CentralDB
    
    ANPZDB -->|Event Sync| CentralDB
    KRNPZDB -->|Event Sync| CentralDB
    SNPZDB -->|Event Sync| CentralDB
    
    ConsoleApp --> CentralDB
    AvaloniaUI --> CentralDB
```

## Components

### Central Hub
- Global asset registry
- Analytics and reporting
- Risk assessment policies
- Incident management
- Event synchronization

### Plant Operations
- Local asset instances
- Measurement data collection
- Defect tracking
- Work order management
- Local event generation

### Data Synchronization
- Outbox/Inbox pattern for event synchronization
- Foreign Data Wrappers for catalog access
- Eventual consistency model

## Design Patterns

- **Repository Pattern**: Data access abstraction
- **Domain-Driven Design**: Clear separation of business logic
- **MVVM Pattern**: UI architecture (Avalonia)
- **Event Sourcing**: Change tracking and synchronization
- **Foreign Data Wrapper**: Distributed data access