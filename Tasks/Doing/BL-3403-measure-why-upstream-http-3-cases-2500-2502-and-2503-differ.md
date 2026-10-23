---
id: BL-3403
title: Measure why upstream HTTP/3 cases 2500, 2502 and 2503 differ on Windows, and hold them there if Curl matches the Schannel reference
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.Http3Cases.cs, Curl.Conformance.UnitTests/UpstreamHttp3ServerTests.cs, Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/ADR-0720-the-harness-reports-http3-to-cases-naming-its-http3-stand-in.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3403 — Measure why upstream HTTP/3 cases 2500, 2502 and 2503 differ on Windows, and hold them there if Curl matches the Schannel reference

## Goal

Upstream HTTP/3 cases 2500, 2502 and 2503 are measured on Windows, and each either passes there and is held on Windows, or its Windows difference is shown to be the Schannel reference's own behaviour and recorded.

## Context

Stewart (2026-10-23) wants every test passing on Windows. The HTTP/3 cases 2500 to 2503 are on `PassingUpstreamCases.txt` (BL-2838) but `IsHeldOnThisPlatform` in `UpstreamConformanceTests.Http3Cases.cs` runs them on Windows without holding them (ADR-0498, ADR-0720): there Curl matches the Schannel build, whose revocation check under `--cacert` fails on the generated test CA (exit 60). A full Windows run on 2026-10-23 reports 2500 and 2502 with `<protocol>` differing and 2503 with `<stdout>` differing (2501 passes and is already listed). Run each with `$env:CURL_UPSTREAM_CASE` and read the difference. ADR-0797 and ADR-0798 (`--cacert` leaf as trust anchor on Schannel, partial-chain anchors on OpenSSL) may already change the picture. Measure the reference curl 8.22.0 (`/ucrt64/bin/curl`, Schannel, ADR-0018) against the same stand-in server, noting that every reference build refuses `--http3` (ADR-0540); if curl itself cannot run these on Windows, the cases stay unheld on Windows and the finding is recorded, not worked around by changing the case's arguments.

## Acceptance criteria

- [ ] A Notes entry gives, for each of 2500, 2502 and 2503 on Windows, the first difference (`<protocol>` or `<stdout>` line) or the exit code, and the cause.
- [ ] Where the cause is Curl's or the stand-in server's, it is fixed with a focused unit test in the fixed library's `.UnitTests` project (ADR-0482) and the case passes on Windows with `$env:CURL_UPSTREAM_CASE` and is held there (`IsHeldOnThisPlatform` no longer excludes it).
- [ ] Where the cause is the Schannel reference's own behaviour, an addendum to ADR-0720 records it with the measured evidence, and the cases keep their `Inconclusive` outcome on Windows with a reason that says so.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f4f89f15 19d14a3f 90a10b16 c5f8cef6.
