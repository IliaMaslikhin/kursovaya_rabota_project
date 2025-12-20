using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OilErp.Tests.Runner.Util;

/// <summary>
/// Результат выполнения сценария.
/// </summary>
/// <param name="Name">Название сценария</param>
/// <param name="Success">true, если прошёл</param>
/// <param name="Error">Текст ошибки, если не прошёл</param>
/// <param name="Skipped">true, если сценарий пропущен</param>
public record TestResult(string Name, bool Success, string? Error = null, bool Skipped = false);

/// <summary>
/// Делегат сценария.
/// </summary>
/// <returns>Результат выполнения</returns>
public delegate Task<TestResult> TestScenario();

/// <summary>
/// Когда запускать сценарий.
/// </summary>
public enum TestRunScope
{
    Always,
    FirstRunOnly
}

/// <summary>
/// Обвязка для регистрации и запуска сценариев.
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
/// Запускает все зарегистрированные сценарии и печатает результат.
/// </summary>
public class TestRunner
{
    private readonly List<TestScenarioDefinition> _scenarios = new();
    public bool IsFirstRun { get; set; } = true;
    public string? MachineCode { get; set; }

    /// <summary>
    /// Регистрирует сценарий.
    /// </summary>
    /// <param name="name">Название</param>
    /// <param name="scenario">Реализация</param>
    public void Register(TestScenarioDefinition definition) => _scenarios.Add(definition);

    /// <summary>
    /// Запускает все зарегистрированные сценарии.
    /// </summary>
    /// <returns>Список результатов</returns>
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
    /// Запускает сценарии и печатает итоги.
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
