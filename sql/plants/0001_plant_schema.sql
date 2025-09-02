-- Plant Database Schema
-- Creates local asset tables and foreign data wrapper connections

BEGIN;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS local_assets;
CREATE SCHEMA IF NOT EXISTS measurements;
CREATE SCHEMA IF NOT EXISTS maintenance;
CREATE SCHEMA IF NOT EXISTS events;

-- Local asset instances
CREATE TABLE local_assets.assets (
    id VARCHAR(50) PRIMARY KEY,
    tag_number VARCHAR(100) NOT NULL,
    description VARCHAR(500),
    asset_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Asset segments
CREATE TABLE local_assets.segments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) REFERENCES local_assets.assets(id),
    segment_name VARCHAR(100) NOT NULL,
    length_m DECIMAL(10,4),
    material_code VARCHAR(20),
    coating_code VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Measurement points
CREATE TABLE measurements.points (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    segment_id UUID REFERENCES local_assets.segments(id),
    point_name VARCHAR(100) NOT NULL,
    distance_from_start DECIMAL(10,4),
    measurement_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Measurements
CREATE TABLE measurements.readings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    point_id UUID REFERENCES measurements.points(id),
    value DECIMAL(12,6),
    unit VARCHAR(20),
    measured_at TIMESTAMP NOT NULL,
    operator_id VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Defects
CREATE TABLE maintenance.defects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) REFERENCES local_assets.assets(id),
    defect_type VARCHAR(50),
    severity VARCHAR(20),
    description TEXT,
    discovered_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Work orders
CREATE TABLE maintenance.work_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) REFERENCES local_assets.assets(id),
    wo_number VARCHAR(50) UNIQUE NOT NULL,
    work_type VARCHAR(50),
    status VARCHAR(20) DEFAULT 'OPEN',
    scheduled_date DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Local events for synchronization
CREATE TABLE events.local_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(50) NOT NULL,
    aggregate_id VARCHAR(100) NOT NULL,
    event_data JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    processed BOOLEAN DEFAULT FALSE
);

COMMIT;