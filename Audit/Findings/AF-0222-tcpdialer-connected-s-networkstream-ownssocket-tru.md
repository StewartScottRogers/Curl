---
id: AF-0222
title: TcpDialer.Connected's NetworkStream ownsSocket: true can become false with no test failing, so a dialed socket is never closed on dispose
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-16_1623.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpDialer.cs:Connected-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpDialer.cs:228:true
task: BL-2275
tasks: BL-2275
found: 2026-10-15
found-at: 3e1062e68644127a82ba2f5ef33ceb1044cc6f67
scorecard: 2026-10-15_0156.md
duplicate-of:
closed: 2026-10-16
closed-how: mechanical
closed-by: 2026-10-16_1623.md
---
# AF-0222 - TcpDialer.Connected's NetworkStream ownsSocket: true can become false with no test failing, so a dialed socket is never closed on dispose

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpDialer.cs:228`: TcpDialer.Connected's NetworkStream ownsSocket: true can become false with no test failing, so a dialed socket is never closed on dispose.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:228`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 (score 0.8684, 32 killed, 1 timed out, 5 survived, 2 stillborn) reported the mutant survived: 'Stream stream = socket.Connected ? new NetworkStream(socket, ownsSocket: true) : new DeferredConnectSocketStream(socket);' -> 'ownsSocket: false'. A second run with the same seed gave the same outcome. Why it matters: StreamConnection.DisposeAsync (StreamConnection.cs:85) only disposes the stream, and DialedTcpConnection wraps that. With ownsSocket false, closing any dialed TCP connection leaves the socket open, so the peer sees no FIN until the process exits. That changes behaviour a server can see: a connection curl ends, or an upload whose end is signalled by closing the connection. The method is [ExcludeFromCodeCoverage] ('measured by the Integration run'), so the unit twin cannot reach it. Curl.Networking.IntegrationTests/TcpDialerIntegrationTests.cs has no test that checks the peer sees the close after DisposeAsync either (its tests check bytes carried, endpoints and a refused dial). Of the 5 survivors this is the only one kept: the other four are equivalent (PinnedPublicKey.cs:140 '>' -> '>=' in a branch reached only when lengths differ; EncryptedPrivateKeyInfoDecryption.cs:96 '&&' -> '||' where the BCL's Pbkdf2 refuses a non-positive count and the same CryptographicException is thrown either way; DnsSourceBinding.cs:31 and DnsServerResolver.cs:168 flip a compile-time-only [NotNullWhen] attribute).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpDialer.cs:228:true -Member Connected -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test fails when a dialed connection's dispose leaves its socket open.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with ownsSocket: false.

## Re-audits

- 2026-10-16 | 2026-10-16_1623.md | reproduces: no | Ran 'Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpDialer.cs:228:true -Member Connected -ExcludeBaselineFailures -TimeoutSeconds 600': resolvedLine 228, mutant 'new NetworkStream(socket, ownsSocket: false)', outcome killed (8328 ms). The baseline left out 3 failing ECH tests, which are unrelated to TcpDialer. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-15: filed proposed.
- 2026-10-15: proposed -> accepted.
- 2026-10-16: accepted -> closed. Re-audit 2026-10-16_1623.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
