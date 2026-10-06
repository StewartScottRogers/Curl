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
completed: 2026-10-02
---
# BL-1248 — Keep nghttp3's literal-only QPACK fields out of the dynamic table

## Goal

`QpackEncoder` encodes `:path`, `age`, `content-length`, `etag`, `if-modified-since`, `if-none-match`, `location`, `set-cookie`, any name outside nghttp3's token list, and any field larger than three quarters of the dynamic table capacity as literals without indexing (never inserted into the dynamic table, `N` clear), as nghttp3's `qpack_encoder_decide_indexing_mode` does.

## Context

- Found under BL-1235: nghttp3 (`lib/nghttp3_qpack.c`, `qpack_encoder_decide_indexing_mode`, https://github.com/ngtcp2/nghttp3/blob/main/lib/nghttp3_qpack.c) returns `NGHTTP3_QPACK_INDEXING_MODE_LITERAL` for the names above, for token `-1` (a name nghttp3 has no token for), and when the entry's table space exceeds `max_dtable_capacity * 3 / 4`; only the rest are stored (`STORE`).
- Today `Curl.Http3.UnitLibrary/QpackEncoder.cs` `TryWriteDynamicIndexed` inserts every field that is not never-indexed and has no exact static match, whenever it fits, so `:path` and custom headers go into the dynamic table where nghttp3 keeps them literal.
- Check whether a literal-mode field may still be referenced by an exact dynamic match in nghttp3 (`nghttp3_qpack_lookup_stable` / dynamic lookup with `INDEXING_MODE_LITERAL`) before pinning bytes; the HTTP/2 counterpart is `Curl.Http2.UnitLibrary/HpackEncoder.cs` (`NamesNotIndexed`).

## Acceptance criteria

- [x] Tests in `Curl.Http3.UnitTests` pin, with a dynamic table, that `:path: /x`, `etag: "a"`, a custom name `x-custom: 1` and a field larger than three quarters of the capacity leave the encoder stream empty and encode as a literal (`0x5X` static name reference or `0x2X` literal name), and that `user-agent: curl/8.21.0` is still inserted.
- [x] Every existing QPACK test passes, apart from any that asserted insertion of these fields, which now assert the literal.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Read nghttp3 main's `qpack_encoder_decide_indexing_mode` and `encoder_qpack_map_find`. `LITERAL` mode only stops insertion: an exact dynamic match is still referenced, and only `NEVER` looks up names alone. So `QpackEncoder` still references an entry that `TryInsert` put in the table, pinned by `EncodeFieldSection_LiteralOnlyFieldAlreadyInTheDynamicTable_IsReferencedFromIt`.
- "A name nghttp3 has no token for" is read as: not in the QPACK static table and not `host`, `te`, `:protocol` or `priority`. nghttp3's other tokens of 1000 and above (`connection`, `transfer-encoding`, `upgrade`, ...) hit its `default` branch, which also returns `LITERAL`, so treating them the same matches nghttp3. The size limit is `Size > Capacity * 3 / 4` (integer division) against the current capacity, nghttp3's `ctx.max_dtable_capacity`.
- Decision: added `QpackEncoder(..., tryIndexEveryName: false)`, which acts like nghttp3's `NGHTTP3_NV_FLAG_TRY_INDEX` on every field: names stop mattering, and the three-quarters size limit still applies. RFC 9204 appendix B's encoder inserts `:path`, and the table-mechanics tests insert custom names. The flag keeps the appendix replay byte for byte and keeps those tests' bytes. curl flags no field, so the default is off.
- `QpackRoundTripTests.DynamicTable_OnceWarm_...` now pins `:path` as a `0x51` static-name literal on both requests.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. QpackEncoder keeps nghttp3's literal-only names, untokened names and fields over 3/4 of capacity out of the dynamic table
