---
id: BL-4591
title: Fix OciRegistry conformance: a blob pulled by digest is verified against the digest (IMAGE-Descriptor-Digests-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciPullFlow.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciRegistryProtocolHandler.cs, Curl.Extended.Protocol.OciRegistry.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4591 — Fix OciRegistry conformance: a blob pulled by digest is verified against the digest (IMAGE-Descriptor-Digests-1)

## Goal

curl refuses a blob pulled with `oci://<registry>/<name>@<digest>` whose content does not hash to the digest, writing none of it to the output.

## Context

Found by BL-4488: `OciPullCases.BlobPull_WhoseContentDoesNotMatchItsDigest_IsRefused` gets exit 0 and the tampered bytes on stdout. The OCI Image Specification (descriptor, "Digests") says content fetched by digest SHOULD be verified before it is consumed; `OciPullFlow.PullAsync` returns the body unchecked. Decide the exit code by the standing rules (record it in an ADR), check the digest after redirects too, and keep tag pulls unchanged.

## Acceptance criteria

- [ ] `OciRegistry:IMAGE-Descriptor-Digests-1` passes against `OciReferenceRegistry` and its `OciRegistry:IMAGE-Descriptor-Digests-1 BL-4583` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes


## Log

- 2026-11-02: Created.
