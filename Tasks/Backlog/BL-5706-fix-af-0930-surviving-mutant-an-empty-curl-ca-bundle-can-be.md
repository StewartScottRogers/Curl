---
id: BL-5706
title: Fix AF-0930: Surviving mutant: an empty CURL_CA_BUNDLE can be taken as the CA bundle path
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5706 — Fix AF-0930: Surviving mutant: an empty CURL_CA_BUNDLE can be taken as the CA bundle path

## Goal

The defect the audit office reported as AF-0930 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0930 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124`

Seed 812510 sample of Curl.Networking.UnitLibrary: ReadEnvironmentVariable("CURL_CA_BUNDLE") is { Length: > 0 } mutated to { Length: >= 0 } survived. curl treats an empty CURL_CA_BUNDLE as unset; the mutant loads the empty path as the default bundle, turning a verified transfer into a certificate-load failure with another exit code. No test sets CURL_CA_BUNDLE to the empty string.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 124` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 124`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
