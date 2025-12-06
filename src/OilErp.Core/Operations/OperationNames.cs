namespace OilErp.Core.Operations;

/// <summary>
/// Operation name constants mapped to fully-qualified SQL identifiers (schema.name)
/// </summary>
public static class OperationNames
{
    /// <summary>
    /// Plant-level operations available in ANPZ/KRNPZ databases (public schema)
    /// </summary>
    public static class Plant
    {
        /// <summary>
        /// Function: public.sp_insert_measurement_batch(p_asset_code text, p_points jsonb, p_source_plant text)
        /// </summary>
        public const string MeasurementsInsertBatch = "public.sp_insert_measurement_batch";

        /// <summary>
        /// Procedure wrapper: public.sp_insert_measurement_batch_prc(p_asset_code text, p_points jsonb, p_source_plant text)
        /// </summary>
        public const string MeasurementsInsertBatchPrc = "public.sp_insert_measurement_batch_prc";
    }

    /// <summary>
    /// Central database operations (public schema)
    /// </summary>
    public static class Central
    {
        // Functions (read/write helpers)

        /// <summary>
        /// Helper calculation: public.fn_calc_cr(prev_thk numeric, prev_date timestamptz, last_thk numeric, last_date timestamptz)
        /// </summary>
        public const string CalcCr = "public.fn_calc_cr";

        /// <summary>
        /// Upsert asset: public.fn_asset_upsert(p_asset_code text, p_name text, p_type text, p_plant_code text)
        /// </summary>
        public const string AssetUpsert = "public.fn_asset_upsert";

        /// <summary>
        /// Upsert policy: public.fn_policy_upsert(p_name text, p_low numeric, p_med numeric, p_high numeric)
        /// </summary>
        public const string PolicyUpsert = "public.fn_policy_upsert";

        /// <summary>
        /// Enqueue event: public.fn_events_enqueue(p_event_type text, p_source_plant text, p_payload jsonb)
        /// </summary>
        public const string EventsEnqueue = "public.fn_events_enqueue";

        /// <summary>
        /// Peek events: public.fn_events_peek(p_limit int) RETURNS TABLE(...)
        /// </summary>
        public const string EventsPeek = "public.fn_events_peek";

        /// <summary>
        /// Ingest events: public.fn_ingest_events(p_limit int)
        /// </summary>
        public const string EventsIngest = "public.fn_ingest_events";

        /// <summary>
        /// Requeue events: public.fn_events_requeue(p_ids bigint[])
        /// </summary>
        public const string EventsRequeue = "public.fn_events_requeue";

        /// <summary>
        /// Cleanup events: public.fn_events_cleanup(p_older_than interval)
        /// </summary>
        public const string EventsCleanup = "public.fn_events_cleanup";

        /// <summary>
        /// Evaluate risk: public.fn_eval_risk(p_asset_code text, p_policy_name text) RETURNS TABLE(...)
        /// </summary>
        public const string EvalRisk = "public.fn_eval_risk";

        /// <summary>
        /// Get asset summary JSON: public.fn_asset_summary_json(p_asset_code text, p_policy_name text) RETURNS jsonb
        /// </summary>
        public const string AnalyticsAssetSummary = "public.fn_asset_summary_json";

        /// <summary>
        /// Top assets by corrosion rate: public.fn_top_assets_by_cr(p_limit int) RETURNS TABLE(...)
        /// </summary>
        public const string AnalyticsTopAssetsByCr = "public.fn_top_assets_by_cr";

        /// <summary>
        /// Plant CR stats: public.fn_plant_cr_stats(p_plant text, p_from timestamptz, p_to timestamptz)
        /// </summary>
        public const string AnalyticsPlantCrStats = "public.fn_plant_cr_stats";

        // Procedures (CALL ...)

        /// <summary>
        /// Procedure: public.sp_ingest_events(p_limit int)
        /// </summary>
        public const string SpIngestEvents = "public.sp_ingest_events";

        /// <summary>
        /// Procedure: public.sp_events_enqueue(p_event_type text, p_source_plant text, p_payload jsonb)
        /// </summary>
        public const string SpEventsEnqueue = "public.sp_events_enqueue";

        /// <summary>
        /// Procedure: public.sp_events_requeue(p_ids bigint[])
        /// </summary>
        public const string SpEventsRequeue = "public.sp_events_requeue";

        /// <summary>
        /// Procedure: public.sp_events_cleanup(p_older_than interval)
        /// </summary>
        public const string SpEventsCleanup = "public.sp_events_cleanup";

        /// <summary>
        /// Procedure: public.sp_policy_upsert(p_name text, p_low numeric, p_med numeric, p_high numeric)
        /// </summary>
        public const string SpPolicyUpsert = "public.sp_policy_upsert";

        /// <summary>
        /// Procedure: public.sp_asset_upsert(p_asset_code text, p_name text, p_type text, p_plant_code text)
        /// </summary>
        public const string SpAssetUpsert = "public.sp_asset_upsert";
    }
}
