---
id: BL-930
title: Write curl's -v and --trace lines for FTP commands and replies on the control connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-930 — Write curl's -v and --trace lines for FTP commands and replies on the control connection

## Goal

For `ftp://` and `ftps://`, `-v` shows every control-connection command as curl's `> ` line and every reply line as curl's `< ` line, and `--trace`/`--trace-ascii` dump them as curl's `=> Send header` and `<= Recv header` blocks, byte for byte as curl 8.21.0 writes them.

## Context

- Audit 2026-09-29 (Stewart's "logging from --none to --verbose" request, part B): the FTP handler reports only a few `ReportInfo` lines (`FtpSession.cs` lines ~578-613, ~838-856). Nothing in `Curl.Protocol.Ftp.UnitLibrary` calls `ITransferEvents.ReportRequestHeader` or `ReportResponseHeader`, so `curl -v ftp://…` prints none of the `> USER …`/`< 220 …` lines real curl prints, and `--trace` has no header blocks. HTTP, IMAP, POP3, SMTP, RTSP and WebSocket already report theirs (see `Pop3ControlChannel.cs`/`SmtpControlChannel.cs` for the pattern: report the exact bytes written and read, CRLF included).
- Where: `FtpControlChannel.cs` (the one place commands are written and replies read): report each command's bytes through `context.Events.ReportRequestHeader` and each reply line's bytes through `ReportResponseHeader`, including multi-line replies line by line as curl does, and including the commands sent after `AUTH TLS` on the encrypted channel.
- curl writes the `PASS` argument in clear in `-v`; do not mask what curl does not mask. Measure it.
- Measure first: `Record-CurlExchange.ps1 -Ftp` (with `-FtpReply` and `-FtpData`) runs real curl against the loopback FTP responder and records its standard error; run it with `-v`, `--trace-ascii -` and `--trace -` for: an anonymous `RETR`, a login with `-u user:pw`, a multi-line `230-` reply, a `550` on `RETR` (exit 78), and `-v --ssl-reqd` against a responder refusing `AUTH` (exit 64). Copy the measured bytes into Notes before pinning any text. The reference is curl 8.21.0 on the platform's build (Schannel on Windows).
- Not in this task: the data-connection info lines and the body dumps (BL-931), STARTTLS TLS lines (BL-806), the diagnostic log (BL-924).

## Acceptance criteria

- [x] Measured `-v`, `--trace-ascii -` and `--trace -` output for the five cases above is copied into Notes with the curl version and build.
- [x] `Curl.Protocol.Ftp.UnitTests` pin, through a recording `ITransferEvents`, the header-out bytes of every command and the header-in bytes of every reply line in the five cases, in curl's order.
- [x] No renderer changes: the existing `VerboseTransferEventWriter` and `TraceTransferEventWriter` in `Curl.Output` already turn these events into `> `/`< ` lines and `Send header`/`Recv header` blocks. If rendering the measured output needs a change there, stop and file it as a separate task rather than widening this one.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel`, via
`Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n'`, each case run with `-v`, `--trace-ascii -`
and `--trace -`. Progress-meter text and data lines trimmed; the `> `/`< ` lines are exact.

Anonymous RETR, `-v ftp://127.0.0.1:18931/f.txt` (exit 0):
```
< 220 Recorder ready
> USER anonymous
< 331 Password required
> PASS ftp@example.com
< 230 Logged in
> PWD
< 257 "/" is current directory
* Entry path is '/'
* Request has same path as previous transfer
> EPSV
* Connect data stream passively
< 229 Entering Extended Passive Mode (|||60573|)
* Connecting to 127.0.0.1 port 60573
> TYPE I
< 200 Type set
> SIZE f.txt
< 213 6
> RETR f.txt
< 150 Opening BINARY mode data connection
* Maxdownload = -1
* Getting file with size: 6
* Remembering we are in directory ""
< 226 Transfer complete
* Connection #0 to host 127.0.0.1:18931 left intact
```
The server's transcript shows curl then sent `QUIT` and read `221 Bye`; neither appears
in `-v` or `--trace`.

Login `-u user:pw`: identical except `> USER user` and `> PASS pw` - the password is
written in clear. `--trace-ascii`: `=> Send header, 11 bytes (0xb)` / `0000: USER user`.

Multi-line `230-` reply (`PASS=230-Welcome\r\n230-Second line\r\n230 Logged in`), `--trace-ascii -`:
```
=> Send header, 22 bytes (0x16)
0000: PASS ftp@example.com
<= Recv header, 13 bytes (0xd)
0000: 230-Welcome
<= Recv header, 17 bytes (0x11)
0000: 230-Second line
<= Recv header, 15 bytes (0xf)
0000: 230 Logged in
=> Send header, 5 bytes (0x5)
0000: PWD
```

`550` on RETR (exit 78, `curl: (78) RETR response: 550`), `--trace -` tail:
```
=> Send header, 12 bytes (0xc)
0000: 52 45 54 52 20 66 2e 74 78 74 0d 0a             RETR f.txt..
<= Recv header, 18 bytes (0x12)
0000: 35 35 30 20 4e 6f 20 73 75 63 68 20 66 69 6c 65 550 No such file
0010: 0d 0a                                           ..
* RETR response: 550
* Remembering we are in directory ""
* Connection #0 to host 127.0.0.1:18954 left intact
```

`-v --ssl-reqd`, AUTH refused (exit 64, `curl: (64) Requested SSL level failed`), `--trace -`:
```
<= Recv header, 20 bytes (0x14)
0000: 32 32 30 20 52 65 63 6f 72 64 65 72 20 72 65 61 220 Recorder rea
0010: 64 79 0d 0a                                     dy..
=> Send header, 10 bytes (0xa)
0000: 41 55 54 48 20 53 53 4c 0d 0a                   AUTH SSL..
<= Recv header, 25 bytes (0x19)
0000: 35 30 30 20 41 55 54 48 20 6e 6f 74 20 75 6e 64 500 AUTH not und
0010: 65 72 73 74 6f 6f 64 0d 0a                      erstood..
=> Send header, 10 bytes (0xa)
0000: 41 55 54 48 20 54 4c 53 0d 0a                   AUTH TLS..
<= Recv header, 25 bytes (0x19)
0000: 35 30 30 20 41 55 54 48 20 6e 6f 74 20 75 6e 64 500 AUTH not und
0010: 65 72 73 74 6f 6f 64 0d 0a                      erstood..
* closing connection #0
```

Extra measurement, `-v -r 0-2` (exit 0): `> ABOR` and its reply `< 226 Transfer complete`
are reported; the `QUIT` that follows is not.

So every header block is the exact bytes on the wire, CRLF included; each reply line is
its own block; `QUIT` and its reply are never reported. Implemented in
`FtpControlChannel` (reports on send and on each complete line read, `StopReporting()`
before `QUIT`), matching `Pop3ControlChannel`. No renderer change was needed. Pinned in
`FtpProtocolHandlerHeaderEventTests`; `RecordingTransferEvents` now records headers.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. curl -v and --trace now show every FTP control command and reply line (QUIT excepted), byte for byte as curl 8.21.0
