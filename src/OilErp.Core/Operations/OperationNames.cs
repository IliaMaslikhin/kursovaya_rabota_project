namespace OilErp.Core.Operations;

/// <summary>
/// Константы с именами SQL-объектов в формате schema.name.
/// </summary>
public static class OperationNames
{
    /// <summary>
    /// Операции на заводах ANPZ/KRNPZ (схема public).
    /// </summary>
    public static class Plant
    {
        /// <summary>
        /// Функция: public.sp_insert_measurement_batch(p_asset_code text, p_points jsonb, p_source_plant text)
        /// </summary>
        public const string MeasurementsInsertBatch = "public.sp_insert_measurement_batch";

        /// <summary>
        /// Процедура-обёртка: public.sp_insert_measurement_batch_prc(p_asset_code text, p_points jsonb, p_source_plant text)
        /// </summary>
        public const string MeasurementsInsertBatchPrc = "public.sp_insert_measurement_batch_prc";
    }

    /// <summary>
    /// Операции в центральной базе (схема public).
    /// </summary>
    public static class Central
    {
        // Функции (чтение и расчёты)

        /// <summary>
        /// Подсчёт CR: public.fn_calc_cr(prev_thk numeric, prev_date timestamptz, last_thk numeric, last_date timestamptz)
        /// </summary>
        public const string CalcCr = "public.fn_calc_cr";

        /// <summary>
        /// Апсерт актива: public.fn_asset_upsert(p_asset_code text, p_name text, p_type text, p_plant_code text)
        /// </summary>
        public const string AssetUpsert = "public.fn_asset_upsert";

        /// <summary>
        /// Апсерт политики: public.fn_policy_upsert(p_name text, p_low numeric, p_med numeric, p_high numeric)
        /// </summary>
        public const string PolicyUpsert = "public.fn_policy_upsert";

        /// <summary>
        /// Расчёт риска: public.fn_eval_risk(p_asset_code text, p_policy_name text) RETURNS TABLE(...)
        /// </summary>
        public const string EvalRisk = "public.fn_eval_risk";

        /// <summary>
        /// Краткая сводка по активу в JSON: public.fn_asset_summary_json(p_asset_code text, p_policy_name text) RETURNS jsonb
        /// </summary>
        public const string AnalyticsAssetSummary = "public.fn_asset_summary_json";

        /// <summary>
        /// Топ активов по скорости коррозии: public.fn_top_assets_by_cr(p_limit int) RETURNS TABLE(...)
        /// </summary>
        public const string AnalyticsTopAssetsByCr = "public.fn_top_assets_by_cr";

        /// <summary>
        /// Статистика CR по заводу: public.fn_plant_cr_stats(p_plant text, p_from timestamptz, p_to timestamptz)
        /// </summary>
        public const string AnalyticsPlantCrStats = "public.fn_plant_cr_stats";

        // Процедуры (CALL ...)

        /// <summary>
        /// Процедура апсерта политики: public.sp_policy_upsert(p_name text, p_low numeric, p_med numeric, p_high numeric)
        /// </summary>
        public const string SpPolicyUpsert = "public.sp_policy_upsert";

        /// <summary>
        /// Процедура апсерта актива: public.sp_asset_upsert(p_asset_code text, p_name text, p_type text, p_plant_code text)
        /// </summary>
        public const string SpAssetUpsert = "public.sp_asset_upsert";
    }
}
