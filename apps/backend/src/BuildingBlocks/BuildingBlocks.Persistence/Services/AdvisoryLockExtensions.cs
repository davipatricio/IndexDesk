using Microsoft.EntityFrameworkCore;

namespace IndexDesk.BuildingBlocks.Persistence.Services;

/// <summary>
/// Postgres advisory locks scoped to the EF Core <c>DbContext</c> connection. We use
/// them as a coarse "only one sync writes at a time" gate across CLI backfill, API
/// trigger, Quartz job and the startup catch-up bootstrap — without standing up a
/// full distributed lock service (Redis/etcd). PG releases the lock automatically
/// when the holding connection drops, so even <c>kill -9</c> cannot leak it.
///
/// The locks are session-scoped (<c>pg_try_advisory_lock</c>), so callers MUST hold
/// the returned handle until they are done; <see cref="AdvisoryLockHandle.DisposeAsync"/>
/// calls <c>pg_advisory_unlock</c> on the same connection.
/// </summary>
public static class AdvisoryLockExtensions
{
    /// <summary>Lock id reserved for the daily-sync / catch-up pipeline. Hex spells
    /// "BACKFILL": 0x4241434B46494C4C. Stable across releases so the same id is
    /// observable in <c>pg_locks</c> from anywhere.</summary>
    public const long BackfillSyncLockId = 0x4241434B46494C4CL;

    /// <summary>Try to acquire a session-scoped advisory lock without blocking. Returns
    /// <c>null</c> if another process/connection already holds it. Each call
    /// consumes one round trip; budget for that on hot paths.</summary>
    public static async Task<AdvisoryLockHandle?> TryAcquireAdvisoryLockAsync(
        this IndexDeskDbContext db,
        long lockId,
        CancellationToken cancellationToken = default
    )
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lockId)";
        var param = command.CreateParameter();
        param.ParameterName = "lockId";
        param.Value = lockId;
        command.Parameters.Add(param);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is bool acquired && acquired)
        {
            return new AdvisoryLockHandle(db, lockId);
        }

        return null;
    }
}

/// <summary>
/// IDisposable wrapper around a held Postgres advisory lock. The lock is released
/// when <see cref="DisposeAsync"/> runs OR when the underlying connection dies —
/// both paths are safe; the second one is what makes <c>kill -9</c> not a problem.
/// </summary>
public sealed class AdvisoryLockHandle : IAsyncDisposable
{
    private readonly IndexDeskDbContext _db;
    private readonly long _lockId;
    private bool _released;

    internal AdvisoryLockHandle(IndexDeskDbContext db, long lockId)
    {
        _db = db;
        _lockId = lockId;
    }

    public long LockId => _lockId;

    public async ValueTask DisposeAsync()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        try
        {
            await _db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_unlock({_lockId})"
            );
        }
        catch
        {
            // Connection already gone: PG auto-released the lock, nothing to do.
        }
    }
}
