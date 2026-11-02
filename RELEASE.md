# Release procedure

The package targets .NET 10 and uses version 1.0.0. Its source tag is
`packages/BenScr.Caching/1.0.0`. Tags identify the immutable source state; publication never
replaces an existing package version or moves an existing release tag.

From a clean checkout, run `pwsh -File tools/Verify.ps1` before creating the tag. The verifier emits
an exact nupkg/snupkg pair, tests, archive/PDB audit, isolated consumer evidence and a release manifest.
`artifacts/latest-verification.txt` identifies that output directory. Verify that the tag, manifest
and nuspec identify the same source commit. Publish those exact tested files; any rebuild needs its
own artifact verification. Publish both the nupkg and adjacent snupkg to nuget.org using an authorized
upload session or suitably scoped credential. Never commit credentials.

After NuGet indexing, verify the real feed with a fresh cache:

```powershell
$commit = git rev-list -n 1 packages/BenScr.Caching/1.0.0
pwsh -File tools/TestPackage.ps1 -Source https://api.nuget.org/v3/index.json `
  -ExpectedCommit $commit -EvidencePath "$PWD/artifacts/public-consumer.json"
```

Check the downloaded archive payloads against the verified files. NuGet may add a repository
signature, changing the overall ZIP hash. Verify the public PDB download, Source Link and rendered
icon, then record the exact package/CI URLs and completed checks. A queued upload is not evidence
of an indexed or verified publication. GitHub release assets include the original package pair and
the final release manifest without local machine paths.

The local Windows checks and remote Windows/Ubuntu CI are separate evidence. Configured CI does not
by itself prove a successful run. Interactive debugger stepping, other architectures, Native AOT and
trimming are not release claims.
