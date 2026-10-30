---
id: BL-4285
title: Extension GCS: read the XML API ListBucketResult and Error bodies
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4284]
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary/Xml, Curl.Extension.GoogleCloudStorage.UnitTests/Xml, Curl.Extension.GoogleCloudStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4285 — Extension GCS: read the XML API ListBucketResult and Error bodies

## Goal

The GoogleCloudStorage extension reads the XML API `ListBucketResult` (list objects, with prefix, delimiter and the continuation value) and `Error` bodies.

## Context

Split from BL-4125 for size, as BL-4198 was split from BL-4124. Copy the shape of `Curl.Extension.S3.UnitLibrary/Xml` (`System.Xml.Linq`, match by local name, a parse result that is never an exception). Specification: Google Cloud Storage XML API, "List objects" (GET bucket) response body and "XML API error responses". Needs BL-4284's project.

## Acceptance criteria

- [x] Parsers in `Xml/` read `ListBucketResult` (keys, sizes, common prefixes, truncation and the continuation value) and `Error` (Code, Message, Details) bodies, returning a failure with its reason for a body that is not well-formed or lacks a required element.
- [x] Unit tests use bodies from the named specification sections, with no network.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: copy the S3 extension's Xml shape (System.Xml.Linq, match by local name, GcsXmlParseResult never an exception) as GcsXmlBody, GcsListBucketResultParser and GcsErrorResponseParser.
- Decided (sensible default): the listing's continuation value is NextContinuationToken when Cloud Storage sends one (list-type=2), else NextMarker (marker paging), so one field serves both paging styles. Error Details is optional (null when absent), Message empty when absent, Code required.
- Touches widened by Curl.Extension.GoogleCloudStorage.UnitLibrary/CLAUDE.md to keep the library's file list true; no task in Doing on origin/work/dark-factory names it.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Extension.GoogleCloudStorage.UnitLibrary: 100% line, 100% branch, 0 failing members. Full build clean, fast tests green. Platform-neutral (string bodies only, no paths or OS text).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9aac869a 49ccba84 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The GCS extension reads XML API ListBucketResult and Error bodies
