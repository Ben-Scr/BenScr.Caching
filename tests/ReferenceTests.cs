using System;
using System.Collections.Generic;
using Xunit;

namespace BenScr.Caching.Tests;

public sealed class ReferenceTests
{
    [Theory]
    [InlineData(42)]
    [InlineData(81)]
    [InlineData(2026)]
    public void MixedOperationsMatchIndependentListModel(int seed)
    {
        var random = new Random(seed);
        var clock = new ManualTimeProvider();
        var cache = new LruCache<int, int>(7, clock);
        var model = new List<ModelEntry>(); // Least recent first; no dictionary or linked list.
        long now = 0;
        for (int step = 0; step < 2_500; step++)
        {
            int key = random.Next(16);
            int advance = random.Next(4);
            now += advance;
            clock.AdvanceTicks(advance);
            switch (random.Next(6))
            {
                case 0:
                    {
                        long? lifetime = random.Next(5) switch { 0 => null, 1 => 0, 2 => 1, 3 => 3, _ => 10 };
                        model.RemoveAll(item => item.ExpiresAt <= now || item.Key == key);
                        if (lifetime != 0)
                        {
                            model.Add(new ModelEntry(key, step, lifetime.HasValue ? now + lifetime : null));
                            if (model.Count > 7)
                            {
                                model.RemoveAt(0);
                            }
                        }
                        cache.Set(key, step, lifetime.HasValue ? TimeSpan.FromTicks(lifetime.Value) : null);
                        break;
                    }
                case 1:
                    {
                        int index = model.FindIndex(item => item.Key == key);
                        bool expected = index >= 0 && !(model[index].ExpiresAt <= now);
                        int expectedValue = expected ? model[index].Value : 0;
                        if (index >= 0)
                        {
                            ModelEntry entry = model[index];
                            model.RemoveAt(index);
                            if (expected)
                            {
                                model.Add(entry);
                            }
                        }
                        Assert.Equal(expected, cache.TryGet(key, out int value));
                        Assert.Equal(expectedValue, value);
                        break;
                    }
                case 2:
                    {
                        int index = model.FindIndex(item => item.Key == key);
                        bool expected = index >= 0 && !(model[index].ExpiresAt <= now);
                        if (index >= 0)
                        {
                            model.RemoveAt(index);
                        }
                        Assert.Equal(expected, cache.Remove(key));
                        break;
                    }
                case 3:
                    Assert.Equal(model.RemoveAll(item => item.ExpiresAt <= now), cache.RemoveExpired());
                    break;
                case 4:
                    model.RemoveAll(item => item.ExpiresAt <= now);
                    Assert.Equal(model.Count, cache.Count);
                    break;
                case 5:
                    if (step % 23 == 0)
                    {
                        model.Clear();
                        cache.Clear();
                    }
                    break;
            }
        }
        for (int key = 0; key < 16; key++)
        {
            ModelEntry? expected = model.Find(item => item.Key == key && !(item.ExpiresAt <= now));
            Assert.Equal(expected is not null, cache.TryGet(key, out int value));
            Assert.Equal(expected?.Value ?? 0, value);
        }
    }

    private sealed record ModelEntry(int Key, int Value, long? ExpiresAt);
}
