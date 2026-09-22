using KorridorX.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KorridorX.Services.Compliance;

// A dedicated non-pooled connection releases all session locks on disposal, including
// exceptional exits. No database transaction is held open across the provider request.
internal sealed class BusinessKybStartLease : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private BusinessKybStartLease(NpgsqlConnection connection) => _connection = connection;

    public static async Task<BusinessKybStartLease> AcquireAsync(
        AppDbContext db, Guid userId, CancellationToken ct)
    {
        var options = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString())
        {
            Pooling = false
        };
        var lease = new BusinessKybStartLease(new NpgsqlConnection(options.ConnectionString));
        try
        {
            await lease._connection.OpenAsync(ct);
            await lease.LockAsync($"korridorx:business-kyb-start:{userId:D}", ct);
            return lease;
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    public Task LockBusinessAsync(Guid businessProfileId, CancellationToken ct) =>
        LockAsync($"korridorx:business-kyb-profile:{businessProfileId:D}", ct);

    private async Task LockAsync(string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtextextended(@key, 0))", _connection);
        command.Parameters.AddWithValue("key", key);
        if (await command.ExecuteScalarAsync(ct) is not true)
            throw new InvalidOperationException(
                "Business verification is already being saved. Wait for that request to finish, then refresh.");
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
