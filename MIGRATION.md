# Compatibility

BenScr.Caching 1.0.0 is the first version of this library. There is no previous API or package ID to
migrate. It targets .NET 10 and does not advertise compatibility with .NET Standard, earlier .NET,
Unity, Native AOT or trimming. The reviewed public surface is recorded in `docs/PublicApi.txt`.

For cache implementations that use sliding expiration, account for this library's fixed lifetime:
`TryGet` updates LRU recency but never extends expiration. Explicitly call `Set` to reset a lifetime.
Use null for an unlimited lifetime; `Timeout.InfiniteTimeSpan` is negative and is rejected. Removal of
an expired entry returns false. Values remain caller-owned, including disposable values.
