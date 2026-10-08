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
completed: 2026-10-07
---
# BL-1659 — Print an FTP greeting code below 100 with three digits in the exit 8 message, as curl does

## Goal

An `ftp://` greeting whose code is below 100, such as `099 Odd`, fails with exit 8 and the message `Got a 099 ftp-server response when 220 was expected`, the code zero-padded to three digits as curl 8.21.0 prints it.

## Context

- Found by BL-1508's adversarial tests. Curl prints `Got a 99 ftp-server response when 220 was expected`; curl 8.21.0 (Schannel build, measured 2026-10-07 with `Record-CurlExchange.ps1 -Ftp -FtpReply 'GREETING=099 Odd'`) prints `curl: (8) Got a 099 ftp-server response when 220 was expected`, exit 8, nothing sent.
- The message is built by `FtpTransferMessages.UnexpectedGreeting` in `Curl.Protocol.Ftp.UnitLibrary`; curl formats the code with `%03d`. Check the other messages that print a reply code (`Access denied`, `RETR response`, `server did not report OK`, `Bad PASV/EPSV response` and the like) against curl's format for codes below 100 at the same time, and measure any you change.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ftp.UnitTests` (add a `099 Odd` row to `FtpProtocolHandlerAdversarialTests.ExecuteAsync_GreetingCodeOutsideTwoHundreds_FailsWithExit8AndSendsNothing`) pins exit 8 and `Got a 099 ftp-server response when 220 was expected`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.21.0 (Schannel) with Record-CurlExchange.ps1 -Ftp, reply code 099 at each stage: greeting `Got a 099 ftp-server response when 220 was expected` (8), USER `Access denied: 099` (67), ACCT `ACCT rejected by server: 099` (11), PRET `PRET command not accepted: 099` (84), PASV after a refused EPSV `Bad PASV/EPSV response: 099` (13), RETR `RETR response: 099` (19). All six now format the code with `D3`.
- Left as plain decimal: `QUOT command failed with` only fires for codes of 400 or more, and a 099 after NOOP or at transfer end left curl at exit 0, so no sub-100 code reaches `QuoteCommandFailed` or `TransferNotOk` to measure.
- Tests: the greeting DataRow test now takes the expected message (099 row added); new adversarial tests pin USER, PASV and RETR at 099.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. FTP reply codes below 100 print with three digits in the greeting, Access denied, ACCT, PRET, PASV/EPSV and RETR messages, as curl does
