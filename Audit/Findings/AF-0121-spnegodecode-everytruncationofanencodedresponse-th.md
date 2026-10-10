---
id: AF-0121
title: SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException passes when a truncation decodes without throwing
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
key: quality:Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs:SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException:weak-assertion
reproduction: none
task: BL-1871
tasks: BL-1871
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0647.md, 2026-10-09_1435.md
---
# AF-0121 - SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException passes when a truncation decodes without throwing

## Summary

Low finding from the quality auditor at `Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs:259`: SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException passes when a truncation decodes without throwing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs:259`

The body loops over every prefix of a 13-byte token and does 'try { _ = SpnegoNegotiationResponse.Decode(whole.AsMemory(0, length)); } catch (SpnegoTokenException) { }', with no assertion. A decoder that silently accepted a truncated token and returned a response would pass, although the name says every truncation throws. Only an exception of another type fails it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs -Pattern 'SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException' -Context 0,15
```

- Expected: The loop asserts each truncation throws (Assert.ThrowsExactly<SpnegoTokenException>).
- Actual: A try/catch (SpnegoTokenException) with an empty handler and no Assert in the method.

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Select-String shows AuthenticationAdversarialTests.cs:259-274 unchanged: a loop over truncations whose try/catch (SpnegoTokenException) has no Assert.Fail after Decode, so a truncation that decodes without throwing still passes.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the Select-String reproduction: for every truncation length the loop calls Assert.ThrowsExactly<SpnegoTokenException>(() => SpnegoNegotiationResponse.Decode(truncated), ...), so a truncation that decodes without throwing fails the test (AuthenticationAdversarialTests.cs:263-268).
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Read the test: every truncation runs Assert.ThrowsExactly<SpnegoTokenException>(() => SpnegoNegotiationResponse.Decode(truncated), ...) inside the loop, so a truncation that decodes without throwing now fails the test.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
