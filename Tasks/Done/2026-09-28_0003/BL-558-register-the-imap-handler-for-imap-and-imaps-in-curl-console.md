---
id: BL-558
title: Register the IMAP handler for imap and imaps in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-554, BL-555, BL-556, BL-557]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-558 — Register the IMAP handler for imap and imaps in Curl.Console

## Goal

`curl imap://...` and `curl imaps://...` run end to end through `Curl.Console` with the IMAP handler, the connector and the SASL authenticator, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-553 to BL-557; context: BL-539.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `imap`/`imaps` with default ports 143 and 993; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run `-u u:p "imap://127.0.0.1:<P>/INBOX;UID=1"`, an `imaps://` variant and an `APPEND` upload through fake connectors with the bytes BL-555 and BL-557 measured, pinning request bytes, stdout, stderr and exit code.
- [x] `curl -V` lists `imap` and `imaps`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- `ImapProtocolHandler` is registered in `CurlComposition.CreateProtocolHandlers` with the shared recording connector, the TLS provider and `CreateSaslAuthenticator()`, as POP3 and SMTP are. Default ports 143/993 already come from `CurlUrlScheme`; `ProtocolDispatcher` needed no change.
- `touches` gained `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`: the `-V` protocols line lives in `CurlVersionText` there (ADR-0021). No task in Doing names them (BL-698 Tls, BL-739 Cryptography).
- `CurlCompositionImapTests` replays the exchanges recorded in BL-554, BL-555 and BL-557 Notes: `-u u:p` fetch of `INBOX;UID=1` over imap and imaps, the UIDVALIDITY exit 78, `-T` APPEND (`21 0`) over both schemes, and the refused APPEND (exit 25, `21 25`). `CurlVersionTextTests` pins `imap imaps` in the protocols line.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console` 100% line, 100% branch, 0 failing of 506 members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl imap:// and imaps:// fetch, log in and APPEND end to end through Curl.Console; -V lists imap imaps
