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
completed:
---
# BL-1201 — Write the --trace-config ftp done line for a range whose post-quote is refused

## Goal

Under `-v --trace-config ftp -r 0-1 -Q -<cmd>`, with the post-quote refused, Curl writes curl 8.21.0's `[FTP]` end lines (`done, result=N`, and where they fall against the quote's lines) as measured.

## Context

- Left over from BL-1197 and BL-1199. Curl writes `[STOP] done, result=0` from `EndRangeAsync` before the post-quotes run, so a refused `-` quote after a range reports result 0 whatever curl writes.
- `FtpStateTrace.Ended` (`Curl.Protocol.Ftp.UnitLibrary`) writes the line once per session; `FtpSession.EndRangeAsync` and `QuitAndSucceedAsync` call it.
- Measure with `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -FtpReply 'NOOP=500 no' -CurlArgs '-sS','--trace-config','ftp','-v','-r','0-1','-Q','-NOOP',...` before pinning.

## Acceptance criteria

- [ ] A test in `FtpProtocolHandlerStateTraceTests` pins the measured `[FTP]` lines from `> ABOR` to the end for a range whose `-` post-quote is refused.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
