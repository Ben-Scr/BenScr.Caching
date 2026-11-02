using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BenScr.Caching.Tests;

public sealed class CacheTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CapacityMustBePositive(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<int, int>(capacity));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void TimestampFrequencyMustBePositive(long frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<int, int>(1, new ManualTimeProvider(frequency)));
    }

    [Fact]
    public void LargeCapacityDoesNotAllocateAllSlotsUpFront()
    {
        var cache = new LruCache<int, int>(int.MaxValue);
        Assert.Equal(int.MaxValue, cache.Capacity);
        Assert.Equal(0, cache.Count);
        cache.Set(1, 42);
        Assert.True(cache.TryGet(1, out int value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void MissingKeyReturnsFalseAndDefault()
    {
        var cache = new LruCache<string, int>(1);
        Assert.False(cache.TryGet("absent", out int value));
        Assert.Equal(0, value);
        Assert.False(cache.Remove("absent"));
        Assert.Equal(0, cache.RemoveExpired());
    }

    [Fact]
    public void SuccessfulReadRefreshesRecency()
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 10);
        cache.Set(2, 20);
        Assert.True(cache.TryGet(1, out _));
        cache.Set(3, 30);
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out int first));
        Assert.Equal(10, first);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void FailedReadDoesNotRefreshAnyEntry()
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 10);
        cache.Set(2, 20);
        Assert.False(cache.TryGet(3, out _));
        cache.Set(3, 30);
        Assert.False(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(2, out _));
    }

    [Fact]
    public void ReplacementRefreshesRecencyWithoutEvictingAnotherKey()
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 10);
        cache.Set(2, 20);
        cache.Set(1, 11);
        Assert.Equal(2, cache.Count);
        cache.Set(3, 30);
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out int value));
        Assert.Equal(11, value);
    }

    [Fact]
    public void CapacityOneAlwaysRetainsMostRecentWrite()
    {
        var cache = new LruCache<int, int>(1);
        for (int i = 0; i < 50; i++)
        {
            cache.Set(i, i * 2);
            Assert.False(cache.TryGet(i - 1, out _));
            Assert.True(cache.TryGet(i, out int value));
            Assert.Equal(i * 2, value);
            Assert.Equal(1, cache.Count);
        }
    }

    [Fact]
    public void NullableValuesAreHits()
    {
        var cache = new LruCache<string, string?>(1);
        cache.Set("key", null);
        Assert.True(cache.TryGet("key", out string? value));
        Assert.Null(value);
        Assert.False(cache.TryGet("missing", out value));
        Assert.Null(value);
        Assert.True(cache.Remove("key"));
    }

    [Fact]
    public void ComparerControlsReadsReplacementAndRemoval()
    {
        var cache = new LruCache<string, int>(2, comparer: StringComparer.OrdinalIgnoreCase);
        cache.Set("Alice", 1);
        cache.Set("ALICE", 2);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("alice", out int value));
        Assert.Equal(2, value);
        cache.Set("Bob", 3);
        cache.Set("Carol", 4);
        Assert.False(cache.TryGet("ALICE", out _));
        Assert.True(cache.Remove("BOB"));
    }

    [Fact]
    public void NullKeysAreRejectedWithoutMutation()
    {
        var cache = new LruCache<string, int>(1);
        cache.Set("kept", 1);
        Assert.Throws<ArgumentNullException>(() => cache.Set(null!, 2));
        Assert.Throws<ArgumentNullException>(() => cache.TryGet(null!, out _));
        Assert.Throws<ArgumentNullException>(() => cache.Remove(null!));
        Assert.True(cache.TryGet("kept", out int value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void RemoveAndClearResetStorageForReuse()
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 1);
        cache.Set(2, 2);
        Assert.True(cache.Remove(1));
        Assert.False(cache.Remove(1));
        cache.Set(3, 3);
        Assert.True(cache.TryGet(2, out _));
        cache.Clear();
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet(2, out _));
        cache.Set(4, 4);
        cache.Set(5, 5);
        Assert.True(cache.TryGet(4, out _));
        Assert.True(cache.TryGet(5, out _));
    }

    [Fact]
    public void ValuesAreBorrowedAndNeverDisposed()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, OwnedValue>(1, clock);
        var value = new OwnedValue();
        cache.Set(1, value);
        value.Number = 7;
        Assert.True(cache.TryGet(1, out OwnedValue? retrieved));
        Assert.Same(value, retrieved);
        Assert.Equal(7, retrieved.Number);
        cache.Set(2, value);
        cache.Remove(2);
        cache.Set(3, value);
        cache.Clear();
        cache.Set(4, value, TimeSpan.FromTicks(1));
        clock.AdvanceTicks(1);
        cache.RemoveExpired();
        Assert.False(value.Disposed);
    }

    [Fact]
    public async Task ConcurrentRemovalHasExactlyOneSuccessfulCaller()
    {
        var cache = new LruCache<string, int>(1);
        cache.Set("key", 42);
        Task<bool>[] removals = Enumerable.Range(0, 32).Select(_ => Task.Run(() => cache.Remove("key"))).ToArray();
        bool[] outcomes = await Task.WhenAll(removals);
        Assert.Single(outcomes, value => value);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task ConcurrentWritesPublishCompleteValues()
    {
        var cache = new LruCache<int, Pair>(1);
        cache.Set(0, new Pair(0, 0));
        Task[] workers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                long value = ((long)worker << 32) + i;
                cache.Set(0, new Pair(value, value));
                Assert.True(cache.TryGet(0, out Pair pair));
                Assert.Equal(pair.First, pair.Second);
            }
        })).ToArray();
        await Task.WhenAll(workers);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task ConcurrentMutationsPreserveCapacityAndStorageInvariants()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(32, clock);
        Task[] workers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                int key = worker * 500 + i;
                cache.Set(key, key, TimeSpan.FromTicks(5));
                if (cache.TryGet(key, out int value))
                {
                    Assert.Equal(key, value);
                }
                cache.Remove(key - 1);
                if (i % 7 == 0)
                {
                    clock.AdvanceTicks(1);
                    cache.RemoveExpired();
                }
                if (i % 31 == 0)
                {
                    cache.Clear();
                }
                Assert.InRange(cache.Count, 0, 32);
            }
        })).ToArray();
        await Task.WhenAll(workers);
        cache.Clear();
        for (int key = 0; key < 32; key++)
        {
            cache.Set(key, key);
        }
        for (int key = 0; key < 32; key++)
        {
            Assert.True(cache.TryGet(key, out int value));
            Assert.Equal(key, value);
        }
    }

    private readonly record struct Pair(long First, long Second);

    private sealed class OwnedValue : IDisposable
    {
        public int Number { get; set; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
