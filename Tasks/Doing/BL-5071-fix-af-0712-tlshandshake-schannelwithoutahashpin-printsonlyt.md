---
id: BL-5071
title: Fix AF-0712: TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines checks only that two lines come back
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-5071 — Fix AF-0712: TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines checks only that two lines come back

## Goal

The defect the audit office reported as AF-0712 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0712 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0712-tlshandshake-schannelwithoutahashpin-printsonlythe.md`.

Location: `Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs:67`

Location: `Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs:67`

Step 1. The only assertion is Assert.HasCount(2, lines). The name promises the two lines are curl's ALPN lines, but any two lines (wrong ALPN text, a public-key-hash line in place of one) would pass. Diagnostics.Assert("line count", 2, ...) only writes a log line.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs -Pattern 'PrintsOnlyTheAlpnLines' -Context 0,9
```

- Expected: CollectionAssert.AreEqual of the two ALPN lines curl's Schannel build prints.
- Actual: Assert.HasCount(2, lines); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
