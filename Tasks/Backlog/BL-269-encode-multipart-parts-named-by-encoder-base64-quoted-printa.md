---
id: BL-269
title: Encode multipart parts named by ;encoder= (base64, quoted-printable, 7bit, 8bit, binary)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-205]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-269 — Encode multipart parts named by ;encoder= (base64, quoted-printable, 7bit, 8bit, binary)

## Goal

`MultipartFormBodyBuilder` sends a part whose `-F` spec carries `;encoder=` encoded as curl 8.21.0 encodes it, with the `Content-Transfer-Encoding` header and a correct `Content-Length`.

## Context

- Found while delivering BL-205 (2026-09-26): BL-189 parses `;encoder=` into `FormPartSpecification.Encoder`, but `MultipartFormPart` (ADR-0025) has no encoder yet.
- libcurl 8.21.0 `lib/mime.c` (`encoders[]`, `encoder_base64_*`, `encoder_qp_*`, `Curl_mime_prepare_headers`) is the reference; an unknown encoder name is refused by curl, so measure what it prints and exits with.
- Where a criterion says *measured*, run curl 8.21.0 (`/mingw64/bin/curl`) with `Record-CurlExchange.ps1`, record the command and bytes in `Notes`, then pin them in a test.

## Acceptance criteria

- [ ] `MultipartFormPart` carries the encoder, and `base64`, `quoted-printable`, `7bit`, `8bit` and `binary` produce the body bytes and `Content-Length` measured on curl 8.21.0 for a text part and a file part.
- [ ] An unknown encoder name gives the measured exit code and message.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

## Log

- 2026-09-26: Created.
