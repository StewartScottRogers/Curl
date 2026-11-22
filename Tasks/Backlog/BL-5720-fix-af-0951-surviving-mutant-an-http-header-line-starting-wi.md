---
id: BL-5720
title: Fix AF-0951: Surviving mutant: an .http header line starting with ':' can be accepted instead of refused with exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5720 — Fix AF-0951: Surviving mutant: an .http header line starting with ':' can be accepted instead of refused with exit 2

## Goal

The defect the audit office reported as AF-0951 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0951 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126`

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126`

Lines 125-128: HeaderOrThrow(line) => line.IndexOf(':') > 0 ? line : throw new ArgumentExpansionException($"http-file: '{line}' is not a header line", CurlExitCode.FailedInit); mutated to >= 0 survived all 43 HttpFile.UnitTests (all read). A header line with an empty name such as ':x' is then passed on as -H ':x' instead of failing the run with exit 2. The only refusal test, Parse_HeaderLineWithoutAColon_FailsWithExit2, uses '?a=1' (no colon at all), so the index-0 case is unpinned.

Reproduction: the audit office's mutation test changed `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs line 126` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs line 126`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
