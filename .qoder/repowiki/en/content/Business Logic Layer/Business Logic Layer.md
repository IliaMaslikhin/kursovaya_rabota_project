
# Business Logic Layer

<cite>
**Referenced Files in This Document**   
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs)
- [MeasurementService.cs](file://src/OilErp.Domain/Services/MeasurementService.cs)
- [WorkOrderService.cs](file://src/OilErp.Domain/Services/WorkOrderService.cs)
- [RiskAssessmentService.cs](file://src/OilErp.Domain/Services/RiskAssessmentService.cs)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs)
- [IServices.cs](file://src/OilErp.Domain/Interfaces/IServices.cs)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs)
- [Asset.cs](file://src/OilErp.Domain/Entities/Asset.cs)
- [MeasurementPoint.cs](file://src/OilErp.Domain/Entities/MeasurementPoint.cs)
- [Reading.cs](file://src/OilErp.Domain/Entities/Reading.cs)
- [WorkOrder.cs](file://src/OilErp.Domain/Entities/WorkOrder.cs)
- [Defect.cs](file://src/OilErp.Domain/Entities/Defect.cs)
- [Segment.cs](file://src/OilErp.Domain/Entities/Segment.cs)
- [RiskAndInspection.cs](file://src/OilErp.Domain/ValueObjects/RiskAndInspection.cs)
- [DomainEnums.cs](file://src/OilErp.Domain/Enums/DomainEnums.cs)
- [Program.cs](file://src/OilErp.App/Program.cs)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [Architecture Overview](#architecture-overview)
3. [Core Domain Entities](#core-domain-entities)
4. [Domain Services](#domain-services)
5. [Repository Pattern Implementation](#repository-pattern-implementation)
6. [Dependency Injection and Service Composition](#dependency-injection-and-service-composition)
7. [Component Interactions and Data Flows](#component-interactions-and-data-flows)
8. [Technical Decisions and Trade-offs](#technical-decisions-and-trade-offs)
9. [Infrastructure Requirements and Deployment](#infrastructure-requirements-and-deployment)
10. [Cross-Cutting Concerns](#cross-cutting-concerns)
11. [Technology Stack](#technology-stack)
12. [Conclusion](#conclusion)

## Introduction

The Business Logic Layer of the Oil ERP system implements a robust domain-driven design architecture for managing oil industry assets, measurements, work orders, and risk assessments. This documentation provides a comprehensive overview of the system's business logic layer, focusing on domain entities, services, repository patterns, and dependency injection. The system is designed to handle complex asset management scenarios in oil refineries, with a focus on corrosion monitoring, defect tracking, maintenance scheduling, and risk assessment.

The architecture follows Domain-Driven Design (DDD) principles, with rich domain entities that encapsulate business logic and validation rules. The system employs the Repository Pattern to abstract data access concerns and uses Dependency Injection for clean service composition. This layered approach ensures separation of concerns, testability, and maintainability while providing a clear boundary between business logic and infrastructure concerns.

**Section sources**
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L1-L212)
- [MeasurementService.cs](file://src/OilErp.Domain/Services/MeasurementService.cs#L1-L226)
- [WorkOrderService.cs](file://src/OilErp.Domain/Services/WorkOrderService.cs#L1-L265)
- [RiskAssessmentService.cs](file://src/OilErp.Domain/Services/RiskAssessmentService.cs#L1-L302)

## Architecture Overview

The business logic layer follows a clean architecture pattern with clear separation between domain entities, services, and data access components. The system is organized into distinct layers that adhere to the Dependency Inversion Principle, where high-level modules do not depend on low-level modules but both depend on abstractions.

```mermaid
graph TB
subgraph "Domain Layer"
A[Domain Entities]
B[Domain Services]
C[Value Objects]
D[Interfaces]
end
subgraph "Data Access Layer"
E[Repositories]
F[UnitOfWork]
G[Database Infrastructure]
end
subgraph "Application Layer"
H[Controllers]
I[API Models]
end
A --> B
B --> D
E --> D
F --> E
G --> F
H --> B
B --> E
B --> F
style A fill:#f9f,stroke:#333
style B fill:#bbf,stroke:#333
style C fill:#f9f,stroke:#333
style D fill:#f96,stroke:#333
style E fill:#6f9,stroke:#333
style F fill:#6f9,stroke:#333
style G fill:#6f9,stroke:#333
style H fill:#9cf,stroke:#333
style I fill:#9cf,stroke:#333
classDef domain fill:#f9f,stroke:#333;
classDef service fill:#bbf,stroke:#333;
classDef interface fill:#f96,stroke:#333;
classDef data fill:#6f9,stroke:#333;
classDef app fill:#9cf,stroke:#333;
class A,C domain
class B service
class D interface
class E,F,G data
class H,I app
```

**Diagram sources **
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L9-L196)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L9-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)
- [IServices.cs](file://src/OilErp.Domain/Interfaces/IServices.cs#L1-L77)

**Section sources**
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L1-L212)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L1-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

## Core Domain Entities

The system's domain model is built around rich entities that encapsulate both data and behavior, following Domain-Driven Design principles. These entities represent the core business concepts of the oil industry asset management system.

### Asset Entity

The Asset entity represents a physical asset in the oil industry pipeline system, such as a pipeline, tank, or pump. It contains core properties like tag number, description, plant code, and asset type, along with navigation properties to related entities.

```mermaid
classDiagram
class Asset {
+string Id
+string TagNumber
+string? Description
+string PlantCode
+string? AssetType
+DateTime CreatedAt
+DateTime UpdatedAt
+ICollection<Segment> Segments
+ICollection<Defect> Defects
+ICollection<WorkOrder> WorkOrders
+void UpdateDescription(string)
+void AddSegment(Segment)
+void AddDefect(Defect)
+void AddWorkOrder(WorkOrder)
+bool HasCriticalDefects()
+int GetSegmentCount()
+decimal GetTotalLength()
}
class Segment {
+Guid Id
+string AssetId
+string SegmentName
+decimal LengthM
+string? MaterialCode
+string? CoatingCode
+DateTime CreatedAt
+Asset? Asset
+ICollection<MeasurementPoint> MeasurementPoints
+void UpdateLength(decimal)
+void SetMaterial(string)
+void SetCoating(string)
+void AddMeasurementPoint(MeasurementPoint)
+int GetMeasurementPointCount()
+IEnumerable<MeasurementPoint> GetMeasurementPointsByType(string)
}
class MeasurementPoint {
+Guid Id
+Guid SegmentId
+string PointName
+decimal DistanceFromStart
+string MeasurementType
+DateTime CreatedAt
+Segment? Segment
+ICollection<Reading> Readings
+void UpdatePosition(decimal)
+void SetMeasurementType(string)
+void AddReading(Reading)
+Reading? GetLatestReading()
+IEnumerable<Reading> GetReadingsInDateRange(DateTime, DateTime)
+decimal? GetAverageReading(DateTime?, DateTime?)
+int GetReadingCount()
}
class Reading {
+Guid Id
+Guid PointId
+decimal Value
+string Unit
+DateTime MeasuredAt
+string? OperatorId
+DateTime CreatedAt
+string? Notes
+bool IsValid
+MeasurementPoint? MeasurementPoint
+void UpdateValue(decimal, string)
+void SetOperator(string)
+void AddNotes(string)
+void MarkAsInvalid(string)
+void MarkAsValid()
+bool IsOutOfRange(decimal, decimal)
+bool IsMeasuredWithinLast(TimeSpan)
+string GetFormattedValue()
+bool IsMeasuredBefore(DateTime)
+bool IsMeasuredAfter(DateTime)
}
Asset "1" --> "*" Segment
Segment "1" --> "*" MeasurementPoint
MeasurementPoint "1" --> "*" Reading
```

**Diagram sources **
- [Asset.cs](file://src/OilErp.Domain/Entities/Asset.cs#L1-L71)
- [Segment.cs](file://src/OilErp.Domain/Entities/Segment.cs#L1-L62)
- [MeasurementPoint.cs](file://src/OilErp.Domain/Entities/MeasurementPoint.cs#L1-L74)
- [Reading.cs](file://src/OilErp.Domain/Entities/Reading.cs#L1-L75)

**Section sources**
- [Asset.cs](file://src/OilErp.Domain/Entities/Asset.cs#L1-L71)
- [Segment.cs](file://src/OilErp.Domain/Entities/Segment.cs#L1-L62)
- [MeasurementPoint.cs](file://src/OilErp.Domain/Entities/MeasurementPoint.cs#L1-L74)
- [Reading.cs](file://src/OilErp.Domain/Entities/Reading.cs#L1-L75)

### Work Order and Defect Entities

The WorkOrder and Defect entities represent maintenance activities and identified issues on assets, forming the core of the system's maintenance management capabilities.

```mermaid
classDiagram
class WorkOrder {
+Guid Id
+string AssetId
+string WoNumber
+string WorkType
+string Status
+DateTime ScheduledDate
+DateTime? StartedAt
+DateTime? CompletedAt
+DateTime CreatedAt
+string? Description
+string? AssignedTo
+string? Priority
+decimal? EstimatedHours
+decimal? ActualHours
+string? CompletionNotes
+Asset? Asset
+Guid? DefectId
+Defect? Defect
+void UpdateDescription(string)
+void AssignTo(string)
+void SetPriority(string)
+void SetEstimatedHours(decimal)
+void Start()
+void Complete(decimal, string)
+void Cancel(string)
+void Reschedule(DateTime)
+bool IsOverdue()
+bool IsInProgress()
+bool IsCompleted()
+bool IsCancelled()
+TimeSpan? GetDuration()
+decimal? GetHoursVariance()
}
class Defect {
+Guid Id
+string AssetId
+string DefectType
+string Severity
+string? Description
+DateTime DiscoveredAt
+DateTime CreatedAt
+string? DiscoveredBy
+string? Location
+bool IsResolved
+DateTime? ResolvedAt
+string? Resolution
+Asset? Asset
+ICollection<WorkOrder> WorkOrders
+void UpdateDescription(string)
+void SetLocation(string)
+void SetDiscoveredBy(string)
+void Resolve(string)
+void Reopen()
+bool IsCritical()
+bool IsHigh()
+bool IsMedium()
+bool IsLow()
+TimeSpan GetAge()
+TimeSpan? GetResolutionTime()
+bool HasWorkOrders()
+int GetWorkOrderCount()
}
Asset "1" --> "*" Defect
Asset "1" --> "*" WorkOrder
Defect "1" --> "*" WorkOrder
WorkOrder --> Defect : "references"
```

**Diagram sources **
- [WorkOrder.cs](file://src/OilErp.Domain/Entities/WorkOrder.cs#L1-L132)
- [Defect.cs](file://src/OilErp.Domain/Entities/Defect.cs#L1-L96)
- [Asset.cs](file://src/OilErp.Domain/Entities/Asset.cs#L1-L71)

**Section sources**
- [WorkOrder.cs](file://src/OilErp.Domain/Entities/WorkOrder.cs#L1-L132)
- [Defect.cs](file://src/OilErp.Domain/Entities/Defect.cs#L1-L96)

### Value Objects and Enums

The system uses value objects and enums to represent domain concepts that have no identity and are defined by their attributes.

```mermaid
classDiagram
class RiskThreshold {
+string Name
+decimal LowThreshold
+decimal MediumThreshold
+decimal HighThreshold
+decimal CriticalThreshold
+string Unit
+string Description
+string GetRiskLevel(decimal)
+bool ExceedsThreshold(decimal, string)
}
class InspectionCriteria {
+string InspectionType
+TimeSpan Frequency
+string[] RequiredMeasurements
+string[] QualifiedPersonnel
+string Procedure
+bool IsInspectionDue(DateTime)
+DateTime GetNextInspectionDate(DateTime)
+bool IsPersonnelQualified(string)
}
class ContactInfo {
+string Name
+string Email
+string Phone
+string? Department
+string? Role
+string GetDisplayName()
}
class AssetLifecycleState {
+Planned
+Active
+Maintenance
+Retired
}
class AssetType {
+Pipeline
+Tank
+Pump
+Valve
+Compressor
+Separator
+HeatExchanger
+Instrument
+Other
}
class DefectSeverity {
+Low
+Medium
+High
+Critical
}
class WorkOrderStatus {
+Scheduled
+InProgress
+Completed
+Cancelled
+OnHold
}
class WorkOrderPriority {
+Low
+Medium
+High
+Emergency
}
class WorkType {
+Inspection
+Preventive
+Corrective
+Emergency
+Predictive
+Calibration
+Cleaning
+Replacement
}
class MeasurementType {
+WallThickness
+Pressure
+Temperature
+Flow
+Vibration
+Corrosion
+PitDepth
+CoatingThickness
+Hardness
+Other
}
class MeasurementUnit {
+Millimeters
+Inches
+PSI
+Bar
+Celsius
+Fahrenheit
+LitersPerMinute
+GPM
+Hertz
+Micrometers
+Mils
}
```

**Diagram sources **
- [RiskAndInspection.cs](file://src/OilErp.Domain/ValueObjects/RiskAndInspection.cs#L1-L162)
- [DomainEnums.cs](file://src/OilErp.Domain/Enums/DomainEnums.cs#L1-L111)

**Section sources**
- [RiskAndInspection.cs](file://src/OilErp.Domain/ValueObjects/RiskAndInspection.cs#L1-L162)
- [DomainEnums.cs](file://src/OilErp.Domain/Enums/DomainEnums.cs#L1-L111)

## Domain Services

The domain services layer contains the core business logic of the system, implementing use cases and coordinating operations between entities and repositories.

### Asset Service

The AssetService handles operations related to asset management, including creation, updates, and retrieval of asset information with validation.

```mermaid
sequenceDiagram
participant Client as "Client Application"
participant AssetService as "AssetService"
participant UnitOfWork as "UnitOfWork"
participant AssetRepository as "AssetRepository"
Client->>AssetService : CreateAssetAsync(asset)
AssetService->>AssetService : Validate asset data
AssetService->>UnitOfWork : Get Assets repository
AssetRepository->>AssetRepository : Check for existing tag number
AssetRepository-->>AssetService : Return existing asset check
alt Asset already exists
AssetService-->>Client : Throw InvalidOperationException
else Valid asset
AssetService->>AssetService : Set creation timestamps
AssetService->>UnitOfWork : CreateAsync(asset)
AssetRepository->>AssetRepository : Insert asset record
AssetRepository-->>AssetService : Return asset ID
AssetService-->>Client : Return asset ID
end
```

**Diagram sources **
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L9-L196)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L9-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

**Section sources**
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L9-L196)

### Measurement Service

The MeasurementService manages measurement operations, including recording readings, validating data, and analyzing trends.

```mermaid
sequenceDiagram
participant Client as "Client Application"
participant MeasurementService as "MeasurementService"
participant UnitOfWork as "UnitOfWork"
participant ReadingRepository as "ReadingRepository"
participant MeasurementPointRepository as "MeasurementPointRepository"
Client->>MeasurementService : RecordReadingAsync(reading)
MeasurementService->>MeasurementService : Validate reading data
MeasurementService->>UnitOfWork : Get MeasurementPoints repository
MeasurementPointRepository->>MeasurementPointRepository : Validate point exists
MeasurementPointRepository-->>MeasurementService : Return point validation
MeasurementService->>MeasurementService : Validate reading value by type
MeasurementService->>MeasurementService : Check for anomalies
MeasurementService->>UnitOfWork : Get Readings repository
ReadingRepository->>ReadingRepository : Create reading record
ReadingRepository-->>MeasurementService : Return reading ID
MeasurementService-->>Client : Return reading ID
```

**Diagram sources **
- [MeasurementService.cs](file://src/OilErp.Domain/Services/MeasurementService.cs#L9-L207)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L9-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

**Section sources**
- [MeasurementService.cs](file://src/OilErp.Domain/Services/MeasurementService.cs#L9-L207)

### Work Order Service

The WorkOrderService manages work order lifecycle operations, including creation, scheduling, and status updates.

```mermaid
sequenceDiagram
participant Client as "Client Application"
participant WorkOrderService as "WorkOrderService"
participant UnitOfWork as "UnitOfWork"
participant WorkOrderRepository as "WorkOrderRepository"
participant AssetRepository as "AssetRepository"
Client->>WorkOrderService : CreateWorkOrderAsync(workOrder)
WorkOrderService->>WorkOrderService : Validate work order data
WorkOrderService->>UnitOfWork : Get Assets repository
AssetRepository->>AssetRepository : Validate asset exists
AssetRepository-->>WorkOrderService : Return asset validation
WorkOrderService->>UnitOfWork : Get WorkOrders repository
WorkOrderRepository->>WorkOrderRepository : Check for duplicate WO number
WorkOrderRepository-->>WorkOrderService : Return duplicate check
WorkOrderService->>WorkOrderService : Set creation timestamp and status
WorkOrderService->>UnitOfWork : CreateAsync(workOrder)
WorkOrderRepository->>WorkOrderRepository : Insert work order record
WorkOrderRepository-->>WorkOrderService : Return work order ID
WorkOrderService-->>Client : Return work order ID
```

**Diagram sources **
- [WorkOrderService.cs](file://src/OilErp.Domain/Services/WorkOrderService.cs#L8-L237)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L9-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

**Section sources**
- [WorkOrderService.cs](file://src/OilErp.Domain/Services/WorkOrderService.cs#L8-L237)

### Risk Assessment Service

The RiskAssessmentService evaluates asset risk based on multiple factors including defects, measurements, and maintenance history.

```mermaid
sequenceDiagram
participant Client as "Client Application"
participant RiskAssessmentService as "RiskAssessmentService"
participant UnitOfWork as "UnitOfWork"
participant MeasurementService as "MeasurementService"
participant AssetRepository as "AssetRepository"
Client->>RiskAssessmentService : AssessAssetRiskAsync(assetId)
RiskAssessmentService->>UnitOfWork : Get Assets repository
AssetRepository->>AssetRepository : Get asset with related data
AssetRepository-->>RiskAssessmentService : Return asset data
RiskAssessmentService->>RiskAssessmentService : Initialize risk factors list
RiskAssessmentService->>RiskAssessmentService : Assess defect risks
RiskAssessmentService->>RiskAssessmentService : Assess measurement risks
RiskAssessmentService->>RiskAssessmentService : Assess maintenance risks
RiskAssessmentService->>RiskAssessmentService : Calculate overall risk score
RiskAssessmentService->>RiskAssessmentService : Determine risk level
RiskAssessmentService->>RiskAssessmentService : Generate recommendations
RiskAssessmentService-->>Client : Return risk assessment result
```

**Diagram sources **
- [RiskAssessmentService.cs](file://src/OilErp.Domain/Services/RiskAssessmentService.cs#L9-L276)
- [UnitOfWork.cs](file://src/OilErp.Data/UnitOfWork.cs#L9-L127)
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

**Section sources**
- [RiskAssessmentService.cs](file://src/OilErp.Domain/Services/RiskAssessmentService.cs#L9-L276)

## Repository Pattern Implementation

The system implements the Repository Pattern to provide an abstraction layer between the domain logic and data access concerns. This pattern ensures that the business logic is not coupled to the specific database implementation.

### Repository Interface Hierarchy

The repository interfaces define contracts for data access operations, with a base interface and specific interfaces for each entity type.

```mermaid
classDiagram
class IRepository~TEntity,TKey~ {
+Task~TEntity?~ GetByIdAsync(TKey, CancellationToken)
+Task~IEnumerable~TEntity~~ GetAllAsync(CancellationToken)
+Task~TKey~ CreateAsync(TEntity, CancellationToken)
+Task UpdateAsync(TEntity, CancellationToken)
+Task DeleteAsync(TKey, CancellationToken)
}
class IAssetRepository {
+Task~Asset?~ GetByAssetCodeAsync(string, CancellationToken)
+Task~Asset?~ GetAssetSummaryAsync(string, string?, CancellationToken)
+Task~IEnumerable~Asset~~ GetByPlantCodeAsync(string, CancellationToken)
+Task~string~ UpsertAssetAsync(string, string, string, string, CancellationToken)
+Task~Asset?~ GetWithSegmentsAsync(string, CancellationToken)
+Task~Asset?~ GetWithDefectsAsync(string, CancellationToken)
+Task~Asset?~ GetWithWorkOrdersAsync(string, CancellationToken)
+Task~Asset?~ GetWithAllRelatedDataAsync(string, CancellationToken)
+Task~int~ GetCountByPlantAsync(string, CancellationToken)
+Task~int~ GetTotalCountAsync(CancellationToken)
}
class ISegmentRepository {
+Task~Segment?~ GetByIdAsync(Guid, CancellationToken)
+Task~IEnumerable~Segment~~ GetByAssetIdAsync(string, CancellationToken)
+Task~Guid~ CreateAsync(Segment, CancellationToken)
+Task~decimal~ GetTotalLengthByAssetAsync(string, CancellationToken)
+Task~Segment?~ GetWithMeasurementPointsAsync(Guid, CancellationToken)
}
class IMeasurementPointRepository {
+Task~MeasurementPoint?~ GetByIdAsync(Guid, CancellationToken)
+Task~IEnumerable~MeasurementPoint~~ GetBySegmentIdAsync(Guid, CancellationToken)
+Task~Guid~ CreateAsync(MeasurementPoint, CancellationToken)
+Task~MeasurementPoint?~ GetWithReadingsAsync(Guid, CancellationToken)
+Task~IEnumerable~MeasurementPoint~~ GetByAssetIdAsync(string, CancellationToken)
}
class IReadingRepository {
+Task~Reading?~ GetByIdAsync(Guid, CancellationToken)
+Task~IEnumerable~Reading~~ GetByPointIdAsync(Guid, CancellationToken)
+Task~Guid~ CreateAsync(Reading, CancellationToken)
+Task~IEnumerable~Reading~~ GetByDateRangeAsync(DateTime, DateTime, CancellationToken)
+Task~Reading?~ GetLatestByPointIdAsync(Guid, CancellationToken)
+Task~IEnumerable~Reading~~ GetByAssetIdAsync(string, CancellationToken)
+Task~decimal?~ GetAverageByPointIdAsync(Guid, DateTime?, DateTime?, CancellationToken)
+Task~decimal?~ GetMinValueByPointIdAsync(Guid, DateTime?, DateTime?, CancellationToken)
+Task~decimal?~ GetMaxValueByPointIdAsync(Guid, DateTime?, DateTime?, CancellationToken)
}
class IDefectRepository {
+Task~Defect?~ GetByIdAsync(Guid, CancellationToken)
+Task~IEnumerable~Defect~~ GetByAssetIdAsync(string, CancellationToken)
+Task~Guid~ CreateAsync(Defect, CancellationToken)
+Task~IEnumerable~Defect~~ GetBySeverityAsync(string, CancellationToken)
+Task~IEnumerable~Defect~~ GetUnresolvedAsync(CancellationToken)
+Task~int~ GetCountBySeverityAsync(string, CancellationToken)
+Task~int~ GetUnresolvedCountAsync(CancellationToken)
}
class IWorkOrderRepository {
+Task~WorkOrder?~ GetByIdAsync(Guid, CancellationToken)
+Task~WorkOrder?~ GetByWoNumberAsync(string, CancellationToken)
+Task~IEnumerable~WorkOrder~~ GetByAssetIdAsync(string, CancellationToken)
+Task~Guid~ CreateAsync(WorkOrder, CancellationToken)
+Task~IEnumerable~WorkOrder~~ GetByStatusAsync(string, CancellationToken)
+Task~IEnumerable~WorkOrder~~ GetOverdueAsync(CancellationToken)
+Task~int~ GetCountByStatusAsync(string, CancellationToken)
+Task~int~ GetOverdueCountAsync(CancellationToken)
}
IRepository~TEntity,TKey~ <|-- IAssetRepository
IRepository~TEntity,TKey~ <|-- ISegmentRepository
IRepository~TEntity,TKey~ <|-- IMeasurementPointRepository
IRepository~TEntity,TKey~ <|-- IReadingRepository
IRepository~TEntity,TKey~ <|-- IDefectRepository
IRepository~TEntity,TKey~ <|-- IWorkOrderRepository
```

**Diagram sources **
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#L1-L218)

**Section sources**
- [IRepositories.cs](file://src/OilErp.Domain/Interfaces/IRepositories.cs#