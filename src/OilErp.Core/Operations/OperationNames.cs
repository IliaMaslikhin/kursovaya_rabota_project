namespace OilErp.Core.Operations;

/// <summary>
/// Operation name constants for database operations
/// </summary>
public static class OperationNames
{
    /// <summary>
    /// Plant measurement batch insertion operations
    /// </summary>
    public static class Plant
    {
        /// <summary>
        /// Insert measurement batch for ANPZ/KRNPZ plants
        /// Maps to: sp_insert_measurement_batch(p_asset_code TEXT, p_points JSONB, p_source_plant TEXT)
        /// </summary>
        public const string MeasurementsInsertBatch = "plant.measurements.insert_batch";
    }

    /// <summary>
    /// Central events operations
    /// </summary>
    public static class Central
    {
        /// <summary>
        /// Ingest events from inbox
        /// Maps to: fn_ingest_events(p_limit int) or sp_ingest_events(p_limit int)
        /// </summary>
        public const string EventsIngest = "central.events.ingest";

        /// <summary>
        /// Cleanup processed events
        /// Maps to: sp_events_cleanup(p_older_than interval)
        /// </summary>
        public const string EventsCleanup = "central.events.cleanup";

        /// <summary>
        /// Get asset summary as JSON
        /// Maps to: fn_asset_summary_json(p_asset_code text, p_policy_name text)
        /// </summary>
        public const string AnalyticsAssetSummary = "central.analytics.asset_summary";

        /// <summary>
        /// Get top assets by corrosion rate
        /// Maps to: fn_top_assets_by_cr(p_limit int)
        /// </summary>
        public const string AnalyticsTopAssetsByCr = "central.analytics.top_assets_by_cr";
    }
}
