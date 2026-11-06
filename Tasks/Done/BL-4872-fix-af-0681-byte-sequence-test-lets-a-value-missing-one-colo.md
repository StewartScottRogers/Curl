---
id: BL-4872
title: Fix AF-0681: Byte-sequence test lets a value missing one colon through: its rows fail only because the base64 inside is invalid
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ContentDigest.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-4872 — Fix AF-0681: Byte-sequence test lets a value missing one colon through: its rows fail only because the base64 inside is invalid

## Goal

The defect the audit office reported as AF-0681 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0681 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0681-byte-sequence-test-lets-a-value-missing-one-colon.md`.

Location: `Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs:39`

Location: `Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs:39`

The rows for a missing colon, "sha-256=AAAA", "sha-256=:AAAA" and "sha-256=AAAA:", each leave 3 characters (AAA) once the first and last character are cut, and that is invalid base64. They are refused whether or not ByteSequenceOf checks the colons. The ContentDigest mutation sample (seed 843793) shows it: DigestField.cs:58 'value.Length < 2 || value[0] != ':' || value[^1] != ':'' -> '... || value[0] != ':' && value[^1] != ':'' survived (score 0.9333). With that mutant, a field such as sha-256=:AAAAA (missing its closing colon, inner text AAAA valid base64) is accepted as a digest. That changes whether a response is failed with exit 56. The -Site form cannot target this second || (it mutates the first, which is killed), so the reproduction is the test's rows.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ContentDigest.UnitTests/DigestFieldTests.cs -Pattern 'DataRow\("sha-256=:AAAA"\)|DataRow\("sha-256=AAAA:"\)'
```

- Expected: A row that misses one colon while the rest is valid base64 (for example sha-256=:AAAAA) is among the refused values.
- Actual: DigestFieldTests.cs:34: [DataRow("sha-256=:AAAA")] and :35: [DataRow("sha-256=AAAA:")], both refused by the base64 check alone.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The three missing-colon rows now carry valid base64 between the cut characters (sha-256=AAAAAA, sha-256=:AAAAA, sha-256=AAAAA:), so only the colon check refuses them and the surviving mutant (|| -> && on DigestField.cs:58) now fails the test. Test-only change; no production code, so no quality run. The finding's Select-String now matches nothing. Full build clean and fast tests green on Windows; the change is platform-neutral.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Done. Missing-colon rows carry valid base64, so only the colon check refuses them
