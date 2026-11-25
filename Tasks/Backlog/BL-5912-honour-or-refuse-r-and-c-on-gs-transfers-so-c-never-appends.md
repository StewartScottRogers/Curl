---
id: BL-5912
title: Honour or refuse -r and -C on gs:// transfers, so -C - never appends the whole object to the partial file
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5912 — Honour or refuse -r and -C on gs:// transfers, so -C - never appends the whole object to the partial file

## Goal

`gs://` either sends a `Range` header for `-r` and `-C` or refuses them with exit 4 before any request.

## Context

Left by BL-5897 (fitness finding EXT-122, which says gs:// probably shares the gap). No file under `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client` reads `Range`, `RangeText` or `ResumeFrom`. Models: `S3Operations.DownloadAsync` (honours, BL-5873) and `AzureStorageByteRangeRefusal` (refuses, ADR-1347).

## Acceptance criteria

- [ ] `-C -` onto a partial file leaves it byte-identical to the object or exits 4 naming the option, and `-r 10-19` writes exactly 10 bytes or exits 4.
- [ ] A focused unit test in the library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
