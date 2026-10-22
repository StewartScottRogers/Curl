---
id: BL-2236
title: Remember a refused EPSV or EPRT on a reused FTP connection so cases 211 and 212 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2236 — Remember a refused EPSV or EPRT on a reused FTP connection so cases 211 and 212 pass

## Goal

When a server answers EPSV (or EPRT) with 500, Curl sends PASV (or PORT) for every later transfer on the same reused FTP connection, as curl does, so upstream cases 211 and 212 pass.

## Context

Found by BL-2221, which let 211 and 212 run (their `<client><stdout>` no longer skips them). Run through the harness (`CURL_UPSTREAM_CASE=211`, `TestCategory=Manual`), they fail:
- test211: `<verify><protocol> differs at byte 100 (line 11): expected "PASV\r\n", got "EPSV\r\n"` - the second URL on the reused connection tries EPSV again.
- test212 (`-P -`): `differs at byte 105 (line 11): expected "PORT \r\n", got "EPRT |1|\r\n"`.
curl 8.22.0 (`lib/ftp.c`) clears `conn->bits.ftp_use_epsv` / `ftp_use_eprt` once the server refuses the command, so the connection never retries it. Start in `Curl.Protocol.Ftp.UnitLibrary` (the EPSV fallback logs `EpsvRefused` in `FtpDiagnosticLog`).

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Ftp.UnitTests` pins that a second transfer on a reused connection whose EPSV was refused sends PASV first, and one whose EPRT was refused sends PORT first.
- [x] Cases 211 and 212 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` shows no member outside a gate.

## Notes

- Delivered directly rather than through the full `/feature` stages: the change is two flags,
  and curl's behaviour is fixed by the task's own Context (no design choice left, so no ADR).
- `FtpKeptConnection` gains `EpsvRefused` and `EprtRefused`; `FtpSession` starts from the reused
  connection's values, sets `EpsvRefused` on a non-229 EPSV reply and on a failed EPSV data dial
  (both are curl's `ftp_epsv_disable`), and `EprtRefused` on an IPv4 EPRT refusal (IPv6 has no
  PORT, so a refusal there ends the transfer anyway). `TryKeepControlConnection` hands both on.
- The skip test moved into `SkipsEpsv` so `OpenPassiveDataConnectionAsync` stays at complexity 10.
- Tests: `FtpProtocolHandlerKeptConnectionTests.ExecuteAsync_SecondUrlAfterEpsvRefused_SendsPasvWithoutEpsv`
  and `ExecuteAsync_SecondUrlAfterEprtRefused_SendsPortWithoutEprt`. Measured: FTP library 100% line,
  100% branch, worst CRAP 10, 0 failing members; fast suite green.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A refused EPSV or EPRT is remembered on the reused FTP connection, so later URLs send PASV or PORT first; cases 211 and 212 pass
