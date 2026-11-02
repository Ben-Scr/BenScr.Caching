using System;
using System.Threading.Tasks;
using BenScr.Caching;

// This example uses controlled timestamps so expiration is reproducible without waiting.
var time = new ExampleTimeProvider();
var cache = new LruCache<string, string?>(2, time, StringComparer.OrdinalIgnoreCase);
cache.Set("profile:42", "Ada");
cache.Set("profile:7", "Grace", TimeSpan.FromMinutes(5));
Check(cache.TryGet("PROFILE:42", out string? name) && name == "Ada", "Case-insensitive read");
cache.Set("profile:99", null);
Check(!cache.TryGet("profile:7", out _), "Least recent entry evicted");
Check(cache.TryGet("profile:99", out string? cachedNull) && cachedNull is null, "Null value is a hit");

cache.Set("profile:99", "New profile", TimeSpan.FromTicks(10));
time.AdvanceTicks(9);
Check(cache.TryGet("profile:99", out _), "Before expiration boundary");
time.AdvanceTicks(1);
Check(!cache.TryGet("profile:99", out _), "Exact fixed expiration boundary");
Check(cache.Count == 1, "Expired entry excluded from count");
cache.Set("profile:42", "ignored", TimeSpan.Zero);
Check(cache.Count == 0, "Zero lifetime removes the key");

var concurrent = new LruCache<int, int>(64);
Parallel.For(0, 64, key => concurrent.Set(key, key * key));
for (int key = 0; key < 64; key++)
{
    Check(concurrent.TryGet(key, out int square) && square == key * key, "Concurrent writes retained");
}
concurrent.Clear();
Check(concurrent.Count == 0, "Cache cleared");
Console.WriteLine("Caching example passed: LRU, comparer, null values, fixed TTL boundary, removal and concurrent writes.");

static void Check(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException("Example failed: " + scenario);
    }
}

internal sealed class ExampleTimeProvider : TimeProvider
{
    private long timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public void AdvanceTicks(long ticks) => timestamp += ticks;
}
