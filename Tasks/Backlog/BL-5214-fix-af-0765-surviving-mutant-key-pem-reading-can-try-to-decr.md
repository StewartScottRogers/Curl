---
id: BL-5214
title: Fix AF-0765: Surviving mutant: --key PEM reading can try to decrypt a non-key block (or decrypt without a passphrase)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5214 — Fix AF-0765: Surviving mutant: --key PEM reading can try to decrypt a non-key block (or decrypt without a passphrase)

## Goal

The defect the audit office reported as AF-0765 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0765 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444`

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444`

Mutant 'if (passphrase is not null && label.SequenceEqual(EncryptedPrivateKeyLabel))' -> '||' survived (seed 799365 sample). With a passphrase, the mutant decrypts the first PEM block whatever its label: a combined file with the CERTIFICATE block first and an ENCRYPTED PRIVATE KEY after it, a common --cert/--key layout with --pass, fails instead of loading. Without a passphrase, an ENCRYPTED PRIVATE KEY block is passed to Decrypt(..., null) instead of giving 'The key file holds no PEM PRIVATE KEY.'. Both change the transfer's exit code and message.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs line 444` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs line 444`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
