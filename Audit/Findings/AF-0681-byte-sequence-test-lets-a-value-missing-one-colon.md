---
id: AF-0681
title: Byte-sequence test lets a value missing one colon through: its rows fail only because the base64 inside is invalid
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs:Parse_AValueThatIsNotADictionaryOfByteSequences_IsIgnoredWhole:weak-assertion
reproduction: none
task: BL-4872
tasks: BL-4872
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0681 - Byte-sequence test lets a value missing one colon through: its rows fail only because the base64 inside is invalid

## Summary

Medium finding from the quality auditor at `Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs:39`: Byte-sequence test lets a value missing one colon through: its rows fail only because the base64 inside is invalid.

## Evidence

Location: `Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs:39`

The rows for a missing colon, "sha-256=AAAA", "sha-256=:AAAA" and "sha-256=AAAA:", each leave 3 characters (AAA) once the first and last character are cut, and that is invalid base64. They are refused whether or not ByteSequenceOf checks the colons. The ContentDigest mutation sample (seed 843793) shows it: DigestField.cs:58 'value.Length < 2 || value[0] != ':' || value[^1] != ':'' -> '... || value[0] != ':' && value[^1] != ':'' survived (score 0.9333). With that mutant, a field such as sha-256=:AAAAA (missing its closing colon, inner text AAAA valid base64) is accepted as a digest. That changes whether a response is failed with exit 56. The -Site form cannot target this second || (it mutates the first, which is killed), so the reproduction is the test's rows.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs -Pattern 'DataRow\("sha-256=:AAAA"\)|DataRow\("sha-256=AAAA:"\)'
```

- Expected: A row that misses one colon while the rest is valid base64 (for example sha-256=:AAAAA) is among the refused values.
- Actual: DigestFieldTests.cs:34: [DataRow("sha-256=:AAAA")] and :35: [DataRow("sha-256=AAAA:")], both refused by the base64 check alone.

## Re-audits

## Log

- 2026-11-05: filed proposed.
