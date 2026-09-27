---
id: BL-351
title: Send curl's Content-Range bytes 0-(L-1)/L for an HTTP -T upload with -C -
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-332]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-351 — Send curl's Content-Range bytes 0-(L-1)/L for an HTTP -T upload with -C -

## Goal

An HTTP `-T f.txt` upload with `-C -` sends the whole file with `Content-Range: bytes 0-9/10` (for a 10-byte file), as curl 8.21.0 does, measured.

## Context

- Measured in BL-332 (Notes there): `/mingw64/bin/curl -C - -T f.txt http://127.0.0.1:18333/up`, f.txt `abcdefghij`, sent
  `PUT /up HTTP/1.1
Host: 127.0.0.1:18333
Content-Range: bytes 0-9/10
User-Agent: curl/8.21.0
Accept: */*
Content-Length: 10

abcdefghij`. No HEAD was sent: curl 8.21.0 turns an upload's `-C -` into offset 0 but keeps the Content-Range.
- `-C 0` sends no Content-Range (also measured), and `ITransferContext.ResumeFrom` is `null` for `-C -` with `-T` (the Console resolves `-C -` only against an output file, `CurlCommandRunner.ResolveResumeFromAsync`), so the HTTP handler cannot tell `-C -` from no `-C`. The contract needs a way to say "resume, offset unknown", e.g. a flag on `ITransferContext`, set by `TransferContextFactory`.
- `HttpUploadResume` (BL-332, ADR-0057) formats the Content-Range; `-C -` is the offset-0 case of it.
- Filed from BL-332.

## Acceptance criteria

- [ ] `-C - -T f.txt` over HTTP sends the measured bytes above, pinned in a `HttpProtocolHandlerTests` test.
- [ ] `-C 0 -T f.txt` still sends no Content-Range.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
