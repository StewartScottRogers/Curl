---
id: BL-3356
title: Fix AF-0379: ReadFirst_KeyOf255Characters_IsRead asserts only that a challenge is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3356 — Fix AF-0379: ReadFirst_KeyOf255Characters_IsRead asserts only that a challenge is not null

## Goal

The defect the audit office reported as AF-0379 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0379 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0379-readfirst-keyof255characters-isread-asserts-only-t.md`.

Location: `Curl.Authentication.UnitTests/DigestChallengeTests.cs:161`

Location: `Curl.Authentication.UnitTests/DigestChallengeTests.cs:161`

The only assertion is Assert.IsNotNull(challenge) on DigestChallenge.ReadFirst(["Digest " + new string('k', 255) + "=v, nonce=n"]). A reader that skipped the long key wrongly and lost the nonce, or read garbage, would still pass. Asserting the nonce 'n' would catch it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/DigestChallengeTests.cs -Pattern 'ReadFirst_KeyOf255Characters_IsRead' -Context 0,10
```

- Expected: An assertion on the challenge's nonce or parameters.
- Actual: Assert.IsNotNull(challenge); is the only assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
