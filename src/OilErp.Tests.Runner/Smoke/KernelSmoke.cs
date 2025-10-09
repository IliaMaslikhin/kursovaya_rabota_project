using OilErp.Core.Contracts;
using OilErp.Core.Operations;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Smoke tests for kernel functionality
/// </summary>
public class KernelSmoke
{
    /// <summary>
    /// Tests that KernelAdapter can be created without throwing
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestKernelCreates()
    {
        try
        {
            var storage = new StorageAdapter();
            var kernel = new KernelAdapter(storage);
            return Task.FromResult(new TestResult("Kernel_Creates", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Kernel_Creates", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that KernelAdapter exposes Storage property that is not null
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestKernelExposesStorage()
    {
        try
        {
            var storage = new StorageAdapter();
            var kernel = new KernelAdapter(storage);
            
            if (kernel.Storage == null)
            {
                return Task.FromResult(new TestResult("Kernel_Exposes_Storage", false, "Storage property is null"));
            }

            return Task.FromResult(new TestResult("Kernel_Exposes_Storage", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Kernel_Exposes_Storage", false, ex.Message));
        }
    }
}
