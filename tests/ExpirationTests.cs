using System;
using Xunit;

namespace BenScr.Caching.Tests;

public sealed class ExpirationTests
{
    [Theory]
    [InlineData(99L, true)]
    [InlineData(100L, false)]
    [InlineData(101L, false)]
    public void LifetimeExpiresAtExactBoundary(long elapsed, bool expectedHit)
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 42, TimeSpan.FromTicks(100));
        clock.AdvanceTicks(elapsed);
        Assert.Equal(expectedHit, cache.TryGet(1, out int value));
        Assert.Equal(expectedHit ? 42 : 0, value);
        Assert.Equal(expectedHit ? 1 : 0, cache.Count);
    }

    [Fact]
    public void ReadsDoNotExtendLifetime()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 42, TimeSpan.FromTicks(10));
        clock.AdvanceTicks(9);
        Assert.True(cache.TryGet(1, out _));
        clock.AdvanceTicks(1);
        Assert.False(cache.TryGet(1, out _));
    }

    [Fact]
    public void ReplacementResetsLifetime()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(10));
        clock.AdvanceTicks(9);
        cache.Set(1, 2, TimeSpan.FromTicks(10));
        clock.AdvanceTicks(9);
        Assert.True(cache.TryGet(1, out int value));
        Assert.Equal(2, value);
        clock.AdvanceTicks(1);
        Assert.False(cache.TryGet(1, out _));
    }

    [Fact]
    public void NullLifetimeRemovesPreviousExpiration()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(1));
        cache.Set(1, 2);
        clock.AdvanceTicks(long.MaxValue);
        Assert.True(cache.TryGet(1, out int value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void NullLifetimeIsUnaffectedByClockChanges()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 42);
        clock.AdvanceTicks(long.MaxValue);
        Assert.Equal(0, cache.RemoveExpired());
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void ZeroLifetimeRemovesExistingKeyWithoutEvictingOthers()
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 1);
        cache.Set(2, 2);
        cache.Set(3, 3, TimeSpan.Zero);
        Assert.Equal(2, cache.Count);
        cache.Set(2, 22, TimeSpan.Zero);
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out _));
        Assert.Equal(1, cache.Count);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(-10_000L)]
    [InlineData(long.MinValue)]
    public void NegativeLifetimeIsRejectedBeforeMutation(long ticks)
    {
        var cache = new LruCache<int, int>(2);
        cache.Set(1, 1);
        cache.Set(2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Set(1, 11, TimeSpan.FromTicks(ticks)));
        cache.Set(3, 3);
        Assert.False(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(2, out int value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void InvalidLifetimeDoesNotPerformExpiryCleanup()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(1));
        clock.AdvanceTicks(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Set(2, 2, TimeSpan.FromTicks(-1)));
        Assert.Equal(1, cache.RemoveExpired());
    }

    [Fact]
    public void UtcClockJumpsDoNotAffectExpiration()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(10));
        clock.UtcTime = DateTimeOffset.MaxValue;
        Assert.True(cache.TryGet(1, out _));
        clock.UtcTime = DateTimeOffset.MinValue;
        clock.AdvanceTicks(10);
        Assert.False(cache.TryGet(1, out _));
    }

    [Theory]
    [InlineData(3L, 1L, false)]
    [InlineData(3L, 2L, true)]
    [InlineData(20_000_000L, 1L, false)]
    [InlineData(20_000_000L, 2L, true)]
    public void ExpirationUsesProviderFrequencyWithoutRounding(long frequency, long elapsed, bool expired)
    {
        var clock = new ManualTimeProvider(frequency);
        var cache = new LruCache<int, int>(1, clock);
        TimeSpan ttl = frequency == 3 ? TimeSpan.FromSeconds(0.5) : TimeSpan.FromTicks(1);
        cache.Set(1, 1, ttl);
        clock.AdvanceTicks(elapsed);
        Assert.Equal(expired, !cache.TryGet(1, out _));
    }

    [Fact]
    public void LargeLifetimeAndFrequencyDoNotOverflow()
    {
        var clock = new ManualTimeProvider(long.MaxValue);
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.MaxValue);
        clock.AdvanceTicks(long.MaxValue);
        Assert.True(cache.TryGet(1, out _));
    }

    [Fact]
    public void SignedTimestampRolloverPreservesElapsedTime()
    {
        var clock = new ManualTimeProvider(initialTimestamp: long.MaxValue - 5);
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(10));
        clock.AdvanceTicks(9);
        Assert.True(cache.TryGet(1, out _));
        clock.AdvanceTicks(1);
        Assert.False(cache.TryGet(1, out _));
    }

    [Fact]
    public void MaximumLifetimeExpiresAtExactBoundaryWithoutDoubleRounding()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(1, clock);
        cache.Set(1, 1, TimeSpan.MaxValue);
        clock.AdvanceTicks(long.MaxValue - 1);
        Assert.True(cache.TryGet(1, out _));
        clock.AdvanceTicks(1);
        Assert.False(cache.TryGet(1, out _));
    }

    [Fact]
    public void ExpiredMruEntryIsRemovedBeforeLiveLruEviction()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(2, clock);
        cache.Set(1, 1);
        cache.Set(2, 2, TimeSpan.FromTicks(1));
        clock.AdvanceTicks(1);
        cache.Set(3, 3);
        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void CountCleansAllExpiredEntriesWithoutChangingLiveRecency()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(3, clock);
        cache.Set(1, 1);
        cache.Set(2, 2, TimeSpan.FromTicks(1));
        cache.Set(3, 3);
        clock.AdvanceTicks(1);
        Assert.Equal(2, cache.Count);
        Assert.Equal(0, cache.RemoveExpired());
        cache.Set(4, 4);
        cache.Set(5, 5);
        Assert.False(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(3, out _));
    }

    [Fact]
    public void RemoveExpiredReturnsOnlyEntriesRemovedByThatCall()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(4, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(1));
        cache.Set(2, 2, TimeSpan.FromTicks(2));
        cache.Set(3, 3, TimeSpan.FromTicks(1));
        cache.Set(4, 4);
        clock.AdvanceTicks(1);
        Assert.Equal(2, cache.RemoveExpired());
        Assert.Equal(0, cache.RemoveExpired());
        Assert.Equal(2, cache.Count);
        clock.AdvanceTicks(1);
        Assert.Equal(1, cache.RemoveExpired());
    }

    [Fact]
    public void KeyOperationsOnlyCleanRequestedExpiredEntry()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(3, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(1));
        cache.Set(2, 2, TimeSpan.FromTicks(1));
        cache.Set(3, 3, TimeSpan.FromTicks(1));
        clock.AdvanceTicks(1);
        Assert.False(cache.TryGet(1, out _));
        Assert.False(cache.Remove(2));
        Assert.Equal(1, cache.RemoveExpired());
    }

    [Fact]
    public void EachOperationUsesOneTimestampSnapshot()
    {
        var clock = new ManualTimeProvider { StepAfterRead = 10 };
        var cache = new LruCache<int, int>(2, clock);
        cache.Set(1, 1, TimeSpan.FromTicks(25));
        cache.Set(2, 2, TimeSpan.FromTicks(25));
        Assert.Equal(2, cache.Count);
        Assert.Equal(3, clock.TimestampReads);
        Assert.Equal(1, cache.RemoveExpired());
        Assert.Equal(4, clock.TimestampReads);
        Assert.False(cache.Remove(2));
        Assert.Equal(5, clock.TimestampReads);
    }

    [Fact]
    public void ClockFailureOccursBeforeMutation()
    {
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(2, clock);
        cache.Set(1, 1);
        cache.Set(2, 2);
        clock.ThrowOnTimestamp = true;
        Assert.Throws<InvalidOperationException>(() => cache.Set(1, 11));
        Assert.Throws<InvalidOperationException>(() => cache.TryGet(1, out _));
        Assert.Throws<InvalidOperationException>(() => cache.Remove(1));
        Assert.Throws<InvalidOperationException>(() => cache.RemoveExpired());
        Assert.Throws<InvalidOperationException>(() => cache.Count);
        clock.ThrowOnTimestamp = false;
        cache.Set(3, 3);
        Assert.False(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(2, out _));
    }
}
