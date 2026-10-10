---
id: BL-1938
title: Bring ImapResponder, FtpControlChannelResponder and the IsBase64Line lambdas under the gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1938 — Bring ImapResponder, FtpControlChannelResponder and the IsBase64Line lambdas under the gates

## Goal

Every failing member of ImapResponder.Append and its IsBase64Line lambda, FtpControlChannelResponder.SwitchDirectory is under 100% branch coverage and complexity of at most 10.

## Context

Split from BL-1936 (BL-1929's measurement, 2026-10-09). Split methods (a lookup table of commands, say) and add the missing branch tests; never raise a threshold. Measure once with `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary -ReportPath <file>` and read the report.

## Acceptance criteria

- [ ] The members named in the Goal are absent from the failing list of `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
