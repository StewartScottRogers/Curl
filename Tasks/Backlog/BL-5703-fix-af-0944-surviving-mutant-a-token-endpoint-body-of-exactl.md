---
id: BL-5703
title: Fix AF-0944: Surviving mutant: a token-endpoint body of exactly the 1 MiB limit can be refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5703 — Fix AF-0944: Surviving mutant: a token-endpoint body of exactly the 1 MiB limit can be refused

## Goal

The defect the audit office reported as AF-0944 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0944 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42`

Location: `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42`

Drawn library, seed 812510: body.WrittenCount + length > MaxBodyLength mutated to >= survived; a body of exactly the limit fails as BodyTooLarge with a failure exit code instead of being read. PostFormAsync_CloseDelimitedBodyOver1MiB_FailsAsBodyTooLarge covers only a body over the limit.

Reproduction: the audit office's mutation test changed `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs line 42` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs line 42`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
