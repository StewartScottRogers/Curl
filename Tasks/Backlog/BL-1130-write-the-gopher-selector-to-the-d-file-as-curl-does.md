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
completed:
---
# BL-1130 — Write the gopher selector to the -D file as curl does

## Goal

A `gopher://` or `gophers://` transfer with `-D` writes the selector it sent, followed by CRLF, to the `-D` stream BL-1129 adds, before any of the reply; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1`: `curl -s -D <file> gopher://127.0.0.1:<port>/1sel` writes exactly `sel\r\n` (5 bytes) to the file; `curl -s -i` the same URL writes only the reply (`iHello\tfake\t(NULL)\t0\r\n.\r\n`) to stdout.
- curl 8.21.0 `lib/gopher.c` `gopher_do` (https://github.com/curl/curl/blob/curl-8_21_0/lib/gopher.c): each piece of the selector sent is passed to `Curl_client_write(data, CLIENTWRITE_HEADER, buf, nwritten)`, and after the final `\r\n` is sent, `Curl_client_write(data, CLIENTWRITE_HEADER, "\r\n", 2)`. So an empty selector (`gopher://host/` or `/1`) writes just `\r\n`, and a failed send writes only what was sent before it - measure the empty-selector case before pinning it.
- Code: `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` `ExchangeAsync` / `TrySendAsync` (the selector from `GopherSelector.FromUrl`); write to the BL-1129 property (read its Notes for the final name) only when it is not `null`. A refused write to the `-D` stream follows the existing output-write failure path (exit 23); say in Notes what curl does there if it can be measured.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Gopher.UnitTests` pin, with the new stream set: `/1sel` writes `sel\r\n` to it before the reply reaches `Output`; the empty selector writes what was measured; with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape), `Output` holds only the reply.
- [ ] The measured `-D` and `-i` bytes (and the empty-selector measurement) are copied into Notes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
