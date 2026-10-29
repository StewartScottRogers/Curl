---
id: BL-4194
title: Extension: S3 XML bodies for listing, multipart upload and errors
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4193]
touches: [Curl.Extension.S3.UnitLibrary/Xml, Curl.Extension.S3.UnitTests/Xml]
requirement: none
created: 2026-10-29
completed:
---
# BL-4194 — Extension: S3 XML bodies for listing, multipart upload and errors

## Goal

`Curl.Extension.S3.UnitLibrary` reads and writes the S3 XML bodies the `s3://` handler needs: `ListBucketResult` (ListObjectsV2), `InitiateMultipartUploadResult`, the `CompleteMultipartUpload` request body, and the `Error` response body.

## Context

Split from BL-4124 for size. The project is created by BL-4193. BCL only (`System.Xml` / `XmlReader` are base class library; no other parser). AOT-safe: no `XmlSerializer`. ListObjectsV2: `Contents` (`Key`, `Size`, `LastModified`, `ETag`), `CommonPrefixes/Prefix`, `IsTruncated`, `NextContinuationToken`, and the S3 namespace `http://s3.amazonaws.com/doc/2006-03-01/` (MinIO and R2 send the same). Complete body lists `Part` (`PartNumber`, `ETag`) in ascending part order. Error: `Code`, `Message`. Code in `Xml/`.

## Acceptance criteria

- [ ] Parsers and the body writer exist under `Curl.Extension.S3.UnitLibrary/Xml/`, with unit tests using the example bodies in the S3 API reference sections "ListObjectsV2", "CreateMultipartUpload", "CompleteMultipartUpload" and "Error responses", each test citing its section.
- [ ] A truncated listing yields its continuation token; a malformed body yields a typed failure, not an unhandled exception.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
