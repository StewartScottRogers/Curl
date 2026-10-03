---
id: BL-1248
title: Keep nghttp3's literal-only QPACK fields out of the dynamic table
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1235]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1248 — Keep nghttp3's literal-only QPACK fields out of the dynamic table

## Goal

`QpackEncoder` encodes `:path`, `age`, `content-length`, `etag`, `if-modified-since`, `if-none-match`, `location`, `set-cookie`, any name outside nghttp3's token list, and any field larger than three quarters of the dynamic table capacity as literals without indexing (never inserted into the dynamic table, `N` clear), as nghttp3's `qpack_encoder_decide_indexing_mode` does.

## Context

- Found under BL-1235: nghttp3 (`lib/nghttp3_qpack.c`, `qpack_encoder_decide_indexing_mode`, https://github.com/ngtcp2/nghttp3/blob/main/lib/nghttp3_qpack.c) returns `NGHTTP3_QPACK_INDEXING_MODE_LITERAL` for the names above, for token `-1` (a name nghttp3 has no token for), and when the entry's table space exceeds `max_dtable_capacity * 3 / 4`; only the rest are stored (`STORE`).
- Today `Curl.Http3.UnitLibrary/QpackEncoder.cs` `TryWriteDynamicIndexed` inserts every field that is not never-indexed and has no exact static match, whenever it fits, so `:path` and custom headers go into the dynamic table where nghttp3 keeps them literal.
- Check whether a literal-mode field may still be referenced by an exact dynamic match in nghttp3 (`nghttp3_qpack_lookup_stable` / dynamic lookup with `INDEXING_MODE_LITERAL`) before pinning bytes; the HTTP/2 counterpart is `Curl.Http2.UnitLibrary/HpackEncoder.cs` (`NamesNotIndexed`).

## Acceptance criteria

- [ ] Tests in `Curl.Http3.UnitTests` pin, with a dynamic table, that `:path: /x`, `etag: "a"`, a custom name `x-custom: 1` and a field larger than three quarters of the capacity leave the encoder stream empty and encode as a literal (`0x5X` static name reference or `0x2X` literal name), and that `user-agent: curl/8.21.0` is still inserted.
- [ ] Every existing QPACK test passes, apart from any that asserted insertion of these fields, which now assert the literal.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
