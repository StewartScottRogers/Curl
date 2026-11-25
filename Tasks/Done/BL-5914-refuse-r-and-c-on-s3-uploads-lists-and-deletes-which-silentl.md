---
id: BL-5914
title: Refuse -r and -C on s3:// uploads, lists and deletes, which silently ignore them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client, Curl.Extended.Protocol.S3.UnitTests/Client, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5914 — Refuse -r and -C on s3:// uploads, lists and deletes, which silently ignore them

## Goal

`s3://` refuses `-r` and `-C` with exit 4 where it cannot send them: uploads, lists and `-X DELETE`.

## Context

Left by BL-5897. Downloads send a `Range` header (BL-5873), but `S3ProtocolHandler` passes `ResumeFrom` and `RangeText` only to `DownloadAsync`, so the other operations ignore them. ADR-1348 decides the same refusal for `az://`.

## Acceptance criteria

- [x] `-r`, and `-C` other than offset 0, with `-T`, a prefix URL or `-X DELETE` exit 4 naming the option before any connection; downloads still send `Range`.
- [x] A focused unit test in the library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `S3ByteRangeRefusal` refuses before any connection (ADR-1372), message `s3:// does not support <option> on an upload, list or delete; only a download sends a range`. `-C 0` is let through; `-C -` on an upload is refused, as for `az://` (ADR-1348).
- Pinned by `S3ByteRangeRefusalTests`. Fast tests green; S3 library 100% line and branch coverage, 0 failing members.
- `--ai-help` unchanged: no option was added or changed in the command line.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 24a0115f aa739c66 3d836d5e.
- 2026-11-24: Doing -> Done. s3:// refuses -r and -C with exit 4 on uploads, lists and -X DELETE; downloads still send Range
