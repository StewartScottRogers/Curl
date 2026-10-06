---
id: BL-545
title: Register the SMTP handler for smtp and smtps in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-541, BL-542, BL-543, BL-544]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-545 — Register the SMTP handler for smtp and smtps in Curl.Console

## Goal

`curl smtp://...` and `curl smtps://...` run end to end through `Curl.Console` with the SMTP handler, the TCP/TLS connector and the SASL authenticator, producing the measured request bytes, output and exit codes, where today they fail as an unsupported protocol.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-540 to BL-544; context: BL-539.
- Registration is in `Curl.Console/CurlComposition.cs` (the handler list next to `DictProtocolHandler`, `MqttProtocolHandler` and so on); scheme dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `smtp`/`smtps` with their default ports (25, 465). `-V`/`--version` lists protocols (ADR-0021: lists only what Curl implements): add `smtp` and `smtps` there if this is where the list is built, with a test.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run `--mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:<P>/` and an `smtps://` variant through fake connectors with the bytes BL-542 measured, pinning request bytes, stdout, stderr and exit code.
- [x] `-u u:p` reaches `AUTH` through the composed SASL authenticator, with a test.
- [x] `curl -V` lists `smtp` and `smtps` as ADR-0021 requires, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: register `new SmtpProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator())`
  in `CurlComposition.CreateProtocolHandlers`, beside MQTT; it rides the same end-point recording
  and pooling connector as every TCP handler. `ProtocolDispatcher` needed no change: `CurlUrlScheme`
  already maps `smtp` to 25 and `smtps` to 465, and the dispatcher routes by `SupportedSchemes`.
- Measured 2026-09-28 with `Record-CurlExchange.ps1 -Smtp` (and `-Tls` for `smtps`), curl 8.21.0
  (WinGet mingw/Schannel build; the script's default found `C:\windows\system32\curl.exe` first on
  PATH, and `[Environment]::CurrentDirectory` must be set for a relative `-T mail.txt`):
  `-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/` with
  `mail.txt` = `Subject: t CRLF CRLF hello CRLF` sends `EHLO mail.txt` (curl appends the upload's
  file name to a URL ending in `/`), `MAIL FROM:<a@b> SIZE=21`, `RCPT TO:<c@d>`, `DATA`, the message,
  `.`, `QUIT`; stdout and stderr empty, exit 0. `smtps` is byte-identical on the wire (the recorder
  omits `STARTTLS` from `EHLO` there). With `-u u:p` curl picks `AUTH CRAM-MD5` from
  `PLAIN LOGIN CRAM-MD5` and answers the challenge
  `PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=` with `dSAwNWVlYTdmN2JkODM3ODYwNDQ2ODBiNzAwYjQ5NjVhNA==`.
  Pinned in `Curl.Console.UnitTests/CurlCompositionSmtpTests.cs`, which runs the production
  composition over a `ScriptedConnector` with the upload in a temporary file (absolute path; curl
  appends only the file name). They stay in the fast run, as `CreateRunner_FileUrlOfTemporaryFile`
  does, because they touch only a temporary file, never the network.
- `touches` widened to `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`: the `-V` protocols line
  lives in `CurlVersionText.ProtocolsLine` (ADR-0021 Decision 6 says the registering task updates
  it). No task in Doing (BL-695, BL-737) names either project.
- `Documentation/Planning/Roadmap.md` and ADR-0021 still quote older protocol lists as history
  or roadmap text; they are outside `touches` and left for `align-and-document`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl smtp:// and smtps:// run end to end through Curl.Console with the SMTP handler and SASL AUTH, and -V lists smtp smtps
