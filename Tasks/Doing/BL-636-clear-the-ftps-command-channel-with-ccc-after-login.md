---
id: BL-636
title: Clear the FTPS command channel with CCC after login
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-634]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-636 — Clear the FTPS command channel with CCC after login

## Goal

With `--ftp-ssl-ccc` on an FTPS session, the handler sends `CCC` after authenticating, shuts TLS down on the control connection (actively or passively per `--ftp-ssl-ccc-mode`) and continues in plain text, as curl 8.21.0 does, with a refused `CCC` handled as curl handles it.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Options: BL-634.
- The control connection is upgraded through `ITlsProvider` (ADR-0102); going back to plain text needs the TLS connection to shut down (`SslStream.ShutdownAsync`) and hand back the inner stream, which the `IConnection` contract may not offer. If a contract change is needed, record it as an ADR marked "Decided by Claude under Stewart's delegation"; the Abstractions, Networking and Decisions paths are in `touches` for that reason.
- `Record-CurlExchange.ps1 -Ftp` with `-Tls` serves AUTH TLS; it may need a `CCC` reply and a TLS shutdown to measure: extend it.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp` (extended if needed): `--ftp-ssl-reqd --ftp-ssl-ccc -k` with `CCC` answered `200` and `500`, and `--ftp-ssl-ccc-mode active`; commands, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the command sequence and that commands after `CCC` go out in plain text, through fake connections.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
