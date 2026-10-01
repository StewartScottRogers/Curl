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
completed: 2026-10-01
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

- [x] IMAP, POP3 and FTP pass the transfer's events to the upgrade's handshake and write the measured connection line after it; a test in each protocol's UnitTests pins it.
- [x] `CurlCommandRunnerImapTransferEventTests` and `CurlCommandRunnerPop3TransferEventTests` pin their `STARTTLS` and `STLS` cases with the measured lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-01 with `Record-CurlExchange.ps1` (`-Imap`, `-Pop3`, `-Ftp`, each `-v -k --ssl-reqd`), curl 8.21.0 Schannel build. All three in-place upgrades write the same as SMTP (BL-1058): after the accepting reply, the two `schannel:` lines, then the connect's `Established connection to ...` line again, unchanged (same local port), then the next command (`A003 CAPABILITY`, `CAPA`, `USER`).
- FTP data connection under `PROT P`: its handshake writes the two `schannel:` lines and no `Established` line of its own (the `Established 2nd connection` line is the data connect's, already written). curl writes them right after `Trying <host>:<data port>...`, before `TYPE I`; Curl handshakes after `RETR`'s `150`, so the lines are now written but later. Moving the handshake is BL-1090.
- Design: the same as SMTP's - each handler wraps its connect target's events in `ConnectionOpenedCapturingTransferEvents` and hands the kept event to the session (`ImapSession` and `Pop3Session` take it as a constructor argument, FTP as `FtpSessionConnections.ControlOpened`), which reports it again once the handshake succeeds. No ADR: it follows ADR-0299 and the measurement; no new choice was made.
- Existing tests that compared `ConnectTarget` with record equality or `AreSame` on its `Events` now normalise `Events` / check forwarding, as SMTP's did after BL-1058.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. IMAP STARTTLS, POP3 STLS and FTP AUTH TLS report their handshake to -v and write curl's second Established connection line after it
