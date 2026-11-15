---
id: BL-5244
title: Fix AF-0793: Surviving mutant: an unusable -x behind --preproxy can be ignored instead of failing the transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5244 — Fix AF-0793: Surviving mutant: an unusable -x behind --preproxy can be ignored instead of failing the transfer

## Goal

The defect the audit office reported as AF-0793 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0793 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:181`

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:181`

Mutant 'return false;' -> 'return true;' in TrySelectProxyBehindPreProxy survived (seed 5851). When selector.TrySelect refuses the proxy text it sets failure and the method must return false. The mutant returns true, so the caller at line 124 goes on with no proxy and the transfer runs straight to the origin instead of ending with the selector's exit code and message. The exit code and the request route both change.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferProxySelection.cs line 181` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferProxySelection.cs line 181`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
