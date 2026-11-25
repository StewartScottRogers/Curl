---
id: BL-5913
title: Honour or refuse -r and -C on oci:// transfers, so -C - never appends the whole artifact to the partial file
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client, Curl.Extended.Protocol.OciRegistry.UnitTests/Client, Documentation/Conformance/Extended/OciRegistry.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5913 — Honour or refuse -r and -C on oci:// transfers, so -C - never appends the whole artifact to the partial file

## Goal

`oci://` either honours `-r` and `-C` or refuses them with exit 4 before any request.

## Context

Left by BL-5897 (fitness finding EXT-122, which says oci:// probably shares the gap). No file under `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client` reads `Range`, `RangeText` or `ResumeFrom`. Models: `S3Operations.DownloadAsync` (honours, BL-5873) and `AzureStorageByteRangeRefusal` (refuses, ADR-1348).

## Acceptance criteria

- [x] `-C -` onto a partial file leaves it byte-identical to the pulled artifact or exits 4 naming the option, and `-r 10-19` writes exactly 10 bytes or exits 4.
- [x] A focused unit test in the library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Refused rather than honoured (ADR-1358): part of a blob pulled by digest cannot be verified (ADR-1206). `-r` and `-C` other than offset 0 exit 4 before any connection (`OciByteRangeRefusal`), for pulls and pushes.
- Checked with the built curl.exe on `-C -` onto a partial file and `-r 10-19`: exit 4 naming the option, file unchanged.
- Fast tests green, build clean, format clean on the touched folders; quality run for the library: 100% line and branch, 0 failing members.
- The change is platform-neutral (no file system, path or OS error in it).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: aefa5959 aa739c66 3d836d5e.
- 2026-11-24: Doing -> Done. oci:// refuses -r and -C with exit 4 before any request (ADR-1358)
