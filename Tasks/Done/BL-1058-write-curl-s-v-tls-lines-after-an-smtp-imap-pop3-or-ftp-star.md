---
id: BL-1058
title: Write curl's -v TLS lines after an SMTP, IMAP, POP3 or FTP STARTTLS upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-546]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-1058 — Write curl's -v TLS lines after an SMTP, IMAP, POP3 or FTP STARTTLS upgrade

## Goal

After an SMTP `STARTTLS` is accepted, `-v` writes the lines curl 8.21.0 writes for the in-place TLS upgrade before the second `EHLO`, byte for byte, as it does after an HTTPS connect.

## Context

- Found by BL-546. `ITlsProvider.AuthenticateAsClientAsync(plaintext, host, token)` takes no `ITransferEvents`, so a STARTTLS upgrade reports nothing; `SslStreamTlsProvider` already reports through `IHandshakeReportingTlsProvider` (Curl.Networking.UnitLibrary) when `TcpConnector` drives it.
- Measured on curl 8.21.0 (mingw, Schannel), `-v -k --ssl-reqd --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18027/client` against `Record-CurlExchange.ps1 -Smtp` (BL-546 Notes). Between `< 220 Ready to start TLS` and `> EHLO client` curl writes:
  ```
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * Established connection to 127.0.0.1 (127.0.0.1 port 18027) from 127.0.0.1 port 53681 
  ```
  The second `Established connection` line repeats the connect's line (trailing space included).
- The two `schannel:` lines are not written after an HTTPS connect either (ADR-0046, BL-490 Notes); decide whether this task writes them for both or files that separately.
- Measure the OpenSSL build's lines on Linux before pinning them there.
- IMAP (BL-559), POP3 and FTP `AUTH TLS` upgrade through the same `ITlsProvider`; once the seam carries events, file a task for each that wants the lines.

## Acceptance criteria

- [x] `ITlsProvider` (or a sibling seam) lets a protocol handler pass its `ITransferEvents` to an in-place upgrade, without breaking the IMAP, POP3 and FTP handlers or their fakes.
- [x] `Curl.Console.UnitTests` `CurlCommandRunnerSmtpTransferEventTests` pins the STARTTLS case with the second `Established connection` line, in place of today's pin without it. The two `schannel:` lines moved to BL-1083, as the Context's fourth point allows (see Notes).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Plan and decision recorded in ADR-0299. `ITlsProvider` gained a default
  `AuthenticateAsClientAsync(plaintext, targetHost, events, token)` that falls back to the
  three-argument handshake; `SslStreamTlsProvider` and `HandBuiltTlsProvider` already had a
  public method with that exact signature (trust and handshake reported, no ALPN), so they
  implement it with no code change. IMAP, POP3 and FTP and every fake compile and behave as
  before.
- `ConnectionOpenedCapturingTransferEvents` (Abstractions) keeps the connect's
  `ConnectionOpenedEvent`; `SmtpProtocolHandler` wraps the connect target's events in it and
  `SmtpSession.UpgradeAsync` reports it again after a successful handshake. It lives in
  Abstractions so BL-1084 can reuse it for IMAP, POP3 and FTP.
- The `schannel:` lines: decided to file them separately (BL-1083) rather than write them
  for STARTTLS alone. The Schannel build writes them before every handshake, HTTPS's
  included (ADR-0046), and they are the TLS client's wording; writing them for every
  handshake changes HTTPS and LDAPS `-v` output across Curl.Output, outside this task's
  `touches`. The test comment in `CurlCommandRunnerSmtpTransferEventTests` names BL-1083.
- Side effect: SMTP's `ConnectTarget.Events` is now the capturing wrapper, so
  `SmtpProtocolHandlerSessionTests` and `CurlCompositionTests.CreateRunner_TcpSchemeUrl_...`
  compare targets with `Events` reset, and the events test checks forwarding instead of
  identity.
- OpenSSL build: the upgrade now also writes that build's trust and handshake lines, as an
  HTTPS connect does; not measured on Linux in this run (no Linux curl here). BL-1083 and
  BL-1084 measure before pinning anything for it.
- Coverage: Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Smtp.UnitLibrary and
  Curl.Networking.UnitLibrary each 100% line and branch, 0 failing members.
- Filed: BL-1083 (schannel lines for every handshake), BL-1084 (IMAP, POP3, FTP upgrades).

## Log

- 2026-09-28: Created.
- 2026-09-30: Renumbered from BL-806, which the archived Done task BL-806 keeps (BL-1054).
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SMTP STARTTLS reports its handshake to the transfer's events and -v writes curl's second Established connection line
