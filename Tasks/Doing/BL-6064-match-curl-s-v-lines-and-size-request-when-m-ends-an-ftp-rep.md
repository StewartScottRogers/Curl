---
id: BL-6064
title: Match curl's -v lines and size_request when -m ends an FTP reply wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6053]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpMaxTimeReplyWaitTests.cs, Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitTests/AbortedHeadRecordingTransferEventsTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6064 — Match curl's -v lines and size_request when -m ends an FTP reply wait

## Goal

When `-m` ends an FTP transfer while a reply is awaited, `-v` and `%{size_request}` match curl 8.22.0.

## Context

Found by BL-6053. Measured 2026-11-26 with `Record-CurlExchange.ps1 -Ftp`, arguments `-sS -v -m 3
--no-ftp-skip-pasv-ip -o NUL`, URL path `/f.txt`, against curl 8.22.0 (Windows):

- `-FtpReply 'SIZE=STALL'`: curl writes `* Operation timed out after N milliseconds with 0 bytes received` and
  `* closing connection #0`; Curl writes neither. curl's `%{size_request}` is 0, Curl's 69.
- `-FtpData 'hello\n' -FtpDataHoldMilliseconds 9000` (the data connection held open after its bytes): curl writes
  `* FTP response timeout`, `* control connection looks dead` and `* shutting down connection #0`; Curl writes none
  of the three. curl's `%{size_request}` is 0, Curl's 81.
- With `-m` ending the data connect, curl's `%{size_request}` is 0, Curl's 55.

BL-6053 marks only the data dial's cancellation (`FtpSession.DialDataAsync`, read by
`TransferPerformer.ReportClosingLines`). `AbortedHeadRecordingTransferEvents` counts every request header as
request size. Measure a `-m` that ends a download part-way before pinning its lines.

## Acceptance criteria

- [ ] A unit test pins both `-v` lines, in curl's order, for `-m` passing while the `SIZE` reply is awaited.
- [ ] A unit test pins the three `-v` lines, in curl's order, for `-m` passing while the end-of-transfer reply is awaited.
- [ ] A unit test pins `%{size_request}` 0 for an FTP transfer `-m` ends, and an HTTP one keeps its request size.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 6d51bcb6 3dadab5b 1310c14b 3d836d5e.
