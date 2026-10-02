---
id: BL-1235
title: Encode authorization and short cookie fields as never-indexed QPACK literals, as nghttp3 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1235 — Encode authorization and short cookie fields as never-indexed QPACK literals, as nghttp3 does

## Goal

`QpackEncoder` encodes an `authorization` field, and a `cookie` field whose value is shorter than 20 bytes, as a never-indexed literal (the `N` bit set, never a full index, never inserted into the dynamic table) even when the caller did not mark it `HeaderField.IsNeverIndexed`, as nghttp3, which curl's HTTP/3 build uses, does.

## Context

- Today `Curl.Http3.UnitLibrary/QpackEncoder.cs` treats a field as never-indexed only when `HeaderField.IsNeverIndexed` is set (lines ~212, 282 and 286), and no caller sets it, so an `Authorization` request header goes out as a literal with name reference and `N` clear (`0x5X`), where nghttp3 writes `0x7X`. The HTTP/2 encoder already follows nghttp2's equivalent rule: `Curl.Http2.UnitLibrary/HpackEncoder.cs` lines 14-18 and 31-38 (`NamesNeverIndexed`, `ShortestIndexedCookieLength = 20`).
- nghttp3, `lib/nghttp3_qpack.c` `qpack_encoder_decide_indexing_mode` (checked on `main` on 2026-10-02, https://github.com/ngtcp2/nghttp3/blob/main/lib/nghttp3_qpack.c, lines ~1303-1371): a field flagged never-index, `authorization`, and `cookie` with a value shorter than 20 bytes get `NGHTTP3_QPACK_INDEXING_MODE_NEVER`; `nghttp3_qpack_lookup_stable` (lines ~1631-1660) then returns only the static table's first entry with that name (no name-and-value match), so the field is a literal with static name reference and `N` set, or a literal with a literal name and `N` set when the static table lacks the name.
- `HeaderField.IsNeverIndexed` is in `Curl.Http2.UnitLibrary` (`HeaderField.cs`), which `Curl.Http3.UnitLibrary` already uses; do not change it. The HTTP library's tests decode request sections and compare `name: value` only, so they do not depend on the `N` bit.
- Compare the rest of nghttp3's rule (fields kept out of the dynamic table: `:path`, `age`, `content-length`, `etag`, `if-modified-since`, `if-none-match`, `location`, `set-cookie`, and entries larger than three quarters of the table capacity) with `QpackEncoder`; if they differ, record the difference in `Notes` and leave it for a follow-up task rather than widening this one.

## Acceptance criteria

- [ ] Tests in `Curl.Http3.UnitTests` pin the encoded bytes: `authorization: Basic dTpw` without `IsNeverIndexed` is encoded with the `N` bit set and a static name reference (static index 84), with and without a dynamic table; `cookie: a=b` likewise (static index 5); `cookie: <20 bytes>` is encoded as before (indexable); a decoder reading each section gets the field back with `IsNeverIndexed` true for the first two.
- [ ] With a dynamic table, neither field is ever inserted or referenced from the dynamic table; a test pins the encoder stream staying empty for them.
- [ ] Every existing QPACK test passes unchanged, apart from any that asserted the old bytes for these two names, which now assert the new ones.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
