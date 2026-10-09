---
id: BL-1871
title: Fix AF-0121: SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException passes when a truncation decodes without throwing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1871 — Fix AF-0121: SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException passes when a truncation decodes without throwing

## Goal

The defect the audit office reported as AF-0121 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0121 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0121-spnegodecode-everytruncationofanencodedresponse-th.md`.

Location: `Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs:259`

Location: `Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs:259`

The body loops over every prefix of a 13-byte token and does 'try { _ = SpnegoNegotiationResponse.Decode(whole.AsMemory(0, length)); } catch (SpnegoTokenException) { }', with no assertion. A decoder that silently accepted a truncated token and returned a response would pass, although the name says every truncation throws. Only an exception of another type fails it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs -Pattern 'SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException' -Context 0,15
```

- Expected: The loop asserts each truncation throws (Assert.ThrowsExactly<SpnegoTokenException>).
- Actual: A try/catch (SpnegoTokenException) with an empty handler and no Assert in the method.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
