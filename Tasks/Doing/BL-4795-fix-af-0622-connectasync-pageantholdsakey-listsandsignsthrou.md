---
id: BL-4795
title: Fix AF-0622: ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping checks the signature it names only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Ssh.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4795 — Fix AF-0622: ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping checks the signature it names only for null

## Goal

The defect the audit office reported as AF-0622 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0622 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0622-connectasync-pageantholdsakey-listsandsignsthrough.md`.

Location: `Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs:52`

Location: `Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs:52`

client.SignAsync(identities![0].Blob, [1, 2, 3], 0, ...) is checked only by Assert.IsNotNull(signature). The other assertions check the identity comment, window.Exchanges == 2 and the first request byte (11). Neither the sign request sent through the mapping nor the signature's bytes are compared with what InMemorySshAgent produces, so a connector that mangled either would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Ssh.UnitTests/Authentication/PageantSshAgentConnectorTests.cs -Pattern 'Assert.IsNotNull\(signature\)'
```

- Expected: No match: the signature is compared with the agent's signature of [1, 2, 3].
- Actual: PageantSshAgentConnectorTests.cs:52: Assert.IsNotNull(signature);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
