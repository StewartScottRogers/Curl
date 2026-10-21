---
id: AF-0291
title: Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Api.UnitTests/Readme/ReadmeTests.cs:Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0291 - Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Readme/ReadmeTests.cs:79`: Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion.

## Evidence

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:79`

Step 0 candidate (no-assertion), read in full. The body is only await ReadmeSamples.CreateClientAsync(); and the sample builds a CurlClient and disposes it. Nothing checks that the client is over the managed engine or that nothing was sent, which is what the name states. It fails only if Create throws.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything' -Context 0,3
```

- Expected: The body asserts the engine kind and that no transfer was performed.
- Actual: The body is { await ReadmeSamples.CreateClientAsync(); } with no Assert.

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
