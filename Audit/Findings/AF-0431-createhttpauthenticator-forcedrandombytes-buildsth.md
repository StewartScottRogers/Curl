---
id: AF-0431
title: CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator:weak-assertion
reproduction: none
task: BL-3497
tasks: BL-3497
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0431 - CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:43`: CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used.

## Evidence

Location: `Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:43`

Body: RankedHttpAuthenticator authenticator = EngineComposition.CreateHttpAuthenticator(contexts, digestRandomBytes: count => new byte[count]); diagnostics.Assert(...); Assert.IsNotNull(authenticator);. diagnostics.Assert only logs (TestDiagnostics.cs); the only real assertion is IsNotNull on a constructor result, so an authenticator that ignored digestRandomBytes would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs -Pattern 'ForcedRandomBytes_BuildsTheAuthenticator' -Context 0,9
```

- Expected: An assertion that a Digest response built by the authenticator carries the cnonce from the forced bytes.
- Actual: Line 43: Assert.IsNotNull(authenticator); is the only assertion.

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
