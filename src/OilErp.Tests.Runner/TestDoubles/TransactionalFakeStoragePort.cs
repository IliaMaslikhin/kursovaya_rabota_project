using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Tests.Runner.TestDoubles;

/// <summary>
/// Extended fake storage port with nested savepoint emulation
/// </summary>
public class TransactionalFakeStoragePort : FakeStoragePort
{
    private readonly Stack<FakeSavepoint> _savepoints = new();

    /// <summary>
    /// Gets the active savepoints
    /// </summary>
    public IReadOnlyCollection<FakeSavepoint> Savepoints => _savepoints.ToArray();

    /// <summary>
    /// Creates a savepoint
    /// </summary>
    /// <param name="name">Savepoint name</param>
    /// <returns>Savepoint instance</returns>
    public FakeSavepoint CreateSavepoint(string name)
    {
        var savepoint = new FakeSavepoint(name);
        _savepoints.Push(savepoint);
        return savepoint;
    }

    /// <summary>
    /// Rolls back to a specific savepoint
    /// </summary>
    /// <param name="savepoint">Savepoint to rollback to</param>
    public void RollbackToSavepoint(FakeSavepoint savepoint)
    {
        if (!_savepoints.Contains(savepoint))
            throw new InvalidOperationException("Savepoint not found");

        while (_savepoints.Count > 0)
        {
            var current = _savepoints.Pop();
            current.IsRolledBack = true;
            if (current == savepoint)
                break;
        }
    }

    /// <summary>
    /// Releases a savepoint
    /// </summary>
    /// <param name="savepoint">Savepoint to release</param>
    public void ReleaseSavepoint(FakeSavepoint savepoint)
    {
        if (!_savepoints.Contains(savepoint))
            throw new InvalidOperationException("Savepoint not found");

        var temp = new Stack<FakeSavepoint>();
        while (_savepoints.Count > 0)
        {
            var current = _savepoints.Pop();
            if (current == savepoint)
            {
                current.IsReleased = true;
                break;
            }
            temp.Push(current);
        }

        while (temp.Count > 0)
        {
            _savepoints.Push(temp.Pop());
        }
    }
}

/// <summary>
/// Fake savepoint implementation
/// </summary>
public class FakeSavepoint
{
    /// <summary>
    /// Gets the savepoint name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether the savepoint was rolled back to
    /// </summary>
    public bool IsRolledBack { get; set; }

    /// <summary>
    /// Gets whether the savepoint was released
    /// </summary>
    public bool IsReleased { get; set; }

    /// <summary>
    /// Initializes a new instance of the FakeSavepoint class
    /// </summary>
    /// <param name="name">Savepoint name</param>
    public FakeSavepoint(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
