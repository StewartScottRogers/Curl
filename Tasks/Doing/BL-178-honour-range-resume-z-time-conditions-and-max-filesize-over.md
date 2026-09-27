---
id: BL-178
title: Honour Range, resume, -z time conditions and --max-filesize over HTTP
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-178 — Honour Range, resume, -z time conditions and --max-filesize over HTTP

## Goal

The HTTP handler sends Range for `ByteRange`/`ResumeFrom`, If-Modified-Since/If-Unmodified-Since for `TimeCondition`, and enforces `MaxFileSize`, with curl's exits.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H10. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ITransferContext.Range`, `ResumeFrom`, `MaxFileSize` and `TimeCondition` already exist; `-r`, `-C` and `--max-filesize` reach them (BL-095); `-z` parsing is BL-138.
- Measured: `-C` against a server that ignores ranges gives exit 33 `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
- `-R`/`--remote-time` (BL-079) stamps the output from `TransferResult.SourceLastWriteTimeUtc`; the HTTP source time is the `Last-Modified` header.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Range from `ByteRange` and from `ResumeFrom` is sent in curl's form (measured); a 200 answer to a resume returns `CurlExitCode.RangeError` (33) with the measured message.
- [ ] `TimeCondition` sends If-Modified-Since or If-Unmodified-Since in RFC 1123 form (measured); a 304 writes no body and exits 0.
- [ ] Content-Length over `MaxFileSize` returns `FilesizeExceeded` (63) with the measured message.
- [ ] `TransferResult.SourceLastWriteTimeUtc` is set from `Last-Modified` when present.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H10 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
