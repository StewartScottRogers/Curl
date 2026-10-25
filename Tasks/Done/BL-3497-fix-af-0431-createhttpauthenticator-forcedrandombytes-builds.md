---
id: BL-3497
title: Fix AF-0431: CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3497 — Fix AF-0431: CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used

## Goal

The defect the audit office reported as AF-0431 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0431 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0431-createhttpauthenticator-forcedrandombytes-buildsth.md`.

Location: `Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:43`

Location: `Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:43`

Body: RankedHttpAuthenticator authenticator = EngineComposition.CreateHttpAuthenticator(contexts, digestRandomBytes: count => new byte[count]); diagnostics.Assert(...); Assert.IsNotNull(authenticator);. diagnostics.Assert only logs (TestDiagnostics.cs); the only real assertion is IsNotNull on a constructor result, so an authenticator that ignored digestRandomBytes would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs -Pattern 'ForcedRandomBytes_BuildsTheAuthenticator' -Context 0,9
```

- Expected: An assertion that a Digest response built by the authenticator carries the cnonce from the forced bytes.
- Actual: Line 43: Assert.IsNotNull(authenticator); is the only assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. forced-bytes test now asserts the Digest cnonce comes from them
