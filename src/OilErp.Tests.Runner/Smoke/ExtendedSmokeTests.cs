using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Extended smoke tests for comprehensive testing
/// </summary>
public class ExtendedSmokeTests
{
    /// <summary>
    /// Tests that QuerySpec allows empty parameters
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestQuerySpecEmptyParametersAllowed()
    {
        try
        {
            var spec = new QuerySpec("test.operation", new Dictionary<string, object?>());
            if (spec.Parameters.Count != 0)
            {
                return Task.FromResult(new TestResult("QuerySpec_EmptyParameters_Allowed", false, "Parameters should be empty"));
            }
            return Task.FromResult(new TestResult("QuerySpec_EmptyParameters_Allowed", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("QuerySpec_EmptyParameters_Allowed", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that CommandSpec allows empty parameters
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestCommandSpecEmptyParametersAllowed()
    {
        try
        {
            var spec = new CommandSpec("test.operation", new Dictionary<string, object?>());
            if (spec.Parameters.Count != 0)
            {
                return Task.FromResult(new TestResult("CommandSpec_EmptyParameters_Allowed", false, "Parameters should be empty"));
            }
            return Task.FromResult(new TestResult("CommandSpec_EmptyParameters_Allowed", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("CommandSpec_EmptyParameters_Allowed", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that OperationNames are unique and non-empty
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestOperationNamesUniqueAndNonEmpty()
    {
        try
        {
            var names = new[]
            {
                OperationNames.Plant.MeasurementsInsertBatch,
                OperationNames.Central.EventsIngest,
                OperationNames.Central.EventsCleanup,
                OperationNames.Central.AnalyticsAssetSummary,
                OperationNames.Central.AnalyticsTopAssetsByCr
            };

            var uniqueNames = names.Distinct().ToArray();
            if (uniqueNames.Length != names.Length)
            {
                return Task.FromResult(new TestResult("OperationNames_Unique_And_NonEmpty", false, "Operation names are not unique"));
            }

            if (names.Any(string.IsNullOrEmpty))
            {
                return Task.FromResult(new TestResult("OperationNames_Unique_And_NonEmpty", false, "Some operation names are empty"));
            }

            return Task.FromResult(new TestResult("OperationNames_Unique_And_NonEmpty", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("OperationNames_Unique_And_NonEmpty", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that MeasurementPointDto constructs with valid data
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestMeasurementPointDtoConstructsWithValidData()
    {
        try
        {
            var dto = new MeasurementPointDto("Point1", DateTime.UtcNow, 10.5m, "Test note");
            
            if (dto.Label != "Point1")
            {
                return Task.FromResult(new TestResult("MeasurementPointDto_Constructs_With_Valid_Data", false, "Label not set correctly"));
            }
            
            if (dto.Thickness != 10.5m)
            {
                return Task.FromResult(new TestResult("MeasurementPointDto_Constructs_With_Valid_Data", false, "Thickness not set correctly"));
            }
            
            if (dto.Note != "Test note")
            {
                return Task.FromResult(new TestResult("MeasurementPointDto_Constructs_With_Valid_Data", false, "Note not set correctly"));
            }

            return Task.FromResult(new TestResult("MeasurementPointDto_Constructs_With_Valid_Data", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("MeasurementPointDto_Constructs_With_Valid_Data", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that IStoragePort BeginTransaction returns disposable
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestIStoragePortBeginTransactionReturnsDisposable()
    {
        try
        {
            var storage = new FakeStoragePort();
            var transaction = storage.BeginTransactionAsync().Result;
            
            if (transaction == null)
            {
                return Task.FromResult(new TestResult("IStoragePort_BeginTransaction_Returns_Disposable", false, "Transaction is null"));
            }
            
            if (transaction is not IAsyncDisposable)
            {
                return Task.FromResult(new TestResult("IStoragePort_BeginTransaction_Returns_Disposable", false, "Transaction is not IAsyncDisposable"));
            }

            return Task.FromResult(new TestResult("IStoragePort_BeginTransaction_Returns_Disposable", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("IStoragePort_BeginTransaction_Returns_Disposable", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests transaction commit path sets flag
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestTransactionCommitPathSetsFlag()
    {
        try
        {
            var storage = new FakeStoragePort();
            var transaction = storage.BeginTransactionAsync().Result as FakeTransaction;
            
            if (transaction == null)
            {
                return Task.FromResult(new TestResult("Transaction_Commit_Path_Sets_Flag", false, "Transaction is not FakeTransaction"));
            }
            
            transaction.Commit();
            
            if (!transaction.IsCommitted)
            {
                return Task.FromResult(new TestResult("Transaction_Commit_Path_Sets_Flag", false, "Commit flag not set"));
            }

            return Task.FromResult(new TestResult("Transaction_Commit_Path_Sets_Flag", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Transaction_Commit_Path_Sets_Flag", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests transaction rollback path sets flag
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestTransactionRollbackPathSetsFlag()
    {
        try
        {
            var storage = new FakeStoragePort();
            var transaction = storage.BeginTransactionAsync().Result as FakeTransaction;
            
            if (transaction == null)
            {
                return Task.FromResult(new TestResult("Transaction_Rollback_Path_Sets_Flag", false, "Transaction is not FakeTransaction"));
            }
            
            transaction.Rollback();
            
            if (!transaction.IsRolledBack)
            {
                return Task.FromResult(new TestResult("Transaction_Rollback_Path_Sets_Flag", false, "Rollback flag not set"));
            }

            return Task.FromResult(new TestResult("Transaction_Rollback_Path_Sets_Flag", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Transaction_Rollback_Path_Sets_Flag", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that rolling back twice doesn't throw
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestTransactionRollbackTwiceNoThrow()
    {
        try
        {
            var storage = new FakeStoragePort();
            var transaction = storage.BeginTransactionAsync().Result as FakeTransaction;
            
            if (transaction == null)
            {
                return Task.FromResult(new TestResult("Transaction_Rollback_Twice_NoThrow", false, "Transaction is not FakeTransaction"));
            }
            
            transaction.Rollback();
            transaction.Rollback(); // Should not throw

            return Task.FromResult(new TestResult("Transaction_Rollback_Twice_NoThrow", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Transaction_Rollback_Twice_NoThrow", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that committing after rollback throws
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestTransactionCommitAfterRollbackThrows()
    {
        try
        {
            var storage = new FakeStoragePort();
            var transaction = storage.BeginTransactionAsync().Result as FakeTransaction;
            
            if (transaction == null)
            {
                return Task.FromResult(new TestResult("Transaction_Commit_After_Rollback_Throws", false, "Transaction is not FakeTransaction"));
            }
            
            transaction.Rollback();
            
            try
            {
                transaction.Commit();
                return Task.FromResult(new TestResult("Transaction_Commit_After_Rollback_Throws", false, "Commit after rollback should throw"));
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(new TestResult("Transaction_Commit_After_Rollback_Throws", true));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Transaction_Commit_After_Rollback_Throws", false, ex.Message));
        }
    }
}
