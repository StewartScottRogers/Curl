---
id: BL-540
title: Open an SMTP session: greeting, EHLO or HELO, STARTTLS, smtps and QUIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-529]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-540 — Open an SMTP session: greeting, EHLO or HELO, STARTTLS, smtps and QUIT

## Goal

An `SmtpProtocolHandler` in `Curl.Protocol.Smtp.UnitLibrary` connects through `IConnector`, reads the greeting and multi-line replies, sends `EHLO <domain>` (falling back to `HELO` as curl does), upgrades with `STARTTLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `smtps://`, ends with `QUIT`, and maps every failure to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; SMTP split: this session, BL-541 auth, BL-542 upload, BL-543 non-upload commands, BL-544 options, BL-545 registration, BL-546 `-v`).
- Design: BL-533's ADR (STARTTLS, security levels, URL model: the path names the `EHLO` domain). Contract: BL-534. Timeouts: follow BL-498's ADR; endpoints: BL-515's ADR.
- `Curl.Protocol.Smtp.UnitLibrary/CLAUDE.md`: reference only Abstractions, take `IConnection`, never a `Socket`/`SslStream`/`HttpClient`. The FTP handler's `AUTH TLS` upgrade through `ITlsProvider` is the model for `STARTTLS`.
- Measure with `Record-CurlExchange.ps1 -Smtp` (BL-529): the default session with `-T`, a greeting of `554`, `EHLO` answered `502` (HELO fallback), `STARTTLS` refused under `--ssl` and under `--ssl-reqd` (exit 64), `smtps://` with `-Tls -k`, a reply line that is not a reply (exit 8), and the server closing early.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout, stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` drive the handler through a fake `IConnector`/`IConnection` replaying the measured server bytes and pin the client bytes and the exit code and message for each case, including multi-line replies split across reads.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
