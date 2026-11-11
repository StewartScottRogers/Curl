---
id: BL-4978
title: Make s3 refuse -X and -I it cannot honour (EXT-004, s3)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3RequestMethod.cs, Curl.Extended.Protocol.S3.UnitTests/S3RequestMethodTests.cs, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4978 — Make s3 refuse -X and -I it cannot honour (EXT-004, s3)

## Goal

`-X DELETE` and `-I` on s3:// fail with a non-zero exit before any request, so a destructive request never reports success unsent.

## Context

Split from BL-4904 (EXT-004 of the 2026-11-11 outside fitness review): s3:// sent GET for `-X DELETE` and `-I`. BL-4904 decided (ADR-1238, refuse rather than implement): `-I` and any `-X` other than GET (no `-T`) / PUT (with `-T`) exit 4 (NotBuiltIn) with `s3:// does not support -X <M>; it sends only GET, or PUT with -T` before any connection. The library has its own `Client/*RequestMethod.RefusalOf` called first in the handler's `ExecuteAsync`. Library: `Curl.Extended.Protocol.S3.UnitLibrary`. ADR-1238 and its README row (Documentation ADR folder) were written in the stash; this task lands them (add to touches if needed).

## Acceptance criteria

- [ ] `-X DELETE`, `-X <other>` and `-I` on s3:// exit 4 before any connection, and never write the object body; a unit test in the library's `.UnitTests` project pins each.
- [ ] `Documentation/Conformance/Extended/S3.md` has a section listing the supported methods (GET, and PUT with -T) and the refusal.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass; Measure-CodeQuality shows the library within its gates.

## Notes

Split from BL-4904 (cost cap).
Continues the stashed work of BL-4904.
The stash holds all four schemes' uncommitted work; keep only this scheme's files (its `*RequestMethod.cs`, handler edit, `*RequestMethodTests.cs`, conformance section) and leave the others' alone. Test files were made from the S3 one by sed: check constructors for compile errors. Measure nothing against real curl (extension scheme, no upstream answer).

## Log

- 2026-11-11: Created.
