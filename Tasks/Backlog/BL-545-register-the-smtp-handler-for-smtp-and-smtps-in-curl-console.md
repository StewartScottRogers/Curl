---
id: BL-545
title: Register the SMTP handler for smtp and smtps in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-541, BL-542, BL-543, BL-544]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-545 — Register the SMTP handler for smtp and smtps in Curl.Console

## Goal

`curl smtp://...` and `curl smtps://...` run end to end through `Curl.Console` with the SMTP handler, the TCP/TLS connector and the SASL authenticator, producing the measured request bytes, output and exit codes, where today they fail as an unsupported protocol.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-540 to BL-544; context: BL-539.
- Registration is in `Curl.Console/CurlComposition.cs` (the handler list next to `DictProtocolHandler`, `MqttProtocolHandler` and so on); scheme dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `smtp`/`smtps` with their default ports (25, 465). `-V`/`--version` lists protocols (ADR-0021: lists only what Curl implements): add `smtp` and `smtps` there if this is where the list is built, with a test.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run `--mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:<P>/` and an `smtps://` variant through fake connectors with the bytes BL-542 measured, pinning request bytes, stdout, stderr and exit code.
- [ ] `-u u:p` reaches `AUTH` through the composed SASL authenticator, with a test.
- [ ] `curl -V` lists `smtp` and `smtps` as ADR-0021 requires, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
