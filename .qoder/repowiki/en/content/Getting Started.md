# Getting Started

<cite>
**Referenced Files in This Document**   
- [README.md](file://README.md)
- [IMPLEMENTATION_README.md](file://IMPLEMENTATION_README.md)
- [sql/README.md](file://sql/README.md)
- [src/OilErp.App/appsettings.json](file://src/OilErp.App/appsettings.json)
- [src/OilErp.App/Program.cs](file://src/OilErp.App/Program.cs)
- [src/OilErp.Data/Infrastructure/DatabaseInfrastructure.cs](file://src/OilErp.Data/Infrastructure/DatabaseInfrastructure.cs)
- [src/OilErp.Ui/Class1.cs](file://src/OilErp.Ui/Class1.cs)
</cite>

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Building the Solution](#building-the-solution)
3. [Running Tests](#running-tests)
4. [Running Applications](#running-applications)
5. [Database Setup](#database-setup)
6. [Configuration](#configuration)
7. [Running the Application and API Access](#running-the-application-and-api-access)

## Prerequisites

Before setting up and running the Oil ERP system, ensure the following prerequisites are installed on your machine:

- **.NET 8 SDK**: Required for building and running the application. The system is built using .NET 8 with C# 12.
- **PostgreSQL 14+**: The system uses PostgreSQL as the database engine, leveraging advanced features such as Foreign Data Wrappers (FDW) for distributed operations.
- **Git**: Required for cloning the repository and managing source code.

These tools form the foundation of the development and runtime environment for the Oil ERP system.

**Section sources**
- [README.md](file://README.md#L0-L95)
- [IMPLEMENTATION_README.md](file://IMPLEMENTATION_README.md#L158-L184)

## Building the Solution

To build the Oil ERP solution, navigate to the root directory of the project and execute the following commands:

```bash
dotnet restore
dotnet build
```

The `dotnet restore` command restores all NuGet packages required by the projects in the solution. This includes dependencies such as Dapper for data access, Serilog for logging, FluentValidation for input validation, and xUnit for testing.

The `dotnet build` command compiles all projects in the solution:
- **OilErp.Domain**: Contains domain models, value objects, and business logic.
- **OilErp.Data**: Implements the data access layer using the repository pattern.
- **OilErp.App**: The main web API application.
- **OilErp.Ui**: Avalonia-based desktop UI project.
- **OilErp.Tests**: Comprehensive test suite.

Successful execution of these commands indicates that the solution is ready for testing and deployment.

**Section sources**
- [README.md](file://README.md#L0-L95)

## Running Tests

The Oil ERP system includes a comprehensive test suite organized into unit, integration, and end-to-end tests. To run all tests, execute the following command from the solution root:

```bash
dotnet test
```

To run specific categories of tests, use the `--filter` option:

```bash
# Run unit tests only
dotnet test --filter "Category=Unit"

# Run integration tests only  
dotnet test --filter "Category=Integration"
```

The test suite validates:
- Business logic in domain services
- Database interactions using TestContainers with real PostgreSQL instances
- API endpoint behavior and error handling
- Service registration and dependency injection configuration

**Section sources**
- [IMPLEMENTATION_README.md](file://IMPLEMENTATION_README.md#L158-L184)

## Running Applications

### Running the Console Application

To run the main Oil ERP application, navigate to the OilErp.App project directory and execute:

```bash
cd src/OilErp.App
dotnet run
```

This starts the ASP.NET Core web API server. By default, the application runs on `https://localhost:5001`. The API includes Swagger documentation available at the root URL (`https://localhost:5001`).

### Running the Desktop UI

To run the Avalonia-based desktop UI application, navigate to the OilErp.Ui project directory and execute:

```bash
cd src/OilErp.Ui
dotnet run
```

Currently, this serves as a placeholder entry point that outputs console messages indicating the UI application startup. In a complete implementation, this would launch the graphical user interface for user interactions.

**Section sources**
- [README.md](file://README.md#L0-L95)
- [src/OilErp.Ui/Class1.cs](file://src/OilErp.Ui/Class1.cs#L0-L10)

## Database Setup

The Oil ERP system uses a distributed database architecture with one central database and multiple plant-specific databases.

### Creating Databases

Create the following databases in your PostgreSQL instance:

```sql
CREATE DATABASE oil_erp_central;
CREATE DATABASE oil_erp_anpz;
CREATE DATABASE oil_erp_krnpz;
```

### Executing SQL Scripts

Execute the SQL scripts located in the `sql/` directory in the following order:

1. **Central Database** (`sql/central/`):
   - `01_tables.sql` — Create tables and indexes
   - `02_functions_core.sql` — Create core functions
   - `03_procedures.sql` — Create stored procedures
   - `04_function_sp_ingest_events_legacy.sql` — Optional legacy compatibility

2. **ANPZ Plant Database** (`sql/anpz/`):
   - `01_tables.sql` — Create plant-specific tables
   - `02_fdw.sql` — Configure Foreign Data Wrapper to central database
   - `03_trigger_measurements_ai.sql` — Create trigger for measurement events
   - `04_function_sp_insert_measurement_batch.sql` — Create batch insertion function
   - `05_procedure_wrapper.sql` — Create procedure wrapper

3. **KRNPZ Plant Database** (`sql/krnpz/`):
   - Execute the same sequence as ANPZ

The scripts are idempotent (using `IF NOT EXISTS` and `OR REPLACE`), so they can be safely re-executed.

**Section sources**
- [sql/README.md](file://sql/README.md#L0-L33)
- [IMPLEMENTATION_README.md](file://IMPLEMENTATION_README.md#L158-L184)

## Configuration

The application configuration is managed through the `appsettings.json` file located in the `src/OilErp.App/` directory.

### Connection Strings

Update the connection strings in `appsettings.json` to match your PostgreSQL setup:

```json
{
  "ConnectionStrings": {
    "CentralDatabase": "Host=localhost;Database=oil_erp_central;Username=postgres;Password=;",
    "AnpzDatabase": "Host=localhost;Database=oil_erp_anpz;Username=postgres;Password=;",
    "KrnpzDatabase": "Host=localhost;Database=oil_erp_krnpz;Username=postgres;Password=;"
  }
}
```

The system uses these connection strings to connect to the central database and plant-specific databases. The `DatabaseInfrastructure` component resolves the appropriate connection string based on the plant code.

### Database Settings

Additional database settings are configured in the same file:

```json
"DatabaseSettings": {
  "CommandTimeout": 30,
  "ConnectionPooling": true,
  "MaxPoolSize": 100
}
```

These settings control command execution timeout, connection pooling behavior, and maximum pool size for database connections.

**Section sources**
- [src/OilErp.App/appsettings.json](file://src/OilErp.App/appsettings.json#L0-L27)
- [src/OilErp.Data/Infrastructure/DatabaseInfrastructure.cs](file://src/OilErp.Data/Infrastructure/DatabaseInfrastructure.cs#L0-L133)

## Running the Application and API Access

After building the solution and setting up the databases, run the application using:

```bash
cd src/OilErp.App
dotnet run
```

### API Endpoints

Once running, the following key API endpoints are available:

- **Asset Management**:
  - `GET /api/assets/{assetCode}` — Retrieve asset details
  - `POST /api/assets` — Create or update an asset
  - `GET /api/assets/plant/{plantCode}` — List assets by plant

- **Measurement Operations**:
  - `POST /api/measurements/batch` — Submit a batch of measurements
  - `GET /api/measurements/{assetCode}` — Retrieve measurement history

- **Analytics & Reporting**:
  - `GET /api/analytics/top-risk` — Get top risk assets
  - `GET /api/analytics/corrosion` — Retrieve corrosion analytics

- **Work Order Management**:
  - `POST /api/work-orders` — Create a work order
  - `PATCH /api/work-orders/{id}/complete` — Complete a work order

### Swagger Documentation

The API is fully documented using Swagger/OpenAPI. Access the interactive documentation at `https://localhost:5001` when the application is running. This provides:
- Complete list of available endpoints
- Request/response models
- Example payloads
- Testing interface for API calls

The system implements a distributed data flow where plant-level measurements trigger events that are processed centrally to update analytics and risk assessments automatically.

**Section sources**
- [src/OilErp.App/Program.cs](file://src/OilErp.App/Program.cs#L0-L102)
- [IMPLEMENTATION_README.md](file://IMPLEMENTATION_README.md#L158-L184)