---
id: BL-5773
title: Fix AF-0976: Surviving mutant: the --ciphers alias SHA256 can be made to also select GCM suites (&& -> ||)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslCipherString.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5773 — Fix AF-0976: Surviving mutant: the --ciphers alias SHA256 can be made to also select GCM suites (&& -> ||)

## Goal

The defect the audit office reported as AF-0976 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0976 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51`

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 891023: survived. Mutated ('SHA256', name => !IsGcm(name) && name.EndsWith("SHA256")) to ('SHA256', name => !IsGcm(name) || name.EndsWith("SHA256")). The SHA256 alias then selects every non-GCM suite and every suite ending SHA256, GCM ones included. In the OpenSSL build, --ciphers SHA256 would offer a different set of suites, changing the ClientHello bytes sent. No test of OpenSslCipherString pins the SHA256 alias's selection.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/OpenSslCipherString.cs line 51` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs line 51`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `OpenSslCipherStringHashAliasTests.Expand_Sha256Alias_SelectsOnlyNonGcmSuitesEndingSha256` (mutant applied by hand: it failed; reverted: all 4407 fast tests in Curl.Networking.UnitTests pass). Added a SHA384 twin test. Production code unchanged.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 79942afa 9742157d.
