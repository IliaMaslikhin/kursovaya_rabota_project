using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OilErp.Tests.Runner.Util;

/// <summary>
/// Test scenario result
/// </summary>
/// <param name="Name">Scenario name</param>
/// <param name="Success">Whether the scenario succeeded</param>
/// <param name="Error">Error message if failed</param>
/// <param name="Skipped">True when the scenario was intentionally skipped</param>
public record TestResult(string Name, bool Success, string? Error = null, bool Skipped = false);

/// <summary>
/// Test scenario delegate
/// </summary>
/// <returns>Test result</returns>
public delegate Task<TestResult> TestScenario();

/// <summary>
/// Defines when a scenario should be executed.
/// </summary>
public enum TestRunScope
{
    Always,
    FirstRunOnly
}

/// <summary>
/// Test runner harness for registering and running named scenarios
/// </summary>
public record TestScenarioDefinition(
    string Name,
    string Category,
    string Title,
    string SuccessHint,
    string FailureHint,
    TestScenario Scenario,
    TestRunScope Scope = TestRunScope.Always);

/// <summary>
/// Test runner harness for registering and running named scenarios
/// </summary>
public class TestRunner
{
    private readonly List<TestScenarioDefinition> _scenarios = new();
    public bool IsFirstRun { get; set; } = true;
    public string? MachineCode { get; set; }

    /// <summary>
    /// Registers a test scenario
    /// </summary>
    /// <param name="name">Scenario name</param>
    /// <param name="scenario">Scenario implementation</param>
    public void Register(TestScenarioDefinition definition) => _scenarios.Add(definition);

    /// <summary>
    /// Runs all registered scenarios
    /// </summary>
    /// <returns>Collection of test results</returns>
    public async Task<IReadOnlyList<(TestScenarioDefinition Definition, TestResult Result)>> RunAllAsync()
    {
        var results = new List<(TestScenarioDefinition, TestResult)>(_scenarios.Count);

        foreach (var definition in _scenarios)
        {
            if (definition.Scope == TestRunScope.FirstRunOnly && !IsFirstRun)
            {
                results.Add((definition, new TestResult(definition.Name, true, "Skipped on repeat run", true)));
                continue;
            }

            try
            {
                var result = await definition.Scenario();
                results.Add((definition, result));
            }
            catch (Exception ex)
            {
                results.Add((definition, new TestResult(definition.Name, false, ex.Message)));
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Runs all scenarios and prints results
    /// </summary>
    public async Task RunAndPrintAsync()
    {
        Console.WriteLine($"Запуск {_scenarios.Count} сценариев...");
        Console.WriteLine($"Режим: {(IsFirstRun ? "первый запуск" : "повторный запуск")} (код машины: {MachineCode ?? "n/a"})");
        Console.WriteLine();

        var results = await RunAllAsync();
        var grouped = results
            .GroupBy(r => r.Definition.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            Console.WriteLine($"Категория: {group.Key}");
            foreach (var item in group)
            {
                var status = item.Result.Skipped ? "⏭" : item.Result.Success ? "✅" : "❌";
                var hint = item.Result.Skipped
                    ? "Пропущен для повторного запуска"
                    : item.Result.Success
                    ? item.Definition.SuccessHint
                    : $"{item.Definition.FailureHint}. Детали: {item.Result.Error}";
                Console.WriteLine($"  {status} {item.Definition.Title} — {hint}");
            }
            Console.WriteLine();
        }

        var totalTests = results.Count;
        var skippedTests = results.Count(r => r.Result.Skipped);
        var executedTests = totalTests - skippedTests;
        var passedTests = results.Count(r => r.Result.Success && !r.Result.Skipped);
        var failedTests = executedTests - passedTests;
        Console.WriteLine($"Итого: {passedTests}/{executedTests} тестов пройдено, {failedTests} не прошло, пропущено: {skippedTests}");

        Console.WriteLine();
        Console.WriteLine("Интерпретация:");
        if (failedTests == 0)
        {
            Console.WriteLine("- Все проверки пройдены — инфраструктура готова к работе.");
        }
        else
        {
            foreach (var item in results.Where(r => !r.Result.Success))
            {
                Console.WriteLine($"- {item.Definition.Title}: {item.Definition.FailureHint}. Причина: {item.Result.Error}");
            }
        }
    }
}
