using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BenScr.Caching;

/// <summary>A thread-safe, bounded least-recently-used cache with optional fixed entry lifetimes.</summary>
/// <typeparam name="TKey">The non-null key type.</typeparam>
/// <typeparam name="TValue">The value type. Null values are permitted.</typeparam>
/// <remarks>
/// Each operation is serialized under one lock. A sequence of operations is not a transaction.
/// Successful reads and writes make an entry most recently used. Reads do not extend its lifetime.
/// Expiration is lazy: there is no background timer. Count, Set and RemoveExpired scan all entries;
/// TryGet and Remove check only the requested key. Expired entries are removed before capacity eviction.
/// Keys and values are retained by reference, never cloned or disposed. The cache does not synchronize
/// access to the objects stored in it. Keys must retain stable equality and hash codes. A supplied
/// comparer must not throw or call back into this cache. A supplied time provider must have a fixed
/// positive timestamp frequency and monotonically advancing timestamps; UTC time is never consulted.
/// Timestamp rollover is supported, provided less than one complete 64-bit timestamp cycle passes
/// between an entry's insertion and its expiration check. The provider must not call back into this cache.
/// </remarks>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly object syncRoot = new();
    private readonly Dictionary<TKey, LinkedListNode<Entry>> entries;
    private readonly LinkedList<Entry> recency = new();
    private readonly TimeProvider timeProvider;
    private readonly long timestampFrequency;

    /// <summary>Creates an empty cache with a fixed maximum number of entries.</summary>
    /// <param name="capacity">The strictly positive maximum number of stored entries.</param>
    /// <param name="timeProvider">The timestamp source, or null for <see cref="TimeProvider.System"/>.</param>
    /// <param name="comparer">The key comparer, or null for the default comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Capacity or the provider's timestamp frequency is not positive.</exception>
    public LruCache(int capacity, TimeProvider? timeProvider = null, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        timestampFrequency = this.timeProvider.TimestampFrequency;
        if (timestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeProvider), "Timestamp frequency must be positive.");
        }
        Capacity = capacity;
        entries = new Dictionary<TKey, LinkedListNode<Entry>>(comparer);
    }

    /// <summary>Gets the maximum number of stored entries.</summary>
    public int Capacity { get; }

    /// <summary>Removes all entries expired at one timestamp snapshot, then returns the live count.</summary>
    /// <remarks>This operation scans the cache in O(n) time and does not change live entries' recency.</remarks>
    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                RemoveExpiredCore(timeProvider.GetTimestamp());
                return entries.Count;
            }
        }
    }

    /// <summary>Stores or replaces a value and makes it most recently used.</summary>
    /// <param name="key">The non-null key.</param>
    /// <param name="value">The value, which may be null.</param>
    /// <param name="timeToLive">The fixed lifetime from this call's timestamp, null for no expiration,
    /// or zero to remove the key without storing a value.</param>
    /// <remarks>
    /// A replacement resets the lifetime, including removal of a previous lifetime when null is passed.
    /// All expired entries are removed before the least recently used live entry is evicted if necessary.
    /// Invalid arguments are rejected before any mutation. This operation scans the cache in O(n) time.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The lifetime is negative, including Timeout.InfiniteTimeSpan.</exception>
    public void Set(TKey key, TValue value, TimeSpan? timeToLive = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (timeToLive < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Lifetime cannot be negative; use null for no expiration.");
        }
        lock (syncRoot)
        {
            long timestamp = timeProvider.GetTimestamp();
            RemoveExpiredCore(timestamp);
            if (timeToLive == TimeSpan.Zero)
            {
                if (entries.TryGetValue(key, out LinkedListNode<Entry>? removed))
                {
                    RemoveNode(removed);
                }
                return;
            }
            if (entries.TryGetValue(key, out LinkedListNode<Entry>? existing))
            {
                // Keep the original equal key so dictionary and linked-list identities stay aligned.
                existing.Value = new Entry(existing.Value.Key, value, timestamp, timeToLive);
                MakeMostRecent(existing);
                return;
            }
            var node = new LinkedListNode<Entry>(new Entry(key, value, timestamp, timeToLive));
            entries.Add(key, node);
            recency.AddFirst(node);
            if (entries.Count > Capacity)
            {
                RemoveNode(recency.Last!);
            }
        }
    }

    /// <summary>Retrieves a live value and makes it most recently used without extending its lifetime.</summary>
    /// <param name="key">The non-null key.</param>
    /// <param name="value">The stored value on success, otherwise default. Stored null values still return true.</param>
    /// <returns>True if a live entry was found; false if absent or expired.</returns>
    /// <remarks>Removes the requested entry when expired. Other entries are not examined. Expected O(1) time.</remarks>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            long timestamp = timeProvider.GetTimestamp();
            if (entries.TryGetValue(key, out LinkedListNode<Entry>? node))
            {
                if (!IsExpired(node.Value, timestamp))
                {
                    value = node.Value.Value;
                    MakeMostRecent(node);
                    return true;
                }
                RemoveNode(node);
            }
            value = default;
            return false;
        }
    }

    /// <summary>Removes a key, returning whether its entry was live at this call's timestamp.</summary>
    /// <param name="key">The non-null key.</param>
    /// <returns>True for a removed live entry; false for an absent or expired entry.</returns>
    /// <remarks>An expired entry is still physically removed. Other entries are not examined. Expected O(1) time.</remarks>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            long timestamp = timeProvider.GetTimestamp();
            if (!entries.TryGetValue(key, out LinkedListNode<Entry>? node))
            {
                return false;
            }
            bool wasLive = !IsExpired(node.Value, timestamp);
            RemoveNode(node);
            return wasLive;
        }
    }

    /// <summary>Removes all entries expired at one timestamp snapshot without changing live recency.</summary>
    /// <returns>The number of expired entries physically removed by this call.</returns>
    /// <remarks>This operation scans the cache in O(n) time.</remarks>
    public int RemoveExpired()
    {
        lock (syncRoot)
        {
            return RemoveExpiredCore(timeProvider.GetTimestamp());
        }
    }

    /// <summary>Removes all stored entries, including expired ones, without disposing any values.</summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            entries.Clear();
            recency.Clear();
        }
    }

    private bool IsExpired(Entry entry, long timestamp)
    {
        if (entry.TimeToLive is not TimeSpan lifetime)
        {
            return false;
        }
        // Integer cross multiplication avoids rounding at TTL boundaries and overflows in deadlines.
        // Unsigned subtraction also preserves elapsed ticks across the signed timestamp rollover.
        ulong elapsed = unchecked((ulong)(timestamp - entry.Timestamp));
        return (UInt128)elapsed * TimeSpan.TicksPerSecond >= (UInt128)lifetime.Ticks * (ulong)timestampFrequency;
    }

    private int RemoveExpiredCore(long timestamp)
    {
        int removed = 0;
        LinkedListNode<Entry>? node = recency.Last;
        while (node is not null)
        {
            LinkedListNode<Entry>? previous = node.Previous;
            if (IsExpired(node.Value, timestamp))
            {
                RemoveNode(node);
                removed++;
            }
            node = previous;
        }
        return removed;
    }

    private void MakeMostRecent(LinkedListNode<Entry> node)
    {
        recency.Remove(node);
        recency.AddFirst(node);
    }

    private void RemoveNode(LinkedListNode<Entry> node)
    {
        entries.Remove(node.Value.Key);
        recency.Remove(node);
    }

    private readonly record struct Entry(TKey Key, TValue Value, long Timestamp, TimeSpan? TimeToLive);
}
