using System;
using OilErp.Bootstrap;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Smoke tests for bootstrap metadata and machine code marker.
/// </summary>
public class BootstrapSmokeTests
{
    /// <summary>
    /// Ensures the machine code marker is written and stable after marking.
    /// </summary>
    public Task<TestResult> TestMachineCodeMarkerPersisted()
    {
        const string testName = "Machine_Code_Marker";
        try
        {
            var initialFirstRun = FirstRunTracker.IsFirstRun(out var codeBefore);
            FirstRunTracker.MarkCompleted(codeBefore);
            var afterMarkFirstRun = FirstRunTracker.IsFirstRun(out var codeAfter);

            if (afterMarkFirstRun)
            {
                return Task.FromResult(new TestResult(testName, false, "Маркер первого запуска не записан", false));
            }

            if (!string.Equals(codeBefore, codeAfter, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new TestResult(testName, false, $"Код машины изменился: {codeBefore} -> {codeAfter}", false));
            }

            // Treat initialFirstRun flag as informational; the marker must be stable either way.
            return Task.FromResult(new TestResult(testName, true, initialFirstRun ? "Первый запуск зафиксирован" : null, false));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult(testName, false, ex.Message, false));
        }
    }
}
