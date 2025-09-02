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
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Coatings catalog
CREATE TABLE catalogs.coatings (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Fluids catalog
CREATE TABLE catalogs.fluids (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    corrosivity VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Global asset registry
CREATE TABLE assets.global_assets (
    id VARCHAR(50) PRIMARY KEY,
    tag_number VARCHAR(100) NOT NULL,
    description VARCHAR(500),
    plant_code VARCHAR(10) NOT NULL,
    asset_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Risk policies
CREATE TABLE risk.policies (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    thresholds JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- TODO: Add remaining tables for analytics, incidents, and sync

COMMIT;