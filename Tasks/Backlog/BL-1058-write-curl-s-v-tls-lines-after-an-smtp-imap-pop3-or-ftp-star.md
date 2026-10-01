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
completed:
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

- [ ] `ITlsProvider` (or a sibling seam) lets a protocol handler pass its `ITransferEvents` to an in-place upgrade, without breaking the IMAP, POP3 and FTP handlers or their fakes.
- [ ] `Curl.Console.UnitTests` `CurlCommandRunnerSmtpTransferEventTests` pins the STARTTLS case with the three lines above, in place of today's pin without them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Renumbered from BL-806, which the archived Done task BL-806 keeps (BL-1054).
