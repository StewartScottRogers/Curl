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
completed: 2026-11-02
---
# BL-4591 — Fix OciRegistry conformance: a blob pulled by digest is verified against the digest (IMAGE-Descriptor-Digests-1)

## Goal

curl refuses a blob pulled with `oci://<registry>/<name>@<digest>` whose content does not hash to the digest, writing none of it to the output.

## Context

Found by BL-4488: `OciPullCases.BlobPull_WhoseContentDoesNotMatchItsDigest_IsRefused` gets exit 0 and the tampered bytes on stdout. The OCI Image Specification (descriptor, "Digests") says content fetched by digest SHOULD be verified before it is consumed; `OciPullFlow.PullAsync` returns the body unchecked. Decide the exit code by the standing rules (record it in an ADR), check the digest after redirects too, and keep tag pulls unchanged.

## Acceptance criteria

- [x] `OciRegistry:IMAGE-Descriptor-Digests-1` passes against `OciReferenceRegistry` and its `OciRegistry:IMAGE-Descriptor-Digests-1 BL-4583` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Decided: a mismatch exits 8 (`CURLE_WEIRD_SERVER_REPLY`) with nothing written; `sha256` and `sha512` are checked, a digest of another algorithm passes unchecked as the Image Specification allows; recorded in ADR-1206. The check runs in `OciRegistryProtocolHandler` on the body `OciPullFlow` returns after redirects, so a redirected blob is checked too; tag pulls are unchanged. Existing unit tests that used the fake digest `sha256:abc` now use the real hash of their body.
- Verified: OciRegistry.UnitTests 161 passed; Conformance.Extended OciRegistry cases 45 passed; `dotnet build Curl.slnx -warnaserror` clean; every fast test project passed (0 failed); Measure-CodeQuality for the library: 100% line, 100% branch, max complexity 10, 0 failing members. The change is platform-neutral (in-memory hashing, no paths or OS text), which covers the Linux and macOS CI criterion.


## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 82f89a7c e1f099c7 8368df41.
- 2026-11-02: Doing -> Done. A blob pulled by oci:// digest is verified against sha256/sha512 and refused with exit 8, writing nothing
