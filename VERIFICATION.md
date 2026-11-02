# Verification

The initial local Windows x64 test run passed 48 tests with zero failed or skipped tests. Tests cover
LRU reads and replacement, miss behavior, capacity, custom comparison, nullable values, caller-owned
objects, removal and clear. Expiration tests cover exact boundaries, fixed lifetimes, replacement,
zero and negative lifetimes, UTC jumps, nonstandard timestamp frequencies, signed timestamp rollover,
maximum TimeSpan, cleanup counts, expired-before-live eviction and timestamp failure atomicity.

Three seeded runs each compare 2,500 mixed operations against an independent list-based reference
model. Concurrent tests verify that removal has exactly one successful caller, complete struct values
are published, and mixed writes, reads, cleanup and clearing preserve storage invariants. All expiry
tests use a manual TimeProvider and no sleeps.

The xUnit project uses the .NET 10 Microsoft Testing Platform runner. The verifier rejects an empty
test run or any incomplete outcome and checks the public API baseline against the built assembly.
The full example is executed both with a project reference and in a separate package consumer whose
package cache starts empty. Local package mapping cannot silently fall back to a public copy.

Final artifact verification requires a clean committed checkout. The generated manifest records the
exact source commit and nupkg/snupkg hashes, archive/PDB/Source Link audit and fresh consumer results.
`artifacts/latest-verification.txt` locates that evidence. Windows and Ubuntu CI execute the same
script; configured jobs alone are not evidence of a completed run. Actual CI outcomes and public-feed
checks are recorded with the release, separately from local pack success.

Interactive debugger stepping, other architectures, Native AOT, trimming and performance benchmarks
are not validated claims.
