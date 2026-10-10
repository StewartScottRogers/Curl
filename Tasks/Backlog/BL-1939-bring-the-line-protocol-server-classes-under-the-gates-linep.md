---
id: BL-1939
title: Bring the line-protocol server classes under the gates (LineProtocolReplyData, LineProtocolServerCommands, TrySplitCommand)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1939 — Bring the line-protocol server classes under the gates (LineProtocolReplyData, LineProtocolServerCommands, TrySplitCommand)

## Goal

Every failing member of LineProtocolReplyData.Select, the three TrySplitCommand methods, LineProtocolServerCommands.Read is under 100% branch coverage and complexity of at most 10.

## Context

Split from BL-1936 (BL-1929's measurement, 2026-10-09). Split methods (a lookup table of commands, say) and add the missing branch tests; never raise a threshold. Measure once with `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary -ReportPath <file>` and read the report.

## Acceptance criteria

- [ ] The members named in the Goal are absent from the failing list of `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
