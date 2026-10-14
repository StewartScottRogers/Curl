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
completed:
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

- [ ] A unit test in `Curl.Protocol.Ftp.UnitTests` pins that a second transfer on a reused connection whose EPSV was refused sends PASV first, and one whose EPRT was refused sends PORT first.
- [ ] Cases 211 and 212 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` shows no member outside a gate.

## Notes

## Log

- 2026-10-14: Created.
