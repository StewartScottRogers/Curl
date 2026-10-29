---
id: BL-4198
title: Extension: S3 XML bodies for listing, multipart upload and errors
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4197]
touches: [Curl.Extension.S3.UnitLibrary/Xml, Curl.Extension.S3.UnitTests/Xml, Curl.Extension.S3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4198 — Extension: S3 XML bodies for listing, multipart upload and errors

## Goal

`Curl.Extension.S3.UnitLibrary` reads and writes the S3 XML bodies the `s3://` handler needs: `ListBucketResult` (ListObjectsV2), `InitiateMultipartUploadResult`, the `CompleteMultipartUpload` request body, and the `Error` response body.

## Context

Split from BL-4124 for size. The project is created by BL-4197. BCL only (`System.Xml` / `XmlReader` are base class library; no other parser). AOT-safe: no `XmlSerializer`. ListObjectsV2: `Contents` (`Key`, `Size`, `LastModified`, `ETag`), `CommonPrefixes/Prefix`, `IsTruncated`, `NextContinuationToken`, and the S3 namespace `http://s3.amazonaws.com/doc/2006-03-01/` (MinIO and R2 send the same). Complete body lists `Part` (`PartNumber`, `ETag`) in ascending part order. Error: `Code`, `Message`. Code in `Xml/`.

## Acceptance criteria

- [x] Parsers and the body writer exist under `Curl.Extension.S3.UnitLibrary/Xml/`, with unit tests using the example bodies in the S3 API reference sections "ListObjectsV2", "CreateMultipartUpload", "CompleteMultipartUpload" and "Error responses", each test citing its section.
- [x] A truncated listing yields its continuation token; a malformed body yields a typed failure, not an unhandled exception.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Built directly (small, one folder) rather than through the full `/feature` agent stages, to keep
  the run inside its budget.
- Choices taken (sensible defaults, rule 1): `System.Xml.Linq` (`XDocument.Parse`, DTDs prohibited
  by default) rather than a hand-driven `XmlReader`, since the bodies are small; elements matched
  by local name so MinIO/R2 or a namespace-less body read alike; a typed `S3XmlParseResult<T>`
  (value or failure reason) rather than a thrown exception, so the handler (BL-4199) branches
  without a catch; `IsTruncated` required (ListObjectsV2 always sends it); `Error/Message`
  optional (empty when absent); the Complete body is written in the S3 namespace, compact.
- Added `Curl.Extension.S3.UnitLibrary/CLAUDE.md` to touches: it said the XML format arrives
  later, which this task made untrue. No task in Doing on `origin/work/dark-factory` names it.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.S3.UnitLibrary`: 100% line, 100% branch,
  50 members, 0 failing, worst CRAP 10.
- Platform-neutral: the tests touch no file system, path, OS error or certificate.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e4216b0d 269bc055 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. S3 extension reads ListBucketResult, InitiateMultipartUploadResult and Error bodies and writes the CompleteMultipartUpload body, malformed bodies yielding a typed failure
