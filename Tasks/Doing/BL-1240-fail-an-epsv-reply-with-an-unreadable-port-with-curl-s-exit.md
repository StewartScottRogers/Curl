---
id: BL-1240
title: Fail an EPSV reply with an unreadable port with curl's exit 13 'Illegal port number in EPSV reply'
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1239]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1240 — Fail an EPSV reply with an unreadable port with curl's exit 13 'Illegal port number in EPSV reply'

## Goal

A `229` reply to `EPSV` whose `(|||` is followed by a digit but whose port is above 65535 or is not closed by the delimiter fails with exit 13 (`CurlExitCode.FtpWeirdPasvReply`) and curl 8.21.0's `Illegal port number in EPSV reply`, while every other unreadable `229` keeps `Weirdly formatted EPSV reply`.

## Context

- Today `Curl.Protocol.Ftp.UnitLibrary/FtpPassiveReply.cs` `TryParseEpsvPort` returns `false` for every reply it cannot read, and `FtpSession.OpenPassiveDataConnectionAsync` (`FtpSession.cs`, the `QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.WeirdEpsvReply)` call) reports them all as `Weirdly formatted EPSV reply`.
- curl 8.21.0, `lib/ftp.c` `ftp_state_pasv_resp` lines 2066-2090 at `curl-8_21_0`: after the first `(`, when the next three characters are one delimiter repeated and the fourth is a digit, the port is read with `curlx_str_number(&p, &num, 0xffff)`; a value above 65535, or a character after the digits other than the delimiter, is `failf "Illegal port number in EPSV reply"`, exit 13. Without `(`, or without the three delimiters and a digit, it is `Weirdly formatted EPSV reply`, exit 13.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 3000 -CurlArgs '-v','ftp://127.0.0.1:<port>/f.txt'`:
  - `-FtpReply 'EPSV=229 Entering Extended Passive Mode (|||99999|)'` and `'EPSV=229 Entering Extended Passive Mode (|||123x)'`: stderr `> EPSV` / `* Connect data stream passively` / `< 229 ...` / `* Illegal port number in EPSV reply` / `* Remembering we are in directory ""` / `* Connection #0 to host 127.0.0.1:<port> left intact` / `curl: (13) Illegal port number in EPSV reply`, exit 13; the transcript shows `QUIT` sent after the `229`.
- Measure `(|||0|)` too before deciding it: curl accepts the number 0 here, and what follows is not pinned yet.

## Acceptance criteria

- [ ] Before the code change, `Notes` holds the measured request lines, stderr and exit code for `(|||0|)`, recorded the same way.
- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` pin both measured cases above byte for byte: the commands sent (`QUIT` included), the `-v` lines in order and exit 13 with `Illegal port number in EPSV reply`.
- [ ] Tests pin that a reply with no `(`, or with `(||x|)`, still fails with `Weirdly formatted EPSV reply`, and that `(|||0|)` behaves as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
