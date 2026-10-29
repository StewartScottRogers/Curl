---
id: BL-4056
title: Measure CGR-289 ldaps:// --cacert variant against real curl and close BL-3818
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046, BL-4055]
touches: [Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4056 — Measure CGR-289 ldaps:// --cacert variant against real curl and close BL-3818

## Goal

The `--cacert` ldaps:// variant of CGR-289 matches real curl, and BL-3818 is closed.

## Context

CGR-289, part 3 of 3 of BL-4044. With a non-issuing CA (Curl.Output.UnitTests/Fixtures/LoopbackChain.pem) both already exit 60 with the same stderr. Measure the trusted-`--cacert` variant (gap report: Server Down) once BL-4054 and the -k match are in; pass `-TlsRootCertificateFile` directly to Record-CurlExchange.ps1, not splatted (it wrote no file in a hashtable-splatted run; fix the recorder if it still fails). When done, tick BL-3818's criteria from the Notes of BL-4044 and move BL-3818 to Done with the board script.

## Acceptance criteria

- [ ] `curl -sS --cacert CA ldaps://localhost:PORT/dc=test` (trusted CA) gives the same exit code, stderr and wire bytes through real curl and Curl; the result is recorded in Notes, and `Cgr289Tests.cs` pins any difference fixed.
- [ ] BL-3818's criteria are ticked and BL-3818 is in Done.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 08162493 8cf8d85c 779790c0.
