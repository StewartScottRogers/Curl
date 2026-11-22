---
id: BL-5733
title: Fix AF-0962: Surviving mutant: --jsonrpc whose method name ends the value can crash with IndexOutOfRangeException (MethodEnd end < text.Length -> <=)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5733 — Fix AF-0962: Surviving mutant: --jsonrpc whose method name ends the value can crash with IndexOutOfRangeException (MethodEnd end < text.Length -> <=)

## Goal

The defect the audit office reported as AF-0962 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0962 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71`

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at JsonRpcCallList.cs:71 operator '<' survived. Original: 'while (end < text.Length && !IsWhitespace(text[end]) && text[end] is not Separator and not (byte)'[' and not (byte)'{')'; mutated: 'end <= text.Length'. With the mutant, a method name that runs to the end of the value (curl --jsonrpc get_data URL) reads text[text.Length] and throws IndexOutOfRangeException out of JsonRpcCallList.Parse, so curl crashes instead of posting the call. No test parses a value that ends in a method name: Parse_AMethodAlone_IsOneCallWithoutParamsAndId1_Section4 uses '  get_data  ' with trailing whitespace, and the batch test ends in 'get_data;'. This changes the exit code and request bytes a user sees.

Reproduction: the audit office's mutation test changed `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs line 71` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs line 71`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: e560cad0 9742157d.
