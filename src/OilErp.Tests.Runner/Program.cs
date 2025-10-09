using OilErp.Tests.Runner.Smoke;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner;

/// <summary>
/// Console application entry point for smoke tests
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("OilErp Extended Smoke Tests");
        Console.WriteLine("============================");
        Console.WriteLine();

        var runner = new TestRunner();
        var kernelSmoke = new KernelSmoke();
        var storageSmoke = new StorageSmoke();
        var extendedSmoke = new ExtendedSmokeTests();
        var asyncSmoke = new AsyncSmokeTests();
        var validationSmoke = new ValidationSmokeTests();

        // Register all test scenarios
        runner.Register("Kernel_Creates", kernelSmoke.TestKernelCreates);
        runner.Register("Kernel_Exposes_Storage", kernelSmoke.TestKernelExposesStorage);
        runner.Register("Storage_Event_Subscribe_Unsubscribe_NoThrow", storageSmoke.TestStorageSubscribeNotifications);
        runner.Register("QuerySpec_EmptyParameters_Allowed", extendedSmoke.TestQuerySpecEmptyParametersAllowed);
        runner.Register("CommandSpec_EmptyParameters_Allowed", extendedSmoke.TestCommandSpecEmptyParametersAllowed);
        runner.Register("OperationNames_Unique_And_NonEmpty", extendedSmoke.TestOperationNamesUniqueAndNonEmpty);
        runner.Register("MeasurementPointDto_Constructs_With_Valid_Data", extendedSmoke.TestMeasurementPointDtoConstructsWithValidData);
        runner.Register("IStoragePort_BeginTransaction_Returns_Disposable", extendedSmoke.TestIStoragePortBeginTransactionReturnsDisposable);
        runner.Register("Transaction_Commit_Path_Sets_Flag", extendedSmoke.TestTransactionCommitPathSetsFlag);
        runner.Register("Transaction_Rollback_Path_Sets_Flag", extendedSmoke.TestTransactionRollbackPathSetsFlag);
        runner.Register("Transaction_Rollback_Twice_NoThrow", extendedSmoke.TestTransactionRollbackTwiceNoThrow);
        runner.Register("Transaction_Commit_After_Rollback_Throws", extendedSmoke.TestTransactionCommitAfterRollbackThrows);
        runner.Register("CancellationToken_Propagates_To_Query", asyncSmoke.TestCancellationTokenPropagatesToQuery);
        runner.Register("CancellationToken_Propagates_To_Command", asyncSmoke.TestCancellationTokenPropagatesToCommand);
        runner.Register("Timeout_On_Query_Is_Observed_By_Adapter", asyncSmoke.TestTimeoutOnQueryIsObservedByAdapter);
        runner.Register("Timeout_On_Command_Is_Observed_By_Adapter", asyncSmoke.TestTimeoutOnCommandIsObservedByAdapter);
        runner.Register("Concurrent_Commands_Counter_Is_Accurate", asyncSmoke.TestConcurrentCommandsCounterIsAccurate);
        runner.Register("Event_Raise_Notified_Delivers_Payload", asyncSmoke.TestEventRaiseNotifiedDeliversPayload);
        runner.Register("NotImplemented_Adapter_Methods_Fail_As_Expected", asyncSmoke.TestNotImplementedAdapterMethodsFailAsExpected);
        runner.Register("Spec_Null_OperationName_Throws_Argument", validationSmoke.TestSpecNullOperationNameThrowsArgument);
        runner.Register("Nested_Savepoint_Emulation_Commit_Then_Rollback_Outer", validationSmoke.TestNestedSavepointEmulationCommitThenRollbackOuter);
        runner.Register("Repeated_Same_Command_Idempotent_In_Fake", validationSmoke.TestRepeatedSameCommandIdempotentInFake);
        runner.Register("Query_Then_Command_Ordering_Captured", validationSmoke.TestQueryThenCommandOrderingCaptured);
        runner.Register("Runner_Summary_Prints_Total_Count", validationSmoke.TestRunnerSummaryPrintsTotalCount);

        await runner.RunAndPrintAsync();
    }
}