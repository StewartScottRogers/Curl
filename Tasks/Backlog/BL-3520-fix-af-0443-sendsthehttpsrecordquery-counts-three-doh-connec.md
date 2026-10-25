---
id: BL-3520
title: Fix AF-0443: SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3520 — Fix AF-0443: SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent

## Goal

The defect the audit office reported as AF-0443 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0443 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0443-sendsthehttpsrecordquery-counts-three-doh-connecti.md`.

Location: `Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:42`

Location: `Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:42`

The test builds the HTTPS (type 65) query bytes 'httpsQuery' but uses them only to script the answer. Its only assertion is 'Assert.HasCount(3, dohConnector.Opened);'. A resolver that sent a third query of any other type, or sent the HTTPS query for the wrong name (not _<port>._https.<host>), would pass. The name promises the HTTPS-record query is sent; the bytes written to the DoH connections are never compared. Found reading Curl.Networking.UnitTests in step 1.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs -Pattern 'Assert\.' 
```

- Expected: An assertion that one DoH request body equals the HTTPS-record query bytes.
- Actual: TcpConnectorShuffledResolverTests.cs:42:        Assert.HasCount(3, dohConnector.Opened);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
