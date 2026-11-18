---
id: BL-5421
title: Stamp timings in EndPointReportingProtocolHandler only on a result that has a report
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5421 — Stamp timings in EndPointReportingProtocolHandler only on a result that has a report

## Goal

A handler failure that returned no `TransferReport` still reaches `ManagedEasyTransfer` with `Report == null`, and `%{size_upload}` counts `-d` data only when it was sent.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (type-design-analyzer and code-reviewer agents), in BL-5410's change.
`Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs:57-66`: `StampTimingsAndUploadSize` does `result.Report ?? new TransferReport { ... }` and stamps timings whenever `Timings` is null, so every result now carries a report (`EngineComposition.cs:210` wraps every handler). Code that reads a null report as "the handler reported nothing" no longer fires:
- `ManagedEasyTransfer.Finish.cs:257` `ResponseCodeOf` (BL-3309) now takes 0 instead of the status the header recording kept.
- `ManagedEasyTransfer.Finish.cs:293` `TimedOutWithNoReport` and `:296` `CutOffAfterHead` (BL-3302, BL-3316) always return false, so `CURLINFO_NUM_CONNECTS` changes.
- `AbortedHeadRecordingTransferEvents.cs:35` matches `Report: null` only.
- `TransferInfoFromReport.cs:39` and `TransferInfoFromReport.Scheme.cs:67`.

`UploadedBy` also returns `context.PostData.Length` when no connection was made, so `-d x` to a refused port reports `%{size_upload}` 1 where curl reports 0.

## Acceptance criteria

- [ ] `StampTimingsAndUploadSize` returns a result whose `Report` is null unchanged (or stamps only when a connection was recorded), and a unit test pins it.
- [ ] A unit test shows a refused connect with `-d x` reports an upload size of 0.
- [ ] A new test shows `ResponseCodeOf` keeps the recorded status after a failure with no handler report; the BL-3309, BL-3302 and BL-3316 tests and `Cgr144Tests` still pass.
- [ ] BL-5410's own tests still pass; Curl.Engine.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 5bbb1515 f8d8edf1 b7a34fb5 049772bf.
