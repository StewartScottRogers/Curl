---
id: BL-301
title: Stream base64, quoted-printable and 7bit multipart file parts instead of reading them whole
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-274]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-301 — Stream base64, quoted-printable and 7bit multipart file parts instead of reading them whole

## Goal

A multipart file part under `;encoder=base64`, `quoted-printable` or `7bit` is encoded as it is sent, never held in memory whole, with the same bytes and framing as today.

## Context

- Found while delivering BL-274 (2026-09-26): ADR-0041 reads such a file whole while building, which holds a large file in memory once. curl 8.21.0 (`lib/mime.c`, `encoder_base64_read`, `encoder_qp_read`, `encoder_7bit_read`) encodes while reading.
- A `7bit` byte above 127 would then surface while sending; keep exit 26 and `read error getting mime data`, which may need the HTTP body writer to report a read failure other than as the end of the stream.

## Acceptance criteria

- [ ] Every test in `MultipartFormBodyBuilderEncoderTests` still passes unchanged.
- [ ] A test shows a `base64` file part is not read before the body is read.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
