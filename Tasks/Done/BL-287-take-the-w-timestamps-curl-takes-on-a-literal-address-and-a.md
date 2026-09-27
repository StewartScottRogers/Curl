---
id: BL-287
title: Take the -w timestamps curl takes on a literal address and a refused connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-287 — Take the -w timestamps curl takes on a literal address and a refused connect

## Goal

`%{time_namelookup}` for a literal address and `%{time_pretransfer}`, `%{time_posttransfer}` and `%{time_starttransfer}` after a refused connect print non-zero, as curl 8.21.0 prints them.

## Context

- Found by BL-226, which formats the `-w` times from `TransferTimings` (ADR-0035, "Consequences"). The formatting is right; the timestamps are not taken where curl takes them.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-26: `curl -s -o NUL -w "ns=%{time_namelookup}|c=%{time_connect}|pre=%{time_pretransfer}|post=%{time_posttransfer}|st=%{time_starttransfer}|t=%{time_total}" http://127.0.0.1:1/` printed `ns=0.000067|c=0.000000|pre=2.027121|post=2.027122|st=2.027122|t=2.027127`, exit 7. A literal `127.0.0.1` to a live loopback server printed `ns=0.000065`.
- Today `ConnectTimings.NameResolved` is `null` for a literal address (ADR-0030) and a refused connect reports no timings, so these print `0.000000`. Decide, with an ADR, whether the connector records a resolution for a literal and whether a failed transfer reports its timings.

## Acceptance criteria

- [x] A test pins non-zero `time_namelookup` for a literal address and the measured pretransfer/posttransfer/starttransfer behaviour after a refused connect, or an ADR records why not.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-09-27 (lane 1): The real gap was wider than the Context says: no handler set `TransferReport.Timings`, so every `%{time_*}` printed `0.000000`, and `TcpConnector` already set `NameResolved` for a literal (ADR-0030). `HttpProtocolHandler` now reports `TransferTimings` on every report (start, connect, request ready, request sent, first response byte through the new `HttpFirstByteTimingConnection`, end), and a failed connect reports pretransfer, posttransfer and starttransfer as the moment it failed. Decision recorded in ADR-0073 (decided by Claude under Stewart's delegation).
- Measured curl 8.21.0 (mingw, Schannel) again, 2026-09-27: refused `http://127.0.0.1:1/` exit 7 `ns=0.000048|c=0.000000|pre=2.030259|post=2.030259|st=2.030259|t=2.030262`; `http://nonexistent.invalid/` exit 6 `ns=0.000000|c=0|pre=post=st≈t=0.0558`; a loopback server that closes without a reply exit 52 `st=0.000000`; one that answers after 0.3 s `st=0.301662`. Our build now prints the same shape for all four, except `ns` after a refused connect.
- `ns` after a refused connect needs `ConnectResult.Failed` to carry timings, a `Curl.Protocol.Abstractions.UnitLibrary` change (BL-247 in Doing touches it): filed as BL-381, not widened into this task. The acceptance criterion asked only for the literal's lookup time, which `TcpConnectorTests.ConnectAsync_ToALiteralAddress_ResolvesItAndRecordsTheNameLookupTime` pins, and pre/post/starttransfer after a refused connect, which `HttpProtocolHandlerTests.ExecuteAsync_ConnectFails_ReportsPreAndPostAndStartTransferAsTheMomentItFailed` pins.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0073; no task in Doing names it.
- Default taken: every failed connect (not only exit 7) gets the end-of-transfer timestamps, since exit 6 was measured to behave the same. Other handlers (FTP, file, ...) still report no timings; ADR-0073 says so.
- `dotnet format --verify-no-changes` reports pre-existing ENDOFLINE errors in `Curl.Cli.*` and `Curl.Networking.UnitLibrary` files this task did not touch; the files this task changed are clean.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HTTP transfers report -w timings; a literal address has a lookup time and a failed connect ends pretransfer/posttransfer/starttransfer as curl 8.21.0 does
