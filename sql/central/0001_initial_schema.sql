-- Central Database Initial Schema
-- Creates the master asset registry and catalog tables

BEGIN;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS catalogs;
CREATE SCHEMA IF NOT EXISTS assets;
CREATE SCHEMA IF NOT EXISTS risk;
CREATE SCHEMA IF NOT EXISTS analytics;
CREATE SCHEMA IF NOT EXISTS incidents;
CREATE SCHEMA IF NOT EXISTS sync;

-- Materials catalog
CREATE TABLE catalogs.materials (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    density DECIMAL(8,4),
    type VARCHAR(50),
    specification TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Coatings catalog
CREATE TABLE catalogs.coatings (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type VARCHAR(50),
    manufacturer VARCHAR(100),
    specification TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Fluids catalog
CREATE TABLE catalogs.fluids (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    corrosivity VARCHAR(20),
    density DECIMAL(8,4),
    viscosity DECIMAL(8,4),
    pressure_rating VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Global asset registry
CREATE TABLE assets.global_assets (
    id VARCHAR(50) PRIMARY KEY,
    tag_number VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500),
    plant_code VARCHAR(10) NOT NULL,
    asset_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Asset segments (detailed structure)
CREATE TABLE segments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES assets.global_assets(id),
    segment_name VARCHAR(100) NOT NULL,
    length_m DECIMAL(10,3) NOT NULL CHECK (length_m > 0),
    material_code VARCHAR(20) REFERENCES catalogs.materials(code),
    coating_code VARCHAR(20) REFERENCES catalogs.coatings(code),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Measurement points on segments
CREATE TABLE measurement_points (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    segment_id UUID NOT NULL REFERENCES segments(id) ON DELETE CASCADE,
    point_name VARCHAR(100) NOT NULL,
    distance_from_start DECIMAL(10,3) NOT NULL CHECK (distance_from_start >= 0),
    measurement_type VARCHAR(50) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Readings from measurement points
CREATE TABLE readings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    point_id UUID NOT NULL REFERENCES measurement_points(id) ON DELETE CASCADE,
    value DECIMAL(15,6) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    measured_at TIMESTAMP NOT NULL,
    operator_id VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    notes TEXT,
    is_valid BOOLEAN DEFAULT true
);

-- Asset defects tracking
CREATE TABLE defects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES assets.global_assets(id),
    defect_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL CHECK (severity IN ('Low', 'Medium', 'High', 'Critical')),
    description TEXT,
    discovered_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    discovered_by VARCHAR(100),
    location VARCHAR(200),
    is_resolved BOOLEAN DEFAULT false,
    resolved_at TIMESTAMP,
    resolution TEXT
);

-- Work orders for maintenance
CREATE TABLE work_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES assets.global_assets(id),
    wo_number VARCHAR(50) NOT NULL UNIQUE,
    work_type VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Scheduled' CHECK (status IN ('Scheduled', 'In Progress', 'Completed', 'Cancelled')),
    scheduled_date TIMESTAMP NOT NULL,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    description TEXT,
    assigned_to VARCHAR(100),
    priority VARCHAR(20) CHECK (priority IN ('Low', 'Medium', 'High', 'Critical')),
    estimated_hours DECIMAL(8,2),
    actual_hours DECIMAL(8,2),
    completion_notes TEXT,
    defect_id UUID REFERENCES defects(id)
);

-- Risk policies
CREATE TABLE risk.policies (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    thresholds JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Risk assessments
CREATE TABLE risk.assessments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES assets.global_assets(id),
    policy_id INTEGER REFERENCES risk.policies(id),
    risk_level VARCHAR(20) NOT NULL,
    risk_score DECIMAL(5,2),
    assessment_date TIMESTAMP NOT NULL,
    assessor VARCHAR(100),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Incident tracking
CREATE TABLE incidents.incidents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) REFERENCES assets.global_assets(id),
    incident_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    occurred_at TIMESTAMP NOT NULL,
    reported_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    reported_by VARCHAR(100),
    status VARCHAR(20) DEFAULT 'Open' CHECK (status IN ('Open', 'Investigating', 'Resolved', 'Closed')),
    impact_assessment TEXT,
    corrective_actions TEXT,
    lessons_learned TEXT
);

-- Analytics aggregations
CREATE TABLE analytics.asset_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES assets.global_assets(id),
    metric_type VARCHAR(50) NOT NULL,
    metric_value DECIMAL(15,6),
    measurement_unit VARCHAR(20),
    calculated_at TIMESTAMP NOT NULL,
    period_start TIMESTAMP,
    period_end TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Plant sync status
CREATE TABLE sync.sync_status (
    id SERIAL PRIMARY KEY,
    plant_code VARCHAR(10) NOT NULL,
    last_sync_at TIMESTAMP,
    sync_status VARCHAR(20) DEFAULT 'Pending',
    records_synced INTEGER DEFAULT 0,
    errors TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance
CREATE INDEX idx_global_assets_plant_code ON assets.global_assets(plant_code);
CREATE INDEX idx_global_assets_asset_type ON assets.global_assets(asset_type);
CREATE INDEX idx_segments_asset_id ON segments(asset_id);
CREATE INDEX idx_measurement_points_segment_id ON measurement_points(segment_id);
CREATE INDEX idx_readings_point_id ON readings(point_id);
CREATE INDEX idx_readings_measured_at ON readings(measured_at);
CREATE INDEX idx_defects_asset_id ON defects(asset_id);
CREATE INDEX idx_defects_severity ON defects(severity);
CREATE INDEX idx_defects_is_resolved ON defects(is_resolved);
CREATE INDEX idx_work_orders_asset_id ON work_orders(asset_id);
CREATE INDEX idx_work_orders_status ON work_orders(status);
CREATE INDEX idx_work_orders_scheduled_date ON work_orders(scheduled_date);
CREATE INDEX idx_risk_assessments_asset_id ON risk.assessments(asset_id);
CREATE INDEX idx_incidents_asset_id ON incidents.incidents(asset_id);
CREATE INDEX idx_incidents_occurred_at ON incidents.incidents(occurred_at);
CREATE INDEX idx_asset_metrics_asset_id ON analytics.asset_metrics(asset_id);
CREATE INDEX idx_asset_metrics_calculated_at ON analytics.asset_metrics(calculated_at);

-- Insert some default data
INSERT INTO catalogs.materials (code, name, density, type, specification) VALUES
('CS_API5L', 'Carbon Steel API 5L', 7850.0, 'Steel', 'API 5L Grade B'),
('SS_316L', 'Stainless Steel 316L', 8000.0, 'Stainless Steel', 'ASTM A312 Grade 316L'),
('AL_6061', 'Aluminum 6061', 2700.0, 'Aluminum', 'ASTM B221 6061-T6');

INSERT INTO catalogs.coatings (code, name, type, manufacturer, specification) VALUES
('3LPE', '3-Layer Polyethylene', 'External', 'Various', 'DIN 30678'),
('FBE', 'Fusion Bonded Epoxy', 'External', 'Various', 'CSA Z245.20'),
('INTERNAL_EPOXY', 'Internal Epoxy Coating', 'Internal', 'Various', 'AWWA C210');

INSERT INTO catalogs.fluids (code, name, corrosivity, density, viscosity, pressure_rating) VALUES
('CRUDE_LIGHT', 'Light Crude Oil', 'Low', 850.0, 10.0, 'ANSI 600'),
('CRUDE_HEAVY', 'Heavy Crude Oil', 'Medium', 950.0, 50.0, 'ANSI 600'),
('NAT_GAS', 'Natural Gas', 'Low', 0.8, 0.01, 'ANSI 1500'),
('H2S_GAS', 'Sour Gas (H2S)', 'High', 1.2, 0.02, 'ANSI 1500');

INSERT INTO risk.policies (name, description, thresholds) VALUES
('Wall Thickness Policy', 'Minimum wall thickness requirements for pipelines', 
 '{"wall_thickness": {"critical": 3.0, "high": 5.0, "medium": 7.0, "low": 9.0, "unit": "mm"}}'),
('Corrosion Rate Policy', 'Maximum allowable corrosion rates', 
 '{"corrosion_rate": {"critical": 1.0, "high": 0.5, "medium": 0.25, "low": 0.1, "unit": "mm/year"}}');

COMMIT;