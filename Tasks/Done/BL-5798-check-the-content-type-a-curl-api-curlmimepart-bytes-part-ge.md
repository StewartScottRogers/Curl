---
id: BL-5798
title: Check the Content-Type a Curl.Api CurlMimePart.Bytes part gets with no ContentType, against libcurl's curl_mime_data and curl -F @file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Mime/CurlMimePart.cs, Curl.Api/Mime/CurlMultipartContent.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5798 — Check the Content-Type a Curl.Api CurlMimePart.Bytes part gets with no ContentType, against libcurl's curl_mime_data and curl -F @file

## Goal

A `Curl.Api` MIME part made with `CurlMimePart.Bytes` and no `ContentType` gets the same `Content-Type` (or none) that libcurl 8.22.0 gives the matching part: `curl_mime_data` with no file name, or with a file name set, over HTTP and SMTP.

## Context

From Surly cookbook bug report `2026-11-22T2157-Curl-bug-report.md` (batch 13 of the 1,000-recipe plan), finding 4. Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11.

Repro: `CurlMimePart.Bytes("report", bytes)` with no `ContentType`, sent over SMTP or HTTP: the part has no `Content-Type` line; the cookbook expected `application/octet-stream`, as `curl -F report=@report.bin` sends. But `-F name=@file` is a file part (`curl_mime_filedata`), whose type libcurl guesses from the file name's extension and otherwise sets `application/octet-stream`; a data part with no file name (`curl_mime_data`) gets no `Content-Type` in libcurl over HTTP, while over SMTP/IMAP libcurl's MIME may differ (check `mime.c` `Curl_mime_prepare_headers`: a part with a file name or data under a non-form MIME gets a default). Measure upstream 8.22.0 for: data part, no name; data part with `curl_mime_filename`; file part; each over HTTP (`-F`) and SMTP (`--mail-rcpt` + `-F`). Make `CurlMimePart.Bytes` (with and without a file name) match libcurl for each, and document on `Bytes` which header the part gets. Recipe `smtp-send-mail-with-an-attachment-in-csharp`.

## Acceptance criteria

- [x] The Notes record libcurl 8.22.0's `Content-Type` for each of the six cases above (data/data-with-filename/file, over HTTP and SMTP).
- [x] `CurlMimePart.Bytes` gives the same for each, pinned by tests of the request bytes; its doc comment says which header a part gets.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- libcurl 8.22.0 (Schannel, Windows), measured with Record-CurlExchange.ps1 (HTTP) and a loopback SMTP script (`--mail-rcpt`, `-F`): part Content-Type for
  - HTTP data part, no file name (`-F report=abc`): none.
  - HTTP data part with file name: `r.bin` none, `r.txt` `text/plain`.
  - HTTP file part (`-F report=@f`): `report.bin` `application/octet-stream`, `report.txt` `text/plain`.
  - SMTP data part, no file name: none (`Content-Disposition: attachment; name="report"`).
  - SMTP data part with file name: none for `r.bin` and for `r.txt` (a default text/plain is unlabelled in mail).
  - SMTP file part: `report.bin` `application/octet-stream` plus `Content-Transfer-Encoding: 8bit`; `report.txt` none.
- Curl 's engine (`MultipartPartHeaders`) already gives byte-identical results for all ten runs (checked against the built curl.exe), so `CurlMimePart.Bytes` already matches libcurl; the cookbook's expectation came from `-F @file` being a file part. No behaviour change.
- Done: doc comment on `Bytes` says which header a part gets; `SendAsync_BytesPartWithNoContentType_LeavesTheTypeToTheEngine` pins that Curl.Api sets no type (and passes the file name) so the engine's guess applies. Curl.Api.UnitTests cannot use a real engine (CLAUDE.md of Curl.Api), so the request bytes themselves are pinned by the engine's own MultipartPartHeaders tests. Only a doc comment changed in production code, so no quality run.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2157, finding 4.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 56cdac44 a11660a4 3d836d5e.
- 2026-11-22: Doing -> Done. Engine already matches libcurl; documented and pinned Curl.Api pass-through
