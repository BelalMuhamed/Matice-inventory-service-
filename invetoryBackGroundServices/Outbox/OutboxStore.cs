using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Outbox
{
    /// <summary>
    /// One pending print-result confirmation that could not be delivered to the Inventory API
    /// after Backend Call #2's in-request retries were exhausted (the "printed but could not
    /// confirm" / HTTP 207 scenario). Persisted so the outcome survives a Printer Agent crash or
    /// restart between the original attempt and eventual reconciliation.
    /// <para>
    /// <b>Known limitation, not yet resolved - see the reconciliation job's own doc comment:</b>
    /// <see cref="BearerToken"/> is the Print Agent token from the <em>original</em> print
    /// attempt, and that token is short-lived (5 minutes by default) while this outbox exists
    /// specifically to survive delays longer than a single request's retry loop. By the time a
    /// scheduled sweep runs, it will typically already be expired.
    /// </para>
    /// </summary>
    public sealed class OutboxEntry
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public long ProductItemId { get; set; }
        public long BranchId { get; set; }
        public bool Success { get; set; }
        public string? HolderName { get; set; }
        public string BearerToken { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
        public string? LastFailureReason { get; set; }
    }

    /// <summary>Persists and retrieves pending <see cref="OutboxEntry"/> records.</summary>
    public interface IOutboxStore
    {
        /// <summary>Persists a new entry, keyed by its idempotency key.</summary>
        Task SaveAsync(OutboxEntry entry);

        /// <summary>Returns every currently pending entry.</summary>
        Task<IReadOnlyList<OutboxEntry>> GetAllAsync();

        /// <summary>Overwrites an existing entry (e.g. after a failed retry, to record the attempt).</summary>
        Task UpdateAsync(OutboxEntry entry);

        /// <summary>Removes an entry once it has been successfully reconciled.</summary>
        Task DeleteAsync(string idempotencyKey);
    }

    /// <summary>
    /// One JSON file per pending entry under a configured directory - deliberately the simplest
    /// mechanism that satisfies "survives a process restart," not a database or queue. The
    /// idempotency key is already unique per physical print attempt, so it doubles as the
    /// filename; no separate id generation needed.
    /// </summary>
    public sealed class FileOutboxStore : IOutboxStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _directory;

        /// <summary>Creates the store, ensuring its backing directory exists.</summary>
        public FileOutboxStore(IOptions<OutboxOptions> options)
        {
            string configured = options.Value.Directory;
            _directory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "Outbox")
                : configured;
            Directory.CreateDirectory(_directory);
        }

        /// <inheritdoc />
        public async Task SaveAsync(OutboxEntry entry)
        {
            await File.WriteAllTextAsync(PathFor(entry.IdempotencyKey), JsonSerializer.Serialize(entry, JsonOptions));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<OutboxEntry>> GetAllAsync()
        {
            var entries = new List<OutboxEntry>();
            foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    OutboxEntry? entry = JsonSerializer.Deserialize<OutboxEntry>(File.ReadAllText(file));
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // A corrupt/partially-written file shouldn't take down the whole sweep -
                    // it's left in place for manual inspection rather than silently deleted.
                }
            }

            return Task.FromResult<IReadOnlyList<OutboxEntry>>(entries);
        }

        /// <inheritdoc />
        public Task UpdateAsync(OutboxEntry entry) => SaveAsync(entry);

        /// <inheritdoc />
        public Task DeleteAsync(string idempotencyKey)
        {
            string path = PathFor(idempotencyKey);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        private string PathFor(string idempotencyKey) => Path.Combine(_directory, $"{idempotencyKey}.json");
    }
}
