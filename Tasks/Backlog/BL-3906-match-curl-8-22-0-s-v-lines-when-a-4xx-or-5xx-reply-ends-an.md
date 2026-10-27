---
id: BL-3906
title: Match curl 8.22.0's -v lines when a 4xx or 5xx reply ends an active-mode accept wait
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Protocol.Ftp.UnitTests/FtpActiveAcceptVerboseTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3906 — Match curl 8.22.0's -v lines when a 4xx or 5xx reply ends an active-mode accept wait

## Goal

`curl -v -P localhost ftp://...` prints the same `*` lines as curl 8.22.0 when a 4xx or 5xx reply behind `RETR`'s `150` ends the wait for the server's data connection.

## Context

Found by BL-3628 (CGR-099), measured with `Record-CurlExchange.ps1 -Ftp -FtpReply 'RETR=150 Opening BINARY mode data connection\r\n425 Cannot open data connection'` and `-v -P localhost`. curl 8.22.0 prints, after `* Data conn was not available immediately`: `* Ctrl conn has data while waiting for data conn`, the `< 425` line, `* FTP code: 425`, `* Remembering we are in directory ""`, then (only when the size was known) `* Received only partial file: 0 bytes`. Curl prints the 8.21.0 lines instead: `* Ready to accept data connection from server`, `* There is negative response in cache while serv connect`, and with the size unknown also `* FTP: The server failed to connect to data port` (curl prints no line for that, it is only `curl_easy_strerror`'s text). Code: `FtpSession.AcceptDataConnectionAsync` and `FailOnNegativeReplyAwaitingConnectAsync`. Measure first whether 8.22.0 still prints `Ready to accept data connection from server` on a successful accept before changing that line.

## Acceptance criteria

- [ ] With the size known and unknown, and the refusal buffered with the `150` or arriving a second later (`{PAUSE}`), Curl's `-v` stderr equals curl 8.22.0's (ports and timings normalised); the commands and both outputs are recorded in Notes.
- [ ] A unit test in `Curl.Protocol.Ftp.UnitTests` pins those lines.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
