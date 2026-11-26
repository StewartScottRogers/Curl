---
id: BL-5988
title: Fix AF-0994: Surviving mutant: curl_url_set can refuse a value of exactly 8,000,000 characters with CURLUE_MALFORMED_INPUT (> -> >=)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5988 — Fix AF-0994: Surviving mutant: curl_url_set can refuse a value of exactly 8,000,000 characters with CURLUE_MALFORMED_INPUT (> -> >=)

## Goal

The defect the audit office reported as AF-0994 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0994 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97`

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97`

Mutant 'if (value?.Length > MaximumInputLength)' -> 'if (value?.Length >= MaximumInputLength)' (MaximumInputLength = 8_000_000, curl's CURL_MAX_INPUT_LENGTH) survived every Curl.Engine.UnitTests test. A part value of exactly the limit then returns CurlUrlCode.MalformedInput instead of being accepted, which changes the code curl_url_set returns to a libcurl caller. No test pins the boundary.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs line 97` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs line 97`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killing test: `ManagedCurlUrlHandleTests.Set_PartOfExactlyMaximumInputLength_IsAccepted`
  (data rows `User` and `Fragment`) in `Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs`.
  With `>` changed to `>=` both rows failed (2 failed, 257 passed, 2 skipped among the class's
  tests); with it reverted, `dotnet build` (0 warnings) and all fast tests passed.
- Production code was already right (curl refuses only `strlen > CURL_MAX_INPUT_LENGTH`), so
  only a test was added. No library production change, so no quality run was needed.
- Added the test file to `touches`; no other task in Doing named it.
- The test sets one part, not the whole URL: a whole URL of that length is refused by the URL
  parser's own limit, which would not isolate this guard. A part value is platform-neutral.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: afaf2634 9742157d.
- 2026-11-25: Doing -> Done. Boundary test kills the AF-0994 mutant; build and fast tests pass
