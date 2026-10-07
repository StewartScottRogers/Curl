---
id: BL-1131
title: Write every FTP control reply line to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1131 — Write every FTP control reply line to the -D file as curl does

## Goal

An `ftp://` or `ftps://` transfer with `-D` writes every control-connection reply line it reads, continuation lines included, byte for byte with its line ending and in arrival order, to the `-D` stream BL-1129 adds; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '220-multi\r\n220 hi\r\n530 no\r\n'`: `curl -s -D <file> ftp://127.0.0.1:<port>/` exits 67 and the file holds exactly `220-multi\r\n220 hi\r\n530 no\r\n`; `curl -s -i` writes nothing to stdout.
- curl 8.21.0 `lib/pingpong.c` `Curl_pp_readresp` (https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c): every complete line is passed to `Curl_client_write(data, CLIENTWRITE_INFO, line, length)`, which the tool's header callback writes to the `-D` stream. The commands curl sends are not written; neither are data-connection bytes.
- Before pinning a whole session, measure a successful download and a directory listing with `Record-CurlExchange.ps1 -Ftp` (with `-D` and with `-i`), and an `-I` transfer: `-I` already writes curl's synthesised `Last-Modified`/`Content-Length` lines to `HeaderOutput` (`FtpSession` around line 853), so record how the two kinds of line interleave in the `-D` file. Copy the measurements into Notes.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpControlChannel.cs` `ReadReplyAsync` / `ReadLineAsync` reads each line; write the raw line bytes to the BL-1129 property (read its Notes for the final name) when it is not `null`. A line refused for its length (exit 100) or, after BL-1117, for a NUL byte is not written, since curl rejects it before handing it on - confirm for the oversized line.
- `Curl.Protocol.Ftp.UnitLibrary` may also have BL-1117 and BL-1118 waiting; they share `touches` and run one after another.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ftp.UnitTests` pin the measured exchange above (the three lines in the new stream, nothing in `Output`), and a full measured download session whose new-stream bytes match real curl's `-D` file byte for byte.
- [x] A test pins that with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape) no reply line reaches `Output`, and that `-I`'s synthesised lines still go to `HeaderOutput` in the measured order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\r\n'`:
  - `-s -D f ftp://.../f.txt`: `220 Recorder ready`, `331 Password required`, `230 Logged in`, `257 "/" is current directory`, `229 Entering Extended Passive Mode (|||p|)`, `200 Type set`, `213 7`, `150 Opening BINARY mode data connection`, `226 Transfer complete`, each with CRLF. QUIT's `221 Bye` is not written.
  - `-s -D f ftp://.../dir/`: the same, with `250 OK` (CWD) after `257` and no `213`.
  - `-s -I -D f ftp://.../f.txt`: `...257`, `213 20260927123456`, `Last-Modified: ...`, `200 Type set`, `213 7`, `Content-Length: 7`, `350 Restarting at 0`, `Accept-ranges: bytes` - each synthesised line right after its reply, which the existing order of `FtpSession`'s writes already gives.
  - `-s -i ftp://.../f.txt`: stdout is the file alone.
- Implementation: `FtpControlChannel` takes `context.DumpHeaderOutput` and writes each line it reports to `-v` there, after the length and NUL checks (so an oversized line is not written, pinned by a test); `StopReporting` stops it before QUIT.
- Decision (ADR-0329): a refused `-D` write is exit 23 `client returned ERROR on write of <n> bytes`, the message `-I`'s lines use; refusal of ABOR's reply is ignored like an unreadable one. Not measured against real curl (needs a failing `-D` destination); based on `tool_header_cb` returning `CURL_WRITEFUNC_ERROR`.
- Tests: 8 in `FtpProtocolHandlerDumpHeaderTests`. FTP tests 532 green; all 33 fast test projects green; `Measure-CodeQuality.ps1 -Library` 100/100, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. ftp:// and ftps:// write every control reply line to the -D file as curl 8.21.0 does
