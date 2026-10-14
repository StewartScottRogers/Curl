---
id: AF-0193
title: Cast128 test named RunsSixteenRounds asserts only that the output is not the 12-round ciphertext
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Cryptography.UnitTests/Cast128Tests.cs:EncryptBlock_EightyBitKeyZeroPaddedTo128Bits_RunsSixteenRoundsAndDiffers:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0193 - Cast128 test named RunsSixteenRounds asserts only that the output is not the 12-round ciphertext

## Summary

Low finding from the quality auditor at `Curl.Cryptography.UnitTests/Cast128Tests.cs:68`: Cast128 test named RunsSixteenRounds asserts only that the output is not the 12-round ciphertext.

## Evidence

Location: `Curl.Cryptography.UnitTests/Cast128Tests.cs:68`

Step 1 sample. The test encrypts RFC 2144 B.1's plaintext under the 80-bit key zero-padded to 128 bits and its only assertion is Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted)) (line 68). The name promises sixteen rounds ran, but any output other than the 12-round ciphertext passes: all zeros, the plaintext, or a 14-round result. No expected 16-round ciphertext is pinned.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/Cast128Tests.cs -Pattern 'Assert\.AreNotEqual\("EB6A711A2C02271B"'
```

- Expected: No match: the test pins the 16-round ciphertext with Assert.AreEqual.
- Actual: Curl.Cryptography.UnitTests\Cast128Tests.cs:68:        Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted));

## Re-audits

## Log

- 2026-10-13: filed proposed.
