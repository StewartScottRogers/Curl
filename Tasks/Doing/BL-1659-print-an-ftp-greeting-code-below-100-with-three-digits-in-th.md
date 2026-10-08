---
id: BL-1659
title: Print an FTP greeting code below 100 with three digits in the exit 8 message, as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1659 — Print an FTP greeting code below 100 with three digits in the exit 8 message, as curl does

## Goal

An `ftp://` greeting whose code is below 100, such as `099 Odd`, fails with exit 8 and the message `Got a 099 ftp-server response when 220 was expected`, the code zero-padded to three digits as curl 8.21.0 prints it.

## Context

- Found by BL-1508's adversarial tests. Curl prints `Got a 99 ftp-server response when 220 was expected`; curl 8.21.0 (Schannel build, measured 2026-10-07 with `Record-CurlExchange.ps1 -Ftp -FtpReply 'GREETING=099 Odd'`) prints `curl: (8) Got a 099 ftp-server response when 220 was expected`, exit 8, nothing sent.
- The message is built by `FtpTransferMessages.UnexpectedGreeting` in `Curl.Protocol.Ftp.UnitLibrary`; curl formats the code with `%03d`. Check the other messages that print a reply code (`Access denied`, `RETR response`, `server did not report OK`, `Bad PASV/EPSV response` and the like) against curl's format for codes below 100 at the same time, and measure any you change.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ftp.UnitTests` (add a `099 Odd` row to `FtpProtocolHandlerAdversarialTests.ExecuteAsync_GreetingCodeOutsideTwoHundreds_FailsWithExit8AndSendsNothing`) pins exit 8 and `Got a 099 ftp-server response when 220 was expected`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
