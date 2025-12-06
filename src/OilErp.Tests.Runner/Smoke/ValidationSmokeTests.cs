using System;
using System.Threading.Tasks;
using OilErp.Bootstrap;
using OilErp.Core.Dto;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Validation smoke tests: schema inventory and reminder stubs for missing objects.
/// </summary>
public class ValidationSmokeTests
{
    /// <summary>
    /// Scans PostgreSQL metadata and ensures all required tables/functions/procedures/triggers exist.
    /// </summary>
    public async Task<TestResult> TestDatabaseInventoryMatchesExpectations()
    {
        const string testName = "Database_Inventory_Matches_Expectations";
        try
        {
            var inspector = new DatabaseInventoryInspector(TestEnvironment.ConnectionString);
            var verification = await inspector.VerifyAsync();
            inspector.PrintSummary();

            if (!verification.Success)
            {
                return new TestResult(testName, false, verification.ErrorMessage);
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Ensures we can detect the connection profile (central vs plant) from the configured connection string.
    /// </summary>
    public Task<TestResult> TestConnectionProfileDetected()
    {
        const string testName = "Connection_Profile_Detected";
        try
        {
            var inspector = new DatabaseInventoryInspector(TestEnvironment.ConnectionString);
            if (inspector.Profile == DatabaseProfile.Unknown)
            {
                return Task.FromResult(new TestResult(testName, false, "Unable to determine DB profile from connection string"));
            }

            Console.WriteLine($"[Валидация] Профиль определён: {inspector.Profile}");
            return Task.FromResult(new TestResult(testName, true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult(testName, false, ex.Message));
        }
    }

    /// <summary>
    /// Verifies the reminder formatting for missing objects (stub per instructions to auto-create later).
    /// </summary>
    public Task<TestResult> TestMissingObjectReminderFormatting()
    {
        const string testName = "Missing_Object_Reminder_Formatting";
        try
        {
            var sampleMissing = new[]
            {
                new DbObjectRequirement("function", "public.fn_demo"),
                new DbObjectRequirement("trigger", "public.trg_demo")
            };
            var reminder = DatabaseInventoryInspector.FormatReminder(sampleMissing);
            if (!reminder.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                !reminder.Contains("fn_demo", StringComparison.Ordinal) ||
                !reminder.Contains("trg_demo", StringComparison.Ordinal))
            {
                return Task.FromResult(new TestResult(testName, false, "Reminder template missing required details"));
            }

            Console.WriteLine($"[Валидация] Пример напоминания: {reminder}");
            return Task.FromResult(new TestResult(testName, true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult(testName, false, ex.Message));
        }
    }
}
