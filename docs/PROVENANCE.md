# Provenance

Created by Ben Schneider — https://github.com/Ben-Scr

This repository contains a new implementation of a bounded LRU cache using the .NET dictionary and
linked-list collections. No legacy package, application-specific source or third-party algorithm
source is included. Timestamp expiration uses exact integer cross multiplication with `UInt128` to
avoid rounding at the lifetime boundary. A separate list-based reference model checks mixed operations.

Build and test packages retain their own licenses and are not runtime dependencies. Package auditing
and build configuration adapt the existing BenScr.NeuralNetworks verification tools under the same
author license. The original custom LICENSE.txt is copied unchanged from that repository, including
its UTF-8 encoding and CRLF bytes. SHA-256:
`f6600e285e17c48188a65745d7595807e1755c72201e8eef201a9174fca18573`.

The common original icon is copied unchanged to `branding/BenScr_Icon_Master.png` and embedded as
`icon.png`. It is a transparent 1254 x 1254 PNG, 795819 bytes, SHA-256:
`a5dcabceb766e13c1840a95130ee98a458e12e3713fd9b07f3ceae75a1f5b993`.

The repository and NuGet target ID were checked before creation. The nuspec, portable PDBs and
generated release manifest identify the exact source commit used for a build.
