---
id: AF-0038
title: VerifyPeer_..._StillRefuses asserts only Assert.IsNotNull(failure), not which refusal
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs:VerifyPeer_WithNoChainAndRevocationCheckBestEffortInTheSchannelBuild_StillRefuses:weak-assertion
task: BL-1381
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0038 - VerifyPeer_..._StillRefuses asserts only Assert.IsNotNull(failure), not which refusal

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs:98`: VerifyPeer_..._StillRefuses asserts only Assert.IsNotNull(failure), not which refusal. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs:98`

Line 98 `Assert.IsNotNull(failure);` is the only assertion. Any failure message passes, so the exact curl error text the refusal should carry is not checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SslStreamTlsProviderTests.RevocationBestEffort.cs -Pattern 'Assert\.IsNotNull\(failure\)'
```

- Expected: No match: the test asserts the refusal's text.
- Actual: SslStreamTlsProviderTests.RevocationBestEffort.cs:98:        Assert.IsNotNull(failure);

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: Select-String finds no 'Assert.IsNotNull(failure)' in SslStreamTlsProviderTests.RevocationBestEffort.cs. The test now asserts Assert.AreEqual((CurlExitCode.PeerFailedVerification, UntrustedRootLine), failure).

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
