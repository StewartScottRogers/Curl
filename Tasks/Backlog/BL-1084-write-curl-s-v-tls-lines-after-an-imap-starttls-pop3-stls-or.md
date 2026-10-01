---
id: BL-1084
title: Write curl's -v TLS lines after an IMAP STARTTLS, POP3 STLS or FTP AUTH TLS upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1084 — Write curl's -v TLS lines after an IMAP STARTTLS, POP3 STLS or FTP AUTH TLS upgrade

## Goal

After an accepted IMAP `STARTTLS`, POP3 `STLS` or FTP `AUTH TLS`, `-v` writes the lines curl 8.21.0 writes for the in-place TLS upgrade, as SMTP does since BL-1058.

## Context

- BL-1058 gave `ITlsProvider` an overload that takes the transfer's `ITransferEvents` (the production providers report their handshake through it) and `ConnectionOpenedCapturingTransferEvents` (Curl.Protocol.Abstractions.UnitLibrary), which keeps the connect's `ConnectionOpenedEvent`. `SmtpProtocolHandler` wraps its connect target's events in one, and `SmtpSession.UpgradeAsync` passes `context.Events` to the handshake and reports the kept event again once it succeeds - curl's second `Established connection` line (ADR-0299).
- `ImapSession`, `Pop3Session` and `FtpSessionConnections` still call the three-argument overload. `CurlCommandRunnerImapTransferEventTests` and `CurlCommandRunnerPop3TransferEventTests` (Curl.Console.UnitTests) carry a comment noting the lines are missing.
- Measure each with `Record-CurlExchange.ps1` (`-Imap`, `-Pop3`, the FTP recorder) before pinning: whether curl repeats `Established connection` after each upgrade as it does for SMTP, and for FTP whether the data connection's handshake under `PROT P` writes anything.
- The `schannel:` lines before each handshake are BL-1083's.

## Acceptance criteria

- [ ] IMAP, POP3 and FTP pass the transfer's events to the upgrade's handshake and write the measured connection line after it; a test in each protocol's UnitTests pins it.
- [ ] `CurlCommandRunnerImapTransferEventTests` and `CurlCommandRunnerPop3TransferEventTests` pin their `STARTTLS` and `STLS` cases with the measured lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
