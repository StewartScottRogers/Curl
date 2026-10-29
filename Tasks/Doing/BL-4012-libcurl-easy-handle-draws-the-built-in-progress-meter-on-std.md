---
id: BL-4012
title: Libcurl easy handle draws the built-in progress meter on stderr when NOPROGRESS=0 and no callback, or the callback returns CURL_PROGRESSFUNC_CONTINUE
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CallbackTransferProgress.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Console/TransferProgressRecorder.cs, Curl.Console/ProgressMeterLines.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4012 — Libcurl easy handle draws the built-in progress meter on stderr when NOPROGRESS=0 and no callback, or the callback returns CURL_PROGRESSFUNC_CONTINUE

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-28: Created.

## Notes

- Found by BL-3811 (CGR-282). The meter (header lines, status lines, `-#`) lives only in Curl.Console (`TransferProgressRecorder`, 554 lines, `ProgressMeterLines`), which Curl.Engine cannot reference. The Libcurl path (`CallbackTransferProgress.Of` in Curl.Engine.UnitLibrary) has no meter, so with `CURLOPT_NOPROGRESS=0` and an XFERINFOFUNCTION returning `ProgressCallbackResult.Continue` (0x10000001) nothing is drawn; real curl writes the ~470-byte meter to stderr. Needed: move the meter drawing to a library both can reference (or an interface the Engine calls), make Continue select it, then BL-3811 adds its Cgr282Tests and criteria. Widen `touches` as the move requires.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
