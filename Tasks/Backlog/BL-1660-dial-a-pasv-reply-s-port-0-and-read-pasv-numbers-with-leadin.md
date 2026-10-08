---
id: BL-1660
title: Dial a PASV reply's port 0 and read PASV numbers with leading zeros past three digits, as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1660 — Dial a PASV reply's port 0 and read PASV numbers with leading zeros past three digits, as curl does

## Goal

A `227` reply naming port 0 (`(127,0,0,1,0,0)`) or a number written with more than three digits (`(127,0,0,1,0001,1)`) is read the way curl 8.21.0 reads it: the data connection is dialled to that port, instead of failing with exit 14 `Could not interpret the 227-response`.

## Context

- Found by BL-1508's adversarial tests. Measured 2026-10-07 on curl 8.21.0 (Schannel build) with `Record-CurlExchange.ps1 -Ftp -FtpReply 'EPSV=500 no','PASV=227 Entering Passive Mode (...)'`:
  - `(127,0,0,1,0,0)`: curl dials port 0 and fails `curl: (7) Failed to connect to 127.0.0.1:<control port> via 127.0.0.1:0 after 126 ms: Could not connect to server`, exit 7. Curl answers exit 14 `Could not interpret the 227-response` without dialling.
  - `(127,0,0,1,0001,1)`: curl reads `0001` as 1 and dials port 257 (`via 127.0.0.1:257`). Curl refuses the reply with exit 14.
- The parser is `FtpPassiveReply.TryParsePasv` in `Curl.Protocol.Ftp.UnitLibrary` (its `MaxPasvDigits` and `port >= 1` checks). Port 0 over EPSV already dials and fails over to PASV (BL-1240, `FtpProtocolHandlerEpsvDialFallbackTests`); check what a failed PASV dial reports and pin it. Numbers above 255 must still fail with exit 14 (pinned in `FtpProtocolHandlerAdversarialTests.ExecuteAsync_PasvNumbersOutOfRangeOrMissing_FailsWithExit14`). Measure how many leading zeros curl accepts before pinning.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ftp.UnitTests` pins a `227` reply with port 0 dialling port 0 and the connector's failure ending the transfer as curl 8.21.0's exit 7 does.
- [ ] A test pins `(127,0,0,1,0001,1)` dialling port 257.
- [ ] `dotnet build` is clean and the fast tests pass, `FtpProtocolHandlerAdversarialTests` included.

## Notes

## Log

- 2026-10-07: Created.
