---
id: BL-5649
title: Fix AF-0914: Surviving mutant: --httpsig with an @file key can refuse every non-empty key file and accept an empty one
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpSignatureSetup.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5649 — Fix AF-0914: Surviving mutant: --httpsig with an @file key can refuse every non-empty key file and accept an empty one

## Goal

The defect the audit office reported as AF-0914 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0914 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81`

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81`

Line 81 `return text.Length == 0 ? Refused(CurlExitCode.BadFunctionArgument, $"curl: httpsig: key file '{fileName}' is empty") : new(new HttpSignatureSettings(algorithm, text, keyId, components), CurlExitCode.Ok, null);` mutated to `text.Length != 0` survived Curl.Engine.UnitTests (seed 994434 sample, 12.3 s). With the mutant every readable, non-empty key file ends the transfer with exit 43 and 'key file ... is empty', and an empty file is signed with an empty key. No test reads a key from a file through WithKey, so the exit code a user sees for --httpsig @key.pem is unpinned.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs line 81` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs line 81`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 9270fae9 9742157d.
