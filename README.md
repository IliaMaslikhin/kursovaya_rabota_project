# Oil ERP Asset Registry and Integrity Tracking System

## Overview

This is a comprehensive .NET 8 C# solution implementing an ERP-style asset registry and integrity tracking system specifically designed for oil company operations. The system manages asset integrity across multiple plants with centralized analytics and distributed data collection.

## Architecture

The system consists of the following components:

### Projects

- **OilErp.Domain**: Domain models, value objects, and business logic
- **OilErp.Data**: Data access layer with repository pattern and PostgreSQL integration
- **OilErp.App**: Console application for system operations and demonstrations
- **OilErp.Ui**: Avalonia-based desktop UI for user interactions
- **OilErp.Tests**: Comprehensive test suite

### Database Architecture

- **Central Database**: Global asset registry, analytics, risk policies, incidents
- **Plant Databases**: Local asset measurements, defect tracking, work orders (ANPZ, KRNPZ, SNPZ)
- **Foreign Data Wrappers (FDW)**: Integration between central and plant databases

## Technology Stack

- **.NET 8** with C# 12
- **PostgreSQL** for data storage
- **Dapper** for data access
- **Avalonia** for cross-platform UI
- **xUnit** for testing
- **Serilog** for logging

## Getting Started

### Prerequisites

- .NET 8 SDK
- PostgreSQL 14+
- Git

### Building the Solution

```bash
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running the Console Application

```bash
cd src/OilErp.App
dotnet run
```

### Running the Desktop UI

```bash
cd src/OilErp.Ui
dotnet run
```

## Database Setup

Database migration scripts are located in the `sql/` directory:

- `sql/central/`: Central database schema and data
- `sql/plants/`: Plant database schema and data

## Documentation

For detailed technical documentation, see the files in the `docs/` directory:

- `architecture.md`: System architecture overview
- `database-design.md`: Database schema and design decisions
- `api-reference.md`: API documentation
- `deployment.md`: Deployment instructions

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Create a pull request

## License

This project is proprietary software for oil company operations.