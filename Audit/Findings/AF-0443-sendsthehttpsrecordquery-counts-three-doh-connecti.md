---
id: AF-0443
title: SendsTheHttpsRecordQuery counts three DoH connections but never checks that an HTTPS-record query was sent
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
key: quality:Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs:ConnectAsync_ToAnHttpsOriginThroughAShuffledDohResolver_SendsTheHttpsRecordQuery:weak-assertion
reproduction: none
task: BL-3520
tasks: BL-3520
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_1542.md, 2026-10-25_1944.md
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

- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the Select-String reproduction. Line 43 now asserts CollectionAssert.AreEqual(DohPost(httpsQuery), dohConnector.Opened[0].Written.ToArray()), so the HTTPS-record query bytes are checked as well as the count of three.
- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | TcpConnectorShuffledResolverTests.cs:43: CollectionAssert.AreEqual(DohPost(httpsQuery), dohConnector.Opened[0].Written.ToArray()) checks that the HTTPS-record query bytes are sent, beside the count of 3 at line 42.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
