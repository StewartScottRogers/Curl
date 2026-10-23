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
completed:
---

## Goal

The differential responder answers every content- and transfer-encoding route of the tester's server.py.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/chunked`, `/chunkbad`, `/gzip`, `/deflate`, `/rawdeflate`, `/badgzip`, `/unknownenc`, `/multienc`, `/gziptrail`, `/gziptrunc`, `/gzipempty`, `/identity`, `/te-gzip-chunked`, `/chunk-huge-size`, `/chunk-missing-crlf`, `/chunk-trunc`. Compressed bodies: Python zlib level 9 bytes cannot be reproduced by .NET's zlib-ng, and Python may not be run; decide (ADR, decided by Claude under Stewart's delegation) whether to embed fixed byte arrays measured another way or produce them with `GZipStream`/`ZLibStream`/`DeflateStream` at `SmallestSize` and pin the decompressed content plus the framing bytes (gzip header with mtime 0, truncation by 8 bytes, trailing garbage). The comparison is curl vs Curl against the same responder, so exact compressor bytes matter less than identical framing.

## Acceptance criteria

- [ ] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.

## Log

- 2026-10-23: Created.
