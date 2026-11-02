using System;
using System.Threading;

namespace BenScr.Caching.Tests;

internal sealed class ManualTimeProvider(long frequency = TimeSpan.TicksPerSecond, long initialTimestamp = 0) : TimeProvider
{
    private long timestamp = initialTimestamp;
    private int timestampReads;

    public override long TimestampFrequency => frequency;
    public DateTimeOffset UtcTime { get; set; } = DateTimeOffset.UnixEpoch;
    public bool ThrowOnTimestamp { get; set; }
    public long StepAfterRead { get; set; }
    public int TimestampReads => timestampReads;

    public override DateTimeOffset GetUtcNow() => UtcTime;

    public override long GetTimestamp()
    {
        if (ThrowOnTimestamp)
        {
            throw new InvalidOperationException("Clock unavailable.");
        }
        Interlocked.Increment(ref timestampReads);
        return unchecked(Interlocked.Add(ref timestamp, StepAfterRead) - StepAfterRead);
    }

    public void AdvanceTicks(long ticks) => Interlocked.Add(ref timestamp, ticks);
}
