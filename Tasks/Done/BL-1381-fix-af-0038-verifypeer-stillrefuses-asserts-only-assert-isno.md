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
completed: 2026-10-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now pins the whole refusal: `(CurlExitCode.PeerFailedVerification, UntrustedRootLine)`, the Schannel build's SEC_E_UNTRUSTED_ROOT line that `SchannelPeerFailedVerification` returns without `--cacert`. No production change; feature pipeline stages beyond the test were not needed for a one-assertion test fix.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. VerifyPeer best-effort test asserts exit 60 and the exact SEC_E_UNTRUSTED_ROOT text (AF-0038)
