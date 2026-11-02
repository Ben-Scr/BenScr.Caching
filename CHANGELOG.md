# Changelog

## 1.0.0

- Add a generic bounded LRU cache with thread-safe individual operations and custom key comparison.
- Support nullable values, replacement, removal, clear, live counts and explicit expiry cleanup.
- Add optional fixed per-entry lifetimes backed by monotonic TimeProvider timestamps, with exact
  expiry boundaries, zero-lifetime removal and timestamp rollover support.
- Include deterministic expiry and reference-model tests, concurrency tests, an executable example,
  XML documentation, an API baseline and package-consumer verification on Windows and Ubuntu CI.
