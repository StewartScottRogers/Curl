---
id: BL-1133
title: Write every POP3 response line to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1133 — Write every POP3 response line to the -D file as curl does

## Goal

A `pop3://` or `pop3s://` transfer with `-D` writes every response line it reads (the greeting, `+OK`/`-ERR` lines and `CAPA` lines), byte for byte with its line ending and in arrival order, to the `-D` stream BL-1129 adds, and not the `RETR` or `LIST` body that goes to the output; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '+OK hi\r\n-ERR no\r\n'`: `curl -s -D <file> pop3://127.0.0.1:<port>/` exits 56 and the file holds exactly `+OK hi\r\n-ERR no\r\n`; `curl -s -i` writes nothing to stdout.
- curl 8.21.0 `lib/pingpong.c` `Curl_pp_readresp` (https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c) passes every response line to `Curl_client_write(data, CLIENTWRITE_INFO, ...)`, which the tool writes to the `-D` stream; the body after a `RETR` or `LIST` `+OK` goes through `pop3_write` to the output.
- Before pinning a whole session, measure with `Record-CurlExchange.ps1 -Pop3` (with `-D` and with `-i`) a `LIST` and a `RETR` (`pop3://host/1`), and record in Notes exactly which lines reach the `-D` file, including whether the `+OK` line that starts the body is written there.
- Code: `Curl.Protocol.Pop3.UnitLibrary/Pop3ControlChannel.cs` (`ReadResponseAsync`, `ReadCapabilitiesAsync`; `ReadChunkAsync` is the body); write to the BL-1129 property (read its Notes for the final name) when it is not `null`. `Curl.Protocol.Pop3.UnitLibrary` may also have BL-1120 waiting; they share `touches` and run one after another.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Pop3.UnitTests` pin the measured exchange above, and the measured `LIST` and `RETR` sessions, so the new stream's bytes match real curl's `-D` file byte for byte and `Output` is unchanged.
- [x] A test pins that with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape) `Output` gains nothing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 on curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Pop3`, `-s -u u:p -D <file>`: for `LIST` the file holds the greeting, `+OK Capability list follows` and every CAPA line through `.`, the AUTH PLAIN `+ ` continuation, `+OK Authenticated` and `+OK 2 messages (266 octets)`; for `RETR 1` the same with `+OK 133 octets`. So the `+OK` line that starts the body is written; the body and `QUIT`'s `+OK Bye` are not. Under `-i` alone stdout held only the message.
- That is exactly the set of lines already reported to `-v`, so `Pop3ControlChannel` takes the `-D` stream (`DumpHeaderOutput`) and writes each line in `EndLineAsync` beside `ReportResponseHeader`; `StopReporting` clears it, so `QUIT`'s answer is not written. A NUL-byte line is refused before it is reported or written.
- Tests: 4 in the new `Pop3ProtocolHandlerDumpHeaderTests`. POP3 tests 263 green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. POP3 transfers write every response line before QUIT to the -D file, as curl does
