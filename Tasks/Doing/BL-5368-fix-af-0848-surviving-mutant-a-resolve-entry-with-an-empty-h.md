---
id: BL-5368
title: Fix AF-0848: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ResolveOverrides.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5368 — Fix AF-0848: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored

## Goal

The defect the audit office reported as AF-0848 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0848 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`

Location: `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`

if (host.Length > 0) -> >= 0 survived. The code's own comment says ':80:127.0.0.1' is ignored (measured). Under the mutant the entry is added, so LoadResolveEntries writes an 'Added :80:127.0.0.1 to DNS cache' -v line curl does not write (stderr bytes).

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ResolveOverrides.cs line 150` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ResolveOverrides.cs line 150`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: c8509edd 9742157d.
