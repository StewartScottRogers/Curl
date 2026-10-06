---
id: BL-1201
title: Write the --trace-config ftp done line for a range whose post-quote is refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1201 — Write the --trace-config ftp done line for a range whose post-quote is refused

## Goal

Under `-v --trace-config ftp -r 0-1 -Q -<cmd>`, with the post-quote refused, Curl writes curl 8.21.0's `[FTP]` end lines (`done, result=N`, and where they fall against the quote's lines) as measured.

## Context

- Left over from BL-1197 and BL-1199. Curl writes `[STOP] done, result=0` from `EndRangeAsync` before the post-quotes run, so a refused `-` quote after a range reports result 0 whatever curl writes.
- `FtpStateTrace.Ended` (`Curl.Protocol.Ftp.UnitLibrary`) writes the line once per session; `FtpSession.EndRangeAsync` and `QuitAndSucceedAsync` call it.
- Measure with `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -FtpReply 'NOOP=500 no' -CurlArgs '-sS','--trace-config','ftp','-v','-r','0-1','-Q','-NOOP',...` before pinning.

## Acceptance criteria

- [x] A test in `FtpProtocolHandlerStateTraceTests` pins the measured `[FTP]` lines from `> ABOR` to the end for a range whose `-` post-quote is refused.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Ftp -FtpData "hello`n" -CurlArgs '-sS','--trace-config','ftp','-v','-r','0-1','-Q','-NOOP',...`: after `partial download completed, closing connection` curl sends the post-quote, writes its `getftpresponse` lines, then `[STOP] done, result=21`, then `shutting down connection #0`; exit 21. ABOR reads the stale 226, so NOOP reads ABOR's reply.
- `FtpSession.EndRangeAsync` now runs the post-quotes (`RunPostQuotesAsync`, which writes the done line with their result) before the shutting-down line, then `QuitAfterPostQuotesAsync`; `QuitAndSucceedAsync` composes the two.
- curl also writes `* QUOT string not accepted: NOOP` (its failf under -v) just before the done line. The handler returns that text as the failure message, not as a -v line, as for every other FTP failure, so the test pins the handler's lines without it.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A range whose - post-quote is refused writes [STOP] done, result=21 after the quote's lines, before shutting down, as curl 8.21.0 does
