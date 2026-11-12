---
id: BL-4999
title: Report the final status line, not an interim 1xx, in the S3, GCS, Azure and OCI HTTP head reporters
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpHeadReporter.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpHeadReporter.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpHeadReporter.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpHeadReporter.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4999 — Report the final status line, not an interim 1xx, in the S3, GCS, Azure and OCI HTTP head reporters

## Goal

After an interim `1xx` response head, `-w '%{response_code}'`, `%{http_version}` and `%{content_type}` on `s3://`, `gs://`, `az://` and `oci://` report the final response, as curl does.

## Context

Found by the shift-end review of 2026-11-11 (ADR-0710), code-reviewer, in the diff e8341591d..4287a89b7 (the four reporters are new in it, BL-4997).

`OciHttpHeadReporter.Remember` (`Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpHeadReporter.cs:61`) parses a status line only while `expectsStatusLine` is true. Only `ReportRequestHead` sets that flag, and the first response line clears it. `OciHttpExchange` (line 26, `while (statusCode < 200)`) skips interim heads and reports each of their lines, then the final head's lines, through the same reporter without a new request head. The final `HTTP/1.1 200 OK` line therefore goes to the `Content-Type:` branch, and the report keeps the `100`/`103` code and version. The S3, GCS and Azure copies (`S3HttpHeadReporter.cs`, `GcsHttpHeadReporter.cs`, `AzureStorageHttpHeadReporter.cs`) hold the identical `Remember` logic. The type-design reviewer also noted the four classes are byte-identical apart from names, so a fix has to land in all four.

## Acceptance criteria

- [ ] Each of the four reporters re-parses the code and HTTP version and resets the content type on every response line that starts with `HTTP/`, not only the first one after a request head.
- [ ] Each of the four libraries' `.UnitTests` has a test that reports a `100` or `103` head followed by a `200` head with a `Content-Type`, and asserts `ResponseCode` 200 and the final head's `ContentType`, with `HeaderSize` counting both heads.
- [ ] `dotnet build` is clean and the fast tests pass; the four libraries stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: ab8d9510 30fe593d 7e3b9a33.
