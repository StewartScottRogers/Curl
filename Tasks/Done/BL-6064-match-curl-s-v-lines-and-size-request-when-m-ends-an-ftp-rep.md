---
id: BL-6064
title: Match curl's -v lines and size_request when -m ends an FTP reply wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6053]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpMaxTimeReplyWaitTests.cs, Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitTests/AbortedHeadRecordingTransferEventsTests.cs, Curl.Engine.UnitTests/FtpMaxTimeReplyWaitClosingLinesTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.LowSpeedReport.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] A unit test pins both `-v` lines, in curl's order, for `-m` passing while the `SIZE` reply is awaited.
- [x] A unit test pins the three `-v` lines, in curl's order, for `-m` passing while the end-of-transfer reply is awaited.
- [x] A unit test pins `%{size_request}` 0 for an FTP transfer `-m` ends, and an HTTP one keeps its request size.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured again 2026-11-26 (curl 8.22.0, Windows, `-m 3`). `SIZE=STALL` and `PASS=STALL`: the timeout line
  then `closing connection #0`. A download ended part-way (`SIZE=213 100`, data held open): the same two lines,
  `%{response_code}` 150. The end-of-transfer reply stalled (`RETRDONE=226-waiting`, or the data held open after
  all its bytes): `FTP response timeout`, `control connection looks dead`, `shutting down connection #0`, and
  `%{response_code}` 000. `%{size_request}` was 0 in every case. Curl's output matched in the `SIZE`, held-data
  and part-way cases when run through the recorder.
- So `FtpSession.RunAsync` now names the closing line on every cancellation that leaves the session, which
  replaces the data-dial-only mark of BL-6053, and the end-of-transfer timeout also clears the response code.
- `AbortedHeadRecordingTransferEvents` counts as request size only a head whose first line holds ` HTTP/` or
  ` RTSP/`, tested per call because a head is reported whole: an FTP command after an HTTP `CONNECT` is not counted.
- `touches` gained `FtpMaxTimeReplyWaitClosingLinesTests.cs` (the `SIZE` case needs the engine, which writes the
  first line) and `ManagedEasyTransferTests.LowSpeedReport.cs` (it reported 62 zero bytes as a request head). No
  task in Doing named either.
- Measure-CodeQuality on both libraries: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 6d51bcb6 3dadab5b 1310c14b 3d836d5e.
- 2026-11-26: Doing -> Done. -m ending an FTP reply wait now writes curl's -v lines and reports size_request 0
