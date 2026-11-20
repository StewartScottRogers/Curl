---
id: BL-5622
title: Fix AF-0907: Surviving mutant: n-fold can drop the end-around carry into its first byte
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Kerberos.UnitLibrary/KerberosNFold.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5622 — Fix AF-0907: Surviving mutant: n-fold can drop the end-around carry into its first byte

## Goal

The defect the audit office reported as AF-0907 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0907 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33`

Location: `Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33`

Invoke-MutationTest -Library Curl.Kerberos.UnitLibrary -MaxMutants 25 -Seed 912464: 'for (int index = output.Length - 1; index >= 0; index--)' in the ones'-complement wrap-around loop mutated to 'index > 0' survived. The mutant never adds the carry into output[0], so any input whose carry reaches byte 0 folds to a wrong value. The RFC 3961 A.1 vectors and the derive-key vectors all pass with it. n-fold builds every DK key-derivation constant (AesSha1KerberosEncryption.cs:41, Des3CbcSha1KerberosEncryption.cs:104) and folds the whole password and salt in DES3 string-to-key (Des3CbcSha1KerberosEncryption.cs:145). A wrong fold gives a wrong key and a failed authentication.

Reproduction: the audit office's mutation test changed `Curl.Kerberos.UnitLibrary/KerberosNFold.cs line 33` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Kerberos.UnitLibrary/KerberosNFold.cs line 33`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `KerberosNFoldTests.Fold_CarryReachesFirstByte_WrapsCarryIntoFirstByte` (fold of FF FF to one byte must be FF; the mutant gives FE). Verified by hand: with `index > 0` it fails, reverted it passes. Test-only change; the library fast tests pass (782).

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 40d60ca7 9742157d.
- 2026-11-20: Doing -> Done. Test added that kills the mutant
