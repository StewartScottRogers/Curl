---
id: BL-2219
title: Add every upstream case that passes in process but is not yet on the PassingUpstreamCases.txt ratchet
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2219 — Add every upstream case that passes in process but is not yet on the PassingUpstreamCases.txt ratchet

## Goal

Put on `PassingUpstreamCases.txt` every case that passes in process but is not listed, so the ratchet holds them.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%); counts from running `Curl.Conformance.UnitTests` on 2026-10-14, where about 100 `UpstreamCase_RunThroughCurl_HoldsTheRatchet` rows ended `Inconclusive` with "testN passes; add N to PassingUpstreamCases.txt" (for example 17, 56, 71, 90, 146, 149, 155, 203, 214, 215, 216, 271, 283-286, 332, 369, 381, 384, 409, 416, 428, 448, 458, 610, 649, 1725, 2004, 2033, 2070, 2079, 3024). The gap tool counts these as `match` already; the ratchet is what keeps them from regressing.

Run `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration&FullyQualifiedName~UpstreamConformanceTests"` and read the Inconclusive messages (trx logger). A case listed for one platform only must follow how the file already handles platform-specific cases (read `UpstreamCaseRatchet.cs` and the file's header; ADR-0488). Lines stay in the file's existing order and format.

## Acceptance criteria

- [ ] Every case the run reports as "passes; add N to PassingUpstreamCases.txt" on Windows is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and a second run reports none, with the same cases not failing on Linux or macOS (a case that passes only on Windows is handled as the ratchet's existing platform rule says, and named in Notes).
- [ ] Any case that passes intermittently is left out and named in Notes with the cause, with a follow-up task.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-14: Created.
