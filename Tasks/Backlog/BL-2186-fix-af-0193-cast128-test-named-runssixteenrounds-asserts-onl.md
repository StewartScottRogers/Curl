---
id: BL-2186
title: Fix AF-0193: Cast128 test named RunsSixteenRounds asserts only that the output is not the 12-round ciphertext
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2186 — Fix AF-0193: Cast128 test named RunsSixteenRounds asserts only that the output is not the 12-round ciphertext

## Goal

The defect the audit office reported as AF-0193 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0193 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0193-cast128-test-named-runssixteenrounds-asserts-only.md`.

Location: `Curl.Cryptography.UnitTests/Cast128Tests.cs:68`

Location: `Curl.Cryptography.UnitTests/Cast128Tests.cs:68`

Step 1 sample. The test encrypts RFC 2144 B.1's plaintext under the 80-bit key zero-padded to 128 bits and its only assertion is Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted)) (line 68). The name promises sixteen rounds ran, but any output other than the 12-round ciphertext passes: all zeros, the plaintext, or a 14-round result. No expected 16-round ciphertext is pinned.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/Cast128Tests.cs -Pattern 'Assert\.AreNotEqual\("EB6A711A2C02271B"'
```

- Expected: No match: the test pins the 16-round ciphertext with Assert.AreEqual.
- Actual: Curl.Cryptography.UnitTests\Cast128Tests.cs:68:        Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
