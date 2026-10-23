---
id: AF-0379
title: ReadFirst_KeyOf255Characters_IsRead asserts only that a challenge is not null
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Authentication.UnitTests/DigestChallengeTests.cs:ReadFirst_KeyOf255Characters_IsRead:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0379 - ReadFirst_KeyOf255Characters_IsRead asserts only that a challenge is not null

## Summary

Low finding from the quality auditor at `Curl.Authentication.UnitTests/DigestChallengeTests.cs:161`: ReadFirst_KeyOf255Characters_IsRead asserts only that a challenge is not null.

## Evidence

Location: `Curl.Authentication.UnitTests/DigestChallengeTests.cs:161`

The only assertion is Assert.IsNotNull(challenge) on DigestChallenge.ReadFirst(["Digest " + new string('k', 255) + "=v, nonce=n"]). A reader that skipped the long key wrongly and lost the nonce, or read garbage, would still pass. Asserting the nonce 'n' would catch it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/DigestChallengeTests.cs -Pattern 'ReadFirst_KeyOf255Characters_IsRead' -Context 0,10
```

- Expected: An assertion on the challenge's nonce or parameters.
- Actual: Assert.IsNotNull(challenge); is the only assertion.

## Re-audits

## Log

- 2026-10-23: filed proposed.
