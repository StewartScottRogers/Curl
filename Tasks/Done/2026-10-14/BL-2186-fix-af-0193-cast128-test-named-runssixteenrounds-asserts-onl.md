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
completed: 2026-10-13
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now pins the 16-round ciphertext 753DE29F5D167D03 with Assert.AreEqual and is renamed EncryptBlock_EightyBitKeyZeroPaddedTo128Bits_GivesTheSixteenRoundOpenSslCiphertext. The value comes from an independent implementation, not from Cast128 itself: OpenSSL 3.5.9, `openssl enc -cast5-ecb -provider legacy -provider default -nopad -K 01234567123456782345000000000000` on 0123456789ABCDEF. Cast128 already gave the same value, so no production change was needed.
- Build clean; fast tests green (Curl.Cryptography.UnitTests 1458 passed, 3 skipped).

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Cast128 test pins the OpenSSL 16-round ciphertext 753DE29F5D167D03 for the zero-padded 80-bit key
