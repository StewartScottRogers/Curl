---
id: AF-0431
title: CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator never checks the forced random bytes are used
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-25_1542.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_0941.md, 2026-10-25_1542.md).
key: quality:Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs:CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator:weak-assertion
reproduction: none
task: BL-3497
tasks: BL-3497
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_0941.md, 2026-10-25_1542.md
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

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the Select-String reproduction: the pattern 'ForcedRandomBytes_BuildsTheAuthenticator' no longer matches. The test is now CreateHttpAuthenticator_ForcedRandomBytes_DigestResponseCarriesTheirCnonce (EngineCompositionDigestClientNonceTests.cs:35). It builds two authenticators with forced zero and 0xFF random bytes, then asserts Assert.AreNotEqual(zerosCnonce, onesCnonce) and Assert.AreEqual("AAAAAAAAAAAAAAAA", zerosCnonce), so the forced bytes are shown to reach the Digest cnonce.
- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the Select-String reproduction: no CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator matches. Its replacement, CreateHttpAuthenticator_ForcedRandomBytes_DigestResponseCarriesTheirCnonce, asserts the zero-bytes and 0xFF-bytes cnonces differ and that the zero one is "AAAAAAAAAAAAAAAA", so the forced random bytes are checked.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1542.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_0941.md, 2026-10-25_1542.md).
