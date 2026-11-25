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
completed:
---
# BL-5914 — Refuse -r and -C on s3:// uploads, lists and deletes, which silently ignore them

## Goal

`s3://` refuses `-r` and `-C` with exit 4 where it cannot send them: uploads, lists and `-X DELETE`.

## Context

Left by BL-5897. Downloads send a `Range` header (BL-5873), but `S3ProtocolHandler` passes `ResumeFrom` and `RangeText` only to `DownloadAsync`, so the other operations ignore them. ADR-1348 decides the same refusal for `az://`.

## Acceptance criteria

- [ ] `-r`, and `-C` other than offset 0, with `-T`, a prefix URL or `-X DELETE` exit 4 naming the option before any connection; downloads still send `Range`.
- [ ] A focused unit test in the library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
