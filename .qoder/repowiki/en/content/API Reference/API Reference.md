
# API Reference

<cite>
**Referenced Files in This Document**   
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs)
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs)
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs)
- [WorkOrdersController.cs](file://src/OilErp.App/Controllers/WorkOrdersController.cs)
- [ApiModels.cs](file://src/OilErp.App/Models/ApiModels.cs)
- [AnalyticsAndCommonModels.cs](file://src/OilErp.App/Models/AnalyticsAndCommonModels.cs)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [API Response Format](#api-response-format)
3. [Asset Management](#asset-management)
4. [Measurement Operations](#measurement-operations)
5. [Analytics & Reporting](#analytics--reporting)
6. [Work Order Management](#work-order-management)
7. [Common Issues and Solutions](#common-issues-and-solutions)

## Introduction
The Oil ERP system provides a comprehensive RESTful API for managing oil and gas assets, measurements, analytics, and work orders. This API documentation details the available endpoints, their request/response schemas, and usage examples. The API follows standard REST conventions with JSON payloads and provides comprehensive error handling.

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L10-L322)
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs#L11-L318)
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L9-L371)
- [WorkOrdersController.cs](file://src/OilErp.App/Controllers/WorkOrdersController.cs#L10-L421)

## API Response Format
All API responses follow a standardized format that includes success status, data payload, message, and error information. This consistent structure makes it easier to handle responses across different endpoints.

```json
{
  "success": true,
  "data": {},
  "message": "Operation completed successfully",
  "errors": [],
  "timestamp": "2023-12-07T10:30:00Z"
}
```

The response structure contains the following fields:
- **success**: Boolean indicating whether the operation was successful
- **data**: The main payload of the response (null if no data)
- **message**: Human-readable message about the operation result
- **errors**: Array of error messages (empty if no errors)
- **timestamp**: UTC timestamp of when the response was generated

This standardized format enables clients to consistently handle API responses regardless of the specific endpoint being called.

**Section sources**
- [AnalyticsAndCommonModels.cs](file://src/OilErp.App/Models/AnalyticsAndCommonModels.cs#L145-L155)

## Asset Management
The Asset Management endpoints provide CRUD operations for managing physical assets in the oil and gas facilities. These endpoints allow you to create, retrieve, and update asset information.

### GET /api/assets/{assetCode}
Retrieves detailed information about a specific asset by its asset code.

**Parameters**
- `assetCode` (path): The unique code identifying the asset

**Response Codes**
- 200: Asset found and returned successfully
- 404: Asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "assetCode": "string",
    "name": "string",
    "assetType": "string",
    "plantCode": "string",
    "description": "string",
    "location": "string",
    "status": "string",
    "createdAt": "string",
    "updatedAt": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/assets/T-1001
```

**Example Response**
```json
{
  "success": true,
  "data": {
    "assetCode": "T-1001",
    "name": "Distillation Tower 1",
    "assetType": "Tower",
    "plantCode": "ANPZ",
    "description": "Main distillation tower",
    "location": "Unit 1",
    "status": "Operational",
    "createdAt": "2023-01-15T09:30:00Z",
    "updatedAt": "2023-11-20T14:22:00Z"
  },
  "message": null,
  "errors": [],
  "timestamp": "2023-12-07T10:30:00Z"
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L35-L78)

### POST /api/assets
Creates a new asset or updates an existing asset (upsert operation).

**Request Body**
```json
{
  "assetCode": "string",
  "name": "string",
  "assetType": "string",
  "plantCode": "string",
  "description": "string",
  "location": "string",
  "status": "string"
}
```

**Response Codes**
- 201: Asset created or updated successfully
- 400: Invalid request data
- 500: Internal server error

**Response Schema**
Same as GET /api/assets/{assetCode}

**Example Request**
```json
POST /api/assets
{
  "assetCode": "T-1002",
  "name": "Distillation Tower 2",
  "assetType": "Tower",
  "plantCode": "ANPZ",
  "description": "Secondary distillation tower",
  "location": "Unit 1"
}
```

**Example Response**
```json
{
  "success": true,
  "data": {
    "assetCode": "T-1002",
    "name": "Distillation Tower 2",
    "assetType": "Tower",
    "plantCode": "ANPZ",
    "description": "Secondary distillation tower",
    "location": "Unit 1",
    "status": "Operational",
    "createdAt": "2023-12-07T10:30:00Z",
    "updatedAt": "2023-12-07T10:30:00Z"
  },
  "message": "Asset created/updated successfully",
  "errors": [],
  "timestamp": "2023-12-07T10:30:00Z"
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L148-L194)

### GET /api/assets/plant/{plantCode}
Retrieves all assets associated with a specific plant.

**Parameters**
- `plantCode` (path): The code identifying the plant

**Response Codes**
- 200: Assets retrieved successfully
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": [
    {
      "assetCode": "string",
      "name": "string",
      "assetType": "string",
      "plantCode": "string",
      "description": "string",
      "location": "string",
      "status": "string",
      "createdAt": "string",
      "updatedAt": "string"
    }
  ],
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/assets/plant/ANPZ
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L201-L238)

### GET /api/assets/{assetCode}/summary
Retrieves a comprehensive summary of an asset including risk assessment and health status.

**Parameters**
- `assetCode` (path): The unique code identifying the asset
- `policyName` (query, optional): Risk policy name to use for assessment (defaults to 'default')

**Response Codes**
- 200: Asset summary retrieved successfully
- 404: Asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "assetCode": "string",
    "name": "string",
    "assetType": "string",
    "plantCode": "string",
    "description": "string",
    "location": "string",
    "status": "string",
    "createdAt": "string",
    "updatedAt": "string",
    "riskAssessment": {
      "riskLevel": "string",
      "corrosionRate": 0,
      "previousThickness": 0,
      "lastThickness": 0,
      "previousDate": "string",
      "lastDate": "string",
      "assessmentDate": "string",
      "policyName": "string"
    },
    "defectCount": 0,
    "openWorkOrderCount": 0,
    "healthStatus": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L85-L141)

### GET /api/assets/{assetCode}/risk
Retrieves the risk assessment information for a specific asset.

**Parameters**
- `assetCode` (path): The unique code identifying the asset
- `policyName` (query, optional): Risk policy name to use for assessment

**Response Codes**
- 200: Risk assessment retrieved successfully
- 404: Asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "riskLevel": "string",
    "corrosionRate": 0,
    "previousThickness": 0,
    "lastThickness": 0,
    "previousDate": "string",
    "lastDate": "string",
    "assessmentDate": "string",
    "policyName": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L141-L147)

## Measurement Operations
The Measurement Operations endpoints handle the submission and retrieval of measurement data for assets, including batch processing and corrosion rate calculations.

### POST /api/measurements/batch
Submits a batch of measurement readings for a specific asset.

**Request Body**
```json
{
  "assetCode": "string",
  "sourcePlant": "string",
  "operatorId": "string",
  "notes": "string",
  "measurementPoints": [
    {
      "label": "string",
      "thickness": 0,
      "timestamp": "string",
      "note": "string"
    }
  ]
}
```

**Response Codes**
- 200: Batch submitted successfully
- 400: Invalid request data or asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "assetCode": "string",
    "sourcePlant": "string",
    "processedPoints": 0,
    "processedAt": "string",
    "success": true,
    "message": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```json
POST /api/measurements/batch
{
  "assetCode": "T-1001",
  "sourcePlant": "ANPZ",
  "operatorId": "OP-001",
  "notes": "Monthly inspection",
  "measurementPoints": [
    {
      "label": "Point-001",
      "thickness": 12.5,
      "timestamp": "2023-12-07T08:00:00Z",
      "note": "Normal reading"
    },
    {
      "label": "Point-002",
      "thickness": 11.8,
      "timestamp": "2023-12-07T08:15:00Z",
      "note": "Slight corrosion observed"
    }
  ]
}
```

**Section sources**
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs#L34-L104)

### GET /api/measurements/{assetCode}
Retrieves the measurement history for a specific asset within an optional date range.

**Parameters**
- `assetCode` (path): The unique code identifying the asset
- `fromDate` (query, optional): Start date for filtering measurements
- `toDate` (query, optional): End date for filtering measurements

**Response Codes**
- 200: Measurements retrieved successfully
- 404: Asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "assetCode": "string",
    "fromDate": "string",
    "toDate": "string",
    "totalMeasurements": 0,
    "measurements": [
      {
        "id": "string",
        "label": "string",
        "thickness": 0,
        "timestamp": "string",
        "note": "string"
      }
    ]
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/measurements/T-1001?fromDate=2023-01-01&toDate=2023-12-07
```

**Section sources**
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs#L111-L157)

### GET /api/measurements/{assetCode}/latest
Retrieves the most recent measurements for a specific asset.

**Parameters**
- `assetCode` (path): The unique code identifying the asset
- `limit` (query, optional): Maximum number of measurements to return (1-1000, default: 100)

**Response Codes**
- 200: Latest measurements retrieved successfully
- 400: Invalid limit parameter
- 404: Asset not found
- 500: Internal server error

**Response Schema**
Same as GET /api/measurements/{assetCode}

**Example Request**
```
GET /api/measurements/T-1001/latest?limit=50
```

**Section sources**
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs#L164-L211)

### GET /api/measurements/corrosion-rate
Calculates the corrosion rate between two thickness measurements.

**Parameters**
- `prevThickness` (query): Previous thickness measurement in mm
- `prevDate` (query): Date of previous measurement
- `lastThickness` (query): Latest thickness measurement in mm
- `lastDate` (query): Date of latest measurement

**Response Codes**
- 200: Corrosion rate calculated successfully
- 400: Invalid input parameters
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": 0,
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/measurements/corrosion-rate?prevThickness=15.0&prevDate=2022-12-07&lastThickness=14.5&lastDate=2023-12-07
```

**Section sources**
- [MeasurementsController.cs](file://src/OilErp.App/Controllers/MeasurementsController.cs#L218-L265)

## Analytics & Reporting
The Analytics & Reporting endpoints provide insights into asset risk, system performance, and corrosion trends across the organization.

### GET /api/analytics/top-risk
Retrieves the top risk assets in the system, ordered by risk level.

**Parameters**
- `limit` (query, optional): Number of top risk assets to return (1-100, default: 10)

**Response Codes**
- 200: Top risk assets retrieved successfully
- 400: Invalid limit parameter
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "totalCount": 0,
    "assets": [
      {
        "assetCode": "string",
        "name": "string",
        "plantCode": "string",
        "riskLevel": "string",
        "corrosionRate": 0,
        "lastThickness": 0,
        "lastMeasurementDate": "string",
        "inspectionPriority": 0,
        "daysUntilNextInspection": 0
      }
    ],
    "generatedAt": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/analytics/top-risk?limit=5
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L38-L94)

### GET /api/analytics/summary
Retrieves a comprehensive summary of system analytics including risk levels, work order status, and other key metrics.

**Response Codes**
- 200: Analytics summary retrieved successfully
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "totalAssets": 0,
    "highRiskAssets": 0,
    "criticalRiskAssets": 0,
    "openWorkOrders": 0,
    "overdueWorkOrders": 0,
    "unresolvedDefects": 0,
    "averageCorrosionRate": 0,
    "generatedAt": "string",
    "plantSummaries": [
      {
        "plantCode": "string",
        "plantName": "string",
        "assetCount": 0,
        "highRiskAssetCount": 0,
        "openWorkOrderCount": 0,
        "averageCorrosionRate": 0
      }
    ]
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/analytics/summary
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L101-L159)

### GET /api/analytics/risk-level/{riskLevel}
Retrieves all assets with a specific risk level.

**Parameters**
- `riskLevel` (path): The risk level to filter by (Low, Medium, High, Critical, Minimal)

**Response Codes**
- 200: Assets retrieved successfully
- 400: Invalid risk level
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": [
    {
      "assetCode": "string",
      "name": "string",
      "plantCode": "string",
      "riskLevel": "string",
      "corrosionRate": 0,
      "lastThickness": 0,
      "lastMeasurementDate": "string",
      "inspectionPriority": 0,
      "daysUntilNextInspection": 0
    }
  ],
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```
GET /api/analytics/risk-level/High
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L166-L224)

### GET /api/analytics/corrosion
Retrieves corrosion analytics data across all assets.

**Response Codes**
- 200: Corrosion analytics retrieved successfully
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "totalAssets": 0,
    "averageCorrosionRate": 0,
    "maxCorrosionRate": 0,
    "trendData": [
      {
        "assetCode": "string",
        "corrosionRate": 0,
        "date": "string",
        "trend": "string"
      }
    ],
    "assets": [
      {
        "corrosionRate": 0,
        "thicknessLoss": 0,
        "measurementPeriodDays": 0,
        "isAccelerating": true,
        "calculatedAt": "string"
      }
    ],
    "generatedAt": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L231-L270)

### POST /api/analytics/risk-policies
Creates or updates a risk assessment policy with custom thresholds.

**Request Body**
```json
{
  "name": "string",
  "thresholdLow": 0,
  "thresholdMedium": 0,
  "thresholdHigh": 0,
  "description": "string"
}
```

**Response Codes**
- 201: Risk policy created or updated successfully
- 400: Invalid request data
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "name": "string",
    "thresholdLow": 0,
    "thresholdMedium": 0,
    "thresholdHigh": 0,
    "description": "string",
    "createdAt": "string",
    "updatedAt": "string",
    "isActive": true
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```json
POST /api/analytics/risk-policies
{
  "name": "Standard Policy",
  "thresholdLow": 2.0,
  "thresholdMedium": 4.0,
  "thresholdHigh": 6.0,
  "description": "Standard corrosion rate thresholds"
}
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L277-L334)

### GET /api/analytics/risk-policies
Retrieves all defined risk policies in the system.

**Response Codes**
- 200: Risk policies retrieved successfully
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": [
    {
      "name": "string",
      "thresholdLow": 0,
      "thresholdMedium": 0,
      "thresholdHigh": 0,
      "description": "string",
      "createdAt": "string",
      "updatedAt": "string",
      "isActive": true
    }
  ],
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Section sources**
- [AnalyticsController.cs](file://src/OilErp.App/Controllers/AnalyticsController.cs#L341-L370)

## Work Order Management
The Work Order Management endpoints handle the creation, retrieval, and updating of work orders for maintenance and inspection activities.

### POST /api/work-orders
Creates a new work order for a specific asset.

**Request Body**
```json
{
  "assetCode": "string",
  "workOrderNumber": "string",
  "description": "string",
  "workType": "string",
  "priority": "string",
  "assignedTo": "string",
  "scheduledDate": "string",
  "estimatedHours": 0,
  "notes": "string"
}
```

**Response Codes**
- 201: Work order created successfully
- 400: Invalid request data or asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": {
    "id": "string",
    "assetCode": "string",
    "workOrderNumber": "string",
    "description": "string",
    "workType": "string",
    "priority": "string",
    "status": "string",
    "assignedTo": "string",
    "createdAt": "string",
    "scheduledDate": "string",
    "completedAt": "string",
    "estimatedHours": 0,
    "actualHours": 0,
    "notes": "string",
    "completionNotes": "string"
  },
  "message": "string",
  "errors": [],
  "timestamp": "string"
}
```

**Example Request**
```json
POST /api/work-orders
{
  "assetCode": "T-1001",
  "workOrderNumber": "WO-2023-001",
  "description": "Inspect and clean distillation tower",
  "workType": "Inspection",
  "priority": "High",
  "scheduledDate": "2023-12-15T08:00:00Z",
  "estimatedHours": 8
}
```

**Section sources**
- [WorkOrdersController.cs](file://src/OilErp.App/Controllers/WorkOrdersController.cs#L34-L103)

### GET /api/work-orders/{id}
Retrieves detailed information about a specific work order by its ID.

**Parameters**
- `id` (path): The unique identifier of the work order (GUID)

**Response Codes**
- 200: Work order retrieved successfully
- 404: Work order not found
- 500: Internal server error

**Response Schema**
Same as POST /api/work-orders response

**Example Request**
```
GET /api/work-orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Section sources**
- [WorkOrdersController.cs](file://src/OilErp.App/Controllers/WorkOrdersController.cs#L110-L147)

### GET /api/work-orders/asset/{assetCode}
Retrieves all work orders associated with a specific asset.

**Parameters**
- `assetCode` (path): The unique code identifying the asset

**Response Codes**
- 200: Work orders retrieved successfully
- 404: Asset not found
- 500: Internal server error

**Response Schema**
```json
{
  "success": true,
  "data": [
    {
      "id": "string",
      "assetCode": "string",
      "workOrderNumber": "string",
      "description": "string",
      "workType": "string",
      "priority": "string",
      "status": "string",
      "assignedTo": "string",
      "createdAt": "string",
      "scheduledDate": "string",
      "completedAt": "string",
      "estimatedHours": 0,
      "actualHours": 0,
      "notes": "string",
      "completionNotes": "string