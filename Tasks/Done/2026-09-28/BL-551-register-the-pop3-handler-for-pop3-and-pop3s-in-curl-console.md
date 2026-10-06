---
id: BL-551
title: Register the POP3 handler for pop3 and pop3s in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-548, BL-549, BL-550]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-551 — Register the POP3 handler for pop3 and pop3s in Curl.Console

## Goal

`curl pop3://...` and `curl pop3s://...` run end to end through `Curl.Console` with the POP3 handler, the connector and the SASL authenticator, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-547 to BL-550; context: BL-539.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `pop3`/`pop3s` with default ports 110 and 995; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run `-u u:p pop3://127.0.0.1:<P>/1` and a `pop3s://` variant through fake connectors with the bytes BL-549 measured, pinning request bytes, stdout, stderr and exit code.
- [x] `curl -V` lists `pop3` and `pop3s`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- Plan (run in-session, as BL-545 did for SMTP): register `Pop3ProtocolHandler` in
  `CurlComposition.CreateProtocolHandlers` with the shared connector, the TLS provider (for
  `STLS`) and `CreateSaslAuthenticator()`. `ProtocolDispatcher` needed no change: the default
  ports 110 and 995 already come from `CurlUrlScheme` in `Curl.Protocol.Abstractions.UnitLibrary`,
  and `CurlCompositionTests` now pins them.
- The `-V` protocols line is built in `Curl.Cli.UnitLibrary/CurlVersionText.cs`, so
  `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests` were added to `touches`; no task in
  `Doing` names either (the others touch Curl.Tls and Curl.Cryptography).
- `CurlCompositionPop3Tests` replays BL-548/BL-549's measured bytes (curl 8.21.0 Schannel):
  `RETR 1` for `pop3` and `pop3s` without login and with `-u u:p` (`AUTH PLAIN`, `AHUAcA==`),
  and `-ERR` on `RETR 9` as exit 8 `Weird server reply`. No new behaviour, so no ADR.
- Results: Curl.Console.UnitTests 1264 passed (5 new); `Measure-CodeQuality.ps1 -Library
  Curl.Console` 100% line, 100% branch, 506 members, 0 failing, worst CRAP 10;
  `dotnet build Curl.slnx -warnaserror` clean; every fast test project passed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl pop3:// and pop3s:// run end to end through Curl.Console with login and RETR as curl 8.21.0 does; -V lists pop3 pop3s
