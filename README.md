# BenScr.Caching

![BenScr](https://raw.githubusercontent.com/Ben-Scr/BenScr.Caching/master/branding/BenScr_Icon_Master.png)

A bounded, thread-safe LRU cache for .NET 10. Entries can have individual fixed lifetimes, measured
with monotonic `TimeProvider` timestamps. The library has no runtime package dependencies.

Created by Ben Schneider — https://github.com/Ben-Scr

## Install

```shell
dotnet add package BenScr.Caching --version 1.0.0
```

```csharp
using System;
using BenScr.Caching;

var profiles = new LruCache<string, string>(capacity: 100,
    comparer: StringComparer.OrdinalIgnoreCase);
profiles.Set("profile:42", "Ada", TimeSpan.FromMinutes(5));

if (profiles.TryGet("PROFILE:42", out string? name))
{
    Console.WriteLine(name);
}

profiles.Remove("profile:42");
```

The [complete executable example](https://github.com/Ben-Scr/BenScr.Caching/blob/master/examples/Program.cs)
also demonstrates deterministic expiration, null values, LRU eviction and concurrent writes.

## Cache behavior

| Operation | Contract |
| --- | --- |
| `new LruCache<TKey,TValue>(capacity, timeProvider, comparer)` | Positive fixed capacity; optional provider and comparer default to `TimeProvider.System` and the key type's default equality. |
| `Set(key, value, timeToLive)` | Inserts or replaces a value, moves it to most recent, and starts a new fixed lifetime. Purges expired entries before evicting the least recent live entry if needed. |
| `TryGet(key, out value)` | Returns true for a live entry, including a stored null. A hit moves the entry to most recent but does not extend its lifetime. A miss returns default. |
| `Remove(key)` | Removes the key and returns whether it was live at the timestamp snapshot. An expired entry is removed but returns false. |
| `Count` | Removes all expired entries at one timestamp snapshot, then returns the live count. Leaves live recency unchanged. |
| `RemoveExpired()` | Removes all expired entries at one timestamp snapshot and returns the number physically removed by this call. Leaves live recency unchanged. |
| `Clear()` | Removes every entry. Capacity remains unchanged. |

`timeToLive: null` means no expiration. Zero removes the key without storing or evicting another live
entry. Every negative lifetime, including `Timeout.InfiniteTimeSpan`, is rejected before mutation.
Passing null for a replacement removes any previous expiration. An entry is expired when elapsed
time is **greater than or equal to** its lifetime. Successful reads never make expiration slide.

`Set`, `Count` and `RemoveExpired` scan the cache in O(n) time. `TryGet` and `Remove` examine only
their requested key and take expected O(1) time. There is no background timer: expired entries can
retain references until an operation removes them. The number of stored entries never exceeds the
capacity after an operation completes. Capacity limits entry count, not bytes.

## Time, ownership and concurrency

Supply a `TimeProvider` to control timestamps in tests. The cache never reads UTC or local time, so
wall-clock corrections do not extend or shorten lifetimes. It compares elapsed timestamp ticks and
lifetime ticks using exact integer arithmetic. The provider needs a fixed positive timestamp frequency
and monotonically advancing timestamps. Signed timestamp rollover is supported, provided an entry is
checked before one complete 64-bit timestamp cycle elapses. Custom providers must not call back into
the cache. The test suite advances a manual provider and never sleeps.

Each public operation is serialized under one lock. Time-sensitive operations use one timestamp
snapshot inside that lock; their result describes that instant. A value can expire immediately after
a successful read. A sequence such as `TryGet` followed by `Set` is not an atomic transaction. There
are no factories, eviction callbacks, asynchronous operations or background workers.

Keys and values are retained by reference, never copied or disposed. The cache does not synchronize
mutations of stored objects. Keys must have stable equality and hash codes. A supplied comparer must
not throw or call back into the cache. Null keys are rejected; nullable values are supported. Invalid
arguments and timestamp acquisition failures occur before any storage or recency mutation.

## Build and verify

Use the .NET SDK pinned in `global.json` and PowerShell 7:

```powershell
dotnet test --project tests/BenScr.Caching.Tests.csproj -c Release
dotnet run --project examples/Example.csproj -c Release
# Full verification requires a clean committed checkout with its GitHub origin.
pwsh -File tools/Verify.ps1
```

The verifier checks locked restores, builds, formatting, tests, the reviewed public API, the example,
exact package contents, XML documentation, portable PDB identities and Source Link checksums. It then
restores the resulting package into a separate project with an empty cache and runs the complete
example through `PackageReference`. Windows and Ubuntu CI run the same script.

See [verification details](https://github.com/Ben-Scr/BenScr.Caching/blob/master/VERIFICATION.md),
[release procedure](https://github.com/Ben-Scr/BenScr.Caching/blob/master/RELEASE.md),
[public API](https://github.com/Ben-Scr/BenScr.Caching/blob/master/docs/PublicApi.txt), and
[provenance](https://github.com/Ben-Scr/BenScr.Caching/blob/master/docs/PROVENANCE.md).

## License

The original [custom author license](https://github.com/Ben-Scr/BenScr.Caching/blob/master/LICENSE.txt)
applies. It requires visible attribution to Ben Schneider; it is not the standard MIT license.
