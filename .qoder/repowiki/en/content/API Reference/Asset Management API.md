# Asset Management API

<cite>
**Referenced Files in This Document**   
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs)
- [AssetRequest.cs](file://src/OilErp.App/Models/ApiModels.cs)
- [AssetResponse.cs](file://src/OilErp.App/Models/ApiModels.cs)
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs)
- [AssetRepository.cs](file://src/OilErp.Data/Repositories/AssetRepository.cs)
- [AssetValidators.cs](file://src/OilErp.App/Validators/AssetValidators.cs)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [Core Models](#core-models)
3. [API Endpoints](#api-endpoints)
4. [Error Handling](#error-handling)
5. [Validation Rules](#validation-rules)
6. [Downstream Processes](#downstream-processes)
7. [Usage Examples](#usage-examples)

## Introduction
The Asset Management API provides comprehensive functionality for managing physical assets within the Oil ERP system. This API enables users to create, retrieve, and update asset information across different plants in the organization. The system supports critical operations such as asset creation, detailed information retrieval by asset code, and plant-based asset queries. Each operation is designed to maintain data integrity while providing seamless integration with downstream processes like risk assessment and measurement tracking.

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L10-L322)

## Core Models

### AssetRequest Model
The `AssetRequest` model defines the structure for creating or updating assets in the system. This request object contains all necessary information to properly register an asset within the ERP system.

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| AssetCode | string | Yes | Max 50 characters, uppercase letters, numbers, underscores, hyphens | Unique identifier for the asset |
| Name | string | Yes | 1-200 characters | Descriptive name of the asset |
| AssetType | string | Yes | 1-50 characters, must be valid type | Classification of the asset |
| PlantCode | string | Yes | 1-10 characters, must be valid plant | Manufacturing facility where asset is located |
| Description | string | No | Max 500 characters | Additional details about the asset |
| Location | string | No | Max 100 characters | Physical location within the plant |
| Status | string | No | Max 20 characters, must be valid status | Current operational status |

Valid Asset Types: Pipeline, Tank, Pump, Vessel, Equipment  
Valid Plant Codes: ANPZ, KRNPZ  
Valid Status Values: Active, Inactive, Maintenance, Decommissioned

**Section sources**
- [AssetRequest.cs](file://src/OilErp.App/Models/ApiModels.cs#L8-L34)
- [AssetValidators.cs](file://src/OilErp.App/Validators/AssetValidators.cs#L10-L50)

### AssetResponse Model
The `AssetResponse` model represents the structure of asset information returned by the API. This response object provides comprehensive details about an asset.

| Field | Type | Description |
|-------|------|-------------|
| AssetCode | string | Unique identifier for the asset |
| Name | string | Descriptive name of the asset |
| AssetType | string | Classification of the asset |
| PlantCode | string | Manufacturing facility where asset is located |
| Description | string | Additional details about the asset |
| Location | string | Physical location within the plant |
| Status | string | Current operational status |
| CreatedAt | DateTime | Timestamp when the asset was created |
| UpdatedAt | DateTime | Timestamp when the asset was last modified |

**Section sources**
- [AssetResponse.cs](file://src/OilErp.App/Models/ApiModels.cs#L39-L50)
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L276-L288)

## API Endpoints

### GET /api/assets/{assetCode}
Retrieves detailed information about a specific asset using its unique asset code.

**Parameters**
- `assetCode` (path): The unique identifier of the asset to retrieve

**HTTP Method**: GET  
**URL Pattern**: `/api/assets/{assetCode}`  
**Authentication**: Required (JWT Bearer token)  
**Response Codes**:
- 200 OK: Asset found and returned successfully
- 404 Not Found: Asset with specified code does not exist
- 500 Internal Server Error: Error occurred during retrieval

**Success Response (200)**
```json
{
  "success": true,
  "data": {
    "assetCode": "PIPE-001",
    "name": "Main Crude Oil Pipeline",
    "assetType": "Pipeline",
    "plantCode": "ANPZ",
    "description": "Primary pipeline for crude oil transportation",
    "createdAt": "2023-01-15T10:30:00Z",
    "updatedAt": "2023-06-20T14:45:00Z"
  }
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L35-L75)

### POST /api/assets
Creates a new asset or updates an existing asset in the system.

**Request Body**: AssetRequest object

**HTTP Method**: POST  
**URL Pattern**: `/api/assets`  
**Authentication**: Required (JWT Bearer token)  
**Response Codes**:
- 201 Created: Asset created or updated successfully
- 400 Bad Request: Invalid request data
- 500 Internal Server Error: Error occurred during creation/update

**Success Response (201)**
```json
{
  "success": true,
  "data": {
    "assetCode": "TANK-005",
    "name": "Storage Tank 5",
    "assetType": "Tank",
    "plantCode": "KRNPZ",
    "description": "Secondary crude oil storage",
    "createdAt": "2023-08-10T09:15:00Z",
    "updatedAt": "2023-08-10T09:15:00Z"
  },
  "message": "Asset created/updated successfully"
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L188-L237)

### GET /api/assets/plant/{plantCode}
Retrieves all assets associated with a specific plant.

**Parameters**
- `plantCode` (path): The code of the plant to retrieve assets for

**HTTP Method**: GET  
**URL Pattern**: `/api/assets/plant/{plantCode}`  
**Authentication**: Required (JWT Bearer token)  
**Response Codes**:
- 200 OK: Assets retrieved successfully
- 500 Internal Server Error: Error occurred during retrieval

**Success Response (200)**
```json
{
  "success": true,
  "data": [
    {
      "assetCode": "PIPE-001",
      "name": "Main Crude Oil Pipeline",
      "assetType": "Pipeline",
      "plantCode": "ANPZ",
      "description": "Primary pipeline for crude oil transportation",
      "createdAt": "2023-01-15T10:30:00Z",
      "updatedAt": "2023-06-20T14:45:00Z"
    },
    {
      "assetCode": "PUMP-003",
      "name": "Transfer Pump 3",
      "assetType": "Pump",
      "plantCode": "ANPZ",
      "description": "Pump for transferring crude between tanks",
      "createdAt": "2023-03-22T11:20:00Z",
      "updatedAt": "2023-03-22T11:20:00Z"
    }
  ]
}
```

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L245-L274)

## Error Handling
The Asset Management API implements comprehensive error handling to provide clear feedback for various failure scenarios.

### 404 Not Found
Returned when attempting to retrieve an asset that does not exist in the system.

**Response Format**
```json
{
  "success": false,
  "message": "Asset with code 'INVALID-CODE' not found"
}
```

**Common Causes**
- Invalid asset code provided in the request
- Asset has been deleted from the system
- Typographical errors in the asset code

### 400 Bad Request
Returned when the request contains invalid data or fails validation.

**Response Format**
```json
{
  "success": false,
  "message": "An error occurred while creating/updating the asset",
  "errors": ["Asset code is required", "Plant code is required"]
}
```

**Common Causes**
- Missing required fields in the request
- Invalid data format or type
- Violation of field constraints (length, pattern, etc.)

### 500 Internal Server Error
Returned when an unexpected error occurs during request processing.

**Response Format**
```json
{
  "success": false,
  "message": "An error occurred while retrieving the asset",
  "errors": ["Database connection failed"]
}
```

**Common Causes**
- Database connectivity issues
- Unexpected application exceptions
- System resource constraints

**Section sources**
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L37-L42)
- [AssetsController.cs](file://src/OilErp.App/Controllers/AssetsController.cs#L190-L195)

## Validation Rules
The API enforces strict validation rules to ensure data integrity and consistency across the system.

### Asset Code Validation
- Must be 1-50 characters long
- Can only contain uppercase letters, numbers, underscores, and hyphens
- Must be unique across the system
- Example: `PIPE-001`, `TANK_005`, `PUMP-003`

### Plant Code Validation
- Must be either "ANPZ" or "KRNPZ"
- Case-insensitive comparison
- Ensures assets are only assigned to valid manufacturing facilities

### Asset Type Validation
- Must be one of: Pipeline, Tank, Pump, Vessel, Equipment
- Case-insensitive comparison
- Maintains consistency in asset classification

### Field Length Constraints
- Name: 1-200 characters
- Description: Up to 500 characters
- Location: Up to 100 characters
- Status: Up to 20 characters

**Section sources**
- [AssetValidators.cs](file://src/OilErp.App/Validators/AssetValidators.cs#L10-L50)

## Downstream Processes
Asset creation and modification trigger several important downstream processes that enhance the overall system functionality.

### Risk Assessment
When an asset is created or updated, the system automatically initiates a risk assessment process:
1. The asset information is validated and stored
2. A risk assessment is triggered using the default policy
3. Corrosion rate calculations are performed based on historical data
4. Risk level is determined and stored in the analytics system

### Measurement Tracking
The asset creation process integrates with the measurement tracking system:
1. Measurement points are initialized for the new asset
2. Historical measurement data is linked to the asset
3. Future measurement submissions are associated with the asset code
4. Corrosion analytics are updated based on new measurements

### Data Synchronization
Changes to asset information are synchronized across multiple systems:
- Central database is updated immediately
- Analytics warehouse is updated asynchronously
- Plant-specific data stores receive updates via database triggers
- Reporting systems reflect changes in near real-time

**Section sources**
- [AssetService.cs](file://src/OilErp.Domain/Services/AssetService.cs#L13-L196)
- [AssetRepository.cs](file://src/OilErp.Data/Repositories/AssetRepository.cs#L53-L66)

## Usage Examples

### Creating a New Pipeline
**Scenario**: Adding a new pipeline to the ANPZ facility

**Request**
```http
POST /api/assets
Content-Type: application/json
Authorization: Bearer <token>

{
  "assetCode": "PIPE-005",
  "name": "Secondary Crude Oil Pipeline",
  "assetType": "Pipeline",
  "plantCode": "ANPZ",
  "description": "Backup pipeline for crude oil transportation",
  "location": "North Sector, Zone 3"
}
```

**Response**
```json
HTTP/1.1 201 Created
{
  "success": true,
  "data": {
    "assetCode": "PIPE-005",
    "name": "Secondary Crude Oil Pipeline",
    "assetType": "Pipeline",
    "plantCode": "ANPZ",
    "description": "Backup pipeline for crude oil transportation",
    "location": "North Sector, Zone 3",
    "createdAt": "2023-09-15T14:30:00Z",
    "updatedAt": "2023-09-15T14:30:00Z"
  },
  "message": "Asset created/updated successfully"
}
```

### Retrieving Assets for KRNPZ Plant
**Scenario**: Getting all assets at the KRNPZ facility for maintenance planning

**Request**
```http
GET /api/assets/plant/KRNPZ
Authorization: Bearer <token>
```

**Response**
```json
HTTP/1.1 200 OK
{
  "success": true,
  "data": [
    {
      "assetCode": "TANK-001",
      "name": "Primary Storage Tank",
      "assetType": "Tank",
      "plantCode": "KRNPZ",
      "description": "Main crude oil storage facility",
      "createdAt": "2022-11-05T08:15:00Z",
      "updatedAt": "2023-07-12T16:20:00Z"
    },
    {
      "assetCode": "PUMP-001",
      "name": "Main Transfer Pump",
      "assetType": "Pump",
      "plantCode": "KRNPZ",
      "description": "Primary pump for tank transfers",
      "createdAt": "2022-11-05T08:15:00Z",
      "updatedAt": "2023-05-18T10:45:00Z"
    }
  ]
}
```

### Handling Invalid Asset Code
**Scenario**: Attempting to retrieve a non-existent asset

**Request**
```http
GET /api/assets/INVALID-CODE
Authorization: Bearer <token>
```

**Response**
```json
HTTP/1.1 404 Not Found
{
  "success": false,
  "message": "Asset with code 'INVALID-CODE' not found"
}
```