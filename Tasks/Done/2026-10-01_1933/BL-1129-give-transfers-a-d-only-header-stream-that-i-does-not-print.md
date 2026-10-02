---
id: BL-1129
title: Give transfers a -D-only header stream that -i does not print
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1129 — Give transfers a -D-only header stream that -i does not print

## Goal

`ITransferContext` carries a second header stream that is the `-D`/`--dump-header` destination alone (`null` without `-D`, never standard output because of `-i`), and `Curl.Console` fills it, so a protocol handler can write what curl writes to the `-D` file but not under `-i`: the gopher selector and the FTP, IMAP, POP3 and SMTP server response lines. No handler uses it yet; BL-1130 to BL-1134 adopt it.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1`:
  - `curl -s -D <file> gopher://127.0.0.1:<port>/1sel` writes `sel\r\n` to the file; `curl -s -i` the same URL writes only the reply to stdout.
  - `curl -s -D <file> ftp://127.0.0.1:<port>/` answered `220-multi\r\n220 hi\r\n530 no\r\n` (exit 67) writes exactly those three lines to the file; `curl -s -i` writes nothing to stdout. The same holds for `pop3://` (`+OK hi\r\n-ERR no\r\n`), `imap://` (`* OK hi\r\nA001 BAD no\r\n`) and `smtp://` (`220 hi\r\n554 no\r\n`): each server line in the `-D` file, nothing under `-i`.
  - Why: libcurl hands gopher's selector over as `CLIENTWRITE_HEADER` (`lib/gopher.c` line 113 at https://github.com/curl/curl/blob/curl-8_21_0/lib/gopher.c) and every pingpong response line as `CLIENTWRITE_INFO` (`lib/pingpong.c`, after the line is read); both go to the header callback, which writes them to the `-D` stream, while the tool's `-i` shows only HTTP-style headers.
- Today `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` has only `HeaderOutput` (line 141), which its remarks say is the same stream as `Output` under `-i` and a separate one under `-D`, and which "a handler ... never inspects which case it has". So a handler cannot write to the `-D` file without also printing under `-i`.
- In `Curl.Console`, `CurlCommandRunner.cs` builds the `-D` stream (`DumpHeaderOutputStream`, line 2917) and `TransferContextFactory.Create` (line 120) folds it into `HeaderOutput` through `HeaderOutputOf`. Set the new property from the same `-D` stream, so writes to either keep their relative order in the file. Decide and document what it is under `-D -` (standard output, as curl writes there) and under `-D` with `-i` together.
- Name the property for what it is (for example `DumpHeaderOutput`), with a doc comment saying which curl writes belong there and that `HeaderOutput` stays the stream for headers `-i` also shows. Add it to `TransferContext` as an `init` property.
- No command-line option changes, so `--ai-help` is untouched.

## Acceptance criteria

- [x] `ITransferContext` and `TransferContext` have the new property, documented as above; `HeaderOutput`'s remarks point to it.
- [x] Tests in `Curl.Console.UnitTests` pin, through `TransferContextFactory`: with `-D <file>` the property is the `-D` stream; with `-i` alone it is `null`; with neither it is `null`; with `-D <file> -i` it is the `-D` stream, not standard output.
- [x] A test pins that a write to the new property and a write to `HeaderOutput` under `-D <file>` land in the file in the order they were made.
- [x] Every existing test passes unchanged; `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Console`.

## Notes

- Property is `DumpHeaderOutput`, set by `TransferContextFactory.Create` straight from its `headerOutput` argument, which `CurlCommandRunner` already makes the `DumpHeaderOutputStream` around the `-D` destination. `HeaderOutput` without `-i` is that same stream, and with `-i` a `HeaderLineTeeStream` writing each whole line through to it, so writes to either keep their order in the file.
- Decisions: under `-D -` it is standard output (wrapped as `-D` always is), as curl writes the `-D` stream there; under `-D <file> -i` it is the file alone, never standard output, since curl's `-i` shows only HTTP-style headers. `-J`'s header watcher wraps `HeaderOutput` only: it reads `Content-Disposition`, an HTTP header, which never goes to the new stream.
- `Curl.Core.UnitLibrary/RedirectFollower.cs` copies a context for each HTTP redirect and does not copy the new property; left so, as it is outside `touches` and only HTTP follows redirects, which never writes to it.
- Tests: 5 new in `TransferContextFactoryTests`, plus the property in `TransferContextTests` and `ForwardingTransferContext`. Fast tests green; `Measure-CodeQuality.ps1 -Library` reports 0 failing members for both libraries.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Transfers carry DumpHeaderOutput, the -D stream alone that -i does not print
