using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using OilErp.Core.Dto;

namespace OilErp.Bootstrap;

public sealed record DbObjectRequirement(string ObjectType, string Name, string? Signature = null);

public sealed record InventorySnapshot(
    HashSet<string> Functions,
    HashSet<string> Procedures,
    HashSet<string> Tables,
    HashSet<string> Triggers);

public sealed record InventoryVerification(bool Success, string? ErrorMessage)
{
    public static InventoryVerification Ok() => new(true, null);
    public static InventoryVerification Fail(string message) => new(false, message);
}

/// <summary>
/// Инвентаризация объектов БД для UI/Tests/Infra.
/// </summary>
public sealed class DatabaseInventoryInspector
{
    private readonly string _connectionString;
    public DatabaseProfile Profile { get; }
    private InventorySnapshot? _snapshot;
    private readonly string? _sqlRoot;

    public DatabaseInventoryInspector(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Profile = DetectProfile(connectionString);
        _sqlRoot = LocateSqlRoot();
    }

    public async Task<InventoryVerification> VerifyAsync()
    {
        var expected = GetExpectedObjects();
        if (expected.Count == 0)
        {
            return InventoryVerification.Fail("No expected objects defined for this profile");
        }

        var snapshot = await GetSnapshotAsync();
        var missing = expected.Where(req => !Exists(snapshot, req)).ToList();

        if (missing.Count > 0)
        {
            var autoCreated = await TryAutoCreateObjectsAsync(Profile, missing);
            if (autoCreated)
            {
                snapshot = await GetSnapshotAsync(forceReload: true);
                missing = expected.Where(req => !Exists(snapshot, req)).ToList();
            }
        }

        var signatureIssues = await FindSignatureMismatchesAsync(expected);

        if (missing.Count == 0 && signatureIssues.Count == 0)
        {
            return InventoryVerification.Ok();
        }

        var parts = new List<string>();
        if (missing.Count > 0)
        {
            parts.Add(FormatReminder(missing));
        }
        if (signatureIssues.Count > 0)
        {
            parts.Add("Signature mismatches: " + string.Join("; ", signatureIssues));
        }

        return InventoryVerification.Fail(string.Join(" | ", parts));
    }

    public void PrintSummary()
    {
        var snapshot = _snapshot;
        if (snapshot == null) return;
        Console.WriteLine($"[Валидация] Профиль={Profile} функций={snapshot.Functions.Count} процедур={snapshot.Procedures.Count} таблиц={snapshot.Tables.Count} триггеров={snapshot.Triggers.Count}");
    }

    public static string FormatReminder(IEnumerable<DbObjectRequirement> missing)
    {
        var list = missing.ToList();
        if (list.Count == 0) return "All objects are present.";

        var lines = list.Select((item, index) => $"{index + 1}. {item.ObjectType} {item.Name}");
        return $"Missing required DB objects:\n{string.Join("\n", lines)}\nTODO: auto-create these objects via sql/ scripts before start.";
    }

    private static DatabaseProfile DetectProfile(string connectionString)
    {
        var profile = DetectProfileFromConnectionString(connectionString);
        if (profile != DatabaseProfile.Unknown) return profile;
        return DatabaseProfile.Unknown;
    }

    private static DatabaseProfile DetectProfileFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var db = builder.Database?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(db)) return DatabaseProfile.Unknown;
            if (db.Contains("central")) return DatabaseProfile.Central;
            if (db.Contains("anpz")) return DatabaseProfile.PlantAnpz;
            if (db.Contains("krnpz")) return DatabaseProfile.PlantKrnpz;
            return DatabaseProfile.Unknown;
        }
        catch
        {
            return DatabaseProfile.Unknown;
        }
    }

    private async Task<InventorySnapshot> GetSnapshotAsync(bool forceReload = false)
    {
        if (!forceReload && _snapshot != null) return _snapshot;
        var functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var procedures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var triggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                select routine_type, routine_schema || '.' || routine_name as name
                from information_schema.routines
                where routine_schema not in ('pg_catalog', 'information_schema')
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(0);
                var name = reader.GetString(1);
                if (string.Equals(type, "FUNCTION", StringComparison.OrdinalIgnoreCase))
                    functions.Add(name);
                else
                    procedures.Add(name);
            }
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                select table_schema || '.' || table_name
                from information_schema.tables
                where table_schema not in ('pg_catalog', 'information_schema')
                  and table_type = 'BASE TABLE'
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                select ns.nspname || '.' || tg.tgname as name
                from pg_trigger tg
                join pg_class cls on cls.oid = tg.tgrelid
                join pg_namespace ns on ns.oid = cls.relnamespace
                where not tg.tgisinternal
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                triggers.Add(reader.GetString(0));
            }
        }

        _snapshot = new InventorySnapshot(functions, procedures, tables, triggers);
        return _snapshot;
    }

    private List<DbObjectRequirement> GetExpectedObjects()
    {
        return Profile switch
        {
            DatabaseProfile.Central => new List<DbObjectRequirement>
            {
                new("function", "public.fn_calc_cr", "prev_thk numeric, prev_date timestamp with time zone, last_thk numeric, last_date timestamp with time zone"),
                new("function", "public.fn_asset_upsert", "p_asset_code text, p_name text, p_type text, p_plant_code text"),
                new("function", "public.fn_policy_upsert", "p_name text, p_low numeric, p_med numeric, p_high numeric"),
                new("function", "public.fn_events_enqueue", "p_event_type text, p_source_plant text, p_payload jsonb"),
                new("function", "public.fn_events_peek", "p_limit integer"),
                new("function", "public.fn_ingest_events", "p_limit integer"),
                new("function", "public.fn_events_requeue", "p_ids bigint[]"),
                new("function", "public.fn_events_cleanup", "p_older_than interval"),
                new("function", "public.fn_eval_risk", "p_asset_code text, p_policy_name text"),
                new("function", "public.fn_asset_summary_json", "p_asset_code text, p_policy_name text"),
                new("function", "public.fn_top_assets_by_cr", "p_limit integer"),
                new("function", "public.fn_plant_cr_stats", "p_plant text, p_from timestamp with time zone, p_to timestamp with time zone"),
                new("procedure", "public.sp_ingest_events", "p_limit integer, OUT processed integer"),
                new("procedure", "public.sp_events_enqueue", "p_event_type text, p_source_plant text, p_payload jsonb, OUT p_id bigint"),
                new("procedure", "public.sp_events_requeue", "p_ids bigint[], OUT n integer"),
                new("procedure", "public.sp_events_cleanup", "p_older_than interval, OUT n integer"),
                new("procedure", "public.sp_policy_upsert", "p_name text, p_low numeric, p_med numeric, p_high numeric, OUT p_id bigint"),
                new("procedure", "public.sp_asset_upsert", "p_asset_code text, p_name text, p_type text, p_plant_code text, OUT p_id bigint"),
                new("table", "public.assets_global"),
                new("table", "public.risk_policies"),
                new("table", "public.analytics_cr"),
                new("table", "public.events_inbox")
            },
            DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz => new List<DbObjectRequirement>
            {
                new("function", "public.sp_insert_measurement_batch", "p_asset_code text, p_points jsonb, p_source_plant text"),
                new("procedure", "public.sp_insert_measurement_batch_prc", "p_asset_code text, p_points jsonb, p_source_plant text, OUT p_inserted integer"),
                new("function", "public.trg_measurements_ai_fn"),
                new("trigger", "public.trg_measurements_ai"),
                new("table", "public.assets_local"),
                new("table", "public.measurement_points"),
                new("table", "public.measurements"),
                new("table", "public.local_events"),
                new("table", "central_ft.events_inbox")
            },
            _ => new List<DbObjectRequirement>()
        };
    }

    private static bool Exists(InventorySnapshot snapshot, DbObjectRequirement requirement)
    {
        return requirement.ObjectType switch
        {
            "function" => snapshot.Functions.Contains(requirement.Name),
            "procedure" => snapshot.Procedures.Contains(requirement.Name),
            "table" => snapshot.Tables.Contains(requirement.Name),
            "trigger" => snapshot.Triggers.Contains(requirement.Name),
            _ => false
        };
    }

    private async Task<bool> TryAutoCreateObjectsAsync(DatabaseProfile profile, IReadOnlyCollection<DbObjectRequirement> missing)
    {
        if (profile == DatabaseProfile.Unknown) return false;
        if (_sqlRoot == null) return false;
        var scripts = GetProfileScripts(profile);
        if (scripts.Length == 0) return false;

        Console.WriteLine($"[Валидация] Обнаружены отсутствующие объекты ({missing.Count}). Пытаемся автосоздать через SQL скрипты профиля {profile}.");
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            foreach (var relative in scripts)
            {
                var path = Path.Combine(_sqlRoot, relative);
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[Валидация] SQL-скрипт не найден: {path}");
                    return false;
                }

                var sql = await File.ReadAllTextAsync(path);
                Console.WriteLine($"[Валидация] Выполняем {relative}...");
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }

            Console.WriteLine("[Валидация] Автосоздание завершено, запускаем повторную проверку.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Валидация] Автосоздание не удалось: {ex.Message}");
            return false;
        }
    }

    private static string[] GetProfileScripts(DatabaseProfile profile)
    {
        return profile switch
        {
            DatabaseProfile.Central => new[]
            {
                Path.Combine("central", "01_tables.sql"),
                Path.Combine("central", "02_functions_core.sql"),
                Path.Combine("central", "03_procedures.sql")
            },
            DatabaseProfile.PlantAnpz => new[]
            {
                Path.Combine("anpz", "01_tables.sql"),
                Path.Combine("anpz", "02_fdw.sql"),
                Path.Combine("anpz", "03_trigger_measurements_ai.sql"),
                Path.Combine("anpz", "04_function_sp_insert_measurement_batch.sql"),
                Path.Combine("anpz", "05_procedure_wrapper.sql")
            },
            DatabaseProfile.PlantKrnpz => new[]
            {
                Path.Combine("krnpz", "01_tables.sql"),
                Path.Combine("krnpz", "02_fdw.sql"),
                Path.Combine("krnpz", "03_trigger_measurements_ai.sql"),
                Path.Combine("krnpz", "04_function_sp_insert_measurement_batch.sql"),
                Path.Combine("krnpz", "05_procedure_wrapper.sql")
            },
            _ => Array.Empty<string>()
        };
    }

    private async Task<List<string>> FindSignatureMismatchesAsync(IEnumerable<DbObjectRequirement> expected)
    {
        var result = new List<string>();
        var signatureMap = await LoadRoutineSignaturesAsync();
        foreach (var req in expected)
        {
            if (req.Signature is null) continue;
            if (!signatureMap.TryGetValue(req.Name, out var actual)) continue;
            if (!string.Equals(NormalizeSignature(actual), NormalizeSignature(req.Signature), StringComparison.OrdinalIgnoreCase))
            {
                result.Add($"{req.Name} expected ({req.Signature}) actual ({actual})");
            }
        }
        return result;
    }

    private async Task<Dictionary<string, string>> LoadRoutineSignaturesAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string sql = """
            select n.nspname || '.' || p.proname as name,
                   coalesce(pg_get_function_identity_arguments(p.oid), '') as signature
            from pg_proc p
            join pg_namespace n on n.oid = p.pronamespace
            where n.nspname not in ('pg_catalog', 'information_schema')
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var sig = reader.GetString(1);
            map[name] = sig;
        }

        return map;
    }

    private static string NormalizeSignature(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return string.Empty;
        var cleaned = signature.ToLowerInvariant();
        cleaned = cleaned.Replace(" ", string.Empty, StringComparison.Ordinal);
        var idx = cleaned.IndexOf("default", StringComparison.Ordinal);
        if (idx >= 0)
        {
            cleaned = cleaned.Substring(0, idx);
        }
        return cleaned;
    }

    private static string? LocateSqlRoot()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "sql");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
