---
id: BL-5912
title: Honour or refuse -r and -C on gs:// transfers, so -C - never appends the whole object to the partial file
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Documentation/Conformance/Extended/GoogleCloudStorage.md, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5912 — Honour or refuse -r and -C on gs:// transfers, so -C - never appends the whole object to the partial file

## Goal

`gs://` either sends a `Range` header for `-r` and `-C` or refuses them with exit 4 before any request.

## Context

Left by BL-5897 (fitness finding EXT-122, which says gs:// probably shares the gap). No file under `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client` reads `Range`, `RangeText` or `ResumeFrom`. Models: `S3Operations.DownloadAsync` (honours, BL-5873) and `AzureStorageByteRangeRefusal` (refuses, ADR-1348).

## Acceptance criteria

- [x] `-C -` onto a partial file leaves it byte-identical to the object or exits 4 naming the option, and `-r 10-19` writes exactly 10 bytes or exits 4.
- [x] A focused unit test in the library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Chose to honour, not refuse (ADR-1359): an object download sends `Range` for `-r` and `-C`, as `s3://` does; an upload or a listing refuses both with exit 4 before any request, since a resumed listing would append the whole listing.
- Replies as over https (copied from the `s3://` code, BL-5873; not re-measured here): 206 body written; resume with 416, or 200 whose Content-Length equals the offset, writes nothing; other 200 to a resume is exit 33. `-r 10-19` writes exactly 10 bytes from a 206, which Cloud Storage sends for GET Object; a server that ignores the header and answers 200 hands over the whole object, as over https.
- Checked end to end with Record-CurlExchange.ps1 and the built curl.exe: `-C -` onto 10 bytes sent `range: bytes=10-`, appended the 206 body, and `-w` printed `206 10 0`.
- Added the library CLAUDE.md to touches (no other Doing task names it).
- Fast tests green; quality run for the library: 100% line and branch, 0 failing members. Tests are platform-neutral.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 54562497 aa739c66 3d836d5e.
- 2026-11-24: Doing -> Done. gs:// downloads send a Range header for -r and -C; uploads and listings refuse them with exit 4
