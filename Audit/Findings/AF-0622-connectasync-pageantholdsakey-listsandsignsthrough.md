---
id: AF-0622
title: ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping checks the signature it names only for null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs:ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping:weak-assertion
reproduction: none
task: BL-4795
tasks: BL-4795
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0622 - ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping checks the signature it names only for null

## Summary

Low finding from the quality auditor at `Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs:52`: ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping checks the signature it names only for null. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs:52`

client.SignAsync(identities![0].Blob, [1, 2, 3], 0, ...) is checked only by Assert.IsNotNull(signature). The other assertions check the identity comment, window.Exchanges == 2 and the first request byte (11). Neither the sign request sent through the mapping nor the signature's bytes are compared with what InMemorySshAgent produces, so a connector that mangled either would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs -Pattern 'Assert.IsNotNull\(signature\)'
```

- Expected: No match: the signature is compared with the agent's signature of [1, 2, 3].
- Actual: PageantSshAgentConnectorTests.cs:52: Assert.IsNotNull(signature);

## Re-audits

- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran the reproduction: no match for Assert.IsNotNull(signature). Line 61 asserts CollectionAssert.AreEqual(expectedSignatureBlob, signatureBlob).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
