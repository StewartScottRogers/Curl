---
id: BL-4980
title: Make gs refuse -X and -I it cannot honour (EXT-004, gs)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4982]
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client/GcsRequestMethodTests.cs, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4980 — Make gs refuse -X and -I it cannot honour (EXT-004, gs)

## Goal

`-X DELETE` and `-I` on gs:// fail with a non-zero exit before any request, so a destructive request never reports success unsent.

## Context

Split from BL-4904 (EXT-004 of the 2026-11-11 outside fitness review): gs:// sent GET for `-X DELETE` and `-I`. BL-4904 decided (ADR-1238, refuse rather than implement): `-I` and any `-X` other than GET (no `-T`) / PUT (with `-T`) exit 4 (NotBuiltIn) with `gs:// does not support -X <M>; it sends only GET, or PUT with -T` before any connection. The library has its own `Client/*RequestMethod.RefusalOf` called first in the handler's `ExecuteAsync`. Library: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary`. ADR-1238 is landed by BL-4982; cite it.

## Acceptance criteria

- [x] `-X DELETE`, `-X <other>` and `-I` on gs:// exit 4 before any connection, and never write the object body; a unit test in the library's `.UnitTests` project pins each.
- [x] `Documentation/Conformance/Extended/GoogleCloudStorage.md` has a section listing the supported methods (GET, and PUT with -T) and the refusal.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass; Measure-CodeQuality shows the library within its gates.

## Notes

Split from BL-4904 (cost cap).
Citations use ADR-1249 (the landed number). Quality run: 0 failing members. Committed with the task.
Continues the stashed work of BL-4904.
The stash holds all four schemes' uncommitted work; keep only this scheme's files (its `*RequestMethod.cs`, handler edit, `*RequestMethodTests.cs`, conformance section) and leave the others' alone. Test files were made from the S3 one by sed: check constructors for compile errors. Measure nothing against real curl (extension scheme, no upstream answer).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 594e1aae a7eadfd4 96270709.
- 2026-11-11: Doing -> Done. gs refuses -I and unsendable -X before connecting
