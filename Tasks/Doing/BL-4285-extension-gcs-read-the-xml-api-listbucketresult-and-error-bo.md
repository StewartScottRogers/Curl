---
id: BL-4285
title: Extension GCS: read the XML API ListBucketResult and Error bodies
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4284]
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary/Xml, Curl.Extension.GoogleCloudStorage.UnitTests/Xml]
requirement: none
created: 2026-10-29
completed:
---
# BL-4285 — Extension GCS: read the XML API ListBucketResult and Error bodies

## Goal

The GoogleCloudStorage extension reads the XML API `ListBucketResult` (list objects, with prefix, delimiter and the continuation value) and `Error` bodies.

## Context

Split from BL-4125 for size, as BL-4198 was split from BL-4124. Copy the shape of `Curl.Extension.S3.UnitLibrary/Xml` (`System.Xml.Linq`, match by local name, a parse result that is never an exception). Specification: Google Cloud Storage XML API, "List objects" (GET bucket) response body and "XML API error responses". Needs BL-4284's project.

## Acceptance criteria

- [ ] Parsers in `Xml/` read `ListBucketResult` (keys, sizes, common prefixes, truncation and the continuation value) and `Error` (Code, Message, Details) bodies, returning a failure with its reason for a body that is not well-formed or lacks a required element.
- [ ] Unit tests use bodies from the named specification sections, with no network.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9aac869a 49ccba84 3180db99 3d836d5e.
