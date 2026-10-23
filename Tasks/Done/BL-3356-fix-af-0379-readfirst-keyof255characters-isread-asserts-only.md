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
completed: 2026-10-23
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now asserts the nonce "n" read after the 255-character key (diagnostics too), so a reader that lost or garbled the nonce fails. Test name kept: it still says what it checks and the finding's reproduction greps for it. Build clean; fast tests green (Curl.Authentication.UnitTests 928 passed, 6 skipped; whole solution no failures).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. ReadFirst_KeyOf255Characters_IsRead asserts the nonce read after the long key
