---
id: BL-5541
title: Fix AF-0877: Surviving mutant: an S3 request header starting with CR or LF is sent instead of refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5541 — Fix AF-0877: Surviving mutant: an S3 request header starting with CR or LF is sent instead of refused

## Goal

The defect the audit office reported as AF-0877 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0877 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54`

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54`

Line 54 refuses a header field holding CR or LF: 'if (name.AsSpan().IndexOfAny('\r', '\n') >= 0 || value.AsSpan().IndexOfAny('\r', '\n') >= 0)'. The targeted -Site run mutates the name check to '> 0' and it survived. The seeded sample mutated the value check to '> 0' and that survived too. So a name or value whose first character is CR or LF passes the guard and is written into the request head, injecting request bytes. SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException has no data row with the line break at index 0.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs line 54` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs line 54`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: c3663800 9742157d.
