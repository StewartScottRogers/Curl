---
id: BL-1130
title: Write the gopher selector to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1130 — Write the gopher selector to the -D file as curl does

## Goal

A `gopher://` or `gophers://` transfer with `-D` writes the selector it sent, followed by CRLF, to the `-D` stream BL-1129 adds, before any of the reply; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1`: `curl -s -D <file> gopher://127.0.0.1:<port>/1sel` writes exactly `sel\r\n` (5 bytes) to the file; `curl -s -i` the same URL writes only the reply (`iHello\tfake\t(NULL)\t0\r\n.\r\n`) to stdout.
- curl 8.21.0 `lib/gopher.c` `gopher_do` (https://github.com/curl/curl/blob/curl-8_21_0/lib/gopher.c): each piece of the selector sent is passed to `Curl_client_write(data, CLIENTWRITE_HEADER, buf, nwritten)`, and after the final `\r\n` is sent, `Curl_client_write(data, CLIENTWRITE_HEADER, "\r\n", 2)`. So an empty selector (`gopher://host/` or `/1`) writes just `\r\n`, and a failed send writes only what was sent before it - measure the empty-selector case before pinning it.
- Code: `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` `ExchangeAsync` / `TrySendAsync` (the selector from `GopherSelector.FromUrl`); write to the BL-1129 property (read its Notes for the final name) only when it is not `null`. A refused write to the `-D` stream follows the existing output-write failure path (exit 23); say in Notes what curl does there if it can be measured.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Gopher.UnitTests` pin, with the new stream set: `/1sel` writes `sel\r\n` to it before the reply reaches `Output`; the empty selector writes what was measured; with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape), `Output` holds only the reply.
- [x] The measured `-D` and `-i` bytes (and the empty-selector measurement) are copied into Notes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01, curl 8.21.0 (Windows) Schannel, `Record-CurlExchange.ps1`, reply `iHello\tfake\t(NULL)\t0\r\n.\r\n`:
  - `-s -D <file> gopher://127.0.0.1:47130/1sel`: file `73-65-6C-0D-0A` (`sel\r\n`); stdout the reply; request `sel\r\n`; exit 0.
  - `-s -D <file> gopher://127.0.0.1:47130/` and `.../1` (empty selector): file `0D-0A` (`\r\n`); request `\r\n`; exit 0.
  - `-s -i gopher://127.0.0.1:47130/1sel`: stdout only the reply (`69-48-65-6C-6C-6F-09-66-61-6B-65-09-28-4E-55-4C-4C-29-09-30-0D-0A-2E-0D-0A`); exit 0.
- Done in `GopherProtocolHandler.ExchangeAsync`: the selector and the CRLF are sent as two pieces, each written to `DumpHeaderOutput` (when not null) once sent, as `gopher_do` does. Each piece is now flushed on its own (two flushes instead of one); harmless on the wire.
- Decision: a `-D` stream that refuses a piece ends the transfer with exit 23 and `client returned ERROR on write of <piece length> bytes`, the text curl's client writer gives a refused header write (as measured for FTP and file:// headers, BL-050); the CRLF is then not sent, as `gopher_do` returns on the error. Not measured for gopher: a `-D` write failure cannot be provoked on Windows without a full device. The `curl: Failed writing headers to <file>` line comes from `Curl.Console`'s `DumpHeaderOutputStream`, unchanged.
- Tests: 6 new in `GopherProtocolHandlerDumpHeaderTests` (70 gopher tests pass). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary`: 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. gopher -D writes the sent selector and CRLF before the reply; -i alone adds nothing
