---
id: BL-351
title: Send curl's Content-Range bytes 0-(L-1)/L for an HTTP -T upload with -C -
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-332]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] `-C - -T f.txt` over HTTP sends the measured bytes above, pinned in a `HttpProtocolHandlerTests` test.
- [x] `-C 0 -T f.txt` still sends no Content-Range.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Measured 2026-09-27 with `/mingw64/bin/curl` 8.21.0 via `Record-CurlExchange.ps1`, f.txt `abcdefghij`, empty `200` answer, all exit 0:
  - `-C - -T f.txt` (port 18351): `PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18351\r\nContent-Range: bytes 0-9/10\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 10\r\n\r\nabcdefghij`.
  - `-C - -T f.txt -o out.txt` with a 3-byte out.txt (18352): the same bytes - `-o` plays no part in an upload's `-C -`.
  - `-C - -T empty.txt` (18353): `Content-Range: bytes 0--1/0`, `Content-Length: 0`, no failure.
  - `-C - -T -` (18354, stdin): `Content-Range: bytes 0--2/-1`, chunked, `Expect: 100-continue`.
  - Every run wrote `** Resuming transfer from byte position -1` to stderr first; filed as BL-416.
- Plan (decided, ADR-0087): `ITransferContext.ResumeUploadFromUnknownOffset`, set by `TransferContextFactory` for `-C -` with a `-T` source (extracted to `ResumesUploadFromUnknownOffset` to keep `Create` at complexity 10); `HttpRequestFraming.Of` passes it to `HttpUploadResume.Of`, which sends the whole source with `bytes 0-(L-1)/L` and ignores `ResumeFrom`. Other handlers ignore the flag.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0087 and the ADR-0057 cross-reference; no task in Doing names it.
- Coverage: `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Protocol.Http.UnitLibrary` 0 failing members. `Curl.Console` 0 failing members from this change; the one listed, `DiskWriteOutFileOpener.TryOpen`, predates it and is covered only by `[TestCategory("Integration")]` tests by design (BL-280).
- Follow-ups: BL-416 (the `-1` resume line on stderr), BL-417 (`RedirectFollower` does not carry the flag to the next hop).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -C - -T sends the whole upload over HTTP with curl's Content-Range bytes 0-(L-1)/L
