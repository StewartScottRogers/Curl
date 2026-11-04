---
id: BL-4790
title: Fix AF-0617: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4790 — Fix AF-0617: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing

## Goal

The defect the audit office reported as AF-0617 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0617 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0617-surviving-mutant-ntlm-s-headerof-can-send-a-header.md`.

Location: `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`

Location: `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`

Mutant 'step.Status is ContinueNeeded or Completed && step.Token.Length != 0' -> '... || step.Token.Length != 0' survived. With it, a ContinueNeeded/Completed step with an empty token sends 'NTLM ' with an empty value, and a Refused/MalformedToken step with a token sends that token instead of nothing. Either way the Authorization request bytes change; no test pins an empty-token step.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183:&& -Member HeaderOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `CreateAuthorizationAsync_Type1StepWithoutAHeaderableToken_SendsNothing` to `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs` (the library's own test project, added to touches): ContinueNeeded and Completed steps with an empty token, and Refused and MalformedToken steps carrying a token, all send no Authorization. No production change.
- Mutant checked by hand, since a lane cannot run `Audit/Tools`: with line 183's `&&` changed to `||`, all four rows failed; with the original restored, `Curl.Authentication.UnitTests` passed (998 passed, 6 skipped as off-Windows, 0 failed). Platform-neutral: no paths, OS text or certificates. No quality run was needed because no production code changed.
- Resumed run: `dotnet build` clean and the solution's fast tests green (no failures) before the move to Done.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A test pins that a Type 1 step with an empty token, or a refused or malformed step, sends no NTLM Authorization, killing the AF-0617 mutant
