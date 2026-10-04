---
id: BL-1381
title: Fix AF-0038: VerifyPeer_..._StillRefuses asserts only Assert.IsNotNull(failure), not which refusal
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1381 — Fix AF-0038: VerifyPeer_..._StillRefuses asserts only Assert.IsNotNull(failure), not which refusal

## Goal

The defect the audit office reported as AF-0038 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0038 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0038-verifypeer-stillrefuses-asserts-only-assert-isnotn.md`.

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs:98`

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs:98`

Line 98 `Assert.IsNotNull(failure);` is the only assertion. Any failure message passes, so the exact curl error text the refusal should carry is not checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs -Pattern 'Assert\.IsNotNull\(failure\)'
```

- Expected: No match: the test asserts the refusal's text.
- Actual: SslStreamTlsProviderTests.RevocationBestEffort.cs:98:        Assert.IsNotNull(failure);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
