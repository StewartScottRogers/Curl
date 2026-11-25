---
id: BL-5507
title: Fix AF-0865: Surviving mutant: an OCI request field name starting with a space, colon or line break is sent
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5507 — Fix AF-0865: Surviving mutant: an OCI request field name starting with a space, colon or line break is sent

## Goal

The defect the audit office reported as AF-0865 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0865 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42`

Location: `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42`

Mutant 'value.AsSpan().IndexOfAny(" \t\r\n:") < 0' -> '<= 0' survived Curl.Extended.Protocol.OciRegistry.UnitTests. RequireToken guards methods and field names (RFC 9110 5.6.2). With the mutant, a forbidden character at index 0 is accepted, so a name beginning with CR LF or ':' is written into the request head (header injection, changed request bytes) instead of failing with InvalidRequestField. SendAsync_FieldThatWouldBreakTheHead_FailsWithoutWriting never puts the bad character first.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs line 42` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs line 42`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mutant `<= 0` applied by hand at line 42: 4 new cases of `SendAsync_FieldThatWouldBreakTheHead_FailsWithoutWriting` fail (colon, space, CR LF first in a name; LF first in the method); reverted, all 231 pass. Fast tests of the OCI project pass; production code unchanged.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: c3a9b46d 9742157d.
- 2026-11-18: Doing -> Done. Added leading-forbidden-character cases; mutant <= 0 now fails 4 tests
