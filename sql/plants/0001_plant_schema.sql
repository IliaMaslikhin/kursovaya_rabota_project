-- Plant Database Schema
-- Creates local asset tables and foreign data wrapper connections

BEGIN;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS local_assets;
CREATE SCHEMA IF NOT EXISTS measurements;
CREATE SCHEMA IF NOT EXISTS maintenance;
CREATE SCHEMA IF NOT EXISTS events;
CREATE SCHEMA IF NOT EXISTS sync;

-- Local asset instances (mirrors central registry)
CREATE TABLE local_assets.assets (
    id VARCHAR(50) PRIMARY KEY,
    tag_number VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500),
    asset_type VARCHAR(50),
    plant_code VARCHAR(10) NOT NULL,
    status VARCHAR(20) DEFAULT 'Active' CHECK (status IN ('Active', 'Inactive', 'Decommissioned')),
    installation_date DATE,
    last_inspection_date DATE,
    next_inspection_date DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Asset segments with detailed properties
CREATE TABLE local_assets.segments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES local_assets.assets(id) ON DELETE CASCADE,
    segment_name VARCHAR(100) NOT NULL,
    length_m DECIMAL(10,3) NOT NULL CHECK (length_m > 0),
    diameter_mm DECIMAL(8,2),
    wall_thickness_mm DECIMAL(6,3),
    material_code VARCHAR(20),
    coating_code VARCHAR(20),
    installation_date DATE,
    operating_pressure_bar DECIMAL(8,2),
    operating_temperature_c DECIMAL(6,2),
    fluid_code VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Measurement points on segments
CREATE TABLE measurements.points (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    segment_id UUID NOT NULL REFERENCES local_assets.segments(id) ON DELETE CASCADE,
    point_name VARCHAR(100) NOT NULL,
    distance_from_start DECIMAL(10,3) NOT NULL CHECK (distance_from_start >= 0),
    measurement_type VARCHAR(50) NOT NULL,
    measurement_method VARCHAR(50),
    frequency_days INTEGER,
    last_measured_at TIMESTAMP,
    next_measurement_due DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- High-frequency measurement readings
CREATE TABLE measurements.readings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    point_id UUID NOT NULL REFERENCES measurements.points(id) ON DELETE CASCADE,
    value DECIMAL(15,6) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    measured_at TIMESTAMP NOT NULL,
    operator_id VARCHAR(50),
    equipment_id VARCHAR(50),
    environmental_conditions JSONB,
    quality_flag VARCHAR(20) DEFAULT 'Good' CHECK (quality_flag IN ('Good', 'Questionable', 'Bad')),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Defect tracking with detailed information
CREATE TABLE maintenance.defects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES local_assets.assets(id),
    defect_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL CHECK (severity IN ('Low', 'Medium', 'High', 'Critical')),
    description TEXT,
    location VARCHAR(200),
    dimensions JSONB, -- length, width, depth, etc.
    discovered_at TIMESTAMP NOT NULL,
    discovered_by VARCHAR(100),
    discovery_method VARCHAR(50),
    risk_assessment TEXT,
    immediate_action_required BOOLEAN DEFAULT false,
    is_resolved BOOLEAN DEFAULT false,
    resolved_at TIMESTAMP,
    resolution TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Work orders with detailed workflow
CREATE TABLE maintenance.work_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES local_assets.assets(id),
    defect_id UUID REFERENCES maintenance.defects(id),
    wo_number VARCHAR(50) UNIQUE NOT NULL,
    work_type VARCHAR(50) NOT NULL,
    priority VARCHAR(20) DEFAULT 'Medium' CHECK (priority IN ('Low', 'Medium', 'High', 'Critical')),
    status VARCHAR(20) DEFAULT 'Scheduled' CHECK (status IN ('Scheduled', 'In Progress', 'Completed', 'Cancelled', 'On Hold')),
    description TEXT,
    scheduled_date TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    assigned_to VARCHAR(100),
    crew_size INTEGER,
    estimated_hours DECIMAL(8,2),
    actual_hours DECIMAL(8,2),
    cost_estimate DECIMAL(12,2),
    actual_cost DECIMAL(12,2),
    materials_used JSONB,
    completion_notes TEXT,
    safety_notes TEXT,
    permit_required BOOLEAN DEFAULT false,
    permit_number VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Inspection records
CREATE TABLE maintenance.inspections (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id VARCHAR(50) NOT NULL REFERENCES local_assets.assets(id),
    inspection_type VARCHAR(50) NOT NULL,
    inspection_date TIMESTAMP NOT NULL,
    inspector VARCHAR(100),
    method VARCHAR(50),
    scope TEXT,
    findings TEXT,
    overall_condition VARCHAR(20) CHECK (overall_condition IN ('Excellent', 'Good', 'Fair', 'Poor', 'Critical')),
    recommendations TEXT,
    next_inspection_due DATE,
    certification_number VARCHAR(50),
    report_file_path VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    synced_at TIMESTAMP
);

-- Local events for synchronization with central database
CREATE TABLE events.local_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(50) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(100) NOT NULL,
    operation VARCHAR(20) NOT NULL CHECK (operation IN ('INSERT', 'UPDATE', 'DELETE')),
    event_data JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    processed BOOLEAN DEFAULT FALSE,
    processed_at TIMESTAMP,
    error_message TEXT
);

-- Sync status tracking
CREATE TABLE sync.sync_batches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_type VARCHAR(50) NOT NULL,
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    status VARCHAR(20) DEFAULT 'Running' CHECK (status IN ('Running', 'Completed', 'Failed')),
    records_processed INTEGER DEFAULT 0,
    records_failed INTEGER DEFAULT 0,
    error_summary TEXT
);

-- Create indexes for performance
CREATE INDEX idx_assets_tag_number ON local_assets.assets(tag_number);
CREATE INDEX idx_assets_plant_code ON local_assets.assets(plant_code);
CREATE INDEX idx_assets_status ON local_assets.assets(status);
CREATE INDEX idx_segments_asset_id ON local_assets.segments(asset_id);
CREATE INDEX idx_segments_material_code ON local_assets.segments(material_code);
CREATE INDEX idx_points_segment_id ON measurements.points(segment_id);
CREATE INDEX idx_points_measurement_type ON measurements.points(measurement_type);
CREATE INDEX idx_readings_point_id ON measurements.readings(point_id);
CREATE INDEX idx_readings_measured_at ON measurements.readings(measured_at);
CREATE INDEX idx_readings_operator_id ON measurements.readings(operator_id);
CREATE INDEX idx_defects_asset_id ON maintenance.defects(asset_id);
CREATE INDEX idx_defects_severity ON maintenance.defects(severity);
CREATE INDEX idx_defects_discovered_at ON maintenance.defects(discovered_at);
CREATE INDEX idx_defects_is_resolved ON maintenance.defects(is_resolved);
CREATE INDEX idx_work_orders_asset_id ON maintenance.work_orders(asset_id);
CREATE INDEX idx_work_orders_status ON maintenance.work_orders(status);
CREATE INDEX idx_work_orders_scheduled_date ON maintenance.work_orders(scheduled_date);
CREATE INDEX idx_work_orders_assigned_to ON maintenance.work_orders(assigned_to);
CREATE INDEX idx_inspections_asset_id ON maintenance.inspections(asset_id);
CREATE INDEX idx_inspections_inspection_date ON maintenance.inspections(inspection_date);
CREATE INDEX idx_local_events_processed ON events.local_events(processed);
CREATE INDEX idx_local_events_created_at ON events.local_events(created_at);

-- Create triggers for automatic event generation (for sync)
CREATE OR REPLACE FUNCTION generate_sync_event()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO events.local_events (event_type, entity_type, entity_id, operation, event_data)
        VALUES (TG_TABLE_NAME || '_changed', TG_TABLE_NAME, NEW.id::text, 'INSERT', row_to_json(NEW));
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO events.local_events (event_type, entity_type, entity_id, operation, event_data)
        VALUES (TG_TABLE_NAME || '_changed', TG_TABLE_NAME, NEW.id::text, 'UPDATE', 
                json_build_object('old', row_to_json(OLD), 'new', row_to_json(NEW)));
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO events.local_events (event_type, entity_type, entity_id, operation, event_data)
        VALUES (TG_TABLE_NAME || '_changed', TG_TABLE_NAME, OLD.id::text, 'DELETE', row_to_json(OLD));
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Create triggers for sync event generation
CREATE TRIGGER sync_assets_trigger
    AFTER INSERT OR UPDATE OR DELETE ON local_assets.assets
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_segments_trigger
    AFTER INSERT OR UPDATE OR DELETE ON local_assets.segments
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_points_trigger
    AFTER INSERT OR UPDATE OR DELETE ON measurements.points
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_readings_trigger
    AFTER INSERT OR UPDATE OR DELETE ON measurements.readings
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_defects_trigger
    AFTER INSERT OR UPDATE OR DELETE ON maintenance.defects
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_work_orders_trigger
    AFTER INSERT OR UPDATE OR DELETE ON maintenance.work_orders
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

CREATE TRIGGER sync_inspections_trigger
    AFTER INSERT OR UPDATE OR DELETE ON maintenance.inspections
    FOR EACH ROW EXECUTE FUNCTION generate_sync_event();

-- Create a view for asset health summary
CREATE VIEW local_assets.asset_health_summary AS
SELECT 
    a.id,
    a.tag_number,
    a.description,
    a.status,
    COUNT(d.id) as total_defects,
    COUNT(CASE WHEN d.severity = 'Critical' AND NOT d.is_resolved THEN 1 END) as critical_defects,
    COUNT(CASE WHEN d.severity = 'High' AND NOT d.is_resolved THEN 1 END) as high_defects,
    COUNT(CASE WHEN wo.status IN ('Scheduled', 'In Progress') THEN 1 END) as active_work_orders,
    MAX(i.inspection_date) as last_inspection_date,
    MIN(CASE WHEN mp.next_measurement_due > CURRENT_DATE THEN mp.next_measurement_due END) as next_measurement_due
FROM local_assets.assets a
LEFT JOIN maintenance.defects d ON a.id = d.asset_id
LEFT JOIN maintenance.work_orders wo ON a.id = wo.asset_id
LEFT JOIN maintenance.inspections i ON a.id = i.asset_id
LEFT JOIN local_assets.segments s ON a.id = s.asset_id
LEFT JOIN measurements.points mp ON s.id = mp.segment_id
GROUP BY a.id, a.tag_number, a.description, a.status;

COMMIT;