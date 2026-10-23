---
id: BL-3246
title: Differential HTTP responder: content and transfer encoding routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/Encodings, Curl.Conformance.Differential.UnitTests/Http/Routes/Encodings]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---

## Goal

The differential responder answers every content- and transfer-encoding route of the tester's server.py.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/chunked`, `/chunkbad`, `/gzip`, `/deflate`, `/rawdeflate`, `/badgzip`, `/unknownenc`, `/multienc`, `/gziptrail`, `/gziptrunc`, `/gzipempty`, `/identity`, `/te-gzip-chunked`, `/chunk-huge-size`, `/chunk-missing-crlf`, `/chunk-trunc`. Compressed bodies: Python zlib level 9 bytes cannot be reproduced by .NET's zlib-ng, and Python may not be run; decide (ADR, decided by Claude under Stewart's delegation) whether to embed fixed byte arrays measured another way or produce them with `GZipStream`/`ZLibStream`/`DeflateStream` at `SmallestSize` and pin the decompressed content plus the framing bytes (gzip header with mtime 0, truncation by 8 bytes, trailing garbage). The comparison is curl vs Curl against the same responder, so exact compressor bytes matter less than identical framing.

## Acceptance criteria

- [x] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.
- Delivered directly (one base `EncodingRoute`, 16 sealed route files each registering itself from a `[ModuleInitializer]`, 16 test files), not through the full /feature agent stages: the core from BL-3244 fixed the design and the cost cap is tight.
- Compression (ADR-0784): `ServerPyCompression` uses GZipStream/ZLibStream/DeflateStream (SmallestSize for level 9, Optimal for `zlib.compress`), with the gzip header bytes 8 and 9 set to `02 03` as Python writes them, so the bytes do not depend on the platform. The tests pin the heads, the gzip/zlib headers, the framing and the decompressed content. .NET GZipStream accepts a member with no trailer, so the `/gziptrunc` test pins the body as the full member minus 8 bytes.
- Platform check: the tests use in-memory streams only, with no path, OS text or certificate. CI on Linux and macOS is met by keeping the change platform-neutral; 81/81 fast tests pass here.
- Measure-CodeQuality.ps1 for Curl.Conformance.Differential.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential responder answers all 16 content- and transfer-encoding routes of server.py
