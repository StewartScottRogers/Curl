---
id: BL-5267
title: Fix AF-0813: Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5267 — Fix AF-0813: Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read

## Goal

The defect the audit office reported as AF-0813 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0813 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25`

Location: `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25`

`BytesReadSinceRewind > 0` changed to `>= 0` survived, so HasBeenRead is always true. That decides whether the engine calls CURLOPT_IOCTLFUNCTION's RESTARTREAD before re-sending a body, so a redirect or auth retry with nothing read would still call the application's ioctl callback (and fail if it refuses). No test covers the not-yet-read case.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs line 25` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs line 25`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: b615ab1a 9742157d.
