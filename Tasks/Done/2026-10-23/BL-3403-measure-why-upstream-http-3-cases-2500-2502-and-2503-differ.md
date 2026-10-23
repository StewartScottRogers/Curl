---
id: BL-3403
title: Measure why upstream HTTP/3 cases 2500, 2502 and 2503 differ on Windows, and hold them there if Curl matches the Schannel reference
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.Http3Cases.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamHttp3ServerTests.cs, Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/ADR-0720-the-harness-reports-http3-to-cases-naming-its-http3-stand-in.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3403 — Measure why upstream HTTP/3 cases 2500, 2502 and 2503 differ on Windows, and hold them there if Curl matches the Schannel reference

## Goal

Upstream HTTP/3 cases 2500, 2502 and 2503 are measured on Windows, and each either passes there and is held on Windows, or its Windows difference is shown to be the Schannel reference's own behaviour and recorded.

## Context

Stewart (2026-10-23) wants every test passing on Windows. The HTTP/3 cases 2500 to 2503 are on `PassingUpstreamCases.txt` (BL-2838) but `IsHeldOnThisPlatform` in `UpstreamConformanceTests.Http3Cases.cs` runs them on Windows without holding them (ADR-0498, ADR-0720): there Curl matches the Schannel build, whose revocation check under `--cacert` fails on the generated test CA (exit 60). A full Windows run on 2026-10-23 reports 2500 and 2502 with `<protocol>` differing and 2503 with `<stdout>` differing (2501 passes and is already listed). Run each with `$env:CURL_UPSTREAM_CASE` and read the difference. ADR-0797 and ADR-0798 (`--cacert` leaf as trust anchor on Schannel, partial-chain anchors on OpenSSL) may already change the picture. Measure the reference curl 8.22.0 (`/ucrt64/bin/curl`, Schannel, ADR-0018) against the same stand-in server, noting that every reference build refuses `--http3` (ADR-0540); if curl itself cannot run these on Windows, the cases stay unheld on Windows and the finding is recorded, not worked around by changing the case's arguments.

## Acceptance criteria

- [x] A Notes entry gives, for each of 2500, 2502 and 2503 on Windows, the first difference (`<protocol>` or `<stdout>` line) or the exit code, and the cause.
- [x] Where the cause is Curl's or the stand-in server's, it is fixed with a focused unit test in the fixed library's `.UnitTests` project (ADR-0482) and the case passes on Windows with `$env:CURL_UPSTREAM_CASE` and is held there (`IsHeldOnThisPlatform` no longer excludes it).
- [x] Where the cause is the Schannel reference's own behaviour, an addendum to ADR-0720 records it with the measured evidence, and the cases keep their `Inconclusive` outcome on Windows with a reason that says so.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured on Windows (2026-10-23), `CURL_UPSTREAM_CASE` with the Manual test, after ADR-0797/0798:
  - 2500: first difference `<protocol>` line 1, expected `GET https://localhost:9019/2500 HTTP/1.1`, got the end; Curl exit 60, `curl: (60) schannel: the revocation status is unknown`. Trace: the QUIC handshake fails certificate verification (`CAfile: .../test-ca.crt`), the h2 fallback finds no TCP listener.
  - 2502: first difference `<protocol>` line 1, expected `GET https://localhost:9019/path/25020001 HTTP/1.1`, got the end. `lib2502` sets `CURLOPT_CAINFO` to `test-ca.cacert` with HTTP/3 only, so the same verification stops it before any request (its kept log folder is behind a deny rule in the lane, so its stderr line was not read).
  - 2503: first difference `<stdout>` line 1, expected `HTTP/3 200 `, got `{` (empty `%{header_json}`); Curl exit 60, same revocation message.
  - Cause: the generated test CA names no revocation endpoint, so the Schannel build's revocation check under `--cacert` ends "status unknown". Reference curl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\curl.exe`) refuses `--http3`; over HTTPS through `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile` (a private root with no revocation endpoint), `curl -sS --cacert root.pem https://localhost:18519/2500` exits 60 with the same message. So it is the Schannel reference's own behaviour, not Curl's or the stand-in's: no fix and no unit test (criterion 2 does not apply), the cases stay unheld on Windows, and ADR-0720 has an addendum with the evidence.
- The Windows `Inconclusive` reason now starts with `Http3CaseNotHeldOnWindowsReason` (`WithPlatformHoldReason` in `UpstreamConformanceTests.Http3Cases.cs`); checked on the test2500 and test2503 ratchet rows.
- Added `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` to touches: the `Assert.Inconclusive` call that gives the reason is there. No task in Doing on origin/work/dark-factory names that file.
- Only test-project code changed (no production library), so no Measure-CodeQuality run was needed. Build clean with -warnaserror; fast tests 0 failed in every project. The change does not depend on the platform: off Windows `IsHeldOnThisPlatform` is true and messages are unchanged, so it meets the Linux and macOS side as well.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f4f89f15 19d14a3f 90a10b16 c5f8cef6.
- 2026-10-23: Doing -> Done. 2500, 2502 and 2503 measured on Windows: Schannel's own revocation check (exit 60), matching curl 8.22.0; ADR-0720 addendum, and the Inconclusive reason now says so
