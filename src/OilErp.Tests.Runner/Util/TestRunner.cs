namespace OilErp.Tests.Runner.Util;

/// <summary>
/// Test scenario result
/// </summary>
/// <param name="Name">Scenario name</param>
/// <param name="Success">Whether the scenario succeeded</param>
/// <param name="Error">Error message if failed</param>
public record TestResult(string Name, bool Success, string? Error = null);

/// <summary>
/// Test scenario delegate
/// </summary>
/// <returns>Test result</returns>
public delegate Task<TestResult> TestScenario();

/// <summary>
/// Test runner harness for registering and running named scenarios
/// </summary>
public class TestRunner
{
    private readonly List<(string Name, TestScenario Scenario)> _scenarios = new();

    /// <summary>
    /// Registers a test scenario
    /// </summary>
    /// <param name="name">Scenario name</param>
    /// <param name="scenario">Scenario implementation</param>
    public void Register(string name, TestScenario scenario)
    {
        _scenarios.Add((name, scenario));
    }

    /// <summary>
    /// Runs all registered scenarios
    /// </summary>
    /// <returns>Collection of test results</returns>
    public async Task<IReadOnlyList<TestResult>> RunAllAsync()
    {
        var results = new List<TestResult>();

        foreach (var (name, scenario) in _scenarios)
        {
            try
            {
                var result = await scenario();
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(new TestResult(name, false, ex.Message));
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Runs all scenarios and prints results
    /// </summary>
    public async Task RunAndPrintAsync()
    {
        Console.WriteLine($"Running {_scenarios.Count} test scenarios...");
        Console.WriteLine();

        var results = await RunAllAsync();

        foreach (var result in results)
        {
            var status = result.Success ? "OK" : "FAIL";
            var message = result.Success ? string.Empty : $" {result.Error}";
            Console.WriteLine($"{result.Name}: {status}{message}");
        }

        Console.WriteLine();
        var totalTests = results.Count;
        var passedTests = results.Count(r => r.Success);
        var failedTests = totalTests - passedTests;
        Console.WriteLine($"Summary: {passedTests}/{totalTests} tests passed, {failedTests} failed");
    }
}
