---
id: BL-5589
title: Fix AF-0901: Surviving mutant: a marshalled read callback that fills the whole buffer can upload nothing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5589 — Fix AF-0901: Surviving mutant: a marshalled read callback that fills the whole buffer can upload nothing

## Goal

The defect the audit office reported as AF-0901 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0901 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:67`

Location: `Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:67`

Mutant if (count > 0 && count <= filled.Length) -> count < filled.Length survived Curl.Engine.UnitTests (seeded sample, seed 622456). ReadThrough runs the read callback on the caller's thread into a copy, then copies the bytes into the transfer's buffer. Mutated, a callback that returns exactly the buffer's length (the normal case for a large upload) reports that count, but its bytes are never copied. The request body sent is whatever was in the buffer, so the request bytes are wrong. No test reads a full buffer through the marshal.

Reproduction: the finding's reproduction runs an audit office tool, which a lane may not run; the audit office reruns it on the re-audit. Prove the fix with a test instead.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] A fast test fails on the code as the evidence above describes it and passes after the fix; Notes name the test.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
