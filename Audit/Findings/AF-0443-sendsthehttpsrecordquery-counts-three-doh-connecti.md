---
id: AF-0443
title: SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:ConnectAsync_ToAnHttpsOriginThroughAShuffledDohResolver_SendsTheHttpsRecordQuery:weak-assertion
reproduction: none
task: BL-3520
tasks: BL-3520
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0443 - SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:42`: SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:42`

The test builds the HTTPS (type 65) query bytes 'httpsQuery' but uses them only to script the answer. Its only assertion is 'Assert.HasCount(3, dohConnector.Opened);'. A resolver that sent a third query of any other type, or sent the HTTPS query for the wrong name (not _<port>._https.<host>), would pass. The name promises the HTTPS-record query is sent; the bytes written to the DoH connections are never compared. Found reading Curl.Networking.UnitTests in step 1.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs -Pattern 'Assert\.' 
```

- Expected: An assertion that one DoH request body equals the HTTPS-record query bytes.
- Actual: TcpConnectorShuffledResolverTests.cs:42:        Assert.HasCount(3, dohConnector.Opened);

## Re-audits

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
